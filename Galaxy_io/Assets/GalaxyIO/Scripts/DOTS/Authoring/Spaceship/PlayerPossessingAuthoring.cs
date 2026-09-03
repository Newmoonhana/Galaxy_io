using GalaxyIO.DOTS.Client.Components;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 이 오브젝트가 "플레이어에게 조종되고 있음"을 나타내는 컴포넌트
    /// </summary>
    public sealed class PlayerPossessingAuthoring : MonoBehaviour
    {
    }

    public sealed class PlayerPossessingBaker : Baker<PlayerPossessingAuthoring>
    {
        public override void Bake(PlayerPossessingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerInputComponent>(entity);
        }
    }
}