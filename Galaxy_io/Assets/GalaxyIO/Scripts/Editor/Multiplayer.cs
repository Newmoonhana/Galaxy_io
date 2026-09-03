using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 멀티플레이 테스트용 빌드 / 실행 도구.
    /// 서버 1개와 클라이언트 N개를 각각 별도의 프로세스로 띄운다.
    /// <p/>
    /// 클라이언트 실행 파일이 PlayType.ClientAndServer 로 빌드되면 프로세스마다
    /// 서버 월드까지 생성되어, 서버 월드의 Companion GameObject가
    /// 클라이언트의 것과 함께 그려진다(우주선이 두 번 보이는 현상).
    /// 이를 막기 위해 역할별 스크립팅 정의를 명시적으로 넘긴다.
    /// </summary>
    public static class Multiplayer
    {
        private const BuildTarget Target = BuildTarget.StandaloneWindows64;

        private const string OutputRoot = "Builds/Win64";

        private const string LogRoot = OutputRoot + "/Logs";

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

            // 서버 / 클라이언트를 각각 한 번씩만 빌드하고, 실행만 여러 번 한다.
            // NETCODE_DEBUG: Netcode 의 접속 / Handshake 진단 로그를 켠다.
            // Build Settings 창을 거치는 빌드에는 development 빌드일 때 자동으로 붙지만,
            // BuildPipeline.BuildPlayer 를 직접 호출하는 이 경로에서는 붙지 않는다.
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

            // 프로세스마다 로그 파일을 분리한다. -logFile 을 주지 않으면 모든 인스턴스가
            // productName 기준의 같은 Player.log 를 공유해서 서로의 로그를 덮어쓴다.
            Directory.CreateDirectory(LogRoot);

            Launch(server, "Server");

            for (int i = 1; i <= clientCount; i++)
            {
                Launch(client, "Client" + i);
            }
        }

        /// <summary>
        /// 전용 로그 파일을 지정해서 플레이어를 실행한다.
        /// </summary>
        private static void Launch(string executable, string logName)
        {
            string log = Path.GetFullPath(Path.Combine(LogRoot, logName + ".log"));

            var startInfo = new ProcessStartInfo
            {
                FileName         = executable,
                Arguments        = "-logFile \"" + log + "\"",
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute  = true,
            };

            Process.Start(startInfo);

            Debug.Log($"{logName} | exe: {executable} | log: {log}");
        }

        /// <summary>
        /// 지정한 역할로 플레이어를 빌드하고, 성공 시 실행 파일의 절대 경로를 반환한다.
        /// 실패하면 null.
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

                // BuildPipeline.BuildPlayer 를 스크립트에서 직접 호출하면, Build Settings 창이
                // 주입해 주는 DOTS 정의(UNITY_CLIENT / UNITY_SERVER)가 적용되지 않는다.
                // 해당 정의는 BuildPlayerWindow.RegisterGetBuildPlayerOptionsHandler 로 등록된
                // 핸들러에서만 붙기 때문에, 창을 거치지 않는 이 경로에서는 직접 넘겨야 한다.
                // ClientServerBootstrap.RequestedPlayType 이 이 정의로 결정된다.
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
