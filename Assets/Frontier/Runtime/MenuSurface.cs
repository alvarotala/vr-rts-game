using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // All layers stay inside the hit rect. The frame, bevel and soft contact
    // shadow share one UI mesh/material, including when the panel is masked.
    [AddComponentMenu("UI/Little Legion/Menu Surface")]
    public sealed class MenuSurface : Image
    {
        public enum SurfaceStyle { Card, Panel, Button, Inset, Flat }
        private const int ArcSteps = 12;
        private const int FillColumns = 8;
        [SerializeField] private SurfaceStyle style;
        [SerializeField] private float radius = 12;
        [SerializeField] private float borderWidth = 1.2f;
        [SerializeField] private Color borderColor = MenuPalette.Border;
        [SerializeField, Range(0, 1)] private float bottomShade = .9f;
        [SerializeField] private float edgeSoftness = .75f;
        [SerializeField, Range(0, 1)] private float state;
        [SerializeField] private Color focusColor=MenuPalette.Accent;

        public SurfaceStyle Style { get => style; set { if (style == value) return; style = value; SetVerticesDirty(); } }
        public float Radius { get => radius; set { value = Mathf.Max(0, value); if (radius == value) return; radius = value; SetVerticesDirty(); } }
        public float BorderWidth { get => borderWidth; set { value = Mathf.Max(0, value); if (borderWidth == value) return; borderWidth = value; SetVerticesDirty(); } }
        public Color BorderColor { get => borderColor; set { if (borderColor == value) return; borderColor = value; SetVerticesDirty(); } }
        public float BottomShade { get => bottomShade; set { value = Mathf.Clamp01(value); if (bottomShade == value) return; bottomShade = value; SetVerticesDirty(); } }
        public float EdgeSoftness { get => edgeSoftness; set { value = Mathf.Max(0, value); if (edgeSoftness == value) return; edgeSoftness = value; SetVerticesDirty(); } }
        public float State { get => state; set { value = Mathf.Clamp01(value); if (state == value) return; state = value; SetVerticesDirty(); } }
        public Color FocusColor { get => focusColor; set { if(focusColor==value)return;focusColor=value;SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0 || color.a <= 0) return;

            float half = Mathf.Min(rect.width, rect.height) * .5f;
            float corner = Mathf.Clamp(radius, 0, half);
            float feather = Mathf.Clamp(edgeSoftness, 0, half);
            float border = Mathf.Clamp(borderWidth, 0, Mathf.Max(0, half - feather));
            bool flat = style == SurfaceStyle.Flat || border <= .001f || half < 7;
            int count = corner > .001f ? (ArcSteps + 1) * 4 : 4;
            float faceInset;

            if (flat)
            {
                faceInset = feather;
                if (feather > 0) Join(Ring(0, Layer.Clear), Ring(feather, Layer.Face));
            }
            else if (style == SurfaceStyle.Panel)
            {
                // The exterior dark lip separates the panel from the scene;
                // two blue lines with a recessed channel give the frame depth.
                float frame = Mathf.Min(half, Mathf.Max(6.8f, border * 3));
                int outer = Ring(0, Layer.Clear);
                outer = Band(outer, feather, Layer.Shadow);
                outer = Band(outer, feather + frame * .12f, Layer.DarkEdge);
                outer = Band(outer, feather + frame * .25f, Layer.Rim);
                outer = Band(outer, feather + frame * .42f, Layer.Frame);
                outer = Band(outer, feather + frame * .77f, Layer.Channel);
                outer = Band(outer, feather + frame * .86f, Layer.InnerRim);
                faceInset = Mathf.Min(half, feather + frame);
                Band(outer, faceInset, Layer.Face);
            }
            else
            {
                bool inset = style == SurfaceStyle.Inset;
                float depth = style == SurfaceStyle.Button ? 3.2f : inset ? 2.2f : 1.8f;
                depth = Mathf.Min(half - feather, depth + border * .5f);
                int outer = Ring(0, Layer.Clear);
                outer = Band(outer, feather, Layer.Shadow);
                outer = Band(outer, feather + depth * .35f, inset ? Layer.InsetEdge : Layer.Rim);
                outer = Band(outer, feather + depth * .68f, inset ? Layer.InsetEdge : Layer.Bevel);
                faceInset = Mathf.Min(half, feather + depth);
                Band(outer, faceInset, Layer.Face);
            }

            FillFace(Inset(rect, faceInset), Mathf.Max(0, corner - faceInset));

            int Band(int outer, float distance, Layer layer)
            {
                int inner = Ring(distance, layer);
                Join(outer, inner);
                return inner;
            }
            int Ring(float distance, Layer layer)
            {
                distance = Mathf.Clamp(distance, 0, half);
                int first = mesh.currentVertCount;
                var bounds = Inset(rect, distance);
                float rounded = Mathf.Max(0, corner - distance);
                for (int i = 0; i < count; i++)
                {
                    var point = Perimeter(bounds, rounded, i, count);
                    Add(mesh, point, Tint(rect, point, layer));
                }
                return first;
            }
            void Join(int outer, int inner)
            {
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    mesh.AddTriangle(inner + i, outer + i, outer + next);
                    mesh.AddTriangle(inner + i, outer + next, inner + next);
                }
            }
            void FillFace(Rect bounds, float rounded)
            {
                int previous = -1;
                if (rounded <= .001f)
                {
                    Row(bounds.yMin, bounds.xMin, bounds.xMax);
                    if (!flat) Middle(bounds.yMin, bounds.yMax);
                    Row(bounds.yMax, bounds.xMin, bounds.xMax);
                }
                else
                {
                    for (int i = 0; i <= ArcSteps; i++)
                    {
                        float angle = i * Mathf.PI * .5f / ArcSteps;
                        float inset = rounded * (1 - Mathf.Sin(angle));
                        Row(bounds.yMin + rounded * (1 - Mathf.Cos(angle)), bounds.xMin + inset, bounds.xMax - inset);
                    }
                    if (!flat) Middle(bounds.yMin + rounded, bounds.yMax - rounded);
                    for (int i = 0; i <= ArcSteps; i++)
                    {
                        float angle = i * Mathf.PI * .5f / ArcSteps;
                        float inset = rounded * (1 - Mathf.Cos(angle));
                        Row(bounds.yMax - rounded * (1 - Mathf.Sin(angle)), bounds.xMin + inset, bounds.xMax - inset);
                    }
                }

                void Middle(float low, float high)
                {
                    for (int i = 1; i < 8; i++) Row(Mathf.Lerp(low, high, i / 8f), bounds.xMin, bounds.xMax);
                }
                void Row(float y, float left, float right)
                {
                    // Horizontal strips avoid the dark diagonal seams produced
                    // by center fans after Unity packs UI color into Color32.
                    int first = mesh.currentVertCount;
                    int columns = flat ? 1 : FillColumns;
                    for (int col = 0; col <= columns; col++)
                    {
                        var point = new Vector2(Mathf.Lerp(left, right, col / (float)columns), y);
                        Add(mesh, point, Tint(rect, point, Layer.Face));
                    }
                    if (previous >= 0)
                        for (int col = 0; col < columns; col++)
                        {
                            mesh.AddTriangle(previous + col, previous + col + 1, first + col);
                            mesh.AddTriangle(first + col, previous + col + 1, first + col + 1);
                        }
                    previous = first;
                }
            }
        }

        private enum Layer { Clear, Shadow, DarkEdge, Rim, Frame, Channel, InnerRim, Bevel, InsetEdge, Face }

        private Color Tint(Rect rect, Vector2 point, Layer layer)
        {
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, point.y);
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, point.x);
            Color tint;
            if (layer == Layer.Face)
            {
                bool flat = style == SurfaceStyle.Flat || borderWidth <= .001f || Mathf.Min(rect.width, rect.height) < 14;
                float low = bottomShade * (style == SurfaceStyle.Button ? .78f : style == SurfaceStyle.Panel ? .86f : .95f);
                float high = style == SurfaceStyle.Button ? 1.1f : style == SurfaceStyle.Panel ? 1.08f : 1.025f;
                float shade = Mathf.Lerp(flat ? bottomShade : low, flat ? 1 : high, y);
                tint = color; tint.r *= shade; tint.g *= shade; tint.b *= shade;
                if (!flat)
                {
                    float glow = Mathf.Sin(x * Mathf.PI) * Mathf.Exp(-Mathf.Pow((y - .82f) * 1.7f, 2));
                    float strength = style == SurfaceStyle.Panel ? 1 : style == SurfaceStyle.Button ? .65f : .25f;
                    tint.r += .004f * glow * strength; tint.g += .01f * glow * strength; tint.b += .014f * glow * strength;
                    tint = Color.Lerp(tint, focusColor, state * .045f);
                    tint.a = color.a;
                }
                return tint;
            }

            switch (layer)
            {
                case Layer.Clear: tint = MenuPalette.Shadow; tint.a = 0; break;
                case Layer.Shadow: tint = MenuPalette.Shadow; tint.a *= Mathf.Lerp(1, .5f, y); break;
                case Layer.DarkEdge: tint = Multiply(borderColor, .25f); break;
                case Layer.Rim: tint = Multiply(borderColor, Mathf.Lerp(.43f, 1.04f, y)); break;
                case Layer.Frame: tint = Multiply(borderColor, Mathf.Lerp(.19f, .4f, y)); break;
                case Layer.Channel: tint = Multiply(color, .48f); break;
                case Layer.InnerRim: tint = Multiply(borderColor, Mathf.Lerp(.33f, .63f, y)); break;
                case Layer.Bevel: tint = Multiply(borderColor, Mathf.Lerp(.23f, .52f, y)); break;
                default: tint = Multiply(borderColor, Mathf.Lerp(.55f, .13f, y)); break;
            }
            if (layer == Layer.Rim || layer == Layer.InnerRim || layer == Layer.Bevel)
            {
                float a = tint.a;
                tint = Color.Lerp(tint, focusColor, state * .55f);
                tint.a = a;
            }
            tint.a *= color.a;
            return tint;
        }

        private static Color Multiply(Color tint, float value) => new Color(tint.r * value, tint.g * value, tint.b * value, tint.a);
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
