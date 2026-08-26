using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetInventory
{
    internal sealed class PipelineConversionImportTracker : AssetPostprocessor
    {
        private const string ActiveSessionKey = "AssetInventory.PipelineConversionImportTracker.Active";
        private const string PathsSessionKey = "AssetInventory.PipelineConversionImportTracker.Paths";

        [Serializable]
        private sealed class PathState
        {
            public List<string> Paths = new List<string>();
        }

        internal static bool IsTracking => SessionState.GetBool(ActiveSessionKey, false);

        internal static void Begin(bool continueExisting)
        {
            if (!continueExisting || !IsTracking)
            {
                SessionState.EraseString(PathsSessionKey);
            }
            SessionState.SetBool(ActiveSessionKey, true);
        }

        internal static List<string> Complete()
        {
            List<string> result = LoadPaths()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Cancel();
            return result;
        }

        internal static void Cancel()
        {
            SessionState.SetBool(ActiveSessionKey, false);
            SessionState.EraseString(PathsSessionKey);
        }

        internal static void RecordImportedAssetsForTests(IEnumerable<string> importedAssets)
        {
            RecordImportedAssets(importedAssets);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            RecordImportedAssets(importedAssets);
            RecordImportedAssets(movedAssets);
        }

        private static void RecordImportedAssets(IEnumerable<string> importedAssets)
        {
            if (!IsTracking || importedAssets == null) return;

            HashSet<string> paths = LoadPaths();
            bool changed = false;
            foreach (string importedAsset in importedAssets)
            {
                string path = NormalizeProjectPath(importedAsset);
                if (!IsRelevantImportedAsset(path)) continue;
                changed |= paths.Add(path);
            }
            if (!changed) return;

            PathState state = new PathState
            {
                Paths = paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList()
            };
            SessionState.SetString(PathsSessionKey, JsonUtility.ToJson(state));
        }

        private static HashSet<string> LoadPaths()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string json = SessionState.GetString(PathsSessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                PathState state = JsonUtility.FromJson<PathState>(json);
                if (state?.Paths == null) return result;
                foreach (string path in state.Paths)
                {
                    if (IsRelevantImportedAsset(path)) result.Add(NormalizeProjectPath(path));
                }
            }
            catch
            {
                SessionState.EraseString(PathsSessionKey);
            }
            return result;
        }

        private static bool IsRelevantImportedAsset(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return extension.Equals(".mat", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeProjectPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
