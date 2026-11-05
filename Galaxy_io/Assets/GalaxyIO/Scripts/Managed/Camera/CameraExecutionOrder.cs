namespace GalaxyIO.Camera
{
    /// <summary>
    /// 카메라 관련 실행 순서 상수.
    /// </summary>
    public static class CameraExecutionOrder
    {
        /// <summary>
        /// CinemachineBrain(100) 이 카메라를 옮긴 뒤. 카메라 위치/시야를 LateUpdate 에서 읽는 컴포넌트용.
        /// </summary>
        public const int AfterBrain = 1000;
    }
}
