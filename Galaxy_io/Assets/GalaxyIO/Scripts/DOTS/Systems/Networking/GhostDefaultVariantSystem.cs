using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// Ghost 컴포넌트의 기본 Variant 를 지정하는 시스템.
    /// </summary>
    public sealed partial class GhostDefaultVariantSystem : DefaultVariantSystemBase
    {
        // Companion GameObject 연결 컴포넌트. internal 타입이라 이름으로 찾음.
        private static readonly string[] CompanionLinkTypeNames =
        {
            "Unity.Entities.CompanionReference, Unity.Entities.Hybrid",
            "Unity.Entities.CompanionLink, Unity.Entities.Hybrid",
            "Unity.Entities.CompanionLinkTransform, Unity.Entities.Hybrid",
        };

        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            // 렌더링용 Companion 은 클라이언트에만 둠.
            // 서버에서는 스폰마다 GameObject 를 복제하지 않음.
            defaultVariants.Add(ComponentType.ReadWrite<SpriteRenderer>(), Rule.ForAll(typeof(ClientOnlyVariant)));

            foreach (string typeName in CompanionLinkTypeNames)
            {
                defaultVariants.Add(
                    ComponentType.ReadWrite(Type.GetType(typeName, true)),
                    Rule.ForAll(typeof(ClientOnlyVariant))
                );
            }
        }
    }
}
