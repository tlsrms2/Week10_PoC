using System;
using System.Collections.Generic;
using Overlap.Tilemaps;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Overlap.WorldMap
{
    /// <summary>
    /// 게임 전체의 턴/페이즈 흐름을 관리합니다.
    ///
    /// 페이즈 전환 흐름:
    ///
    ///   [게임 시작]
    ///       └─▶ Planning (턴 N 시작)
    ///               │
    ///               ├─ 플레이어: PlaceTower   ──▶ 타워 배치 후 EndTurn()
    ///               │
    ///               ├─ 플레이어: OccupyHub    ──▶ Defense 페이즈
    ///               │                               │
    ///               │                               └─ WaveCleared ──▶ 점령 성공 ──▶ Planning (턴 N+1)
    ///               │
    ///               └─ 침략 이벤트 발생        ──▶ Defense 페이즈
    ///                                               │
    ///                                               └─ WaveCleared ──▶ Planning (턴 N+1)
    ///
    /// 외부(UI)는 이 클래스의 공개 메서드와 이벤트만 사용합니다.
    /// </summary>
    public sealed class TurnManager : MonoBehaviour
    {
        // ── 이벤트 ───────────────────────────────────────────────────────────
        /// <summary>Planning 페이즈가 시작될 때 (턴 번호 전달)</summary>
        public event Action<int> PlanningPhaseStarted;

        /// <summary>Defense 페이즈가 시작될 때 (대상 거점 전달)</summary>
        public event Action<HubNode> DefensePhaseStarted;

        /// <summary>Defense 페이즈가 끝날 때 (성공 여부 전달)</summary>
        public event Action<bool> DefensePhaseEnded;

        /// <summary>침략 일정이 추가될 때 (대상 거점, 남은 턴 전달)</summary>
        public event Action<HubNode, int> InvasionScheduled;

        /// <summary>침략이 활성화되어 반드시 디펜스해야 할 때 (대상 거점 전달)</summary>
        public event Action<HubNode> InvasionActivated;

        public event Action GameOver;

        // ── 데이터 구조 ───────────────────────────────────────────────────────
        [Serializable]
        public class ScheduledInvasion
        {
            public HubNode TargetHub;
            public int RemainingTurns;
        }

        // ── Inspector 설정 ────────────────────────────────────────────────────
        [Header("World Map")]
        [SerializeField] private HubNode mainHub;
        [SerializeField] private HubNode[] allHubs = Array.Empty<HubNode>();

        [Header("Placement")]
        [SerializeField] private TilemapPlacementController placementController;

        [Header("Invasion Settings")]
        [Tooltip("턴 종료 시 침략이 스케줄링될 확률 (0.0 ~ 1.0)")]
        [SerializeField, Range(0f, 1f)] private float invasionProbability = 0.3f;
        [Tooltip("침략 스케줄링 시 몇 턴 뒤에 발생하는지")]
        [SerializeField, Min(1)] private int minInvasionDelayTurns = 1;
        [SerializeField, Min(1), FormerlySerializedAs("invasionDelayTurns")] private int maxInvasionDelayTurns = 4;

        // ── 런타임 상태 ───────────────────────────────────────────────────────
        private TurnPhase  currentPhase  = TurnPhase.None;
        private TurnAction currentAction = TurnAction.None;
        private int        turnNumber    = 0;
        private HubNode    activeDefenseHub;
        private bool       isGameOver;

        private List<ScheduledInvasion> scheduledInvasions = new List<ScheduledInvasion>();
        private List<HubNode>           activeInvasions    = new List<HubNode>();

        public TurnPhase  Phase      => currentPhase;
        public TurnAction Action     => currentAction;
        public int        TurnNumber => turnNumber;
        public IReadOnlyList<HubNode> ActiveInvasions => activeInvasions;
        public IReadOnlyList<ScheduledInvasion> ScheduledInvasions => scheduledInvasions;

        // ── 수명주기 ──────────────────────────────────────────────────────────
        private void Awake()
        {
            allHubs = FindObjectsByType<HubNode>(FindObjectsSortMode.None);
            Debug.Log($"[TurnManager] Auto-assigned {allHubs.Length} HubNodes.");

            if (mainHub != null)
            {
                mainHub.ForceSetMain();
                if (mainHub.CoreHealth != null)
                {
                    mainHub.CoreHealth.Destroyed += OnMainCoreDestroyed;
                }
            }
        }

        private void OnDestroy()
        {
            if (mainHub != null && mainHub.CoreHealth != null)
            {
                mainHub.CoreHealth.Destroyed -= OnMainCoreDestroyed;
            }
        }

        private void Start()
        {
            StartPlanning();
        }

        // ── 공개 API (UI / 플레이어 입력이 호출) ─────────────────────────────

        /// <summary>
        /// 현재 턴의 행동을 마치고 다음 턴 Planning으로 넘어갑니다.
        /// (PlaceTower 행동 후 버튼 클릭 등)
        /// </summary>
        public void EndTurn()
        {
            if (isGameOver) return;

            if (currentPhase != TurnPhase.Planning)
            {
                Debug.LogWarning("[TurnManager] EndTurn called outside Planning phase.");
                return;
            }

            if (activeInvasions.Count > 0)
            {
                Debug.LogWarning("[TurnManager] Cannot end turn! You must handle active invasions first.");
                return;
            }

            Debug.Log($"[TurnManager] Turn {turnNumber} ended.");
            RollForNewInvasion();
            StartPlanning();
        }

        /// <summary>
        /// 빈 거점을 선택하여 점령을 시도합니다.
        /// 내부적으로 Defense 페이즈로 전환됩니다.
        /// </summary>
        public void TryOccupyHub(HubNode hub)
        {
            if (isGameOver) return;

            if (currentPhase != TurnPhase.Planning)
            {
                Debug.LogWarning("[TurnManager] Cannot occupy hub outside Planning phase.");
                return;
            }

            if (hub == null || !hub.IsEmpty)
            {
                Debug.LogWarning("[TurnManager] Target hub is null or not empty.");
                return;
            }

            currentAction   = TurnAction.OccupyHub;
            activeDefenseHub = hub;

            hub.OccupationSucceeded += OnOccupationSucceeded;
            hub.OccupationFailed    += OnOccupationFailed;

            EnterDefensePhase(hub);
            hub.BeginOccupation();
        }

        /// <summary>
        /// 활성화된 침략을 방어합니다.
        /// </summary>
        public void TryHandleInvasion(HubNode invasionHub)
        {
            if (isGameOver) return;

            if (currentPhase != TurnPhase.Planning) return;
            if (!activeInvasions.Contains(invasionHub))
            {
                Debug.LogWarning($"[TurnManager] Hub {invasionHub?.HubName} does not have an active invasion.");
                return;
            }

            currentAction   = TurnAction.HandleInvasion;
            activeDefenseHub = invasionHub;

            // 이벤트 구독
            invasionHub.InvasionRepelledEvent += OnInvasionRepelled;
            if (invasionHub.CoreHealth != null)
                invasionHub.CoreHealth.Destroyed += OnInvasionFailed;
            else
                Debug.LogWarning($"[TurnManager] Invaded Hub {invasionHub.HubName} has no CoreHealth attached! Defense cannot fail by HP.");

            EnterDefensePhase(invasionHub);
            invasionHub.BeginInvasion();
        }

        private void OnInvasionRepelled(HubNode hub)
        {
            hub.InvasionRepelledEvent -= OnInvasionRepelled;
            if (hub.CoreHealth != null)
                hub.CoreHealth.Destroyed -= OnInvasionFailed;
                
            ExitDefensePhase(true);
        }

        private void OnInvasionFailed()
        {
            if (activeDefenseHub != null)
            {
                activeDefenseHub.InvasionRepelledEvent -= OnInvasionRepelled;
                if (activeDefenseHub.CoreHealth != null)
                    activeDefenseHub.CoreHealth.Destroyed -= OnInvasionFailed;
            }

            ExitDefensePhase(false);
        }

        // ── 내부 페이즈 전환 ──────────────────────────────────────────────────
        private void OnMainCoreDestroyed()
        {
            if (isGameOver)
            {
                return;
            }

            isGameOver = true;
            currentPhase = TurnPhase.GameOver;
            currentAction = TurnAction.None;

            if (placementController != null)
            {
                placementController.enabled = false;
            }

            StopAllCoroutines();

            var spawners = FindObjectsByType<Combat.Wave.WaveSpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                spawner.StopWave();
            }

            var enemies = FindObjectsByType<Combat.Enemy.EnemyUnit>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.gameObject != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            Debug.LogWarning("[TurnManager] Game over. Main core destroyed.");
            GameOver?.Invoke();
        }

        private void StartPlanning()
        {
            if (isGameOver) return;

            turnNumber++;
            currentPhase  = TurnPhase.Planning;
            currentAction = TurnAction.None;

            // 타워 배치 활성화
            if (placementController != null)
                placementController.enabled = true;

            Debug.Log($"[TurnManager] === Planning Phase — Turn {turnNumber} ===");
            
            // 침략 스케줄 업데이트
            UpdateInvasionSchedules();

            PlanningPhaseStarted?.Invoke(turnNumber);
        }

        private void ResumePlanning()
        {
            if (isGameOver) return;

            currentPhase  = TurnPhase.Planning;
            currentAction = TurnAction.None;

            // 타워 배치 다시 활성화
            if (placementController != null)
                placementController.enabled = true;

            Debug.Log($"[TurnManager] === Resumed Planning Phase (Still Turn {turnNumber}) ===");
            PlanningPhaseStarted?.Invoke(turnNumber);
        }

        private void EnterDefensePhase(HubNode hub)
        {
            if (isGameOver) return;

            currentPhase = TurnPhase.Defense;

            // 디펜스 중에는 타워 배치 차단
            if (placementController != null)
                placementController.enabled = false;

            Debug.Log($"[TurnManager] === Defense Phase — Hub: {hub.HubName} ===");
            DefensePhaseStarted?.Invoke(hub);
        }

        private void ExitDefensePhase(bool success)
        {
            StartCoroutine(ExitDefensePhaseRoutine(success));
        }

        private System.Collections.IEnumerator ExitDefensePhaseRoutine(bool success)
        {
            if (isGameOver) yield break;

            // 디펜스 실패 시 공통 페널티 (점령/침략 무관)
            if (!success && activeDefenseHub != null)
            {
                Debug.Log($"[TurnManager] Defense at {activeDefenseHub.HubName} failed. Penalty: destroying nearby blocks!");
                if (placementController != null)
                {
                    // 반경 7칸 이내의 블록 철거
                    placementController.ClearBlocksAround(activeDefenseHub.transform.position, 7);
                }
            }

            if (currentAction == TurnAction.HandleInvasion)
            {
                if (success)
                {
                    activeInvasions.Remove(activeDefenseHub);
                    Debug.Log($"[TurnManager] Invasion at {activeDefenseHub.HubName} cleared.");
                }
                else
                {
                    activeInvasions.Remove(activeDefenseHub);
                    // 침략당한 거점이 점령지였다면 다시 빈 거점으로 초기화
                    if (activeDefenseHub != null && !activeDefenseHub.IsMain)
                    {
                        activeDefenseHub.ResetToEmpty();
                    }
                }
            }

            // 모든 진행 중인 웨이브 스포너 강제 중지 및 남은 적 즉시 파괴
            var spawners = FindObjectsByType<Combat.Wave.WaveSpawner>(FindObjectsSortMode.None);
            foreach (var sp in spawners) { sp.StopWave(); }

            var allEnemies = FindObjectsByType<Combat.Enemy.EnemyUnit>(FindObjectsSortMode.None);
            foreach (var e in allEnemies) 
            { 
                if (e != null && e.gameObject != null) Destroy(e.gameObject); 
            }

            var finishedHub = activeDefenseHub;
            activeDefenseHub = null;
            DefensePhaseEnded?.Invoke(success);

            // 결과 확인을 위해 2초 대기
            yield return new WaitForSeconds(2f);

            ResumePlanning();
        }

        // ── 거점 결과 콜백 ────────────────────────────────────────────────────
        private void OnOccupationSucceeded(HubNode hub)
        {
            hub.OccupationSucceeded -= OnOccupationSucceeded;
            hub.OccupationFailed    -= OnOccupationFailed;
            Debug.Log($"[TurnManager] Hub '{hub.HubName}' occupied!");
            ExitDefensePhase(true);
        }

        private void OnOccupationFailed(HubNode hub)
        {
            hub.OccupationSucceeded -= OnOccupationSucceeded;
            hub.OccupationFailed    -= OnOccupationFailed;
            Debug.Log($"[TurnManager] Occupation of '{hub.HubName}' failed.");
            ExitDefensePhase(false);
        }

        // ── 침략 일정 로직 ────────────────────────────────────────────────────
        private void RollForNewInvasion()
        {
            if (isGameOver) return;

            if (UnityEngine.Random.value > invasionProbability) return; // 확률 실패

            var invasionCandidates = FindInvasionCandidates();
            if (invasionCandidates.Count == 0) return;

            var target = invasionCandidates[UnityEngine.Random.Range(0, invasionCandidates.Count)];
            
            // 이미 스케줄에 있거나 활성 상태면 중복 추가 방지
            if (activeInvasions.Contains(target)) return;
            foreach (var inv in scheduledInvasions)
            {
                if (inv.TargetHub == target) return;
            }

            var delayTurns = UnityEngine.Random.Range(minInvasionDelayTurns, maxInvasionDelayTurns + 1);
            scheduledInvasions.Add(new ScheduledInvasion { TargetHub = target, RemainingTurns = delayTurns });
            Debug.Log($"[TurnManager] Invasion scheduled! Hub: {target.HubName} in {delayTurns} turns.");
            InvasionScheduled?.Invoke(target, delayTurns);
        }

        private void UpdateInvasionSchedules()
        {
            for (int i = scheduledInvasions.Count - 1; i >= 0; i--)
            {
                var schedule = scheduledInvasions[i];
                schedule.RemainingTurns--;

                if (schedule.RemainingTurns <= 0)
                {
                    scheduledInvasions.RemoveAt(i);
                    activeInvasions.Add(schedule.TargetHub);
                    Debug.Log($"[TurnManager] INVASION ACTIVE at {schedule.TargetHub.HubName}!");
                    InvasionActivated?.Invoke(schedule.TargetHub);
                }
            }
        }

        /// <summary>
        /// 침략 후보: 플레이어가 소유한 거점(Main 또는 Occupied) 중 빈 거점과 하나라도 연결된 곳
        /// </summary>
        private List<HubNode> FindInvasionCandidates()
        {
            var candidates = new List<HubNode>();
            foreach (var hub in allHubs)
            {
                if (hub != null && (hub.IsMain || hub.State == HubState.Occupied))
                {
                    bool hasEmptyNeighbor = false;
                    
                    // 쌍방향 연결성 검사: 내 Connected에 있거나 남의 Connected에 내가 있거나
                    foreach (var neighbor in allHubs)
                    {
                        if (neighbor != null && neighbor.IsEmpty)
                        {
                            if (hub.ConnectedHubs.Contains(neighbor) || neighbor.ConnectedHubs.Contains(hub))
                            {
                                hasEmptyNeighbor = true;
                                break;
                            }
                        }
                    }

                    if (hasEmptyNeighbor)
                    {
                        candidates.Add(hub);
                    }
                }
            }
            return candidates;
        }

        // ── 에디터 시각화 ─────────────────────────────────────────────────────
        private void OnValidate()
        {
            minInvasionDelayTurns = Mathf.Max(1, minInvasionDelayTurns);
            maxInvasionDelayTurns = Mathf.Max(minInvasionDelayTurns, maxInvasionDelayTurns);
        }

        private void OnDrawGizmos()
        {
            if (mainHub == null) return;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(mainHub.transform.position, 0.6f);
        }
    }
}
