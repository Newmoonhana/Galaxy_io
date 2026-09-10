using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components
{
    /// <summary>
    /// <see cref="StraightforwardProjectileAuthoring"/>의 DOTS 구조체. 공통된 Projectile의 정보 함유.
    /// </summary>
    public struct SharedProjectileComponent : IComponentData
    {
        // 남은 생명 주기
        public float Lifetime;

        // 공격력
        public float Damage;

        // 이 투사체를 발사한 우주선. 자기가 쏜 탄에 맞지 않도록 판정에서 건너뜀.
        public Entity Owner;

        // 직전 틱 이동 변위. 고속 투사체가 표적을 통과하지 않도록 충돌체를 늘리는 데 씀.
        // 이동 시스템이 매 틱 기록함.
        public float2 Displacement;

        // 발사한 클라이언트의 보간 지연(틱). 서버가 그 시점 우주선 위치로 되감아 판정함.
        // 클라이언트에서는 항상 0임.
        public uint InterpolationDelay;

        // 클라이언트가 직전 프레임에 판정한 위치. 그 사이 지나간 구간을 훑는 데 씀.
        public float2 LastTestedPosition;

        // LastTestedPosition이 채워졌는지. 스폰 첫 프레임에는 훑을 구간이 없음.
        public bool Tested;

        // 클라이언트가 명중으로 보고 감췄는지. 한 번 감추면 서버가 지울 때까지 그대로 둠.
        // 서버에서는 항상 false.
        public bool Hidden;
    }
}