using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// 유니티가 기본 지원하지 않는 Companion 타입(<see cref="TrailRenderer"/>)을 베이킹 대상에 등록함.
    /// </summary>
    [InitializeOnLoad]
    public static class CompanionTypeRegistration
    {
        static CompanionTypeRegistration()
        {
            // 등록 API 가 internal 이라 리플렉션으로 호출함. 베이킹은 에디터에서만 일어나므로 빌드에는 필요 없음.
            typeof(Baker<>).Assembly
                .GetType("Unity.Entities.BakingUtility")
                .GetMethod("AddAdditionalCompanionComponentType", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { (ComponentType)typeof(TrailRenderer) });
        }
    }
}
