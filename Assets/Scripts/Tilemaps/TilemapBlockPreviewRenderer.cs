using System;
using System.Collections.Generic;
using Overlap.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    [RequireComponent(typeof(TilemapRenderer))]
    public sealed class TilemapBlockPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap previewTilemap = null;
        [SerializeField] private int sortingOrder = 20;
        [SerializeField] private TileBase validTile = null;
        [SerializeField] private TileBase invalidTile = null;
        [SerializeField] private TowerTileDefinition[] elementTiles = Array.Empty<TowerTileDefinition>();

        private readonly Dictionary<TowerElement, TileBase> tileByElement = new Dictionary<TowerElement, TileBase>();

        public void Render(TowerBlock block, GridPoint origin, bool canPlace)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            ApplySortingOrder();
            EnsureTileLookup();
            Clear();

            foreach (var blockCell in block.Cells)
            {
                var position = origin + blockCell.Offset;
                var tile = ResolveTile(blockCell.Element, canPlace);
                if (tile != null)
                {
                    previewTilemap.SetTile(TilemapGridPointMapper.ToCellPosition(position), tile);
                }
            }
        }

        public void Clear()
        {
            if (previewTilemap != null)
            {
                previewTilemap.ClearAllTiles();
            }
        }

        private TileBase ResolveTile(TowerElement element, bool canPlace)
        {
            if (canPlace && tileByElement.TryGetValue(element, out var elementTile) && elementTile != null)
            {
                return elementTile;
            }

            if (canPlace)
            {
                return validTile != null ? validTile : invalidTile;
            }

            return invalidTile;
        }

        private void EnsureTileLookup()
        {
            if (previewTilemap == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapBlockPreviewRenderer)} requires a preview tilemap.");
            }

            tileByElement.Clear();
            foreach (var definition in elementTiles)
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
            if (previewTilemap == null)
            {
                previewTilemap = GetComponent<Tilemap>();
            }

            ApplySortingOrder();
        }
    }
}
