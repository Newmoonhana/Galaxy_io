using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Singleton
{
    /// <summary>
    /// 서버가 알려 준 게임 모드와 팀 모드 상태. 클라이언트 월드당 한 개. 복제되지 않음.
    /// </summary>
    public struct GameModeStateComponent : IComponentData
    {
        // 서버의 게임 모드를 받음. 받은 뒤에 모드 씬을 로드함.
        public bool ModeReceived;
        public GameMode Mode;

        // 최대 인원이 차서 들어갈 수 없음.
        public bool Full;

        public Team Team;

        // 이 시각(ElapsedTime)부터 부활할 수 있음.
        public double RespawnAt;

        // 마지막으로 격추한 쪽 이름. 격추가 아니면 비어 있음.
        public FixedString64Bytes Killer;

        // SpaceshipCategory 순서의 우리 팀 기체 수와 최대 수. 최대 0 이면 고를 수 없음.
        public FixedList32Bytes<byte> Occupied;
        public FixedList32Bytes<byte> Max;

        public int RedScore;
        public int BlueScore;

        // 이긴 팀. 판이 진행 중이면 None.
        public Team Winner;

        // 판이 끝났으면 다음 판이 시작되는 시각(ElapsedTime)
        public double RestartAt;

        // 새 판의 맵을 받는 동안 이 시각(ElapsedTime)까지 로딩 화면을 띄움.
        public double LoadingUntil;

        // 새 판을 시작할 때 로딩 화면을 띄우는 시간(초)
        public const double RebuildLoadingTime = 2.0;
    }
}
