using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 채팅을 보낸 플레이어 우주선 위에 말풍선을 띄우는 클라이언트 전용 뷰.
    /// 받은 채팅이면 보고 있는 탭과 관계없이 띄움.
    /// </summary>
    public sealed class ChatBubbles : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 말풍선 템플릿")]
        private ChatBubbleView bubbleTemplate;

        [SerializeField]
        [Tooltip("선체 위쪽 끝과 말풍선 꼬리 끝 사이 간격")]
        private float gap = 0.2F;

        [SerializeField]
        [Tooltip("말풍선이 떠 있는 시간(초)")]
        [Min(0.0F)]
        private float duration = 4.0F;

        // 띄울 말 하나. 같은 사람이 다시 말하면 새 것으로 바뀜.
        private sealed class Speech
        {
            public string Text;
            public Color Color;
            public float ExpireTime;
        }

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;

        // 보낸 플레이어 NetworkId -> 띄울 말
        private readonly Dictionary<int, Speech> speeches = new Dictionary<int, Speech>();

        // 시간이 다 된 말. speeches 에서 지울 목록.
        private readonly List<int> expired = new List<int>();

        // 우주선 수만큼 늘어나는 말풍선 풀
        private readonly List<ChatBubbleView> bubbles = new List<ChatBubbleView>();

        // 말풍선마다 표시 중인 말. 바뀔 때만 크기를 다시 잼.
        private readonly List<Speech> shownSpeeches = new List<Speech>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            bubbleTemplate.gameObject.SetActive(false);
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
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        /// <summary>
        /// 보낸 플레이어의 우주선 위에 말풍선을 띄움. 우주선이 보이지 않아도 시간은 흐름.
        /// </summary>
        public void Show(int networkId, string text, Color color)
        {
            speeches[networkId] = new Speech
            {
                Text       = text,
                Color      = color,
                ExpireTime = Time.time + duration,
            };
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            foreach (var (networkId, speech) in speeches)
            {
                if (speech.ExpireTime <= Time.time)
                {
                    expired.Add(networkId);
                }
            }

            foreach (int networkId in expired)
            {
                speeches.Remove(networkId);
            }

            expired.Clear();

            int shown = 0;

            if (speeches.Count > 0)
            {
                using var spaceships = spaceshipQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity spaceship in spaceships)
                {
                    int networkId = entityManager.GetComponentData<GhostOwner>(spaceship).NetworkId;

                    if (!speeches.TryGetValue(networkId, out Speech speech))
                    {
                        continue;
                    }

                    var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);

                    // 회전한 선체의 위쪽 끝에 붙임.
                    Aabb bounds = ColliderBounds.ToAabb(
                        ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(spaceship), transformData)
                    );

                    int index = shown++;
                    ChatBubbleView bubble = Rent(index);

                    bubble.transform.position = new Vector3(
                        transformData.Position.x,
                        bounds.Max.y + gap,
                        transformData.Position.z
                    );

                    if (shownSpeeches[index] != speech)
                    {
                        shownSpeeches[index] = speech;
                        bubble.Set(speech.Text, speech.Color);
                    }
                }
            }

            // 이번 프레임에 쓰이지 않은 말풍선은 감춤.
            for (int i = shown; i < bubbles.Count; i++)
            {
                bubbles[i].gameObject.SetActive(false);
            }
        }

        private ChatBubbleView Rent(int index)
        {
            while (bubbles.Count <= index)
            {
                bubbles.Add(Instantiate(bubbleTemplate, transform));
                shownSpeeches.Add(null);
            }

            bubbles[index].gameObject.SetActive(true);
            return bubbles[index];
        }
    }
}