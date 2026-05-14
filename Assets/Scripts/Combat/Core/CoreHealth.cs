using System;
using UnityEngine;

namespace Overlap.Combat.Core
{
    /// <summary>
    /// 거점 코어의 내구도를 관리합니다.
    /// IHittable을 구현하여 EnemyUnit의 코어 도달 이벤트에서 직접 호출됩니다.
    /// </summary>
    public sealed class CoreHealth : MonoBehaviour, IHittable
    {
        [SerializeField, Min(1f)] private float maxHealth = 20f;

        // ── 이벤트 ───────────────────────────────────────────────────────────
        /// <summary>HP가 변할 때마다 발행. UI 갱신에 사용합니다. (current, max)</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>코어가 파괴되었을 때 발행. 게임 오버 시퀀스를 트리거합니다.</summary>
        public event Action Destroyed;

        // ── 상태 ─────────────────────────────────────────────────────────────
        private float currentHealth;

        public bool  IsDead          => currentHealth <= 0f;
        public float CurrentHealth   => currentHealth;
        public float MaxHealth       => maxHealth;
        public float HealthRatio     => currentHealth / maxHealth;

        // ── 수명주기 ──────────────────────────────────────────────────────────
        private void Awake()
        {
            currentHealth = maxHealth;
        }

        // ── IHittable ────────────────────────────────────────────────────────
        public void TakeHit(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(currentHealth, maxHealth);

            if (IsDead)
            {
                Debug.LogWarning($"[CoreHealth] Core '{name}' destroyed!");
                Destroyed?.Invoke();
            }
        }

        /// <summary>거점 탈환/초기화 시 HP 복구에 사용합니다.</summary>
        public void Restore()
        {
            currentHealth = maxHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            currentHealth = maxHealth; // 에디터에서 maxHealth 변경 시 동기화
        }
#endif
    }
}
