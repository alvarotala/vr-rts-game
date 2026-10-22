using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Networking;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class StartCountdownPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

        public static void Run()
        {
            Render();
            MenuFlowPreview.Render();
            ComfortAudioPreview.Render();
            MultiplayerValidation.RunSessionChecks();
            StartupMenuPreview.Render();
        }

        [MenuItem("Frontier/Preview match countdown")]
        public static void Render()
        {
            const string output = "TestResults/StartCountdown";
            string directory = Path.Combine(Path.GetTempPath(), "legion-countdown-" + Guid.NewGuid().ToString("N"));
            string language = Localization.Preference;
            bool hadEffects = PlayerPrefs.HasKey(AudioFeedback.EffectsKey);
            float effects = PlayerPrefs.GetFloat(AudioFeedback.EffectsKey);
            var preview = new PreviewRenderUtility();
            var fixture = new GameObject("Countdown fixture");
            var console = new GameObject("Countdown console");
            Camera createdCamera = null;
            CommanderInput input = null;
            int checks = 0;
            try
            {
                Directory.CreateDirectory(output);
                Localization.SetPreference("es");
                PlayerPrefs.SetFloat(AudioFeedback.EffectsKey, .5f);
                var session = fixture.AddComponent<GameSession>();
                if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("training");
                Set(session, "history", new MatchArchiveStore(directory)); Set(session, "archiveInEditor", true);
                var boardObject = new GameObject("Countdown board"); boardObject.transform.SetParent(fixture.transform);
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; Call(board, "Start"); board.Placed = true;
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                if (quest.Camera == null) Call(quest, "Awake"); createdCamera = quest.Camera;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest, false);
                preview.camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
                var panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest; Call(panel, "Start");
                preview.AddSingleGO(console);
                input = fixture.AddComponent<CommanderInput>(); input.Session = session; input.Board = board; input.Panel = panel; input.Quest = quest;
                var audio = fixture.AddComponent<AudioFeedback>(); audio.Session = session; audio.Board = board; Call(audio, "Start");
                var numbers = new List<int>(); session.CountdownChanged += numbers.Add;
                Refresh(); var root = Field<RectTransform>(panel, "root"); var center = root.position; var scale = root.localScale;
                quest.RequestStartMatch(); Refresh();
                Check(session.IsCountingDown && session.CountdownNumber == 3 && session.IsPaused && !session.HasSystemPause, "Real start flow begins at 3 without pretending the headset is interrupted");
                Check(panel.Page == CommandPanel.MenuPage.Actions && !panel.AcceptsGameInput && root.position == center && root.localScale == scale, "Countdown retains the common menu center and scale");
                var card = Field<RectTransform>(panel, "countdownCard");
                Check(card.position == root.position && card.gameObject.activeInHierarchy && !Field<RectTransform>(panel, "moveHandle").gameObject.activeSelf, "Only the centered countdown is visible, with no move control");
                Check(audio.LastCue == SoundCue.Countdown && Field<AudioSource>(audio, "ui").volume == .325f, "First number plays its own cue at the effects setting");
                Capture("countdown-3-es");
                var worker = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker");
                string initial = JsonUtility.ToJson(session.Match.View(0));
                for (int number = 3; number >= 1; number--)
                {
                    Check(session.CountdownNumber == number && Field<Text>(panel, "countdownLabel").text == number.ToString(), "Visible number " + number);
                    session.Select(worker.id, false); session.SelectRectangle(default, new Point(140, 140), false);
                    session.SelectSameTypeOnMap(worker.id, false); session.SelectIdleWorkers(); session.SelectArmy();
                    Check(session.Selection.Count == 0, "Selection shortcuts are blocked at " + number);
                    session.ChooseBuild(session.Catalog.Role(session.Faction, "supply").id); session.ChooseAttackMove();
                    Check(!session.ChooseAbility("missing") && !session.HasTargetingMode, "Targeting modes are blocked at " + number);
                    session.Selection.Add(worker.id); session.Group(0, true); session.Deselect(worker.id);
                    Check(session.GroupSize(0) == 0 && session.Selection.Contains(worker.id), "Group assignment and deselect are blocked at " + number);
                    foreach (CommandKind kind in Enum.GetValues(typeof(CommandKind))) Check(!session.Send(kind), "Order blocked: " + kind + " at " + number);
                    session.Selection.Clear();
                    var ray = new Ray(root.position - root.forward, root.forward);
                    panel.Point(ray, true, false); panel.Point(ray, false, true); panel.ToggleMenu(); panel.Back(); panel.Recall();
                    Check(panel.Page == CommandPanel.MenuPage.Actions && !panel.IsDragging && root.position == center, "Menu clicks, pause, back and recall stay disabled at " + number);
                    Check((bool)Call(input, "BlockCountdownInput", true), "Held input is consumed at " + number);
                    session.Match.Step();
                    Check(session.Match.Tick == 0 && JsonUtility.ToJson(session.Match.View(0)) == initial, "Economy and both players remain frozen at " + number);
                    if (number == 2)
                    {
                        session.Pause("headset", true); Advance(8);
                        Check(session.CountdownNumber == 2 && numbers.SequenceEqual(new[] { 2 }), "Headset interruption freezes the current number without repeating sounds");
                        session.Pause("headset", false);
                        quest.BeginPlacement(); Advance(8);
                        Check(session.CountdownNumber == 2 && !(bool)Call(input, "BlockCountdownInput", false), "Placement recovery is possible while gameplay stays paused");
                        quest.CancelPlacement(); Refresh();
                    }
                    Advance(4); Refresh();
                }
                Check(numbers.SequenceEqual(new[] { 2, 1, 0 }) && !session.IsCountingDown && !session.IsPaused && session.Match.Tick == 0, "Three full seconds complete in order without advancing the match clock");
                Check(audio.LastCue == SoundCue.Start && panel.AcceptsGameInput && !card.gameObject.activeSelf && Field<RectTransform>(panel, "moveHandle").gameObject.activeSelf, "Final cue restores the HUD and its move handle");
                Check(root.position == center && root.localScale == scale, "Restored HUD uses exactly the countdown center and scale");
                Check((bool)Call(input, "BlockCountdownInput", true), "Held buttons cannot leak into the first live frame");
                Check((bool)Call(input, "BlockCountdownInput", false) && !(bool)Call(input, "BlockCountdownInput", false), "Release frame is consumed before accepting fresh input");
                session.Select(worker.id, false);
                Check(session.Send(CommandKind.Hold), "Normal commands work after the count");
                session.Match.Step(); Check(session.Match.Tick == 1, "Simulation advances after the count");
                quest.RequestStartMatch(); Refresh();
                Check(session.CountdownNumber == 3 && session.Match.Tick == 0 && session.Selection.Count == 0, "Rematch restarts a fresh 3 instead of retaining countdown state");
                Advance(4); session.LeaveMatch(); Refresh();
                Check(!session.IsCountingDown && panel.Page == CommandPanel.MenuPage.Main && !card.gameObject.activeSelf, "Closing a countdown restores the main menu without a phantom completion");
                board.Placed = false; quest.RequestStartMatch();
                Check(quest.Placing && !session.Running && !session.IsCountingDown, "Countdown waits until board placement is confirmed");
                quest.CancelPlacement();
                var recording = session.History.List().First();
                Check(session.StartReplay(recording.Id) && !session.IsCountingDown && panel.Page == CommandPanel.MenuPage.Replay, "Replays keep their own transport without a start countdown");
                session.StopReplay();

                var server = fixture.AddComponent<NgoMatchServer>();
                var authority = new Simulation(session.Map, session.Catalog);
                typeof(NgoMatchServer).GetProperty("Match").SetValue(server, authority); Set(server, "startServerTime", 13d);
                Check((bool)Call(server, "AwaitingStart", 10d), "Authority waits on the common start timestamp");
                authority.Step(); Check(authority.Tick == 0 && !authority.Submit(new GameCommand { player = 0, kind = CommandKind.Surrender }).accepted, "Server simulation rejects even direct commands during countdown");
                Check((bool)Call(server, "AwaitingStart", 12.999d) && !(bool)Call(server, "AwaitingStart", 13d), "Server unlocks at the shared deadline");
                authority.Step(); Check(authority.Tick == 1, "Server advances only after the shared deadline");
                var clip = SoundPalette.Effect(SoundCue.Countdown); var samples = new float[clip.samples]; clip.GetData(samples, 0);
                Check(samples.Any(s => Mathf.Abs(s) > .01f) && samples.All(s => Mathf.Abs(s) < 1) && Mathf.Abs(samples[0]) < .001f && Mathf.Abs(samples.Last()) < .001f, "Countdown sound is audible, bounded and faded at both edges");
                Object.DestroyImmediate(clip);
                Debug.Log($"FRONTIER_START_COUNTDOWN passed: {checks} checks, 1 view; real start flow, input gate, audio source, replay exclusion and server deadline; no physical Quest or live transport implied");

                void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
                void Advance(int frames) { for (int i = 0; i < frames; i++) Call(session, "AdvanceCountdown", .25f); }
                void Refresh() { Set(panel, "nextText", 0f); Call(panel, "LateUpdate"); }
                void Capture(string name)
                {
                    var position = root.position; var rotation = root.rotation; var savedScale = root.localScale; var size = root.sizeDelta;
                    var eyePosition = preview.camera.transform.position; var eyeRotation = preview.camera.transform.rotation;
                    root.sizeDelta = new Vector2(480, 440);
                    HarvestingPreview.SaveCanvas(console.transform, root, output + "/" + name + ".png", preview, 960, 880);
                    root.sizeDelta = size; root.SetPositionAndRotation(position, rotation); root.localScale = savedScale;
                    preview.camera.transform.SetPositionAndRotation(eyePosition, eyeRotation);
                }
            }
            finally
            {
                if (input != null) Object.DestroyImmediate(input);
                Object.DestroyImmediate(console); Object.DestroyImmediate(fixture);
                if (createdCamera != null) Object.DestroyImmediate(createdCamera.gameObject);
                preview.Cleanup();
                Localization.SetPreference(language);
                if (hadEffects) PlayerPrefs.SetFloat(AudioFeedback.EffectsKey, effects); else PlayerPrefs.DeleteKey(AudioFeedback.EffectsKey);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
