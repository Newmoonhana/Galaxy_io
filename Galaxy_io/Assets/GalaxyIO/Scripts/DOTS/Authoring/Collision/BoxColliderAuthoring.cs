using GalaxyIO.DOTS.Components.Actor;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 오브젝트와 함께 회전하는 직사각형 충돌체(OBB).
    /// </summary>
    public sealed class BoxColliderAuthoring : ColliderAuthoring
    {
        [SerializeField]
        [Tooltip("충돌체의 가로 / 세로 크기")]
        public Vector2 Size = Vector2.one;

        public override ColliderComponent ToComponent()
        {
            return new ColliderComponent
            {
                Shape   = ColliderShape.Obb,
                Center  = new float2(Center.x, Center.y),
                Extents = new float2(Size.x, Size.y) * 0.5F,
            };
        }

        // Scene 뷰 와이어프레임. 드래그 핸들은 BoxColliderAuthoringEditor가 그림.
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4F, 1.0F, 0.5F, 0.8F);
            Gizmos.DrawWireCube(Center, new Vector3(Size.x, Size.y, 0.0F));
        }
    }
}