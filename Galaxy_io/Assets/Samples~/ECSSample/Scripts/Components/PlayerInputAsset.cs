using Unity.Entities;
using UnityEngine.InputSystem;

namespace GalaxyIO
{
    /// <summary>
    /// <see cref="SimulationAuthoring"/>의 인스펙터에 지정된 <see cref="InputActionAsset"/>을
    /// 그대로 엔티티 월드로 전달하기 위한 Managed 컴포넌트입니다.
    ///
    /// <see cref="InputActionAsset"/>은 <c>UnityEngine.Object</c>이므로 Unmanaged 컴포넌트에는
    /// 담을 수 없고, 클래스 기반의 Managed <see cref="IComponentData"/>로만 보관할 수 있습니다.
    /// 이 컴포넌트를 읽는 쪽은 메인 스레드에서 동작하는 <see cref="PlayerInputSystem"/> 하나뿐이며,
    /// 샘플링 결과는 곧바로 Unmanaged 싱글톤인 <see cref="PlayerInputState"/>로 복사되므로
    /// 이후 단계는 그대로 Burst 호환입니다.
    /// </summary>
    public class PlayerInputAsset : IComponentData
    {
        public InputActionAsset Value;
    }
}
