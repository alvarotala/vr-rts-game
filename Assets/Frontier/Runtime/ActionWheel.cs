using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public enum WheelPage { Root, Groups, Selection, Orders, QuickSelect }

    public sealed class ActionWheel : MonoBehaviour
    {
        public GameSession Session;
        public CommandPanel Panel;
        public QuestBridge Quest;
        public bool IsOpen { get; private set; }
        public WheelPage Page { get; private set; }
        public bool InGroupMenu => IsOpen && Page == WheelPage.Groups;
        public bool HintsEnabled { get; private set; } = true;
        public bool HintVisible => hintRoot != null && hintRoot.gameObject.activeSelf;
        public int Highlighted { get; private set; } = -1;
        public string HighlightedId => Highlighted < 0 || Highlighted >= items.Count ? null : items[Highlighted].Id;
        public IReadOnlyList<SelectionAction> Items => items;
        public const float NeutralCloseDelay = .4f;
        public const float ConfirmationDuration = .35f;
        private readonly List<SelectionAction> items = new List<SelectionAction>();
        private readonly List<RadialSector> sectors = new List<RadialSector>();
        private readonly List<Text> sectorLabels = new List<Text>();
        private readonly List<float> angles = new List<float>();
        private readonly HashSet<string> disabled = new HashSet<string>();
        private RectTransform root, slices, hintRoot, confirmationRoot, confirmationGlyph;
        private RadialSector confirmationArc, confirmationBadge;
        private Text confirmationLabel;
        private Image[] confirmationCheck;
        private string confirmedId;
        private WheelPage confirmedPage;
        private object confirmedMatch;
        private int[] confirmedSelection;
        private float confirmationStarted = -1, confirmationStrength;
        private static readonly Color Confirmed = new Color(.65f, 1, .77f);
        private Text heading, title, detail, help, hintSelection, hintOrders;
        private readonly Text[] hintLabels = new Text[4];
        private readonly Text[] buttonHints = new Text[5];
        private static readonly string[] hintInputs = { "touch:trigger", "touch:b", "touch:a", "touch:grip", "touch:stick" };
        private readonly RectTransform[] hintLines = new RectTransform[15];
        private bool? hintLeft;
        private string hintBindings;
        private Font font;
        private object openedMatch;
        private int[] selectedIds;
        private string actionSignature, failedAction, failureMessage;
        private PlayerView actionView, hintView;
        private string hintSelectionSignature;
        private bool placementHintContext, placementHintsEnabled = true;
        private bool stickReady, openReady;
        private float neutralSince = -1;
        private static readonly Color Normal = MenuPalette.Card, Accent = MenuPalette.Accent, Ink = MenuPalette.Muted;
        private static readonly Color Disabled = new Color(.7f, .78f, .82f), Save = new Color(1, .7f, .3f);
        public bool CanOpen => Session != null && Session.View != null && Panel != null && Panel.AcceptsGameInput && !Panel.IsDragging && Quest != null && !Quest.Placing;

        // The router calls this before console/terrain input. Every open frame is consumed,
        // including closing and confirmation; it must also drain the confirming trigger release.
        public bool ProcessUnified(Ray pointer, Vector2 stick, bool triggerDown, bool triggerHeld, bool triggerUp, bool cancel, float now,
            bool stickDown = false, bool stickHeld = false, bool stickUp = false)
        {
            if (IsOpen && !ValidContext()) { Close(); TickHint(pointer, false, now); return true; }
            if (!CanOpen) { openReady = false; TickHint(pointer, false, now); return false; }
            if (cancel && IsOpen)
            {
                Back(stick);
                if (IsOpen) UpdatePose(pointer);
                TickHint(pointer, true, now); return true;
            }
            if (cancel) { openReady = false; TickHint(pointer, true, now); return false; }
            if (stickDown)
            {
                if (InGroupMenu)
                {
                    Aim(stick);
                    if (Highlighted >= 0) ConfirmGroup(Highlighted, true, now);
                }
                else HintsEnabled = !HintsEnabled;
                if (IsOpen) UpdatePose(pointer);
                else openReady = false;
                TickHint(pointer, true, now);
                return true;
            }
            if (!IsOpen)
            {
                if (stick.magnitude < .25f) openReady = true;
                if (!openReady || stick.magnitude < .45f) { TickHint(pointer, true, now); return false; }
                Open(); stickReady = true; Aim(stick); Confirm(stick, now, false);
                if (IsOpen) UpdatePose(pointer);
                TickHint(pointer, true, now);
                // Enter the hinted category and highlight the same direction there.
                // Only a subsequent trigger press can execute its highlighted option.
                return true;
            }

            RefreshActions(); UpdatePose(pointer);
            Aim(stick);
            if (InGroupMenu) RefreshGroups();
            if (triggerDown)
            {
                neutralSince = -1; Confirm(stick, now, true);
                if (!IsOpen) { TickHint(pointer, true, now); return true; }
            }
            if (IsOpen && stick.magnitude < .25f && !triggerHeld && !stickHeld)
            {
                if (neutralSince < 0) neutralSince = now;
                else if (now - neutralSince >= NeutralCloseDelay) Close();
            }
            else neutralSince = -1;
            TickHint(pointer, true, now);
            return true;
        }

        public void ToggleHints()
        {
            if(UpdatePlacementHintContext())placementHintsEnabled=!placementHintsEnabled;
            else HintsEnabled=!HintsEnabled;
        }
        private bool UpdatePlacementHintContext()
        {
            bool placing=Quest!=null&&Quest.Placing;
            if(placing!=placementHintContext)
            {
                placementHintContext=placing;
                if(placing)placementHintsEnabled=true;
                hintBindings=null;
            }
            return placing;
        }
        private bool ReplayHintContext=>Session!=null&&Session.IsReplay&&Panel!=null&&Panel.AcceptsReplayInput&&!Panel.IsDragging;
        public void TickHint(Ray pointer, bool available, float? now = null)
        {
            TickConfirmation(pointer, available, now ?? Time.unscaledTime);
            bool placing=UpdatePlacementHintContext(),replay=ReplayHintContext;
            bool visible = (placing?placementHintsEnabled:HintsEnabled) && available && (CanOpen||replay||placing)
                && Session!=null && !Session.HasSystemPause && !IsOpen;
            if (!visible) { if (hintRoot != null) hintRoot.gameObject.SetActive(false); return; }
            EnsureHint(); hintRoot.gameObject.SetActive(true);
            hintLabels[0].text=placing?"↑↓ Height":replay?"↑↓ Speed":"↑ Groups";
            hintLabels[1].text=placing?"→ Turn right":replay?"→ +15 seconds":"→ Selection";
            hintLabels[2].text=placing?"← Turn left":replay?"← −15 seconds":"← Unit orders";
            hintLabels[3].gameObject.SetActive(!placing&&!replay);
            hintLines[5].gameObject.SetActive(!placing&&!replay);
            string selection = (placing?"placement:":replay?"replay:":"")+string.Join(",", Session.Selection.OrderBy(id => id));
            if (!ReferenceEquals(hintView, Session.View) || selection != hintSelectionSignature)
            {
                hintView = Session.View; hintSelectionSignature = selection;
                if(placing||replay){hintSelection.color=hintOrders.color=Color.white;}
                else
                {
                    var actions = AvailableActions();
                    hintSelection.color = actions.Any(a => a.Category != ActionCategory.Orders) ? Color.white : Disabled;
                    hintOrders.color = actions.Any(a => a.Category == ActionCategory.Orders) ? Color.white : Disabled;
                }
            }
            LayoutHint(Quest.ActiveController == OVRInput.Controller.LTouch);
            RefreshButtonHints();
            Place(hintRoot, pointer, .00022f, .004f);
        }

        private void Open()
        {
            HideConfirmation(); EnsureView(); IsOpen = true; Page = WheelPage.Root; Highlighted = -1; stickReady = false; openReady = false;
            selectedIds = Session.Selection.OrderBy(id => id).ToArray(); openedMatch = Session.SessionIdentity;
            neutralSince = -1; actionSignature = null; root.gameObject.SetActive(true); RefreshActions();
        }

        public void Close()
        {
            IsOpen = false; Page = WheelPage.Root; Highlighted = -1; openedMatch = null; openReady = false;
            neutralSince = -1;
            // A successful receipt may outlive the wheel while the router drains Trigger.
            // TickHint clears it on context loss; opening a new wheel replaces it.
            if (root != null) root.gameObject.SetActive(false);
            if (hintRoot != null) hintRoot.gameObject.SetActive(false);
        }

        private bool ValidContext() => CanOpen && Session.SessionIdentity == openedMatch &&
            selectedIds.SequenceEqual(Session.Selection.OrderBy(id => id)) &&
            selectedIds.All(id => Session.View.entities.Any(e => e.id == id && e.owner == Session.View.player));

        private void Back(Vector2 stick)
        {
            HideConfirmation();
            if (Page == WheelPage.Root) { Close(); return; }
            Enter(WheelPage.Root, stick);
        }

        private void Enter(WheelPage page, Vector2 stick)
        {
            Page = page; actionSignature = null; Highlighted = -1; stickReady = false; neutralSince = -1;
            failedAction = failureMessage = null;
            RefreshActions();
            // Deliberate navigation preserves the held direction, including Back.
            // Unexpected action-list changes still invalidate a stale highlight.
            stickReady = true; Aim(stick);
        }

        private static SelectionAction Navigation(string id, string label, string description)
            => new SelectionAction { Id = id, Title = label, Label = label, Detail = description };

        // Refunds stay on the console so starting/finishing production never shifts sectors.
        private IEnumerable<SelectionAction> AvailableActions()
            => SelectionActions.For(Session).Items.Where(a => a.Id != "cancel");

        private IEnumerable<SelectionAction> QuickSelectActions()
        {
            int army=Session.Army.Count(),idle=Session.IdleWorkers.Count();
            yield return new SelectionAction{Id="all-army",Title="All army",Label="All army\n"+army+" units",
                Detail=army==0?"No army units available":army+" army units\nAcross the map",Execute=Session.SelectArmy};
            yield return new SelectionAction{Id="idle-workers",Title="Idle workers",Label="Idle workers\n"+idle+" available",
                Detail=idle==0?"No idle workers available":idle+" idle workers\nAcross the map",Execute=Session.SelectIdleWorkers};
        }

        private void RefreshActions()
        {
            if (InGroupMenu && actionSignature != null) { RefreshGroups(); return; }
            if (actionSignature != null && ReferenceEquals(actionView, Session.View)) return;
            actionView = Session.View;
            var available = AvailableActions().ToArray();
            var current = new List<SelectionAction>(); disabled.Clear();
            if (Page == WheelPage.Root)
            {
                current.Add(Navigation("groups-menu", "Groups", "Select a control group"));
                current.Add(Navigation("selection-menu", "Selection", "Build, train, research and abilities"));
                current.Add(Navigation("quick-select-menu", "Quick select", "Idle workers and all army units"));
                current.Add(Navigation("orders-menu", "Unit orders", "Attack, stop and hold position"));
                if (!available.Any(a => a.Category != ActionCategory.Orders)) disabled.Add("selection-menu");
                if (!available.Any(a => a.Category == ActionCategory.Orders)) disabled.Add("orders-menu");
            }
            else if (InGroupMenu)
            {
                for (int i = 0; i < GameSession.ControlGroupCount; i++)
                    current.Add(Navigation("group-" + (i + 1), "Group " + (i + 1), ""));
            }
            else if(Page==WheelPage.QuickSelect)
            {
                current.AddRange(QuickSelectActions());
                if(!Session.Army.Any())disabled.Add("all-army");
                if(!Session.IdleWorkers.Any())disabled.Add("idle-workers");
                if(failedAction!=null&&!disabled.Contains(failedAction))failedAction=failureMessage=null;
            }
            else
            {
                current.AddRange(available.Where(a => Page == WheelPage.Orders
                    ? a.Category == ActionCategory.Orders : a.Category != ActionCategory.Orders));
                current.Add(Navigation("back", "Back", "Return to the main wheel"));
            }
            string signature = Page + ":" + string.Join("|", current.Select(a => a.Id));
            if (signature == actionSignature)
            {
                // Resource, energy and cooldown changes retain the chosen sector.
                for (int i = 0; i < current.Count; i++) { items[i] = current[i]; sectorLabels[i].text=current[i].Label; }
                UpdateHighlight(); UpdateDescription(); return;
            }
            actionSignature = signature; items.Clear(); items.AddRange(current);
            failedAction = failureMessage = null;
            // Never remap a held direction when the available commands change.
            Highlighted = -1; stickReady = false; BuildSectors();
            var selection = Session.View.entities.Where(e => Session.Selection.Contains(e.id)).ToArray();
            heading.text = Page==WheelPage.QuickSelect ? "QUICK SELECT" : InGroupMenu ? "CONTROL GROUPS" : selection.Length == 1 ? Session.Catalog[selection[0].definition].name : selection.Length > 1 ? selection.Length + " selected items" : "COMMAND WHEEL";
            if (InGroupMenu) RefreshGroups();
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
            bool cardinal = Page == WheelPage.Root || InGroupMenu || Page==WheelPage.QuickSelect;
            int commands = cardinal ? items.Count : items.Count - 1;
            float step = cardinal ? 360f/items.Count : 270f / Mathf.Max(1, commands);
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
                // Quick select has two full half-rings: use their real horizontal
                // space so longer translated titles keep the count on its own line.
                float labelWidth = Page == WheelPage.QuickSelect ? 260 : cardinal || back ? 146 : Mathf.Clamp(434 * Mathf.Sin(step * .45f * Mathf.Deg2Rad), 80, 146);
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
                if (Highlighted >= 0) { Highlighted = -1; failedAction = failureMessage = null; UpdateHighlight(); UpdateDescription(); }
                return;
            }
            if (!stickReady || stick.magnitude < .45f || items.Count == 0) return;
            float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
            bool cardinal = Page == WheelPage.Root || InGroupMenu || Page==WheelPage.QuickSelect;
            float step = cardinal ? 360f/items.Count : 270f / Mathf.Max(1, items.Count - 1);
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
                var color = i == Highlighted ? (unavailable ? new Color(.15f, .22f, .26f) : Accent) : Normal;
                bool confirmed = confirmationStrength > 0 && confirmedPage == Page && confirmedId == items[i].Id;
                sectors[i].color = confirmed ? Color.Lerp(color, Confirmed, confirmationStrength) : color;
                sectorLabels[i].color = unavailable ? Disabled : i == Highlighted || confirmed ? new Color(.015f, .05f, .065f) : Color.white;
            }
        }

        private void UpdateDescription()
        {
            if (title == null) return;
            detail.color = Ink;
            if (InGroupMenu)
            {
                title.text = Highlighted >= 0 ? "GROUP " + (Highlighted + 1) : "CONTROL GROUPS";
                detail.text = Highlighted < 0 ? "Tilt to choose a group" : Session.GroupSize(Highlighted) + " assigned\n" + Session.Selection.Count + " selected";
                help.text = "Trigger · Select     Stick click · Assign\n" + Panel.Bindings.MenuBackName + " · Back     Center stick to hide";
                return;
            }
            title.text = Highlighted >= 0 ? items[Highlighted].Title : Page == WheelPage.Root ? "COMMANDS" : Page==WheelPage.QuickSelect ? "QUICK SELECT" : Page == WheelPage.Selection ? "SELECTION" : "UNIT ORDERS";
            detail.text = Highlighted >= 0 ? disabled.Contains(items[Highlighted].Id)&&Page!=WheelPage.QuickSelect ? "No actions for this selection" : items[Highlighted].Detail : "Tilt to choose\nTrigger to enter";
            help.text = Page == WheelPage.Root ? "Trigger · Enter     " + Panel.Bindings.MenuBackName + " · Close\nCenter stick to hide" : Page==WheelPage.QuickSelect ? "Trigger · Select     "+Panel.Bindings.MenuBackName+" · Back\nCenter stick to hide" : "Trigger · Confirm     Down · Back\nCenter stick to hide";
            if (failedAction != null && failedAction == HighlightedId) { detail.text = failureMessage; detail.color = Save; }
        }

        private void ConfirmGroup(int group, bool save, float now)
        {
            float angle = angles[group], sweep = sectors[group].Sweep;
            HideConfirmation(); Close(); Session.CancelMode(); Session.Group(group, save);
            if (save || Session.Selection.Count > 0)
                ShowConfirmation("group-" + (group + 1), "Group " + (group + 1) + (save ? " assigned" : " selected"), WheelPage.Groups, angle, sweep, now);
        }

        private void Confirm(Vector2 stick, float now, bool feedback)
        {
            if (Highlighted < 0) return;
            string id = items[Highlighted].Id, label = items[Highlighted].Title;
            float angle = angles[Highlighted], sweep = sectors[Highlighted].Sweep;
            var page = Page;
            if (feedback) HideConfirmation();
            if (disabled.Contains(id)&&Page!=WheelPage.QuickSelect) { detail.text = "No actions for this selection"; detail.color = Save; return; }
            if (InGroupMenu) { ConfirmGroup(Highlighted, false, now); return; }
            if (id == "back") Back(stick);
            else if (id == "groups-menu") Enter(WheelPage.Groups, stick);
            else if (id == "selection-menu") Enter(WheelPage.Selection, stick);
            else if (id == "orders-menu") Enter(WheelPage.Orders, stick);
            else if (id == "quick-select-menu") Enter(WheelPage.QuickSelect, stick);
            else
            {
                // Resolve again just before submitting; availability can change while aiming.
                var action = (Page==WheelPage.QuickSelect?QuickSelectActions():AvailableActions()).FirstOrDefault(a => a.Id == id);
                if (action == null) { actionSignature = null; RefreshActions(); return; }
                if (!action.Execute()) { failedAction = id; failureMessage = Session.Message; UpdateDescription(); return; }
                failedAction = failureMessage = null;
                // Keep Selection ready for another fresh Trigger press in the held direction.
                if (Page == WheelPage.Selection) { RefreshActions(); UpdateDescription(); }
                else Close();
            }
            if (feedback) ShowConfirmation(id, label, page, angle, sweep, now);
        }

        private void ShowConfirmation(string id, string label, WheelPage page, float angle, float sweep, float now)
        {
            EnsureConfirmation();
            confirmedId = id; confirmedPage = page; confirmedMatch = Session.SessionIdentity;
            confirmedSelection = Session.Selection.ToArray(); confirmationStarted = now; confirmationStrength = 1;
            confirmationLabel.text = label;
            confirmationArc.Angle = angle; confirmationArc.Sweep = sweep; confirmationArc.SetVerticesDirty();
            confirmationRoot.gameObject.SetActive(true); UpdateHighlight();
        }

        private void TickConfirmation(Ray pointer, bool available, float now)
        {
            if (confirmationStarted < 0) return;
            if (!available || !CanOpen || Session.SessionIdentity != confirmedMatch || !Session.Selection.SetEquals(confirmedSelection)
                || confirmedSelection.Any(id => !Session.View.entities.Any(e => e.id == id && e.owner == Session.View.player))
                || now - confirmationStarted >= ConfirmationDuration)
            { HideConfirmation(); return; }
            float age = Mathf.Max(0, now - confirmationStarted);
            confirmationStrength = 1 - Mathf.InverseLerp(.12f, ConfirmationDuration, age);
            Place(confirmationRoot, pointer, .00028f, .025f, faceViewer: true);
            // A closed wheel leaves only a small receipt. It never extends input ownership.
            confirmationArc.gameObject.SetActive(IsOpen && Page == confirmedPage);
            confirmationLabel.gameObject.SetActive(!IsOpen);
            confirmationGlyph.anchoredPosition = IsOpen ? new Vector2(0, 112) : Vector2.zero;
            confirmationGlyph.localScale = Vector3.one * (IsOpen ? .32f : 1);
            var color = Confirmed; color.a = confirmationStrength;
            confirmationArc.color = color; confirmationLabel.color = color;
            foreach (var stroke in confirmationCheck) stroke.color = color;
            confirmationBadge.color = new Color(.018f, .08f, .055f, .98f * confirmationStrength);
            UpdateHighlight();
        }

        private void HideConfirmation()
        {
            if (confirmationStarted < 0) return;
            confirmationStarted = -1; confirmationStrength = 0; confirmedId = null;
            if (confirmationRoot != null) confirmationRoot.gameObject.SetActive(false);
            UpdateHighlight();
        }

        private void EnsureConfirmation()
        {
            if (confirmationRoot != null) return;
            EnsureFont();
            confirmationRoot = Rect("Wheel confirmation", transform, Vector2.zero, new Vector2(640, 740));
            var canvas = confirmationRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 21;
            canvas.scaleFactor = 2; confirmationRoot.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            confirmationArc = Rect("Confirmed sector", confirmationRoot, Vector2.zero, new Vector2(620, 620)).gameObject.AddComponent<RadialSector>();
            confirmationArc.InnerRadius = 294; confirmationArc.OuterRadius = 305; confirmationArc.raycastTarget = false;
            confirmationGlyph = Rect("Confirmation check", confirmationRoot, Vector2.zero, new Vector2(130, 130));
            confirmationBadge = Rect("Confirmation badge", confirmationGlyph, Vector2.zero, new Vector2(130, 130)).gameObject.AddComponent<RadialSector>();
            confirmationBadge.InnerRadius = 0; confirmationBadge.OuterRadius = 62; confirmationBadge.Sweep = 360; confirmationBadge.raycastTarget = false;
            confirmationCheck = new Image[2];
            Stroke(0, new Vector2(-28, 1), new Vector2(-7, -20)); Stroke(1, new Vector2(-7, -20), new Vector2(31, 23));
            confirmationLabel = Label(confirmationRoot, "", new Vector2(0, -96), new Vector2(310, 60), 23, Confirmed);
            confirmationLabel.resizeTextForBestFit = true; confirmationLabel.resizeTextMinSize = 18; confirmationLabel.resizeTextMaxSize = 23;
            var outline = confirmationLabel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.015f, .035f, .05f, 1); outline.effectDistance = new Vector2(1, -1);
            void Stroke(int index, Vector2 from, Vector2 to)
            {
                var line = Rect("Check " + index, confirmationGlyph, (from + to) * .5f, new Vector2(Vector2.Distance(from, to), 9));
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                confirmationCheck[index] = line.gameObject.AddComponent<Image>(); confirmationCheck[index].raycastTarget = false;
            }
        }

        private void UpdatePose(Ray pointer) => Place(root, pointer, .00028f, .025f, faceViewer: true);

        private void Place(RectTransform view, Ray pointer, float scale, float height, bool faceViewer = false)
        {
            var rotation = Quest.TrackingSpace != null ? Quest.PointerRotation() : Quaternion.LookRotation(pointer.direction, Vector3.up);
            var surface = ControllerVisuals.StickSurface(Quest.ActiveController == OVRInput.Controller.LTouch);
            var normal = surface.rotation * Vector3.back;
            var position = pointer.origin + rotation * (surface.position + normal * height);
            var orientation = rotation * surface.rotation;
            // Keep the position tied to the hand. A bounded tilt makes the open
            // wheel readable without turning it into a freely rotating billboard.
            if (faceViewer && Quest.Camera != null)
            {
                var toViewer = Quest.Camera.transform.position - position;
                if (toViewer.sqrMagnitude > .0001f)
                {
                    var outward = orientation * Vector3.back;
                    var facing = Vector3.RotateTowards(outward, toViewer.normalized, 35 * Mathf.Deg2Rad, 0);
                    orientation = Quaternion.FromToRotation(outward, facing) * orientation;
                }
            }
            view.SetPositionAndRotation(position, orientation);
            view.localScale = Vector3.one * scale;
        }

        private void EnsureFont() { if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }

        private void EnsureHint()
        {
            if (hintRoot != null) return;
            EnsureFont(); hintRoot = Rect("Controller command hint", transform, Vector2.zero, new Vector2(1240, 460));
            var canvas = hintRoot.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 19;
            canvas.scaleFactor = 2; hintRoot.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            for (int i = 0; i < hintLines.Length; i++)
            {
                hintLines[i] = Rect("Hint leader " + i, hintRoot, Vector2.zero, Vector2.one);
                var line = hintLines[i].gameObject.AddComponent<Image>();
                line.color = new Color(Ink.r, Ink.g, Ink.b, .65f); line.raycastTarget = false;
            }
            hintLabels[0] = Label(hintRoot, "↑ Groups", Vector2.zero, new Vector2(210, 38), 24, Color.white);
            hintLabels[1] = hintSelection = Label(hintRoot, "→ Selection", Vector2.zero, new Vector2(210, 38), 24, Color.white);
            hintLabels[2] = hintOrders = Label(hintRoot, "← Unit orders", Vector2.zero, new Vector2(210, 38), 24, Color.white);
            hintLabels[3] = Label(hintRoot, "↓ Quick select", Vector2.zero, new Vector2(210, 38), 24, Color.white);
            for (int i = 0; i < buttonHints.Length; i++)
                buttonHints[i] = Label(hintRoot, "", Vector2.zero, new Vector2(i == 4 ? 250 : 350, 80), 22, Color.white);
            foreach (var label in hintLabels.Concat(buttonHints))
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.015f, .035f, .05f, 1);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }
        }

        private void LayoutHint(bool left)
        {
            if (hintLeft == left) return;
            hintLeft = left; hintBindings = null; float side = left ? -1 : 1;
            // Mirror the callout geometry, never the words or directional arrows.
            Line(0, new Vector3(side * 40, 0), new Vector3(side * 155, 0));
            Line(1, new Vector3(side * 155, -180), new Vector3(side * 155, 50));
            for (int i = 0; i < hintLabels.Length; i++)
            {
                float y = 50 - i * 50;
                hintLabels[i].rectTransform.anchoredPosition = new Vector2(side * 280, y);
                hintLabels[i].alignment = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                Line(i + 2, new Vector3(side * 155, y), new Vector3(side * 169, y));
            }
            buttonHints[4].rectTransform.anchoredPosition = new Vector2(side * 300, -180);
            buttonHints[4].alignment = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            Line(6, new Vector3(side * 155, -180), new Vector3(side * 169, -180));
            var surface = ControllerVisuals.StickSurface(left);
            var origin = surface.position + surface.rotation * Vector3.back * .004f;
            for (int i = 0; i < 4; i++)
            {
                float y = 180 - i * 120;
                buttonHints[i].rectTransform.anchoredPosition = new Vector2(-side * 385, y);
                buttonHints[i].alignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                var start = Quaternion.Inverse(surface.rotation) * (ControllerVisuals.ButtonPosition(left, hintInputs[i]) - origin) / .00022f;
                var elbow = new Vector3(-side * 170, y);
                Line(7 + i * 2, start, elbow);
                Line(8 + i * 2, elbow, new Vector3(-side * 200, y));
            }
            void Line(int index, Vector3 start, Vector3 end)
            {
                var delta = end - start; var line = hintLines[index];
                line.anchoredPosition3D = (start + end) * .5f;
                line.sizeDelta = new Vector2(delta.magnitude, 1.5f);
                line.localRotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
            }
        }

        private void RefreshButtonHints()
        {
            var bindings = Panel.Bindings;
            bool placing=Quest.Placing,replay=ReplayHintContext;
            string signature = (placing?"placement:":replay?"replay:":"")+string.Join("|", HotkeyBindings.Definitions(true).Select(d => bindings.Get(true, d.Id)));
            if (signature == hintBindings) return;
            hintBindings = signature;
            for (int i = 0; i < hintInputs.Length; i++)
            {
                if(placing)
                {
                    string buttonName=HotkeyBindings.Display(hintInputs[i],hintLeft==true);
                    string actionName=i==0?"Hold: enlarge board":i==1?"Cancel placement":i==2?"Place board":i==3?"Hold: shrink board":"Show / hide hints";
                    ((LocalizedText)buttonHints[i]).SetKey(i==4?"{0}\n{1}":"{0} · {1}",buttonName,actionName);continue;
                }
                if(replay)
                {
                    string buttonName=HotkeyBindings.Display(hintInputs[i],hintLeft==true);
                    string actionName=i==0?"Use replay panel":i==1?"Exit replay\nGrip: zoom out":i==2?"Play / pause\nGrip: zoom in":i==3?"Hold + stick: pan": "Show / hide hints";
                    ((LocalizedText)buttonHints[i]).SetKey(i==4?"{0}\n{1}":"{0} · {1}",buttonName,actionName);continue;
                }
                string id = HotkeyBindings.Definitions(true).First(d => bindings.Get(true, d.Id) == hintInputs[i]).Id;
                string action = id == "select" ? "Select / place\nHold: drag · Double: same type" :
                    id == "order" ? "Move / interact\nDouble: Attack Move" :
                    id == "add" ? "Hold: add / queue" :
                    id == "back" ? "Cancel / pause" : "Show / hide hints";
                string button = HotkeyBindings.Display(hintInputs[i], hintLeft == true);
                ((LocalizedText)buttonHints[i]).SetKey(i==4?"{0}\n{1}":"{0} · {1}",button,action);
            }
        }

        private void EnsureView()
        {
            if (root != null) return;
            EnsureFont();
            root = Rect("Controller action wheel", transform, Vector2.zero, new Vector2(640, 740));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 20;
            canvas.scaleFactor = 2; root.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            var center = Rect("Center", root, Vector2.zero, new Vector2(600, 600)).gameObject.AddComponent<RadialSector>();
            center.InnerRadius = 0; center.OuterRadius = 137; center.Sweep = 360; center.color = MenuPalette.Background; center.raycastTarget = false;
            slices = Rect("Sectors", root, Vector2.zero, new Vector2(600, 600));
            heading = Label(root, "", new Vector2(0, 328), new Vector2(560, 44), 29, MenuPalette.Ink);
            heading.fontStyle = FontStyle.Bold;
            title = Label(root, "", new Vector2(0, 44), new Vector2(222, 80), 25, MenuPalette.Ink);
            title.fontStyle = FontStyle.Bold;
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
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<LocalizedText>(); label.font = font; label.text = text;
            label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        private void OnDisable() { HideConfirmation(); Close(); if (hintRoot != null) hintRoot.gameObject.SetActive(false); }
    }
}
