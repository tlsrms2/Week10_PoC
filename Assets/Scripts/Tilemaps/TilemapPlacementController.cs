using System;
using Overlap.Blocks;
using Overlap.Core;
using Overlap.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    public sealed class TilemapPlacementController : MonoBehaviour
    {
        [SerializeField] private TilemapBoardReader boardReader = null;
        [SerializeField] private TilemapTowerRenderer towerRenderer = null;
        [SerializeField] private TilemapBlockPreviewRenderer previewRenderer = null;
        [SerializeField] private Tilemap targetTilemap = null;
        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private BlockInventory inventory = null;
        [SerializeField] private TowerBlockDefinition selectedBlock = null;

        private TowerBoard board = null;
        private int rotationQuarterTurns;

        public TowerBoard Board => board;

        private void Awake()
        {
            RebuildBoardFromTilemap();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            HandleSelectionInput();

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotateSelectedBlockClockwise();
            }

            // UI 위에서는 프리뷰/배치 모두 차단
            var isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            
            // 거점 위에서는 프리뷰/배치 모두 차단
            bool isOverHub = false;
            var cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse != null)
            {
                Vector2 worldPos = cameraToUse.ScreenToWorldPoint(mouse.position.ReadValue());
                var hitCols = Physics2D.OverlapPointAll(worldPos);
                foreach (var col in hitCols)
                {
                    if (col.GetComponent<WorldMap.HubInteractable>() != null)
                    {
                        isOverHub = true;
                        break;
                    }
                }
            }

            if (!isOverUI && !isOverHub)
            {
                UpdatePreview(mouse.position.ReadValue());
            }
            else
            {
                previewRenderer?.Clear();
            }

            if (!isOverUI && !isOverHub && mouse.leftButton.wasPressedThisFrame)
            {
                if (ResolveSelectedBlock() != null)
                {
                    TryPlaceAtScreenPosition(mouse.position.ReadValue());
                }
            }
        }

        [ContextMenu("Rebuild Board From Tilemap")]
        public void RebuildBoardFromTilemap()
        {
            if (boardReader == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapPlacementController)} requires a board reader.");
            }

            board = boardReader.CreateBoard();
            towerRenderer?.Clear();
            previewRenderer?.Clear();
            Debug.Log($"Tower board rebuilt from tilemap. Buildable cells: {boardReader.ReadBuildableCells().Count}");
        }

        public void RotateSelectedBlockClockwise()
        {
            rotationQuarterTurns = (rotationQuarterTurns + 1) % 4;
            Debug.Log($"Selected block rotation: {rotationQuarterTurns * 90} degrees.");
        }

        public bool TryPlaceAtScreenPosition(Vector2 screenPosition)
        {
            var cellPosition = ScreenToCell(screenPosition);
            return TryPlaceAtCell(cellPosition);
        }

        public bool TryPlaceAtCell(Vector3Int cellPosition)
        {
            if (board == null)
            {
                RebuildBoardFromTilemap();
            }

            var block = CreateSelectedBlock().RotateClockwise(rotationQuarterTurns);
            var origin = TilemapGridPointMapper.ToGridPoint(cellPosition);
            var result = board.Place(block, origin);

            if (!result.CanPlace)
            {
                Debug.Log($"Placement failed at {cellPosition}: {string.Join(", ", result.Issues)}");
                return false;
            }

            towerRenderer?.Render(board);
            inventory?.TryConsumeSelected();
            Debug.Log($"Placed selected block at {cellPosition}.");
            return true;
        }

        public void ClearBlocksAround(Vector3 worldPosition, int cellRadius)
        {
            if (board == null) return;
            var cellPosition = ResolveTargetTilemap().WorldToCell(worldPosition);
            var centerGridPoint = TilemapGridPointMapper.ToGridPoint(cellPosition);
            board.ClearArea(centerGridPoint, cellRadius);
            towerRenderer?.Render(board);
            Debug.Log($"Cleared blocks around {worldPosition} within radius {cellRadius}.");
        }

        private void UpdatePreview(Vector2 screenPosition)
        {
            if (previewRenderer == null || ResolveSelectedBlock() == null)
            {
                previewRenderer?.Clear();
                return;
            }

            if (board == null)
            {
                RebuildBoardFromTilemap();
            }

            var cellPosition = ScreenToCell(screenPosition);
            var origin = TilemapGridPointMapper.ToGridPoint(cellPosition);
            var block = CreateSelectedBlock().RotateClockwise(rotationQuarterTurns);
            previewRenderer.Render(block, origin, board.CanPlace(block, origin).CanPlace);
        }

        private TowerBlock CreateSelectedBlock()
        {
            var blockDefinition = ResolveSelectedBlock();
            if (blockDefinition == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapPlacementController)} requires a selected block definition.");
            }

            return blockDefinition.CreateBlock();
        }

        private TowerBlockDefinition ResolveSelectedBlock()
        {
            return inventory != null ? inventory.SelectedBlock : selectedBlock;
        }

        private void HandleSelectionInput()
        {
            if (inventory == null || Keyboard.current == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame) inventory.TrySelect(0);
            if (keyboard.digit2Key.wasPressedThisFrame) inventory.TrySelect(1);
            if (keyboard.digit3Key.wasPressedThisFrame) inventory.TrySelect(2);
            if (keyboard.digit4Key.wasPressedThisFrame) inventory.TrySelect(3);
            if (keyboard.digit5Key.wasPressedThisFrame) inventory.TrySelect(4);
        }

        private Vector3Int ScreenToCell(Vector2 screenPosition)
        {
            var cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapPlacementController)} requires a target camera or Camera.main.");
            }

            var tilemapToUse = ResolveTargetTilemap();
            var worldPosition = cameraToUse.ScreenToWorldPoint(screenPosition);
            return tilemapToUse.WorldToCell(worldPosition);
        }

        private Tilemap ResolveTargetTilemap()
        {
            if (targetTilemap != null)
            {
                return targetTilemap;
            }

            if (boardReader != null && boardReader.BuildableTilemap != null)
            {
                return boardReader.BuildableTilemap;
            }

            throw new InvalidOperationException($"{nameof(TilemapPlacementController)} requires a target tilemap.");
        }

        private void OnValidate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }
}
