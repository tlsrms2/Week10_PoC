using System;
using System.Collections.Generic;
using System.Linq;
using Overlap.Combat.Core;
using Overlap.Combat.Enemy;
using Overlap.Combat.Wave;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Overlap.WorldMap
{
    /// <summary>
    /// 월드맵의 거점 하나를 나타내는 씬 컴포넌트입니다.
    ///
    /// 구조 규칙:
    ///   - 모든 거점은 연결된 이웃 거점(connectedHubs) 목록을 가집니다.
    ///   - 메인 거점은 HubState.Main, 플레이어 점령은 Occupied, 나머지는 Empty.
    ///   - 빈 거점(Empty)만 적 스폰 포인트가 됩니다.
    ///   - 각 빈 거점에는 메인 거점까지의 WaypointPath와 WaveSpawner가 연결됩니다.
    ///
    /// 이벤트 흐름:
    ///   TurnManager → HubNode.BeginOccupation() → WaveSpawner.StartWave()
    ///   WaveSpawner.WaveCleared → HubNode → TurnManager (점령 성공 통보)
    /// </summary>
    public sealed class HubNode : MonoBehaviour
    {
        // ── 이벤트 ───────────────────────────────────────────────────────────
        /// <summary>디펜스 성공 → 점령 완료 시 발행</summary>
        public event Action<HubNode> OccupationSucceeded;

        /// <summary>디펜스 실패 시 발행 (코어 파괴)</summary>
        public event Action<HubNode> OccupationFailed;

        /// <summary>침략 방어 성공 시 발행</summary>
        public event Action<HubNode> InvasionRepelledEvent;

        // ── Inspector 설정 ────────────────────────────────────────────────────
        private const float MaxAutoPathEndpointScore = 8f;

        [Header("Identity")]
        [SerializeField] private string hubName = "Hub";
        [SerializeField] private HubState initialState = HubState.Empty;

        [Header("World Map Connections")]
        [Tooltip("이 거점과 직접 연결된 이웃 거점들입니다.")]
        [SerializeField] private HubNode[] connectedHubs = Array.Empty<HubNode>();

        [Serializable]
        public struct PathMapping
        {
            public HubNode TargetHub;
            public WaypointPath Path;
        }

        [Header("Defense Setup (Empty Hub Only)")]
        [Tooltip("적이 타 거점으로 이동하는 기본 경로 (단일 경로일 때 사용)")]
        [SerializeField] private WaypointPath enemyPath;
        [Tooltip("이 거점에서 적을 스폰하는 WaveSpawner")]
        [SerializeField] private WaveSpawner waveSpawner;
        [Tooltip("타겟 거점별 지정 경로 (다중 경로일 때 우선 적용)")]
        [SerializeField] private PathMapping[] pathMappings;

        [Header("Defense Core")]
        [Tooltip("모든 거점에 필수: 디펜스 실패 여부를 감지할 CoreHealth (빈 거점은 체력 1로 세팅)")]
        [SerializeField] private CoreHealth coreHealth;

        [Header("Tilemap Visuals")]
        [Tooltip("비워두면 씬의 Tilemap들을 자동 검색합니다.")]
        [SerializeField] private Tilemap hubTilemap;
        [SerializeField] private Color emptyColor = Color.white;
        [SerializeField] private Color occupiedColor = new Color(0.4f, 1f, 0.4f, 1f);
        [SerializeField] private Color contestedColor = new Color(1f, 0.5f, 0f, 1f);

        // ── 런타임 상태 ───────────────────────────────────────────────────────
        private HubState currentState;

        public string   HubName       => hubName;
        public HubState State         => currentState;
        public bool     IsEmpty       => currentState == HubState.Empty;
        public bool     IsMain        => currentState == HubState.Main;
        public bool     IsContested   => currentState == HubState.Contested;
        public IReadOnlyList<HubNode> ConnectedHubs => connectedHubs;
        public CoreHealth CoreHealth  => coreHealth;

        // ── 수명주기 ──────────────────────────────────────────────────────────
        private void Awake()
        {
            currentState = initialState;
        }

        private void Start()
        {
            UpdateTileColor();
        }

        // ── 점령 시도 API (TurnManager가 호출) ────────────────────────────────
        private int activeOccupationWaves = 0;
        private List<WaveSpawner> activeOccupationSpawners = new List<WaveSpawner>();

        /// <summary>
        /// 플레이어가 이 빈 거점을 점령 시도합니다.
        /// 연결된 빈 거점들에서 적이 스폰됩니다.
        /// </summary>
        public void BeginOccupation()
        {
            if (!IsEmpty)
            {
                Debug.LogWarning($"[HubNode] {hubName} is not empty — cannot begin occupation.");
                return;
            }

            activeOccupationWaves = 0;
            activeOccupationSpawners.Clear();

            var neighbors = GetAllConnectedHubs();
            foreach (var neighbor in neighbors)
            {
                if (neighbor != null && neighbor.IsEmpty && neighbor.waveSpawner != null)
                {
                    WaypointPath path = neighbor.GetPathTo(this);
                    if (path == null)
                    {
                        Debug.LogWarning($"[HubNode] No valid path from {neighbor.HubName} to {HubName}. Skipping occupation wave.");
                        continue;
                    }

                    activeOccupationSpawners.Add(neighbor.waveSpawner);
                    neighbor.waveSpawner.WaveCleared += OnOccupationNeighborWaveCleared;
                    
                    neighbor.waveSpawner.StartWave(path, this.coreHealth);
                    activeOccupationWaves++;
                }
            }

            currentState = HubState.Contested;
            UpdateTileColor();
            
            if (coreHealth != null)
                coreHealth.Destroyed += OnDefenseFailed;
            else
                Debug.LogWarning($"[HubNode] {hubName} has no CoreHealth assigned. Occupation cannot fail by HP loss.");

            if (activeOccupationWaves == 0)
            {
                Debug.LogWarning($"[HubNode] {hubName} has no empty neighbors to spawn enemies from. Auto-occupied!");
                OnDefenseCleared();
            }
            else
            {
                Debug.Log($"[HubNode] Occupation started at {hubName}. {activeOccupationWaves} waves incoming.");
            }
        }

        private void OnOccupationNeighborWaveCleared()
        {
            activeOccupationWaves--;
            if (activeOccupationWaves <= 0)
            {
                OnDefenseCleared();
            }
        }

        private int activeInvasionWaves = 0;
        private List<WaveSpawner> activeSpawners = new List<WaveSpawner>();

        /// <summary>
        /// 침략 이벤트: 이 거점(점령됨/메인)이 공격받습니다. 연결된 빈 거점들에서 적이 스폰됩니다.
        /// </summary>
        public void BeginInvasion()
        {
            activeInvasionWaves = 0;
            activeSpawners.Clear();

            // 이웃한 모든 "빈 거점"에서 스폰
            var neighbors = GetAllConnectedHubs();
            foreach (var neighbor in neighbors)
            {
                if (neighbor != null && neighbor.IsEmpty && neighbor.waveSpawner != null)
                {
                    WaypointPath path = neighbor.GetPathTo(this);
                    if (path == null)
                    {
                        Debug.LogWarning($"[HubNode] No valid path from {neighbor.HubName} to {HubName}. Skipping invasion wave.");
                        continue;
                    }

                    activeSpawners.Add(neighbor.waveSpawner);
                    neighbor.waveSpawner.WaveCleared += OnNeighborWaveCleared;
                    
                    neighbor.waveSpawner.StartWave(path, this.coreHealth);
                    activeInvasionWaves++;
                }
            }

            if (activeInvasionWaves == 0)
            {
                Debug.LogWarning($"[HubNode] {hubName} was invaded but has no empty neighbors to spawn from! Instantly repelled.");
                InvasionRepelledEvent?.Invoke(this);
            }
            else
            {
                Debug.Log($"[HubNode] Invasion at {hubName}! {activeInvasionWaves} waves incoming.");
            }
        }

        private void OnNeighborWaveCleared()
        {
            activeInvasionWaves--;
            if (activeInvasionWaves <= 0)
            {
                // 모든 웨이브 종료 -> 침략 방어 성공
                foreach (var spawner in activeSpawners)
                {
                    if (spawner != null) spawner.WaveCleared -= OnNeighborWaveCleared;
                }
                activeSpawners.Clear();
                
                Debug.Log($"[HubNode] Invasion at {hubName} successfully repelled!");
                InvasionRepelledEvent?.Invoke(this);
            }
        }

        // ── 내부 이벤트 처리 ──────────────────────────────────────────────────
        private void OnDefenseCleared()
        {
            foreach (var spawner in activeOccupationSpawners)
            {
                if (spawner != null) spawner.WaveCleared -= OnOccupationNeighborWaveCleared;
            }
            activeOccupationSpawners.Clear();

            if (coreHealth != null)
                coreHealth.Destroyed -= OnDefenseFailed;
                
            currentState = HubState.Occupied;
            UpdateTileColor();

            Debug.Log($"[HubNode] {hubName} occupied by player!");
            OccupationSucceeded?.Invoke(this);
        }

        private void OnDefenseFailed()
        {
            foreach (var spawner in activeOccupationSpawners)
            {
                if (spawner != null) spawner.WaveCleared -= OnOccupationNeighborWaveCleared;
            }
            activeOccupationSpawners.Clear();

            if (coreHealth != null)
                coreHealth.Destroyed -= OnDefenseFailed;

            currentState = HubState.Empty;
            UpdateTileColor();

            Debug.Log($"[HubNode] {hubName} occupation failed!");
            
            if (coreHealth != null)
                coreHealth.Restore(); // 다음 도전을 위해 체력 복구

            OccupationFailed?.Invoke(this);
        }

        public void ResetToEmpty()
        {
            if (IsMain) return; // 메인 거점은 빈 거점이 될 수 없음
            currentState = HubState.Empty;
            UpdateTileColor();
        }

        public void ForceSetMain()
        {
            currentState = HubState.Main;
            UpdateTileColor();
        }

        public List<HubNode> GetAllConnectedHubs()
        {
            var result = new HashSet<HubNode>();
            
            if (connectedHubs != null)
            {
                foreach (var neighbor in connectedHubs)
                {
                    if (neighbor != null) result.Add(neighbor);
                }
            }

            var allHubs = FindObjectsByType<HubNode>(FindObjectsSortMode.None);
            foreach (var hub in allHubs)
            {
                if (hub != null && hub.connectedHubs != null && hub.connectedHubs.Contains(this))
                {
                    result.Add(hub);
                }
            }

            return result.ToList();
        }

        public WaypointPath GetPathTo(HubNode target)
        {
            if (target == null)
            {
                return null;
            }

            // 1. 명시적 매핑 우선 확인
            if (pathMappings != null)
            {
                foreach (var mapping in pathMappings)
                {
                    if (mapping.TargetHub == target && mapping.Path != null)
                        return mapping.Path;
                }
            }

            var reverseMappedPath = target != null ? target.FindMappedPathTo(this) : null;
            if (reverseMappedPath != null)
            {
                Debug.Log($"[HubNode] Reversed mapped path from {target.HubName} to {hubName}.");
                return reverseMappedPath.CreateReversedPath();
            }

            if (target != null && target.TryOrientPath(target.enemyPath, this, out var targetEnemyPathToThis))
            {
                Debug.Log($"[HubNode] Reversed target enemy path from {target.HubName} to {hubName}.");
                return targetEnemyPathToThis.CreateReversedPath();
            }

            if (TryOrientPath(enemyPath, target, out var orientedEnemyPath))
            {
                return orientedEnemyPath;
            }

            // 2. 씬 내의 모든 WaypointPath를 뒤져서 양 끝점이 나와 타겟을 잇는 경로 자동 찾기
            WaypointPath bestPath = null;
            var bestScore = float.MaxValue;
            var shouldReverseBestPath = false;
            var allPaths = FindObjectsByType<WaypointPath>(FindObjectsSortMode.None);
            foreach (var p in allPaths)
            {
                if (p.Count < 2) continue;
                
                Vector2 start = p.GetPoint(0);
                Vector2 end = p.GetPoint(p.Count - 1);
                
                // 내 위치 -> 타겟 위치 (정방향)
                if (Vector2.Distance(start, transform.position) < 1f && 
                    Vector2.Distance(end, target.transform.position) < 1f)
                {
                    return p;
                }
                
                // 타겟 위치 -> 내 위치 (역방향)
                if (Vector2.Distance(start, target.transform.position) < 1f && 
                    Vector2.Distance(end, transform.position) < 1f)
                {
                    Debug.Log($"[HubNode] Auto-reversed path from {target.HubName} to {hubName}.");
                    return p.CreateReversedPath();
                }

                var directScore = EndpointScore(p, this, target);
                if (directScore < bestScore)
                {
                    bestPath = p;
                    bestScore = directScore;
                    shouldReverseBestPath = false;
                }

                var reverseScore = EndpointScore(p, target, this);
                if (reverseScore < bestScore)
                {
                    bestPath = p;
                    bestScore = reverseScore;
                    shouldReverseBestPath = true;
                }
            }

            if (bestPath != null && bestScore <= MaxAutoPathEndpointScore)
            {
                if (shouldReverseBestPath)
                {
                    Debug.Log($"[HubNode] Auto-selected reversed nearest path from {target.HubName} to {hubName}. Score: {bestScore:0.00}");
                    return bestPath.CreateReversedPath();
                }

                Debug.Log($"[HubNode] Auto-selected nearest path from {hubName} to {target.HubName}. Score: {bestScore:0.00}");
                return bestPath;
            }

            Debug.LogWarning($"[HubNode] Could not resolve path from {hubName} to {target?.HubName}. Add a Path Mapping on either hub.");
            return null;
        }

        private WaypointPath FindMappedPathTo(HubNode target)
        {
            if (pathMappings == null)
            {
                return null;
            }

            foreach (var mapping in pathMappings)
            {
                if (mapping.TargetHub == target && mapping.Path != null)
                {
                    return mapping.Path;
                }
            }

            return null;
        }

        private bool TryOrientPath(WaypointPath path, HubNode target, out WaypointPath orientedPath)
        {
            orientedPath = null;
            if (path == null || path.Count < 2 || target == null)
            {
                return false;
            }

            if (EndpointScore(path, this, target) <= MaxAutoPathEndpointScore)
            {
                orientedPath = path;
                return true;
            }

            if (EndpointScore(path, target, this) <= MaxAutoPathEndpointScore)
            {
                orientedPath = path.CreateReversedPath();
                return true;
            }

            return false;
        }

        private static float EndpointScore(WaypointPath path, HubNode startHub, HubNode endHub)
        {
            var startDistance = Vector2.Distance(path.GetPoint(0), startHub.transform.position);
            var endDistance = Vector2.Distance(path.GetPoint(path.Count - 1), endHub.transform.position);
            return startDistance + endDistance;
        }

        private void UpdateTileColor()
        {
            Color targetColor = currentState switch
            {
                HubState.Occupied => occupiedColor,
                HubState.Contested => contestedColor,
                _ => emptyColor
            };

            // 만약 메인 거점 전용 색상이 필요하다면 여기서 추가 처리 가능
            // if (IsMain) targetColor = mainColor;

            var tilemaps = hubTilemap != null 
                ? new[] { hubTilemap } 
                : FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

            foreach (var tm in tilemaps)
            {
                var centerCell = tm.WorldToCell(transform.position);
                if (tm.HasTile(centerCell))
                {
                    // 3x3 영역 색상 변경
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            var cell = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z);
                            if (tm.HasTile(cell))
                            {
                                tm.SetTileFlags(cell, TileFlags.None);
                                tm.SetColor(cell, targetColor);
                            }
                        }
                    }
                }
            }
        }

        // ── 에디터 시각화 ─────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            // 상태별 색상
            Gizmos.color = currentState switch
            {
                HubState.Main      => new Color(0.2f, 0.6f, 1f, 0.9f),   // 파랑
                HubState.Occupied  => new Color(0.2f, 1f, 0.4f, 0.9f),   // 초록
                HubState.Empty     => new Color(0.9f, 0.9f, 0.2f, 0.8f), // 노랑
                HubState.Contested => new Color(1f, 0.4f, 0.1f, 0.9f),   // 주황
                _                  => Color.white,
            };

            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // 연결선 그리기
            Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.4f);
            foreach (var neighbor in connectedHubs)
            {
                if (neighbor != null)
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 선택 시 연결 강조
            Gizmos.color = Color.white;
            foreach (var neighbor in connectedHubs)
            {
                if (neighbor != null)
                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }
    }
}
