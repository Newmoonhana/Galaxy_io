using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 데미지를 때린 플레이어와 맞은 플레이어에게 알리는 RPC. 데미지 숫자와 명중음에 씀.
    /// </summary>
    public struct DamageRPC : IRpcCommand
    {
        // 맞은 우주선. 클라이언트에 복제되지 않았으면 Entity.Null.
        public Entity Spaceship;

        // 맞은 지점
        public float2 Position;

        public float Amount;

        // 맞힌 포탑. 충돌 데미지는 None.
        public ItemId Turret;

        // 받는 쪽이 때린 쪽인지. 아니면 맞은 쪽.
        public bool Dealt;
    }
}
