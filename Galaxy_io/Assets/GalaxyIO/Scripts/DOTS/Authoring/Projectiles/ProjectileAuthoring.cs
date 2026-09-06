using System.ComponentModel;
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
        [Tooltip("투사체 공격력")]
        public float Damage;
    }
}