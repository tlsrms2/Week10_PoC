using System;
using System.Collections.Generic;
using Overlap.Blocks;
using UnityEngine;

namespace Overlap.Inventory
{
    public sealed class BlockInventory : MonoBehaviour
    {
        [SerializeField] private int capacity = 5;
        [SerializeField] private TowerBlockDefinition[] startingBlocks = Array.Empty<TowerBlockDefinition>();
        [SerializeField] private int selectedIndex;

        private readonly List<TowerBlockDefinition> blocks = new List<TowerBlockDefinition>();

        public event Action Changed;

        public int Capacity => capacity;

        public int Count => blocks.Count;

        public int SelectedIndex => selectedIndex;

        public TowerBlockDefinition SelectedBlock =>
            selectedIndex >= 0 && selectedIndex < blocks.Count ? blocks[selectedIndex] : null;

        public IReadOnlyList<TowerBlockDefinition> Blocks => blocks;

        private void Awake()
        {
            RebuildFromStartingBlocks();
        }

        [ContextMenu("Rebuild From Starting Blocks")]
        public void RebuildFromStartingBlocks()
        {
            blocks.Clear();

            foreach (var block in startingBlocks)
            {
                if (block == null || blocks.Count >= capacity)
                {
                    continue;
                }

                blocks.Add(block);
            }

            ClampSelectedIndex();
            Changed?.Invoke();
        }

        public bool TryAdd(TowerBlockDefinition block)
        {
            if (block == null || blocks.Count >= capacity)
            {
                return false;
            }

            blocks.Add(block);
            ClampSelectedIndex();
            Changed?.Invoke();
            return true;
        }

        public bool TrySelect(int index)
        {
            if (index < 0 || index >= blocks.Count)
            {
                return false;
            }

            selectedIndex = index;
            Changed?.Invoke();
            return true;
        }

        public bool TryConsumeSelected()
        {
            if (SelectedBlock == null)
            {
                return false;
            }

            blocks.RemoveAt(selectedIndex);
            ClampSelectedIndex();
            Changed?.Invoke();
            return true;
        }

        private void ClampSelectedIndex()
        {
            if (blocks.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, blocks.Count - 1);
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(1, capacity);
            selectedIndex = Mathf.Max(0, selectedIndex);
        }
    }
}
