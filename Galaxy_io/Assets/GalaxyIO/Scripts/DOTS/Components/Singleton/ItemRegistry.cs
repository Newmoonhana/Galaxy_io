using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="ItemId"/>로 월드 아이템 스프라이트와 포탑 사양을 찾는 레지스트리. 월드당 한 개.
    /// 버퍼 인덱스가 곧 <see cref="ItemId"/> 값임. 나머지 정의는 <see cref="ItemRegistryAssetComponent"/>에서 찾음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ItemRegistryElement : IBufferElementData
    {
        // 월드에 떨어진 아이템 Entity 의 스프라이트. 빈 칸이면 null.
        public UnityObjectRef<Sprite> Sprite;

        // 스프라이트에 맞춘 줍기 충돌체 (반너비, 반높이)
        public float2 HalfExtents;

        // 포탑만. 우주선의 모든 총구에 적용하는 사양. 포탑이 아니면 default (쏘지 않음).
        public TurretSpecData Turret;

        // 포탑만. 자동 포탑 칸에 장착했을 때의 사양.
        public AutoTurretSpecData AutoTurret;

        // 포탑만. 사격 시작/중지 전환 시간(초).
        public float SwitchTime;

        // 포탑만. 사격 중 우주선 회전속도가 줄어드는 비율(0 ~ 1).
        public float ManeuverPenalty;

        // 포탑만. 쌓이는 열과 방식.
        public float Heat;
        public TurretHeatMode HeatMode;

        // 포탑만. 사격 시작음, 사격 중 반복음. 클라이언트만 씀.
        public FixedString64Bytes LaunchSound;
        public FixedString64Bytes LoopSound;
    }

    /// <summary>
    /// 포탑 아이템의 사양. 우주선의 모든 총구가 같은 사양으로 쏨.
    /// </summary>
    public struct TurretSpecData
    {
        // 없으면 쏘지 않음.
        public Entity ProjectilePrefab;

        // 투사체가 Ghost 가 아님. 서버는 복제 없이 데미지 판정용으로만 쏘고,
        // 클라이언트는 사격 전환 상태를 보고 연출용으로 따로 쏨.
        public bool LocalLaunch;

        // 첫 총구의 발사 간격(초)
        public float MaxFireCooldown;

        // 첫 총구가 쏜 뒤 다음 총구가 차례로 쏘는 간격(초). 0 이면 모든 총구가 함께 쏨.
        public float MuzzleDelay;

        // 총구 하나가 한 번에 쏘는 투사체 수. 1 이상.
        public byte ProjectileCount;

        // 탄 퍼짐 정도(Radian)
        public float Spread;

        // 속력 분포. 발사 속력에 1 ± 이 값 사이의 배율을 곱함.
        public float SpeedSpread;

        // 투사체 공격력
        public float Damage;

        public FixedString64Bytes ShotSound;
    }

    /// <summary>
    /// 포탑 아이템을 자동 포탑으로 쓸 때의 사양. 발사는 <see cref="TurretSpecData"/>를 따름.
    /// </summary>
    public struct AutoTurretSpecData
    {
        // 한 번에 사격하는 시간(초)
        public float FireDuration;

        // 사격 후 휴식 시간 추출 범위(초). (최소, 최대)
        public float2 RestDurationRange;

        // 대상을 향해 도는 속도(라디안/초)
        public float TurningSpeed;

        // 시야각 절반(라디안). 처음 방향에서 양쪽으로 이만큼 돔.
        public float HalfSightAngle;

        // 대상을 찾는 거리. 대상 선체 바깥까지.
        public float DetectRange;
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
    }
}
