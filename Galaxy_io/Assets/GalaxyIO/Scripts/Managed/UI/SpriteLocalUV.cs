using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// 스프라이트 기준 0~1 로컬 UV를 UV1에 기록하는 메시 이펙트. 아틀라스 패킹된 스프라이트에서 보조 텍스처 샘플용.
    [RequireComponent(typeof(Image))]
    public sealed class SpriteLocalUV : BaseMeshEffect
    {
        Image _image;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            if (_image == null) _image = GetComponent<Image>();
            Sprite sprite = _image.overrideSprite;
            if (sprite == null) return;

            // 아틀라스 내 스프라이트 영역 (xMin, yMin, xMax, yMax).
            Vector4 outer = DataUtility.GetOuterUV(sprite);
            Vector2 size = new(outer.z - outer.x, outer.w - outer.y);
            if (size.x <= 0f || size.y <= 0f) return;

            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv1 = new Vector4((v.uv0.x - outer.x) / size.x, (v.uv0.y - outer.y) / size.y, 0f, 0f);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
