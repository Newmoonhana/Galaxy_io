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
        // 입력창 글자 수 제한. 한글 기준 3바이트씩이라 FixedString64Bytes 에 들어감.
        public const int MaxLength = 12;

        [GhostField]
        public FixedString64Bytes Value;
    }
}