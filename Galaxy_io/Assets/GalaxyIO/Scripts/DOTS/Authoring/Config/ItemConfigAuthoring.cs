using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 아이템 공통 설정. 월드당 한 개.
    /// </summary>
    public sealed class ItemConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("아이템을 주웠을 때 재생할 효과음. 비워두면 소리 없음.")]
        public AudioClip PickupSound;
    }

    public sealed class ItemConfigBaker : Baker<ItemConfigAuthoring>
    {
        public override void Bake(ItemConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new ItemConfigComponent
            {
                PickupSound = authoring.PickupSound,
            });
        }
    }
}
