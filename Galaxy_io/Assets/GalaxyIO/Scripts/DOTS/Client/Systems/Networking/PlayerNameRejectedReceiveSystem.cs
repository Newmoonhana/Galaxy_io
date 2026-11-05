using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 서버의 이름 거절(<see cref="PlayerNameRejectedRPC"/>)을 받아 리스폰 창을 다시 열고 경고창 표시를 알리는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PlayerNameRejectedReceiveSystem : SystemBase
    {
        /// <summary>
        /// 거절 알림. 매니지드 UI(GalaxyIO_Main)가 구독해 경고창을 띄움.
        /// </summary>
        public static event System.Action<PlayerNameError> Rejected;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerStateComponent>();
            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerNameRejectedRPC, ReceiveRpcCommandRequest>().Build());
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<PlayerNameRejectedRPC>>()
                         .WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                // 리스폰 요청 때 꺼진 Dead 를 다시 켜 리스폰 창을 엶.
                SystemAPI.GetSingletonRW<PlayerStateComponent>().ValueRW.Dead = true;
                Rejected?.Invoke(rpc.ValueRO.Error);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
