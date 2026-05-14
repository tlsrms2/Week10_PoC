using System;
using System.Collections.Generic;
using Overlap.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    public sealed class TilemapBoardReader : MonoBehaviour
    {
        [SerializeField] private Tilemap buildableTilemap = null;

        public Tilemap BuildableTilemap => buildableTilemap;

        public TowerBoard CreateBoard()
        {
            var cells = ReadBuildableCells();
            return new TowerBoard(cells);
        }

        public IReadOnlyList<GridPoint> ReadBuildableCells()
        {
            if (buildableTilemap == null)
            {
                throw new InvalidOperationException($"{nameof(TilemapBoardReader)} requires a buildable tilemap.");
            }

            var cells = new List<GridPoint>();
            foreach (var position in buildableTilemap.cellBounds.allPositionsWithin)
            {
                if (!buildableTilemap.HasTile(position))
                {
                    continue;
                }

                cells.Add(TilemapGridPointMapper.ToGridPoint(position));
            }

            return cells;
        }

        private void OnValidate()
        {
            if (buildableTilemap == null)
            {
                buildableTilemap = GetComponent<Tilemap>();
            }
        }
    }
}
