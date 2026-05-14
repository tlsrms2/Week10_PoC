using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Overlap.Core
{
    public sealed class TowerBoard
    {
        private readonly HashSet<GridPoint> buildableCells;
        private readonly Dictionary<GridPoint, TowerCell> placedCells = new Dictionary<GridPoint, TowerCell>();

        public TowerBoard(IEnumerable<GridPoint> buildableCells)
        {
            if (buildableCells == null)
            {
                throw new ArgumentNullException(nameof(buildableCells));
            }

            this.buildableCells = new HashSet<GridPoint>(buildableCells);
            if (this.buildableCells.Count == 0)
            {
                throw new ArgumentException("A board must contain at least one buildable cell.", nameof(buildableCells));
            }
        }

        public int OccupiedCellCount => placedCells.Count;

        public bool IsBuildable(GridPoint position)
        {
            return buildableCells.Contains(position);
        }

        public TowerCell GetCell(GridPoint position)
        {
            return placedCells.TryGetValue(position, out var cell) ? cell : TowerCell.Empty;
        }

        public PlacementResult CanPlace(TowerBlock block, GridPoint origin)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            var issues = new List<PlacementIssue>();
            foreach (var blockCell in block.Cells)
            {
                var position = origin + blockCell.Offset;
                if (!buildableCells.Contains(position))
                {
                    issues.Add(new PlacementIssue(
                        position,
                        PlacementIssueType.OutOfBuildableArea,
                        TowerElement.None,
                        blockCell.Element));
                    continue;
                }

                var existingCell = GetCell(position);
                if (!existingCell.CanMerge(blockCell.Cell))
                {
                    issues.Add(new PlacementIssue(
                        position,
                        PlacementIssueType.ElementMismatch,
                        existingCell.Element,
                        blockCell.Element));
                }
            }

            return issues.Count == 0 ? PlacementResult.Success() : PlacementResult.Failure(issues);
        }

        public PlacementResult Place(TowerBlock block, GridPoint origin)
        {
            var result = CanPlace(block, origin);
            if (!result.CanPlace)
            {
                return result;
            }

            foreach (var blockCell in block.Cells)
            {
                var position = origin + blockCell.Offset;
                placedCells[position] = GetCell(position).Merge(blockCell.Cell);
            }

            return result;
        }

        public void ClearAll()
        {
            placedCells.Clear();
        }

        public void ClearArea(GridPoint center, int radius)
        {
            var pointsToRemove = new List<GridPoint>();
            foreach (var kvp in placedCells)
            {
                int dx = Math.Abs(kvp.Key.X - center.X);
                int dy = Math.Abs(kvp.Key.Y - center.Y);
                if (Math.Max(dx, dy) <= radius) // Chebyshev distance
                {
                    pointsToRemove.Add(kvp.Key);
                }
            }

            foreach (var p in pointsToRemove)
            {
                placedCells.Remove(p);
            }
        }

        public IReadOnlyCollection<GridPoint> CreateBuildableCellSnapshot()
        {
            return Array.AsReadOnly(new List<GridPoint>(buildableCells).ToArray());
        }

        public IReadOnlyDictionary<GridPoint, TowerCell> CreatePlacedCellSnapshot()
        {
            return new ReadOnlyDictionary<GridPoint, TowerCell>(
                new Dictionary<GridPoint, TowerCell>(placedCells));
        }
    }
}
