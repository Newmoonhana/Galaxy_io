using System;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 서버 월드의 Ghost 가 아닌 투사체 프리팹에서 렌더링용 Companion 을 떼는 시스템. e.g. Bore, Raptor
    /// Ghost 프리팹은 <see cref="GhostDefaultVariantSystem"/>의 규칙대로 Netcode 가 뗌.
    /// 에디터의 Listen 서버에서 서버 투사체가 같이 그려지지 않고, 서버가 스폰마다 GameObject 를 복제하지 않음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ServerCompanionStripSystem : ISystem
    {
        private EntityQuery query;
        private ComponentTypeSet companions;

        public void OnCreate(ref SystemState state)
        {
            var types = new FixedList128Bytes<ComponentType> { ComponentType.ReadWrite<SpriteRenderer>() };

            foreach (Type type in CompanionTypes.Types)
            {
                types.Add(ComponentType.ReadWrite(type));
            }

            foreach (string typeName in CompanionTypes.LinkTypeNames)
            {
                types.Add(ComponentType.ReadWrite(Type.GetType(typeName, true)));
            }

            companions = new ComponentTypeSet(types);

            var any = new ComponentType[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                any[i] = types[i];
            }

            // 서브씬이 늦게 로드되면 그때 다시 실행됨.
            query = state.GetEntityQuery(new EntityQueryDesc
            {
                All     = new[] { ComponentType.ReadOnly<Prefab>(), ComponentType.ReadOnly<SharedProjectileComponent>() },
                Any     = any,
                None    = new[] { ComponentType.ReadOnly<GhostType>() },
                Options = EntityQueryOptions.IncludePrefab,
            });

            state.RequireForUpdate(query);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.RemoveComponent(query, companions);
        }
    }
}
