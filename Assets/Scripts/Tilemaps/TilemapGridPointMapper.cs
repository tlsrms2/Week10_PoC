using Overlap.Core;
using UnityEngine;

namespace Overlap.Tilemaps
{
    public static class TilemapGridPointMapper
    {
        public static GridPoint ToGridPoint(Vector3Int cellPosition)
        {
            return new GridPoint(cellPosition.x, cellPosition.y);
        }

        public static Vector3Int ToCellPosition(GridPoint gridPoint)
        {
            return new Vector3Int(gridPoint.X, gridPoint.Y, 0);
        }
    }
}
