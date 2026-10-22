using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A solid rounded surface. Only the silhouette fades in alpha for edge
    // antialiasing; there are no decorative outlines, shadows or RGB gradients.
    [AddComponentMenu("UI/Little Legion/Menu Surface")]
    public sealed class MenuSurface : Image
    {
        private const int ArcSteps = 12;
        [SerializeField] private float radius = 12;
        [SerializeField] private float edgeSoftness = 1.25f;
        private float state;

        public float Radius { get => radius; set { value = Mathf.Max(0, value); if (radius == value) return; radius = value; SetVerticesDirty(); } }
        public float EdgeSoftness { get => edgeSoftness; set { value = Mathf.Max(0, value); if (edgeSoftness == value) return; edgeSoftness = value; SetVerticesDirty(); } }
        // Pointer feedback changes the whole fill through Image.color.
        public float State { get => state; set => state = Mathf.Clamp01(value); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0 || color.a <= 0) return;

            float half = Mathf.Min(rect.width, rect.height) * .5f;
            float corner = Mathf.Clamp(radius, 0, half);
            float feather = Mathf.Clamp(edgeSoftness, 0, half);
            int count = corner > .001f ? (ArcSteps + 1) * 4 : 4;
            var inside = Inset(rect, feather);
            float innerCorner = Mathf.Max(0, corner - feather);
            Add(mesh, rect.center, color);
            for (int i = 0; i < count; i++)
                Add(mesh, Perimeter(inside, innerCorner, i, count), color);
            for (int i = 0; i < count; i++)
                mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % count);

            if (feather <= 0) return;
            var clear = color; clear.a = 0;
            int outer = mesh.currentVertCount;
            for (int i = 0; i < count; i++)
                Add(mesh, Perimeter(rect, corner, i, count), clear);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(1 + i, outer + i, outer + next);
                mesh.AddTriangle(1 + i, outer + next, 1 + next);
            }
        }

        private static Rect Inset(Rect rect, float amount) => new Rect(rect.xMin + amount, rect.yMin + amount, Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));
        private static Vector2 Perimeter(Rect rect, float radius, int index, int count)
        {
            if (count == 4)
            {
                switch (index)
                {
                    case 0: return new Vector2(rect.xMax, rect.yMax);
                    case 1: return new Vector2(rect.xMin, rect.yMax);
                    case 2: return new Vector2(rect.xMin, rect.yMin);
                    default: return new Vector2(rect.xMax, rect.yMin);
                }
            }
            int corner = index / (ArcSteps + 1), step = index % (ArcSteps + 1);
            var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            float angle = (corner * 90f + step * 90f / ArcSteps) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        private static void Add(VertexHelper mesh, Vector2 point, Color tint) => mesh.AddVert(new Vector3(point.x, point.y, 0), tint, Vector2.zero);
    }
}
