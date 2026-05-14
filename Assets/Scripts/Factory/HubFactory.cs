using System;
using System.Collections.Generic;
using Overlap.Blocks;
using Overlap.Core;
using Overlap.WorldMap;
using UnityEngine;
using UnityEngine.Serialization;

namespace Overlap.Factory
{
    /// <summary>
    /// 플레이어가 소유한 거점(Main 또는 Occupied)에서 블록을 생산하는 공장입니다.
    /// 턴이 지날 때마다 카운트가 오르며, 완료 시 3개의 후보 블록을 생성해 대기합니다.
    /// </summary>
    [RequireComponent(typeof(HubNode))]
    public sealed class HubFactory : MonoBehaviour
    {
        // ── 이벤트 ───────────────────────────────────────────────────────────
        /// <summary>생산 완료 시 (UI 띄우기 용도)</summary>
        public event Action<HubFactory> ProductionReady;

        // ── Inspector 설정 ────────────────────────────────────────────────────
        [Header("Factory Settings")]
        [Tooltip("몇 턴마다 블록을 생산하는지")]
        [SerializeField, Min(1)] private int generationIntervalTurns = 3;

        [Tooltip("이 공장에서 생산 가능한 블록 형태들 (보통 같은 칸 수(n칸)끼리 묶음)")]
        [SerializeField] private TowerBlockDefinition[] shapeTemplates;

        [Tooltip("확정적으로 포함될 속성")]
        [SerializeField] private TowerElement guaranteedElement = TowerElement.Fire;

        [Header("Random Shape Generation")]
        [Tooltip("When enabled, this hub generates random block shapes inside an n x n area instead of using Shape Templates.")]
        [SerializeField] private bool useRandomShapeGeneration = true;

        [Tooltip("The side length of the random shape area.")]
        [SerializeField, Min(1)] private int randomShapeGridSize = 3;

        [Tooltip("The minimum number of cells in each generated block. Clamped to n x n.")]
        [SerializeField, Min(1)] private int randomShapeMinCellCount = 3;

        [Tooltip("The maximum number of cells in each generated block. Clamped to n x n.")]
        [SerializeField, Min(1), FormerlySerializedAs("randomShapeCellCount")] private int randomShapeMaxCellCount = 4;

        [Tooltip("The number of candidate blocks shown when production is ready.")]
        [SerializeField, Min(1)] private int candidateCount = 3;

        // ── 런타임 상태 ───────────────────────────────────────────────────────
        private HubNode hubNode;
        private int currentTurnCount = 0;
        private bool isReadyToHarvest = false;
        private List<TowerBlockDefinition> currentCandidates = new List<TowerBlockDefinition>();

        public HubNode HubNode => hubNode;
        public int GenerationIntervalTurns => generationIntervalTurns;
        public int CurrentTurnCount => currentTurnCount;
        public TowerElement GuaranteedElement => guaranteedElement;
        public bool UsesRandomShapeGeneration => useRandomShapeGeneration;
        public int RandomShapeGridSize => randomShapeGridSize;
        public int RandomShapeMinCellCount => Mathf.Min(randomShapeMinCellCount, randomShapeGridSize * randomShapeGridSize);
        public int RandomShapeMaxCellCount => Mathf.Min(randomShapeMaxCellCount, randomShapeGridSize * randomShapeGridSize);
        public bool IsReadyToHarvest => isReadyToHarvest;
        public IReadOnlyList<TowerBlockDefinition> Candidates => currentCandidates;

        private void Awake()
        {
            hubNode = GetComponent<HubNode>();
        }

        private void Start()
        {
            var turnManager = FindAnyObjectByType<TurnManager>();
            if (turnManager != null)
            {
                turnManager.PlanningPhaseStarted += OnTurnStarted;
            }
        }

        private void OnDestroy()
        {
            var turnManager = FindAnyObjectByType<TurnManager>();
            if (turnManager != null)
            {
                turnManager.PlanningPhaseStarted -= OnTurnStarted;
            }
        }

