using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 클라이언트 접속이 끊긴 사유. 안내창 표기용으로 관리 코드가 읽음.
    /// </summary>
    public struct ClientDisconnectedMessage : IComponentData
    {
        public FixedString32Bytes Reason;
    }
}
