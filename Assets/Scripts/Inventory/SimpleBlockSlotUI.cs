using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Overlap.Core;
using Overlap.Blocks;
using Overlap.Tilemaps;
using UnityEngine.EventSystems;

namespace Overlap.Inventory
{
    public class SimpleBlockSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI blockNameText;
        [SerializeField] private RectTransform blockContainer; // 블록 픽셀을 담을 컨테이너 (없으면 transform 사용)
        [SerializeField] private float cellSize = 15f; // UI 픽셀 사이즈
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.yellow;
        [SerializeField] private Color emptyColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        
        [Header("Element Colors")]
        [SerializeField] private Color fireColor = Color.red;
        [SerializeField] private Color iceColor = Color.blue;
        [SerializeField] private Color rockColor = new Color(0.6f, 0.3f, 0.1f);
        [SerializeField] private Color windColor = Color.green;
        [SerializeField] private Color poisonColor = new Color(0.5f, 0f, 0.5f);
        [SerializeField] private TowerTileDefinition[] elementTiles;

        private Action clicked;
        private bool hasBlock;

        public void SetClickHandler(Action onClicked)
        {
            clicked = onClicked;
        }

        public void SetEmpty()
        {
            hasBlock = false;
            if (background != null) background.color = emptyColor;
            if (blockNameText != null) blockNameText.text = ""; // 텍스트 숨김
            ClearContainer();
        }

        public void SetBlock(IEnumerable<BlockCellDefinition> cells, bool isSelected)
        {
            hasBlock = true;
            if (background != null) background.color = isSelected ? selectedColor : normalColor;
            if (blockNameText != null) blockNameText.text = ""; // 텍스트 숨김
            
            ClearContainer();
            if (cells != null) BuildBlockVisual(cells);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!hasBlock || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            clicked?.Invoke();
        }

        private void ClearContainer()
        {
            Transform parent = blockContainer != null ? blockContainer : transform;
            foreach (Transform child in parent)
            {
                if (child.name == "BlockVisualCell")
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void BuildBlockVisual(IEnumerable<BlockCellDefinition> cells)
        {
            Transform parent = blockContainer != null ? blockContainer : transform;
            
            // 중앙 정렬을 위한 바운드 계산
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            var cellList = new List<BlockCellDefinition>(cells);

            if (cellList.Count == 0) return;

            foreach (var cell in cellList)
            {
                minX = Mathf.Min(minX, cell.offset.x);
                maxX = Mathf.Max(maxX, cell.offset.x);
                minY = Mathf.Min(minY, cell.offset.y);
                maxY = Mathf.Max(maxY, cell.offset.y);
            }

            float centerX = (minX + maxX) * 0.5f * cellSize;
            float centerY = (minY + maxY) * 0.5f * cellSize;
            
            foreach (var cell in cellList)
            {
                GameObject cellObj = new GameObject("BlockVisualCell");
                cellObj.transform.SetParent(parent, false);
                var img = cellObj.AddComponent<Image>();
                
                TowerTileVisualResolver.ApplyToImage(
                    img,
                    cell.element,
                    ResolveElementTiles(),
                    GetColor(cell.element));
                img.raycastTarget = false;
                
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
                TowerElement.Fire => fireColor,
                TowerElement.Ice => iceColor,
                TowerElement.Rock => rockColor,
                TowerElement.Wind => windColor,
                TowerElement.Poison => poisonColor,
                _ => Color.white
            };
        }

        private TowerTileDefinition[] ResolveElementTiles()
        {
            if (elementTiles != null && elementTiles.Length > 0)
            {
                return elementTiles;
            }

            var towerRenderer = FindAnyObjectByType<TilemapTowerRenderer>();
            if (towerRenderer != null)
            {
                elementTiles = towerRenderer.TowerTiles;
            }

            return elementTiles;
        }
    }
}
