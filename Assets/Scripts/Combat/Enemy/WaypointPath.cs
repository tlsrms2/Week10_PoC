using System.Collections.Generic;
using UnityEngine;

namespace Overlap.Combat.Enemy
{
    /// <summary>
    /// 씬에 배치하는 적 이동 경로 컴포넌트입니다.
    /// 
    /// ScriptableObject 대신 MonoBehaviour를 사용하는 이유:
    ///   - Gizmos와 Handles를 통해 씬 뷰에서 타일맵을 보면서 직관적으로 경로 편집 가능
    ///   - Scene Overlay나 Custom Editor를 통해 클릭으로 포인트 추가/드래그/삭제 지원
    /// 
    /// 사용법:
    ///   1. 빈 GameObject에 이 컴포넌트를 추가합니다.
    ///   2. Inspector에서 [Add Point] 버튼으로 포인트를 추가하거나,
    ///      씬 뷰에서 Shift+Click으로 포인트를 추가합니다.
    ///   3. 씬 뷰에서 노란 핸들을 드래그해 위치를 조정합니다.
    /// </summary>
    public sealed class WaypointPath : MonoBehaviour
    {
        [SerializeField] private List<Vector3> worldPoints = new List<Vector3>();

        [Header("Gizmos")]
        [SerializeField] private Color pathColor      = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private Color waypointColor  = Color.yellow;
        [SerializeField] private float waypointRadius = 0.18f;

        // ── 경로 데이터 API ───────────────────────────────────────────────
        public int Count => worldPoints.Count;

        /// <summary>인덱스로 웨이포인트를 가져옵니다. 범위 초과 시 마지막 포인트를 반환합니다.</summary>
        public Vector3 GetPoint(int index)
        {
            if (worldPoints.Count == 0) return transform.position;
            return worldPoints[Mathf.Clamp(index, 0, worldPoints.Count - 1)];
        }

        public bool IsLastIndex(int index) => index >= worldPoints.Count - 1;

        public void SetPoints(List<Vector3> points)
        {
            worldPoints = new List<Vector3>(points);
        }

        public WaypointPath CreateReversedPath()
        {
            var go = new GameObject($"{gameObject.name}_Reversed");
            // 런타임에만 존재하는 임시 오브젝트이므로 하이어라키에서 숨기고 저장 안 함
            go.hideFlags = HideFlags.HideAndDontSave;
            
            var reversed = go.AddComponent<WaypointPath>();
            var points = new List<Vector3>(worldPoints);
            points.Reverse();
            reversed.SetPoints(points);
            
            return reversed;
        }

        // ── 에디터 전용 API (Editor 스크립트에서 호출) ─────────────────────
#if UNITY_EDITOR
        public List<Vector3> EditorPoints => worldPoints;

        public void EditorAddPoint()
        {
            var lastPos = worldPoints.Count > 0
                ? worldPoints[^1] + Vector3.right
                : transform.position;
            worldPoints.Add(lastPos);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorRemovePoint(int index)
        {
            if (index < 0 || index >= worldPoints.Count) return;
            worldPoints.RemoveAt(index);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        // ── Gizmos ────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (worldPoints == null || worldPoints.Count == 0) return;

            Gizmos.color = pathColor;
            for (var i = 0; i < worldPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(worldPoints[i], worldPoints[i + 1]);
            }

            Gizmos.color = waypointColor;
            for (var i = 0; i < worldPoints.Count; i++)
            {
                Gizmos.DrawWireSphere(worldPoints[i], waypointRadius);
                // 첫 포인트(스폰), 마지막 포인트(코어 방향)는 크게 강조
                if (i == 0 || i == worldPoints.Count - 1)
                {
                    Gizmos.DrawWireSphere(worldPoints[i], waypointRadius * 1.6f);
                }
            }
        }
    }
}
