using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 유니티가 기본 지원하지 않는 Companion 타입 등록. 둘 다 있어야 GetCompanion<T> 로 조회됨.
[assembly: RegisterUnityEngineComponentType(typeof(TrailRenderer))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<TrailRenderer>))]
[assembly: RegisterUnityEngineComponentType(typeof(Light2D))]
[assembly: RegisterGenericComponentType(typeof(CompanionComponent<Light2D>))]

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 추가 Companion 타입 목록. 위 어셈블리 속성, 아래 베이커와 같은 타입을 유지할 것.
    /// 베이킹 대상 등록은 에디터의 CompanionTypeRegistration 이 이 목록으로 함.
    /// </summary>
    public static class CompanionTypes
    {
        public static readonly Type[] Types = { typeof(TrailRenderer), typeof(Light2D) };

        // Companion GameObject 연결 컴포넌트. internal 타입이라 이름으로 찾음.
        public static readonly string[] LinkTypeNames =
        {
            "Unity.Entities.CompanionReference, Unity.Entities.Hybrid",
            "Unity.Entities.CompanionLink, Unity.Entities.Hybrid",
            "Unity.Entities.CompanionLinkTransform, Unity.Entities.Hybrid",
        };
    }

    // 타입 등록은 "남길 컴포넌트" 지정일 뿐. Entity 에 컴포넌트를 붙이는 베이커가 있어야 Companion 이 됨.

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

    /// <summary>
    /// 모든 <see cref="Light2D"/>를 Companion 으로 베이킹함.
    /// </summary>
    public sealed class Light2DBaker : Baker<Light2D>
    {
        public override void Bake(Light2D authoring)
        {
            AddComponentObject(GetEntity(TransformUsageFlags.Dynamic), authoring);
        }
    }
}
