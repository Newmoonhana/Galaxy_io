using GalaxyIO.Components;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.Authoring
{
    public sealed class ProjectileAuthoring : MonoBehaviour
    {
        // 초기 설정값 자체가 Max Lifetime이며, 0이 되면 파괴.
        [SerializeField]
        public float Lifetime;

        [SerializeField]
        public float Damage;
    }

    public sealed class ProjectileBaker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None); // PlayerControl은 단순한 싱글톤 객체이므로 None.

            AddComponent(entity, new ProjectileComponent
            {
                Lifetime = authoring.Lifetime,
                Damage = authoring.Damage
            });
        }
    }
}