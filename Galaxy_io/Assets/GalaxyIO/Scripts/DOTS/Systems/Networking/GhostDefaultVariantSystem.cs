using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
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
        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            // 렌더링용 Companion 은 클라이언트에만 둠.
            // 서버에서는 스폰마다 GameObject 를 복제하지 않음.
            defaultVariants.Add(ComponentType.ReadWrite<SpriteRenderer>(), Rule.ForAll(typeof(ClientOnlyVariant)));

            // 추가 Companion 타입(Light2D 등)도 같음. 서버에 남으면 Companion 없이 복제하다 예외가 남.
            foreach (Type type in CompanionTypes.Types)
            {
                defaultVariants.Add(ComponentType.ReadWrite(type), Rule.ForAll(typeof(ClientOnlyVariant)));
            }

            foreach (string typeName in CompanionTypes.LinkTypeNames)
            {
                defaultVariants.Add(
                    ComponentType.ReadWrite(Type.GetType(typeName, true)),
                    Rule.ForAll(typeof(ClientOnlyVariant))
                );
            }
        }
    }
}
