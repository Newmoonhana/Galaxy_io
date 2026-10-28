using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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

        // 뒷장이 앞장에서 비껴나는 정도. 스프라이트 크기 대비 비율. 오른쪽 아래로 쌓임.
        private static readonly Vector2 LayerOffset = new Vector2(0.125F, -0.125F);

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
                SystemAPI.Query<RefRO<ItemComponent>, RefRO<CompanionComponent<SpriteRenderer>>>()
            ) {
                if (!registry.Contains(item.ValueRO.Id))
                {
                    continue;
                }

                Sprite sprite = registry[(int)item.ValueRO.Id].Sprite;
                SpriteRenderer front = renderer.ValueRO.CompanionRef.Value;

                if (front.sprite != sprite)
                {
                    front.sprite = sprite;
                }

                // 일부만 주워지면 개수가 줄어듦. 장 수가 달라질 때만 뒷장을 다시 만듦.
                int backLayers = math.clamp(item.ValueRO.Count, 1, MaxLayers) - 1;
                Transform root = front.transform;

                if (root.childCount == backLayers)
                {
                    continue;
                }

                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(root.GetChild(i).gameObject);
                }

                // 뒷장 정렬 순서가 배경보다 낮아져도 묶음 전체는 앞장 순서로 그려지게 함.
                if (!root.TryGetComponent(out SortingGroup _))
                {
                    root.gameObject.AddComponent<SortingGroup>();
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
