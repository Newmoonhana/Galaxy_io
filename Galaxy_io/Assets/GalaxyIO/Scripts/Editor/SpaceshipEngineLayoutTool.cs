using System.Collections.Generic;
using System.IO;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Scripts.Managed.Effects;
using UnityEditor;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 기종별 엔진(노즐) 자리를 우주선 스프라이트에서 재서 <see cref="SpaceshipEngineLayout"/> 에셋에 채우는 도구.
    /// 스프라이트는 기수가 위(+y)라고 봄. 열마다 가장 아래(꼬리 쪽) 불투명 픽셀을 찾고,
    /// 꼬리 끝에서 가까운 열들이 이어진 덩어리를 노즐 하나로 봄.
    /// 결과가 어색한 기종은 에셋에서 손으로 고치면 됨(다시 재면 덮어씀).
    /// </summary>
    public static class SpaceshipEngineLayoutTool
    {
        private const string AssetPath = "Assets/GalaxyIO/Prefabs/VFX/SO_SpaceshipEngineLayout.asset";
        private const string PrefabFolder = "Assets/GalaxyIO/Prefabs/Spaceships";

        // 불투명으로 볼 알파
        private const byte AlphaThreshold = 40;

        // 꼬리 끝에서 이 비율(스프라이트 높이) 안쪽 열만 노즐 후보
        private const float RearBand = 0.07F;

        [MenuItem("GalaxyIO/VFX/엔진 위치 측정")]
        public static void Measure()
        {
            SpaceshipEngineLayout layout = AssetDatabase.LoadAssetAtPath<SpaceshipEngineLayout>(AssetPath);

            if (layout == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath) ?? "Assets");
                layout = ScriptableObject.CreateInstance<SpaceshipEngineLayout>();
                AssetDatabase.CreateAsset(layout, AssetPath);
            }

            layout.Entries.Clear();
            var report = new System.Text.StringBuilder("[EngineLayout]");

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                SpaceshipAuthoring authoring = prefab != null ? prefab.GetComponent<SpaceshipAuthoring>() : null;
                SpriteRenderer body = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;

                if (authoring == null || body == null || body.sprite == null)
                {
                    continue;
                }

                SpaceshipId kind = authoring.Kind;

                // 기종이 없는 것(위성, 함재기, AI 적)은 플레이어 우주선 이펙트 대상이 아님.
                if (kind == SpaceshipId.None)
                {
                    continue;
                }

                List<SpaceshipEngineLayout.Engine> engines = MeasureSprite(body.sprite, prefab.transform.localScale);

                // 같은 함선에서 가장 큰 노즐의 35% 보다 작은 조각은 노즐로 보지 않음.
                float largest = 0.0F;

                foreach (SpaceshipEngineLayout.Engine engine in engines)
                {
                    largest = Mathf.Max(largest, engine.Radius);
                }

                engines.RemoveAll(engine => engine.Radius < largest * 0.35F);

                if (engines.Count == 0)
                {
                    continue;
                }

                layout.Entries.Add(new SpaceshipEngineLayout.Entry { Ship = kind, Engines = engines });
                report.Append($" {kind}:{engines.Count}");
            }

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        private static List<SpaceshipEngineLayout.Engine> MeasureSprite(Sprite sprite, Vector3 scale)
        {
            var engines = new List<SpaceshipEngineLayout.Engine>();
            // 아틀라스로 묶인 스프라이트는 texture 가 아틀라스라서, 스프라이트가 들어 있는 원본 이미지 경로를 씀.
            string path = AssetDatabase.GetAssetPath(sprite);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return engines;
            }

            // 가져오기 설정과 관계없이 원본 이미지 픽셀을 읽음.
            var source = new Texture2D(2, 2);

            if (!source.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(source);
                return engines;
            }

            Rect rect = sprite.rect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            Color32[] pixels = source.GetPixels32();
            int stride = source.width;

            // 열마다 가장 아래 불투명 픽셀(없으면 -1)
            var bottom = new int[width];
            int lowest = int.MaxValue;

            for (int x = 0; x < width; x++)
            {
                bottom[x] = -1;

                for (int y = 0; y < height; y++)
                {
                    if (pixels[(y0 + y) * stride + x0 + x].a >= AlphaThreshold)
                    {
                        bottom[x] = y;
                        lowest = Mathf.Min(lowest, y);
                        break;
                    }
                }
            }

            Object.DestroyImmediate(source);

            if (lowest == int.MaxValue)
            {
                return engines;
            }

            int band = Mathf.Max(1, Mathf.RoundToInt(height * RearBand));
            float pixelsPerUnit = sprite.pixelsPerUnit;
            Vector2 pivot = sprite.pivot;

            // 꼬리 끝 근처 열들의 이어진 덩어리 = 노즐
            int start = -1;

            for (int x = 0; x <= width; x++)
            {
                bool rear = x < width && bottom[x] >= 0 && bottom[x] <= lowest + band;

                if (rear && start < 0)
                {
                    start = x;
                }
                else if (!rear && start >= 0)
                {
                    int end = x - 1;
                    int deepest = int.MaxValue;

                    for (int i = start; i <= end; i++)
                    {
                        deepest = Mathf.Min(deepest, bottom[i]);
                    }

                    float centerX = (start + end + 1) * 0.5F;
                    float radius = Mathf.Max(1.0F, (end - start + 1) * 0.5F);

                    engines.Add(new SpaceshipEngineLayout.Engine
                    {
                        Position = new Vector2(
                            (centerX - pivot.x) / pixelsPerUnit * scale.x,
                            (deepest - pivot.y) / pixelsPerUnit * scale.y
                        ),
                        Radius = Mathf.Min(radius, height * 0.08F) / pixelsPerUnit * Mathf.Abs(scale.x),
                    });

                    start = -1;
                }
            }

            return engines;
        }
    }
}
