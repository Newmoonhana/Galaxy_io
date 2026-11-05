using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어 이름. 우주선에 붙어 복제되고, 서버에서는 리스폰용으로 Connection 에도 붙음.
    /// </summary>
    public struct PlayerNameComponent : IComponentData
    {
        // 이름 글자 수 제한. 한글 기준 3바이트씩이라 FixedString64Bytes 에 들어감.
        public const int MaxLength = 12;

        [GhostField]
        public FixedString64Bytes Value;

        /// <summary>
        /// 이름 검증. 클라이언트 입력창과 서버가 같은 규칙을 씀. 앞뒤 공백을 지운 이름을 돌려줌.
        /// 글자 수는 TMP_InputField.characterLimit 와 같은 UTF-16 길이 기준.
        /// </summary>
        public static PlayerNameError Validate(string _name, out string _trimmed)
        {
            _trimmed = (_name ?? string.Empty).Trim();

            if (_trimmed.Length == 0) return PlayerNameError.Empty;
            if (_trimmed.Length > MaxLength) return PlayerNameError.TooLong;

            return PlayerNameError.None;
        }
    }

    /// <summary>
    /// 이름 검증 결과.
    /// </summary>
    public enum PlayerNameError : byte
    {
        None,
        Empty,
        TooLong,
    }
}