using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 멀티플레이 테스트용 빌드/실행 도구를 포함한 에디터 툴.
    /// </summary>
    public static class Multiplayer
    {
        private const BuildTarget Target = BuildTarget.StandaloneWindows64;

        private const string OutputRoot = "Builds/Win64";

        private const string LogRoot = OutputRoot + "/Logs";

        // 실행되는 플레이어 창의 크기. 플레이어는 마지막 창 크기를 레지스트리에
        // 저장하고 Player Settings 의 기본값보다 우선해서 사용하므로,
        // -screen-width / -screen-height 로 매번 명시해서 덮어씀.
        private const int ScreenWidth = 1280;

        private const int ScreenHeight = 720;

        [MenuItem("Tools/Run Multiplayer/Win64/1 Players")]
        private static void PerformWin64Build1()
        {
            BuildAndRun(1);
        }

        [MenuItem("Tools/Run Multiplayer/Win64/2 Players")]
        private static void PerformWin64Build2()
        {
            BuildAndRun(2);
        }

        [MenuItem("Tools/Run Multiplayer/Win64/3 Players")]
        private static void PerformWin64Build3()
        {
            BuildAndRun(3);
        }

        [MenuItem("Tools/Run Multiplayer/Win64/4 Players")]
        private static void PerformWin64Build4()
        {
            BuildAndRun(4);
        }

        private static void BuildAndRun(int clientCount)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, Target);

            // 서버 / 클라이언트를 각각 한 번씩만 빌드하고, 실행만 여러 번 함.
            // NETCODE_DEBUG: Netcode 의 접속 / Handshake 진단 로그 활성화.
            string server = Build("Server", new[] { "UNITY_SERVER", "NETCODE_DEBUG" });

            if (server == null)
            {
                return;
            }

            string client = Build("Client", new[] { "UNITY_CLIENT", "NETCODE_DEBUG" });

            if (client == null)
            {
                return;
            }

            // 프로세스별 로그 파일 분리. -logFile 파라미터로 로그 파일의
            // 이름을 분리함. 여러 프로세스가 서로 같은 파일에 덮어쓰는 것 방지.
            Directory.CreateDirectory(LogRoot);

            Launch(server, "Server");

            for (int i = 1; i <= clientCount; i++)
            {
                Launch(client, "Client" + i);
            }
        }

        /// <summary>
        /// 전용 로그 파일을 지정해서 플레이어를 실행함.
        /// </summary>
        private static void Launch(string executable, string logName)
        {
            string log = Path.GetFullPath(Path.Combine(LogRoot, logName + ".log"));

            var startInfo = new ProcessStartInfo
            {
                FileName         = executable,
                Arguments        = "-logFile \"" + log + "\"" +
                                   " -screen-width " + ScreenWidth +
                                   " -screen-height " + ScreenHeight +
                                   " -screen-fullscreen 0",
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute  = true,
            };

            Process.Start(startInfo);

            Debug.Log($"{logName} | exe: {executable} | log: {log}");
        }

        /// <summary>
        /// 지정한 역할로 플레이어를 빌드함. 성공 시 실행 파일의 절대 경로, 실패 시 null 반환.
        /// </summary>
        private static string Build(string role, string[] extraDefines)
        {
            string name = GetProjectName() + "_" + role;
            string executable = OutputRoot + "/" + name + "/" + name + ".exe";

            var options = new BuildPlayerOptions
            {
                scenes           = GetScenePaths(),
                locationPathName = executable,
                target           = Target,
                targetGroup      = BuildTargetGroup.Standalone,
                options          = BuildOptions.Development,

                // 스크립트에서 직접 빌드하면 UNITY_CLIENT / UNITY_SERVER 정의가 자동으로
                // 붙지 않으므로 직접 넘김. ClientServerBootstrap.RequestedPlayType 이
                // 이 정의로 결정됨.
                extraScriptingDefines = extraDefines,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"{role} 빌드 실패: {report.summary.result} " +
                    $"(에러 {report.summary.totalErrors}개)"
                );

                return null;
            }

            return Path.GetFullPath(executable);
        }

        private static string GetProjectName()
        {
            string[] s = Application.dataPath.Split('/');
            return s[s.Length - 2];
        }

        private static string[] GetScenePaths()
        {
            string[] scenes = new string[EditorBuildSettings.scenes.Length];

            for (int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = EditorBuildSettings.scenes[i].path;
            }

            return scenes;
        }
    }
}
