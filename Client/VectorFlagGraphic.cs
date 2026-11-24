using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HKMP.Rounds.Client
{
    internal sealed class VectorFlagGraphic : MaskableGraphic
    {
        internal enum FlagKind
        {
            Russia,
            UnitedKingdom
        }

        public FlagKind Kind;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (Kind == FlagKind.Russia)
            {
                AddRectangle(vertexHelper, rect, 0f, 2f / 3f, 1f, 1f, Color.white);
                AddRectangle(vertexHelper, rect, 0f, 1f / 3f, 1f, 2f / 3f, new Color32(0, 57, 166, 255));
                AddRectangle(vertexHelper, rect, 0f, 0f, 1f, 1f / 3f, new Color32(213, 43, 30, 255));
                return;
            }

            Color navy = new Color32(1, 33, 105, 255);
            Color red = new Color32(200, 16, 46, 255);
            AddRectangle(vertexHelper, rect, 0f, 0f, 1f, 1f, navy);

            AddPolygon(vertexHelper, rect, new List<Vector2>
            {
                new Vector2(-0.08f, 0f),
                new Vector2(0f, -0.08f),
                new Vector2(1.08f, 1f),
                new Vector2(1f, 1.08f)
            }, Color.white);
            AddPolygon(vertexHelper, rect, new List<Vector2>
            {
                new Vector2(-0.08f, 1f),
                new Vector2(0f, 1.08f),
                new Vector2(1.08f, 0f),
                new Vector2(1f, -0.08f)
            }, Color.white);

            AddPolygon(vertexHelper, rect, new List<Vector2>
            {
                new Vector2(-0.035f, 0f),
                new Vector2(0f, -0.035f),
                new Vector2(1.035f, 1f),
                new Vector2(1f, 1.035f)
            }, red);
            AddPolygon(vertexHelper, rect, new List<Vector2>
            {
                new Vector2(-0.035f, 1f),
                new Vector2(0f, 1.035f),
                new Vector2(1.035f, 0f),
                new Vector2(1f, -0.035f)
            }, red);

            AddRectangle(vertexHelper, rect, 0f, 0.31f, 1f, 0.69f, Color.white);
            AddRectangle(vertexHelper, rect, 0.36f, 0f, 0.64f, 1f, Color.white);
            AddRectangle(vertexHelper, rect, 0f, 0.405f, 1f, 0.595f, red);
            AddRectangle(vertexHelper, rect, 0.435f, 0f, 0.565f, 1f, red);
        }

        private static void AddRectangle(
            VertexHelper helper,
            Rect rect,
            float xMin,
            float yMin,
            float xMax,
            float yMax,
            Color color)
        {
            AddPolygon(
                helper,
                rect,
                new List<Vector2>
                {
                    new Vector2(xMin, yMin),
                    new Vector2(xMax, yMin),
                    new Vector2(xMax, yMax),
                    new Vector2(xMin, yMax)
                },
                color);
        }

        private static void AddPolygon(
            VertexHelper helper,
            Rect rect,
            List<Vector2> points,
            Color color,
            bool alreadyClipped = false)
        {
            List<Vector2> polygon = alreadyClipped ? points : ClipToUnitRect(points);
            if (polygon == null || polygon.Count < 3)
            {
                return;
            }

            int startIndex = helper.currentVertCount;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(
                    Mathf.Lerp(rect.xMin, rect.xMax, point.x),
                    Mathf.Lerp(rect.yMin, rect.yMax, point.y),
                    0f);
                vertex.color = color;
                helper.AddVert(vertex);
            }

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                helper.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
            }
        }

        private static List<Vector2> ClipToUnitRect(List<Vector2> source)
        {
            List<Vector2> result = source;
            result = Clip(result, 0, 0f, true);
            result = Clip(result, 0, 1f, false);
            result = Clip(result, 1, 0f, true);
            result = Clip(result, 1, 1f, false);
            return result;
        }

        private static List<Vector2> Clip(
            List<Vector2> input,
            int axis,
            float boundary,
            bool keepGreater)
        {
            List<Vector2> output = new List<Vector2>();
            if (input == null || input.Count == 0)
            {
                return output;
            }

            Vector2 previous = input[input.Count - 1];
            bool previousInside = IsInside(previous, axis, boundary, keepGreater);

            for (int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                bool currentInside = IsInside(current, axis, boundary, keepGreater);

                if (currentInside != previousInside)
                {
                    output.Add(Intersection(previous, current, axis, boundary));
                }

                if (currentInside)
                {
                    output.Add(current);
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static bool IsInside(
            Vector2 point,
            int axis,
            float boundary,
            bool keepGreater)
        {
            float value = axis == 0 ? point.x : point.y;
            return keepGreater ? value >= boundary : value <= boundary;
        }

        private static Vector2 Intersection(
            Vector2 first,
            Vector2 second,
            int axis,
            float boundary)
        {
            float firstValue = axis == 0 ? first.x : first.y;
            float secondValue = axis == 0 ? second.x : second.y;
            float denominator = secondValue - firstValue;
            float t = Mathf.Abs(denominator) < 0.000001f
                ? 0f
                : (boundary - firstValue) / denominator;

            return new Vector2(
                Mathf.Lerp(first.x, second.x, t),
                Mathf.Lerp(first.y, second.y, t));
        }
    }
}
