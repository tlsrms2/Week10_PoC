using System;
using UnityEngine;
using UnityEngine.UI;

namespace Overlap.Combat.Enemy
{
    /// <summary>
    /// 적 한 마리의 런타임 컴포넌트입니다.
    /// - WaypointPath(MonoBehaviour)를 따라 이동
    /// - IHittable로 데미지를 수신
    /// - 코어 도달 또는 사망 시 이벤트 발행
    /// 
    /// 코어 도달 감지: CoreZone(BoxCollider2D Trigger)이 OnTriggerEnter2D에서
    ///   NotifyArrivedAtCore()를 호출합니다. EnemyUnit은 경로 끝에서 그냥 정지합니다.
    /// 신호 흐름: CoreZone → EnemyUnit.NotifyArrivedAtCore() → ReachedCore 이벤트 → WaveSpawner
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public sealed class EnemyUnit : MonoBehaviour, IHittable
    {
        // ── 이벤트 ──────────────────────────────────────────────────────────
        /// <summary>사망 시 발행. 정리 작업(풀 반환, 킬 카운트 등)에 사용합니다.</summary>
        public event Action<EnemyUnit> Died;

        /// <summary>마지막 웨이포인트 통과(코어 도달) 시 발행. 코어 HP 감소에 사용합니다.</summary>
        public event Action<EnemyUnit> ReachedCore;

        [Header("Health Bar")]
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.35f, 0f);
        [SerializeField] private Vector2 healthBarSize = new Vector2(0.55f, 0.07f);
        [SerializeField] private Color healthBarFillColor = new Color(0.15f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);

        // ── 상태 ────────────────────────────────────────────────────────────
        private EnemyDefinition definition;
        private WaypointPath    path;

        private float currentHp;
        private float maxHp;
        private float moveSpeed;
        private float speedMultiplier = 1f;
        private float slowEndTime;
        private int   waypointIndex;
        private bool  isDead;
        private bool  hasReachedCore;
        private Slider healthSlider;
        private GameObject healthBarRoot;

        // ── IHittable ────────────────────────────────────────────────────────
        public bool IsDead => isDead;
        public float CoreDamage => definition != null ? definition.CoreDamage : 1f;
        public float CurrentHealth => currentHp;
        public float MaxHealth => maxHp;

        public void TakeHit(float amount)
        {
            if (isDead || hasReachedCore) return;

            currentHp -= amount;
            UpdateHealthBar();
            if (currentHp <= 0f) Die();
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (isDead || hasReachedCore || duration <= 0f)
            {
                return;
            }

            speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
        }

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        // ── 초기화 (풀링을 고려해 Awake 대신 명시적 초기화 메서드 사용) ──────
        /// <summary>스폰 직후 WaveSpawner가 호출합니다.</summary>
        public void Initialize(EnemyDefinition def, WaypointPath waypointPath)
        {
            Initialize(def, waypointPath, 1);
        }

        public void Initialize(EnemyDefinition def, WaypointPath waypointPath, int turnNumber)
        {
            definition      = def ?? throw new ArgumentNullException(nameof(def));
            path            = waypointPath ?? throw new ArgumentNullException(nameof(waypointPath));
            maxHp           = def.GetMaxHealthForTurn(turnNumber);
            currentHp       = maxHp;
            moveSpeed       = def.GetMoveSpeedForTurn(turnNumber);
            speedMultiplier = 1f;
            slowEndTime     = 0f;
            waypointIndex   = 0;
            isDead          = false;
            hasReachedCore  = false;
            EnsureHealthBar();
            UpdateHealthBar();

            if (path.Count > 0)
            {
                transform.position = path.GetPoint(0);
                waypointIndex      = 1; // 첫 웨이포인트는 스폰 위치
            }
        }

        // ── 이동 ────────────────────────────────────────────────────────────
        private void Update()
        {
            if (isDead || hasReachedCore || path == null) return;
            MoveAlongPath();
        }

        private void MoveAlongPath()
        {
            // 경로 끝: 그냥 정지. 코어 도달 판정은 CoreZone(Trigger)이 담당합니다.
            if (waypointIndex >= path.Count) return;

            var target    = path.GetPoint(waypointIndex);
            var direction = (target - transform.position);
            var distance  = direction.magnitude;
            var step      = moveSpeed * CurrentSpeedMultiplier() * Time.deltaTime;

            if (step >= distance)
            {
                transform.position = target;
                waypointIndex++;
                if (waypointIndex >= path.Count)
                {
                    NotifyArrivedAtCore();
                }
            }
            else
            {
                transform.position += direction.normalized * step;
            }
        }

        private float CurrentSpeedMultiplier()
        {
            if (slowEndTime <= Time.time)
            {
                speedMultiplier = 1f;
            }

            return speedMultiplier;
        }

        // ── 코어 도달 ─────────────────────────────────────────────────────────
        /// <summary>
        /// 경로의 마지막 웨이포인트에 도달했을 때 내부적으로 호출됩니다.
        /// </summary>
        private void NotifyArrivedAtCore()
        {
            if (hasReachedCore || isDead) return;
            hasReachedCore = true;
            ReachedCore?.Invoke(this);
            ReturnToPool();
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(false);
            }
            Died?.Invoke(this);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            // 현재는 단순 Destroy. 추후 ObjectPool로 교체 가능
            gameObject.SetActive(false);
            Destroy(gameObject, 0f);
        }

        // ── 에디터 시각화 ─────────────────────────────────────────────────
        private void EnsureHealthBar()
        {
            if (!showHealthBar)
            {
                if (healthBarRoot != null)
                {
                    healthBarRoot.SetActive(false);
                }

                return;
            }

            if (healthBarRoot != null)
            {
                healthBarRoot.SetActive(true);
                return;
            }

            healthBarRoot = new GameObject("HealthBar");
            healthBarRoot.transform.SetParent(transform, false);
            healthBarRoot.transform.localPosition = healthBarOffset;

            var canvas = healthBarRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            var canvasRect = healthBarRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(100f, 12f);
            canvasRect.localScale = new Vector3(healthBarSize.x / 100f, healthBarSize.y / 12f, 1f);

            healthSlider = healthBarRoot.AddComponent<Slider>();
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.interactable = false;
            healthSlider.transition = Selectable.Transition.None;

            var background = CreateHealthBarImage("Background", healthBarRoot.transform, healthBarBackgroundColor);
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(healthBarRoot.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fill = CreateHealthBarImage("Fill", fillArea.transform, healthBarFillColor);

            healthSlider.targetGraphic = background;
            healthSlider.fillRect = fill.rectTransform;
        }

        private Image CreateHealthBarImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return image;
        }

        private void UpdateHealthBar()
        {
            if (healthSlider == null)
            {
                return;
            }

            healthSlider.value = maxHp <= 0f ? 0f : Mathf.Clamp01(currentHp / maxHp);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}
