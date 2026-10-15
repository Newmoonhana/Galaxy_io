using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    public enum ChatChannel : byte
    {
        Public,
        Whisper,

        // 서버 안내문. 서버만 보냄.
        System,

        // 파티원에게만 보내는 채팅
        Party,
    }

    /// <summary>
    /// 채팅 한 건. 클라이언트가 서버로 보내고, 서버가 받을 클라이언트에게 다시 보냄.
    /// </summary>
    public struct ChatRPC : IRpcCommand
    {
        // 입력창 글자 수 제한. 한글 기준 3바이트씩이라 FixedString512Bytes 에 들어감.
        public const int MaxLength = 100;

        public ChatChannel Channel;

        // 보낸 플레이어. 서버가 채움.
        public FixedString64Bytes Sender;

        // 보낸 플레이어의 NetworkId. 서버가 채움. 받은 쪽이 자신의 글인지 가리고 말풍선을 띄울 우주선을 찾는 데 씀.
        // 파티 초대 안내문에서는 초대한 플레이어.
        public int SenderNetworkId;

        // 귓속말을 받는 플레이어. 서버가 채움. 안내문에서는 번역문의 {0} 에 들어갈 값.
        public FixedString64Bytes Target;

        // 본문. 클라이언트가 보내는 귓속말은 "{받는 이름} {본문}". 안내문은 번역 키.
        public FixedString512Bytes Text;

        // 귓속말을 보낸 쪽에 돌려주는 사본인지
        public bool Echo;
    }
}