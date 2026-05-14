using System;
using Overlap.Combat.Enemy;
using UnityEngine;

namespace Overlap.Combat.Projectiles
{
    /// <summary>
    /// 타워가 발사하는 투사체입니다.
    /// - 목표(IHittable)를 추적하여 이동
    /// - 명중 또는 목표 소멸 시 자신을 제거
    /// 
    /// 투사체 스펙(속도, 데미지)은 Launch() 호출 시 주입받아 SO 없이도 유연하게 구성됩니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float hitRadius = 0.15f;

        // ── 런타임 주입 데이터 ───────────────────────────────────────────────
        private IHittable target;
        private MonoBehaviour targetBehaviour;
        private ProjectileDefinition definition;
        private float damage;
        private float speed;

        /// <summary>투사체 생성 직후 TowerAttackSystem에서 호출합니다.</summary>
        public void Launch(IHittable target, float damage, float speed)
        {
            SetTarget(target);
            this.definition = null;
            this.damage = damage;
            this.speed  = speed;
        }

        public void Launch(IHittable target, ProjectileDefinition projectileDefinition, int grade)
        {
            SetTarget(target);
            definition = projectileDefinition ?? throw new ArgumentNullException(nameof(projectileDefinition));
            damage = definition.GetDamage(grade);
            speed = definition.ProjectileSpeed;
        }

        // ── 이동 및 명중 판정 ────────────────────────────────────────────────
        private void Update()
        {
            // 목표가 이미 소멸했으면 투사체도 제거
            if (!HasValidTarget())
            {
                Destroy(gameObject);
                return;
            }

            var targetPos  = targetBehaviour.transform.position;
            var direction  = (targetPos - transform.position);
            var distanceSq = direction.sqrMagnitude;
            var step       = speed * Time.deltaTime;

            // 명중 판정: 이번 프레임의 이동거리가 남은 거리보다 크면 명중
            if (step * step >= distanceSq)
            {
                Hit();
                return;
            }

            transform.position += direction.normalized * step;
            // 진행 방향으로 스프라이트 회전 (선택적 시각 처리)
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Hit()
        {
            if (HasValidTarget())
            {
                target.TakeHit(damage);
                ApplyEffect();
            }
            Destroy(gameObject);
        }

        private void SetTarget(IHittable newTarget)
        {
            target = newTarget ?? throw new ArgumentNullException(nameof(newTarget));
            targetBehaviour = newTarget as MonoBehaviour;

            if (targetBehaviour == null)
            {
                throw new ArgumentException("Projectile target must be a MonoBehaviour.", nameof(newTarget));
            }
        }

        private bool HasValidTarget()
        {
            return targetBehaviour != null && target != null && !target.IsDead;
        }

        private void ApplyEffect()
        {
            if (definition == null)
            {
                return;
            }

            switch (definition.EffectType)
            {
                case ProjectileEffectType.AreaDamage:
                    ApplyAreaDamage();
                    break;
                case ProjectileEffectType.Slow:
                    ApplySlow();
                    break;
            }
        }

        private void ApplyAreaDamage()
        {
            if (definition.AreaRadius <= 0f)
            {
                return;
            }

            var enemies = FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy == targetBehaviour)
                {
                    continue;
                }

                var distance = Vector2.Distance(enemy.transform.position, transform.position);
                if (distance <= definition.AreaRadius)
                {
                    enemy.TakeHit(damage);
                }
            }
        }

        private void ApplySlow()
        {
            if (targetBehaviour is EnemyUnit enemy)
            {
                enemy.ApplySlow(definition.SlowMultiplier, definition.SlowDuration);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
