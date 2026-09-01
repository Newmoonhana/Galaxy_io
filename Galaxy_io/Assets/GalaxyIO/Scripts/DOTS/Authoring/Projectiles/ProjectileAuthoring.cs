using System.ComponentModel;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.Authoring
{
    /// <summary>
    /// 투사체 공통 설정값.
    /// <p/>
    /// 해당 클래스를 상속받는 모든 투사체 구현 클래스들은
    /// <see cref="Baker{TAuthoringType}"/>를 통해 DOTS의
    /// <see cref="IComponent"/>로 변환되어야 함.
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