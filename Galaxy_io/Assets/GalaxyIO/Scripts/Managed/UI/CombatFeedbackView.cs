using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 로컬 플레이어 우주선의 전투 상태를 HUD 에 알리는 클라이언트 전용 뷰.
    /// <list type="bullet">
    /// <item>체력 숫자(현재 / 최대). 낮으면 경고 색.</item>
    /// <item>수리까지 남은 시간. 맞으면 수리 지연(RepairDelay)부터 다시 셈. 다 차면 수리 중 맥동.</item>
    /// <item>주포 재장전(발사 간격이 긴 포탑만). 다 차는 순간 한 번 번쩍이고 효과음.</item>
    /// <item>자동 포탑 칸별 상태(사격 중 / 쉬는 중 / 준비).</item>
    /// <item>과열 임박 경고.</item>
    /// </list>
    /// 수리 대기 시간과 자동 포탑 휴식은 복제하지 않는 값이라 클라이언트에서 추정함.
    /// </summary>
    public sealed class CombatFeedbackView : MonoBehaviour
    {
        [Header("체력")]

        [SerializeField]
        [Tooltip("\"현재 / 최대\" 체력 글자")]
        private TMP_Text healthLabel;

        [SerializeField]
        [Tooltip("체력 비율(0 ~ 1)별 글자 색")]
        private Gradient healthColor = new Gradient();

        [Header("수리")]

        [SerializeField]
        [Tooltip("수리까지 남은 시간을 채우는 원형 게이지(Filled, Radial360). 수리 중이면 맥동함.")]
        private Image repairRing;

        [SerializeField]
        [Tooltip("남은 시간(초) 글자. 수리 중이면 비움.")]
        private TMP_Text repairLabel;

        [SerializeField]
        [Tooltip("수리를 기다리는 동안의 색")]
        private Color repairWaitColor = new Color(0.55F, 0.65F, 0.75F, 0.8F);

        [SerializeField]
        [Tooltip("수리 중 색. 맥동함.")]
        private Color repairActiveColor = new Color(0.3F, 1.0F, 0.55F, 1.0F);

        [Header("주포 재장전")]

        [SerializeField]
        [Tooltip("주포 재장전 원형 게이지(Filled, Radial360). 발사 간격이 짧은 포탑은 숨김.")]
        private Image reloadRing;

        [SerializeField]
        [Tooltip("이 간격(초) 이상인 포탑만 재장전을 보여 줌. 연사 포탑은 HEAT 막대로 충분함.")]
        [Min(0.0F)]
        private float reloadMinInterval = 0.5F;

        [SerializeField]
        [Tooltip("장전 중 색")]
        private Color reloadingColor = new Color(0.55F, 0.65F, 0.75F, 0.8F);

        [SerializeField]
        [Tooltip("장전 완료 색. 다 차는 순간 HDR 섬광 뒤 이 색으로 돌아옴.")]
        private Color readyColor = new Color(0.25F, 0.91F, 1.0F, 1.0F);

        [SerializeField]
        [Tooltip("장전 완료 섬광 색(HDR 이면 블룸)")]
        [ColorUsage(true, true)]
        private Color readyFlashColor = new Color(2.0F, 2.0F, 2.0F, 1.0F);

        [SerializeField]
        [Tooltip("장전 완료 섬광 시간(초)")]
        [Min(0.01F)]
        private float readyFlashDuration = 0.25F;

        [SerializeField]
        [Tooltip("장전 완료 때 한 번 재생. 비우면 소리 없음.")]
        private string reloadReadySound = "UI_Press";

        [Header("자동 포탑")]

        [SerializeField]
        [Tooltip("자동 포탑 칸 표시를 늘어놓을 부모")]
        private RectTransform autoTurretRow;

        [SerializeField]
        [Tooltip("자동 포탑 칸 하나(원형 Image, Filled). 칸 수만큼 복제함.")]
        private Image autoTurretPipTemplate;

        [SerializeField]
        [Tooltip("사격 중 색")]
        private Color autoFiringColor = new Color(1.0F, 0.75F, 0.3F, 1.0F);

        [Header("과열 임박")]

        [SerializeField]
        [Tooltip("과열 임박 경고 글자. 과열 중에는 HEAT 막대가 알리므로 숨김.")]
        private TMP_Text overheatWarning;

        [SerializeField]
        [Tooltip("이 열 비율 이상이면 경고")]
        [Range(0.0F, 1.0F)]
        private float overheatWarningRatio = 0.8F;

        [SerializeField]
        [Tooltip("경고 초당 깜빡임 수")]
        [Min(0.0F)]
        private float warningBlinkFrequency = 3.0F;

        private EntityQuery shipQuery;
        private EntityQuery registryQuery;
        private World clientWorld;

        // 수리 추정: 지난 체력, 마지막으로 맞은 시각.
        private float lastHealth = -1.0F;
        private float lastHitTime = -1000.0F;

        // 주포: 직전 프레임에 장전 중이었는지, 섬광 남은 시간.
        private bool reloading;
        private float flashRemaining;

        // 자동 포탑 칸별: 직전 사격 여부, 사격이 끝난 시각.
        private readonly List<Image> pips = new List<Image>();
        private readonly List<bool> pipFiring = new List<bool>();
        private readonly List<float> pipRestStart = new List<float>();

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            shipQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>()
            );
            registryQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryElement>());

            if (autoTurretPipTemplate != null)
            {
                autoTurretPipTemplate.gameObject.SetActive(false);
            }

            SetVisible(false);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                SetVisible(false);
                enabled = false;
                return;
            }

            if (shipQuery.IsEmpty)
            {
                SetVisible(false);
                lastHealth = -1.0F;
                return;
            }

            SetVisible(true);

            EntityManager entityManager = clientWorld.EntityManager;
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);
            Entity ship = ships[0];
            SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(ship);

            UpdateHealth(spec);
            UpdateRepair(spec);

            if (!registryQuery.IsEmpty)
            {
                DynamicBuffer<ItemRegistryElement> registry = registryQuery.GetSingletonBuffer<ItemRegistryElement>(true);
                UpdateReload(entityManager, ship, registry);
                UpdateAutoTurrets(entityManager, ship, registry);
            }

            UpdateOverheatWarning(entityManager, ship);
        }

        private void UpdateHealth(in SpaceshipSpecComponent spec)
        {
            if (healthLabel == null)
            {
                return;
            }

            float ratio = spec.MaxHealth > 0.0F ? Mathf.Clamp01(spec.Health / spec.MaxHealth) : 0.0F;
            healthLabel.text = $"{Mathf.CeilToInt(spec.Health)} / {Mathf.CeilToInt(spec.MaxHealth)}";
            healthLabel.color = healthColor.Evaluate(ratio);
        }

        private void UpdateRepair(in SpaceshipSpecComponent spec)
        {
            // 체력이 줄었으면 맞은 것. 수리 지연을 처음부터 셈.
            if (lastHealth >= 0.0F && spec.Health < lastHealth - 0.01F)
            {
                lastHitTime = Time.time;
            }

            lastHealth = spec.Health;

            if (repairRing == null)
            {
                return;
            }

            bool full = spec.Health >= spec.MaxHealth - 0.01F;
            repairRing.gameObject.SetActive(!full && spec.Repair > 0.0F);

            if (full || spec.Repair <= 0.0F)
            {
                return;
            }

            float waited = Time.time - lastHitTime;
            float delay = Mathf.Max(spec.RepairDelay, 0.01F);

            if (waited < delay)
            {
                repairRing.fillAmount = waited / delay;
                repairRing.color = repairWaitColor;

                if (repairLabel != null)
                {
                    repairLabel.text = (delay - waited).ToString("0.0");
                }
            }
            else
            {
                // 수리 중: 꽉 찬 고리가 맥동함.
                float pulse = 0.65F + 0.35F * Mathf.Sin(Time.time * 6.0F);
                repairRing.fillAmount = 1.0F;
                repairRing.color = new Color(repairActiveColor.r, repairActiveColor.g, repairActiveColor.b, repairActiveColor.a * pulse);

                if (repairLabel != null)
                {
                    repairLabel.text = string.Empty;
                }
            }
        }

        private void UpdateReload(EntityManager entityManager, Entity ship, DynamicBuffer<ItemRegistryElement> registry)
        {
            if (reloadRing == null)
            {
                return;
            }

            float interval = 0.0F;
            float remaining = 0.0F;

            if (entityManager.HasComponent<EquippedTurretComponent>(ship) && entityManager.HasBuffer<TurretCooldownElement>(ship))
            {
                ItemRegistryElement turret = registry.Get(entityManager.GetComponentData<EquippedTurretComponent>(ship).Value);
                interval = turret.Turret.MaxFireCooldown;

                // 재장전 강화만큼 간격이 줄어듦(PlayerTurretFireSystem 과 같은 식).
                if (entityManager.HasBuffer<UpgradeParamElement>(ship) && entityManager.HasBuffer<UpgradeLevelElement>(ship))
                {
                    float bonus = Upgrade.Value(
                        entityManager.GetBuffer<UpgradeParamElement>(ship, true),
                        entityManager.GetBuffer<UpgradeLevelElement>(ship, true),
                        UpgradeParam.ReloadTime
                    );

                    if (interval > 0.0F && bonus > 0.0F)
                    {
                        interval = Mathf.Max(interval - bonus, Mathf.Min(interval, Time.fixedDeltaTime));
                    }
                }

                // 다음에 쏠 수 있는 총구 기준
                DynamicBuffer<TurretCooldownElement> cooldowns = entityManager.GetBuffer<TurretCooldownElement>(ship, true);
                remaining = float.MaxValue;

                for (int i = 0; i < cooldowns.Length; i++)
                {
                    remaining = Mathf.Min(remaining, cooldowns[i].Value);
                }

                remaining = cooldowns.Length > 0 ? Mathf.Max(remaining, 0.0F) : 0.0F;
            }

            bool shown = interval >= reloadMinInterval;
            reloadRing.gameObject.SetActive(shown);

            if (!shown)
            {
                reloading = false;
                return;
            }

            bool nowReloading = remaining > 0.0001F;

            // 장전이 끝나는 순간: 섬광 + 효과음 한 번.
            if (reloading && !nowReloading)
            {
                flashRemaining = readyFlashDuration;

                if (!string.IsNullOrEmpty(reloadReadySound) && SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySfx(reloadReadySound);
                }
            }

            reloading = nowReloading;
            reloadRing.fillAmount = nowReloading ? 1.0F - Mathf.Clamp01(remaining / interval) : 1.0F;

            if (flashRemaining > 0.0F)
            {
                flashRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(flashRemaining / readyFlashDuration);
                Color flash = Color.LerpUnclamped(readyColor, readyFlashColor, t);
                float intensity = Mathf.Max(1.0F, flash.maxColorComponent);
                reloadRing.color = new Color(flash.r / intensity, flash.g / intensity, flash.b / intensity, flash.a);
                reloadRing.rectTransform.localScale = Vector3.one * (1.0F + 0.25F * t);
            }
            else
            {
                reloadRing.color = nowReloading ? reloadingColor : readyColor;
                reloadRing.rectTransform.localScale = Vector3.one;
            }
        }

        private void UpdateAutoTurrets(EntityManager entityManager, Entity ship, DynamicBuffer<ItemRegistryElement> registry)
        {
            if (autoTurretRow == null || autoTurretPipTemplate == null)
            {
                return;
            }

            int count = entityManager.HasBuffer<AutoTurretElement>(ship) ? entityManager.GetBuffer<AutoTurretElement>(ship, true).Length : 0;

            while (pips.Count < count)
            {
                Image pip = Instantiate(autoTurretPipTemplate, autoTurretRow);
                pip.gameObject.SetActive(true);
                pips.Add(pip);
                pipFiring.Add(false);
                pipRestStart.Add(-1000.0F);
            }

            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].gameObject.SetActive(i < count);
            }

            if (count == 0)
            {
                return;
            }

            DynamicBuffer<AutoTurretElement> autos = entityManager.GetBuffer<AutoTurretElement>(ship, true);

            for (int i = 0; i < count; i++)
            {
                AutoTurretElement auto = autos[i];
                Image pip = pips[i];

                if (auto.Turret == ItemId.None)
                {
                    pip.fillAmount = 0.0F;
                    continue;
                }

                if (pipFiring[i] && !auto.Firing)
                {
                    pipRestStart[i] = Time.time;
                }

                pipFiring[i] = auto.Firing;

                if (auto.Firing)
                {
                    pip.fillAmount = 1.0F;
                    pip.color = autoFiringColor;
                    continue;
                }

                // 사격이 끝난 뒤 최소 휴식 시간만큼 채워지면 준비.
                float rest = Mathf.Max(registry.Get(auto.Turret).AutoTurret.RestDurationRange.x, 0.01F);
                float t = Mathf.Clamp01((Time.time - pipRestStart[i]) / rest);
                pip.fillAmount = t;
                pip.color = t >= 1.0F ? readyColor : reloadingColor;
            }
        }

        private void UpdateOverheatWarning(EntityManager entityManager, Entity ship)
        {
            if (overheatWarning == null)
            {
                return;
            }

            bool warn = false;

            if (entityManager.HasComponent<TurretHeatComponent>(ship))
            {
                TurretHeatComponent heat = entityManager.GetComponentData<TurretHeatComponent>(ship);
                warn = !heat.Overheated && heat.MaxHeat > 0.0F && heat.Heat / heat.MaxHeat >= overheatWarningRatio;
            }

            overheatWarning.gameObject.SetActive(warn);

            if (warn)
            {
                Color color = overheatWarning.color;
                color.a = Mathf.Sin(Time.time * warningBlinkFrequency * 2.0F * Mathf.PI) > 0.0F ? 1.0F : 0.35F;
                overheatWarning.color = color;
            }
        }

        private void SetVisible(bool visible)
        {
            if (healthLabel != null) healthLabel.gameObject.SetActive(visible);
            if (!visible && repairRing != null) repairRing.gameObject.SetActive(false);
            if (!visible && reloadRing != null) reloadRing.gameObject.SetActive(false);
            if (!visible && overheatWarning != null) overheatWarning.gameObject.SetActive(false);
            if (autoTurretRow != null) autoTurretRow.gameObject.SetActive(visible);
        }
    }
}
