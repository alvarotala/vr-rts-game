using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Persistent, non-interactive workforce counters attached to operating own bases.
    // All values come from PlayerView, including workers travelling or depositing cargo.
    public sealed class HarvestingMarkers : System.IDisposable
    {
        public static readonly Color ExcessColor = new Color(1, .22f, .18f);
        private const float PixelScale = .00032f;
        private readonly Transform root;
        private readonly Font font;
        private readonly Material stemMaterial;
        private readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();
        private readonly List<Rect> occupied = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private PlayerView lastView;
        private sealed class Marker
        {
            public RectTransform rect;
            public Text count;
            public LineRenderer stem;
        }
        public HarvestingMarkers(Transform parent)
        {
            root = new GameObject("Harvesting counters").transform; root.SetParent(parent, false);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stemMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); stemMaterial.color = new Color(.3f, .65f, .69f);
        }
        public void Update(BoardView board, Camera viewer)
        {
            var session = board.Session; var view = session.View;
            if (view == null || !session.Running || viewer == null || !board.Content.gameObject.activeInHierarchy) { Hide(); return; }
            root.gameObject.SetActive(true);
            if (!ReferenceEquals(lastView, view))
            {
                lastView = view; var active = new HashSet<int>();
                foreach (var building in view.entities)
                {
                    if (building.owner != view.player || !building.complete || building.remembered) continue;
                    string role = session.Catalog[building.definition].role;
                    if (role != "hq" && role != "extractor") continue;
                    bool gas = role == "extractor";
                    var deposits = view.resources.Where(r => gas ? r.kind == "catalyst" && r.extractorId == building.id : r.kind == "mineral" && r.baseId == building.id).ToArray();
                    if (deposits.Length == 0) continue;
                    active.Add(building.id);
                    if (!markers.TryGetValue(building.id, out var marker)) markers[building.id] = marker = Create(building.id, gas);
                    int assigned = deposits.Sum(r => r.assignedWorkers), optimal = deposits.Sum(r => r.optimalWorkers);
                    string value = assigned + "/" + optimal;
                    if (marker.count.text != value) marker.count.text = value;
                    marker.count.color = assigned > optimal ? ExcessColor : Color.white;
                }
                foreach (int id in markers.Keys.Where(id => !active.Contains(id)).ToArray())
                { Release(markers[id].rect.gameObject); markers.Remove(id); }
            }
            occupied.Clear();
            foreach (var item in markers)
            {
                var marker = item.Value;
                if (!board.TryEntityTop(item.Key, out var top) || !InsideBoard(board, top)) { marker.rect.gameObject.SetActive(false); continue; }
                marker.rect.gameObject.SetActive(true);
                marker.rect.localScale = Vector3.one * (PixelScale / board.CellScale);
                marker.rect.position = top + board.transform.up * .035f;
                Face(marker.rect, viewer);
                // Keep nearby mineral/gas counters distinct even on a compact board.
                Rect screen = ScreenRect(marker.rect, viewer);
                for (int attempt = 0; attempt < 6 && occupied.Any(rect => rect.Overlaps(screen)); attempt++)
                {
                    marker.rect.position += viewer.transform.up * .032f; Face(marker.rect, viewer);
                    screen = ScreenRect(marker.rect, viewer);
                }
                bool visible = viewer.WorldToViewportPoint(marker.rect.position).z > 0;
                marker.rect.gameObject.SetActive(visible);
                if (!visible) continue;
                occupied.Add(screen);
                marker.stem.SetPosition(0, top + board.transform.up * .003f);
                marker.stem.SetPosition(1, marker.rect.TransformPoint(new Vector3(0, -42, 0)));
            }
        }
        private Marker Create(int id, bool gas)
        {
            var rect = new GameObject((gas ? "Gas" : "Mineral") + " workers " + id, typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            rect.SetParent(root, false); rect.sizeDelta = new Vector2(180, 84);
            var canvas = rect.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
            var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.015f, .035f, .05f, .96f); background.raycastTarget = false;
            var title = Text(rect, "Resource", new Vector2(0, 24), new Vector2(170, 23), 15);
            title.text = gas ? "GAS WORKERS" : "MINERAL WORKERS"; title.color = gas ? new Color(.83f, .65f, 1) : new Color(.4f, .88f, 1);
            var count = Text(rect, "Assigned / optimal", new Vector2(0, -10), new Vector2(170, 48), 38);
            var stem = new GameObject("Attachment").AddComponent<LineRenderer>(); stem.transform.SetParent(rect, false);
            stem.sharedMaterial = stemMaterial; stem.positionCount = 2; stem.useWorldSpace = true; stem.widthMultiplier = .0006f;
            return new Marker { rect = rect, count = count, stem = stem };
        }
        private Text Text(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>(); text.transform.SetParent(parent, false);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private static void Face(Transform label, Camera viewer)
        {
            var forward = label.position - viewer.transform.position;
            if (forward.sqrMagnitude > .0001f) label.rotation = Quaternion.LookRotation(forward, viewer.transform.up);
        }
        private Rect ScreenRect(RectTransform label, Camera viewer)
        {
            label.GetWorldCorners(corners); var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            { var p = (Vector2)viewer.WorldToViewportPoint(corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x - .003f, min.y - .003f, max.x + .003f, max.y + .003f);
        }
        private static bool InsideBoard(BoardView board, Vector3 world)
        { var point = board.transform.InverseTransformPoint(world); return Mathf.Abs(point.x) < board.Width * .5f && Mathf.Abs(point.z) < board.Depth * .5f; }
        public void Hide() { if (root != null) root.gameObject.SetActive(false); }
        public void Dispose() { if (root != null) Release(root.gameObject); Release(stemMaterial); markers.Clear(); }
        private static void Release(Object item) { if (item == null) return; if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item); }
    }
}
