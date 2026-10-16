using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed class ActionWheel : MonoBehaviour
    {
        public GameSession Session;
        public CommandPanel Panel;
        public QuestBridge Quest;
        public bool IsOpen { get; private set; }
        public bool InBuildMenu { get; private set; }
        public int Highlighted { get; private set; } = -1;
        public string HighlightedId => Highlighted < 0 ? null : items[Highlighted].Id;
        public IReadOnlyList<SelectionAction> Items => items;
        private readonly List<SelectionAction> items = new List<SelectionAction>();
        private readonly List<RadialSector> sectors = new List<RadialSector>();
        private readonly List<Text> sectorLabels = new List<Text>();
        private RectTransform root, slices;
        private Text heading, title, detail, help;
        private Font font;
        private Simulation openedMatch;
        private int[] selectedIds;
        private string actionSignature;
        private PlayerView actionView;
        private bool stickReady;
        private static readonly Color Normal = new Color(.035f, .09f, .14f, .98f), Accent = new Color(.16f, .65f, .68f), Ink = new Color(.68f, .86f, .91f);
        private bool Allowed => Panel != null && Panel.AcceptsGameInput && !Panel.IsDragging && !Quest.Placing;

        // Called before console/terrain input. Every frame of an open wheel is consumed,
        // including the frame that closes it: A must never also become a contextual map order.
        public bool Process(Ray pointer, Vector2 stick, bool press, bool confirm, bool cancel)
        {
            bool wasOpen = IsOpen;
            if (IsOpen && !ValidContext()) { Close(); return true; }
            if (cancel && IsOpen) { Back(); return true; }
            if (press)
            {
                if (IsOpen) Close();
                else if (Allowed) { Open(); UpdatePose(pointer); }
                return wasOpen || Allowed;
            }
            if (!IsOpen) return false;
            RefreshActions(); UpdatePose(pointer); Aim(stick);
            if (confirm) Confirm();
            return true;
        }

        private void Open()
        {
            EnsureView(); IsOpen = true; InBuildMenu = false; Highlighted = -1; stickReady = false;
            selectedIds = Session.Selection.OrderBy(id => id).ToArray(); openedMatch = Session.Match;
            actionSignature = null; root.gameObject.SetActive(true); RefreshActions();
        }

        public void Close()
        {
            IsOpen = false; InBuildMenu = false; Highlighted = -1; openedMatch = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        private bool ValidContext() => Allowed && Session.Match == openedMatch &&
            selectedIds.SequenceEqual(Session.Selection.OrderBy(id => id)) &&
            selectedIds.All(id => Session.View.entities.Any(e => e.id == id && e.owner == Session.View.player));

        private void Back()
        {
            if (!InBuildMenu) { Close(); return; }
            InBuildMenu = false; actionSignature = null; Highlighted = -1; stickReady = false; RefreshActions();
        }

        private void RefreshActions()
        {
            if (actionSignature != null && ReferenceEquals(actionView, Session.View)) return;
            actionView = Session.View;
            var available = SelectionActions.For(Session);
            var current = new List<SelectionAction>();
            if (InBuildMenu) current.AddRange(available.Items.Where(a => a.Building));
            else
            {
                if (available.Items.Any(a => a.Building)) current.Add(new SelectionAction { Id = "build-menu", Title = "Build", Label = "Build", Detail = "Choose a building" });
                current.AddRange(available.Items.Where(a => !a.Building));
            }
            string signature = string.Join("|", current.Select(a => a.Id));
            if (signature == actionSignature) return;
            actionSignature = signature; items.Clear(); items.AddRange(current);
            // A queue changing can add/remove Cancel. Never remap an old direction to a
            // different command until the player centers the stick and chooses again.
            Highlighted = -1; stickReady = false;
            for (int i = slices.childCount - 1; i >= 0; i--)
            {
                var child = slices.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            sectors.Clear(); sectorLabels.Clear();
            float step = items.Count == 0 ? 360 : 360f / items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                var slice = Rect("Action " + items[i].Id, slices, Vector2.zero, new Vector2(600, 600));
                var sector = slice.gameObject.AddComponent<RadialSector>(); sector.Angle = i * step; sector.Sweep = step - 3; sector.color = Normal; sector.raycastTarget = false;
                sectors.Add(sector);
                var direction = new Vector2(Mathf.Sin(i * step * Mathf.Deg2Rad), Mathf.Cos(i * step * Mathf.Deg2Rad));
                sectorLabels.Add(Label(slice, items[i].Label, direction * 217, new Vector2(146, 78), 23, Color.white));
            }
            var selection = Session.View.entities.Where(e => Session.Selection.Contains(e.id)).ToArray();
            heading.text = selection.Length == 1 ? Session.Catalog[selection[0].definition].name : selection.Length > 1 ? selection.Length + " selected items" : "NO SELECTION";
            title.text = InBuildMenu ? "BUILD" : available.Heading;
            detail.text = items.Count == 0 ? available.Hint : "Tilt right stick\nto choose";
            detail.color = Ink;
            help.text = "A · Execute     " + (InBuildMenu ? "B · Back" : "B · Close");
        }

        private void Aim(Vector2 stick)
        {
            if (stick.magnitude < .25f) { stickReady = true; return; }
            if (!stickReady || stick.magnitude < .45f || items.Count == 0) return;
            float angle = Mathf.Repeat(Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg, 360);
            float step = 360f / items.Count;
            if (Highlighted >= 0 && Mathf.Abs(Mathf.DeltaAngle(Highlighted * step, angle)) < step * .5f + 7) return;
            Highlighted = Mathf.FloorToInt((angle + step * .5f) / step) % items.Count;
            for (int i = 0; i < sectors.Count; i++)
            {
                sectors[i].color = i == Highlighted ? Accent : Normal;
                sectorLabels[i].color = i == Highlighted ? new Color(.015f, .05f, .065f) : Color.white;
            }
            title.text = items[Highlighted].Title; detail.text = items[Highlighted].Detail; detail.color = Ink;
        }

        private void Confirm()
        {
            if (Highlighted < 0) return;
            string id = items[Highlighted].Id;
            if (id == "build-menu")
            { InBuildMenu = true; actionSignature = null; RefreshActions(); return; }
            // Resolve again immediately before submitting, rather than retaining stale
            // command availability from the frame when the wheel was opened.
            var action = SelectionActions.For(Session).Items.FirstOrDefault(a => a.Id == id);
            if (action == null) { actionSignature = null; RefreshActions(); return; }
            if (action.Execute()) Close();
            else { detail.text = Session.Message; detail.color = new Color(1, .67f, .36f); }
        }

        private void UpdatePose(Ray pointer)
        {
            var camera = Quest.Camera.transform;
            var position = pointer.GetPoint(.28f) + camera.up * .10f;
            var offset = position - camera.position;
            if (offset.magnitude < .48f) position = camera.position + (offset.sqrMagnitude > .001f ? offset.normalized : camera.forward) * .48f;
            root.SetPositionAndRotation(position, Quaternion.LookRotation(position - camera.position, camera.up));
            root.localScale = Vector3.one * .00055f;
        }

        private void EnsureView()
        {
            if (root != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = Rect("Controller action wheel", transform, Vector2.zero, new Vector2(640, 740));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 20;
            var center = Rect("Center", root, Vector2.zero, new Vector2(600, 600)).gameObject.AddComponent<RadialSector>();
            center.InnerRadius = 0; center.OuterRadius = 137; center.Sweep = 360; center.color = new Color(.018f, .035f, .055f, .98f); center.raycastTarget = false;
            slices = Rect("Sectors", root, Vector2.zero, new Vector2(600, 600));
            heading = Label(root, "", new Vector2(0, 328), new Vector2(560, 44), 29, Color.white);
            title = Label(root, "", new Vector2(0, 44), new Vector2(222, 80), 25, Color.white);
            detail = Label(root, "", new Vector2(0, -47), new Vector2(224, 94), 21, Ink);
            detail.resizeTextForBestFit = true; detail.resizeTextMinSize = 15; detail.resizeTextMaxSize = 21;
            help = Label(root, "", new Vector2(0, -332), new Vector2(610, 62), 21, Ink);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<Text>(); label.font = font; label.text = text;
            label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        private void OnDisable() => Close();
    }
}
