using System;
using System.IO;
using GalaxyIO.DOTS.Components.Singleton;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;

namespace GalaxyIO.DOTS.Networking
{
    /// <summary>
    /// 데디케이티드 서버 설정. 실행 인자 `-gameMode Free|Regicide|Conquest`가 있으면 그것을,
    /// 없으면 실행 파일 옆의 server_config.json 을 읽음. 파일이 없으면 기본값으로 만들어 둠.
    /// </summary>
    [Serializable]
    public sealed class DedicatedServerConfig
    {
        private const string FileName = "server_config.json";
        private const string ModeArgument = "-gameMode";

        // 게임 모드 이름. GameMode 값과 같음.
        public string Mode = nameof(GameMode.Free);

        /// <summary>
        /// 이번 실행의 게임 모드.
        /// </summary>
        public static GameMode LoadMode()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], ModeArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return Parse(args[i + 1]);
                }
            }

            // 빌드는 Galaxy_io_Data 의 부모(실행 파일 폴더), 에디터는 프로젝트 폴더.
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName);

            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonUtility.ToJson(new DedicatedServerConfig(), true));
                }

                return Parse(JsonUtility.FromJson<DedicatedServerConfig>(File.ReadAllText(path)).Mode);
            }
            catch (Exception e)
            {
                DebugLog.LogError($"서버 설정({path})을 읽지 못함. 자유 모드로 엶. {e.Message}");
                return GameMode.Free;
            }
        }

        private static GameMode Parse(string value)
        {
            if (Enum.TryParse(value, true, out GameMode mode))
            {
                return mode;
            }

            DebugLog.LogError($"알 수 없는 게임 모드: {value}. 자유 모드로 엶.");
            return GameMode.Free;
        }
    }
}
