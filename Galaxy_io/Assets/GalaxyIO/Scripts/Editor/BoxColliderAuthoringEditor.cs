using GalaxyIO.DOTS.Authoring;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// <see cref="BoxColliderAuthoring"/>의 Scene 뷰 크기 조절 핸들.
    /// </summary>
    [CustomEditor(typeof(BoxColliderAuthoring))]
    [CanEditMultipleObjects]
    public sealed class BoxColliderAuthoringEditor : UnityEditor.Editor
    {
        private readonly BoxBoundsHandle handle = new BoxBoundsHandle
        {
            // 2D 게임이라 Z축 핸들은 감춤.
            axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y,
        };

        private void OnSceneGUI()
        {
            var authoring = (BoxColliderAuthoring)target;

            // 오브젝트의 회전 / 스케일을 물려받아 핸들이 선체와 같이 기울어짐.
            using (new Handles.DrawingScope(new Color(0.4F, 1.0F, 0.5F, 0.8F), authoring.transform.localToWorldMatrix))
            {
                handle.center = authoring.Center;
                handle.size = authoring.Size;

                EditorGUI.BeginChangeCheck();
                handle.DrawHandle();

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                Undo.RecordObject(authoring, "Edit Box Collider");

                authoring.Center = handle.center;
                authoring.Size = new Vector2(Mathf.Abs(handle.size.x), Mathf.Abs(handle.size.y));
            }
        }
    }
}