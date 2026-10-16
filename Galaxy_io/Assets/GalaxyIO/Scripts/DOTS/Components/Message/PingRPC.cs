using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 핑이 가리키는 대상의 종류. 받는 쪽이 자신의 언어로 대상 이름을 만듦.
    /// </summary>
    public enum PingTarget : byte
    {
        // 빈 공간
        Destination,

        // 플레이어 우주선. TargetId 는 SpaceshipId.
        Spaceship,

        // AI 우주선. TargetName 이 표시 이름.
        Enemy,

        // 떨어진 아이템. TargetId 는 ItemId.
        Item,
    }

    /// <summary>
    /// 초계함의 목표 지정 핑. 클라이언트가 서버로 보내고, 서버가 보낸 사람을 포함한 파티원에게 다시 보냄.
    /// 보낸 사람마다 핑은 하나. 새 핑이 오면 이전 핑을 대신함.
    /// </summary>
    public struct PingRPC : IRpcCommand
    {
        // 핑을 찍은 월드 좌표
        public float2 Position;

        public PingTarget Target;

        // Spaceship, Item 의 Id
        public ushort TargetId;

        // Enemy 의 표시 이름
        public FixedString32Bytes TargetName;

        // 보낸 사람의 핑을 지움. 나머지 값은 쓰지 않음.
        public bool Remove;

        // 보낸 플레이어. 서버가 채움.
        public FixedString64Bytes Sender;

        // 보낸 플레이어의 NetworkId. 서버가 채움.
        public int SenderNetworkId;
    }
}
