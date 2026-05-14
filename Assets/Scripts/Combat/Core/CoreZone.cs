using UnityEngine;
using Overlap.Combat.Enemy;

namespace Overlap.Combat.Core
{
    /// <summary>
    /// 거점 코어의 피격 감지 영역을 정의하는 컴포넌트입니다.
    /// 
    /// 역할:
    ///   - BoxCollider2D (isTrigger)로 적이 코어 영역에 진입했을 때 감지
    ///   - CoreHealth.TakeHit()를 호출하여 데미지 전달
    ///   - 씬 뷰 Gizmos로 피격 범위를 항상 시각화 (선택 안 해도 표시)
    /// 
    /// 씬 세팅 방법:
    ///   1. 거점 GameObject에 이 컴포넌트 추가 (BoxCollider2D 자동 생성)
    ///   2. BoxCollider2D의 Size를 타일맵의 거점 크기(예: 9칸 = 3x3 → size(3,3))에 맞춤
    ///   3. Inspector에서 CoreHealth 레퍼런스 연결
    ///   4. DamagePerEnemy로 적 한 마리가 통과할 때 깎이는 코어 HP 설정
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CoreZone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CoreHealth coreHealth;

        [Header("Gizmos")]
        [SerializeField] private Color fillColor    = new Color(1f, 0.15f, 0.15f, 0.20f);
        [SerializeField] private Color borderColor  = new Color(1f, 0.15f, 0.15f, 0.85f);
        [SerializeField] private bool  alwaysShow   = true;

        private BoxCollider2D zone;

        // ── 수명주기 ──────────────────────────────────────────────────────
        private void Awake()
        {
            zone          = GetComponent<BoxCollider2D>();
            zone.isTrigger = true;
        }

        // ── 트리거 감지 제거 (이제 EnemyUnit이 자체적으로 도달 판정) ───────────

        // ── 에디터 시각화 ─────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!alwaysShow) return;
            DrawZoneGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (alwaysShow) return; // 이미 OnDrawGizmos에서 그렸으면 중복 생략
            DrawZoneGizmo();
        }

        private void DrawZoneGizmo()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            // 로컬 오프셋 + 스케일 고려한 월드 좌표 계산
            var worldCenter = transform.TransformPoint(col.offset);
            var worldSize   = new Vector3(
                col.size.x * transform.lossyScale.x,
                col.size.y * transform.lossyScale.y,
                0f);

            // 반투명 채움
            Gizmos.color  = fillColor;
            Gizmos.DrawCube(worldCenter, worldSize);

            // 테두리
            Gizmos.color  = borderColor;
            Gizmos.DrawWireCube(worldCenter, worldSize);

            // 라벨
#if UNITY_EDITOR
            var labelStyle = new GUIStyle
            {
                normal   = { textColor = borderColor },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            UnityEditor.Handles.Label(worldCenter + Vector3.up * (worldSize.y * 0.5f + 0.25f),
                "CORE ZONE", labelStyle);
#endif
        }

        // ── OnValidate: isTrigger 자동 강제 설정 ─────────────────────────
        private void OnValidate()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
        }
    }
}
