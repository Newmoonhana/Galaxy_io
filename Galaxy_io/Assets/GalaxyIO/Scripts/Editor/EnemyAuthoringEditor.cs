using GalaxyIO.DOTS.Authoring;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GalaxyIO.Scripts.Editor
{
    /// <summary>
    /// <see cref="EnemyAuthoring"/>의 Scene 뷰 시야 편집 핸들.
    /// </summary>
    [CustomEditor(typeof(EnemyAuthoring))]
    [CanEditMultipleObjects]
    public sealed class EnemyAuthoringEditor : UnityEditor.Editor
    {
        private static readonly Color SightColor = new Color(1.0F, 0.9F, 0.3F, 0.8F);
        private static readonly Color SightFillColor = new Color(1.0F, 0.9F, 0.3F, 0.1F);
        private static readonly Color ChaseColor = new Color(1.0F, 0.3F, 0.3F, 0.8F);

        // ArcHandle 은 XZ 평면에 +Z 기준으로 그림. +Z -> 기수(+Y), 회전축 +Y -> -Z 로 눕힘.
        private static readonly Quaternion ToShipPlane = Quaternion.LookRotation(Vector3.up, Vector3.back);

        // 부채꼴의 반쪽만 편집함. 나머지 반쪽은 대칭으로 그림.
        private readonly ArcHandle handle = new ArcHandle();

        private void OnEnable()
        {
            handle.SetColorWithRadiusHandle(SightColor, SightFillColor.a);
        }

        private void OnSceneGUI()
        {
            var authoring = (EnemyAuthoring)target;
            Matrix4x4 local = authoring.transform.localToWorldMatrix;

            // 추격 거리는 우주선 위치 기준.
            using (new Handles.DrawingScope(ChaseColor, local * Matrix4x4.Rotate(ToShipPlane)))
            {
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, authoring.ChaseDistance);
            }

            Matrix4x4 sight = local * Matrix4x4.TRS(authoring.SightCenter, ToShipPlane, Vector3.one);

            using (new Handles.DrawingScope(SightColor, sight))
            {
                float halfAngle = authoring.SightAngle * 0.5F;
                float radius = authoring.SightRadius;

                // 대칭 반쪽
                Handles.DrawWireArc(Vector3.zero, Vector3.up, Vector3.forward, -halfAngle, radius);
                Handles.DrawLine(Vector3.zero, Quaternion.AngleAxis(-halfAngle, Vector3.up) * Vector3.forward * radius);

                using (new Handles.DrawingScope(SightFillColor))
                {
                    Handles.DrawSolidArc(Vector3.zero, Vector3.up, Vector3.forward, -halfAngle, radius);
                }

                handle.angle = halfAngle;
                handle.radius = radius;

                EditorGUI.BeginChangeCheck();
                handle.DrawHandle();

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                Undo.RecordObject(authoring, "Edit Enemy Sight");

                authoring.SightAngle = Mathf.Clamp(handle.angle * 2.0F, 0.0F, 360.0F);
                authoring.SightRadius = Mathf.Max(handle.radius, 0.0F);
            }
        }
    }
}