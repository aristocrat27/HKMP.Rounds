using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HKMP.Rounds.Client
{
    internal sealed class VectorStrengthGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color outer = new Color32(238, 173, 72, 255);
            Color inner = new Color32(191, 48, 42, 255);
            Color highlight = new Color32(255, 225, 153, 255);
            Vector2 center = new Vector2(0.5f, 0.49f);
            AddDisc(helper, rect, center, 0.39f, outer, 20);
            AddDisc(helper, rect, center, 0.29f, inner, 20);

            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 2f * i / 8f - Mathf.PI / 2f;
                float nextAngle = angle + Mathf.PI / 8f;
                Vector2 tip = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.48f;
                Vector2 left = center + new Vector2(Mathf.Cos(angle - 0.19f), Mathf.Sin(angle - 0.19f)) * 0.26f;
                Vector2 right = center + new Vector2(Mathf.Cos(angle + 0.19f), Mathf.Sin(angle + 0.19f)) * 0.26f;
                AddPolygon(helper, rect, new[] { left, tip, right, center }, outer);
            }

            AddPolygon(helper, rect, new[]
            {
                new Vector2(0.5f, 0.69f),
                new Vector2(0.62f, 0.5f),
                new Vector2(0.5f, 0.29f),
                new Vector2(0.38f, 0.5f)
            }, highlight);
            AddDisc(helper, rect, center, 0.09f, inner, 16);
        }

        private static void AddDisc(VertexHelper helper, Rect rect, Vector2 center, float radius, Color color, int segments)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, rect, center, color);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                AddVertex(helper, rect, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
            }
            for (int i = 0; i < segments; i++)
            {
                helper.AddTriangle(start, start + i + 1, start + i + 2);
            }
        }

        private static void AddPolygon(VertexHelper helper, Rect rect, Vector2[] points, Color color)
        {
            int start = helper.currentVertCount;
            for (int i = 0; i < points.Length; i++)
            {
                AddVertex(helper, rect, points[i], color);
            }
            for (int i = 1; i < points.Length - 1; i++)
            {
                helper.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void AddVertex(VertexHelper helper, Rect rect, Vector2 normalized, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalized.y),
                0f);
            vertex.color = color;
            helper.AddVert(vertex);
        }
    }
}
