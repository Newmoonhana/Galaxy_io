using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Party
{
    /// <summary>
    /// 서버의 파티 목록과 파티 명령어 처리. 서버 월드당 한 개.
    /// 파티원은 NetworkId 로 묶음. 우주선이 파괴돼도 파티는 남음.
    /// </summary>
    public sealed class PartyService
    {
        private sealed class Party
        {
            // 우주선의 FactionComponent.Party 에 들어가는 값
            public uint Id;

            public int Master;

            // 들어온 순서. 파티장이 나가면 맨 앞 사람이 이어받음.
            public readonly List<int> Members = new List<int>();

            // 파티 명령어 권한을 받은 파티원
            public readonly HashSet<int> Authorized = new HashSet<int>();

            public bool CanManage(int networkId)
            {
                return Master == networkId || Authorized.Contains(networkId);
            }
        }

        private readonly EntityManager entityManager;
        private readonly EntityQuery connectionQuery;
        private readonly EntityQuery shipQuery;

        // NetworkId -> 속한 파티
        private readonly Dictionary<int, Party> parties = new Dictionary<int, Party>();

        // 아직 받아들이지 않은 초대. 초대받은 사람이 어느 파티에든 들어가면 모두 사라짐.
        private readonly HashSet<(int Invitee, int Inviter)> invites = new HashSet<(int Invitee, int Inviter)>();

        // 0 은 파티 없음. 서버가 도는 동안 같은 값을 다시 쓰지 않음.
        private uint nextId = 1;

        public PartyService(EntityManager entityManager)
        {
            this.entityManager = entityManager;

            // 입장한 플레이어의 Connection
            connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<PlayerNameComponent>()
            );
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadWrite<FactionComponent>(),
                ComponentType.ReadWrite<PartyMasterComponent>()
            );
        }

        /// <summary>
        /// `/p` 명령어 처리. 결과는 안내문으로 알림.
        /// </summary>
        public void Execute(int sender, PartyCommandRPC rpc)
        {
            if (rpc.Command == PartyCommand.Invite)
            {
                int invitee = Find(rpc.Target, sender);

                if (invitee == 0)
                {
                    Notice(sender, Name(sender) == rpc.Target ? "Party/SelfInvite" : "Chat/UserNotFound", rpc.Target);
                }
                else if (parties.TryGetValue(sender, out Party own) && !own.CanManage(sender))
                {
                    Notice(sender, "Party/NoPermission");
                }
                else if (parties.ContainsKey(invitee))
                {
                    Notice(sender, "Party/AlreadyInParty", rpc.Target);
                }
                else
                {
                    invites.Add((invitee, sender));
                    Notice(sender, "Party/InviteSent", rpc.Target);
                    Notice(invitee, "Party/Invited", Name(sender), sender);
                }

                return;
            }

            if (rpc.Command == PartyCommand.Accept)
            {
                int inviter = rpc.Inviter;

                if (!invites.Remove((sender, inviter)))
                {
                    Notice(sender, "Party/NoInvite");
                    return;
                }

                // 초대한 사람이 권한 없는 파티원이 됐으면 무효.
                if (parties.TryGetValue(inviter, out Party party) && !party.CanManage(inviter))
                {
                    Notice(sender, "Party/InviteExpired");
                    return;
                }

                // 파티가 없던 사람의 초대. 초대한 사람이 파티장.
                if (party == null)
                {
                    party = new Party { Id = nextId++, Master = inviter };
                    party.Members.Add(inviter);
                    parties[inviter] = party;
                    invites.RemoveWhere(invite => invite.Invitee == inviter);
                    Send(inviter, new PartyStateRPC { InParty = true });
                }

                party.Members.Add(sender);
                parties[sender] = party;
                invites.RemoveWhere(invite => invite.Invitee == sender);
                Send(sender, new PartyStateRPC { InParty = true });
                Notify(party, "Party/Joined", Name(sender));

                return;
            }

            if (!parties.TryGetValue(sender, out Party current))
            {
                Notice(sender, "Party/NotInParty");
                return;
            }

            if (rpc.Command == PartyCommand.Quit)
            {
                Remove(sender, Name(sender), "Party/Left");
                return;
            }

            // 나머지 명령어는 같은 파티원이 대상.
            int target = Find(rpc.Target, sender);

            if (!parties.TryGetValue(target, out Party targetParty) || targetParty != current)
            {
                Notice(sender, "Party/NotMember", rpc.Target);
                return;
            }

            switch (rpc.Command)
            {
                case PartyCommand.Delegate:
                    if (current.Master != sender)
                    {
                        Notice(sender, "Party/NoPermission");
                        break;
                    }

                    current.Master = target;
                    current.Authorized.Remove(target);
                    Notify(current, "Party/MasterChanged", rpc.Target);
                    break;

                // 권한을 받은 파티원은 파티장과 다른 권한자를 내보낼 수 없음.
                case PartyCommand.Kick:
                    if (!current.CanManage(sender)
                        || target == current.Master
                        || (current.Authorized.Contains(target) && current.Master != sender))
                    {
                        Notice(sender, "Party/NoPermission");
                        break;
                    }

                    Remove(target, rpc.Target, "Party/Kicked");
                    break;

                case PartyCommand.Authorize:
                    if (!current.CanManage(sender) || target == current.Master)
                    {
                        Notice(sender, "Party/NoPermission");
                        break;
                    }

                    current.Authorized.Add(target);
                    Notify(current, "Party/Authorized", rpc.Target);
                    break;

                case PartyCommand.Revoke:
                    if (current.Master != sender)
                    {
                        Notice(sender, "Party/NoPermission");
                        break;
                    }

                    if (!current.Authorized.Remove(target))
                    {
                        Notice(sender, "Party/NotAuthorized", rpc.Target);
                        break;
                    }

                    Notify(current, "Party/Revoked", rpc.Target);
                    break;
            }
        }

        /// <summary>
        /// 접속이 끊긴 플레이어를 파티와 초대 목록에서 뺌. NetworkId 는 다음 접속에 재사용됨.
        /// </summary>
        public void Disconnect(int networkId, FixedString64Bytes name)
        {
            invites.RemoveWhere(invite => invite.Invitee == networkId || invite.Inviter == networkId);
            Remove(networkId, name, "Party/Left");
        }

        /// <summary>
        /// 같은 파티원의 NetworkId. 들어온 순서. 파티가 없으면 null.
        /// </summary>
        public IReadOnlyList<int> Members(int networkId)
        {
            return parties.TryGetValue(networkId, out Party party) ? party.Members : null;
        }

        /// <summary>
        /// 서버 틱마다 호출. 플레이어 우주선에 파티와 파티장 여부를 씀.
        /// 리스폰, 기종 변경으로 우주선이 새로 생기므로 틱마다 맞추고, 바뀐 값만 씀.
        /// </summary>
        public void Update()
        {
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity ship in ships)
            {
                int networkId = entityManager.GetComponentData<GhostOwner>(ship).NetworkId;
                parties.TryGetValue(networkId, out Party party);

                FactionComponent faction = entityManager.GetComponentData<FactionComponent>(ship);
                uint id = party?.Id ?? 0;

                if (faction.Party != id)
                {
                    faction.Party = id;
                    entityManager.SetComponentData(ship, faction);
                }

                bool master = party != null && party.Master == networkId;

                if (entityManager.GetComponentData<PartyMasterComponent>(ship).Value != master)
                {
                    entityManager.SetComponentData(ship, new PartyMasterComponent { Value = master });
                }
            }
        }

        // 파티에서 뺌. 떠나는 사람도 안내문을 받음. 한 명만 남으면 해산하고, 파티장이 나가면 가장 먼저 들어온 사람이 이어받음.
        private void Remove(int networkId, FixedString64Bytes name, string key)
        {
            if (!parties.TryGetValue(networkId, out Party party))
            {
                return;
            }

            Notify(party, key, name);

            parties.Remove(networkId);
            party.Members.Remove(networkId);
            party.Authorized.Remove(networkId);
            Send(networkId, new PartyStateRPC { InParty = false });

            if (party.Members.Count < 2)
            {
                foreach (int member in party.Members)
                {
                    parties.Remove(member);
                    Notice(member, "Party/Disbanded");
                    Send(member, new PartyStateRPC { InParty = false });
                }

                return;
            }

            if (party.Master == networkId)
            {
                party.Master = party.Members[0];
                party.Authorized.Remove(party.Master);
                Notify(party, "Party/MasterChanged", Name(party.Master));
            }
        }

        // 이름이 같은 플레이어의 NetworkId. except 는 후보에서 뺌. 없으면 0.
        private int Find(FixedString64Bytes name, int except)
        {
            using NativeArray<NetworkId> ids = connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
            using NativeArray<PlayerNameComponent> names = connectionQuery.ToComponentDataArray<PlayerNameComponent>(Allocator.Temp);

            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i].Value != except && names[i].Value == name)
                {
                    return ids[i].Value;
                }
            }

            return 0;
        }

        private FixedString64Bytes Name(int networkId)
        {
            Entity connection = Connection(networkId);

            return connection == Entity.Null
                ? default
                : entityManager.GetComponentData<PlayerNameComponent>(connection).Value;
        }

        // 입장한 플레이어의 Connection. 없으면 Entity.Null.
        private Entity Connection(int networkId)
        {
            using NativeArray<Entity> connections = connectionQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity connection in connections)
            {
                if (entityManager.GetComponentData<NetworkId>(connection).Value == networkId)
                {
                    return connection;
                }
            }

            return Entity.Null;
        }

        private void Notify(Party party, string key, FixedString64Bytes argument)
        {
            foreach (int member in party.Members)
            {
                Notice(member, key, argument);
            }
        }

        // 서버는 언어를 모르므로 번역 키와 번역문의 {0} 만 보냄. sender 는 초대 안내문의 [수락] 링크에 씀.
        private void Notice(int networkId, string key, FixedString64Bytes argument = default, int sender = 0)
        {
            Send(networkId, new ChatRPC
            {
                Channel         = ChatChannel.System,
                Text            = key,
                Target          = argument,
                SenderNetworkId = sender,
            });
        }

        // 접속이 끊긴 플레이어에게는 보내지 않음.
        private void Send<T>(int networkId, T rpc) where T : unmanaged, IRpcCommand
        {
            Entity connection = Connection(networkId);

            if (connection == Entity.Null)
            {
                return;
            }

            Entity entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, rpc);
            entityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}