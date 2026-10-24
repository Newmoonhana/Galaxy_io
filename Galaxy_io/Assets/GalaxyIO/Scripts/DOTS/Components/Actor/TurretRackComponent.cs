using GalaxyIO.DOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="TurretRackAuthoring"/>의 DOTS 구조체. 조종 포탑의 장전된 투사체를 우주선 위에 그리는 발사대.
    /// 장전 상태는 모든 클라이언트에 복제함.
    /// </summary>
    public struct TurretRackComponent : IComponentData
    {
        // 첫 총구 쿨다운이 이만큼(초) 남으면 장전된 것으로 봄.
        public float ShowTime;

        // 클라이언트만. 숨은 자리에서 다 나오기까지 걸리는 시간(초).
        public float SlideTime;

        // 장전된 총구 비트. i 번 비트가 i 번 총구.
        [GhostField]
        public byte Loaded;
    }

    /// <summary>
    /// 발사대에 그리는 투사체 하나. 클라이언트만 씀.
    /// </summary>
    public struct TurretRackElement : IBufferElementData
    {
        // 투사체 그림(SpriteRenderer) Entity. 우주선의 자식.
        public Entity Sprite;

        // 이 그림이 나타내는 총구 번호
        public int Muzzle;

        // 다 나온 자리, 숨은 자리(로컬)
        public float3 Shown;
        public float3 Hidden;

        // 꺼내기 전 기다리는 시간. SlideTime 단위.
        public float Delay;

        // 나온 정도. 0 이하 숨음 ~ 1 다 나옴. 감추면 -Delay 부터 다시 셈.
        public float Progress;
    }

    /// <summary>
    /// 발사대 장전 규칙. 서버, 예측하는 클라이언트가 같이 씀.
    /// </summary>
    public static class TurretRack
    {
        /// <summary>
        /// 쿨다운으로 장전된 총구 비트를 구함. 첫 총구가 곧 쏠 수 있으면 모든 총구가 장전됨.
        /// 첫 총구가 쏜 뒤에는 아직 쏘지 않은 뒤 총구만 장전된 채로 남음.
        /// </summary>
        public static byte Loaded(NativeArray<float> cooldowns, float showTime)
        {
            if (cooldowns.Length == 0)
            {
                return 0;
            }

            bool ready = cooldowns[0] <= showTime;
            int loaded = 0;

            for (int i = 0; i < math.min(cooldowns.Length, 8); i++)
            {
                if (ready || (i > 0 && cooldowns[i] != TurretCycle.Waiting))
                {
                    loaded |= 1 << i;
                }
            }

            return (byte)loaded;
        }
    }
}
