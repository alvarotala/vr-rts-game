using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Console-only styling. The controller wheel and its shared text stay independent.
    public static class ConsoleTheme
    {
        public static readonly Color Background = Rgb(52, 76, 89);
        public static readonly Color Surface = Rgb(60, 86, 99);
        public static readonly Color Card = Rgb(73, 99, 112);
        public static readonly Color Primary = Rgb(46, 101, 109);
        public static readonly Color Ink = Rgb(242, 242, 232);
        public static readonly Color Muted = Rgb(209, 222, 224);
        public static readonly Color Accent = Rgb(177, 232, 221);
        public static readonly Color Focus = Accent;
        public static readonly Color HoverInk = Rgb(25, 52, 60);
        public static readonly Color Success = Rgb(48, 112, 78);
        public static readonly Color Danger = Rgb(151, 57, 58);
        public static readonly Color Track = Rgb(38, 61, 72);
        // Keep generated assets out of saved files, but allow reclamation after a domain reload.
        private const HideFlags GeneratedAssetFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        private static Sprite rounded;
        private static Sprite pill;

        private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1).linear;

        public static void StyleSurface(Image image)
        {
            var size = image.rectTransform.rect.size;
            if (Mathf.Min(size.x, size.y) < 28 || image.color.a == 0) return;
            if (rounded == null)
            {
                // One small alpha mask, native UI rendering, fixed-size corners.
                // No extra shader, decorative frame, lighting, or text effect.
                const int side = 32, radius = 10;
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, true, true)
                {
                    name = "Console rounded fill", hideFlags = GeneratedAssetFlags,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[side * side];
                for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
                {
                    var point = new Vector2(x + .5f, y + .5f);
                    var nearest = new Vector2(Mathf.Clamp(point.x, radius, side - radius), Mathf.Clamp(point.y, radius, side - radius));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + .5f - Vector2.Distance(point, nearest)) * 255);
                    pixels[y * side + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels); texture.Apply(true, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, side, side), Vector2.one * .5f, 100, 0,
                    SpriteMeshType.FullRect, Vector4.one * radius);
                rounded.name = "Console rounded fill"; rounded.hideFlags = GeneratedAssetFlags;
            }
            image.sprite = rounded; image.type = Image.Type.Sliced;
        }

        // Apply after setting the rectangle, including when the grip changes size.
        // Both axes use the same radius so short bars retain circular end caps.
        public static void StylePill(Image image)
        {
            const int side = 64, radius = side / 2;
            if (pill == null)
            {
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, true, true)
                {
                    name = "Console pill fill", hideFlags = GeneratedAssetFlags,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[side * side];
                var center = Vector2.one * radius;
                for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), center);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + .5f - distance) * 255);
                    pixels[y * side + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels); texture.Apply(true, true);
                pill = Sprite.Create(texture, new Rect(0, 0, side, side), Vector2.one * .5f, 100, 0,
                    SpriteMeshType.FullRect, Vector4.one * radius);
                pill.name = "Console pill fill"; pill.hideFlags = GeneratedAssetFlags;
            }
            image.sprite = pill; image.type = Image.Type.Sliced; image.fillCenter = true;
            var size = image.rectTransform.rect.size;
            image.pixelsPerUnitMultiplier = side / (Mathf.Max(1, Mathf.Min(size.x, size.y)) * image.pixelsPerUnit);
        }
    }
}
