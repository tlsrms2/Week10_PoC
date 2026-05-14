using System.Collections.Generic;
using Overlap.Combat.Projectiles;
using Overlap.Core;
using Overlap.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Overlap.Combat.Enemy;

namespace Overlap.Combat.Tower
{
    /// <summary>
    /// TowerBoard(타일맵 데이터)와 Combat 레이어를 연결하는 브릿지입니다.
    /// 
    /// 역할:
    ///   - TowerBoard에서 배치된 타워(GridPoint, TowerCell) 정보를 읽어옵니다.
    ///   - 각 타워의 사거리·쿨다운·데미지를 Grade 기반으로 계산합니다.
    ///   - 사거리 안에 있는 적을 감지하고 Projectile을 발사합니다.
    /// 
    /// 이 클래스는 Tilemap을 직접 건드리지 않습니다.
    /// TowerBoard를 통해 순수 데이터만 읽고, 시각적 처리는 Tilemaps 레이어에 위임합니다.
    /// </summary>
    public sealed class TowerAttackSystem : MonoBehaviour
    {
        [Header("Tilemap Bridge")]
        [SerializeField] private TilemapPlacementController placementController;
        [SerializeField] private Tilemap targetTilemap; // 셀 → 월드 좌표 변환에만 사용

        [Header("Tower Base Stats (Grade 1)")]
        [SerializeField, Min(0.1f)] private float baseDamage    = 5f;
        [SerializeField, Min(0.5f)] private float baseRange     = 3f;
        [SerializeField, Min(0.1f)] private float baseFireRate  = 1f; // shots/second
        [SerializeField, Min(1f)]   private float projectileSpeed = 8f;

        [Header("Grade Scaling")]
        [Tooltip("Grade당 데미지 배율 증가량. Grade 2 = baseDamage * (1 + 1*scale)")]
        [SerializeField, Min(0f)] private float damageScalePerGrade = 0.5f;
        [Tooltip("Grade당 사거리 배율 증가량")]
        [SerializeField, Min(0f)] private float rangeScalePerGrade  = 0.2f;

        [Header("Projectile")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private ProjectileDefinition[] projectileDefinitions = System.Array.Empty<ProjectileDefinition>();

        private readonly Dictionary<TowerElement, ProjectileDefinition> projectileDefinitionByElement =
            new Dictionary<TowerElement, ProjectileDefinition>();

        // ── 내부 상태 ─────────────────────────────────────────────────────
        // 각 타워 셀의 다음 발사 가능 시각을 추적 (GridPoint → next fire time)
        private readonly Dictionary<GridPoint, float> fireCooldowns = new Dictionary<GridPoint, float>();

        // 살아있는 적 목록 (WaveSpawner가 등록/해제)
        private readonly List<EnemyUnit> activeEnemies = new List<EnemyUnit>();

        // ── 적 등록 API ───────────────────────────────────────────────────
        public void RegisterEnemy(EnemyUnit enemy)
        {
            if (enemy == null) return;
            activeEnemies.Add(enemy);
            enemy.Died       += OnEnemyRemoved;
            enemy.ReachedCore += OnEnemyRemoved;
        }

        private void OnEnemyRemoved(EnemyUnit enemy)
        {
            activeEnemies.Remove(enemy);
            enemy.Died        -= OnEnemyRemoved;
            enemy.ReachedCore -= OnEnemyRemoved;
        }

        // ── 메인 루프 ─────────────────────────────────────────────────────
        private void Update()
        {
            if (placementController == null || activeEnemies.Count == 0) return;
            var board = placementController.Board;
            if (board == null) return;

            foreach (var pair in board.CreatePlacedCellSnapshot())
            {
                var gridPoint = pair.Key;
                var cell      = pair.Value;
                if (cell.IsEmpty) continue;

                ProcessTowerCell(gridPoint, cell);
            }
        }

        private void ProcessTowerCell(GridPoint gridPoint, TowerCell cell)
        {
            // 쿨다운 확인
            if (fireCooldowns.TryGetValue(gridPoint, out var nextFireTime))
            {
                if (Time.time < nextFireTime) return;
            }

            var worldPos = GridToWorld(gridPoint);
            var definition = ResolveProjectileDefinition(cell.Element);
            var range    = EffectiveRange(cell, definition);
            var target   = FindClosestEnemyInRange(worldPos, range);

            if (target == null) return;

            FireProjectile(worldPos, target, cell);

            // 다음 발사 가능 시각 설정 (FireRate는 Grade에 독립적으로 고정, 확장 가능)
            fireCooldowns[gridPoint] = Time.time + EffectiveFireInterval(definition);
        }

        private void FireProjectile(Vector3 origin, EnemyUnit target, TowerCell cell)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("[TowerAttackSystem] Projectile prefab is not assigned.");
                return;
            }

            var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
            var definition = ResolveProjectileDefinition(cell.Element);
            if (definition != null)
            {
                proj.Launch(target, definition, cell.Grade);
            }
            else
            {
                proj.Launch(target, EffectiveDamage(cell.Grade), projectileSpeed);
            }
        }

        // ── 유틸리티 ─────────────────────────────────────────────────────
        private EnemyUnit FindClosestEnemyInRange(Vector3 towerPos, float range)
        {
            EnemyUnit closest  = null;
            var        closestDistSq = range * range;

            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                var distSq = (enemy.transform.position - towerPos).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest       = enemy;
                }
            }
            return closest;
        }

        /// <summary>GridPoint → 월드 좌표 변환. Tilemap의 CellToWorld를 위임합니다.</summary>
        private Vector3 GridToWorld(GridPoint gridPoint)
        {
            if (targetTilemap == null)
            {
                // fallback: 셀 좌표를 직접 사용 (타일 크기 1x1 가정)
                return new Vector3(gridPoint.X + 0.5f, gridPoint.Y + 0.5f, 0f);
            }
            var cellPos = TilemapGridPointMapper.ToCellPosition(gridPoint);
            return targetTilemap.GetCellCenterWorld(cellPos);
        }

        private float EffectiveDamage(int grade) =>
            baseDamage * (1f + (grade - 1) * damageScalePerGrade);

        private float EffectiveRange(TowerCell cell, ProjectileDefinition definition) =>
            definition != null
                ? definition.GetRange(cell.Grade)
                : baseRange * (1f + (cell.Grade - 1) * rangeScalePerGrade);

        private float EffectiveFireInterval(ProjectileDefinition definition) =>
            definition != null ? definition.GetFireInterval() : 1f / baseFireRate;

        private ProjectileDefinition ResolveProjectileDefinition(TowerElement element)
        {
            if (projectileDefinitionByElement.Count == 0)
            {
                RebuildProjectileDefinitionLookup();
            }

            return projectileDefinitionByElement.TryGetValue(element, out var definition)
                ? definition
                : null;
        }

        private void RebuildProjectileDefinitionLookup()
        {
            projectileDefinitionByElement.Clear();
            foreach (var definition in projectileDefinitions)
            {
                if (definition == null || definition.Element == TowerElement.None)
                {
                    continue;
                }

                projectileDefinitionByElement[definition.Element] = definition;
            }
        }

        // ── 에디터 시각화 ─────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (placementController?.Board == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            foreach (var pair in placementController.Board.CreatePlacedCellSnapshot())
            {
                if (pair.Value.IsEmpty) continue;
                var worldPos = GridToWorld(pair.Key);
                var range    = EffectiveRange(pair.Value, ResolveProjectileDefinition(pair.Value.Element));
                Gizmos.DrawWireSphere(worldPos, range);
            }
        }
    }
}
