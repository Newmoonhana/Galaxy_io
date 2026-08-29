using NUnit.Framework.Interfaces;
using Unity.Entities;

namespace GalaxyIO.Components.Tags
{
    /// <summary>
    /// 해당 함선이 로컬에서 플레이어가 조종 중임을 나타내는 태그.
    /// 클라이언트당 1개 (싱글톤), 서버에는 존재 x.
    /// </summary>
    public struct LocalPlayerTag : IComponentData
    {
    }
}