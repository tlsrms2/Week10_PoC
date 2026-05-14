using System;
using System.Collections.Generic;
using Overlap.Blocks;
using Overlap.Core;
using UnityEditor;
using UnityEngine;

namespace Overlap.Editor.Blocks
{
    [CustomEditor(typeof(TowerBlockDefinition))]
    public sealed class TowerBlockDefinitionEditor : UnityEditor.Editor
    {
        private const int MinCoordinate = -2;
        private const int MaxCoordinate = 2;
        private const float CellSize = 34f;
        private const float CellGap = 3f;

        private static readonly Color EmptyColor = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color OriginColor = new Color(0.32f, 0.32f, 0.32f);
        private static readonly Color BorderColor = new Color(0.08f, 0.08f, 0.08f);

        private SerializedProperty displayNameProperty;
        private SerializedProperty cellsProperty;
        private TowerElement selectedElement = TowerElement.Fire;

        private void OnEnable()
        {
            displayNameProperty = serializedObject.FindProperty("displayName");
            cellsProperty = serializedObject.FindProperty("cells");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(displayNameProperty);
            EditorGUILayout.Space(8f);

            DrawElementSelector();
            EditorGUILayout.Space(8f);
            DrawGridEditor();
            EditorGUILayout.Space(8f);
            DrawUtilityButtons();
            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(cellsProperty, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawElementSelector()
        {
            EditorGUILayout.LabelField("Paint Element", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawElementButton(TowerElement.Fire);
                DrawElementButton(TowerElement.Ice);
                DrawElementButton(TowerElement.Rock);
                DrawElementButton(TowerElement.Wind);
                DrawElementButton(TowerElement.Poison);
            }
        }

        private void DrawElementButton(TowerElement element)
        {
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = ElementColor(element);

            var isSelected = selectedElement == element;
            var label = isSelected ? $"{element} *" : element.ToString();
            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                selectedElement = element;
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawGridEditor()
        {
            EditorGUILayout.LabelField("Block Shape", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Left click paints the selected element. Right click removes a cell. The highlighted center is offset (0, 0).", MessageType.None);

            var cellByOffset = ReadCells();
            var gridSize = (MaxCoordinate - MinCoordinate + 1) * CellSize + (MaxCoordinate - MinCoordinate) * CellGap;
            var gridRect = GUILayoutUtility.GetRect(gridSize, gridSize, GUILayout.ExpandWidth(false));

            for (var y = MaxCoordinate; y >= MinCoordinate; y--)
            {
                for (var x = MinCoordinate; x <= MaxCoordinate; x++)
                {
                    var offset = new Vector2Int(x, y);
                    var rect = new Rect(
                        gridRect.x + (x - MinCoordinate) * (CellSize + CellGap),
                        gridRect.y + (MaxCoordinate - y) * (CellSize + CellGap),
                        CellSize,
                        CellSize);

                    DrawGridCell(rect, offset, cellByOffset);
                    HandleGridCellInput(rect, offset, cellByOffset);
                }
            }
        }

        private void DrawGridCell(Rect rect, Vector2Int offset, Dictionary<Vector2Int, TowerElement> cellByOffset)
        {
            var hasCell = cellByOffset.TryGetValue(offset, out var element);
            var fillColor = hasCell ? ElementColor(element) : offset == Vector2Int.zero ? OriginColor : EmptyColor;

            EditorGUI.DrawRect(rect, fillColor);
            DrawRectOutline(rect, BorderColor);

            var label = hasCell ? ElementLabel(element) : offset == Vector2Int.zero ? "0" : string.Empty;
            if (!string.IsNullOrEmpty(label))
            {
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                EditorGUI.LabelField(rect, label, style);
            }
        }

        private void HandleGridCellInput(Rect rect, Vector2Int offset, Dictionary<Vector2Int, TowerElement> cellByOffset)
        {
            var currentEvent = Event.current;
            if (!rect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type != EventType.MouseDown)
            {
                return;
            }

            if (currentEvent.button == 0)
            {
                SetCell(offset, selectedElement, cellByOffset);
                currentEvent.Use();
            }
            else if (currentEvent.button == 1)
            {
                RemoveCell(offset, cellByOffset);
                currentEvent.Use();
            }
        }

        private void DrawUtilityButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Normalize To Origin"))
                {
                    NormalizeCellsToOrigin();
                }

                if (GUILayout.Button("Clear"))
                {
                    cellsProperty.ClearArray();
                }
            }
        }

        private Dictionary<Vector2Int, TowerElement> ReadCells()
        {
            var result = new Dictionary<Vector2Int, TowerElement>();

            for (var i = 0; i < cellsProperty.arraySize; i++)
            {
                var cellProperty = cellsProperty.GetArrayElementAtIndex(i);
                var offset = cellProperty.FindPropertyRelative("offset").vector2IntValue;
                var element = (TowerElement)cellProperty.FindPropertyRelative("element").enumValueIndex;

                if (element == TowerElement.None)
                {
                    element = TowerElement.Fire;
                }

                result[offset] = element;
            }

            return result;
        }

        private void SetCell(Vector2Int offset, TowerElement element, Dictionary<Vector2Int, TowerElement> cellByOffset)
        {
            if (cellByOffset.ContainsKey(offset))
            {
                for (var i = 0; i < cellsProperty.arraySize; i++)
                {
                    var cellProperty = cellsProperty.GetArrayElementAtIndex(i);
                    if (cellProperty.FindPropertyRelative("offset").vector2IntValue != offset)
                    {
                        continue;
                    }

                    cellProperty.FindPropertyRelative("element").enumValueIndex = (int)element;
                    return;
                }
            }

            var nextIndex = cellsProperty.arraySize;
            cellsProperty.InsertArrayElementAtIndex(nextIndex);
            var newCell = cellsProperty.GetArrayElementAtIndex(nextIndex);
            newCell.FindPropertyRelative("offset").vector2IntValue = offset;
            newCell.FindPropertyRelative("element").enumValueIndex = (int)element;
        }

        private void RemoveCell(Vector2Int offset, Dictionary<Vector2Int, TowerElement> cellByOffset)
        {
            if (!cellByOffset.ContainsKey(offset))
            {
                return;
            }

            for (var i = cellsProperty.arraySize - 1; i >= 0; i--)
            {
                var cellProperty = cellsProperty.GetArrayElementAtIndex(i);
                if (cellProperty.FindPropertyRelative("offset").vector2IntValue == offset)
                {
                    cellsProperty.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        private void NormalizeCellsToOrigin()
        {
            if (cellsProperty.arraySize == 0)
            {
                return;
            }

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            for (var i = 0; i < cellsProperty.arraySize; i++)
            {
                var offset = cellsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector2IntValue;
                minX = Math.Min(minX, offset.x);
                minY = Math.Min(minY, offset.y);
            }

            var delta = new Vector2Int(minX, minY);
            for (var i = 0; i < cellsProperty.arraySize; i++)
            {
                var offsetProperty = cellsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("offset");
                offsetProperty.vector2IntValue -= delta;
            }
        }

        private static void DrawRectOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static string ElementLabel(TowerElement element)
        {
            switch (element)
            {
                case TowerElement.Fire:
                    return "F";
                case TowerElement.Ice:
                    return "I";
                case TowerElement.Rock:
                    return "R";
                case TowerElement.Wind:
                    return "W";
                case TowerElement.Poison:
                    return "P";
                default:
                    return "?";
            }
        }

        private static Color ElementColor(TowerElement element)
        {
            switch (element)
            {
                case TowerElement.Fire:
                    return new Color(0.86f, 0.25f, 0.18f);
                case TowerElement.Ice:
                    return new Color(0.24f, 0.62f, 0.93f);
                case TowerElement.Rock:
                    return new Color(0.58f, 0.51f, 0.42f);
                case TowerElement.Wind:
                    return new Color(0.26f, 0.72f, 0.48f);
                case TowerElement.Poison:
                    return new Color(0.58f, 0.36f, 0.74f);
                default:
                    return EmptyColor;
            }
        }
    }
}
