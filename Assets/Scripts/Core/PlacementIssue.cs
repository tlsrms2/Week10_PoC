namespace Overlap.Core
{
    public readonly struct PlacementIssue
    {
        public PlacementIssue(
            GridPoint position,
            PlacementIssueType type,
            TowerElement existingElement,
            TowerElement incomingElement)
        {
            Position = position;
            Type = type;
            ExistingElement = existingElement;
            IncomingElement = incomingElement;
        }

        public GridPoint Position { get; }

        public PlacementIssueType Type { get; }

        public TowerElement ExistingElement { get; }

        public TowerElement IncomingElement { get; }

        public override string ToString()
        {
            return $"{Type} at {Position}: {ExistingElement} <- {IncomingElement}";
        }
    }
}
