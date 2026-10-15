using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 파티 구성. 서버가 구성이 바뀔 때마다 파티원 모두에게 보내고, 파티를 나간 사람에게는 빈 구성을 보냄.
    /// 클라이언트는 파티 탭과 닉네임 메뉴의 파티 명령어 표시에 씀.
    /// </summary>
    public struct PartyRosterRPC : IRpcCommand
    {
        // 파티장 NetworkId. 파티가 없으면 0.
        public int Master;

        // 파티원 NetworkId. 들어온 순서. 비어 있으면 파티 없음.
        public FixedList512Bytes<int> Members;

        // 파티 명령어 권한을 받은 파티원
        public FixedList512Bytes<int> Authorized;
    }
}
