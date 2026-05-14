using UnityEngine;
using Overlap.Factory;
using System.Linq;

namespace Overlap.WorldMap.UI
{
    /// <summary>
    /// 거점 중앙에 띄워지는 인월드 UI(또는 아이콘)를 관리합니다.
    /// 공장에서 블록 생산이 완료되었을 때 시각적으로 알려줍니다.
    /// </summary>
    [RequireComponent(typeof(HubFactory))]
    public class HubInWorldUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("생산 완료 시 띄워줄 알림 아이콘 (SpriteRenderer 또는 Canvas 묶음)")]
        [SerializeField] private GameObject readyIcon;

        [Tooltip("침략 활성화 시 띄워줄 경고 아이콘")]
        [SerializeField] private GameObject invasionWarningIcon;

        [Header("Persistent Info UI Settings")]
        [Tooltip("정보 텍스트의 오프셋 위치")]
        [SerializeField] private Vector3 infoOffset = new Vector3(0, 1.2f, 0);
        [Tooltip("정보 텍스트의 폰트 크기")]
        [SerializeField] private float infoFontSize = 2.5f;
        [Tooltip("메인 거점에도 정보를 표시할지 여부")]
        [SerializeField] private bool showMainHubInfo = true;

        private TMPro.TextMeshPro dynamicTurnsText;
        private TMPro.TextMeshPro hubInfoText;

        private HubFactory hubFactory;
        private TurnManager turnManager;
        private HubNode hubNode;

        private void Awake()
        {
            hubFactory = GetComponent<HubFactory>();
            hubNode = GetComponent<HubNode>();
        }

        private void Start()
        {
            turnManager = FindAnyObjectByType<TurnManager>();
        }

        private void OnEnable()
        {
            if (hubFactory != null)
            {
                hubFactory.ProductionReady += OnProductionReady;
            }
        }

        private void OnDisable()
        {
            if (hubFactory != null)
            {
                hubFactory.ProductionReady -= OnProductionReady;
            }
        }

        private void Update()
        {
            // 생산 수확 알림
            if (readyIcon != null && readyIcon.activeSelf && (hubFactory == null || !hubFactory.IsReadyToHarvest))
            {
                readyIcon.SetActive(false);
            }

            UpdatePersistentHubInfo();

            // 침략 경고 알림 및 남은 턴 수
            if (turnManager != null && hubNode != null)
            {
                bool isScheduled = false;
                int turnsLeft = 0;
                foreach (var inv in turnManager.ScheduledInvasions)
                {
                    if (inv.TargetHub == hubNode)
                    {
                        isScheduled = true;
                        turnsLeft = inv.RemainingTurns;
                        break;
                    }
                }

                bool isActive = turnManager.ActiveInvasions.Contains(hubNode);
                bool shouldShowWarning = isScheduled || isActive;

                if (invasionWarningIcon != null && invasionWarningIcon.activeSelf != shouldShowWarning)
                {
                    invasionWarningIcon.SetActive(shouldShowWarning);
                }

                if (shouldShowWarning && invasionWarningIcon != null)
                {
                    if (dynamicTurnsText == null)
                    {
                        var textObj = new GameObject("DynamicTurnsText");
                        textObj.transform.SetParent(invasionWarningIcon.transform, false);
                        textObj.transform.localPosition = Vector3.zero;
                        
                        dynamicTurnsText = textObj.AddComponent<TMPro.TextMeshPro>();
                        dynamicTurnsText.color = Color.black;
                        dynamicTurnsText.alignment = TMPro.TextAlignmentOptions.Center;
                        dynamicTurnsText.fontSize = 4f;
                        dynamicTurnsText.fontStyle = TMPro.FontStyles.Bold;
                        dynamicTurnsText.sortingOrder = 100; // 아이콘보다 앞에 렌더링되도록
                    }

                    if (isActive)
                        dynamicTurnsText.text = "!";
                    else if (isScheduled)
                        dynamicTurnsText.text = turnsLeft.ToString();
                }
            }
        }

        private void UpdatePersistentHubInfo()
        {
            if (hubFactory == null || hubNode == null) return;
            
            // 설정에 따라 메인 거점 표시 여부 결정
            if (hubNode.IsMain && !showMainHubInfo) 
            {
                if (hubInfoText != null) hubInfoText.gameObject.SetActive(false);
                return;
            }

            if (hubInfoText == null)
            {
                var textObj = new GameObject("HubInfoText");
                textObj.transform.SetParent(transform, false);
                
                hubInfoText = textObj.AddComponent<TMPro.TextMeshPro>();
                hubInfoText.color = Color.black;
                hubInfoText.alignment = TMPro.TextAlignmentOptions.Center;
                hubInfoText.fontStyle = TMPro.FontStyles.Bold;
                hubInfoText.sortingOrder = 90;
            }

            // 실시간 위치 및 크기 반영
            hubInfoText.transform.localPosition = infoOffset;
            hubInfoText.fontSize = infoFontSize;

            if (!hubInfoText.gameObject.activeSelf) hubInfoText.gameObject.SetActive(true);

            int turnsLeft = Mathf.Max(0, hubFactory.GenerationIntervalTurns - hubFactory.CurrentTurnCount);
            string blockSizeRange = hubFactory.UsesRandomShapeGeneration
                ? $"{hubFactory.RandomShapeMinCellCount}~{hubFactory.RandomShapeMaxCellCount}"
                : ResolveTemplateBlockSizeRange();

            // 유저 요청: 속성, 남은 턴수, 블록 칸 수 (모두 검은색으로 통일)
            hubInfoText.text = $"{hubFactory.GuaranteedElement}\n" +
                               $"{turnsLeft} Turns\n" +
                               $"{blockSizeRange} Cells";
        }

        private string ResolveTemplateBlockSizeRange()
        {
            if (hubFactory.Candidates.Count == 0)
            {
                return "0~0";
            }

            int min = int.MaxValue;
            int max = int.MinValue;
            foreach (var candidate in hubFactory.Candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                int cellCount = candidate.Cells.Count;
                min = Mathf.Min(min, cellCount);
                max = Mathf.Max(max, cellCount);
            }

            return min == int.MaxValue ? "0~0" : $"{min}~{max}";
        }

        private void OnProductionReady(HubFactory factory)
        {
            if (readyIcon != null)
            {
                readyIcon.SetActive(true);
            }
        }
    }
}
