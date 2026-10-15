using GalaxyIO.DOTS.Authoring;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// <see cref="BoxColliderAuthoring"/>의 Scene 뷰 크기 조절 도구. 선택하면 Tools 오버레이에 버튼이 생김.
    /// </summary>
    [EditorTool("Edit Box Collider", typeof(BoxColliderAuthoring))]
    public sealed class BoxColliderAuthoringTool : EditorTool
    {
        private readonly BoxBoundsHandle handle = new BoxBoundsHandle
        {
            // 2D 게임이라 Z축 핸들은 감춤.
            axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y,
        };

        private GUIContent icon;

        public override GUIContent toolbarIcon =>
            icon ??= new GUIContent(EditorGUIUtility.IconContent("EditCollider").image, "Edit the collider geometry.");

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView))
            {
                return;
            }

            foreach (Object obj in targets)
            {
                if (!(obj is BoxColliderAuthoring authoring))
                {
                    continue;
                }

                // 오브젝트의 회전 / 스케일을 물려받아 핸들이 선체와 같이 기울어짐.
                using (new Handles.DrawingScope(BoxColliderAuthoring.GizmoColor, authoring.transform.localToWorldMatrix))
                {
                    handle.center = authoring.Center;
                    handle.size = authoring.Size;

                    EditorGUI.BeginChangeCheck();
                    handle.DrawHandle();

                    if (!EditorGUI.EndChangeCheck())
                    {
                        continue;
                    }

                    Undo.RecordObject(authoring, "Edit Box Collider");

                    authoring.Center = handle.center;
                    authoring.Size = new Vector2(Mathf.Abs(handle.size.x), Mathf.Abs(handle.size.y));
                }
            }
        }
    }
}
