using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선이 마지막으로 쏜 뒤 지난 시간. 서버와 예측하는 클라이언트가 같은 규칙으로 계산함.
    /// 주인에게만 복제하며 예측 롤백 시 서버 값으로 되감음. 포탑 열이 식기 시작하는 시점을 정함.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct TurretStandbyComponent : IComponentData
    {
        // 마지막 발사 후 지난 시간(초). 무사격 대기 시간에서 멈춤.
        [GhostField(Quantization = 0)]
        public float NoFireTime;
    }

    /// <summary>
    /// 무사격 시간 규칙. 플레이어, AI 발사 시스템이 같이 씀.
    /// </summary>
    public static class TurretStandby
    {
        /// <summary>
        /// 이번 틱 발사 여부를 반영함. 대기 시간에 닿으면 더 세지 않음. 값이 그대로면 스냅샷에 실리지 않음.
        /// </summary>
        public static void Update(ref TurretStandbyComponent standby, bool fired, float deltaTime, float standbyTime)
        {
            standby.NoFireTime = fired ? 0.0F : math.min(standby.NoFireTime + deltaTime, standbyTime);
        }
    }
}
