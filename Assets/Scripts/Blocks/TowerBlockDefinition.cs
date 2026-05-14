using System;
using System.Collections.Generic;
using System.Linq;
using Overlap.Core;
using UnityEngine;

namespace Overlap.Blocks
{
    [CreateAssetMenu(menuName = "Overlap/Tower Block Definition", fileName = "TowerBlockDefinition")]
    public sealed class TowerBlockDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Tower Block";
        [SerializeField] private BlockCellDefinition[] cells =
        {
            new BlockCellDefinition { offset = new Vector2Int(0, 0), element = TowerElement.Fire }
        };

        public string DisplayName => displayName;

        public IReadOnlyList<BlockCellDefinition> Cells => Array.AsReadOnly(cells);

        public void Initialize(string newDisplayName, BlockCellDefinition[] newCells)
        {
            displayName = newDisplayName;
            cells = newCells;
        }

        public TowerBlock CreateBlock()
        {
            if (cells == null || cells.Length == 0)
            {
                throw new InvalidOperationException($"{name} must contain at least one block cell.");
            }

            return new TowerBlock(cells.Select(cell =>
                new BlockCell(
                    new GridPoint(cell.offset.x, cell.offset.y),
                    cell.element == TowerElement.None ? TowerElement.Fire : cell.element)));
        }
    }
}
