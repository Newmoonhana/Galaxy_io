using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="StraightforwardProjectileAuthoring"/>의 DOTS 구조체. 공통된 Projectile의 정보 함유.
    /// </summary>
    public struct SharedProjectileComponent : IComponentData
    {
        // 남은 생명 주기
        public float Lifetime;

        // 공격력. 발사할 때 포탑 사양에서 채움.
        public float Damage;

        // 남은 관통력. 명중할 때마다 우주선 두께만큼 줄고 0 이하가 되면 사라짐.
        // 서버와 클라이언트(명중 연출)가 각자 깎음.
        public float Pierce;

        // 0보다 크면 명중으로 사라질 때 이 반경으로 폭발함.
        public float ExplosionRadius;

        // 명중할 때마다 클라이언트가 명중 지점에 띄울 연출. None 이면 없음.
        public PrefabId SpawnOnHit;

        // 서버가 데미지를 준 명중 수. 클라이언트는 늘어난 것을 보고 명중 연출을 띄움.
        [GhostField]
        public byte HitCount;

        // 마지막 명중 지점
        [GhostField(Quantization = 100)]
        public float2 HitPosition;

        // 서버가 명중으로 끝낸 투사체. 판정에서 빠지고 클라이언트는 감춤.
        // 마지막 명중이 스냅샷에 실리도록 잠시 남겨 두었다가 지움.
        [GhostField]
        public bool Dead;

        // 클라이언트가 연출을 띄운 명중 수. 서버에서는 쓰지 않음.
        public byte PresentedHitCount;

        // 이 투사체를 발사한 우주선. 자기가 쏜 탄에 맞지 않도록 판정에서 건너뜀.
        // 클라이언트 명중 연출에서도 건너뛰도록 복제함.
        [GhostField]
        public Entity Owner;

        // 발사한 총구 번호. 클라이언트가 Owner 의 장착 포탑에서 발사음을 찾는 데 씀.
        [GhostField]
        public byte TurretIndex;

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