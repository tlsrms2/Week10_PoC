using System;
using System.Collections.Generic;
using Overlap.Core;
using UnityEngine;

namespace Overlap.Blocks
{
    public static class TowerBlockShapeGenerator
    {
        private static readonly TowerElement[] ElementPool =
        {
            TowerElement.Fire,
            TowerElement.Ice,
            TowerElement.Rock,
            TowerElement.Wind,
            TowerElement.Poison
        };

        public static BlockCellDefinition[] GenerateCells(int gridSize, int cellCount, TowerElement guaranteedElement)
        {
            gridSize = Mathf.Max(1, gridSize);
            cellCount = Mathf.Clamp(cellCount, 1, gridSize * gridSize);

            var selectedOffsets = GenerateConnectedOffsets(gridSize, cellCount);
            var cells = new BlockCellDefinition[selectedOffsets.Count];
            var guaranteedIndex = UnityEngine.Random.Range(0, selectedOffsets.Count);

            for (var i = 0; i < selectedOffsets.Count; i++)
            {
                var element = i == guaranteedIndex && guaranteedElement != TowerElement.None
                    ? guaranteedElement
                    : RandomElement();

                cells[i] = new BlockCellDefinition
                {
                    offset = selectedOffsets[i],
                    element = element
                };
            }

            return cells;
        }

        private static List<Vector2Int> GenerateConnectedOffsets(int gridSize, int cellCount)
        {
            var min = -gridSize / 2;
            var max = min + gridSize - 1;
            var selected = new List<Vector2Int>(cellCount)
            {
                new Vector2Int(UnityEngine.Random.Range(min, max + 1), UnityEngine.Random.Range(min, max + 1))
            };
            var selectedSet = new HashSet<Vector2Int>(selected);

            while (selected.Count < cellCount)
            {
                var frontier = new List<Vector2Int>();
                foreach (var offset in selected)
                {
                    AddIfAvailable(frontier, selectedSet, new Vector2Int(offset.x + 1, offset.y), min, max);
                    AddIfAvailable(frontier, selectedSet, new Vector2Int(offset.x - 1, offset.y), min, max);
                    AddIfAvailable(frontier, selectedSet, new Vector2Int(offset.x, offset.y + 1), min, max);
                    AddIfAvailable(frontier, selectedSet, new Vector2Int(offset.x, offset.y - 1), min, max);
                }

                if (frontier.Count == 0)
                {
                    throw new InvalidOperationException("Could not generate a connected tower block shape.");
                }

                var next = frontier[UnityEngine.Random.Range(0, frontier.Count)];
                selected.Add(next);
                selectedSet.Add(next);
            }

            return selected;
        }

        private static void AddIfAvailable(
            List<Vector2Int> frontier,
            HashSet<Vector2Int> selected,
            Vector2Int candidate,
            int min,
            int max)
        {
            if (candidate.x < min || candidate.x > max || candidate.y < min || candidate.y > max)
            {
                return;
            }

            if (selected.Contains(candidate) || frontier.Contains(candidate))
            {
                return;
            }

            frontier.Add(candidate);
        }

        private static TowerElement RandomElement()
        {
            return ElementPool[UnityEngine.Random.Range(0, ElementPool.Length)];
        }
    }
}
