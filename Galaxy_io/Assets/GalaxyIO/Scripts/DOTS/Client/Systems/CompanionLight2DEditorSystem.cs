#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 에디터 전용. Light2D 를 가진 Companion 을 활성 씬으로 옮겨 게임 카메라에 비치게 하는 시스템.
    /// <p/>
    /// 에디터의 Companion 은 프리뷰 씬에 생기고, URP 2D 는 프리뷰 씬의 라이트를 게임 카메라에 쓰지 않음.
    /// 빌드에서는 Companion 이 활성 씬에 생기므로 필요 없음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CompanionLight2DEditorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CompanionComponent<Light2D>>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            foreach (var companion in SystemAPI.Query<RefRO<CompanionComponent<Light2D>>>())
            {
                Light2D light = companion.ValueRO.CompanionRef.Value;
                if (light == null) continue;

                GameObject go = light.gameObject;
                if (!EditorSceneManager.IsPreviewSceneObject(go)) continue;

                // 끄면 프리뷰 씬 목록에서 빠지고, 옮긴 뒤 켜면 게임 카메라 목록에 들어감.
                bool wasEnabled = light.enabled;
                light.enabled = false;
                SceneManager.MoveGameObjectToScene(go, activeScene);
                light.enabled = wasEnabled;
            }
        }
    }
}
#endif
