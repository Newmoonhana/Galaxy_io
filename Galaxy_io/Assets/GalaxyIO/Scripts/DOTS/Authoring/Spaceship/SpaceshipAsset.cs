using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 상점에서 살 수 있는 우주선 기종. <see cref="Config.SpaceshipRegistryAsset"/>에 등록해야 상점에 나타남.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Spaceship", menuName = "GalaxyIO/Spaceship")]
    public sealed class SpaceshipAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("코드와 네트워크가 이 기종을 가리키는 키. 프리팹의 SpaceshipAuthoring.Kind 와 같아야 함.")]
        public SpaceshipId Id;

        [SerializeField]
        public SpaceshipCategory Category;

        [SerializeField]
        [Tooltip("살 때 바꿔 탈 우주선. 비우면 상점에 보이기만 하고 살 수 없음.")]
        public SpaceshipAuthoring Prefab;

        [SerializeField]
        [Tooltip("UI 에 외곽선으로 그릴 스프라이트. 비우면 아무것도 그리지 않음.")]
        public Sprite Sprite;

        [SerializeField]
        [Tooltip("외곽선 셰이더의 Sensitivity. 1 에 가까울수록 작은 색 차이도 외곽선으로 봄.")]
        [Range(0.0F, 1.0F)]
        public float OutlineSensitivity = 0.5F;

        [SerializeField]
        [Tooltip("포탑 칸 위치. 스프라이트 사각형 기준 (0, 0) 좌하단 ~ (1, 1) 우상단. " +
                 "첫 칸이 플레이어가 쏘는 주 포탑, 나머지는 자동 포탑 칸.")]
        public List<Vector2> TurretSlots = new List<Vector2> { new Vector2(0.5F, 0.5F) };

        [SerializeField]
        public List<RecipeAsset.Ingredient> Ingredients = new List<RecipeAsset.Ingredient>();

        /// <summary>
        /// 크기 area 인 영역에 비율을 지켜 꽉 채워 그린 스프라이트 위의 slot 번째 포탑 칸 위치. 영역 중심 기준.
        /// </summary>
        public Vector2 TurretSlotIn(int slot, Vector2 area)
        {
            float scale = Mathf.Min(area.x / Sprite.rect.width, area.y / Sprite.rect.height);

            return (TurretSlots[slot] - new Vector2(0.5F, 0.5F)) * (Sprite.rect.size * scale);
        }
    }
}
