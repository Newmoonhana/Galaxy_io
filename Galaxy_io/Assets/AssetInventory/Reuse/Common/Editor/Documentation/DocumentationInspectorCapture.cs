using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ImpossibleRobert.Common.Editor.Documentation
{
    public sealed class DocumentationInspectorCaptureTarget
    {
        public Object InspectedObject { get; }
        public Object Owner { get; }

        public DocumentationInspectorCaptureTarget(Object inspectedObject, Object owner)
        {
            InspectedObject = inspectedObject != null
                ? inspectedObject
                : throw new ArgumentNullException(nameof(inspectedObject));
            Owner = owner != null ? owner : inspectedObject;
        }
    }

    public sealed class DocumentationInspectorCaptureSpec
    {
        public string Name { get; }
        public string OutputPath { get; }
        public Func<DocumentationInspectorCaptureTarget> CreateTarget { get; }

        public DocumentationInspectorCaptureSpec(
            string name,
            string outputPath,
            Func<DocumentationInspectorCaptureTarget> createTarget)
        {
            Name = !string.IsNullOrWhiteSpace(name)
                ? name
                : throw new ArgumentException("A capture name is required.", nameof(name));
            OutputPath = IsProjectAssetPath(outputPath)
                ? outputPath.Replace('\\', '/')
                : throw new ArgumentException("The output path must begin with Assets/.", nameof(outputPath));
            CreateTarget = createTarget ?? throw new ArgumentNullException(nameof(createTarget));
        }

        static bool IsProjectAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal);
        }
    }

    public sealed class DocumentationInspectorCaptureOptions
    {
        static readonly string[] DefaultEditorStyleSheetNames =
        {
            "common",
            "DefaultCommonDark_inter.uss",
            "dark",
            "Dark",
            "InspectorWindow",
            "CommonInspector",
            "DocumentationInspectorCapture"
        };

        public int Width { get; set; } = 720;
        public int Height { get; set; } = 4096;
        public float HorizontalPadding { get; set; } = 12f;
        public float VerticalPadding { get; set; } = 12f;
        public float SurfacePadding { get; set; } = 10f;
        public float SurfaceCornerRadius { get; set; } = 8f;
        public int TransparentCropPadding { get; set; } = 4;
        public Color BackgroundColor { get; set; } = Color.clear;
        public Color SurfaceColor { get; set; } = new Color(0.125f, 0.133f, 0.149f, 1f);
        public Color SurfaceBorderColor { get; set; } = new Color(0.27f, 0.29f, 0.33f, 1f);
        public string ThemeAssetName { get; set; } = "DocumentationCaptureRuntimeTheme";
        public IReadOnlyList<string> EditorStyleSheetNames { get; set; } = DefaultEditorStyleSheetNames;
    }

    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Static-constructor state is code-load scoped; cleanup is handled explicitly where the type crosses Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class DocumentationInspectorCaptureCoordinator
    {
        const string PendingSessionKey = "ImpossibleRobert.Common.DocumentationInspectorCapture.Pending";
        const string RunningSessionKey = "ImpossibleRobert.Common.DocumentationInspectorCapture.Running";

        static readonly Dictionary<string, Registration> Registrations =
            new Dictionary<string, Registration>(StringComparer.Ordinal);

        static DocumentationInspectorCaptureCoordinator()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.delayCall += ResumeIfNeeded;
        }

        public static void Register(
            string id,
            string displayName,
            Func<IReadOnlyList<DocumentationInspectorCaptureSpec>> createSpecs,
            DocumentationInspectorCaptureOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable capture-set ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", nameof(displayName));
            if (createSpecs == null)
                throw new ArgumentNullException(nameof(createSpecs));

            Registrations[id] = new Registration(
                displayName,
                createSpecs,
                options ?? new DocumentationInspectorCaptureOptions());
            EditorApplication.delayCall += ResumeIfNeeded;
        }

        public static void Start(string id)
        {
            if (!Registrations.ContainsKey(id))
                throw new InvalidOperationException("No documentation inspector capture set is registered as " + id + ".");

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before rebuilding documentation inspector screenshots.");
                return;
            }

            SessionState.SetString(PendingSessionKey, id);
            SessionState.SetBool(RunningSessionKey, false);
            EditorApplication.EnterPlaymode();
        }

        static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ResumeIfNeeded;
        }

        static void ResumeIfNeeded()
        {
            string id = SessionState.GetString(PendingSessionKey, string.Empty);
            if (string.IsNullOrEmpty(id) || !EditorApplication.isPlaying)
                return;
            if (SessionState.GetBool(RunningSessionKey, false))
                return;
            if (!Registrations.TryGetValue(id, out Registration registration))
                return;

            IReadOnlyList<DocumentationInspectorCaptureSpec> specs = registration.CreateSpecs();
            if (specs == null || specs.Count == 0)
            {
                Finish(registration.DisplayName, false);
                throw new InvalidOperationException("The documentation inspector capture set is empty: " + id + ".");
            }

            SessionState.SetBool(RunningSessionKey, true);
            DocumentationInspectorCaptureRunner.Start(
                specs,
                registration.Options,
                succeeded => Finish(registration.DisplayName, succeeded));
        }

        static void Finish(string displayName, bool succeeded)
        {
            SessionState.EraseString(PendingSessionKey);
            SessionState.SetBool(RunningSessionKey, false);
            Debug.Log(succeeded
                ? "Rebuilt " + displayName + " documentation inspector screenshots."
                : "The " + displayName + " documentation inspector screenshot capture failed. See the preceding error.");
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
        }

        sealed class Registration
        {
            public string DisplayName { get; }
            public Func<IReadOnlyList<DocumentationInspectorCaptureSpec>> CreateSpecs { get; }
            public DocumentationInspectorCaptureOptions Options { get; }

            public Registration(
                string displayName,
                Func<IReadOnlyList<DocumentationInspectorCaptureSpec>> createSpecs,
                DocumentationInspectorCaptureOptions options)
            {
                DisplayName = displayName;
                CreateSpecs = createSpecs;
                Options = options;
            }
        }
    }

    static class DocumentationInspectorCaptureRunner
    {
        public static void Start(
            IReadOnlyList<DocumentationInspectorCaptureSpec> specs,
            DocumentationInspectorCaptureOptions options,
            Action<bool> completed)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Inspector capture must run in Play Mode.");

            GameObject hostObject = new GameObject("Documentation Inspector Capture Runner");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            DocumentationInspectorCaptureHost host = hostObject.AddComponent<DocumentationInspectorCaptureHost>();
            host.StartCapture(specs, options, completed);
        }
    }

    [AddComponentMenu("")]
    sealed class DocumentationInspectorCaptureHost : MonoBehaviour
    {
        IReadOnlyList<DocumentationInspectorCaptureSpec> _specs;
        DocumentationInspectorCaptureOptions _options;
        Action<bool> _completed;

        public void StartCapture(
            IReadOnlyList<DocumentationInspectorCaptureSpec> specs,
            DocumentationInspectorCaptureOptions options,
            Action<bool> completed)
        {
            _specs = specs;
            _options = options;
            _completed = completed;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            bool succeeded = false;
            try
            {
                yield return null;
                for (int index = 0; index < _specs.Count; index++)
                {
                    IEnumerator capture = Capture(_specs[index]);
                    bool captureFailed = false;
                    while (true)
                    {
                        bool hasNext = false;
                        object current = null;
                        try
                        {
                            hasNext = capture.MoveNext();
                            if (hasNext)
                                current = capture.Current;
                        }
                        catch (Exception exception)
                        {
                            captureFailed = true;
                            Debug.LogException(exception);
                        }

                        if (captureFailed || !hasNext)
                            break;
                        yield return current;
                    }

                    if (captureFailed)
                        yield break;
                }

                AssetDatabase.Refresh();
                succeeded = true;
            }
            finally
            {
                Action<bool> completed = _completed;
                DestroyImmediate(gameObject);
                completed?.Invoke(succeeded);
            }
        }

        IEnumerator Capture(DocumentationInspectorCaptureSpec spec)
        {
            RenderTexture targetTexture = null;
            Texture2D readback = null;
            Texture2D cropped = null;
            PanelSettings panelSettings = null;
            GameObject panelHost = null;
            DocumentationInspectorCaptureTarget captureTarget = null;
            UnityEditor.Editor editor = null;

            try
            {
                captureTarget = spec.CreateTarget();
                if (captureTarget == null || captureTarget.InspectedObject == null)
                    throw new InvalidOperationException("The target factory returned no object for " + spec.Name + ".");

                editor = UnityEditor.Editor.CreateEditor(captureTarget.InspectedObject);
                VisualElement inspector = editor.CreateInspectorGUI();
                if (inspector == null)
                    throw new InvalidOperationException(
                        "The inspector did not provide a UI Toolkit visual tree for " + spec.Name + ".");

                targetTexture = new RenderTexture(
                    _options.Width,
                    _options.Height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 4,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = spec.Name + " Documentation Inspector"
                };
                targetTexture.Create();

                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.hideFlags = HideFlags.HideAndDontSave;
                panelSettings.name = spec.Name + " Documentation Inspector Panel";
                panelSettings.targetTexture = targetTexture;
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                panelSettings.scale = 1f;
                panelSettings.clearColor = true;
                panelSettings.colorClearValue = _options.BackgroundColor;
                panelSettings.themeStyleSheet = FindThemeStyleSheet(_options.ThemeAssetName);
                if (panelSettings.themeStyleSheet == null)
                    throw new InvalidOperationException(
                        "The documentation capture theme could not be found: " + _options.ThemeAssetName + ".");

                panelHost = new GameObject(spec.Name + " Inspector Capture Host");
                panelHost.hideFlags = HideFlags.HideAndDontSave;
                panelHost.SetActive(false);
                UIDocument document = panelHost.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                panelHost.SetActive(true);

                VisualElement root = document.rootVisualElement;
                root.style.width = _options.Width;
                root.style.height = _options.Height;
                root.style.backgroundColor = _options.BackgroundColor;
                root.style.paddingLeft = _options.HorizontalPadding;
                root.style.paddingRight = _options.HorizontalPadding;
                root.style.paddingTop = _options.VerticalPadding;
                root.style.paddingBottom = _options.VerticalPadding;
                AddEditorStyleSheets(root, _options.EditorStyleSheetNames);

                VisualElement surface = new VisualElement();
                surface.name = "documentation-inspector-surface";
                surface.style.width = Length.Percent(100f);
                surface.style.flexGrow = 0f;
                surface.style.flexShrink = 0f;
                surface.style.paddingLeft = _options.SurfacePadding;
                surface.style.paddingRight = _options.SurfacePadding;
                surface.style.paddingTop = _options.SurfacePadding;
                surface.style.paddingBottom = _options.SurfacePadding;
                surface.style.backgroundColor = _options.SurfaceColor;
                surface.style.borderLeftColor = _options.SurfaceBorderColor;
                surface.style.borderRightColor = _options.SurfaceBorderColor;
                surface.style.borderTopColor = _options.SurfaceBorderColor;
                surface.style.borderBottomColor = _options.SurfaceBorderColor;
                surface.style.borderLeftWidth = 1f;
                surface.style.borderRightWidth = 1f;
                surface.style.borderTopWidth = 1f;
                surface.style.borderBottomWidth = 1f;
                surface.style.borderTopLeftRadius = _options.SurfaceCornerRadius;
                surface.style.borderTopRightRadius = _options.SurfaceCornerRadius;
                surface.style.borderBottomLeftRadius = _options.SurfaceCornerRadius;
                surface.style.borderBottomRightRadius = _options.SurfaceCornerRadius;
                surface.style.overflow = Overflow.Hidden;

                inspector.style.flexGrow = 0f;
                inspector.style.flexShrink = 0f;
                inspector.style.minWidth = 0f;
                inspector.Bind(editor.serializedObject);
                surface.Add(inspector);
                root.Add(surface);

                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();

                float requiredHeight = surface.resolvedStyle.height + _options.VerticalPadding * 2f;
                if (!float.IsNaN(requiredHeight) && requiredHeight > _options.Height)
                {
                    throw new InvalidOperationException(
                        spec.Name + " needs " + Mathf.CeilToInt(requiredHeight) +
                        " pixels of capture height, but the configured staging canvas is only " +
                        _options.Height + " pixels high.");
                }

                readback = new Texture2D(
                    _options.Width,
                    _options.Height,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = targetTexture;
                    readback.ReadPixels(
                        new Rect(0f, 0f, _options.Width, _options.Height),
                        0,
                        0,
                        false);
                    readback.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                cropped = CropTransparent(readback, _options.TransparentCropPadding);

                string fullPath = ProjectAssetPathToFullPath(spec.OutputPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException("The output path has no directory: " + spec.OutputPath + ".");
                Directory.CreateDirectory(directory);
                byte[] pngData = cropped.EncodeToPNG();
                string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".capturing";
                File.WriteAllBytes(temporaryPath, pngData);
                IOException writeException = null;
                bool written = false;
                try
                {
                    for (int attempt = 0; attempt < 20 && !written; attempt++)
                    {
                        AssetDatabase.ReleaseCachedFileHandles();
                        try
                        {
                            if (File.Exists(fullPath))
                                File.Replace(temporaryPath, fullPath, null);
                            else
                                File.Move(temporaryPath, fullPath);
                            written = true;
                        }
                        catch (IOException exception)
                        {
                            writeException = exception;
                        }

                        if (!written && attempt < 19)
                            yield return null;
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }

                if (!written)
                {
                    throw new IOException(
                        "Could not atomically replace the documentation screenshot after releasing Unity's cached file handles: " +
                        fullPath,
                        writeException);
                }
            }
            finally
            {
                if (editor != null)
                    DestroyImmediate(editor);
                if (panelHost != null)
                    DestroyImmediate(panelHost);
                if (captureTarget?.Owner != null)
                    DestroyImmediate(captureTarget.Owner);
                if (readback != null)
                    DestroyImmediate(readback);
                if (cropped != null)
                    DestroyImmediate(cropped);
                if (targetTexture != null)
                {
                    targetTexture.Release();
                    DestroyImmediate(targetTexture);
                }
                if (panelSettings != null)
                    DestroyImmediate(panelSettings);
            }
        }

        static Texture2D CropTransparent(Texture2D source, int padding)
        {
            Color32[] pixels = source.GetPixels32();
            int minX = source.width;
            int minY = source.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < source.height; y++)
            {
                int row = y * source.width;
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[row + x].a <= 1)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                throw new InvalidOperationException("The inspector capture rendered no visible pixels.");

            int safePadding = Mathf.Max(0, padding);
            minX = Mathf.Max(0, minX - safePadding);
            minY = Mathf.Max(0, minY - safePadding);
            maxX = Mathf.Min(source.width - 1, maxX + safePadding);
            maxY = Mathf.Min(source.height - 1, maxY + safePadding);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            Color32[] croppedPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(
                    pixels,
                    (minY + y) * source.width + minX,
                    croppedPixels,
                    y * width,
                    width);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            result.SetPixels32(croppedPixels);
            result.Apply(false, false);
            return result;
        }

        static ThemeStyleSheet FindThemeStyleSheet(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets(assetName + " t:ThemeStyleSheet");
            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(assetPath);
                if (theme != null && theme.name == assetName)
                    return theme;
            }

            return null;
        }

        static void AddEditorStyleSheets(VisualElement root, IReadOnlyList<string> wantedNames)
        {
            StyleSheet[] loaded = Resources.FindObjectsOfTypeAll<StyleSheet>();
            HashSet<StyleSheet> added = new HashSet<StyleSheet>();
            for (int nameIndex = 0; nameIndex < wantedNames.Count; nameIndex++)
            {
                bool found = false;
                for (int sheetIndex = 0; sheetIndex < loaded.Length; sheetIndex++)
                {
                    StyleSheet sheet = loaded[sheetIndex];
                    if (sheet == null || sheet.name != wantedNames[nameIndex] || !added.Add(sheet))
                        continue;
                    root.styleSheets.Add(sheet);
                    found = true;
                }

                if (found)
                    continue;

                string[] guids = AssetDatabase.FindAssets(wantedNames[nameIndex] + " t:StyleSheet");
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
                    if (sheet == null || sheet.name != wantedNames[nameIndex] || !added.Add(sheet))
                        continue;

                    root.styleSheets.Add(sheet);
                    break;
                }
            }
        }

        static string ProjectAssetPathToFullPath(string assetPath)
        {
            string relative = assetPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
