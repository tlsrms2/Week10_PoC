using System;

namespace Overlap.Core
{
    public readonly struct BlockCell
    {
        public BlockCell(GridPoint offset, TowerElement element)
            : this(offset, new TowerCell(element))
        {
        }

        public BlockCell(GridPoint offset, TowerCell cell)
        {
            if (cell.IsEmpty)
            {
                throw new ArgumentException("Block cells must contain a tower.", nameof(cell));
            }

            Offset = offset;
            Cell = cell;
        }

        public GridPoint Offset { get; }

        public TowerCell Cell { get; }

        public TowerElement Element => Cell.Element;

        public int Grade => Cell.Grade;

        public BlockCell RotateClockwise()
        {
            return new BlockCell(Offset.RotateClockwise(), Cell);
        }
    }
}
