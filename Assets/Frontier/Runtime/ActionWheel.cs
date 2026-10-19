using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public enum WheelPage { Root, Groups, Selection, Build, Orders }

    public sealed class ActionWheel : MonoBehaviour
    {
        public GameSession Session;
        public CommandPanel Panel;
        public QuestBridge Quest;
        public bool IsOpen { get; private set; }
        public WheelPage Page { get; private set; }
        public bool InBuildMenu => IsOpen && Page == WheelPage.Build;
        public bool InGroupMenu => IsOpen && Page == WheelPage.Groups;
        public bool GroupSaveMode { get; private set; }
        public bool HintVisible => hintRoot != null && hintRoot.gameObject.activeSelf;
        public int Highlighted { get; private set; } = -1;
        public string HighlightedId => Highlighted < 0 || Highlighted >= items.Count ? null : items[Highlighted].Id;
        public IReadOnlyList<SelectionAction> Items => items;
        public const float NeutralCloseDelay = .8f;
        private readonly List<SelectionAction> items = new List<SelectionAction>();
        private readonly List<RadialSector> sectors = new List<RadialSector>();
        private readonly List<Text> sectorLabels = new List<Text>();
        private readonly List<float> angles = new List<float>();
        private readonly HashSet<string> disabled = new HashSet<string>();
        private RectTransform root, slices, hintRoot;
        private Text heading, title, detail, help, hintSelection, hintOrders;
        private Font font;
        private Simulation openedMatch;
        private int[] selectedIds;
        private string actionSignature, failedAction, failureMessage;
        private PlayerView actionView, hintView;
        private string hintSelectionSignature;
        private bool stickReady, openReady, saveGesturePending, saveGestureReady;
        private int saveTarget = -1;
        private float neutralSince = -1;
        private static readonly Color Normal = new Color(.035f, .09f, .14f, .98f), Accent = new Color(.16f, .65f, .68f), Ink = new Color(.68f, .86f, .91f);
        private static readonly Color Disabled = new Color(.38f, .47f, .52f), Save = new Color(1, .7f, .3f);
        public bool CanOpen => Session != null && Session.View != null && Panel != null && Panel.AcceptsGameInput && !Panel.IsDragging && Quest != null && !Quest.Placing;

        // The router calls this before console/terrain input. Every open frame is consumed,
        // including closing and confirmation; it must also drain the confirming trigger release.
        public bool ProcessUnified(Ray pointer, Vector2 stick, bool triggerDown, bool triggerHeld, bool triggerUp, bool cancel, float now,
            bool stickDown = false, bool stickHeld = false, bool stickUp = false)
        {
            if (IsOpen && !ValidContext()) { Close(); TickHint(pointer, false); return true; }
            if (!CanOpen) { openReady = false; TickHint(pointer, false); return false; }
            if (cancel && IsOpen)
            {
                Back();
                if (IsOpen) UpdatePose(pointer);
                TickHint(pointer, true); return true;
            }
            if (cancel) { openReady = false; TickHint(pointer, true); return false; }
            if (!IsOpen)
            {
                if (stick.magnitude < .25f) openReady = true;
                if (!openReady || stick.magnitude < .45f) { TickHint(pointer, true); return false; }
                Open(); stickReady = true; Aim(stick); UpdatePose(pointer); TickHint(pointer, true);
                // Opening with a tilt is navigation only, even if a trigger edge arrives together.
                return true;
            }

            RefreshActions(); UpdatePose(pointer);
            if (InGroupMenu)
            {
                if (stickDown)
                {
                    saveGesturePending = true; saveGestureReady = stick.magnitude < .25f;
                    GroupSaveMode = saveGestureReady; saveTarget = -1;
                }
                if (saveGesturePending && stickHeld && stick.magnitude < .25f)
                { saveGestureReady = true; GroupSaveMode = true; }
                // Save the explicitly chosen slot even if releasing the click centers the stick.
                // Centering alone never recalls or saves a group.
                if (stickUp && saveGesturePending)
                {
                    int group = saveTarget; bool save = saveGestureReady && group >= 0;
                    ResetSaveGesture();
                    if (save) { ConfirmGroup(group, true); TickHint(pointer, true); return true; }
                }
                else if (saveGesturePending && !stickHeld && !stickDown) ResetSaveGesture();
            }
            Aim(stick);
            if (InGroupMenu && GroupSaveMode && Highlighted >= 0) saveTarget = Highlighted;
            if (InGroupMenu) RefreshGroups();
            if (triggerDown && !saveGesturePending)
            {
                neutralSince = -1; Confirm();
                if (!IsOpen) { TickHint(pointer, true); return true; }
            }
            if (IsOpen && stick.magnitude < .25f && !triggerHeld && !stickHeld)
            {
                if (neutralSince < 0) neutralSince = now;
                else if (now - neutralSince >= NeutralCloseDelay) Close();
            }
            else neutralSince = -1;
            TickHint(pointer, true);
            return true;
        }

        public void TickHint(Ray pointer, bool available)
        {
            bool visible = available && CanOpen && !IsOpen;
            if (!visible) { if (hintRoot != null) hintRoot.gameObject.SetActive(false); return; }
            EnsureHint(); hintRoot.gameObject.SetActive(true);
            string selection = string.Join(",", Session.Selection.OrderBy(id => id));
            if (!ReferenceEquals(hintView, Session.View) || selection != hintSelectionSignature)
            {
                hintView = Session.View; hintSelectionSignature = selection;
                var actions = SelectionActions.For(Session).Items;
                hintSelection.color = actions.Any(a => a.Category != ActionCategory.Orders) ? Color.white : Disabled;
                hintOrders.color = actions.Any(a => a.Category == ActionCategory.Orders) ? Color.white : Disabled;
            }
            Place(hintRoot, pointer, .11f, .035f, .26f, .00038f);
        }

        private void Open()
        {
            EnsureView(); IsOpen = true; Page = WheelPage.Root; Highlighted = -1; stickReady = false; openReady = false;
            selectedIds = Session.Selection.OrderBy(id => id).ToArray(); openedMatch = Session.Match;
            ResetSaveGesture(); neutralSince = -1; actionSignature = null; root.gameObject.SetActive(true); RefreshActions();
        }

        public void Close()
        {
            IsOpen = false; Page = WheelPage.Root; Highlighted = -1; openedMatch = null; openReady = false;
            neutralSince = -1; ResetSaveGesture();
            if (root != null) root.gameObject.SetActive(false);
            if (hintRoot != null) hintRoot.gameObject.SetActive(false);
        }

        private bool ValidContext() => CanOpen && Session.Match == openedMatch &&
            selectedIds.SequenceEqual(Session.Selection.OrderBy(id => id)) &&
            selectedIds.All(id => Session.View.entities.Any(e => e.id == id && e.owner == Session.View.player));

        private void Back()
        {
            if (Page == WheelPage.Root) { Close(); return; }
            Enter(Page == WheelPage.Build ? WheelPage.Selection : WheelPage.Root);
        }

        private void Enter(WheelPage page)
        {
            Page = page; actionSignature = null; Highlighted = -1; stickReady = false; neutralSince = -1;
            failedAction = failureMessage = null;
            ResetSaveGesture(); RefreshActions();
        }

        private static SelectionAction Navigation(string id, string label, string description)
            => new SelectionAction { Id = id, Title = label, Label = label, Detail = description };

        private void RefreshActions()
        {
            if (Page == WheelPage.Groups && actionSignature != null) { RefreshGroups(); return; }
            if (actionSignature != null && ReferenceEquals(actionView, Session.View)) return;
            actionView = Session.View;
            var available = SelectionActions.For(Session);
            var current = new List<SelectionAction>(); disabled.Clear();
            if (Page == WheelPage.Root)
            {
                current.Add(Navigation("groups-menu", "Groups", "Select or save a control group"));
                current.Add(Navigation("selection-menu", "Selection", "Build, train, research and abilities"));
                current.Add(Navigation("close-wheel", "Close", "Return to the battlefield"));
                current.Add(Navigation("orders-menu", "Unit orders", "Attack, stop and hold position"));
                if (!available.Items.Any(a => a.Category != ActionCategory.Orders)) disabled.Add("selection-menu");
                if (!available.Items.Any(a => a.Category == ActionCategory.Orders)) disabled.Add("orders-menu");
            }
            else if (Page == WheelPage.Groups)
            {
                for (int i = 0; i < GameSession.ControlGroupCount; i++)
                    current.Add(Navigation("group-" + (i + 1), "Group " + (i + 1), ""));
            }
            else
            {
                if (Page == WheelPage.Build) current.AddRange(available.Items.Where(a => a.Category == ActionCategory.Build));
                else if (Page == WheelPage.Orders) current.AddRange(available.Items.Where(a => a.Category == ActionCategory.Orders));
                else
                {
                    if (available.Items.Any(a => a.Category == ActionCategory.Build)) current.Add(Navigation("build-menu", "Build", "Choose a building"));
                    current.AddRange(available.Items.Where(a => a.Category != ActionCategory.Build && a.Category != ActionCategory.Orders));
                }
                current.Add(Navigation("back", "Back", "Return to " + (Page == WheelPage.Build ? "selection actions" : "the main wheel")));
            }
            string signature = Page + ":" + string.Join("|", current.Select(a => a.Id));
            if (signature == actionSignature)
            {
                // Resource, energy and cooldown changes retain the chosen sector.
                for (int i = 0; i < current.Count; i++) items[i] = current[i];
                UpdateHighlight(); UpdateDescription(); return;
            }
            actionSignature = signature; items.Clear(); items.AddRange(current);
            failedAction = failureMessage = null;
            // Never remap a held direction when a queue change adds/removes a command.
            Highlighted = -1; stickReady = false; BuildSectors();
            var selection = Session.View.entities.Where(e => Session.Selection.Contains(e.id)).ToArray();
            heading.text = Page == WheelPage.Groups ? "CONTROL GROUPS" : selection.Length == 1 ? Session.Catalog[selection[0].definition].name : selection.Length > 1 ? selection.Length + " selected items" : "COMMAND WHEEL";
            if (Page == WheelPage.Groups) RefreshGroups();
            else { UpdateHighlight(); UpdateDescription(); }
        }

        private void BuildSectors()
        {
            for (int i = slices.childCount - 1; i >= 0; i--)
            {
                var child = slices.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            sectors.Clear(); sectorLabels.Clear(); angles.Clear();
            bool cardinal = Page == WheelPage.Root || Page == WheelPage.Groups;
            int commands = cardinal ? items.Count : items.Count - 1;
            float step = cardinal ? 360f / Mathf.Max(1, commands) : 270f / Mathf.Max(1, commands);
            for (int i = 0; i < items.Count; i++)
            {
                bool back = items[i].Id == "back";
                float angle = cardinal ? i * step : back ? 180 : -135 + step * (i + .5f);
                float sweep = cardinal ? step : back ? 90 : step;
                angles.Add(angle);
                var slice = Rect("Action " + items[i].Id, slices, Vector2.zero, new Vector2(600, 600));
                var sector = slice.gameObject.AddComponent<RadialSector>(); sector.Angle = angle; sector.Sweep = sweep - 3; sector.color = Normal; sector.raycastTarget = false;
                sectors.Add(sector);
                var direction = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                float labelWidth = cardinal || back ? 146 : Mathf.Clamp(434 * Mathf.Sin(step * .45f * Mathf.Deg2Rad), 80, 146);
                var label = Label(slice, items[i].Label, direction * 217, new Vector2(labelWidth, 78), 23, Color.white);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 16; label.resizeTextMaxSize = 23; sectorLabels.Add(label);
            }
        }

        public Vector2 DirectionFor(string id)
        {
            int index = items.FindIndex(item => item.Id == id);
            if (index < 0 || index >= angles.Count) return Vector2.zero;
            float radians = angles[index] * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        private void RefreshGroups()
        {
            for (int i = 0; i < items.Count; i++)
            {
                int count = Session.GroupSize(i);
                sectorLabels[i].text = "Group " + (i + 1) + "\n" + (count == 0 ? "Empty" : count + " assigned");
            }
            UpdateHighlight(); UpdateDescription();
        }

        private void Aim(Vector2 stick)
        {
            if (stick.magnitude < .25f)
            {
                stickReady = true;
                // Releasing the held save modifier still commits this slot after
                // centering, so keep its destination visible until release or cancel.
                if (InGroupMenu && GroupSaveMode && saveTarget >= 0) return;
                if (Highlighted >= 0) { Highlighted = -1; failedAction = failureMessage = null; UpdateHighlight(); UpdateDescription(); }
                return;
            }
            if (!stickReady || stick.magnitude < .45f || items.Count == 0) return;
            float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
            bool cardinal = Page == WheelPage.Root || Page == WheelPage.Groups;
            float step = cardinal ? 360f / items.Count : 270f / Mathf.Max(1, items.Count - 1);
            int next = cardinal ? Mathf.FloorToInt((Mathf.Repeat(angle, 360) + step * .5f) / step) % items.Count :
                Mathf.Abs(angle) >= 135 || items.Count == 1 ? items.Count - 1 : Mathf.Clamp(Mathf.FloorToInt((angle + 135) / step), 0, items.Count - 2);
            if (Highlighted >= 0)
            {
                float half = !cardinal && items[Highlighted].Id == "back" ? 45 : step * .5f;
                if (Mathf.Abs(Mathf.DeltaAngle(angles[Highlighted], angle)) < half + 5) return;
            }
            Highlighted = next; failedAction = failureMessage = null; UpdateHighlight(); UpdateDescription();
        }

        private void UpdateHighlight()
        {
            for (int i = 0; i < sectors.Count; i++)
            {
                bool unavailable = disabled.Contains(items[i].Id);
                sectors[i].color = i == Highlighted ? (unavailable ? new Color(.15f, .22f, .26f) : GroupSaveMode ? Save : Accent) : Normal;
                sectorLabels[i].color = unavailable ? Disabled : i == Highlighted ? new Color(.015f, .05f, .065f) : Color.white;
            }
        }

        private void UpdateDescription()
        {
            if (title == null) return;
            detail.color = Ink;
            if (InGroupMenu)
            {
                string saveButton = Panel.Bindings.Name(true, "wheel");
                title.text = GroupSaveMode ? "SAVE GROUP" : "CONTROL GROUPS";
                if (Highlighted >= 0) title.text = (GroupSaveMode ? "SAVE GROUP " : "GROUP ") + (Highlighted + 1);
                detail.text = saveGesturePending && !saveGestureReady ? "Center stick to start saving" : GroupSaveMode ? Session.Selection.Count + " selected\nRelease " + saveButton + " to save" : Highlighted < 0 ? "Tilt to choose a group" : Session.GroupSize(Highlighted) + " assigned\nTrigger to select";
                help.text = "Trigger · Select     Hold " + saveButton + " + tilt · Save\n" + Panel.Bindings.MenuBackName + " · Back";
                return;
            }
            title.text = Highlighted >= 0 ? items[Highlighted].Title : Page == WheelPage.Root ? "COMMANDS" : Page == WheelPage.Selection ? "SELECTION" : Page == WheelPage.Build ? "BUILD" : "UNIT ORDERS";
            detail.text = Highlighted >= 0 ? disabled.Contains(items[Highlighted].Id) ? "No actions for this selection" : items[Highlighted].Detail : "Tilt to choose\nTrigger to enter";
            help.text = "Trigger · " + (Page == WheelPage.Root ? "Enter" : "Confirm") + "     Down · " + (Page == WheelPage.Root ? "Close" : "Back") + "\nCenter stick to hide";
            if (failedAction != null && failedAction == HighlightedId) { detail.text = failureMessage; detail.color = Save; }
        }

        private void ResetSaveGesture()
        {
            GroupSaveMode = false; saveGesturePending = saveGestureReady = false; saveTarget = -1;
        }

        private void ConfirmGroup(int group, bool save)
        {
            Close(); Session.CancelMode(); Session.Group(group, save);
        }

        private void Confirm()
        {
            if (Highlighted < 0) return;
            string id = items[Highlighted].Id;
            if (disabled.Contains(id)) { detail.text = "No actions for this selection"; detail.color = Save; return; }
            if (InGroupMenu) { ConfirmGroup(Highlighted, false); return; }
            if (id == "close-wheel") { Close(); return; }
            if (id == "back") { Back(); return; }
            if (id == "groups-menu") { Enter(WheelPage.Groups); return; }
            if (id == "selection-menu") { Enter(WheelPage.Selection); return; }
            if (id == "orders-menu") { Enter(WheelPage.Orders); return; }
            if (id == "build-menu") { Enter(WheelPage.Build); return; }
            // Resolve again just before submitting; availability can change while aiming.
            var action = SelectionActions.For(Session).Items.FirstOrDefault(a => a.Id == id);
            if (action == null) { actionSignature = null; RefreshActions(); return; }
            if (action.Execute()) Close();
            else { failedAction = id; failureMessage = Session.Message; UpdateDescription(); }
        }

        private void UpdatePose(Ray pointer) => Place(root, pointer, .28f, .10f, .48f, .00055f);

        private void Place(RectTransform view, Ray pointer, float reach, float lift, float minimumDistance, float scale)
        {
            var camera = Quest.Camera.transform;
            var position = pointer.GetPoint(reach) + camera.up * lift;
            var offset = position - camera.position;
            if (offset.magnitude < minimumDistance) position = camera.position + (offset.sqrMagnitude > .001f ? offset.normalized : camera.forward) * minimumDistance;
            view.SetPositionAndRotation(position, Quaternion.LookRotation(position - camera.position, camera.up));
            view.localScale = Vector3.one * scale;
        }

        private void EnsureFont() { if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }

        private void EnsureHint()
        {
            if (hintRoot != null) return;
            EnsureFont(); hintRoot = Rect("Controller command hint", transform, Vector2.zero, new Vector2(360, 280));
            var canvas = hintRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 19;
            foreach (float angle in new[] { 0f, 90f, 270f })
            {
                var slice = Rect("Direction " + angle, hintRoot, Vector2.zero, new Vector2(240, 240)).gameObject.AddComponent<RadialSector>();
                slice.Angle = angle; slice.Sweep = 78; slice.InnerRadius = 27; slice.OuterRadius = 92; slice.color = Normal; slice.raycastTarget = false;
            }
            Label(hintRoot, "Groups", new Vector2(0, 118), new Vector2(116, 34), 22, Color.white);
            hintSelection = Label(hintRoot, "Selection", new Vector2(128, 0), new Vector2(110, 42), 21, Color.white);
            hintOrders = Label(hintRoot, "Unit orders", new Vector2(-128, 0), new Vector2(110, 52), 21, Color.white);
            Label(hintRoot, "Move stick", new Vector2(0, -72), new Vector2(144, 34), 20, Ink);
            Label(hintRoot, "+", Vector2.zero, new Vector2(42, 42), 28, Accent);
        }

        private void EnsureView()
        {
            if (root != null) return;
            EnsureFont();
            root = Rect("Controller action wheel", transform, Vector2.zero, new Vector2(640, 740));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 20;
            var center = Rect("Center", root, Vector2.zero, new Vector2(600, 600)).gameObject.AddComponent<RadialSector>();
            center.InnerRadius = 0; center.OuterRadius = 137; center.Sweep = 360; center.color = new Color(.018f, .035f, .055f, .98f); center.raycastTarget = false;
            slices = Rect("Sectors", root, Vector2.zero, new Vector2(600, 600));
            heading = Label(root, "", new Vector2(0, 328), new Vector2(560, 44), 29, Color.white);
            title = Label(root, "", new Vector2(0, 44), new Vector2(222, 80), 25, Color.white);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 18; title.resizeTextMaxSize = 25;
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
        private void OnDisable() { Close(); if (hintRoot != null) hintRoot.gameObject.SetActive(false); }
    }
}
