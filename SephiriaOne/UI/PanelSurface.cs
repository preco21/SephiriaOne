using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    // A plain Image targetGraphic with one mesh for fill and border. Standard uGUI
    // CanvasRenderer tinting, stencil masks and RectMask2D clipping remain native.
    internal sealed class PanelSurface : Image
    {
        // Dropdown templates are cloned by Unity, so their style must serialize.
        [SerializeField] private float radius = 6f;
        [SerializeField] private float borderWidth = 1f;
        [SerializeField] private Color borderColor = new Color32(88, 112, 140, 150);

        public float Radius
        {
            get => radius;
            set { if (radius.Equals(value)) return; radius = value; SetVerticesDirty(); }
        }

        public float BorderWidth
        {
            get => borderWidth;
            set { if (borderWidth.Equals(value)) return; borderWidth = value; SetVerticesDirty(); }
        }

        public Color BorderColor
        {
            get => borderColor;
            set { if (borderColor.Equals(value)) return; borderColor = value; SetVerticesDirty(); }
        }

        internal static Image Add(GameObject target, Color fill, float radius = 6f, float border = 1f)
        {
            var surface = target.AddComponent<PanelSurface>();
            surface.color = fill;
            surface.radius = radius;
            surface.borderWidth = border;
            return surface;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            var shape = PanelShapeGeometry.Create(rect.x, rect.y, rect.width, rect.height, radius, borderWidth);
            if (!shape.IsValid) return;
            if (shape.BorderWidth <= 0)
            {
                AddFill(vertices, shape, false, color);
                return;
            }

            Color edge = borderColor;
            edge.a *= color.a;
            if (!shape.HasFill)
            {
                AddFill(vertices, shape, false, edge);
                return;
            }

            AddFill(vertices, shape, true, color);
            int first = vertices.currentVertCount;
            for (int i = 0; i < PanelShapeGeometry.PointCount; i++)
            {
                shape.GetPoint(i, false, out float x, out float y);
                AddVertex(vertices, x, y, edge);
                shape.GetPoint(i, true, out x, out y);
                AddVertex(vertices, x, y, edge);
            }
            for (int i = 0; i < PanelShapeGeometry.PointCount; i++)
            {
                int current = first + i * 2;
                int next = first + ((i + 1) % PanelShapeGeometry.PointCount) * 2;
                vertices.AddTriangle(current, next, current + 1);
                vertices.AddTriangle(next, next + 1, current + 1);
            }
        }

        private static void AddFill(VertexHelper vertices, PanelShapeGeometry shape, bool inner, Color32 tint)
        {
            int center = vertices.currentVertCount;
            AddVertex(vertices, shape.X + shape.Width * 0.5f, shape.Y + shape.Height * 0.5f, tint);
            for (int i = 0; i < PanelShapeGeometry.PointCount; i++)
            {
                shape.GetPoint(i, inner, out float x, out float y);
                AddVertex(vertices, x, y, tint);
            }
            for (int i = 0; i < PanelShapeGeometry.PointCount; i++)
                vertices.AddTriangle(center, center + i + 1, center + ((i + 1) % PanelShapeGeometry.PointCount) + 1);
        }

        private static void AddVertex(VertexHelper vertices, float x, float y, Color32 tint)
        {
            vertices.AddVert(new Vector3(x, y, 0), tint, Vector2.zero);
        }
    }
}
