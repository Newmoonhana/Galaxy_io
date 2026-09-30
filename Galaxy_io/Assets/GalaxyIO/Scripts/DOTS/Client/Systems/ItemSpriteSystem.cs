using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 아이템 Entity 의 Companion 스프라이트를 아이템 종류에 맞게 바꾸고,
    /// 묶음 개수만큼(최대 <see cref="MaxLayers"/>장) 스프라이트를 겹쳐 그리는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ItemSpriteSystem : ISystem
    {
        private const int MaxLayers = 4;

        // 뒷장이 앞장에서 비껴나는 정도. 스프라이트 크기 대비 비율.
        private static readonly Vector2 LayerOffset = new Vector2(0.12F, 0.12F);

        // 뒷장으로 갈수록 한 장마다 곱하는 밝기
        private const float LayerShade = 0.8F;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<ItemComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);

            foreach (
                var (item, renderer) in
                SystemAPI.Query<RefRO<ItemComponent>, SystemAPI.ManagedAPI.UnityEngineComponent<SpriteRenderer>>()
            ) {
                if (!registry.Contains(item.ValueRO.Id))
                {
                    continue;
                }

                Sprite sprite = registry[(int)item.ValueRO.Id].Sprite;
                SpriteRenderer front = renderer.Value;

                if (front.sprite != sprite)
                {
                    front.sprite = sprite;
                }

                // 아이템 종류와 개수는 스폰 후 바뀌지 않으므로 뒷장은 한 번만 만듦.
                int backLayers = math.clamp(item.ValueRO.Count, 1, MaxLayers) - 1;
                Transform root = front.transform;

                if (backLayers == 0 || root.childCount > 0)
                {
                    continue;
                }

                Vector2 offset = Vector2.Scale(sprite.bounds.size, LayerOffset);

                for (int i = 1; i <= backLayers; i++)
                {
                    var layer = new GameObject("ItemLayer").AddComponent<SpriteRenderer>();
                    layer.gameObject.layer = root.gameObject.layer;
                    layer.transform.SetParent(root, false);
                    layer.transform.localPosition = offset * i;

                    float shade = math.pow(LayerShade, i);

                    layer.sprite         = sprite;
                    layer.sharedMaterial = front.sharedMaterial;
                    layer.sortingLayerID = front.sortingLayerID;
                    layer.sortingOrder   = front.sortingOrder - i;
                    layer.color          = new Color(shade, shade, shade, front.color.a);
                }
            }
        }
    }
}
