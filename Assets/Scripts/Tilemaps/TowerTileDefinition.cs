using System;
using Overlap.Core;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    [Serializable]
    public sealed class TowerTileDefinition
    {
        public TowerElement element;
        public TileBase tile = null;
    }
}
