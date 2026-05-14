using UnityEngine;

namespace Overlap.Inventory
{
    public class SimpleBlockInventoryUI : MonoBehaviour
    {
        [SerializeField] private BlockInventory inventory;
        [SerializeField] private SimpleBlockSlotUI[] slots;

        private void OnEnable()
        {
            if (inventory != null)
            {
                BindSlotClicks();
                inventory.Changed += RefreshUI;
                RefreshUI();
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Changed -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            if (inventory == null || slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < inventory.Count)
                {
                    var block = inventory.Blocks[i];
                    bool isSelected = (i == inventory.SelectedIndex);
                    slots[i].SetBlock(block.Cells, isSelected);
                }
                else
                {
                    slots[i].SetEmpty();
                }
            }
        }

        private void BindSlotClicks()
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                int slotIndex = i;
                slots[i].SetClickHandler(() => SelectSlot(slotIndex));
            }
        }

        private void SelectSlot(int index)
        {
            if (inventory == null)
            {
                return;
            }

            inventory.TrySelect(index);
        }
    }
}
