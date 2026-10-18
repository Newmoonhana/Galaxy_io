using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 진영 종류. 값마다 비트 하나를 차지해 OR 로 진영 집합을 만듦.
    /// </summary>
    public enum FactionID : uint
    {
        // 어느 진영에도 속하지 않음. 인지 대상이 되지 않음.
        None = 0,

        // 같은 진영끼리도 적대함. (PvP)
        Player = 1 << 0,

        // 같은 진영끼리도 적대함.
        Neutralize = 1 << 1,
    }

    /// <summary>
    /// 우주선, 투사체가 속한 진영. 우주선은 <see cref="SpaceshipAuthoring"/>, 투사체는 발사 시점에 채움.
    /// </summary>
    public struct FactionComponent : IComponentData
    {
        // 투사체 진영은 서버가 발사 시점에 정함. 클라이언트 명중 연출에 쓰려고 복제함.
        [GhostField]
        public FactionID Value;

        // 소속 파티. 같은 파티끼리는 같은 편. 플레이어 우주선은 서버의 PartyService 가 채우고,
        // 파티가 없는 플레이어도 혼자만의 번호를 받음. 함재기는 모선 값을 따름. 0 이면 편이 없음(AI).
        [GhostField]
        public uint Party;
    }

    /// <summary>
    /// 진영 간 관계 판정.
    /// </summary>
    public static class Faction
    {
        // 같은 진영끼리도 적대하는 진영.
        public const uint SelfHostile = (uint)(FactionID.Player | FactionID.Neutralize);

        // 파티가 없는 플레이어의 파티 번호. 파티 번호(1부터 차례로 붙임)와 겹치지 않게 최상위 비트를 켬.
        public static uint SoloParty(int networkId)
        {
            return 0x80000000u | (uint)networkId;
        }

        // 서로 통과하는 사이인지. 같은 진영이면 통과, 단 SelfHostile 진영은 같은 진영끼리도 맞음.
        public static bool IsAllied(FactionID lhs, FactionID rhs)
        {
            return lhs == rhs && ((uint)lhs & SelfHostile) == 0;
        }

        // 같은 파티인지. 같은 파티끼리는 투사체가 통과하고 서로 데미지를 주지 않음.
        public static bool IsSameParty(uint lhs, uint rhs)
        {
            return lhs != 0 && lhs == rhs;
        }

        // 서로 데미지를 주지 않는 사이인지.
        public static bool IsFriendly(in FactionComponent lhs, in FactionComponent rhs)
        {
            return IsAllied(lhs.Value, rhs.Value) || IsSameParty(lhs.Party, rhs.Party);
        }
    }
}