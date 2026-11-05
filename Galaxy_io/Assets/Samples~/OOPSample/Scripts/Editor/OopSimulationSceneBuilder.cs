using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OOPSample.Editor
{
    /// <summary>
    /// ECS 샘플의 <c>SimulationSceneBuilder</c>와 짝을 이루는 OOP용 씬 빌더입니다.
    ///
    /// 흰색 머티리얼, 박스 프리팹 두 개, 카메라와 HUD와 러너가 들어 있는 메인 씬을 메뉴 한 번으로
    /// 다시 만듭니다. 전부 결정적이고 멱등하므로, 성능 비교를 다시 세팅할 때마다 손으로 만든
    /// 에셋을 뒤질 필요가 없습니다.
    ///
    /// ECS 쪽과 다른 점은 SubScene이 없다는 것입니다. 베이킹할 것이 없으니 오브젝트를 그냥
    /// 메인 씬에 두면 됩니다.
    /// </summary>
    public static class OopSimulationSceneBuilder
    {
        const string k_Root = "Assets/OOPSample";
        const string k_MaterialPath = k_Root + "/Materials/WhiteBox.mat";
        const string k_BotPrefabPath = k_Root + "/Prefabs/BotBox.prefab";
        const string k_PlayerPrefabPath = k_Root + "/Prefabs/PlayerBox.prefab";
        const string k_MainScenePath = k_Root + "/Scenes/OopSimulation.unity";
        const string k_InputActionsPath = k_Root + "/Assets/InputAction/SimControls.inputactions";

        const float k_WorldExtent = 1000f;

        [MenuItem("OOP Simulator/Build Simulation Scene", false, 0)]
        public static void Build()
        {
            EnsureFolders();

            // 여기서 만드는 임시 오브젝트가 전부 우리가 저장할 씬 안에 생기도록 빈 씬에서 시작합니다.
            var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var material = CreateWhiteMaterial();
            var botPrefab = CreateBoxPrefab<BotBehaviour>("BotBox", k_BotPrefabPath, material);
            var playerPrefab = CreateBoxPrefab<PlayerBehaviour>("PlayerBox", k_PlayerPrefabPath, material);

            CreateCamera();

            var runner = CreateRunner(botPrefab, playerPrefab);
            CreateHud(runner);

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene, k_MainScenePath);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[OOP Simulator] Built " + k_MainScenePath + ". Press Play.");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Materials", "Prefabs", "Scenes" })
                Directory.CreateDirectory(Path.Combine(k_Root, folder));

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// ECS 샘플과 같은 URP/Unlit 흰색 머티리얼입니다.
        ///
        /// <c>enableInstancing</c>이 여기서는 성능 비교의 핵심 변수입니다. ECS 쪽은 Entities
        /// Graphics(BatchRendererGroup)가 알아서 배칭하지만, GameObject 쪽에서 같은 메시와
        /// 머티리얼 수만 개를 그리려면 GPU 인스턴싱이 켜져 있어야 합니다. 꺼 두면 드로우콜에
        /// 파묻혀서 시뮬레이션 로직 비용은 재 보지도 못합니다.
        /// </summary>
        static Material CreateWhiteMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new FileNotFoundException("URP Unlit shader not found - is the project using URP?");

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, k_MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// 흰색 Unlit 머티리얼을 쓰는 1x1 쿼드. 메시가 1x1이므로 크기 조절은 곧
        /// <c>Transform.localScale</c>입니다(ECS 쪽 <c>LocalTransform.Scale</c>과 같은 자리).
        ///
        /// 렌더러 옵션을 ECS 프리팹과 동일하게 꺼 둡니다. 그림자, 라이트 프로브, 모션 벡터가
        /// 한쪽에만 켜져 있으면 비교가 무의미해집니다.
        /// </summary>
        static T CreateBoxPrefab<T>(string name, string path, Material material) where T : MonoBehaviour
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            temp.name = name;

            var collider = temp.GetComponent<MeshCollider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            var renderer = temp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            temp.AddComponent<T>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<T>();
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -100f);

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = k_WorldExtent * 1.06f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 5000f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        static SimulationRunner CreateRunner(BotBehaviour botPrefab, PlayerBehaviour playerPrefab)
        {
            var go = new GameObject("Simulation");
            var runner = go.AddComponent<SimulationRunner>();

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_InputActionsPath);
            if (inputActions == null)
                Debug.LogWarning("[OOP Simulator] Input actions not found at " + k_InputActionsPath);

            runner.ConfigureForBuild(
                botPrefab,
                playerPrefab,
                inputActions,
                new Vector2(-k_WorldExtent, -k_WorldExtent),
                new Vector2(k_WorldExtent, k_WorldExtent));

            EditorUtility.SetDirty(runner);
            return runner;
        }

        static void CreateHud(SimulationRunner runner)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateRect("Panel", canvasGo.transform, new Vector2(20f, -20f), new Vector2(340f, 236f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.86f);

            CreateLabel("Title", panel, font, "OOP BOX SIMULATOR", 17, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.95f), new Vector2(14f, -12f), new Vector2(312f, 24f));

            CreateLabel("MaxLabel", panel, font, "MAX BOT BOXES", 12, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.55f), new Vector2(14f, -46f), new Vector2(200f, 18f));

            var field = CreateInputField(panel, font, new Vector2(14f, -68f), new Vector2(196f, 32f));
            var button = CreateButton(panel, font, "APPLY", new Vector2(218f, -68f), new Vector2(108f, 32f));

            var stats = CreateLabel("Stats", panel, font, "starting...", 14, FontStyle.Normal,
                Color.white, new Vector2(14f, -112f), new Vector2(312f, 86f));
            stats.alignment = TextAnchor.UpperLeft;
            stats.lineSpacing = 1.15f;

            CreateLabel("Hint", panel, font, "WASD or arrow keys to move", 11, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.45f), new Vector2(14f, -206f), new Vector2(312f, 18f));

            var hud = canvasGo.AddComponent<SimulationHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("m_Runner").objectReferenceValue = runner;
            serialized.FindProperty("m_MaxBotsField").objectReferenceValue = field;
            serialized.FindProperty("m_ApplyButton").objectReferenceValue = button;
            serialized.FindProperty("m_StatsText").objectReferenceValue = stats;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // uGUI needs an EventSystem; with the new Input System that means the
            // InputSystemUIInputModule, which self-assigns the default UI actions on enable.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform CreateStretchedRect(string name, Transform parent, Vector2 padMin, Vector2 padMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = padMin;
            rect.offsetMax = padMax;
            return rect;
        }

        static Text CreateLabel(string name, Transform parent, Font font, string content, int fontSize,
            FontStyle style, Color color, Vector2 position, Vector2 size)
        {
            var rect = CreateRect(name, parent, position, size);
            return ConfigureText(rect.gameObject, font, content, fontSize, style, color, TextAnchor.MiddleLeft);
        }

        static Text ConfigureText(GameObject go, Font font, string content, int fontSize, FontStyle style,
            Color color, TextAnchor anchor)
        {
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static InputField CreateInputField(Transform parent, Font font, Vector2 position, Vector2 size)
        {
            var rect = CreateRect("MaxBotsField", parent, position, size);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.10f);

            var textRect = CreateStretchedRect("Text", rect, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            var text = ConfigureText(textRect.gameObject, font, string.Empty, 15, FontStyle.Normal,
                Color.white, TextAnchor.MiddleLeft);

            var placeholderRect = CreateStretchedRect("Placeholder", rect, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            var placeholder = ConfigureText(placeholderRect.gameObject, font, "1000", 15, FontStyle.Italic,
                new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);

            var field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = background;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 7;
            field.lineType = InputField.LineType.SingleLine;
            field.text = "1000";
            return field;
        }

        static Button CreateButton(Transform parent, Font font, string label, Vector2 position, Vector2 size)
        {
            var rect = CreateRect("ApplyButton", parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.20f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var textRect = CreateStretchedRect("Text", rect, Vector2.zero, Vector2.zero);
            ConfigureText(textRect.gameObject, font, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            return button;
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == k_MainScenePath);
            scenes.Add(new EditorBuildSettingsScene(k_MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
