using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class WindowScalePreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string ScalePrefix = "frontier.panel-scale.v1.", PlacementKey = "frontier.panel-placement.v1";
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Preview independent window scaling")]
        public static void Render()
        {
            const string output = "TestResults/WindowScale";
            var scaleKeys = Enum.GetValues(typeof(CommandPanel.MenuPage)).Cast<CommandPanel.MenuPage>().Select(page => ScalePrefix + page).ToArray();
            var savedScales = scaleKeys.ToDictionary(key => key, key => (exists: PlayerPrefs.HasKey(key), value: PlayerPrefs.GetFloat(key)));
            bool hadPlacement = PlayerPrefs.HasKey(PlacementKey); string placement = PlayerPrefs.GetString(PlacementKey, "");
            string language = Localization.Preference;
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Window scaling fixture");
            GameObject console = null; Camera createdCamera = null;
            int checks = 0;
            try
            {
                Directory.CreateDirectory(output);
                foreach (string key in scaleKeys) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(PlacementKey); Localization.SetPreference("en");
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake"); session.LoadMap("training");
                var boardObject = new GameObject("Window scaling board"); boardObject.transform.SetParent(fixture.transform);
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; Call(board, "Start"); board.Width = 1.4f; board.Fit(); board.Placed = true;
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                if (quest.Camera == null) Call(quest, "Awake"); createdCamera = quest.Camera;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest, true);
                preview.camera.transform.SetPositionAndRotation(new Vector3(2, 1.6f, -3), Quaternion.Euler(0, 30, 0));
                var panel = CreatePanel(); var root = Field<RectTransform>(panel, "root"); Refresh();
                Vector3 commonCenter = root.position; float baseline = Field<float>(panel, "baseWindowScale");
                Check(Near(root.localScale.x, baseline), "Uncustomized main menu starts at the common default scale");
                Check(scaleKeys.All(key => !PlayerPrefs.HasKey(key)), "Opening a menu does not overwrite scale preferences");
                Scale(1, 20); CheckFactor(1, "Stick input cannot scale an ungrabbed menu");
                CheckGrip();

                var mainRay = BeginDrag();
                foreach (float axis in new[] { 0f, .1f, -.1f }) Scale(axis, 10);
                CheckFactor(1, "Stick deadzone prevents scale drift while holding the window");
                Scale(.8f, 12); float mainScale = Factor();
                Check(mainScale > 1.1f && mainScale < 1.6f, "Stick right grows the grabbed window gradually");
                CheckCenter("Scaling the main menu");
                panel.Point(mainRay, false, true); Refresh();
                Check(!panel.IsDragging && Near(PlayerPrefs.GetFloat(ScalePrefix + CommandPanel.MenuPage.Main), mainScale), "Releasing the grab persists the main menu scale");
                Check(!PlayerPrefs.HasKey(ScalePrefix + CommandPanel.MenuPage.Settings), "Main menu scaling does not write another page's preference");

                Show(CommandPanel.MenuPage.Settings); CheckFactor(1, "Settings keeps its default scale after growing the main menu");
                BeginDrag(); Scale(-1, 100); CheckFactor(.65f, "Stick left stops at the minimum scale");
                Scale(1, 150); CheckFactor(1.6f, "Stick right stops at the maximum scale");
                Scale(-1, 15); float settingsScale = Factor();
                Check(!Near(settingsScale, mainScale), "Settings has an independent chosen scale");
                Show(CommandPanel.MenuPage.Main);
                Check(!panel.IsDragging && Near(PlayerPrefs.GetFloat(ScalePrefix + CommandPanel.MenuPage.Settings), settingsScale), "Changing page while dragging saves the page being left");
                CheckFactor(mainScale, "Returning to the main menu restores its own scale");
                Show(CommandPanel.MenuPage.Settings); CheckFactor(settingsScale, "Returning to Settings restores its own scale");

                BeginDrag(); Scale(.7f, 6); settingsScale = Factor();
                Call(panel, "OnApplicationFocus", false); Call(panel, "OnApplicationFocus", true); Refresh();
                Check(!panel.IsDragging && Near(PlayerPrefs.GetFloat(ScalePrefix + CommandPanel.MenuPage.Settings), settingsScale), "Losing focus ends a grab and saves the current page scale");
                CheckCenter("Focus recovery");
                Show(CommandPanel.MenuPage.Main); var interruptedRay = BeginDrag(); Scale(-.5f, 10); mainScale = Factor();
                Call(panel, "UpdatePosition", false);
                Check(!panel.IsDragging && !root.gameObject.activeSelf && Near(PlayerPrefs.GetFloat(ScalePrefix + CommandPanel.MenuPage.Main), mainScale), "Tracking loss hides the menu, ends its grab and preserves the scale");
                Refresh(); CheckFactor(mainScale, "Tracking recovery restores the saved main menu scale");
                panel.Point(new Ray(interruptedRay.origin + root.right * .6f, interruptedRay.direction), false, true); Refresh();
                CheckCenter("Stale release after tracking recovery");

                session.StartMatch(); Refresh(); CheckFactor(1, "The in-game window starts at its own default scale");
                var gameRay = BeginDrag(); Scale(1, 8); float gameScale = Factor(); panel.Point(gameRay, false, true); Refresh();
                panel.ToggleMenu(); Refresh(); CheckFactor(1, "Pause has an independent default scale");
                var pauseRay = BeginDrag(); Scale(-.7f, 10); float pauseScale = Factor(); panel.Point(pauseRay, false, true); Refresh();
                panel.Back(); Refresh(); CheckFactor(gameScale, "Returning from Pause restores the in-game window scale");
                session.LeaveMatch(); Refresh(); CheckFactor(mainScale, "Leaving the match restores the main menu scale");
                CheckCenter("Main, settings, in-game and pause transitions");

                Object.DestroyImmediate(console); console = null;
                panel = CreatePanel(); root = Field<RectTransform>(panel, "root"); Refresh();
                CheckFactor(mainScale, "Recreating the console restores the main menu scale from preferences");
                CheckCenter("Restoring shared placement with independently saved sizes");
                Show(CommandPanel.MenuPage.Settings); CheckFactor(settingsScale, "A recreated Settings window restores its saved scale");
                session.StartMatch(); Refresh(); CheckFactor(gameScale, "A recreated in-game window restores its saved scale");
                panel.ToggleMenu(); Refresh(); CheckFactor(pauseScale, "A recreated Pause window restores its saved scale");
                float beforeBoardResize = root.localScale.x; board.Width = .7f; board.Fit(); Refresh();
                Check(Near(root.localScale.x, beforeBoardResize), "Board resizing cannot change a window's chosen scale");
                session.LeaveMatch(); Refresh(); Localization.SetPreference("es");
                panel.Point(GripRay(), false, false); Canvas.ForceUpdateCanvases();
                var pose = root.localToWorldMatrix;
                preview.camera.orthographic = false; preview.camera.fieldOfView = 65; preview.camera.nearClipPlane = .02f; preview.camera.farClipPlane = 30;
                HarvestingPreview.SaveCanvas(panel.transform, root, output + "/move-window-es.png", preview, 1400, 1100, preservePose: true);
                Check(root.localToWorldMatrix == pose, "Capturing the move affordance preserves its physical pose and scale");
                Debug.Log($"FRONTIER_WINDOW_SCALE passed: {checks} checks, 1 view; synthetic pointer and stick values, no physical Quest validation");

                CommandPanel CreatePanel()
                {
                    console = new GameObject("Window scaling console"); preview.AddSingleGO(console);
                    var created = console.AddComponent<CommandPanel>(); created.Session = session; created.Board = board; created.Quest = quest;
                    created.PlaceRequested = quest.BeginPlacement; Call(created, "Start"); return created;
                }
                void Refresh() { typeof(CommandPanel).GetField("nextText", Private).SetValue(panel, 0f); Call(panel, "LateUpdate"); }
                void Show(CommandPanel.MenuPage page) { Call(panel, "Show", page); Refresh(); CheckCenter("Opening " + page); }
                float Factor() => root.localScale.x / baseline;
                void Scale(float axis, int frames) { for (int i = 0; i < frames; i++) Call(panel, "AdjustWindowScale", axis, .05f); }
                void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; Debug.Log("WINDOW_SCALE_PASS: " + message); }
                void CheckFactor(float value, string message) => Check(Near(Factor(), value) && Near(Field<float>(panel, "windowScale"), root.localScale.x), message);
                void CheckCenter(string context) => Check(Vector3.Distance(root.position, commonCenter) < .0001f && root.rect.center.sqrMagnitude < .0001f, context + " preserves the common world center");
                Ray GripRay()
                {
                    var grip = Field<RectTransform>(panel, "moveHandle"); var point = grip.TransformPoint(grip.rect.center);
                    return new Ray(point - grip.forward * .6f, grip.forward);
                }
                Ray BeginDrag()
                {
                    var ray = GripRay(); Check(panel.Point(ray, true, false) && panel.IsDragging, "The detached grip starts a window drag"); return ray;
                }
                void CheckGrip()
                {
                    var image = Field<Image>(panel, "moveAffordanceImage"); var idleTint = image.color;
                    var caption = Field<Text>(panel, "moveCaption"); var hints = Field<Text>(panel, "moveHints");
                    Check(!caption.gameObject.activeSelf && !hints.gameObject.activeSelf, "Resting grip shows only its short line");
                    panel.Point(GripRay(), false, false); Canvas.ForceUpdateCanvases();
                    Check(image.color == idleTint && caption.gameObject.activeSelf && ((LocalizedText)caption).Source == "Move window", "Hovered Move window pill keeps the line's color");
                    var grip = Field<RectTransform>(panel, "moveHandle");
                    var pillBounds = RelativeBounds(grip, image.rectTransform); var hintBounds = RelativeBounds(grip, hints.rectTransform);
                    Check(hints.gameObject.activeSelf && hints.transform.parent == grip && !string.IsNullOrWhiteSpace(hints.text)
                        && hintBounds.yMax < pillBounds.yMin && !hintBounds.Overlaps(pillBounds), "Movement hints sit below and outside the move pill");
                    Check(!hints.raycastTarget && hints.preferredHeight <= hints.rectTransform.rect.height + .5f, "Move hints fit and cannot steal pointer interaction");
                    panel.Point(new Ray(root.position + root.right * 5 - root.forward, root.forward), false, false);
                    Check(!caption.gameObject.activeSelf && !hints.gameObject.activeSelf, "Leaving the grip hides its caption and movement hints");
                }
            }
            finally
            {
                if (console != null) Object.DestroyImmediate(console); Object.DestroyImmediate(fixture);
                if (createdCamera != null) Object.DestroyImmediate(createdCamera.gameObject); preview.Cleanup();
                foreach (var item in savedScales) { if (item.Value.exists) PlayerPrefs.SetFloat(item.Key, item.Value.value); else PlayerPrefs.DeleteKey(item.Key); }
                if (hadPlacement) PlayerPrefs.SetString(PlacementKey, placement); else PlayerPrefs.DeleteKey(PlacementKey);
                Localization.SetPreference(language); PlayerPrefs.Save();
            }
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        private static Rect RelativeBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
    }
}
