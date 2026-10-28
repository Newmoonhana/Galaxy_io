using GalaxyIO.DOTS.Client.Components.Singleton;
using Unity.Burst;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 클라이언트 월드가 만들어질 때 <see cref="PlayerStateComponent"/> 싱글톤을 만드는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerStateInitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton<PlayerStateComponent>();
            state.Enabled = false;
        }
    }
}
