using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 화면 우측 상단에 킬 로그를 쌓는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class KillLogView : MonoBehaviour
    {
        [Header("Layout")]

        [SerializeField]
        [Tooltip("킬 로그 한 줄 프리팹. 아이콘 스프라이트 에셋이 연결돼 있어야 함.")]
        private KillLogRow rowPrefab;

        [SerializeField]
        [Tooltip("줄이 쌓이는 부모. 위에서 아래로 정렬됨.")]
        private RectTransform container;

        [SerializeField]
        [Tooltip("한 줄이 떠 있는 시간(초)")]
        [Min(0.0F)]
        private float lifetime = 8.0F;

        [SerializeField]
        [Tooltip("동시에 보이는 최대 줄 수. 넘치면 오래된 줄부터 지움.")]
        [Min(1)]
        private int maxRows = 5;

        [Header("Icons")]

        [SerializeField]
        [Tooltip("투사체 격추 아이콘의 스프라이트 이름")]
        private string indirectSprite = "indirect_0";

        [SerializeField]
        [Tooltip("충돌 격추 아이콘의 스프라이트 이름")]
        private string ramSprite = "ram_0";

        [Header("Colors")]

        [SerializeField]
        private Color playerColor = new Color(0.35F, 0.9F, 0.45F);

        [SerializeField]
        private Color enemyColor = new Color(1.0F, 0.6F, 0.2F);

        [SerializeField]
        private Color victimColor = new Color(0.95F, 0.3F, 0.3F);

        [SerializeField]
        [Tooltip("로컬 플레이어가 관련된 줄의 배경")]
        private Color localBackground = new Color(1.0F, 0.0F, 0.0F, 0.3F);

        private EntityManager entityManager;
        private EntityQuery messageQuery;

        private string playerHex;
        private string enemyHex;
        private string victimHex;

        // 표시 중인 줄과 사라질 시각. 오래된 순.
        private readonly Queue<(KillLogRow Row, float ExpireTime)> rows = new Queue<(KillLogRow, float)>();

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
            messageQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<KillLogMessage>());

            playerHex = ColorUtility.ToHtmlStringRGB(playerColor);
            enemyHex  = ColorUtility.ToHtmlStringRGB(enemyColor);
            victimHex = ColorUtility.ToHtmlStringRGB(victimColor);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (!messageQuery.IsEmpty)
            {
                using var messages = messageQuery.ToComponentDataArray<KillLogMessage>(Allocator.Temp);

                foreach (KillLogMessage message in messages)
                {
                    Push(message);
                }

                entityManager.DestroyEntity(messageQuery);
            }

            while (rows.Count > 0 && rows.Peek().ExpireTime <= Time.time)
            {
                Destroy(rows.Dequeue().Row.gameObject);
            }
        }

        private void Push(in KillLogMessage message)
        {
            if (rows.Count >= maxRows)
            {
                Destroy(rows.Dequeue().Row.gameObject);
            }

            KillLogRow row = Instantiate(rowPrefab, container);
            row.Set(Format(message), message.IsLocal, localBackground);

            rows.Enqueue((row, Time.time + lifetime));
        }

        // [Killer( + Assistant) 아이콘 Victim]. 막타를 알 수 없으면 [ + Assistant 아이콘 Victim].
        // 이름은 noparse 로 감싸 리치 텍스트 태그 삽입 방지.
        private string Format(in KillLogMessage message)
        {
            string text = "";

            if (!message.Killer.IsEmpty)
            {
                string killerHex = message.KillerIsEnemy ? enemyHex : playerHex;
                text = $"<color=#{killerHex}><noparse>{message.Killer}</noparse></color>";
            }

            if (!message.Assistant.IsEmpty)
            {
                text += $"<color=#{playerHex}> + <noparse>{message.Assistant}</noparse></color>";
            }

            string icon = message.Type == KillType.Ram ? ramSprite : indirectSprite;

            return text + $" <sprite name=\"{icon}\"> <color=#{victimHex}><noparse>{message.Victim}</noparse></color>";
        }
    }
}
