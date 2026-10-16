using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // UI geometry keeps the wheel crisp without textures or per-frame mesh allocation.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadialSector : MaskableGraphic
    {
        public float Angle, Sweep = 90, InnerRadius = 142, OuterRadius = 290;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); int steps = Mathf.CeilToInt(Sweep / 5);
            for (int i = 0; i <= steps; i++)
            {
                float angle = (Angle - Sweep / 2 + Sweep * i / steps) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(direction * InnerRadius, color, Vector2.zero);
                vh.AddVert(direction * OuterRadius, color, Vector2.zero);
                if (i == 0) continue;
                int n = i * 2; vh.AddTriangle(n - 2, n - 1, n); vh.AddTriangle(n, n - 1, n + 1);
            }
        }
    }
}
