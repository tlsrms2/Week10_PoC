using System;
using System.Collections.Generic;
using Overlap.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Overlap.Tilemaps
{
    public sealed class TowerGradeOverlayRenderer : MonoBehaviour
    {
        [SerializeField] private Tilemap targetTilemap = null;
        [SerializeField] private int sortingOrder = 30;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color highGradeTextColor = new Color(1f, 0.86f, 0.25f);
        [SerializeField] private float fontSize = 0.28f;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0f, -0.1f);
        [SerializeField] private bool hideGradeOne = false;

        private readonly Dictionary<GridPoint, TextMesh> labelByPosition = new Dictionary<GridPoint, TextMesh>();

        public void Render(TowerBoard board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (targetTilemap == null)
            {
                throw new InvalidOperationException($"{nameof(TowerGradeOverlayRenderer)} requires a target tilemap.");
            }

            Clear();

            foreach (var placedCell in board.CreatePlacedCellSnapshot())
            {
                if (hideGradeOne && placedCell.Value.Grade <= 1)
                {
                    continue;
                }

                var label = CreateLabel(placedCell.Key, placedCell.Value.Grade);
                labelByPosition[placedCell.Key] = label;
            }
        }

        public void Clear()
        {
            foreach (var label in labelByPosition.Values)
            {
                if (label != null)
                {
                    Destroy(label.gameObject);
                }
            }

            labelByPosition.Clear();
        }

        private TextMesh CreateLabel(GridPoint position, int grade)
        {
            var gameObject = new GameObject($"Grade {position.X},{position.Y}");
            gameObject.transform.SetParent(transform, false);
            gameObject.transform.position = targetTilemap.GetCellCenterWorld(TilemapGridPointMapper.ToCellPosition(position)) + localOffset;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sortingOrder = sortingOrder;

            var label = gameObject.AddComponent<TextMesh>();
            label.text = grade.ToString();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = fontSize;
            label.fontSize = 32;
            label.color = grade > 1 ? highGradeTextColor : textColor;

            return label;
        }

        private void OnValidate()
        {
            if (targetTilemap == null)
            {
                targetTilemap = GetComponent<Tilemap>();
            }
        }
    }
}
