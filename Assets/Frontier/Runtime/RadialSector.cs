using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A solid fill with alpha-only antialiasing inside the original sector.
    // Input continues to use ActionWheel's unchanged angle/radius contract.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadialSector : MaskableGraphic
    {
        public float Angle, Sweep = 90, InnerRadius = 142, OuterRadius = 290;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float outer = Mathf.Max(0, OuterRadius), inner = Mathf.Clamp(InnerRadius, 0, outer), width = outer - inner;
            float sweep = Mathf.Clamp(Sweep, 0, 360) * Mathf.Deg2Rad;
            if (width <= .001f || sweep <= .00001f || color.a <= 0) return;
            bool disc = inner <= .001f, closed = Sweep >= 359.999f;
            float feather = Mathf.Min(.7f, width * .07f);
            float[] radii = disc ? new[] { 0, outer - feather, outer }
                : new[] { inner, inner + feather, outer - feather, outer };
            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep * Mathf.Rad2Deg / 3)), rows = radii.Length;
            int columns = closed ? steps + 1 : steps + 3;
            float start = Angle * Mathf.Deg2Rad - sweep * .5f;
            for (int i = 0; i < columns; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    float radius = radii[j];
                    // Inset the first solid vertex by the same distance at each
                    // radius, so each straight edge has a narrow, uniform fade.
                    float inset = Mathf.Min(sweep * .25f, Mathf.Asin(Mathf.Min(1, feather / Mathf.Max(radius, feather))));
                    float angle = closed ? start + sweep * i / steps
                        : i == 0 ? start
                        : i == columns - 1 ? start + sweep
                        : start + inset + (sweep - 2 * inset) * (i - 1) / steps;
                    var point = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                    var tint = color;
                    if (j == rows - 1 || (!disc && j == 0) || (!closed && (i == 0 || i == columns - 1))) tint.a = 0;
                    vh.AddVert(point, tint, Vector2.zero);
                    if (i == 0 || j == 0) continue;
                    int n = i * rows + j;
                    vh.AddTriangle(n - rows - 1, n - rows, n - 1);
                    vh.AddTriangle(n - 1, n - rows, n);
                }
            }
        }
    }
}
