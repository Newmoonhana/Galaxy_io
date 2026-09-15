using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 한 번 재생하고 사라지는 파티클 연출. <see cref="ParticleSystem"/>은 Companion 으로 베이킹됨.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class EffectAuthoring : MonoBehaviour
    {
    }

    public sealed class EffectBaker : Baker<EffectAuthoring>
    {
        public override void Bake(EffectAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            ParticleSystem.MainModule main = GetComponent<ParticleSystem>().main;

            // 방출이 끝난 뒤 마지막 파티클이 사라질 때까지 기다림.
            // startLifetime 은 상수 / 두 상수 모드만 지원.
            AddComponent(entity, new EffectLifetimeComponent
            {
                Remaining = main.duration + main.startLifetime.constantMax,
            });
        }
    }
}