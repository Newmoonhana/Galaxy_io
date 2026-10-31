using System;
using System.Collections.Generic;
using System.IO;
using GalaxyIO.DOTS.Components.Singleton;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;

namespace GalaxyIO.DOTS.Networking
{
    /// <summary>
    /// 데디케이티드 서버 설정. 실행 파일 옆의 server_config.json 을 읽음. 파일이 없으면 기본값으로 만들어 둠.
    /// 실행 인자 `-gameMode Free|Regicide|Conquest`가 있으면 파일의 게임 모드보다 우선함.
    /// </summary>
    [Serializable]
    public sealed class DedicatedServerConfig
    {
        [Serializable]
        public sealed class DropTable
        {
            // EnemyType 이름
            public string Enemy;

            // Roll 마다 따로 굴림.
            public List<DropRoll> Rolls = new List<DropRoll>();
        }

        [Serializable]
        public sealed class DropRoll
        {
            // 성공 확률 (0 ~ 1). 성공하면 항목 중 하나를 가중치로 고름.
            public float Chance;

            public List<DropEntry> Entries = new List<DropEntry>();
        }

        [Serializable]
        public sealed class DropEntry
        {
            // ItemId 이름
            public string Item;

            // 한 번에 떨어뜨릴 개수 범위 (양 끝 포함)
            public int Min;
            public int Max;

            // 같은 Roll 안에서 뽑힐 비중
            public int Weight;
        }

        private const string FileName = "server_config.json";
        private const string ModeArgument = "-gameMode";

        // 게임 모드 이름. GameMode 값과 같음.
        public string Mode = nameof(GameMode.Free);

        // 적 종류별 드랍 테이블. 없는 적 종류는 서버 시작 시 베이킹된 테이블로 채워 저장함.
        public List<DropTable> DropTables = new List<DropTable>();

        /// <summary>
        /// 이번 실행의 설정. 데디케이티드 서버가 아니거나 파일을 읽지 못하면 null.
        /// </summary>
        public static DedicatedServerConfig Current { get; private set; }

        // 빌드는 Galaxy_io_Data 의 부모(실행 파일 폴더), 에디터는 프로젝트 폴더.
        private static string FilePath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            Current = null;
        }

        /// <summary>
        /// 설정 파일을 읽어 <see cref="Current"/>에 둠.
        /// </summary>
        /// <returns>이번 실행의 게임 모드</returns>
        public static GameMode Load()
        {
            string path = FilePath;

            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonUtility.ToJson(new DedicatedServerConfig(), true));
                }

                Current = JsonUtility.FromJson<DedicatedServerConfig>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                DebugLog.LogError($"서버 설정({path})을 읽지 못함. 기본 설정으로 엶. {e.Message}");
                Current = null;
            }

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], ModeArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return Parse(args[i + 1]);
                }
            }

            return Parse(Current != null ? Current.Mode : nameof(GameMode.Free));
        }

        /// <summary>
        /// 설정을 파일에 씀.
        /// </summary>
        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                DebugLog.LogError($"서버 설정({FilePath})을 저장하지 못함. {e.Message}");
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
