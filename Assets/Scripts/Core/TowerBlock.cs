using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlap.Core
{
    public sealed class TowerBlock
    {
        private readonly BlockCell[] cells;
        private readonly IReadOnlyList<BlockCell> cellView;

        public TowerBlock(IEnumerable<BlockCell> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            var cellArray = cells.ToArray();
            if (cellArray.Length == 0)
            {
                throw new ArgumentException("A block must contain at least one cell.", nameof(cells));
            }

            var occupiedOffsets = new HashSet<GridPoint>();
            foreach (var cell in cellArray)
            {
                if (!occupiedOffsets.Add(cell.Offset))
                {
                    throw new ArgumentException($"Duplicate block offset: {cell.Offset}.", nameof(cells));
                }
            }

            this.cells = cellArray;
            cellView = Array.AsReadOnly(this.cells);
        }

        public IReadOnlyList<BlockCell> Cells => cellView;

        public int CellCount => cells.Length;

        public TowerBlock RotateClockwise(int quarterTurns = 1)
        {
            var turns = ((quarterTurns % 4) + 4) % 4;
            var rotatedCells = cells;

            for (var i = 0; i < turns; i++)
            {
                rotatedCells = rotatedCells.Select(cell => cell.RotateClockwise()).ToArray();
            }

            return new TowerBlock(rotatedCells);
        }
    }
}
