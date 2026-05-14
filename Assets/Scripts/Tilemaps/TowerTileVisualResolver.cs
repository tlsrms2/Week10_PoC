using Overlap.Core;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Overlap.Tilemaps
{
    public static class TowerTileVisualResolver
    {
        public static void ApplyToImage(
            Image image,
            TowerElement element,
            TowerTileDefinition[] definitions,
            Color fallbackColor)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.color = fallbackColor;

            var tile = ResolveTile(element, definitions);
            if (tile is Tile unityTile && unityTile.sprite != null)
            {
                image.sprite = unityTile.sprite;
                image.color = unityTile.color;
                image.preserveAspect = true;
            }
        }

        public static TileBase ResolveTile(TowerElement element, TowerTileDefinition[] definitions)
        {
            if (definitions == null)
            {
                return null;
            }

            foreach (var definition in definitions)
            {
                if (definition != null && definition.element == element)
                {
                    return definition.tile;
                }
            }

            return null;
        }
    }
}
