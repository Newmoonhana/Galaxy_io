using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 봇 서버(데디케이티드)와 관찰자 클라이언트 빌드 도구.
    /// 배치 모드: Unity.exe -batchmode -quit -projectPath ... -executeMethod GalaxyIO.Scripts.Editor.BotServerBuild.BuildServerBatch -buildOutput 폴더
    /// 결과물 기본 위치는 Builds/BotServer, Builds/SpectatorClient.
    /// 관찰자 클라이언트는 일반 클라이언트와 같은 빌드이고, 실행 인자 -spectator 로 관찰자 모드가 됨.
    /// </summary>
    public static class BotServerBuild
    {
        [MenuItem("GalaxyIO/Build/Bot Server (Dedicated)")]
        public static void BuildServerMenu() => Build(true, "Builds/BotServer");

        [MenuItem("GalaxyIO/Build/Spectator Client (Windows)")]
        public static void BuildClientMenu() => Build(false, "Builds/SpectatorClient");

        public static void BuildServerBatch() => Build(true, Argument("-buildOutput") ?? "Builds/BotServer");

        public static void BuildClientBatch() => Build(false, Argument("-buildOutput") ?? "Builds/SpectatorClient");

        private static void Build(bool server, string outputDirectory)
        {
            // 스크립트를 고친 뒤 빌드하면 엔티티 씬 베이크 결과의 오브젝트 참조 일부가 빠지는 일이 있었음
            // ("N missing object reference(s) while loading subscene", 클라이언트는 크래시). 빌드 전에 다시 베이크하게 함.
            InvalidateEntityScenes();

            // 클라이언트는 UI, 사운드 등을 Addressables 로 읽음. 콘텐츠를 먼저 빌드함.
            if (!server)
            {
                AddressableAssetSettings.BuildPlayerContent(out var addressables);

                if (!string.IsNullOrEmpty(addressables.Error))
                {
                    Debug.LogError($"[BotServerBuild] Addressables 빌드 실패: {addressables.Error}");
                    Finish(false);
                    return;
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(outputDirectory, "GalaxyIO.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
                // 증분 빌드 캐시가 오래된 엔티티 씬 콘텐츠를 재사용하면 오브젝트 참조가 빠짐. 늘 새로 빌드함.
                options = BuildOptions.CleanBuildCache,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool succeeded = report.summary.result == BuildResult.Succeeded;

            Debug.Log($"[BotServerBuild] {(server ? "Server" : "Client")}: {report.summary.result}, "
                      + $"{report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime} -> {outputDirectory}");

            Finish(succeeded);
        }

        // Unity.Scenes.EntitiesCacheUtility.UpdateEntitySceneGlobalDependency (internal) 호출.
        private static void InvalidateEntityScenes()
        {
            Type utility = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name == "Unity.Scenes.Editor")
                .Select(a => a.GetType("Unity.Scenes.EntitiesCacheUtility"))
                .FirstOrDefault(t => t != null);

            var method = utility?.GetMethod(
                "UpdateEntitySceneGlobalDependency",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            );

            if (method == null)
            {
                Debug.LogWarning("[BotServerBuild] 엔티티 씬 캐시를 비우지 못함(API 없음). 그대로 빌드함.");
                return;
            }

            method.Invoke(null, method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray());
            AssetDatabase.Refresh();
        }

        private static void Finish(bool succeeded)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
