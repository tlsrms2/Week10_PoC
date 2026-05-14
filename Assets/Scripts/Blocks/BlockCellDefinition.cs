using System;
using Overlap.Core;
using UnityEngine;

namespace Overlap.Blocks
{
    [Serializable]
    public struct BlockCellDefinition
    {
        public Vector2Int offset;
        public TowerElement element;
    }
}
