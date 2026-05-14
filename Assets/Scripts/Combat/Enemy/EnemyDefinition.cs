using UnityEngine;
using UnityEngine.Serialization;

namespace Overlap.Combat.Enemy
{
    /// <summary>
    /// 적 유닛의 정적 스펙을 정의하는 ScriptableObject입니다.
    /// 런타임 상태(현재 HP, 위치 등)는 EnemyHealth / EnemyMover에서 관리합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Overlap/Combat/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Enemy";

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 10f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float coreDamage = 1f;

        [Header("Wave Settings")]
        [SerializeField, Min(1)] private int enemyCount = 5;
        [SerializeField, Min(0f)] private float spawnInterval = 1f;

        [Header("Turn Scaling")]
        [Tooltip("Scaling applies once per this many turns. Turn 1 is always the base step.")]
        [SerializeField, Min(1)] private int scalingStepTurns = 3;
        [Tooltip("Flat HP added per scaling step.")]
        [SerializeField, Min(0f), FormerlySerializedAs("healthGrowthPerTurn")] private float healthGrowthPerStep = 0f;
        [Tooltip("Multiplicative HP growth per scaling step. 0.1 means +10% per step.")]
        [SerializeField, Min(0f), FormerlySerializedAs("healthGrowthRatePerTurn")] private float healthGrowthRatePerStep = 0f;
        [SerializeField, Min(0)] private int enemyCountIncreasePerStep = 1;
        [SerializeField, Min(0f)] private float spawnIntervalDecreasePerStep = 0.1f;
        [SerializeField] private float minSpawnInterval = 0.2f;
        [SerializeField, Min(0f)] private float moveSpeedIncreasePerStep = 0.1f;
        [SerializeField, Min(0.1f)] private float maxMoveSpeed = 4f;

        public string DisplayName => displayName;
        public float MaxHealth    => maxHealth;
        public float MoveSpeed    => moveSpeed;
        public float CoreDamage   => coreDamage;

        public float GetMaxHealthForTurn(int turnNumber)
        {
            var scalingStep = GetScalingStep(turnNumber);
            var flatScaled = maxHealth + healthGrowthPerStep * scalingStep;
            return flatScaled * Mathf.Pow(1f + healthGrowthRatePerStep, scalingStep);
        }

        public int GetEnemyCountForTurn(int turnNumber)
        {
            return Mathf.Max(1, enemyCount + enemyCountIncreasePerStep * GetScalingStep(turnNumber));
        }

        public float GetSpawnIntervalForTurn(int turnNumber)
        {
            var scaledInterval = spawnInterval - spawnIntervalDecreasePerStep * GetScalingStep(turnNumber);
            return Mathf.Max(minSpawnInterval, scaledInterval);
        }

        public float GetMoveSpeedForTurn(int turnNumber)
        {
            var scaledSpeed = moveSpeed + moveSpeedIncreasePerStep * GetScalingStep(turnNumber);
            return Mathf.Min(maxMoveSpeed, scaledSpeed);
        }

        private int GetScalingStep(int turnNumber)
        {
            return Mathf.Max(0, (turnNumber - 1) / Mathf.Max(1, scalingStepTurns));
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            coreDamage = Mathf.Max(0f, coreDamage);
            enemyCount = Mathf.Max(1, enemyCount);
            spawnInterval = Mathf.Max(0f, spawnInterval);
            scalingStepTurns = Mathf.Max(1, scalingStepTurns);
            enemyCountIncreasePerStep = Mathf.Max(0, enemyCountIncreasePerStep);
            spawnIntervalDecreasePerStep = Mathf.Max(0f, spawnIntervalDecreasePerStep);
            minSpawnInterval = Mathf.Clamp(minSpawnInterval, 0f, spawnInterval);
            moveSpeedIncreasePerStep = Mathf.Max(0f, moveSpeedIncreasePerStep);
            maxMoveSpeed = Mathf.Max(moveSpeed, maxMoveSpeed);
        }
    }
}
