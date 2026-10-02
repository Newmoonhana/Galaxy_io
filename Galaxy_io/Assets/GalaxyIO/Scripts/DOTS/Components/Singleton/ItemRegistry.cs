using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="ItemId"/>로 월드 아이템 스프라이트와 포탑 사양을 찾는 레지스트리. 월드당 한 개.
    /// 버퍼 인덱스가 곧 <see cref="ItemId"/> 값임. 나머지 정의는 <see cref="ItemRegistryAssetComponent"/>에서 찾음.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ItemRegistryElement : IBufferElementData
    {
        // 월드에 떨어진 아이템 Entity 의 스프라이트. 빈 칸이면 null.
        public UnityObjectRef<Sprite> Sprite;

        // 스프라이트에 맞춘 줍기 충돌체 (반너비, 반높이)
        public float2 HalfExtents;

        // 포탑만. 총구 순서대로 놓인 TurretSpecElement 범위.
        public ushort TurretSpecStart;
        public byte TurretSpecCount;

        // 포탑만. 사격 시작/중지 전환 시간(초).
        public float SwitchTime;

        // 포탑만. 사격 시작음, 사격 중 반복음. 클라이언트만 씀.
        public UnityObjectRef<AudioClip> LaunchSound;
        public UnityObjectRef<AudioClip> LoopSound;
    }

    /// <summary>
    /// 포탑 아이템의 총구 하나의 사양. 레지스트리 Entity 에 모든 포탑 것이 이어 붙어 있음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TurretSpecElement : IBufferElementData
    {
        // 없으면 이 총구는 쏘지 않음.
        public Entity ProjectilePrefab;

        // 투사체가 Ghost 가 아님. 서버는 복제 없이 데미지 판정용으로만 쏘고,
        // 클라이언트는 사격 전환 상태를 보고 연출용으로 따로 쏨.
        public bool LocalLaunch;

        // 따라 쏘는 총구는 쓰지 않음.
        public float MaxFireCooldown;

        // 따라 쏠 총구 번호(0부터). 없으면 -1. 따라 쏘는 총구는 앞 총구가 쏠 때마다 FollowDelay 뒤에 한 번 쏨.
        public sbyte FollowMuzzle;

        // 따라 쏘는 총구만. 앞 총구가 쏜 뒤 쏘기까지의 시간(초).
        public float FollowDelay;

        // 한 번에 쏘는 투사체 수. 1 이상.
        public byte ProjectileCount;

        // 탄 퍼짐 정도(Radian)
        public float Spread;

        // 속력 분포. 발사 속력에 1 ± 이 값 사이의 배율을 곱함.
        public float SpeedSpread;

        // 투사체 공격력
        public float Damage;

        public UnityObjectRef<AudioClip> ShotSound;
    }

    public static class ItemRegistry
    {
        /// <summary>
        /// 등록된 아이템 항목. 없으면 default (포탑 사양, 전환 시간, 소리 없음).
        /// </summary>
        public static ItemRegistryElement Get(this DynamicBuffer<ItemRegistryElement> registry, ItemId id)
        {
            return registry.Contains(id) ? registry[(int)id] : default;
        }

        public static bool Contains(this DynamicBuffer<ItemRegistryElement> registry, ItemId id)
        {
            return id != ItemId.None
                   && (int)id < registry.Length
                   && registry[(int)id].Sprite != default(UnityObjectRef<Sprite>);
        }

        /// <summary>
        /// 포탑의 muzzle 번째 총구 사양. 포탑이 아니거나 그 총구 사양이 없으면 default (쏘지 않음).
        /// </summary>
        public static TurretSpecElement GetTurretSpec(
            this DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret,
            int muzzle)
        {
            if (!registry.Contains(turret))
            {
                return default;
            }

            ItemRegistryElement item = registry[(int)turret];
            return muzzle < item.TurretSpecCount ? specs[item.TurretSpecStart + muzzle] : default;
        }
    }
}
