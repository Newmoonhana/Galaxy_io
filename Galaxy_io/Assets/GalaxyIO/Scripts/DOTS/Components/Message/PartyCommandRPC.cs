using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    public enum PartyCommand : byte
    {
        // 파티 초대. 파티가 없으면 받아들인 순간 초대한 쪽이 파티장인 파티가 생김.
        Invite,

        // 초대 받아들이기. 채팅창의 [수락] 링크로만 보냄.
        Accept,

        // 파티장 넘기기
        Delegate,

        // 강퇴
        Kick,

        // 파티 명령어 권한 주기
        Authorize,

        // 파티 명령어 권한 회수
        Revoke,

        // 파티 나가기
        Quit,
    }

    /// <summary>
    /// 채팅창의 `/p` 명령어. 클라이언트가 서버로 보냄. 결과는 <see cref="ChatRPC"/> 안내문으로 돌아옴.
    /// </summary>
    public struct PartyCommandRPC : IRpcCommand
    {
        public PartyCommand Command;

        // 대상 플레이어 이름. Accept, Quit 은 비어 있음.
        public FixedString64Bytes Target;

        // Accept 가 받아들일 초대를 보낸 플레이어의 NetworkId
        public int Inviter;
    }
}