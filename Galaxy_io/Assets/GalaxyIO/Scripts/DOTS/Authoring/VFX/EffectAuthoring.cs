using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

// TrailRenderer 를 Companion 으로 쓰기 위한 타입 등록. Companion 생성 대상 등록은 CompanionTypeRegistration 이 맡음.
[assembly: RegisterUnityEngineComponentType(typeof(TrailRenderer))]

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 한 번 재생하고 사라지는 연출. 자신 또는 자식의 <see cref="ParticleSystem"/>, <see cref="TrailRenderer"/>를
    /// 여러 개 묶음. 각각 Companion 으로 베이킹됨.
    /// </summary>
    public sealed class EffectAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("연출을 이루는 ParticleSystemRenderer 또는 TrailRenderer. 자신 또는 자식의 것만 넣을 것.")]
        public List<Renderer> Effects = new List<Renderer>();

        [SerializeField]
        [Tooltip("연출과 함께 재생할 효과음. 비워두면 소리 없음.")]
        public string Sound;
    }

    public sealed class EffectBaker : Baker<EffectAuthoring>
    {
        public override void Bake(EffectAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            DynamicBuffer<EffectPartElement> parts = AddBuffer<EffectPartElement>(entity);
            float remaining = 0.0F;

            foreach (Renderer effect in authoring.Effects)
            {
                if (effect is ParticleSystemRenderer)
                {
                    // 방출이 끝난 뒤 마지막 파티클이 사라질 때까지 기다림.
                    // startLifetime 은 상수 / 두 상수 모드만 지원.
                    ParticleSystem.MainModule main = GetComponent<ParticleSystem>(effect).main;
                    remaining = Mathf.Max(remaining, main.duration + main.startLifetime.constantMax);
                }
                else if (effect is TrailRenderer)
                {
                    // 꼬리가 다 줄어들 때까지 기다림.
                    remaining = Mathf.Max(remaining, GetComponent<TrailRenderer>(effect).time);
                }
                else
                {
                    Debug.LogError($"{authoring.name}: EffectAuthoring.Effects 에는 ParticleSystemRenderer 또는 TrailRenderer 만 넣을 것.", authoring);
                    continue;
                }

                parts.Add(new EffectPartElement { Value = GetEntity(effect, TransformUsageFlags.Dynamic) });
            }

            AddComponent(entity, new EffectLifetimeComponent { Remaining = remaining });

            if (!string.IsNullOrEmpty(authoring.Sound))
            {
                AddComponent(entity, new EffectSoundComponent { SoundKey = authoring.Sound });
            }
        }
    }

    /// <summary>
    /// 모든 <see cref="TrailRenderer"/>를 Companion 으로 베이킹함. 파티클은 유니티 기본 베이커가 맡음.
    /// </summary>
    public sealed class TrailRendererBaker : Baker<TrailRenderer>
    {
        public override void Bake(TrailRenderer authoring)
        {
            AddComponentObject(GetEntity(TransformUsageFlags.Dynamic), authoring);
        }
    }
}
