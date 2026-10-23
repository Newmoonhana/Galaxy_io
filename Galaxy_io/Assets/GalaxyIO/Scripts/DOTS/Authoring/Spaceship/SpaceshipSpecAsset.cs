using GalaxyIO.DOTS.Components.Actor;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선 기체 사양. <see cref="SpaceshipAuthoring"/>이 참조함.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_SpaceshipSpec", menuName = "GalaxyIO/Spaceship Spec")]
    public sealed class SpaceshipSpecAsset : ScriptableObject
    {
        [Header("Attributes")]

        // 월드 좌표 상 이동속도
        public float Speed;

        // 호버링(WASD) 최대 이동속도
        [Min(0.0F)]
        public float HoveringSpeed = 10.0F;

        // 호버링 가속도. 작을수록 옆으로 미끄러지듯 붙음.
        [Min(0.0F)]
        public float HoveringAcceleration = 40.0F;

        // 호버링 감속도. WASD를 뗐을 때 초당 줄어드는 속도.
        [Min(0.0F)]
        public float HoveringDeceleration = 60.0F;

        // 호버링 방향 전환 가속도. 움직이던 중에 WASD 방향을 바꾸면 가감속 대신 이 값으로 붙음.
        [Min(0.0F)]
        public float HoveringManeuver = 40.0F;

        // 가속도
        public float Acceleration;

        // 감속도. 가속 키를 뗐을 때 초당 줄어드는 속력.
        [Min(0.0F)]
        public float Deceleration = 5.0F;

        // 회전 속도(Degree/초)
        public float Maneuver;

        // 회전 가속도(도/초²). 회전 속도가 Maneuver 까지 붙는 빠르기. 클수록 즉각적.
        [Min(1.0F)]
        public float ManeuverAcceleration = 3600.0F;

        // 회전 감속도(도/초²). 목표 방향에 가까워지면 이 값으로 회전을 멈춤. 작을수록 묵직하게 멈춤.
        [Min(1.0F)]
        public float ManeuverDeceleration = 3600.0F;

        // 고속 선회 억제: 최대 속력일 때 회전 속도가 깎이는 비율.
        [Range(0.0F, 1.0F)]
        public float ManeuverPenalty = 0.6F;

        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        // 피격 후 자가 수리가 다시 시작되기까지의 시간(초)
        [Min(0.0F)]
        public float RepairDelay = 3.0F;

        // 선체 두께. 보이는 선체 크기(지름)에 맞출 것.
        // 관통 투사체가 이 우주선을 지날 때 관통력이 이만큼 줄어들고, AI 는 이 절반만큼 더 떨어져서 싸움.
        [Min(0.0F)]
        public float Thickness = 1.0F;

        // 이 속력 이상으로 날면 다른 우주선과 부딪히지 않음. 아이템은 그대로 주움. 플레이어만. 0 이면 항상 부딪힘.
        [Min(0.0F)]
        public float PhaseSpeed;

        // 포탑 최대 열. 플레이어만. 0 이면 과열하지 않음.
        [Min(0.0F)]
        public float MaxHeat = 20.0F;

        // 격추한 플레이어가 얻는 점수
        [Min(0)]
        public int KillPoint = 1;

        [Header("Turn Brake")]

        // 기준 회전속도(도/초). 기준 속력에서 이 속도로 돌 때 TurnBrakeDeceleration 만큼 감속함. 플레이어만.
        [Min(1.0F)]
        public float TurnBrakeTurnRate = 270.0F;

        // 기준 속력에서 기준 회전속도로 돌 때 초당 줄어드는 속력
        [Min(0.0F)]
        public float TurnBrakeDeceleration = 30.0F;

        // 곡선의 휘는 정도. 1이면 직선, 클수록 느린 선회는 덜 깎이고 빠른 선회에서 급격히 커짐.
        [Min(1.0F)]
        public float TurnBrakeExponent = 3.0F;

        // 제동이 걸리기 시작하는 비행 속력. 이 이하에서는 감속하지 않음.
        [Min(0.0F)]
        public float TurnBrakeStartSpeed = 15.0F;

        // 기준 속력. StartSpeed 부터 선형으로 커져 이 속력에서 Deceleration 그대로 걸리고, 그 위로도 계속 커짐.
        [Min(0.0F)]
        public float TurnBrakeReferenceSpeed = 30.0F;

        // 회전 관성. 선회 제동이 셀수록 속도가 기수를 덜 따라 돌아 돌기 전 방향으로 밀려남.
        // 0이면 밀리지 않고, 1이면 기준 제동에서 기수를 전혀 따라 돌지 않음. 무거운 기체일수록 크게.
        [Min(0.0F)]
        public float Inertia = 1.0F;

        // 기수와 어긋난 옆 방향 속도가 초당 줄어드는 양
        [Min(0.0F)]
        public float InertiaDeceleration = 20.0F;

        // 뒤처진 속도 방향이 기수 방향을 따라 도는 속도(도/초)
        [Min(0.0F)]
        public float InertiaTurnRate = 180.0F;

        [Header("Camera")]

        // 가장 멀리 축소했을 때의 시야 크기(Orthographic Size). 플레이어만.
        [Min(1.0F)]
        public float MaxOrthographicSize = 15.0F;

        [Header("Radar")]

        // 레이더 탐지 거리. Ghost 복제 반경으로도 씀. 화면 대각선 절반보다 크게 잡을 것.
        [Min(1.0F)]
        public float RadarRange = 64.0F;

        // 레이더 스캐너 회전 속도(도/초)
        [Min(0.0F)]
        public float RadarSweepSpeed = 120.0F;

        // 스캐너가 지나간 뒤 레이더 점이 나타나기까지의 시간(초)
        [Min(0.0F)]
        public float RadarResponseTime = 0.1F;

        [Header("Faction")]

        // 소속 진영
        public FactionID Faction = FactionID.None;

        [Header("Upgrade")]

        // 강화 사양. 비우면 강화할 수 없는 기체.
        public SpaceshipUpgradeAsset Upgrade;
    }
}
