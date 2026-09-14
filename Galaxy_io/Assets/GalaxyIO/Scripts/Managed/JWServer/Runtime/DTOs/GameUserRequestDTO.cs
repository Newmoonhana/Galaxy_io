using System;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Config;
using Newmoonhana.Runtime.Utils.DEBUG;

namespace JWServer.Runtime.DTO
{
    /// <summary>
    /// JW 게임 서버로 요청 보낼때의 DTO<p/>
    /// Mandatory 표기 값: 반드시 존재해야 하는 컬럼<p/>
    /// Optional 표기 값: 상황에 따라 존재해야 하거나, 필수적이지 않은 컬럼들<p/>
    /// JsonUtility로 직렬화되므로 필드명이 곧 JSON 키가 됨 —> 반드시 서버 스펙과 동일하게 유지할 것
    /// </summary>
    [Serializable]
    public struct GameUserRequestDTO
    {
        /// <summary>
        /// JW 서버에서 발급한 게임 코드 (Mandatory)
        /// </summary>
        public string gameCode;

        /// <summary>
        /// 멤버 유형: 0(게임을 통한 순수가입자) / 1(SNS 로그인) (Mandatory)
        /// </summary>
        public int memberType;

        /// <summary>
        /// 액션 유형: JOIN(회원가입) / LOGIN(로그인) (Mandatory)
        /// </summary>
        public string actionType;

        /// <summary>
        /// OAuth 인증 제공자 ID (Optional)
        /// </summary>
        public string snsProviderId;

        /// <summary>
        /// OAuth 인증 제공자 이름 (Optional)
        /// </summary>
        public string snsProvider;

        /// <summary>
        /// 멤버 ID (Mandatory)
        /// </summary>
        public string memberId;

        /// <summary>
        /// 멤버 비밀번호 (Mandatory, 6글자 이상)
        /// </summary>
        public string memberPw;

        /// <summary>
        /// 멤버 이름 (실명) (Mandatory)
        /// </summary>
        public string memberName;

        /// <summary>
        /// 멤버 닉네임 (Optional)
        /// </summary>
        public string nickName;

        /// <summary>
        /// 이메일 주소 (Optional)
        /// </summary>
        public string email;

        /// <summary>
        /// 연락처1 (Optional)
        /// </summary>
        public string mobilePhone1;

        /// <summary>
        /// 연락처2 (Optional)
        /// </summary>
        public string mobilePhone2;

        /// <summary>
        /// 연락처3 (Optional)
        /// </summary>
        public string mobilePhone3;

        /// <summary>
        /// 본인인증 CI (개발 단계에선 Optional)
        /// </summary>
        public string ci;

        // [TEMP] _ci는 개발 단계 이후 어떻게 될지 모름. ' = "" ' 부분을 지울수도 있으니 참고
        public GameUserRequestDTO(string _gameCode, int _memberType, string _actionType,
            string _memberId, string _memberPw, string _memberName, string _ci = "",
            string _snsProviderId = "", string _snsProvider = "", string _nickName = "", string _email = "",
            string _mobilePhone1 = "", string _mobilePhone2 = "", string _mobilePhone3 = "")
        {
            gameCode = _gameCode;
            memberType = _memberType;
            actionType = _actionType;
            memberId = _memberId;
            memberPw = _memberPw;
            memberName = _memberName;
            ci = _ci;
            snsProviderId = _snsProviderId;
            snsProvider = _snsProvider;
            nickName = _nickName;
            email = _email;
            mobilePhone1 = _mobilePhone1;
            mobilePhone2 = _mobilePhone2;
            mobilePhone3 = _mobilePhone3;
        }

        public override int GetHashCode()
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return -1;
        }
        public override bool Equals(object obj)
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return false;
        }
        public bool Equals(GameUserRequestDTO other)
        {
            if (gameCode != other.gameCode
                || memberType != other.memberType
                || actionType != other.actionType
                || memberId != other.memberId
                || memberPw != other.memberPw
                || memberName != other.memberName)
                return false;

            return true;
        }
        public static bool operator ==(GameUserRequestDTO a, GameUserRequestDTO b) => a.Equals(b);
        public static bool operator !=(GameUserRequestDTO a, GameUserRequestDTO b) => !(a.Equals(b));
        public bool IsVaild()
        {
            if (string.IsNullOrEmpty(gameCode)) return false;
            if (memberType is < 0 or > 1) return false;
            if (string.IsNullOrEmpty(actionType) || (actionType != "JOIN" && actionType != "LOGIN")) return false;
            if (string.IsNullOrEmpty(memberId)) return false;
            if (string.IsNullOrEmpty(memberPw)) return false;
            if (string.IsNullOrEmpty(memberName)) return false;
            // TODO -> ci가 개발단계에선 Optional이라 해서 일단 이렇게 구현해놓음. 추후 작동 방식에 따라 조건식 구조 변경 필요.
            if (SO_NmhnConfigData.Instance.ModeType == MODETYPE.BUILD && string.IsNullOrEmpty(ci))
                return false;
            DebugLog.Log($"data = {gameCode} / {memberType} / {actionType} / {memberId} / {memberPw} / {memberName} / {ci}");
            return true;
        }
    }
}
