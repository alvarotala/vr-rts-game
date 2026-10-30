using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Persistent, non-interactive construction status and workforce counters on own buildings.
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
        private bool mobilePresentation;
        private sealed class Marker
        {
            public RectTransform rect;
            public Text count, title;
            public RectTransform progress;
            public Vector2 progressSize;
            public bool construction;
            public LineRenderer stem;
        }
        public HarvestingMarkers(Transform parent)
        {
            root = new GameObject("Harvesting counters").transform; root.SetParent(parent, false);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stemMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); stemMaterial.color = new Color(.65f, .65f, .65f);
        }
        public void Update(BoardView board, Camera viewer)
        {
            var session = board.Session; var view = session.View;
            if (view == null || !session.Running || viewer == null || !board.Content.gameObject.activeInHierarchy) { Hide(); return; }
            bool mobile = false;
#if FRONTIER_BROWSER
            mobile = BrowserMobileInput.IsTouchMode;
#endif
            if (mobilePresentation != mobile)
            {
                mobilePresentation = mobile; lastView = null;
                foreach (var marker in markers.Values) { marker.rect.gameObject.SetActive(false); Release(marker.rect.gameObject); }
                markers.Clear();
            }
            root.gameObject.SetActive(true);
            if (!ReferenceEquals(lastView, view))
            {
                lastView = view; var active = new HashSet<int>();
                foreach (var building in view.entities)
                {
                    if (building.owner != view.player || building.remembered) continue;
                    string role = session.Catalog[building.definition].role;
                    bool construction = !building.complete;
                    if (!construction && role != "hq" && role != "extractor") continue;
                    bool gas = role == "extractor";
                    var deposits = view.resources.Where(r => gas ? r.kind == "catalyst" && r.extractorId == building.id : r.kind == "mineral" && r.baseId == building.id).ToArray();
                    if (!construction && deposits.Length == 0) continue;
                    active.Add(building.id);
                    if (markers.TryGetValue(building.id, out var marker) && marker.construction != construction)
                    { Release(marker.rect.gameObject); markers.Remove(building.id); marker = null; }
                    if (marker == null) markers[building.id] = marker = Create(building.id, gas, construction, role == "hq");
                    if (construction)
                    {
                        marker.title.text = building.awaitingClearance ? "CLEARING AREA" : "UNDER CONSTRUCTION";
                        float progress = Mathf.Clamp01(building.progress);
                        marker.count.text = Mathf.Min(99, Mathf.FloorToInt(progress * 100)) + "%";
                        marker.progress.sizeDelta = new Vector2(marker.progressSize.x * progress, marker.progressSize.y);
                        continue;
                    }
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
                float overlapStep = .032f;
                if (mobilePresentation)
                {
                    // Keep text readable at overview zoom, with a cap when the camera is close.
                    // Health bars sit one content cell above the roof; leave room above them.
                    var anchor = top + board.Content.TransformVector(Vector3.up * 1.35f);
                    marker.rect.position = anchor; Face(marker.rect, viewer);
                    FitMobileHeight(marker.rect, viewer, marker.construction);
                    float height = marker.rect.TransformVector(Vector3.up * marker.rect.rect.height).magnitude;
                    marker.rect.position = anchor + viewer.transform.up * (height * .64f); Face(marker.rect, viewer);
                    marker.stem.widthMultiplier = Mathf.Min(.0006f, height / 48f);
                    overlapStep = height * 1.12f;
                }
                // Keep nearby mineral/gas counters distinct even on a compact board.
                Rect screen = ScreenRect(marker.rect, viewer);
                for (int attempt = 0; attempt < 6 && occupied.Any(rect => rect.Overlaps(screen)); attempt++)
                {
                    marker.rect.position += viewer.transform.up * overlapStep; Face(marker.rect, viewer);
                    screen = ScreenRect(marker.rect, viewer);
                }
                bool visible = viewer.WorldToViewportPoint(marker.rect.position).z > 0;
                marker.rect.gameObject.SetActive(visible);
                if (!visible) continue;
                occupied.Add(screen);
                marker.stem.SetPosition(0, top + board.transform.up * .003f);
                marker.stem.SetPosition(1, marker.rect.TransformPoint(new Vector3(0, marker.rect.rect.yMin, 0)));
            }
        }
        private Marker Create(int id, bool gas, bool construction, bool headquarters)
        {
            var rect = new GameObject(construction ? "Construction " + id : (gas ? "Gas" : "Mineral") + " workers " + id, typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            rect.SetParent(root, false); rect.sizeDelta = construction ? new Vector2(220, 112) : new Vector2(180, 84);
            if (mobilePresentation) rect.sizeDelta = construction ? new Vector2(200, 60) : new Vector2(104, 36);
            var canvas = rect.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
            var background = rect.gameObject.AddComponent<Image>(); background.color = construction ? new Color(.04f, .04f, .04f, .98f) : new Color(.015f, .035f, .05f, .96f); background.raycastTarget = false;
            var title = Text(rect, construction ? "Construction status" : "Resource", new Vector2(0, construction ? 36 : 24), new Vector2(construction ? 210 : 170, 23), 15);
            title.text = construction ? "UNDER CONSTRUCTION" : gas ? "GAS WORKERS" : "MINERAL WORKERS";
            title.color = construction ? Color.white : gas ? new Color(.83f, .65f, 1) : new Color(.4f, .88f, 1);
            var count = Text(rect, construction ? "Construction percent" : "Assigned / optimal", new Vector2(0, construction ? 4 : -10), new Vector2(170, 48), construction ? 32 : 38);
            count.color = Color.white;
            if (mobilePresentation)
            {
                var tint = background.color; tint.a = .72f; background.color = tint;
                count.rectTransform.anchoredPosition = construction ? new Vector2(0, -8) : new Vector2(14, 0);
                count.rectTransform.sizeDelta = construction ? new Vector2(188, 30) : new Vector2(72, 34);
                count.fontSize = 26; count.resizeTextForBestFit = true; count.resizeTextMinSize = 18; count.resizeTextMaxSize = 26;
                count.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (construction)
                {
                    title.rectTransform.anchoredPosition = new Vector2(0, 19); title.rectTransform.sizeDelta = new Vector2(188, 22);
                    title.fontSize = 18; title.resizeTextForBestFit = true; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 18;
                    title.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
                else
                {
                    title.gameObject.SetActive(false);
                    var glyph = new GameObject("Resource symbol", typeof(RectTransform)).AddComponent<ConsoleGlyph>();
                    glyph.transform.SetParent(rect, false); glyph.rectTransform.anchoredPosition = new Vector2(-37, 0);
                    glyph.rectTransform.sizeDelta = new Vector2(22, 22); glyph.Glyph = gas ? ConsoleGlyph.Kind.Gas : ConsoleGlyph.Kind.Mineral;
                    glyph.color = title.color;
                }
            }
            RectTransform progress = null;
            var progressSize = mobilePresentation ? new Vector2(176, 3) : new Vector2(196, 7);
            if (construction)
            {
                var track = new GameObject("Construction progress track", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                track.SetParent(rect, false); track.anchoredPosition = new Vector2(0, -24); track.sizeDelta = new Vector2(196, 7);
                if (mobilePresentation) { track.anchoredPosition = new Vector2(0, -27); track.sizeDelta = progressSize; }
                track.GetComponent<Image>().color = new Color(.25f, .25f, .25f); track.GetComponent<Image>().raycastTarget = false;
                progress = new GameObject("Construction progress", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                progress.SetParent(track, false); progress.anchorMin = progress.anchorMax = progress.pivot = new Vector2(0, .5f);
                progress.GetComponent<Image>().color = Color.white; progress.GetComponent<Image>().raycastTarget = false;
                var status = Text(rect, "Availability", new Vector2(0, -43), new Vector2(210, 20), 13);
                status.text = headquarters ? "NO RESOURCE DELIVERY YET" : "NOT OPERATIONAL YET";
                status.color = Color.white;
                if (mobilePresentation) status.gameObject.SetActive(false);
            }
            var stem = new GameObject("Attachment").AddComponent<LineRenderer>(); stem.transform.SetParent(rect, false);
            stem.sharedMaterial = stemMaterial; stem.positionCount = 2; stem.useWorldSpace = true; stem.widthMultiplier = .0006f;
            return new Marker { rect = rect, count = count, title = title, stem = stem, progress = progress, progressSize = progressSize, construction = construction };
        }
        private Text Text(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<LocalizedText>(); text.transform.SetParent(parent, false);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private static void Face(Transform label, Camera viewer)
        {
            var forward = label.position - viewer.transform.position;
            if (forward.sqrMagnitude > .0001f) label.rotation = Quaternion.LookRotation(forward, viewer.transform.up);
        }
        private void FitMobileHeight(RectTransform label, Camera viewer, bool construction)
        {
            if (viewer.pixelWidth <= 0 || viewer.pixelHeight <= 0 || viewer.WorldToViewportPoint(label.position).z <= 0) return;
            label.GetWorldCorners(corners); float min = float.MaxValue, max = float.MinValue;
            foreach (var corner in corners)
            { float y = viewer.WorldToScreenPoint(corner).y; min = Mathf.Min(min, y); max = Mathf.Max(max, y); }
            float projectedHeight = max - min;
            if (projectedHeight <= .001f) return;
            // Short-axis units keep both orientations equally legible. A continuous clamp
            // grows only distant badges and shrinks only those exceeding the compact cap.
            float referenceScale = Mathf.Min(viewer.pixelWidth, viewer.pixelHeight) / 720f;
            float minimum = (construction ? 48 : 28) * referenceScale;
            float maximum = label.rect.height * referenceScale;
            label.localScale *= Mathf.Clamp(projectedHeight, minimum, maximum) / projectedHeight;
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
