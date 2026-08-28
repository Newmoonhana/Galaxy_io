using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// 봇 Prefab => Entity 변환기
    ///
    /// 봇 Prefab에는 `MeshFilter`와 `MeshRenderer` 컴포넌트가 소속되어 있고 이들은 Entities Graphics
    /// 패키지를 통해 자동으로 Baking됨. `BotBaker`는 순수 데이터만 변환함.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Bot Authoring")]
    [DisallowMultipleComponent]
    public class BotAuthoring : MonoBehaviour { }

    public class BotBaker : Baker<BotAuthoring>
    {
        public override void Bake(BotAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BotTag>(entity);
            AddComponent(entity, new Velocity { Value = float2.zero });
            AddComponent(entity, new BoxSize { Value = 1f });
        }
    }
}