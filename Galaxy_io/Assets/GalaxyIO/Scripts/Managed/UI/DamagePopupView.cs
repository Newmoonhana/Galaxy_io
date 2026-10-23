using GalaxyIO.Camera;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 맞은 우주선 위에 데미지 숫자를, 로컬 우주선 위에 얻은 점수를 띄우는 클라이언트 전용 HUD 뷰.
    /// 숫자는 우주선을 따라 움직이고, 위로 떠오르며 점점 느려지고 커짐.
    /// 때린 플레이어에게는 명중음도 냄. 거리만큼 작아지지만 최소 음량이 있어 화면 밖 명중도 들림.
    /// 로컬 우주선이 맞으면 받은 데미지 / 최대 체력에 비례해 카메라를 흔듦.
    /// </summary>
    public sealed class DamagePopupView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("숫자 프리팹. 가운데 정렬.")]
        private TMP_Text popupPrefab;

        [Header("Animation")]

        [SerializeField]
        [Tooltip("숫자가 떠 있는 시간(초)")]
        [Min(0.1F)]
        private float lifetime = 0.9F;

        [SerializeField]
        [Tooltip("다 떠올랐을 때의 높이(캔버스 단위)")]
        private float riseHeight = 28.0F;

        [SerializeField]
        [Tooltip("떠오르는 속도가 줄어드는 빠르기(초). 작을수록 빨리 멈춤.")]
        [Min(0.01F)]
        private float riseTime = 0.2F;

        [SerializeField]
        [Tooltip("처음 크기 비율. 로그 곡선으로 1 까지 커짐.")]
        [Range(0.0F, 1.0F)]
        private float startScale = 0.5F;

        [SerializeField]
        [Tooltip("끝에서 사라지는 시간(초)")]
        [Min(0.01F)]
        private float fadeTime = 0.25F;

        [SerializeField]
        [Tooltip("명중 지점에서 흩어지는 반경(캔버스 단위)")]
        [Min(0.0F)]
        private float noise = 10.0F;

        [Header("Size")]

        [SerializeField]
        [Tooltip("가장 작은 숫자의 글자 크기(캔버스 단위)")]
        private float minFontSize = 10.0F;

        [SerializeField]
        [Tooltip("가장 큰 숫자의 글자 크기(캔버스 단위)")]
        private float maxFontSize = 24.0F;

        [SerializeField]
        [Tooltip("이 데미지부터 가장 큰 글자")]
        [Min(1.0F)]
        private float maxSizeDamage = 1000.0F;

        [SerializeField]
        [Tooltip("이 점수부터 가장 큰 글자")]
        [Min(1.0F)]
        private float maxSizeScore = 20.0F;

        [Header("Colors")]

        [SerializeField]
        [Tooltip("내가 입힌 데미지")]
        private Color dealtColor = new Color(1.0F, 0.85F, 0.3F);

        [SerializeField]
        [Tooltip("내가 받은 데미지")]
        private Color takenColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        [Tooltip("얻은 점수")]
        private Color scoreColor = new Color(0.35F, 1.0F, 0.5F);

        [Header("Hit Sound")]

        [SerializeField]
        [Tooltip("때린 플레이어의 명중음 음량 배율")]
        [Min(0.0F)]
        private float attackerVolume = 1.5F;

        [SerializeField]
        [Tooltip("때린 플레이어의 명중음을 리스너에서 이 거리 안으로 끌어와 재생함. 소리 그룹의 최소 거리보다 작게.")]
        [Min(0.0F)]
        private float attackerSoundRadius = 4.0F;

        [SerializeField]
        [Tooltip("때린 플레이어의 명중음 거리 감쇠. (이 거리까지 최대 음량, 이 거리에서 최소 음량)")]
        private Vector2 attackerFalloff = new Vector2(5.0F, 35.0F);

        [SerializeField]
        [Tooltip("때린 플레이어의 명중음 최소 음량 비율. 아무리 멀어도 이만큼은 들림.")]
        [Range(0.0F, 1.0F)]
        private float attackerMinVolume = 0.25F;

        private sealed class Popup
        {
            public TMP_Text Text;

            // 따라갈 우주선. 사라지면 마지막 위치에 머묾.
            public Entity Ship;
            public Vector2 Offset;
            public Vector2 Anchor;

            public Vector2 Noise;
            public float FontSize;
            public float Age;
        }

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery attackerSoundQuery;
        private EntityQuery connectionQuery;
        private EntityQuery localShipQuery;
        private EntityQuery registryQuery;

        private readonly List<Popup> popups = new List<Popup>();
        private readonly Stack<TMP_Text> pool = new Stack<TMP_Text>();

        // 이번 프레임에 재생한 명중음. 같은 소리를 겹쳐 내지 않음.
        private readonly HashSet<FixedString64Bytes> played = new HashSet<FixedString64Bytes>();

        private Canvas canvas;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            messageQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DamageRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            attackerSoundQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AttackerHitSoundMessage>());
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            localShipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryElement>());

            canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// 로컬 우주선 위에 얻은 점수를 띄움.
        /// </summary>
        public void ShowScore(int gain)
        {
            if (!ClientWorldScope.IsAlive(clientWorld) || localShipQuery.IsEmpty)
            {
                return;
            }

            using var ships = localShipQuery.ToEntityArray(Allocator.Temp);
            Vector2 position = (Vector2)entityManager.GetComponentData<LocalTransform>(ships[0]).Position.xy;

            Spawn(ships[0], position, $"+{gain}", gain / maxSizeScore, scoreColor);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 접속이 끊기면 비움.
            if (connectionQuery.IsEmpty)
            {
                entityManager.DestroyEntity(messageQuery);
                entityManager.DestroyEntity(attackerSoundQuery);

                for (int i = popups.Count - 1; i >= 0; i--)
                {
                    Release(i);
                }

                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            played.Clear();

            if (!messageQuery.IsEmpty)
            {
                using var messages = messageQuery.ToComponentDataArray<DamageRPC>(Allocator.Temp);

                entityManager.DestroyEntity(messageQuery);

                foreach (DamageRPC message in messages)
                {
                    var position = new Vector2(message.Position.x, message.Position.y);

                    Spawn(
                        message.Spaceship,
                        position,
                        Mathf.CeilToInt(message.Amount).ToString(),
                        message.Amount / maxSizeDamage,
                        message.Dealt ? dealtColor : takenColor
                    );

                    // 서버 판정 포탑의 명중음만 냄. 예측 판정 포탑은 AttackerHitSoundMessage 로 이미 냄.
                    if (message.Dealt && !registryQuery.IsEmpty)
                    {
                        ItemRegistryElement turret = registryQuery.GetSingletonBuffer<ItemRegistryElement>(true).Get(message.Turret);

                        if (turret.HitSoundMode == HitSoundMode.Simulated)
                        {
                            PlayAttackerSound(mainCamera, turret.HitSound, position);
                        }
                    }

                    // 로컬 우주선이 맞음. 큰 우주선은 같은 데미지에 덜 흔들림.
                    if (!message.Dealt
                        && mainCamera != null
                        && mainCamera.TryGetComponent(out FollowPlayerEntity follow)
                        && entityManager.HasComponent<PlayerSpaceshipMovementComponent>(message.Spaceship)
                        && entityManager.HasComponent<SpaceshipSpecComponent>(message.Spaceship))
                    {
                        float maxHealth = entityManager.GetComponentData<SpaceshipSpecComponent>(message.Spaceship).MaxHealth;

                        if (maxHealth > 0.0F)
                        {
                            follow.Shake(message.Amount / maxHealth);
                        }
                    }
                }
            }

            if (!attackerSoundQuery.IsEmpty)
            {
                using var sounds = attackerSoundQuery.ToComponentDataArray<AttackerHitSoundMessage>(Allocator.Temp);

                entityManager.DestroyEntity(attackerSoundQuery);

                foreach (AttackerHitSoundMessage sound in sounds)
                {
                    PlayAttackerSound(mainCamera, sound.SoundKey, new Vector2(sound.Position.x, sound.Position.y));
                }
            }

            if (mainCamera == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                popup.Age += deltaTime;

                if (popup.Age >= lifetime)
                {
                    Release(i);
                    continue;
                }

                // 우주선 속도를 따라 움직임.
                if (entityManager.Exists(popup.Ship) && entityManager.HasComponent<LocalTransform>(popup.Ship))
                {
                    popup.Anchor = (Vector2)entityManager.GetComponentData<LocalTransform>(popup.Ship).Position.xy + popup.Offset;
                }

                // 떠오르는 속도는 지수적으로 줄고, 크기는 로그 곡선으로 커짐.
                float t = popup.Age / lifetime;
                float rise = riseHeight * (1.0F - Mathf.Exp(-popup.Age / riseTime));
                float scale = Mathf.Lerp(startScale, 1.0F, Mathf.Log(1.0F + 9.0F * t) / Mathf.Log(10.0F));

                Vector2 screen = mainCamera.WorldToScreenPoint(popup.Anchor);
                screen += (popup.Noise + new Vector2(0.0F, rise)) * canvas.scaleFactor;

                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)transform, screen, canvas.worldCamera, out Vector3 position);

                popup.Text.transform.position = position;
                popup.Text.fontSize = popup.FontSize * scale;
                popup.Text.alpha = Mathf.Clamp01((lifetime - popup.Age) / fadeTime);
            }
        }

        // ratio 는 최대 크기 기준 값의 비율. 로그 곡선으로 글자 크기를 정함.
        private void Spawn(Entity ship, Vector2 position, string text, float ratio, Color color)
        {
            TMP_Text label = pool.Count > 0 ? pool.Pop() : Instantiate(popupPrefab, transform);
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            label.text = text;
            label.color = color;

            float size = Mathf.Log(1.0F + Mathf.Clamp01(ratio) * 9.0F) / Mathf.Log(10.0F);

            var popup = new Popup
            {
                Text     = label,
                Ship     = ship,
                Anchor   = position,
                Noise    = Random.insideUnitCircle * noise,
                FontSize = Mathf.Lerp(minFontSize, maxFontSize, size),
            };

            if (entityManager.Exists(ship) && entityManager.HasComponent<LocalTransform>(ship))
            {
                popup.Offset = position - (Vector2)entityManager.GetComponentData<LocalTransform>(ship).Position.xy;
            }
            else
            {
                popup.Ship = Entity.Null;
            }

            popups.Add(popup);
        }

        // 때린 플레이어의 명중음. 같은 소리는 한 프레임에 한 번만 냄.
        // 방향만 살려 리스너 가까이 끌어오고, 거리 감쇠는 최소 음량을 지켜 직접 계산함.
        private void PlayAttackerSound(UnityEngine.Camera mainCamera, FixedString64Bytes sound, Vector2 position)
        {
            if (mainCamera == null || sound.IsEmpty || SoundManager.Instance == null || !played.Add(sound))
            {
                return;
            }

            Vector3 listener = mainCamera.transform.position;
            Vector2 offset = position - (Vector2)listener;
            float falloff = 1.0F - Mathf.InverseLerp(attackerFalloff.x, attackerFalloff.y, offset.magnitude);

            offset = Vector2.ClampMagnitude(offset, attackerSoundRadius);

            SoundManager.Instance.PlaySfx3D(
                sound.ToString(),
                new Vector3(listener.x + offset.x, listener.y + offset.y, listener.z),
                attackerVolume * Mathf.Max(falloff, attackerMinVolume)
            );
        }

        private void Release(int index)
        {
            TMP_Text label = popups[index].Text;
            label.gameObject.SetActive(false);
            pool.Push(label);

            popups.RemoveAt(index);
        }
    }
}