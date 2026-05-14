using System.Collections.Generic;
using NUnit.Framework;
using Overlap.Core;

namespace Overlap.Tests.EditMode
{
    public sealed class TowerBoardTests
    {
        [Test]
        public void Place_AllowsEmptyBuildableCells()
        {
            var board = CreateBoard(3, 3);
            var block = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Fire),
                new BlockCell(new GridPoint(1, 0), TowerElement.Ice)
            });

            var result = board.Place(block, new GridPoint(1, 1));

            Assert.That(result.CanPlace, Is.True);
            Assert.That(board.GetCell(new GridPoint(1, 1)).Element, Is.EqualTo(TowerElement.Fire));
            Assert.That(board.GetCell(new GridPoint(2, 1)).Element, Is.EqualTo(TowerElement.Ice));
        }

        [Test]
        public void Place_MergesSameElementAndAddsGrade()
        {
            var board = CreateBoard(2, 2);
            var fire = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Fire)
            });

            board.Place(fire, new GridPoint(0, 0));
            var result = board.Place(fire, new GridPoint(0, 0));

            Assert.That(result.CanPlace, Is.True);
            Assert.That(board.GetCell(new GridPoint(0, 0)).Grade, Is.EqualTo(2));
        }

        [Test]
        public void Place_RejectsDifferentElementAndLeavesBoardUnchanged()
        {
            var board = CreateBoard(2, 2);
            var fire = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Fire)
            });
            var ice = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Ice)
            });

            board.Place(fire, new GridPoint(0, 0));
            var result = board.Place(ice, new GridPoint(0, 0));

            Assert.That(result.CanPlace, Is.False);
            Assert.That(result.Issues[0].Type, Is.EqualTo(PlacementIssueType.ElementMismatch));
            Assert.That(board.GetCell(new GridPoint(0, 0)).Element, Is.EqualTo(TowerElement.Fire));
            Assert.That(board.GetCell(new GridPoint(0, 0)).Grade, Is.EqualTo(1));
        }

        [Test]
        public void Place_RejectsCellsOutsideBuildableArea()
        {
            var board = CreateBoard(1, 1);
            var block = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Rock),
                new BlockCell(new GridPoint(1, 0), TowerElement.Rock)
            });

            var result = board.Place(block, new GridPoint(0, 0));

            Assert.That(result.CanPlace, Is.False);
            Assert.That(result.Issues[0].Type, Is.EqualTo(PlacementIssueType.OutOfBuildableArea));
            Assert.That(board.OccupiedCellCount, Is.EqualTo(0));
        }

        [Test]
        public void RotateClockwise_RotatesOffsetsWithoutFlipping()
        {
            var block = new TowerBlock(new[]
            {
                new BlockCell(new GridPoint(0, 0), TowerElement.Wind),
                new BlockCell(new GridPoint(0, 1), TowerElement.Wind)
            });

            var rotated = block.RotateClockwise();

            Assert.That(rotated.Cells[0].Offset, Is.EqualTo(new GridPoint(0, 0)));
            Assert.That(rotated.Cells[1].Offset, Is.EqualTo(new GridPoint(1, 0)));
        }

        private static TowerBoard CreateBoard(int width, int height)
        {
            var cells = new List<GridPoint>();
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells.Add(new GridPoint(x, y));
                }
            }

            return new TowerBoard(cells);
        }
    }
}