        private void OnTurnStarted(int turn)
        {
            // 빈 거점(미점령)이거나 이미 수확 대기 중이면 턴 카운트를 올리지 않음
            if (hubNode.IsEmpty || hubNode.IsContested || isReadyToHarvest) return;

            currentTurnCount++;
            
            if (currentTurnCount >= generationIntervalTurns)
            {
                GenerateCandidates();
            }
        }

        private void GenerateCandidates()
        {
            currentCandidates.Clear();

            if (useRandomShapeGeneration)
            {
                GenerateRandomCandidates();
            }
            else
            {
                GenerateTemplateCandidates();
            }

            if (currentCandidates.Count == 0)
            {
                return;
            }

            isReadyToHarvest = true;
            currentTurnCount = 0;

            Debug.Log($"[HubFactory] {hubNode.HubName} production ready! Click to harvest.");
            ProductionReady?.Invoke(this);
        }

        private void GenerateRandomCandidates()
        {
            var minCellCount = RandomShapeMinCellCount;
            var maxCellCount = RandomShapeMaxCellCount;

            for (var i = 0; i < candidateCount; i++)
            {
                var cellCount = UnityEngine.Random.Range(minCellCount, maxCellCount + 1);
                var cells = TowerBlockShapeGenerator.GenerateCells(
                    randomShapeGridSize,
                    cellCount,
                    guaranteedElement);

                var generatedBlock = ScriptableObject.CreateInstance<TowerBlockDefinition>();
                generatedBlock.name = $"{hubNode.HubName}_GeneratedBlock_{i + 1}";
                generatedBlock.Initialize(
                    $"{hubNode.HubName} Block {i + 1}",
                    cells);

                currentCandidates.Add(generatedBlock);
            }
        }

        private void GenerateTemplateCandidates()
        {
            if (shapeTemplates == null || shapeTemplates.Length == 0)
            {
                Debug.LogWarning($"[HubFactory] {hubNode.HubName} has no shape templates!");
                return;
            }

            // 조건에 맞는 템플릿 필터링 (확정 속성 포함)
            var validTemplates = new List<TowerBlockDefinition>();
            foreach (var t in shapeTemplates)
            {
                if (t != null && t.Cells != null)
                {
                    bool hasGuaranteed = false;
                    foreach (var cell in t.Cells)
                    {
                        if (cell.element == guaranteedElement)
                        {
                            hasGuaranteed = true;
                            break;
                        }
                    }
                    if (hasGuaranteed) validTemplates.Add(t);
                }
            }

            if (validTemplates.Count == 0)
            {
                Debug.LogWarning($"[HubFactory] No shape templates contain the guaranteed element: {guaranteedElement}");
                // 조건에 맞는 게 없으면 그냥 아무거나 쓰기 (Fallback)
                validTemplates.AddRange(shapeTemplates);
            }

            // 3개의 후보 생성
            for (int i = 0; i < candidateCount; i++)
            {
                currentCandidates.Add(validTemplates[UnityEngine.Random.Range(0, validTemplates.Count)]);
            }
        }

        /// <summary>
        /// 플레이어가 후보 중 하나를 선택해 수확합니다.
        /// </summary>
        public TowerBlockDefinition Harvest(int candidateIndex)
        {
            if (!isReadyToHarvest || candidateIndex < 0 || candidateIndex >= currentCandidates.Count)
            {
                return null;
            }

            var selected = currentCandidates[candidateIndex];
            isReadyToHarvest = false;
            currentCandidates.Clear();

            return selected;
        }

        public void SetGuaranteedElement(TowerElement element)
        {
            guaranteedElement = element;
            Debug.Log($"[HubFactory] {hubNode.HubName} guaranteed element set to {element}");
        }

        private void OnValidate()
        {
            generationIntervalTurns = Mathf.Max(1, generationIntervalTurns);
            randomShapeGridSize = Mathf.Max(1, randomShapeGridSize);
            var maxPossibleCellCount = randomShapeGridSize * randomShapeGridSize;
            randomShapeMinCellCount = Mathf.Clamp(randomShapeMinCellCount, 1, maxPossibleCellCount);
            randomShapeMaxCellCount = Mathf.Clamp(randomShapeMaxCellCount, randomShapeMinCellCount, maxPossibleCellCount);
            candidateCount = Mathf.Max(1, candidateCount);
        }
    }
}
