using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 모선 주인이 함재기에 명령을 내려 달라고 요청하는 RPC.
    /// </summary>
    public struct CarrierOrderRPC : IRpcCommand
    {
        public CarrierOrder Order;

        // Attack 대상 Ghost
        public Entity Target;

        // SecureArea 지점
        public float2 Position;
    }
}
