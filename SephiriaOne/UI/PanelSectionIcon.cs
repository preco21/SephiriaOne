using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    internal enum PanelSectionSymbol { Stats, Costumes, Fun, Pvp, Growth, Optimization, System, Up, Down }

    // Font-independent monochrome icons, rebuilt only when uGUI dirties this graphic.
    internal sealed class PanelSectionIcon : MaskableGraphic
    {
        [SerializeField] private PanelSectionSymbol symbol;
        private Vector2 center;
        private float extent;
        private Color32 tint;

        internal void Configure(PanelSectionSymbol value, Color ink)
        {
            symbol = value;
            color = ink;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            extent = Mathf.Min(rect.width, rect.height) * 0.46f;
            if (extent <= 0 || float.IsNaN(extent) || float.IsInfinity(extent)) return;
            center = rect.center;
            tint = color;
            switch (symbol)
            {
                case PanelSectionSymbol.Stats:
                    Box(mesh, -.8f, -.75f, -.4f, .05f);
                    Box(mesh, -.2f, -.75f, .2f, .75f);
                    Box(mesh, .4f, -.75f, .8f, .4f);
                    break;
                case PanelSectionSymbol.Costumes:
                    Ellipse(mesh, -.31f, .35f, .2f, .6f);
                    Ellipse(mesh, .31f, .35f, .2f, .6f);
                    Ellipse(mesh, 0, -.4f, .66f, .5f);
                    break;
                case PanelSectionSymbol.Fun:
                    Line(mesh, -.75f, -.75f, .75f, -.75f, .16f);
                    Line(mesh, .75f, -.75f, .75f, .75f, .16f);
                    Line(mesh, .75f, .75f, -.75f, .75f, .16f);
                    Line(mesh, -.75f, .75f, -.75f, -.75f, .16f);
                    Ellipse(mesh, -.37f, -.37f, .14f, .14f);
                    Ellipse(mesh, .37f, -.37f, .14f, .14f);
                    Ellipse(mesh, 0, 0, .14f, .14f);
                    Ellipse(mesh, -.37f, .37f, .14f, .14f);
                    Ellipse(mesh, .37f, .37f, .14f, .14f);
                    break;
                case PanelSectionSymbol.Pvp:
                    Sword(mesh, 1);
                    Sword(mesh, -1);
                    break;
                case PanelSectionSymbol.Growth:
                    Line(mesh, 0, -.85f, 0, .3f, .16f);
                    Quad(mesh, new Vector2(-.03f, -.05f), new Vector2(-.66f, .14f),
                        new Vector2(-.8f, .64f), new Vector2(-.26f, .49f));
                    Quad(mesh, new Vector2(.03f, .2f), new Vector2(.22f, .68f),
                        new Vector2(.8f, .87f), new Vector2(.64f, .36f));
                    Line(mesh, -.45f, -.85f, .45f, -.85f, .14f);
                    break;
                case PanelSectionSymbol.Optimization:
                    Triangle(mesh, new Vector2(.22f, .92f), new Vector2(-.66f, -.12f), new Vector2(.04f, -.12f));
                    Triangle(mesh, new Vector2(-.22f, -.92f), new Vector2(.66f, .12f), new Vector2(-.04f, .12f));
                    break;
                case PanelSectionSymbol.System:
                    Line(mesh, -.82f, .55f, .82f, .55f, .13f);
                    Line(mesh, -.82f, 0, .82f, 0, .13f);
                    Line(mesh, -.82f, -.55f, .82f, -.55f, .13f);
                    Box(mesh, -.45f, .3f, -.15f, .8f);
                    Box(mesh, .2f, -.25f, .5f, .25f);
                    Box(mesh, -.6f, -.8f, -.3f, -.3f);
                    break;
                case PanelSectionSymbol.Up:
                    Triangle(mesh, new Vector2(-.65f, -.35f), new Vector2(.65f, -.35f), new Vector2(0, .45f));
                    break;
                case PanelSectionSymbol.Down:
                    Triangle(mesh, new Vector2(-.65f, .35f), new Vector2(0, -.45f), new Vector2(.65f, .35f));
                    break;
            }
        }

        private void Sword(VertexHelper mesh, float direction)
        {
            Line(mesh, -.72f * direction, -.72f, .49f * direction, .49f, .17f);
            Triangle(mesh, new Vector2(.37f * direction, .58f), new Vector2(.82f * direction, .82f),
                new Vector2(.58f * direction, .37f));
            Line(mesh, -.68f * direction, -.26f, -.26f * direction, -.68f, .15f);
        }

        private void Box(VertexHelper mesh, float left, float bottom, float right, float top)
        {
            Quad(mesh, new Vector2(left, bottom), new Vector2(right, bottom),
                new Vector2(right, top), new Vector2(left, top));
        }

        private void Line(VertexHelper mesh, float x0, float y0, float x1, float y1, float width)
        {
            var start = new Vector2(x0, y0);
            var end = new Vector2(x1, y1);
            Vector2 offset = new Vector2(-(y1 - y0), x1 - x0).normalized * (width * .5f);
            Quad(mesh, start - offset, end - offset, end + offset, start + offset);
        }

        private void Ellipse(VertexHelper mesh, float x, float y, float rx, float ry)
        {
            const int segments = 12;
            int first = mesh.currentVertCount;
            Vertex(mesh, new Vector2(x, y));
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2 / segments);
                Vertex(mesh, new Vector2(x + Mathf.Cos(angle) * rx, y + Mathf.Sin(angle) * ry));
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(first, first + i + 1, first + ((i + 1) % segments) + 1);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a); Vertex(mesh, b); Vertex(mesh, c);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a); Vertex(mesh, b); Vertex(mesh, c); Vertex(mesh, d);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Vertex(VertexHelper mesh, Vector2 point)
        {
            mesh.AddVert(new Vector3(center.x + point.x * extent, center.y + point.y * extent, 0), tint, Vector2.zero);
        }
    }
}
