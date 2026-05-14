using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Overlap.WorldMap.UI
{
    /// <summary>
    /// 게임의 현재 페이즈(Normal / Defense)를 전역 UI로 표시하는 매니저입니다.
    /// TurnManager의 이벤트에 반응하여 UI 텍스트와 패널을 전환합니다.
    /// </summary>
    public class PhaseUIManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("게임의 턴 매니저")]
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private Button endTurnButton;
        [SerializeField, Range(0f, 1f)] private float disabledEndTurnButtonAlpha = 0.35f;

        [Header("Normal Phase UI")]
        [Tooltip("일반 기획 페이즈 시 켜질 패널 오브젝트")]
        [SerializeField] private GameObject normalPhasePanel;
        [Tooltip("현재 턴 수를 보여줄 텍스트 (예: Normal Phase - Turn 1)")]
        [SerializeField] private TextMeshProUGUI normalPhaseText;

        [Header("Defense Phase UI")]
        [Tooltip("디펜스 페이즈 시 켜질 패널 오브젝트")]
        [SerializeField] private GameObject defensePhasePanel;
        [Tooltip("디펜스 상태를 보여줄 텍스트 (예: Defense Phase)")]
        [SerializeField] private TextMeshProUGUI defensePhaseText;

        private CanvasGroup endTurnButtonCanvasGroup;

        private void OnEnable()
        {
            if (turnManager == null)
                turnManager = FindAnyObjectByType<TurnManager>();

            if (turnManager != null)
            {
                turnManager.PlanningPhaseStarted += OnPlanningPhaseStarted;
                turnManager.DefensePhaseStarted += OnDefensePhaseStarted;
                turnManager.DefensePhaseEnded += OnDefensePhaseEnded;
                turnManager.GameOver += OnGameOver;
            }
        }

        private void OnDisable()
        {
            if (turnManager != null)
            {
                turnManager.PlanningPhaseStarted -= OnPlanningPhaseStarted;
                turnManager.DefensePhaseStarted -= OnDefensePhaseStarted;
                turnManager.DefensePhaseEnded -= OnDefensePhaseEnded;
                turnManager.GameOver -= OnGameOver;
            }
        }

        private void Start()
        {
            // 초기 셋업
            if (turnManager != null)
            {
                if (turnManager.Phase == TurnPhase.Planning)
                    OnPlanningPhaseStarted(turnManager.TurnNumber);
                else if (turnManager.Phase == TurnPhase.Defense)
                    OnDefensePhaseStarted(null); // 대상은 생략
                else if (turnManager.Phase == TurnPhase.GameOver)
                    OnGameOver();
            }
        }

        private void OnPlanningPhaseStarted(int turnNumber)
        {
            SetEndTurnButtonEnabled(true);

            if (normalPhasePanel != null) normalPhasePanel.SetActive(true);
            if (defensePhasePanel != null) defensePhasePanel.SetActive(false);

            if (normalPhaseText != null)
            {
                normalPhaseText.text = $"Normal Phase (Turn {turnNumber})";
            }
        }

        private void OnDefensePhaseStarted(HubNode targetHub)
        {
            SetEndTurnButtonEnabled(false);

            if (normalPhasePanel != null) normalPhasePanel.SetActive(false);
            if (defensePhasePanel != null) defensePhasePanel.SetActive(true);

            if (defensePhaseText != null)
            {
                if (targetHub != null)
                    defensePhaseText.text = $"Defense Phase\n<size=70%>Protect {targetHub.HubName}!</size>";
                else
                    defensePhaseText.text = "Defense Phase";
            }
        }

        private void OnDefensePhaseEnded(bool success)
        {
            if (defensePhaseText != null)
            {
                if (success)
                    defensePhaseText.text = "<color=#4CAF50>DEFENSE SUCCESS!</color>";
                else
                    defensePhaseText.text = "<color=#F44336>DEFENSE FAILED!</color>\n<size=70%>Core Destroyed!</size>";
            }
        }

        private void OnGameOver()
        {
            SetEndTurnButtonEnabled(false);

            if (normalPhasePanel != null) normalPhasePanel.SetActive(true);
            if (defensePhasePanel != null) defensePhasePanel.SetActive(false);

            if (normalPhaseText != null)
            {
                normalPhaseText.text = "<color=#F44336>GAME OVER</color>";
            }
        }

        private void SetEndTurnButtonEnabled(bool enabled)
        {
            ResolveEndTurnButton();

            if (endTurnButton == null) return;

            endTurnButton.interactable = enabled;

            if (endTurnButtonCanvasGroup != null)
            {
                endTurnButtonCanvasGroup.alpha = enabled ? 1f : disabledEndTurnButtonAlpha;
            }
        }

        private void ResolveEndTurnButton()
        {
            if (endTurnButton == null)
            {
                var foundButtonObject = GameObject.Find("TurnEnd_Button");
                if (foundButtonObject != null)
                {
                    endTurnButton = foundButtonObject.GetComponent<Button>();
                }
            }

            if (endTurnButton != null && endTurnButtonCanvasGroup == null)
            {
                endTurnButtonCanvasGroup = endTurnButton.GetComponent<CanvasGroup>();
                if (endTurnButtonCanvasGroup == null)
                {
                    endTurnButtonCanvasGroup = endTurnButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}
