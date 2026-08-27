using System.Collections.Generic;
using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GalaxyIO.Editor
{
    /// <summary>
    /// One-click regeneration of every authored asset the simulation needs: the white
    /// material, the two box prefabs, the SubScene that holds the authoring component, and
    /// the main scene with the camera and the HUD.
    ///
    /// Everything here is deterministic and idempotent, so the whole setup can be rebuilt
    /// from source at any time instead of being a pile of hand-made assets nobody can
    /// reproduce.
    /// </summary>
    public static class SimulationSceneBuilder
    {
        const string k_Root = "Assets/GalaxyIO";
        const string k_MaterialPath = k_Root + "/Materials/WhiteBox.mat";
        const string k_BotPrefabPath = k_Root + "/Prefabs/BotBox.prefab";
        const string k_PlayerPrefabPath = k_Root + "/Prefabs/PlayerBox.prefab";
        const string k_SubScenePath = k_Root + "/Scenes/SimulationSubScene.unity";
        const string k_MainScenePath = k_Root + "/Scenes/EcsSimulation.unity";

        const float k_WorldExtent = 1000f;

        [MenuItem("ECS Simulator/Build Simulation Scene", false, 0)]
        public static void Build()
        {
            EnsureFolders();

            // Start from a clean empty scene so that every temporary object created below
            // is created inside the scene we are about to author and save.
            var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var material = CreateWhiteMaterial();
            var botPrefab = CreateBoxPrefab("BotBox", k_BotPrefabPath, material, typeof(BotAuthoring));
            var playerPrefab = CreateBoxPrefab("PlayerBox", k_PlayerPrefabPath, material, typeof(PlayerAuthoring));

            CreateCamera();
            CreateHud();

            // Save before touching the sub scene: Unity refuses to open a scene additively
            // while an untitled scene is still unsaved.
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene, k_MainScenePath);

            CreateSubSceneAsset(botPrefab, playerPrefab);

            var subSceneGo = new GameObject("Simulation SubScene");
            var subScene = subSceneGo.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(k_SubScenePath);
            subScene.AutoLoadScene = true;

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene, k_MainScenePath);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ECS Simulator] Built " + k_MainScenePath + ". Press Play.");
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Materials", "Prefabs", "Scenes" })
                Directory.CreateDirectory(Path.Combine(k_Root, folder));

            AssetDatabase.Refresh();
        }

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
        /// A 1x1 quad with a white unlit material. Entities Graphics bakes the MeshFilter +
        /// MeshRenderer pair into MaterialMeshInfo / RenderBounds, so the entity draws with
        /// no extra work; scaling is then just <c>LocalTransform.Scale</c>.
        /// </summary>
        static GameObject CreateBoxPrefab(string name, string path, Material material, System.Type authoringType)
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

            temp.AddComponent(authoringType);

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -100f);

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            // A little larger than the world half-extent so the boundary is visible.
            camera.orthographicSize = k_WorldExtent * 1.06f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 5000f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        static void CreateHud()
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

            CreateLabel("Title", panel, font, "ECS BOX SIMULATOR", 17, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.95f), new Vector2(14f, -12f), new Vector2(312f, 24f));

            CreateLabel("MaxLabel", panel, font, "MAX BOT BOXES", 12, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.55f), new Vector2(14f, -46f), new Vector2(200f, 18f));

            var field = CreateInputField(panel, font, new Vector2(14f, -68f), new Vector2(196f, 32f));
            var button = CreateButton(panel, font, "APPLY", new Vector2(218f, -68f), new Vector2(108f, 32f));

            var stats = CreateLabel("Stats", panel, font, "streaming sub scene...", 14, FontStyle.Normal,
                Color.white, new Vector2(14f, -112f), new Vector2(312f, 86f));
            stats.alignment = TextAnchor.UpperLeft;
            stats.lineSpacing = 1.15f;

            CreateLabel("Hint", panel, font, "WASD or arrow keys to move", 11, FontStyle.Normal,
                new Color(1f, 1f, 1f, 0.45f), new Vector2(14f, -206f), new Vector2(312f, 18f));

            var hud = canvasGo.AddComponent<SimulationHud>();
            var serialized = new SerializedObject(hud);
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

        /// <summary>
        /// The SubScene is what makes baking happen: its contents are converted to entities
        /// ahead of time and streamed into the world at runtime.
        /// </summary>
        static void CreateSubSceneAsset(GameObject botPrefab, GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var go = new GameObject("Simulation");
            SceneManager.MoveGameObjectToScene(go, scene);

            var authoring = go.AddComponent<SimulationAuthoring>();
            authoring.BotPrefab = botPrefab;
            authoring.PlayerPrefab = playerPrefab;
            authoring.BoundsMin = new Vector2(-k_WorldExtent, -k_WorldExtent);
            authoring.BoundsMax = new Vector2(k_WorldExtent, k_WorldExtent);
            authoring.InitialMaxBotCount = 1000;
            authoring.HardMaxBotCount = 200000;
            authoring.MaxSpawnsPerFrame = 1000;
            authoring.BotSize = 12f;
            authoring.BotSpeedMin = 40f;
            authoring.BotSpeedMax = 200f;
            authoring.PlayerSize = 40f;
            authoring.PlayerSpeed = 600f;
            authoring.RandomSeed = 24601u;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, k_SubScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == k_MainScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(k_MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}