using UnityEngine;
using Overlap.Factory;
using Overlap.Inventory;
using Overlap.Blocks;
using Overlap.Core;
using Overlap.Tilemaps;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Overlap.WorldMap.UI
{
    /// <summary>
    /// 플레이어가 거점을 클릭했을 때 뜨는 UI 패널들을 관리합니다.
    /// (1) 거점 관리 창 (체력, 블록 크기, 남은 턴, 속성 변경)
    /// (2) 블록 3선택지 창 (생성 완료 시)
    /// </summary>
    public class HubUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlockInventory inventory;
        [SerializeField] private Button endTurnButton;
        [SerializeField, Range(0f, 1f)] private float disabledEndTurnButtonAlpha = 0.35f;

        [Header("UI Panels (Assign in Inspector)")]
        [Tooltip("생성 완료 시 3개의 후보를 보여주는 패널 오브젝트")]
        [SerializeField] private GameObject blockSelectionPanel;
        
        [Tooltip("일반 상태일 때 거점 정보를 보여주는 관리 패널 오브젝트")]
        [SerializeField] private GameObject hubManagementPanel;

        [Tooltip("빈 거점 클릭 시 점령 의사를 묻는 패널 오브젝트")]
        [SerializeField] private GameObject occupationConfirmPanel;

        [Tooltip("침략 활성화 시 방어 의사를 묻는 패널 오브젝트")]
        [SerializeField] private GameObject invasionConfirmPanel;

        [Header("Block Selection Elements")]
        [SerializeField] private Button[] blockSelectButtons;
        [SerializeField] private TextMeshProUGUI[] blockSelectTexts;
        [Tooltip("블록 선택 버튼 내부에서 그려질 셀의 크기")]
        [SerializeField] private float blockSelectionCellSize = 25f;
        [SerializeField] private TowerTileDefinition[] blockPreviewTiles;

        [Header("Hub Management Elements")]
        [SerializeField] private TextMeshProUGUI hubInfoText;
        [Tooltip("확정 속성을 변경할 드롭다운 (옵션의 순서가 TowerElement enum과 일치해야 함)")]
        [SerializeField] private TMP_Dropdown guaranteedElementDropdown;
        [Tooltip("드롭다운 리스트의 개별 항목 높이 (빽빽하게 겹칠 때 키워주세요)")]
        [SerializeField] private float dropdownItemHeight = 40f;

        [Header("Occupation Elements")]
        [SerializeField] private TextMeshProUGUI occupationHubNameText;

        [Header("Invasion Elements")]
        [SerializeField] private TextMeshProUGUI invasionHubNameText;

        // 현재 상호작용 중인 공장
        private HubFactory currentFactory;
        private HubNode targetOccupationHub;
        private HubNode targetInvasionHub;
        private CanvasGroup endTurnButtonCanvasGroup;
        private TurnManager turnManager;

        private void OnEnable()
        {
            HubInteractable.OnHarvestRequested += ShowBlockSelectionUI;
            HubInteractable.OnHubManagementRequested += ShowHubManagementUI;
            HubInteractable.OnOccupationRequested += ShowOccupationUI;
            HubInteractable.OnInvasionRequested += ShowInvasionUI;
        }

        private void OnDisable()
        {
            HubInteractable.OnHarvestRequested -= ShowBlockSelectionUI;
            HubInteractable.OnHubManagementRequested -= ShowHubManagementUI;
            HubInteractable.OnOccupationRequested -= ShowOccupationUI;
            HubInteractable.OnInvasionRequested -= ShowInvasionUI;
            SetEndTurnButtonEnabled(true);
        }

        private void Start()
        {
            SetEndTurnButtonEnabled(blockSelectionPanel == null || !blockSelectionPanel.activeSelf);

            // 드롭다운 리스너 등록 및 초기화
            if (guaranteedElementDropdown != null)
            {
                PopulateElementDropdown();
                guaranteedElementDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }
        }

        private void PopulateElementDropdown()
        {
            if (guaranteedElementDropdown == null) return;

            guaranteedElementDropdown.ClearOptions();
            
            var options = new System.Collections.Generic.List<string>();
            var enumValues = System.Enum.GetValues(typeof(TowerElement));
            
            foreach (var val in enumValues)
            {
                options.Add(val.ToString());
            }
            
            guaranteedElementDropdown.AddOptions(options);

            // 리스트 항목들이 빽빽하게 겹치는 문제 해결 (높이 조절)
            if (guaranteedElementDropdown.template != null)
            {
                // Template 내부의 Item RectTransform을 찾아 높이 수정
                var itemTransform = guaranteedElementDropdown.template.Find("Viewport/Content/Item");
                if (itemTransform != null)
                {
                    var rect = itemTransform.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        var size = rect.sizeDelta;
                        size.y = dropdownItemHeight;
                        rect.sizeDelta = size;
                    }
                }
            }
        }

        private void Update()
        {
            // 거점 관리 창이 열려있을 때, UI 바깥(빈 공간)을 클릭하면 창을 닫습니다.
            if (hubManagementPanel != null && hubManagementPanel.activeSelf)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                    {
                        CloseHubManagementUI();
                    }
                }
            }
            
            // 3선택지 창도 마찬가지로 바깥 클릭 시 닫기 원한다면 아래 주석 해제
            /*
            if (blockSelectionPanel != null && blockSelectionPanel.activeSelf)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
                    {
                        if (blockSelectionPanel != null) blockSelectionPanel.SetActive(false);
                        currentFactory = null;
                    }
                }
            }
            */
        }

        // ── (1) 블록 3선택지 (수확) ───────────────────────────────────────────
        private void ShowBlockSelectionUI(HubNode node, HubFactory factory)
        {
            currentFactory = factory;
            
            // 버튼과 텍스트 갱신
            for (int i = 0; i < blockSelectButtons.Length; i++)
            {
                if (i < factory.Candidates.Count)
                {
                    var candidate = factory.Candidates[i];
                    blockSelectButtons[i].gameObject.SetActive(true);
                    
                    if (blockSelectTexts.Length > i && blockSelectTexts[i] != null)
                    {
                        blockSelectTexts[i].text = ""; // 텍스트 숨김
                    }

                    // 블록 모양 그리기
                    BuildBlockVisualOnButton(blockSelectButtons[i].transform, candidate);
                }
                else
                {
                    // 후보 개수가 버튼 수보다 적을 경우 숨김
                    blockSelectButtons[i].gameObject.SetActive(false);
                }
            }

            if (blockSelectionPanel != null)
                blockSelectionPanel.SetActive(true);
            SetEndTurnButtonEnabled(false);

            if (hubManagementPanel != null)
                hubManagementPanel.SetActive(false);
        }

        private void BuildBlockVisualOnButton(Transform buttonTransform, TowerBlockDefinition def)
        {
            // 기존 비주얼 삭제
            foreach (Transform child in buttonTransform)
            {
                if (child.name == "BlockVisualCell")
                {
                    Destroy(child.gameObject);
                }
            }

            if (def == null) return;

            float cellSize = blockSelectionCellSize;
            
            // 중앙 정렬을 위한 바운드 계산
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            
            if (def.Cells.Count == 0) return;

            foreach (var cell in def.Cells)
            {
                minX = Mathf.Min(minX, cell.offset.x);
                maxX = Mathf.Max(maxX, cell.offset.x);
                minY = Mathf.Min(minY, cell.offset.y);
                maxY = Mathf.Max(maxY, cell.offset.y);
            }

            float centerX = (minX + maxX) * 0.5f * cellSize;
            float centerY = (minY + maxY) * 0.5f * cellSize;

            foreach (var cell in def.Cells)
            {
                GameObject cellObj = new GameObject("BlockVisualCell");
                cellObj.transform.SetParent(buttonTransform, false);
                var img = cellObj.AddComponent<Image>();
                
                TowerTileVisualResolver.ApplyToImage(
                    img,
                    cell.element,
                    ResolveBlockPreviewTiles(),
                    GetColor(cell.element));
                
                var rect = cellObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(cellSize, cellSize);
                
                // 중심점 오프셋 적용
                float posX = cell.offset.x * cellSize - centerX;
                float posY = cell.offset.y * cellSize - centerY;
                rect.anchoredPosition = new Vector2(posX, posY);
            }
        }

        private Color GetColor(TowerElement element)
        {
            return element switch
            {
                TowerElement.Fire => Color.red,
                TowerElement.Ice => Color.blue,
                TowerElement.Rock => new Color(0.6f, 0.3f, 0.1f),
                TowerElement.Wind => Color.green,
                TowerElement.Poison => new Color(0.5f, 0f, 0.5f),
                _ => Color.white
            };
        }

        private TowerTileDefinition[] ResolveBlockPreviewTiles()
        {
            if (blockPreviewTiles != null && blockPreviewTiles.Length > 0)
            {
                return blockPreviewTiles;
            }

            var towerRenderer = FindAnyObjectByType<TilemapTowerRenderer>();
            if (towerRenderer != null)
            {
                blockPreviewTiles = towerRenderer.TowerTiles;
            }

            return blockPreviewTiles;
        }

        /// <summary>
        /// UI 버튼 클릭 시 호출할 메서드 (Index: 0, 1, 2)
        /// </summary>
        public void SelectBlockCandidate(int index)
        {
            if (currentFactory == null) return;

            var selectedBlock = currentFactory.Harvest(index);
            if (selectedBlock != null)
            {
                if (inventory != null)
                {
                    inventory.TryAdd(selectedBlock);
                }
                else
                {
                    Debug.LogWarning("[HubUIManager] Inventory is not assigned!");
                }
            }

            // 창 닫기
            if (blockSelectionPanel != null)
                blockSelectionPanel.SetActive(false);
            SetEndTurnButtonEnabled(true);
            
            currentFactory = null;
        }

        // ── (2) 거점 관리 창 ──────────────────────────────────────────────────
        private void ShowHubManagementUI(HubNode node, HubFactory factory)
        {
            currentFactory = factory;

            if (hubInfoText != null)
            {
                // 코어 체력이 없는 빈 거점의 경우 예외 처리
                string hpText = node.CoreHealth != null 
                    ? $"{node.CoreHealth.CurrentHealth} / {node.CoreHealth.MaxHealth}" 
                    : "N/A";

                int totalTurns = factory.GenerationIntervalTurns;
                int turnsLeft = totalTurns - factory.CurrentTurnCount;
                
                // 블록 크기 계산 (첫 번째 템플릿 기준)
                string blockSizeText = factory.UsesRandomShapeGeneration
                    ? factory.RandomShapeMinCellCount == factory.RandomShapeMaxCellCount
                        ? factory.RandomShapeMaxCellCount.ToString()
                        : $"{factory.RandomShapeMinCellCount}~{factory.RandomShapeMaxCellCount}"
                    : "0";
                if (!factory.UsesRandomShapeGeneration && factory.Candidates.Count > 0 && factory.Candidates[0] != null)
                    blockSizeText = factory.Candidates[0].Cells.Count.ToString();

                string info = $"<size=120%><b>{node.HubName}</b></size>\n\n" +
                              $"거점 체력: {hpText}\n" +
                              $"블록 크기: {blockSizeText} 칸\n" +
                              $"생산 주기: {totalTurns} 턴\n" +
                              $"남은 턴수: {turnsLeft} 턴\n" +
                              $"확정 속성: {factory.GuaranteedElement}";
                              
                hubInfoText.text = info;
            }

            // 드롭다운 값 동기화
            if (guaranteedElementDropdown != null)
            {
                guaranteedElementDropdown.SetValueWithoutNotify((int)factory.GuaranteedElement);
            }

            if (hubManagementPanel != null)
                hubManagementPanel.SetActive(true);
            
            if (blockSelectionPanel != null)
                blockSelectionPanel.SetActive(false);
            SetEndTurnButtonEnabled(true);
        }

        private void SetEndTurnButtonEnabled(bool enabled)
        {
            ResolveEndTurnButton();

            if (endTurnButton == null) return;

            var shouldEnable = enabled && CanEnableEndTurnButtonByPhase();
            endTurnButton.interactable = shouldEnable;

            if (endTurnButtonCanvasGroup != null)
            {
                endTurnButtonCanvasGroup.alpha = shouldEnable ? 1f : disabledEndTurnButtonAlpha;
            }
        }

        private bool CanEnableEndTurnButtonByPhase()
        {
            if (turnManager == null)
            {
                turnManager = FindAnyObjectByType<TurnManager>();
            }

            return turnManager == null || turnManager.Phase == TurnPhase.Planning;
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

        /// <summary>
        /// 드롭다운 값이 변경될 때 호출
        /// </summary>
        private void OnDropdownValueChanged(int index)
        {
            ChangeGuaranteedElement((TowerElement)index);
        }

        /// <summary>
        /// 속성 변경을 직접 호출할 때 사용
        /// </summary>
        public void ChangeGuaranteedElement(TowerElement newElement)
        {
            if (currentFactory != null)
            {
                currentFactory.SetGuaranteedElement(newElement);
                // 변경 후 UI 갱신
                ShowHubManagementUI(currentFactory.HubNode, currentFactory);
            }
        }

        public void CloseHubManagementUI()
        {
            if (hubManagementPanel != null)
                hubManagementPanel.SetActive(false);
            
            currentFactory = null;
        }

        // ── (3) 거점 점령 확인 창 ────────────────────────────────────────────────
        private void ShowOccupationUI(HubNode node)
        {
            targetOccupationHub = node;
            if (occupationHubNameText != null)
                occupationHubNameText.text = $"{node.HubName} 거점을 점령하시겠습니까?";
            
            if (occupationConfirmPanel != null)
                occupationConfirmPanel.SetActive(true);
        }

        public void ConfirmOccupation()
        {
            if (targetOccupationHub != null)
            {
                var turnManager = FindAnyObjectByType<TurnManager>();
                if (turnManager != null)
                {
                    turnManager.TryOccupyHub(targetOccupationHub);
                }
            }
            CloseOccupationUI();
        }

        public void CloseOccupationUI()
        {
            targetOccupationHub = null;
            if (occupationConfirmPanel != null)
                occupationConfirmPanel.SetActive(false);
        }

        // ── (4) 침략 방어 확인 창 ────────────────────────────────────────────────
        private void ShowInvasionUI(HubNode node)
        {
            targetInvasionHub = node;
            if (invasionHubNameText != null)
                invasionHubNameText.text = $"{node.HubName} 거점을 방어하시겠습니까?";
            
            if (invasionConfirmPanel != null)
                invasionConfirmPanel.SetActive(true);
        }

        public void ConfirmInvasion()
        {
            if (targetInvasionHub != null)
            {
                var turnManager = FindAnyObjectByType<TurnManager>();
                if (turnManager != null)
                {
                    turnManager.TryHandleInvasion(targetInvasionHub);
                }
            }
            CloseInvasionUI();
        }

        public void CloseInvasionUI()
        {
            targetInvasionHub = null;
            if (invasionConfirmPanel != null)
                invasionConfirmPanel.SetActive(false);
        }
    }
}
