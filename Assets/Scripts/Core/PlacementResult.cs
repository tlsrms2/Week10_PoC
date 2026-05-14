using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlap.Core
{
    public sealed class PlacementResult
    {
        private static readonly PlacementResult SuccessResult = new PlacementResult(Array.Empty<PlacementIssue>());

        private PlacementResult(IReadOnlyList<PlacementIssue> issues)
        {
            Issues = issues;
        }

        public bool CanPlace => Issues.Count == 0;

        public IReadOnlyList<PlacementIssue> Issues { get; }

        internal static PlacementResult Success()
        {
            return SuccessResult;
        }

        internal static PlacementResult Failure(IReadOnlyList<PlacementIssue> issues)
        {
            return new PlacementResult(Array.AsReadOnly(issues.ToArray()));
        }
    }
}
