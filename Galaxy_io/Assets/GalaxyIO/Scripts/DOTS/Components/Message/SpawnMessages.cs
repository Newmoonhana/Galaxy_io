using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 투사체 스폰 요청. <see cref="SpawnSystem"/>이 스폰한 우주선의 이동이 끝난 위치에 만듦.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ProjectileSpawnMessage : IBufferElementData
    {
        public Entity Prefab;

        // 쏜 우주선. 투사체의 주인, 진영, 위치 기준이 됨.
        public Entity Spawner;

        // 쏜 우주선 기준 총구 Transform. 탄퍼짐까지 적용한 값.
        public LocalTransform Muzzle;

        public int NetworkId;
        public uint SpawnId;
        public ItemId Turret;
        public float Damage;
        public uint InterpolationDelay;

        // 직진 투사체의 발사 속력. 쏜 우주선의 속력은 아직 더하지 않은 값.
        public float Speed;

        // 쏜 우주선 기준 축. 우주선 속도의 이 방향 성분을 투사체 속력에 더함.
        public float2 InheritAxis;
    }

    /// <summary>
    /// 서버가 쏜 <see cref="ProjectileReplication.Shot"/> 투사체의 발사 정보. 서버만 씀.
    /// <see cref="ProjectileShotNotifySystem"/>이 근처 접속에 <see cref="ProjectileShotRPC"/>로 보냄.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ProjectileShotMessage : IBufferElementData
    {
        public ProjectileShotRPC Shot;

        // 쏜 플레이어. 직접 쏘므로 받지 않음. AI, 자동 포탑은 -1.
        public int NetworkId;

        // 투사체가 수명 동안 날아가는 거리. 복제 범위 밖에서 날아 들어오는 투사체도 보냄.
        public float Reach;
    }

    /// <summary>
    /// 월드 좌표에 우주선을 스폰하는 요청. e.g. 적 우주선
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SpaceshipSpawnMessage : IBufferElementData
    {
        public Entity Prefab;
        public float3 Position;
        public quaternion Rotation;
        public int NetworkId;
    }

    /// <summary>
    /// 모선에서 함재기를 출격시키는 요청. 모선의 이동이 끝난 위치와 속도로 나감.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CraftSpawnMessage : IBufferElementData
    {
        public Entity Prefab;
        public Entity Carrier;
        public CarriedCraftComponent Craft;
        public float HealthBonus;
    }

    /// <summary>
    /// 아이템 Entity 스폰 요청.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ItemSpawnMessage : IBufferElementData
    {
        public Entity Prefab;
        public ItemId Id;
        public int Count;
        public float3 Position;

        // 미끄러지는 속도와 줍기 대기 시간
        public float2 Velocity;
        public float PickupDelay;
    }
}
