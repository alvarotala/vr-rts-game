using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class ControllerHintPreview
    {
        [MenuItem("Frontier/Preview and check controller hints")]
        public static void Render()
        {
            using (var f = new UnifiedWheelFixture("ControllerHints"))
            {
                var host = new GameObject("Controller hints preview");
                try
                {
                    var hints = host.AddComponent<ControllerHints>();
                    hints.Session = f.Session; hints.Panel = f.Panel; hints.Quest = f.Quest; hints.Wheel = f.Wheel;
                    f.Input.Hints = hints;
                    var preview = UnifiedWheelFixture.Field<PreviewRenderUtility>(f, "preview"); preview.AddSingleGO(host);
                    f.Panel.Bindings.Reset(true); f.Panel.Bindings.SelectHand(false); f.Start("industrial");
                    f.Session.Select(f.Session.Match.Entities.Values.First(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "worker").id, false);
                    void Tick(bool available = true, bool overPanel = false) => hints.Tick(f.Ray, available, overPanel);
                    bool HasButton(string value) => Enumerable.Range(0, 5).Any(i => Contains(hints.ButtonAt(i), value));
                    bool HasRow(string button, string action) => Enumerable.Range(0, 5).Any(i => Contains(hints.ButtonAt(i), button) && Contains(hints.ActionAt(i), action));
                    void Capture(string name)
                    {
                        Tick();
                        foreach (var label in hints.GetComponentsInChildren<Text>())
                        {
                            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                            settings.resizeTextForBestFit = false;
                            settings.fontSize = label.resizeTextForBestFit ? label.resizeTextMinSize : label.fontSize;
                            float height = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                            f.Check(height <= label.rectTransform.rect.height + .5f, "Controller hint text fits: " + label.text);
                            using (var bounded = new TextGenerator()) using (var complete = new TextGenerator())
                            {
                                settings.verticalOverflow = VerticalWrapMode.Truncate; bounded.Populate(label.text, settings);
                                settings.verticalOverflow = VerticalWrapMode.Overflow; complete.Populate(label.text, settings);
                                f.Check(bounded.vertexCount == complete.vertexCount, "Controller hint retains every glyph: " + label.text);
                            }
                        }
                        HarvestingPreview.SaveCanvas(hints.transform, UnifiedWheelFixture.Field<RectTransform>(hints, "root"),
                            "TestResults/ControllerHints/" + name + ".png", preview, 1100, 650);
                    }

                    Tick(); f.Check(hints.IsVisible, "A controller hint stays visible during gameplay");
                    f.Check(HasRow("Trigger", "Select") && HasRow("A", "Move") && HasRow("B", "Pause") && HasButton("Grip") && HasButton("Stick"),
                        "Default gameplay hints explain Trigger, A, B, Grip and stick");
                    var root = UnifiedWheelFixture.Field<RectTransform>(hints, "root"); var position = root.position;
                    f.Ray = new Ray(f.Ray.origin + new Vector3(.12f, .04f, .03f), f.Ray.direction); Tick();
                    f.Check(root.position != position, "The controller card follows the pointing hand"); Capture("gameplay");
                    CaptureContext(f, hints, preview);
                    f.Panel.Bindings.SelectHand(true); Tick();
                    f.Check(HasRow("X", "Move") && HasRow("Y", "Pause"), "Left-hand hints use X and Y"); Capture("left-hand");
                    f.Panel.Bindings.SelectHand(false);
                    f.Check(f.Panel.Bindings.Assign(true, "select", "touch:a", true, out _), "Select can be remapped for hint validation"); Tick();
                    f.Check(HasRow("A", "Select") && HasRow("Trigger", "Move"), "Gameplay remaps update the hint on the next frame"); Capture("remapped");
                    f.Panel.Bindings.Reset(true); f.Session.ChooseAttackMove(); Tick();
                    f.Check(HasRow("Trigger", "Attack here") && HasRow("A", "Attack here") && HasRow("B", "Cancel"), "Targeting explains both confirmation inputs and cancel"); Capture("targeting");
                    f.Session.CancelMode(); f.Drain();
                    f.Panel.Bindings.Assign(true, "select", "touch:a", true, out _);
                    f.Panel.Bindings.Assign(true, "back", "touch:grip", true, out _);
                    f.OpenRoot(); Tick();
                    f.Check(hints.IsVisible && HasRow("Trigger", "Choose") && HasRow("B", "Close"), "Wheel navigation shows fixed physical Trigger and B despite gameplay remaps"); Capture("wheel");
                    CaptureContext(f, hints, preview, true);
                    f.Enter("groups-menu"); f.Panel.Bindings.Assign(true, "wheel", "touch:a", true, out _); Tick();
                    f.Check(HasRow("A", "Save") && HasButton("Trigger") && HasRow("B", "Back"), "Groups show the configured save modifier alongside fixed wheel navigation"); Capture("groups");
                    f.Dismiss(); f.Panel.Bindings.Reset(true); f.Panel.ToggleMenu(); Tick();
                    f.Check(f.Session.IsPaused && !f.Session.HasSystemPause && hints.IsVisible, "A user-opened pause menu keeps the hand hint visible");
                    f.Check(HasButton("Trigger") && HasButton("A") && HasRow("B", "Back"), "Menus explain their fixed confirm and back buttons"); Capture("menu");
                    f.Session.Pause("tracking", true); Tick();
                    f.Check(f.Session.HasSystemPause && !hints.IsVisible, "Tracking loss hides hints even while the menu also pauses the game");
                    f.Session.Pause("tracking", false); f.Panel.ToggleMenu(); f.Quest.BeginPlacement(); Tick();
                    f.Check(hints.IsVisible && HasButton("Trigger") && HasRow("B", "Cancel") && HasRow("Grip", "Height"), "Board placement keeps confirm, cancel and grip-height instructions visible"); Capture("placement");
                    f.Quest.CancelPlacement(); Tick(overPanel: true);
                    f.Check(HasButton("Trigger") && HasButton("A"), "Pointing at the console shows its physical confirm controls");
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", false);
                    f.Check(!hints.IsVisible, "Input focus loss immediately hides the controller hint");
                    Tick(); UnifiedWheelFixture.Call(f.Input, "LateUpdate");
                    f.Check(!hints.IsVisible, "The input caller keeps hints hidden on later frames without focus");
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", true); Tick();
                    UnifiedWheelFixture.Call(hints, "OnApplicationFocus", false); Tick();
                    f.Check(!hints.IsVisible, "The hint also guards its own focus lifecycle");
                    UnifiedWheelFixture.Call(hints, "OnApplicationFocus", true);
                    Tick(false); f.Check(!hints.IsVisible, "Unavailable input hides the hint");
                    Tick(); UnifiedWheelFixture.Call(hints, "OnDisable"); f.Check(!hints.IsVisible, "Disabling the hint hides its canvas immediately");
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest, false); Tick();
                    f.Check(!hints.IsVisible, "Desktop input never shows a floating controller card");
                    f.Report("CONTROLLER_HINTS");
                    Debug.Log("FRONTIER_CONTROLLER_HINT_VIEWS: 10 captures; no player build");
                }
                finally { Object.DestroyImmediate(host); }
            }
            UnifiedWheelPreview.Render();
        }

        private static void CaptureContext(UnifiedWheelFixture f, ControllerHints hints, PreviewRenderUtility preview, bool fullWheel = false)
        {
            preview.camera.transform.SetPositionAndRotation(new Vector3(0, fullWheel ? 1.47f : 1.43f, -.8f), Quaternion.identity);
            preview.camera.orthographic = true; preview.camera.orthographicSize = fullWheel ? .32f : .18f;
            f.Ray = new Ray(new Vector3(0, 1.45f, -.1f), Vector3.forward);
            if (fullWheel) f.Frame(Vector2.up);
            f.Wheel.TickHint(f.Ray, true); hints.Tick(f.Ray, true); Canvas.ForceUpdateCanvases();
            var meshes = new List<Mesh>(); var materials = new List<Material>();
            preview.BeginStaticPreview(new Rect(0, 0, 1000, 1000));
            foreach (var graphic in f.Wheel.GetComponentsInChildren<Graphic>().Concat(hints.GetComponentsInChildren<Graphic>()))
            {
                graphic.Rebuild(CanvasUpdate.PreRender); var source = graphic.canvasRenderer.GetMesh(); if (source == null) continue;
                var mesh = Object.Instantiate(source); meshes.Add(mesh); if (mesh.vertexCount == 0) continue;
                var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture }; materials.Add(material);
                // Match Canvas hierarchy order when manually submitting tilted transparent UI.
                material.renderQueue = 3000 + meshes.Count;
                if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                preview.DrawMesh(mesh, Matrix4x4.Translate(Vector3.back * .00001f * meshes.Count) * graphic.transform.localToWorldMatrix, material, 0);
            }
            preview.Render(true); var image = preview.EndStaticPreview();
            File.WriteAllBytes("TestResults/ControllerHints/" + (fullWheel ? "wheel-context" : "hand-context") + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh); foreach (var material in materials) Object.DestroyImmediate(material);
            f.Check((fullWheel ? f.Wheel.IsOpen : f.Wheel.HintVisible) && hints.IsVisible,
                fullWheel ? "The full wheel and button legend remain visible together" : "The direction hint and button legend remain visible together");
        }
        private static bool Contains(string text, string value) => text?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
