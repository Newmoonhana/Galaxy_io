using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 서버 접속에 실패했음을 나타내는 태그.
    /// 시작 화면이 이 태그를 보고 경고를 띄운 뒤 제거함.
    /// </summary>
    public struct PlayerConnectionFailedTag : IComponentData
    {
    }
}
