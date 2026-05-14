using System;
using System.Collections.Generic;
using Overlap.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    [RequireComponent(typeof(TilemapRenderer))]
    public sealed class TilemapTowerRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap towerTilemap = null;
        [SerializeField] private TowerGradeOverlayRenderer gradeOverlayRenderer = null;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField] private TileBase fallbackTile = null;
        [SerializeField] private TowerTileDefinition[] towerTiles = Array.Empty<TowerTileDefinition>();

        private readonly Dictionary<TowerElement, TileBase> tileByElement = new Dictionary<TowerElement, TileBase>();

        public TowerTileDefinition[] TowerTiles => towerTiles;

        public void Render(TowerBoard board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            ApplySortingOrder();
            EnsureTileLookup();

            towerTilemap.ClearAllTiles();
            foreach (var placedCell in board.CreatePlacedCellSnapshot())
            {
                var tile = ResolveTile(placedCell.Value.Element);
                if (tile == null)
                {
                    continue;
                }

                towerTilemap.SetTile(TilemapGridPointMapper.ToCellPosition(placedCell.Key), tile);
            }

            gradeOverlayRenderer?.Render(board);
        }

        private TileBase ResolveTile(TowerElement element)
        {
            if (tileByElement.TryGetValue(element, out var tile) && tile != null)
            {
                return tile;
            }

            return fallbackTile;
        }

        public void Clear()
        {
            if (towerTilemap != null)
            {
                towerTilemap.ClearAllTiles();
            }

            gradeOverlayRenderer?.Clear();
        }

        private void EnsureTileLookup()
        {
            if (towerTilemap == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapTowerRenderer)} requires a tower tilemap.");
            }

            tileByElement.Clear();
            foreach (var definition in towerTiles)
            {
                if (definition == null || definition.element == TowerElement.None)
                {
                    continue;
                }

                tileByElement[definition.element] = definition.tile;
            }
        }

        private void ApplySortingOrder()
        {
            var tilemapRenderer = GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
            {
                tilemapRenderer.sortingOrder = sortingOrder;
            }
        }

        private void OnValidate()
        {
            if (towerTilemap == null)
            {
                towerTilemap = GetComponent<Tilemap>();
            }

            if (gradeOverlayRenderer == null)
            {
                gradeOverlayRenderer = GetComponent<TowerGradeOverlayRenderer>();
            }

            ApplySortingOrder();
        }
    }
}
