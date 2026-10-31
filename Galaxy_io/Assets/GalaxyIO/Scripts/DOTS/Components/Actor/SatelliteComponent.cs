using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="SatelliteAuthoring"/>의 DOTS 구조체. 점령전 위성. 자동 포탑 체력은 <see cref="SpaceshipSpecComponent"/>.
    /// 포탑이 부서지면 위성은 남고 포탑만 꺼졌다가 일정 시간 뒤 점령한 팀 편으로 다시 켜짐. 점령 전이면 꺼진 채 남음.
    /// 포탑이 꺼진 점령 범위에 한 팀만 있으면 점령도가 오름. 함재기도 주인 한 명으로 셈.
    /// 모든 접속에 항상 복제함.
    /// </summary>
    public struct SatelliteComponent : IComponentData
    {
        // 0 부터 A, B, C, ...
        [GhostField]
        public byte Index;

        // 점령한 팀. 처음에는 None.
        [GhostField]
        public Team Owner;

        // 점령도를 올리는 중인 팀
        [GhostField]
        public Team Capturing;

        // 점령도(0 ~ 100)
        [GhostField(Quantization = 10)]
        public float Progress;

        // 포탑이 꺼진 점령 범위에 혼자 있는 팀. 없거나 두 팀이 함께 있으면 None.
        [GhostField]
        public Team Occupier;

        // 서버만. 고정 위치. 부딪혀 밀려도 돌아옴.
        public float2 Home;

        // 서버만. 다음 점수까지 남은 시간(초)
        public float ScoreRemaining;

        // 서버만. 부서진 포탑이 다시 켜지기까지 남은 시간(초)
        public float ReviveRemaining;

        // 서버만. 점령도를 올리던 팀이 점령 범위를 비운 시간(초)
        public float AbsentTime;

        public const float FullProgress = 100.0F;

        // 점령 중 깜빡임 주기(초)
        private const double FlashPeriod = 0.5;

        /// <summary>
        /// 다른 팀이 점령도를 올리는 중인지.
        /// </summary>
        public bool IsBeingCaptured => Occupier != Team.None && Occupier == Capturing && Occupier != Owner;

        /// <summary>
        /// 지금 보여 줄 팀 색. 점령되는 중이면 점령한 팀과 점령하는 팀 색을 번갈아 보여 줌.
        /// </summary>
        public Team ShownTeam(double time)
        {
            return IsBeingCaptured && time % FlashPeriod >= FlashPeriod * 0.5 ? Capturing : Owner;
        }

        /// <summary>
        /// 위성 이름. A, B, C, ...
        /// </summary>
        public static string Name(byte index)
        {
            return ((char)('A' + index)).ToString();
        }
    }
}
