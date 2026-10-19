using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A read-only legend beside the active hand. The wheel's direction hint remains
    // separate, so opening it never removes the explanation of the physical buttons.
    public sealed class ControllerHints : MonoBehaviour
    {
        public GameSession Session;
        public CommandPanel Panel;
        public QuestBridge Quest;
        public ActionWheel Wheel;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public string Heading => heading == null ? "" : heading.text;
        public string ButtonAt(int index) => buttons[index] == null ? "" : buttons[index].text;
        public string ActionAt(int index) => actions[index] == null ? "" : actions[index].text;
        private RectTransform root;
        private Text heading;
        private readonly RectTransform[] rows = new RectTransform[5];
        private readonly Text[] buttons = new Text[5], actions = new Text[5];
        private Font font;
        private bool hasFocus = true;

        public void Tick(Ray pointer, bool available, bool overPanel = false)
        {
            if (!available || !hasFocus || !isActiveAndEnabled || Quest == null || !Quest.Active || Quest.Camera == null ||
                Session == null || Session.HasSystemPause || Panel == null || Panel.Bindings == null)
            { Hide(); return; }
            EnsureView();
            var keys = Panel.Bindings;
            bool wheel = Wheel != null && Wheel.IsOpen;
            string context;
            int count;
            if (Quest.Placing)
            {
                context = "PLACE BOARD"; count = 4;
                Row(0, keys.Name(true, "select"), "Place board");
                Row(1, "Stick", "Rotate / size");
                Row(2, keys.Name(true, "add"), "Hold + stick: height");
                Row(3, keys.Name(true, "back"), "Cancel");
            }
            else if (wheel)
            {
                context = "WHEEL"; count = Wheel.InGroupMenu ? 4 : 3;
                Row(0, "Trigger", Wheel.InGroupMenu ? "Recall group" : "Choose option");
                Row(1, keys.MenuBackName, Wheel.Page == WheelPage.Root ? "Close wheel" : "Back");
                Row(2, "Stick", "Choose direction");
                if (Wheel.InGroupMenu)
                {
                    Row(3, keys.Name(true, "wheel"), Wheel.GroupSaveMode ? "Release: save group" : "Hold + tilt: save");
                    if (Wheel.GroupSaveMode) Row(0, "Trigger", "Release save first");
                }
            }
            else if (Panel.IsDragging)
            {
                context = "MOVE MENU"; count = 4;
                Row(0, "Trigger", "Hold: move menu");
                Row(1, keys.MenuConfirmName, "Hold: move menu");
                Row(2, "Stick", "Menu distance");
                Row(3, keys.MenuBackName, Panel.AcceptsGameInput && Session.HasTargetingMode ? "Cancel" : "Back");
            }
            else if (!Panel.AcceptsGameInput || overPanel)
            {
                context = "MENU"; count = 3;
                Row(0, "Trigger", "Select");
                Row(1, keys.MenuConfirmName, "Select");
                Row(2, keys.MenuBackName, Panel.AcceptsGameInput && Session.HasTargetingMode ? "Cancel" : "Back");
            }
            else
            {
                context = "MAP"; count = 5;
                string target = Session.BuildDefinition != null ? "Place building" : Session.AttackMoveMode ? "Attack here" :
                    Session.AbilityDefinitionId != null ? "Cast here" : null;
                Row(0, keys.Name(true, "select"), target ?? "Select");
                Row(1, keys.Name(true, "order"), target ?? "Move / order");
                Row(2, keys.Name(true, "add"), "Add / queue");
                Row(3, keys.Name(true, "back"), target == null ? "Pause" : "Cancel");
                Row(4, "Stick", "Command wheel");
            }
            string title = keys.HandName.ToUpperInvariant() + " HAND · " + context;
            if (heading.text != title) heading.text = title;
            float height = 44 + count * 34;
            root.sizeDelta = new Vector2(420, height);
            heading.rectTransform.anchoredPosition = new Vector2(0, height * .5f - 22);
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].gameObject.SetActive(i < count);
                rows[i].anchoredPosition = new Vector2(0, height * .5f - 60 - i * 34);
                if (i >= count) { buttons[i].text = ""; actions[i].text = ""; }
            }
            root.gameObject.SetActive(true);
            var camera = Quest.Camera.transform;
            // Under the compact stick hint; move down while the full wheel is open.
            var position = pointer.GetPoint(.08f) + camera.up * (wheel ? -.21f : -.095f);
            var offset = position - camera.position;
            if (offset.magnitude < .26f) position = camera.position + (offset.sqrMagnitude > .001f ? offset.normalized : camera.forward) * .26f;
            root.SetPositionAndRotation(position, Quaternion.LookRotation(position - camera.position, camera.up));
            root.localScale = Vector3.one * .00038f;
        }

        public void Hide() { if (root != null) root.gameObject.SetActive(false); }

        private void Row(int index, string button, string action)
        {
            if (buttons[index].text != button) buttons[index].text = button;
            if (actions[index].text != action) actions[index].text = action;
        }

        private void EnsureView()
        {
            if (root != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = Rect("Controller button legend", transform, Vector2.zero, new Vector2(420, 214));
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 21;
            var background = root.gameObject.AddComponent<Image>(); background.color = new Color(.025f, .055f, .085f, .96f); background.raycastTarget = false;
            heading = Label(root, new Vector2(0, 85), new Vector2(392, 28), 18, new Color(.6f, .8f, .84f), TextAnchor.MiddleLeft);
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Rect("Button " + i, root, Vector2.zero, new Vector2(392, 30));
                var badge = Rect("Key", rows[i], new Vector2(-136, 0), new Vector2(120, 28));
                var image = badge.gameObject.AddComponent<Image>(); image.color = new Color(.09f, .25f, .30f); image.raycastTarget = false;
                buttons[i] = Label(badge, Vector2.zero, new Vector2(114, 28), 19, new Color(.55f, .95f, .95f), TextAnchor.MiddleCenter);
                actions[i] = Label(rows[i], new Vector2(65, 0), new Vector2(250, 30), 20, Color.white, TextAnchor.MiddleLeft);
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        private Text Label(Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var label = Rect("Label", parent, position, size).gameObject.AddComponent<Text>(); label.font = font;
            label.fontSize = fontSize; label.alignment = alignment; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        private void OnApplicationFocus(bool focused) { hasFocus = focused; if (!focused) Hide(); }
        private void OnDisable() => Hide();
    }
}
