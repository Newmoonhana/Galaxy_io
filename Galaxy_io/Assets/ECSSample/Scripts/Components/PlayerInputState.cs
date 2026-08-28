using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>
    /// <c>PlayerInputSystem</c>이 프레임당 한 번 작성하고 <c>PlayerMovementSystem</c>이 읽는
    /// 싱글톤입니다. 이는 Managed한 엔진 입력을 Burst로 컴파일된 작업(job)으로 전달하는 표준적인 방법으로,
    /// 메인 스레드에서 입력을 샘플링한 뒤 단순 데이터 형태로 게시하는 방식입니다.
    /// </summary>
    public struct PlayerInputState : IComponentData
    {
        public float2 Move;
    }
}