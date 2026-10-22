using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Render the actual shared UI, then exercise its existing pointer targets.
    // This gallery complements MenuFlowPreview's complete navigation regression.
    public static class MenuStylePreview
    {
        public static void Validate()
        {
            Render();
            ValidateMenus();
        }
        public static void ValidateMenus()
        {
            LocalizationPreview.Run();
            SelectionGridPreview.Render();
            ValidateGameplay();
        }
        public static void ValidateGameplay()
        {
            ProductionQueuePreview.Render();
            UnifiedWheelPreview.Render();
        }
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Preview Little Legion menu style")]
        public static void Render()
        {
            const string output = "TestResults/MenuStyle";
            Directory.CreateDirectory(output);
            bool hadLanguage = PlayerPrefs.HasKey(Localization.PreferenceKey);
            string savedLanguage = PlayerPrefs.GetString(Localization.PreferenceKey);
            var bounds = Shader.GetGlobalVector("_FrontierBounds");
            var matrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            float lighting = Shader.GetGlobalFloat("_FrontierEnhancedLighting");
            var lightColor = Shader.GetGlobalColor("_FrontierLightColor");
            var preview = new PreviewRenderUtility();
            var fixture = new GameObject("Menu style session");
            var console = new GameObject("Menu style console");
            int checks = 0, captures = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("rift");
                var boardObject = new GameObject("Menu style board"); boardObject.transform.SetParent(fixture.transform, false);
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Call(board, "Start");
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, preview.camera);
                preview.camera.transform.position = new Vector3(0, 1.7f, -2);
                var panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest;
                panel.PlaceRequested = quest.BeginPlacement; Call(panel, "Start"); preview.AddSingleGO(console);

                foreach (string language in new[] { "en", "es" })
                {
                    Localization.SetPreference(language); Refresh();
                    Check(panel.Page == CommandPanel.MenuPage.Main && !session.Running, language + ": gallery starts at the real main menu");
                    foreach (string title in new[] { "New Match", "Multiplayer LAN", "Guides", "Settings", "Match history" })
                        Check(Target(title) != null, language + ": main menu retains target " + title);
                    Check(Target("New Match").rect.width > Target("Settings").rect.width * 1.5f,
                        language + ": the new-match action has the primary full-width hierarchy");
                    Check(panel.GetComponentsInChildren<MenuIcon>().Length >= 5, language + ": all five main actions have graphic icons");
                    Capture(language + "-main");
                    float mainLogoWidth = Field<RectTransform>(panel, "brandLogo").rect.width;
                    Hover("New Match", language + "-main-new-match-hover");
                    Hover("Settings", language + "-main-settings-hover");
                    Click("Guides", true); Capture(language + "-guides");
                    Click("Control guide", true);
                    Check(panel.Page == CommandPanel.MenuPage.Guide && !session.Running, language + ": decorative guide emblem opens the control guide");
                    Capture(language + "-control-guide"); Click("Back", true); Click("Back", true);
                    Click("Settings", true);
                    foreach (string tab in new[] { "Controls", "Audio", "Language", "Visuals" })
                    {
                        Click(tab, true);
                        Check(panel.Page == CommandPanel.MenuPage.Settings && !session.Running, language + ": styled " + tab + " settings do not create a match");
                        Capture(language + "-settings-" + tab.ToLowerInvariant());
                    }
                    Hover("Back", language + "-settings-back-hover"); Click("Back", true);

                    foreach (string faction in new[] { "industrial", "biological" })
                    {
                        // Click over the decorative icon itself, not just the button's
                        // empty center: the icon must not block the existing action.
                        Click("New Match", true);
                        Check(panel.Page == CommandPanel.MenuPage.MatchSetup, language + ": play icon forwards to the existing match setup");
                        session.Faction = faction; session.EnemyFaction = faction == "industrial" ? "biological" : "industrial";
                        Call(panel, "Rebuild"); Refresh();
                        if (faction == "industrial") Capture(language + "-new-match");
                        board.Placed = true; Click("START MATCH");
                        Check(session.Running && panel.AcceptsGameInput, language + "/" + faction + ": new match starts through the real button");
                        var worker = session.View.entities.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker");
                        session.Select(worker.id, false); Refresh();
                        Check(Field<RectTransform>(panel, "brandLogo").rect.width < mainLogoWidth,
                            language + "/" + faction + ": match HUD uses a compact version of the same logo");
                        Capture(language + "-" + faction + "-worker");
                        var hq = session.View.entities.First(e => e.owner == 0 && session.Catalog[e.definition].role == "hq");
                        session.Select(hq.id, false); Refresh(); Capture(language + "-" + faction + "-production");
                        var before = JsonUtility.ToJson(session.Match.Snapshot()); var selection = session.Selection.ToArray();
                        Click("Pause");
                        Check(session.IsPaused && panel.Page == CommandPanel.MenuPage.Pause, language + "/" + faction + ": styled pause menu preserves pause behavior");
                        Capture(language + "-" + faction + "-pause");
                        Click("Settings"); Click("Visuals"); Capture(language + "-" + faction + "-paused-visuals");
                        Check(session.IsPaused && session.Selection.SetEquals(selection), language + "/" + faction + ": visual menu preserves selection and pause");
                        Click("Back"); Click("Resume match");
                        Check(panel.AcceptsGameInput && session.Selection.SetEquals(selection)
                            && JsonUtility.ToJson(session.Match.Snapshot()) == before,
                            language + "/" + faction + ": resume preserves the actual match state");
                        session.LeaveMatch(); Refresh();
                    }
                }
                Debug.Log($"FRONTIER_MENU_STYLE passed: {checks} checks, {captures} panels; output {output}; no physical Quest validation");

                void Refresh()
                {
                    typeof(CommandPanel).GetField("nextText", Private).SetValue(panel, 0f);
                    Call(panel, "LateUpdate"); Canvas.ForceUpdateCanvases();
                }
                RectTransform Target(string name) => panel.GetComponentsInChildren<RectTransform>()
                    .FirstOrDefault(r => r.gameObject.activeInHierarchy && r.name == name);
                void Click(string name, bool overIcon = false)
                {
                    Refresh(); var rect = Target(name); Check(rect != null, "Existing menu target: " + name);
                    var icon = overIcon ? rect.GetComponentInChildren<MenuIcon>() : null;
                    if (overIcon) Check(icon != null, "The action exposes a decorative icon: " + name);
                    var surface = icon != null ? icon.rectTransform : rect;
                    var point = surface.TransformPoint(surface.rect.center); var ray = new Ray(point - rect.forward, rect.forward);
                    Check(panel.Point(ray, true, false) && panel.Point(ray, false, true), "Pointer accepts styled target: " + name); Refresh();
                }
                void Hover(string name, string captureName)
                {
                    Refresh(); var rect = Target(name); Check(rect != null, "Existing focus target: " + name);
                    var surface = rect.GetComponent<MenuSurface>(); Check(surface != null, "Focus uses the shared menu surface: " + name);
                    var page = panel.Page; var size = rect.sizeDelta; var position = rect.anchoredPosition;
                    var idleColor = surface.color;
                    var icon = rect.GetComponentInChildren<MenuIcon>();
                    Check(icon != null, "Focus passes through a decorative icon: " + name);
                    var point = icon.rectTransform.TransformPoint(icon.rectTransform.rect.center);
                    Check(panel.Point(new Ray(point - rect.forward, rect.forward), false, false), "Hover ray reaches the card through its icon: " + name);
                    for (int i = 0; i < 3; i++) Call(panel, "UpdateMenuFeedback", .05f);
                    Check(surface.State > .99f && surface.color != idleColor, "Hover reaches its illuminated state: " + name);
                    Check(Target(name) == rect && rect.sizeDelta == size && rect.anchoredPosition == position && panel.Page == page,
                        "Hover retains target bounds and does not navigate: " + name);
                    Capture(captureName);
                    var root = Field<RectTransform>(panel, "root");
                    var background = root.TransformPoint(new Vector3(root.rect.xMin + 12, root.rect.yMin + 12, 0));
                    Check(panel.Point(new Ray(background - root.forward, root.forward), false, false), "Panel background consumes the released hover ray: " + name);
                    for (int i = 0; i < 3; i++) Call(panel, "UpdateMenuFeedback", .05f);
                    Check(surface.State < .001f && surface.color == idleColor && panel.Page == page,
                        "Leaving the card restores the idle appearance without navigation: " + name);
                    Capture(captureName + "-restored");
                }
                void Capture(string name)
                {
                    Refresh(); var logos = panel.GetComponentsInChildren<LegionLogo>();
                    Check(logos.Length == 1, name + ": exactly one branded logo is visible");
                    var logo = logos[0];
                    Check(!logo.raycastTarget && logo.rectTransform.rect.width > 0 && logo.rectTransform.rect.height > 0,
                        name + ": logo is visible decoration, not a ray target");
                    logo.Rebuild(CanvasUpdate.PreRender); var mesh = logo.canvasRenderer.GetMesh();
                    Check(mesh != null && mesh.vertexCount > 24, name + ": wordmark generates real vector geometry");
                    var icons = panel.GetComponentsInChildren<MenuIcon>();
                    Check(icons.All(i => !i.raycastTarget), name + ": icons cannot intercept UI rays");
                    var targets = (IEnumerable)Field<object>(panel, "targets"); bool decorationsAreTargets = false;
                    foreach (var target in targets)
                    {
                        var rect = (RectTransform)target.GetType().GetField("rect").GetValue(target);
                        decorationsAreTargets |= rect == logo.rectTransform || icons.Any(icon => icon.rectTransform == rect);
                    }
                    Check(!decorationsAreTargets, name + ": logo and icons add no separate command targets");
                    foreach (var label in panel.GetComponentsInChildren<Text>().Where(t => !t.resizeTextForBestFit))
                        Check(label.preferredHeight <= label.rectTransform.rect.height + .5f, name + ": text fits: " + label.text);
                    HarvestingPreview.SavePanel(panel, output + "/" + name + ".png", preview); captures++;
                }
                void Check(bool condition, string message)
                {
                    if (!condition) throw new Exception(message); checks++; Debug.Log("MENU_STYLE_PASS: " + message);
                }
            }
            finally
            {
                Object.DestroyImmediate(console); Object.DestroyImmediate(fixture); preview.Cleanup();
                if (hadLanguage) PlayerPrefs.SetString(Localization.PreferenceKey, savedLanguage); else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                PlayerPrefs.Save(); Localization.ReloadPreference();
                Shader.SetGlobalVector("_FrontierBounds", bounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", matrix);
                Shader.SetGlobalFloat("_FrontierEnhancedLighting", lighting); Shader.SetGlobalColor("_FrontierLightColor", lightColor);
            }
        }
    }
}
