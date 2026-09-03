using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Components
{
    /// <summary>
    /// 클라이언트 사용자의 입력을 실시간으로 추적해서 기록해놓은 데이터
    /// </summary>
    public struct PlayerInputComponent : IInputComponentData
    {
        // 터렛 사격 버튼이 눌렸는지
        public bool TurretTriggered;

        // 터렛 사격 버튼이 최초로 눌렸는지 판단. 버튼을 뗐다가 다시
        // 누를 때까지 False값 유지.
        public bool TurretTriggeredInThisFrame;

        // 주포 사격 버튼이 눌렸는지
        public bool ArtilleryTriggered;

        // 애프터버너 어빌리티 발동 버튼이 눌렸는지
        public bool AfterburnerTriggered;

        // WASD 또는 상하좌우 화살표 버튼 입력의 합산 벡터
        public float2 MoveArrows;

        // 마우스 커서의 위치
        public float2 MouseCursor;
    }
}