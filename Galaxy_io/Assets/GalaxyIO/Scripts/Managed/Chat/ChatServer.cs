using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Chat
{
    /// <summary>
    /// 클라이언트가 보낸 <see cref="ChatRPC"/>를 받을 클라이언트에게 전달하는 서버 전용 중계기.
    /// </summary>
    public sealed class ChatServer : MonoBehaviour
    {
        private EntityManager entityManager;
        private EntityQuery requestQuery;
        private EntityQuery playerQuery;

        // 캐시한 쿼리가 속한 서버 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World serverWorld;

        private void Start()
        {
            serverWorld = ClientServerBootstrap.ServerWorld;

            if (serverWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = serverWorld.EntityManager;
            requestQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ChatRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );

            // 입장한 플레이어의 Connection
            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<PlayerNameComponent>()
            );
        }

        private void LateUpdate()
        {
            // 세션을 나가면 서버 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!serverWorld.IsCreated || serverWorld != ClientServerBootstrap.ServerWorld)
            {
                enabled = false;
                return;
            }

            if (requestQuery.IsEmpty)
            {
                return;
            }

            using var chats = requestQuery.ToComponentDataArray<ChatRPC>(Allocator.Temp);
            using var requests = requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            using var players = playerQuery.ToEntityArray(Allocator.Temp);
            using var names = playerQuery.ToComponentDataArray<PlayerNameComponent>(Allocator.Temp);

            entityManager.DestroyEntity(requestQuery);

            for (int i = 0; i < chats.Length; i++)
            {
                ChatRPC chat = chats[i];
                Entity sender = requests[i].SourceConnection;

                // 입장 전이거나 이미 끊긴 접속은 무시.
                if (chat.Text.IsEmpty || !entityManager.HasComponent<PlayerNameComponent>(sender))
                {
                    continue;
                }

                chat.Sender = entityManager.GetComponentData<PlayerNameComponent>(sender).Value;

                if (chat.Channel == ChatChannel.Public)
                {
                    Send(chat, Entity.Null);
                    continue;
                }

                if (chat.Channel != ChatChannel.Whisper)
                {
                    continue;
                }

                // "{받는 이름} {본문}". 이름에 공백이 들어갈 수 있어 가장 길게 맞는 이름을 고름.
                string text = chat.Text.ToString();
                int target = -1;
                int nameLength = 0;

                for (int j = 0; j < names.Length; j++)
                {
                    string name = names[j].Value.ToString();

                    if (name.Length > nameLength && text.StartsWith(name + " ", StringComparison.Ordinal))
                    {
                        target = j;
                        nameLength = name.Length;
                    }
                }

                if (target < 0)
                {
                    // 서버는 언어를 모르므로 번역 키와 입력한 이름만 보냄.
                    var notice = new ChatRPC { Channel = ChatChannel.System, Text = "Chat/UserNotFound" };
                    notice.Target.CopyFromTruncated(text.Split(' ')[0]);

                    Send(notice, sender);
                    continue;
                }

                chat.Target = names[target].Value;
                chat.Text = text.Substring(nameLength + 1);

                // 자신에게 보낸 귓속말은 사본만 돌려줌.
                if (players[target] != sender)
                {
                    Send(chat, players[target]);
                }

                chat.Echo = true;
                Send(chat, sender);
            }
        }

        // Entity.Null 이면 모든 접속에 전송.
        private void Send(in ChatRPC chat, Entity target)
        {
            Entity rpcEntity = entityManager.CreateEntity(typeof(ChatRPC), typeof(SendRpcCommandRequest));

            entityManager.SetComponentData(rpcEntity, chat);
            entityManager.SetComponentData(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}
