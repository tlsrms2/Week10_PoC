using Overlap.Core;
using UnityEngine;

namespace Overlap.Combat.Projectiles
{
    [CreateAssetMenu(menuName = "Overlap/Combat/Projectile Definition", fileName = "ProjectileDefinition")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Projectile";
        [SerializeField] private TowerElement element = TowerElement.None;

        [Header("Tower Stats")]
        [SerializeField, Min(0.1f)] private float damage = 5f;
        [SerializeField, Min(0.1f)] private float range = 3f;
        [SerializeField, Min(0.1f)] private float fireRate = 1f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;

        [Header("Grade Scaling")]
        [SerializeField, Min(0f)] private float damageScalePerGrade = 0.5f;
        [SerializeField, Min(0f)] private float rangeScalePerGrade = 0.2f;

        [Header("Effect")]
        [SerializeField] private ProjectileEffectType effectType = ProjectileEffectType.None;
        [SerializeField, Min(0f)] private float areaRadius = 1f;
        [SerializeField, Range(0.05f, 1f)] private float slowMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float slowDuration = 2f;

        public string DisplayName => displayName;
        public TowerElement Element => element;
        public float ProjectileSpeed => projectileSpeed;
        public ProjectileEffectType EffectType => effectType;
        public float AreaRadius => areaRadius;
        public float SlowMultiplier => slowMultiplier;
        public float SlowDuration => slowDuration;

        public float GetDamage(int grade)
        {
            return damage * (1f + (Mathf.Max(1, grade) - 1) * damageScalePerGrade);
        }

        public float GetRange(int grade)
        {
            return range * (1f + (Mathf.Max(1, grade) - 1) * rangeScalePerGrade);
        }

        public float GetFireInterval()
        {
            return 1f / Mathf.Max(0.1f, fireRate);
        }
    }
}
