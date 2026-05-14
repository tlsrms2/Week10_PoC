using UnityEngine;
using UnityEditor;

namespace Overlap.Combat.Enemy
{
    [UnityEditor.CustomEditor(typeof(WaypointPath))]
    public sealed class WaypointPathEditor : UnityEditor.Editor
    {
        private WaypointPath path;

        private void OnEnable()
        {
            path = (WaypointPath)target;
        }

        // ── Inspector GUI ──────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            // 기본 필드 (pathColor, waypointColor, radius 등) 표시
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space(6f);
            UnityEditor.EditorGUILayout.LabelField("Waypoint Controls", UnityEditor.EditorStyles.boldLabel);

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋ Add Point"))
                {
                    UnityEditor.Undo.RecordObject(path, "Add Waypoint");
                    path.EditorAddPoint();
                }

                using (new UnityEditor.EditorGUI.DisabledScope(path.EditorPoints.Count == 0))
                {
                    if (GUILayout.Button("－ Remove Last"))
                    {
                        UnityEditor.Undo.RecordObject(path, "Remove Waypoint");
                        path.EditorRemovePoint(path.EditorPoints.Count - 1);
                    }
                }
            }

            UnityEditor.EditorGUILayout.HelpBox(
                "씬 뷰에서 노란 핸들을 드래그해 경로를 편집하세요.\n" +
                "Shift+Click으로 마지막에 포인트를 추가합니다.",
                UnityEditor.MessageType.None);
        }

        // ── Scene View 핸들 ───────────────────────────────────────────────
        private void OnSceneGUI()
        {
            if (path == null || path.EditorPoints == null) return;

            var points  = path.EditorPoints;
            var changed = false;

            // Shift+Click → 새 포인트 추가
            var evt = UnityEngine.Event.current;
            if (evt.type == UnityEngine.EventType.MouseDown &&
                evt.button == 0 && evt.shift)
            {
                var ray       = UnityEditor.HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                var worldPos  = ray.origin;
                worldPos.z    = 0f; // 2D

                UnityEditor.Undo.RecordObject(path, "Add Waypoint (Shift+Click)");
                points.Add(worldPos);
                UnityEditor.EditorUtility.SetDirty(path);
                evt.Use();
            }

            // 각 포인트에 Position Handle 표시
            UnityEditor.Handles.color = Color.yellow;
            for (var i = 0; i < points.Count; i++)
            {
                UnityEditor.EditorGUI.BeginChangeCheck();

                var newPos = UnityEditor.Handles.PositionHandle(points[i], Quaternion.identity);

                if (UnityEditor.EditorGUI.EndChangeCheck())
                {
                    UnityEditor.Undo.RecordObject(path, "Move Waypoint");
                    newPos.z   = 0f; // 2D 고정
                    points[i]  = newPos;
                    changed    = true;
                }

                // 포인트 번호 라벨
                var labelStyle = new GUIStyle
                {
                    normal   = { textColor = Color.white },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                UnityEditor.Handles.Label(points[i] + Vector3.up * 0.3f,
                    i == 0                  ? $"[{i}] START" :
                    i == points.Count - 1   ? $"[{i}] END" :
                                              $"[{i}]",
                    labelStyle);
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(path);
            }
        }
    }
}
