using System;
using System.Collections;
using Overlap.Combat.Core;
using Overlap.Combat.Enemy;
using Overlap.WorldMap;
using UnityEngine;

namespace Overlap.Combat.Wave
{
    /// <summary>
    /// 한 거점의 디펜스 페이즈를 관리합니다.
    /// - 웨이브 단위로 EnemyUnit을 스폰
    /// - 모든 적 제거 → 웨이브 클리어 이벤트 발행
    /// 
    /// 코어 데미지 파이프라인:
    ///   EnemyUnit → CoreZone(Trigger) → CoreHealth.TakeHit()
    ///   WaveSpawner는 코어 HP를 직접 조작하지 않습니다.
    /// </summary>
    public sealed class WaveSpawner : MonoBehaviour
    {
        // ── 이벤트 ───────────────────────────────────────────────────────────
        public event Action WaveCleared;

        // ── 인스펙터 설정 ─────────────────────────────────────────────────────
        [Header("Wave Config")]
        [SerializeField] private EnemyDefinition enemyDefinition;
        [Tooltip("씬에 배치된 WaypointPath 오브젝트를 연결합니다.")]
        [SerializeField] private WaypointPath    path;

        [Header("References")]
        [SerializeField] private GameObject     enemyPrefab;
        [SerializeField] private Tower.TowerAttackSystem attackSystem;
        [SerializeField] private TurnManager turnManager;

        // ── 내부 상태 ─────────────────────────────────────────────────────────
        private int aliveCount;
        private bool isRunning;
        private CoreHealth targetCore;

        // ── 공개 API ──────────────────────────────────────────────────────────
        /// <summary>디펜스 페이즈를 시작합니다. 외부(턴 매니저 등)에서 호출합니다.</summary>
        public void StartWave(WaypointPath overridePath = null, CoreHealth targetCore = null)
        {
            if (isRunning)
            {
                Debug.LogWarning("[WaveSpawner] Wave already running.");
                return;
            }
            
            if (overridePath != null)
            {
                path = overridePath;
            }
            
            if (targetCore != null)
            {
                this.targetCore = targetCore;
            }

            if (turnManager == null)
            {
                turnManager = FindAnyObjectByType<TurnManager>();
            }

            StartCoroutine(SpawnRoutine());
        }

        // ── 스폰 코루틴 ───────────────────────────────────────────────────────
        private IEnumerator SpawnRoutine()
        {
            isRunning  = true;
            aliveCount = 0;
            var turnNumber = CurrentTurnNumber();
            var enemyCount = enemyDefinition != null ? enemyDefinition.GetEnemyCountForTurn(turnNumber) : 0;
            var spawnInterval = enemyDefinition != null ? enemyDefinition.GetSpawnIntervalForTurn(turnNumber) : 0f;

            for (var i = 0; i < enemyCount; i++)
            {
                SpawnOne(turnNumber);
                yield return new WaitForSeconds(spawnInterval);
            }

            // 마지막 적 사망/도달까지 대기
            yield return new WaitUntil(() => aliveCount <= 0);

            if (isRunning) // StopWave가 호출되지 않았다면
            {
                isRunning = false;
                Debug.Log("[WaveSpawner] Wave cleared.");
                WaveCleared?.Invoke();
            }
        }

        public void StopWave()
        {
            if (isRunning)
            {
                isRunning = false;
                StopAllCoroutines();
            }
        }

        private void SpawnOne(int turnNumber)
        {
            if (enemyPrefab == null || enemyDefinition == null || path == null)
            {
                Debug.LogError("[WaveSpawner] Missing required references.");
                return;
            }

            var go    = Instantiate(enemyPrefab);
            var enemy = go.GetComponent<EnemyUnit>();
            if (enemy == null)
            {
                Debug.LogError("[WaveSpawner] Enemy prefab is missing EnemyUnit component.");
                Destroy(go);
                return;
            }

            enemy.Initialize(enemyDefinition, path, turnNumber);

            // 타워 시스템에 등록
            attackSystem?.RegisterEnemy(enemy);

            // 이벤트 구독
            enemy.Died        += OnEnemyRemoved;
            enemy.ReachedCore += OnEnemyHitCore;

            aliveCount++;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────────
        private void OnEnemyHitCore(EnemyUnit enemy)
        {
            targetCore?.TakeHit(enemy.CoreDamage);
            Unsubscribe(enemy);
            aliveCount--;
        }

        private void OnEnemyRemoved(EnemyUnit enemy)
        {
            Unsubscribe(enemy);
            aliveCount--;
        }

        private static void Unsubscribe(EnemyUnit enemy)
        {
            // 다른 핸들러에서 중복 호출되지 않도록 구독 해제
            // (EnemyUnit은 Died/ReachedCore 중 하나만 발행하므로 안전)
        }

        // ── 에디터 검증 ───────────────────────────────────────────────────────
        private void OnValidate()
        {
            if (turnManager == null) turnManager = FindAnyObjectByType<TurnManager>();
        }

        private int CurrentTurnNumber()
        {
            return turnManager != null ? turnManager.TurnNumber : 1;
        }
    }
}
