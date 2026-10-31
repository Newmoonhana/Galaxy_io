using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 플레이어 우주선 아래에 이름을 그리는 클라이언트 전용 뷰.
    /// 회전한 선체 충돌체의 아래쪽 끝에 붙이고, 선체 두께(Thickness)만큼 크게 그림.
    /// 로컬 플레이어와 같은 파티원 이름은 초록색으로 칠하고, 파티장과 권한자는 이름 왼쪽에 아이콘을 붙임.
    /// 파티 구성은 <see cref="ChatView"/>가 받은 것을 씀. 다른 파티의 구성은 모르므로 보이지 않음.
    /// 점령전 위성 아래에는 같은 이름표로 점령도(%)를 점령하는 팀 색으로 그림.
    /// </summary>
    public sealed class SpaceshipNameLabels : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 이름표 템플릿. 위쪽 정렬이라 위치 아래로 글자가 붙음. 자식에 파티 아이콘.")]
        private TMP_Text labelTemplate;

        [SerializeField]
        [Tooltip("선체 아래쪽 끝과 이름표 사이 간격. 체력바 아래에 둠. 두께 배율이 함께 곱해짐.")]
        private float gap = 0.35F;

        [SerializeField]
        [Tooltip("두께 1 당 늘어나는 크기 배율. 두께 1이면 템플릿 크기 그대로.")]
        [Min(0.0F)]
        private float scalePerThickness = 0.1F;

        [SerializeField]
        [Tooltip("파티 구성을 받는 채팅창")]
        private ChatView chat;

        [SerializeField]
        [Tooltip("파티장 이름 왼쪽에 붙는 왕관")]
        private Sprite crownSprite;

        [SerializeField]
        [Tooltip("권한자 이름 왼쪽에 붙는 아이콘")]
        private Sprite authSprite;

        [SerializeField]
        [Tooltip("아이콘 높이. 이름표 기준 단위라 두께 배율이 함께 곱해짐.")]
        [Min(0.0F)]
        [FormerlySerializedAs("crownSize")]
        private float iconSize = 0.5F;

        [SerializeField]
        [Tooltip("이름 글자와 아이콘 사이 간격. 이름표 기준 단위.")]
        [FormerlySerializedAs("crownGap")]
        private float iconGap = 0.05F;

        [Header("Satellite")]

        [SerializeField]
        [Tooltip("위성 아래쪽 끝과 점령도 사이 간격. 두께 배율이 함께 곱해짐.")]
        private float satelliteGap = 0.35F;

        [SerializeField]
        private Color redColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        private Color blueColor = new Color(0.3F, 0.6F, 1.0F);

        // 프리팹 크기. 두께 배율의 기준.
        private Vector3 baseScale;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;
        private EntityQuery satelliteQuery;

        // 위성 수만큼 늘어나는 점령도 표시 풀. 아이콘은 끔.
        private readonly List<TMP_Text> satelliteLabels = new List<TMP_Text>();

        // 점령도 표시마다 보여 주는 값(팀 * 1000 + 정수 %). 바뀔 때만 문자열을 만듦.
        private readonly List<int> shownProgress = new List<int>();

        // 우주선 수만큼 늘어나는 이름표 풀
        private readonly List<TMP_Text> labels = new List<TMP_Text>();

        // 이름표마다 하나씩 붙은 파티장 / 권한자 아이콘. 이름표의 자식.
        private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();

        // 이름표마다 표시 중인 이름. 바뀔 때만 문자열을 만들어 GC 할당을 막음.
        private readonly List<FixedString64Bytes> shownNames = new List<FixedString64Bytes>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            labelTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            spaceshipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            satelliteQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SatelliteComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            baseScale = labelTemplate.transform.localScale;
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            PartyRosterRPC roster = chat.PartyRoster;

            using var spaceships = spaceshipQuery.ToEntityArray(Allocator.Temp);

            int shown = 0;

            foreach (Entity spaceship in spaceships)
            {
                var playerName = entityManager.GetComponentData<PlayerNameComponent>(spaceship).Value;

                // 스폰 직후 이름이 아직 도착하지 않았음.
                if (playerName.IsEmpty)
                {
                    continue;
                }

                var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);
                float thickness = entityManager.GetComponentData<SpaceshipSpecComponent>(spaceship).Thickness;
                Aabb bounds = ColliderBounds.ToAabb(
                    ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(spaceship), transformData)
                );
                float scale = 1.0F + Mathf.Max(thickness - 1.0F, 0.0F) * scalePerThickness;

                int index = shown++;
                TMP_Text label = Rent(index);

                label.transform.localScale = baseScale * scale;
                label.transform.position = new Vector3(
                    transformData.Position.x,
                    bounds.Min.y - gap * scale,
                    transformData.Position.z
                );

                if (shownNames[index] != playerName)
                {
                    shownNames[index] = playerName;
                    label.text = playerName.ToString();

                    // 아이콘 위치에 쓸 글자 범위를 바로 갱신함.
                    label.ForceMeshUpdate();
                }

                int networkId = entityManager.GetComponentData<GhostOwner>(spaceship).NetworkId;
                Sprite sprite = roster.Master == networkId ? crownSprite
                    : roster.Authorized.Contains(networkId) ? authSprite
                    : null;

                SpriteRenderer icon = icons[index];
                icon.gameObject.SetActive(sprite != null);

                if (sprite != null)
                {
                    // 스프라이트마다 크기가 달라도 높이를 맞춤. 피벗이 가운데라 너비의 절반만큼 더 밀어냄.
                    float size = iconSize / sprite.bounds.size.y;
                    Bounds text = label.textBounds;

                    icon.sprite = sprite;
                    icon.transform.localScale = Vector3.one * size;
                    icon.transform.localPosition = new Vector3(text.min.x - iconGap - sprite.bounds.extents.x * size, text.center.y, 0.0F);
                }
            }

            // 이번 프레임에 쓰이지 않은 이름표는 감춤.
            for (int i = shown; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }

            UpdateSatellites();
        }

        // 점령도가 쌓인 위성 아래에 점령하는 팀 색으로 점령도를 그림.
        private void UpdateSatellites()
        {
            int shown = 0;

            if (!satelliteQuery.IsEmpty)
            {
                using var satellites = satelliteQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity entity in satellites)
                {
                    SatelliteComponent satellite = entityManager.GetComponentData<SatelliteComponent>(entity);

                    if (satellite.Capturing == Team.None || satellite.Progress <= 0.0F)
                    {
                        continue;
                    }

                    var transformData = entityManager.GetComponentData<LocalTransform>(entity);
                    float thickness = entityManager.GetComponentData<SpaceshipSpecComponent>(entity).Thickness;
                    Aabb bounds = ColliderBounds.ToAabb(
                        ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(entity), transformData)
                    );
                    float scale = 1.0F + Mathf.Max(thickness - 1.0F, 0.0F) * scalePerThickness;

                    int index = shown++;
                    TMP_Text label = RentSatellite(index);

                    label.transform.localScale = baseScale * scale;
                    label.transform.position = new Vector3(
                        transformData.Position.x,
                        bounds.Min.y - satelliteGap * scale,
                        transformData.Position.z
                    );

                    int percent = Mathf.FloorToInt(satellite.Progress);
                    int key = (int)satellite.Capturing * 1000 + percent;

                    if (shownProgress[index] != key)
                    {
                        shownProgress[index] = key;
                        label.text = $"{percent}%";
                        label.color = satellite.Capturing == Team.Red ? redColor : blueColor;
                    }
                }
            }

            for (int i = shown; i < satelliteLabels.Count; i++)
            {
                satelliteLabels[i].gameObject.SetActive(false);
            }
        }

        private TMP_Text RentSatellite(int index)
        {
            while (satelliteLabels.Count <= index)
            {
                TMP_Text label = Instantiate(labelTemplate, transform);
                label.name = "SatelliteProgress";

                SpriteRenderer icon = label.GetComponentInChildren<SpriteRenderer>(true);

                if (icon != null) icon.gameObject.SetActive(false);

                satelliteLabels.Add(label);
                shownProgress.Add(-1);
            }

            satelliteLabels[index].gameObject.SetActive(true);
            return satelliteLabels[index];
        }

        private TMP_Text Rent(int index)
        {
            while (labels.Count <= index)
            {
                TMP_Text label = Instantiate(labelTemplate, transform);
                // 파티 아이콘. 정렬 레이어/순서는 템플릿에서 이름표와 맞춰 둠.
                SpriteRenderer icon = label.GetComponentInChildren<SpriteRenderer>(true);

                labels.Add(label);
                icons.Add(icon);
                shownNames.Add(default);
            }

            labels[index].gameObject.SetActive(true);
            return labels[index];
        }
    }
}