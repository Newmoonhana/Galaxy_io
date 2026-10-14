using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="ItemAuthoring"/>의 DOTS 구조체. 월드에 떨어진 아이템 한 묶음.
    /// 줍기 판정은 Entity 의 <see cref="ColliderComponent"/>(스프라이트 크기 OBB)로 함.
    /// </summary>
    public struct ItemComponent : IComponentData
    {
        // 클라이언트가 스프라이트를 고르는 데 씀.
        [GhostField]
        public ItemId Id;

        // 묶음 개수. 클라이언트가 겹쳐 그릴 스프라이트 수를 정하는 데 씀.
        [GhostField]
        public int Count;

        // 끌려가는 대상 플레이어. 없으면 Entity.Null.
        public Entity Target;

        // 플레이어를 감지하는 원. 이 안의 플레이어에게 끌려감.
        public ColliderComponent Attraction;

        // 대상에게 끌려가는 속력
        public float FollowSpeed;

        // 떠다니는 속도. 스폰 직후엔 미끄러지는 속도로 시작해 떠다니는 속력까지 느려짐.
        public float2 Velocity;

        // 스폰 시 미끄러지는 최대 속력
        public float SlideSpeed;

        // 미끄러지는 속력의 감속도
        public float SlideDeceleration;

        // 미끄러짐이 끝난 뒤 계속 떠다니는 속력
        public float FloatSpeed;

        // 떠다니며 도는 각속도(라디안/초). 오른쪽으로 가면 시계 방향, 왼쪽이면 반시계 방향.
        public float SpinSpeed;

        // 남은 수명(초). 0 이 되면 사라짐.
        public float Lifetime;

        // 남은 줍기 대기 시간(초). 0 이 될 때까지 끌려가지도, 주워지지도 않음.
        public float PickupDelay;

        // 우주선이 파괴되며 떨어뜨린 아이템의 줍기 대기 시간(초)
        public float DropPickupDelay;

        /// <summary>
        /// 주어진 속도로 미끄러뜨림. 줍기 대기 시간도 새로 걺.
        /// </summary>
        public void Launch(float2 velocity, float pickupDelay)
        {
            Velocity = velocity;
            PickupDelay = pickupDelay;
        }
    }
}
