using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Effects
{
    /// <summary>
    /// 이펙트 사각형을 한 메쉬에 모아 한 번에 그리는 도우미. 매 프레임 Begin -> Add... -> End.
    /// 텍스처는 <see cref="FxAtlas"/> 의 칸(UV 사각형)을 씀.
    /// </summary>
    public sealed class FxQuadBatch
    {
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<Vector2> uvs = new List<Vector2>(1024);
        private readonly List<Color> colors = new List<Color>(1024);
        private readonly List<int> triangles = new List<int>(1536);

        public FxQuadBatch(string name)
        {
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
        }

        public Mesh Mesh => mesh;

        public int Count => vertices.Count / 4;

        public void Begin()
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        /// <summary>
        /// 가운데, 크기(너비, 높이), 방향(위쪽 = forward), UV 칸, 색. 높이 방향이 forward.
        /// pivotY 0.5 면 가운데, 1 이면 위쪽 끝이 center(불꽃처럼 뒤로 뻗는 모양).
        /// </summary>
        public void Add(Vector2 center, Vector2 size, Vector2 forward, Rect uv, Color color, float pivotY = 0.5F)
        {
            Vector2 up = forward.sqrMagnitude > 1e-6F ? forward.normalized : Vector2.up;
            Vector2 right = new Vector2(up.y, -up.x);
            Vector2 halfRight = right * (size.x * 0.5F);
            Vector2 top = up * (size.y * (1.0F - pivotY));
            Vector2 bottom = -up * (size.y * pivotY);

            int index = vertices.Count;
            vertices.Add(center + bottom - halfRight);
            vertices.Add(center + top - halfRight);
            vertices.Add(center + top + halfRight);
            vertices.Add(center + bottom + halfRight);

            uvs.Add(new Vector2(uv.xMin, uv.yMin));
            uvs.Add(new Vector2(uv.xMin, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMin));

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);

            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);

            // 화면 어디서나 그려지도록 넉넉한 경계. 카메라가 컬링하지 않게 함.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000.0F);
        }
    }

    /// <summary>
    /// 이펙트용 픽셀 텍스처 아틀라스. 코드로 만듦(외부 이미지 없음).
    /// 배경과 같은 픽셀 아트 느낌이 나도록 낮은 해상도에 단계진 밝기, Point 필터.
    /// 칸: 0 둥근 빛, 1 불꽃(위가 밝고 아래로 가늘어짐), 2 줄무늬(먼지 꼬리), 3 고리(충격파), 4 꽉 찬 점(불똥, 파편).
    /// </summary>
    public static class FxAtlas
    {
        public const int Cell = 32;
        public const int Cells = 5;

        private static Texture2D texture;

        public static Texture2D Texture
        {
            get
            {
                if (texture == null)
                {
                    texture = Build();
                }

                return texture;
            }
        }

        public static Rect Glow => Uv(0);
        public static Rect Flame => Uv(1);
        public static Rect Streak => Uv(2);
        public static Rect Ring => Uv(3);
        public static Rect Dot => Uv(4);

        private static Rect Uv(int cell)
        {
            // 칸 경계에서 이웃 칸이 번지지 않게 반 픽셀 안쪽.
            float inset = 0.5F / (Cell * Cells);
            return new Rect((float)cell / Cells + inset, inset, 1.0F / Cells - inset * 2.0F, 1.0F - inset * 2.0F);
        }

        // 밝기를 몇 단계로 끊어 픽셀 아트처럼 보이게 함.
        private static float Step(float value, int steps)
        {
            return Mathf.Ceil(Mathf.Clamp01(value) * steps) / steps;
        }

        private static Texture2D Build()
        {
            var atlas = new Texture2D(Cell * Cells, Cell, TextureFormat.RGBA32, false)
            {
                name = "FxAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Cell * Cells * Cell];
            float half = Cell * 0.5F;

            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    float u = (x + 0.5F) / Cell;
                    float v = (y + 0.5F) / Cell;
                    float d = Vector2.Distance(new Vector2(x + 0.5F, y + 0.5F), new Vector2(half, half)) / half;

                    // 0 둥근 빛: 가운데 하얗고 가장자리로 4 단계
                    float glow = Step(Mathf.Pow(Mathf.Clamp01(1.0F - d), 1.6F), 4);

                    // 1 불꽃: 위(v=1)에서 넓고 밝게, 아래로 가늘어짐
                    float width = Mathf.Lerp(0.05F, 0.5F, v);
                    float across = Mathf.Abs(u - 0.5F) / Mathf.Max(width, 0.001F);
                    float flame = Step(Mathf.Clamp01(1.0F - across) * Mathf.Pow(v, 0.8F), 4);

                    // 2 줄무늬: 가로 가운데 얇은 선, 위로 갈수록 밝음(머리)
                    float streakAcross = Mathf.Abs(u - 0.5F) < 0.18F ? 1.0F : 0.0F;
                    float streak = Step(streakAcross * v, 3);

                    // 3 고리
                    float ring = Mathf.Abs(d - 0.82F) < 0.12F ? 1.0F : Mathf.Abs(d - 0.82F) < 0.2F ? 0.4F : 0.0F;

                    // 4 꽉 찬 점
                    float dot = d < 0.6F ? 1.0F : d < 0.85F ? 0.5F : 0.0F;

                    float[] cells = { glow, flame, streak, ring, dot };

                    for (int c = 0; c < Cells; c++)
                    {
                        pixels[y * Cell * Cells + c * Cell + x] = new Color32(255, 255, 255, (byte)(cells[c] * 255));
                    }
                }
            }

            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            return atlas;
        }
    }
}
