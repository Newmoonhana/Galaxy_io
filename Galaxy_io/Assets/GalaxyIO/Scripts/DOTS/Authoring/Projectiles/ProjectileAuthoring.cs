using System.ComponentModel;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 투사체 공통 설정값. 구현 클래스는 <see cref="Baker{TAuthoringType}"/>로
    /// DOTS 컴포넌트를 생성함.
    /// </summary>
    public abstract class ProjectileAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("투사체 지속시간")]
        public float Lifetime;

        [SerializeField]
        [Tooltip("관통력. 맞힌 우주선의 Thickness 만큼 줄고 0 이하가 되면 사라짐. 0이면 첫 명중에 사라짐.")]
        [Min(0.0F)]
        public float Pierce;

        [SerializeField]
        [Tooltip("폭발 반경. 0보다 크면 명중으로 사라질 때 폭발함. 반경 안의 다른 우주선은 거리에 따라 줄어든 데미지를 받음.")]
        [Min(0.0F)]
        public float ExplosionRadius;

        [SerializeField]
        [Tooltip("명중할 때마다 클라이언트가 명중 지점에 띄울 연출(EffectAuthoring). 프리팹 레지스트리에 등록할 것. None 이면 없음.")]
        public PrefabId SpawnOnHit = PrefabId.None;

        [SerializeField]
        [Tooltip("클라이언트가 투사체에 붙여 재생할 연출(EffectAuthoring). 파티클은 재생 속도를 투사체 수명에 맞추고, 트레일은 투사체가 사라진 뒤 꼬리가 줄어들고 사라짐. 비우면 없음.")]
        public GameObject Visual;

        [SerializeField]
        [Tooltip("이 투사체를 쏘는 동안 클라이언트가 총구에서 뻗어 그릴 광선(BeamAuthoring). 길이는 사거리까지, 첫 우주선에서 멈춤. 비우면 없음.")]
        public GameObject Beam;
    }
}