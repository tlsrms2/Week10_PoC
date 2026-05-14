using System;

namespace Overlap.Core
{
    public readonly struct TowerCell
    {
        public static TowerCell Empty => new TowerCell(TowerElement.None, 0);

        public TowerCell(TowerElement element, int grade = 1)
        {
            if (element == TowerElement.None)
            {
                if (grade != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(grade), "Empty cells must have grade 0.");
                }
            }
            else if (grade < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(grade), "Tower cells must have grade 1 or higher.");
            }

            Element = element;
            Grade = grade;
        }

        public TowerElement Element { get; }

        public int Grade { get; }

        public bool IsEmpty => Element == TowerElement.None;

        public bool CanMerge(TowerCell incoming)
        {
            return IsEmpty || incoming.IsEmpty || Element == incoming.Element;
        }

        public TowerCell Merge(TowerCell incoming)
        {
            if (!CanMerge(incoming))
            {
                throw new InvalidOperationException($"Cannot merge {incoming.Element} into {Element}.");
            }

            if (IsEmpty)
            {
                return incoming;
            }

            if (incoming.IsEmpty)
            {
                return this;
            }

            return new TowerCell(Element, Grade + incoming.Grade);
        }
    }
}
