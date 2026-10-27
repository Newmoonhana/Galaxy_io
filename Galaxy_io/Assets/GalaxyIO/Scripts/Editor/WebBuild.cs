using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 웹 플레이어 빌드 도구. 결과물은 Builds/Web/{MT|ST}{-Fast|-Debug}.
    /// </summary>
    public static class WebBuild
    {
        private const string OutputRoot = "Builds/Web";

        // 웹 빌드 설정의 Code Optimization 키. 값은 BuildTimes / RuntimeSpeed 등.
        private const string CodeOptimizationKey = "CodeOptimization";

        private enum Mode
        {
            Fast,           // 기능 확인용. 빌드 시간 최소화
            Diagnostics,    // Fast + 함수 이름, C# 예외 스택
            Release         // 성능 확인용
        }

        [MenuItem("GalaxyIO/Build/Web Fast (Multithreaded)")]
        public static void BuildMultithreadedFast() => Build(true, Mode.Fast);

        [MenuItem("GalaxyIO/Build/Web Fast (Single-threaded)")]
        public static void BuildSingleThreadedFast() => Build(false, Mode.Fast);

        [MenuItem("GalaxyIO/Build/Web Diagnostics (Multithreaded)")]
        public static void BuildMultithreadedDiagnostics() => Build(true, Mode.Diagnostics);

        [MenuItem("GalaxyIO/Build/Web Diagnostics (Single-threaded)")]
        public static void BuildSingleThreadedDiagnostics() => Build(false, Mode.Diagnostics);

        [MenuItem("GalaxyIO/Build/Web Release (Multithreaded)")]
        public static void BuildMultithreaded() => Build(true, Mode.Release);

        [MenuItem("GalaxyIO/Build/Web Release (Single-threaded)")]
        public static void BuildSingleThreaded() => Build(false, Mode.Release);

        private static void Build(bool threads, Mode mode)
        {
            NamedBuildTarget target = NamedBuildTarget.WebGL;
            string platformName = BuildPipeline.GetBuildTargetName(BuildTarget.WebGL);

            bool prevThreads = PlayerSettings.WebGL.threadsSupport;
            WebGLCompressionFormat prevCompression = PlayerSettings.WebGL.compressionFormat;
            WebGLDebugSymbolMode prevSymbols = PlayerSettings.WebGL.debugSymbolMode;
            WebGLExceptionSupport prevExceptions = PlayerSettings.WebGL.exceptionSupport;
            Il2CppCodeGeneration prevCodeGen = PlayerSettings.GetIl2CppCodeGeneration(target);
            string prevCodeOptimization = EditorUserBuildSettings.GetPlatformSettings(platformName, CodeOptimizationKey);

            bool isRelease = mode == Mode.Release;

            // 멀티스레드는 Burst 잡을 워커 스레드로 돌림. 페이지에 COOP/COEP 헤더가 필요함.
            PlayerSettings.WebGL.threadsSupport = threads;

            // 테스트 빌드는 압축을 생략함. 릴리스는 http 에서도 풀리는 Gzip (Brotli 는 https 전용).
            PlayerSettings.WebGL.compressionFormat = isRelease ? WebGLCompressionFormat.Gzip : WebGLCompressionFormat.Disabled;

            // 테스트 빌드는 C++ 생성량과 최적화 패스를 줄여 빌드 시간을 단축함. 실행 속도는 느려짐.
            PlayerSettings.SetIl2CppCodeGeneration(target, isRelease ? Il2CppCodeGeneration.OptimizeSpeed : Il2CppCodeGeneration.OptimizeSize);
            EditorUserBuildSettings.SetPlatformSettings(platformName, CodeOptimizationKey, isRelease ? "RuntimeSpeed" : "BuildTimes");

            if (mode == Mode.Diagnostics)
            {
                PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Embedded;
                PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            }

            string name = (threads ? "MT" : "ST") + mode switch
            {
                Mode.Fast => "-Fast",
                Mode.Diagnostics => "-Debug",
                _ => ""
            };

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Path.Combine(OutputRoot, name),
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                bool succeeded = report.summary.result == BuildResult.Succeeded;

                Debug.Log($"[WebBuild] {name}: {report.summary.result}, " +
                          $"{report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");

                if (!succeeded && Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                PlayerSettings.WebGL.threadsSupport = prevThreads;
                PlayerSettings.WebGL.compressionFormat = prevCompression;
                PlayerSettings.WebGL.debugSymbolMode = prevSymbols;
                PlayerSettings.WebGL.exceptionSupport = prevExceptions;
                PlayerSettings.SetIl2CppCodeGeneration(target, prevCodeGen);
                EditorUserBuildSettings.SetPlatformSettings(platformName, CodeOptimizationKey, prevCodeOptimization);
            }
        }
    }
}
