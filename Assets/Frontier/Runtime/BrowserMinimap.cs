#if FRONTIER_BROWSER
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // The overview uses the same filtered view as the battlefield. It never
    // reads Simulation, including when a local match happens to be available.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BrowserMinimap : MaskableGraphic
    {
        public GameSession Session;
        public BoardView Board;
        public QuestBridge Quest;

        private const float RefreshInterval = .2f;
        private static readonly Color32 Unseen = new Color(.025f, .045f, .055f, 1).linear;
        private static readonly Color32 Minerals = new Color(.38f, .83f, 1);
        private static readonly Color32 Gas = new Color(.85f, .62f, 1);
        private Texture2D overview;
        private MapDefinition map;
        private Color32[] ground, pixels;
        private float nextRefresh;
        private bool dragging;

        public override Texture mainTexture => overview != null ? overview : Texture2D.whiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = Color.white;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            nextRefresh = 0;
        }

        protected override void OnDisable()
        {
            dragging = false;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseTexture();
            base.OnDestroy();
        }

        private void Update()
        {
            if (Session == null || Session.Map == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            if (!ReferenceEquals(map, Session.Map)) PrepareMap(Session.Map);
            if (overview == null) return;
            RefreshOverview(Session.View);
        }

        // Call every pointer frame while the match HUD is active. A drag that
        // starts on this map keeps ownership until release, even outside it.
        public bool Point(Vector2 screenPosition, bool down, bool held)
        {
            if (!isActiveAndEnabled || Session == null || Session.Map == null) return false;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out var local))
                return dragging;
            var bounds = MapRect(rectTransform.rect, Session.Map);
            if (bounds.width <= 0 || bounds.height <= 0) return false;
            bool inside = rectTransform.rect.Contains(local);
            bool wasDragging = dragging;
            if (down && bounds.Contains(local)) dragging = true;
            if (dragging && (held || down) && Quest != null && Quest.Navigation != null)
            {
                float x = Mathf.Clamp01((local.x - bounds.xMin) / bounds.width) * Session.Map.width;
                float y = Mathf.Clamp01((local.y - bounds.yMin) / bounds.height) * Session.Map.height;
                Quest.Navigation.CenterOn(new Point(Mathf.Min(x, Session.Map.width - .01f), Mathf.Min(y, Session.Map.height - .01f)));
                if (down) nextRefresh = 0;
            }
            if (!held && !down) dragging = false;
            return inside || wasDragging || dragging;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = MapRect(GetPixelAdjustedRect(), map ?? Session?.Map);
            if (bounds.width <= 0 || bounds.height <= 0) return;
            mesh.AddVert(new Vector3(bounds.xMin, bounds.yMin, 0), color, new Vector2(0, 0));
            mesh.AddVert(new Vector3(bounds.xMin, bounds.yMax, 0), color, new Vector2(0, 1));
            mesh.AddVert(new Vector3(bounds.xMax, bounds.yMax, 0), color, new Vector2(1, 1));
            mesh.AddVert(new Vector3(bounds.xMax, bounds.yMin, 0), color, new Vector2(1, 0));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }

        private static Rect MapRect(Rect bounds, MapDefinition definition)
        {
            if (definition == null || definition.width <= 0 || definition.height <= 0) return bounds;
            float scale = Mathf.Min(bounds.width / definition.width, bounds.height / definition.height);
            var size = new Vector2(definition.width * scale, definition.height * scale);
            return new Rect(bounds.center - size * .5f, size);
        }

        private void PrepareMap(MapDefinition definition)
        {
            ReleaseTexture();
            map = definition;
            if (map.width <= 0 || map.height <= 0) return;
            ground = new Color32[map.width * map.height];
            pixels = new Color32[ground.Length];
            var terrain = Color.Lerp(ConsoleTheme.Surface, ConsoleTheme.Accent, .14f);
            if (map.theme == "forest") terrain = Color.Lerp(terrain, new Color(.17f, .28f, .20f).linear, .5f);
            else if (map.theme == "desert") terrain = Color.Lerp(terrain, new Color(.46f, .34f, .22f).linear, .55f);
            else if (map.theme == "neon") terrain = Color.Lerp(terrain, ConsoleTheme.Secondary, .25f);
            for (int i = 0; i < ground.Length; i++) ground[i] = terrain;
            // Only static, public terrain comes from MapDefinition. Resources,
            // units and building memories below come exclusively from PlayerView.
            if (map.terrain != null) foreach (var region in map.terrain)
            {
                if (region == null) continue;
                var tint = !region.walkable ? Color.Lerp(terrain, ConsoleTheme.Track, .75f)
                    : region.ramp ? Color.Lerp(terrain, ConsoleTheme.Accent, .35f)
                    : region.elevation > 0 ? Color.Lerp(terrain, ConsoleTheme.Muted, .18f) : terrain;
                int right = Mathf.Min(map.width, region.x + region.width), top = Mathf.Min(map.height, region.y + region.height);
                for (int y = Mathf.Max(0, region.y); y < top; y++)
                    for (int x = Mathf.Max(0, region.x); x < right; x++) ground[y * map.width + x] = tint;
            }
            overview = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false, true)
            {
                name = "Browser minimap", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            SetMaterialDirty();
            SetVerticesDirty();
        }

        private void RefreshOverview(PlayerView view)
        {
            bool hasVision = view != null && view.visibility != null && view.visibility.Length == pixels.Length;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte vision = hasVision ? view.visibility[i] : (byte)0;
                var tint = ground[i];
                pixels[i] = vision == 2 ? tint : vision == 1
                    ? new Color32((byte)(tint.r * .4f), (byte)(tint.g * .4f), (byte)(tint.b * .4f), 255) : Unseen;
            }
            if (view != null)
            {
                int marker = Mathf.Max(1, map.width / 128);
                if (view.resources != null) foreach (var resource in view.resources)
                {
                    if (resource.remaining == 0) continue;
                    int x = Mathf.FloorToInt(resource.position.x), y = Mathf.FloorToInt(resource.position.y);
                    FillBox(x - marker, y - marker, marker * 2 + 1, marker * 2 + 1, resource.kind == "catalyst" ? Gas : Minerals);
                }
                if (view.entities != null && Session.Catalog != null) foreach (var entity in view.entities)
                {
                    if (!Session.Catalog.Has(entity.definition)) continue;
                    var type = Session.Catalog[entity.definition];
                    bool own = entity.owner == view.player;
                    Color tint = own ? ConsoleTheme.Accent : new Color(1, .36f, .23f);
                    if (own && Session.Selection.Contains(entity.id)) tint = ConsoleTheme.Ink;
                    if (entity.remembered) tint = Color.Lerp(ConsoleTheme.Track, tint, .4f);
                    int x = Mathf.FloorToInt(entity.position.x), y = Mathf.FloorToInt(entity.position.y);
                    if (type.building) FillBox(x, y, Mathf.Max(marker * 2 + 1, type.footprint), Mathf.Max(marker * 2 + 1, type.footprint), tint);
                    else FillBox(x - marker, y - marker, marker * 2 + 1, marker * 2 + 1, tint);
                }
                DrawCameraFrame();
            }
            overview.SetPixels32(pixels);
            overview.Apply(false, false);
        }

        private void FillBox(int x, int y, int width, int height, Color32 tint)
        {
            int right = Mathf.Min(map.width, x + width), top = Mathf.Min(map.height, y + height);
            for (int row = Mathf.Max(0, y); row < top; row++)
                for (int column = Mathf.Max(0, x); column < right; column++) pixels[row * map.width + column] = tint;
        }

        private void DrawCameraFrame()
        {
            if (Quest == null || Quest.Camera == null || Board == null) return;
            var camera = Quest.Camera;
            var viewport = camera.pixelRect;
            if (!Board.RayToSelectionPlane(camera.ScreenPointToRay(new Vector3(viewport.xMin, viewport.yMin, 0)), out var a)
                || !Board.RayToSelectionPlane(camera.ScreenPointToRay(new Vector3(viewport.xMax, viewport.yMin, 0)), out var b)
                || !Board.RayToSelectionPlane(camera.ScreenPointToRay(new Vector3(viewport.xMax, viewport.yMax, 0)), out var c)
                || !Board.RayToSelectionPlane(camera.ScreenPointToRay(new Vector3(viewport.xMin, viewport.yMax, 0)), out var d)) return;
            DrawLine(a, b); DrawLine(b, c); DrawLine(c, d); DrawLine(d, a);
        }

        private void DrawLine(Point from, Point to)
        {
            float dx = to.x - from.x, dy = to.y - from.y, begin = 0, end = 1;
            if (!Clip(-dx, from.x, ref begin, ref end) || !Clip(dx, map.width - 1 - from.x, ref begin, ref end)
                || !Clip(-dy, from.y, ref begin, ref end) || !Clip(dy, map.height - 1 - from.y, ref begin, ref end)) return;
            int x = Mathf.Clamp(Mathf.RoundToInt(from.x + dx * begin), 0, map.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(from.y + dy * begin), 0, map.height - 1);
            int lastX = Mathf.Clamp(Mathf.RoundToInt(from.x + dx * end), 0, map.width - 1);
            int lastY = Mathf.Clamp(Mathf.RoundToInt(from.y + dy * end), 0, map.height - 1);
            int distanceX = Mathf.Abs(lastX - x), distanceY = -Mathf.Abs(lastY - y);
            int stepX = x < lastX ? 1 : -1, stepY = y < lastY ? 1 : -1, error = distanceX + distanceY;
            Color32 tint = ConsoleTheme.Ink;
            while (true)
            {
                pixels[y * map.width + x] = tint;
                if (x == lastX && y == lastY) break;
                int twice = error * 2;
                if (twice >= distanceY) { error += distanceY; x += stepX; }
                if (twice <= distanceX) { error += distanceX; y += stepY; }
            }
        }

        private static bool Clip(float direction, float distance, ref float begin, ref float end)
        {
            if (Mathf.Abs(direction) < .00001f) return distance >= 0;
            float fraction = distance / direction;
            if (direction < 0) { if (fraction > end) return false; begin = Mathf.Max(begin, fraction); }
            else { if (fraction < begin) return false; end = Mathf.Min(end, fraction); }
            return true;
        }

        private void ReleaseTexture()
        {
            if (overview == null) return;
            if (Application.isPlaying) Destroy(overview); else DestroyImmediate(overview);
            overview = null;
        }
    }
}
#endif
