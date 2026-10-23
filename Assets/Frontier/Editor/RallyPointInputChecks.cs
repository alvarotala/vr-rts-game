using System;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Uses the actual session, controller routing and shared actions; no XR device or player build.
    public static class RallyPointInputChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Check rally point input")]
        public static void Run()
        {
            var fixture = new GameObject("Rally input fixture"); var console = new GameObject("Rally input console");
            int checks = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake"); session.LoadMap("training");
                var board = fixture.AddComponent<BoardView>(); board.Session = session; Call(board, "Start");
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                var cameraObject = new GameObject("Rally input camera", typeof(Camera)); cameraObject.transform.SetParent(fixture.transform);
                cameraObject.transform.position = new Vector3(0, 1.7f, -2);
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, cameraObject.GetComponent<Camera>());
                var panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest; Call(panel, "Start");
                var input = fixture.AddComponent<CommanderInput>(); input.Session = session; input.Board = board; input.Quest = quest; input.Panel = panel;
                var wheel = fixture.AddComponent<ActionWheel>(); wheel.Session = session; wheel.Panel = panel; wheel.Quest = quest; input.Wheel = wheel;
                var first = new Point(20.5f, 15.5f); var second = new Point(25.5f, 15.5f); float clock = 100;

                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = faction; session.StartMatch(); session.Match.Players[0].minerals = 5000; session.Match.Players[0].catalyst = 5000;
                    var worker = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker");
                    var hq = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "hq");
                    var barracks = Add("barracks", 1001, new Point(22, 12));
                    var tech = Add("tech", 1002, new Point(22, 18));
                    var supply = Add("supply", 1003, new Point(20, 8));
                    Refresh();

                    foreach (var producer in new[] { hq, barracks, tech })
                    {
                        session.Select(producer.id, false); Reset();
                        Check(session.CanSetRallyPoint, "A completed " + session.Catalog[producer.definition].name + " accepts a rally point");
                        Check(!SelectionActions.For(session).Items.Any(a => a.Id == "clear-rally-point"), "An unset rally point has no Clear action");
                        var setRally = SelectionActions.For(session).Items.Single(a => a.Id == "set-rally-point");
                        Check(setRally.Execute() && session.RallyPointMode && session.HasTargetingMode && !producer.hasRallyPoint,
                            "The menu Set rally action waits for a separate map destination");
                        session.CancelMode();
                        Check(!session.RallyPointMode && !session.HasTargetingMode && !producer.hasRallyPoint,
                            "Cancelling rally targeting leaves the producer unchanged");
                        string role = session.Catalog[producer.definition].role == "hq" ? "worker" : session.Catalog[producer.definition].role == "tech" ? "siege" : "ranged";
                        Check(session.Send(CommandKind.Train, session.Catalog.Role(faction, role).id), "Producer has an actual paid training order");
                        int[] queue = producer.queue.Select(q => q.id).ToArray(); int minerals = session.View.minerals, gas = session.View.catalyst;

                        Call(input, "PointBoard", RayAt(first), false, false, true, false);
                        Check(producer.hasRallyPoint && producer.rallyPoint.Distance(first) < .01f, "A on the map sets the selected producer's destination");
                        Check(Field<float>(input, "lastActionPress") < 0, "Setting a rally point cannot seed a double-A attack gesture");
                        Call(input, "OrderClick", second, 0, 0, true, 10f);
                        Call(input, "OrderClick", new Point(second.x + .1f, second.y), 0, 0, true, 10.1f);
                        Check(producer.rallyPoint.Distance(second) < .01f, "Grip plus double A updates the single rally point at the chosen cell center");
                        Check(producer.order != CommandKind.AttackMove && producer.order != CommandKind.Attack && producer.pendingOrders.Count == 0, "Rally clicks never enqueue movement or combat on a building");
                        Check(session.Context(first, 0, 0, append: true, attack: true, replaceSequence: 999) > 0 && producer.rallyPoint.Distance(first) < .01f,
                            "Session routes producer input to rally even with attack and append modifiers");
                        Check(producer.queue.Select(q => q.id).SequenceEqual(queue) && session.View.minerals == minerals && session.View.catalyst == gas,
                            "Changing a rally point preserves production identity and paid resources");
                        Check(!session.Send(CommandKind.SetRallyPoint, position: new Point(-3, -3)) && producer.hasRallyPoint && producer.rallyPoint.Distance(first) < .01f,
                            "An invalid map destination preserves the previous rally point");

                        var clear = SelectionActions.For(session).Items.Single(a => a.Id == "clear-rally-point");
                        Check(clear.Category == ActionCategory.Cancel && clear.Execute() && !producer.hasRallyPoint, "Shared Clear rally action resets the destination");
                        Check(producer.queue.Select(q => q.id).SequenceEqual(queue), "Clear rally does not cancel a production queue item");
                        Check(!SelectionActions.For(session).Items.Any(a => a.Id == "clear-rally-point"), "Clear action disappears after reset");
                        Check(SelectionActions.For(session).Items.Any(a => a.Id == "set-rally-point"), "Set rally returns after clearing the destination");
                    }

                    session.Select(hq.id, false); session.Select(barracks.id, true); session.Select(supply.id, true); Reset();
                    Check(session.CanSetRallyPoint && session.Context(second, 0, 0) > 0,
                        "A building group can share one rally destination even with a nonproducer selected");
                    Check(hq.hasRallyPoint && barracks.hasRallyPoint && hq.rallyPoint.Distance(second) < .01f && barracks.rallyPoint.Distance(second) < .01f && !tech.hasRallyPoint && !supply.hasRallyPoint,
                        "A group rally changes exactly the selected eligible producers");
                    Check(SelectionActions.For(session).Items.Single(a => a.Id == "clear-rally-point").Execute() && !hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "The shared Clear action resets every selected producer");
                    Check(session.ChooseRallyPoint() && session.RallyPointMode, "A group can enter rally targeting");
                    session.Deselect(barracks.id);
                    Check(!session.RallyPointMode && !hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "Changing the target group cancels its armed rally destination");
                    Check(session.ChooseRallyPoint(), "Rally can be armed again after a selection change");
                    session.Select(worker.id, true);
                    Check(!session.RallyPointMode && !session.CanSetRallyPoint, "Adding a mobile unit cancels rally targeting and gives units priority");

                    session.Select(hq.id, false); session.Context(first, 0, 0); session.Select(worker.id, true);
                    Check(!session.CanSetRallyPoint, "A mixed building and mobile-unit selection does not silently become rally input");
                    Check(session.Context(second, 0, 0) > 0 && worker.order == CommandKind.Move,
                        "A map order moves the unit in a mixed selection");
                    Check(session.Context(first, 0, 0, append: true) > 0 && worker.pendingOrders.Count == 1,
                        "A mixed selection can append a unit movement order");
                    Check(session.Context(second, 0, 0, attack: true) > 0 && worker.order == CommandKind.AttackMove,
                        "An attack-move map order ignores the buildings in a mixed selection");
                    var enemyUnit = session.Match.Entities.Values.First(e => e.owner == 1 && session.Catalog[e.definition].role == "worker");
                    enemyUnit.position = new Point(worker.position.x + 2, worker.position.y);
                    Call(session.Match, "UpdateVision"); Refresh();
                    Check(session.Context(enemyUnit.position, enemyUnit.id, 0) > 0 && worker.order == CommandKind.Attack && worker.target == enemyUnit.id,
                        "A contextual enemy click attacks with the mobile part of a mixed selection");
                    var mixedActions = SelectionActions.For(session).Items;
                    Check(mixedActions.Any(a => a.Id == "attack-move") && mixedActions.Any(a => a.Category == ActionCategory.Build)
                        && !mixedActions.Any(a => a.Category == ActionCategory.Train || a.Category == ActionCategory.Research || a.Id.Contains("rally-point")),
                        "Mixed selection actions match the selected worker and omit building actions and rallies");
                    Check(mixedActions.Single(a => a.Id == "hold").Execute() && worker.order == CommandKind.Hold,
                        "The mixed-selection Hold menu action affects the mobile unit");
                    Check(mixedActions.Single(a => a.Id == "stop").Execute() && worker.order == CommandKind.Stop,
                        "The mixed-selection Stop menu action affects the mobile unit");
                    Refresh();
                    Check(panel.GetComponentsInChildren<RectTransform>().Any(t => t.name == "Attack Move" && t.gameObject.activeInHierarchy)
                        && !Field<RectTransform>(panel, "productionStrip").gameObject.activeSelf && Field<RectTransform>(panel, "orderPlanStrip").gameObject.activeSelf,
                        "Mixed HUD shows unit Options and queued orders, with no production overlap");
                    Reset(); WheelFrame(); WheelFrame(stick: Vector2.right);
                    Check(wheel.IsOpen && wheel.Page == WheelPage.Selection && wheel.Items.Any(a => a.Category == ActionCategory.Build)
                        && !wheel.Items.Any(a => a.Category == ActionCategory.Train || a.Id.Contains("rally-point")),
                        "The mixed-selection wheel exposes worker construction instead of building training or rallies");
                    Reset(); WheelFrame(); WheelFrame(stick: Vector2.left);
                    Check(wheel.IsOpen && wheel.Items.Any(a => a.Id == "attack-move") && wheel.Items.Any(a => a.Id == "hold"),
                        "The mixed-selection wheel retains mobile orders");
                    Check(hq.hasRallyPoint && hq.rallyPoint.Distance(first) < .01f && hq.order != CommandKind.Attack && hq.order != CommandKind.AttackMove
                        && hq.pendingOrders.Count == 0 && session.Selection.SetEquals(new[] { hq.id, worker.id }),
                        "Mixed orders preserve the producer's rally and the full visible selection");
                    Reset();
                    session.Select(hq.id, false); session.ClearRallyPoint();

                    // Rally input must not change the existing immediate/queued unit-order controls.
                    session.Select(worker.id, false); Reset(); session.Send(CommandKind.Stop);
                    Check(!session.CanSetRallyPoint, "A selected mobile unit does not enter rally input");
                    Call(input, "OrderClick", first, 0, 0, false, 20f);
                    Check(worker.order == CommandKind.Move && worker.destination.Distance(first) < 1.5f, "A still moves a selected unit");
                    Call(input, "OrderClick", second, 0, 0, true, 21f);
                    Check(worker.pendingOrders.Count == 1 && worker.pendingOrders[0].kind == CommandKind.Move, "Grip plus A still queues unit movement");
                    Call(input, "OrderClick", second, 0, 0, true, 21.1f);
                    Check(worker.pendingOrders.Count == 1 && worker.pendingOrders[0].kind == CommandKind.AttackMove, "Double A still converts that queued unit order to Attack Move");

                    session.Select(supply.id, false); Reset();
                    Check(!session.CanSetRallyPoint && !session.Send(CommandKind.SetRallyPoint, position: first) && !supply.hasRallyPoint,
                        "Nonproducing buildings cannot receive a rally point");
                    Check(!SelectionActions.For(session).Items.Any(a => a.Id.Contains("rally-point")) && !session.ChooseRallyPoint(),
                        "Nonproducers expose no rally actions or targeting mode");
                    barracks.complete = false; Refresh(); session.Select(barracks.id, false);
                    Check(!session.CanSetRallyPoint && !session.Send(CommandKind.SetRallyPoint, position: first) && !barracks.hasRallyPoint,
                        "An unfinished producer cannot receive a rally point");
                    session.Select(hq.id, true);
                    Check(session.CanSetRallyPoint && session.Context(first, 0, 0) > 0 && hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "A building group excludes unfinished producers from rally commands");
                    session.ClearRallyPoint();
                    barracks.complete = true; Refresh();
                    var enemy = session.Match.Entities.Values.First(e => e.owner == 1 && session.Catalog[e.definition].role == "hq");
                    session.Selection.Clear(); session.Selection.Add(enemy.id);
                    Check(!session.CanSetRallyPoint && !session.Send(CommandKind.SetRallyPoint, position: first) && !enemy.hasRallyPoint,
                        "An enemy producer cannot be redirected even by a stale or forged selection");

                    // The console A alias must consume its press/release without setting a new rally point.
                    session.Select(hq.id, false); session.Context(first, 0, 0); Refresh(); Reset();
                    var clearAction = SelectionActions.For(session).Items.Single(a => a.Id == "clear-rally-point");
                    Check(panel.GetComponentsInChildren<RectTransform>().Any(t => t.name == clearAction.Title && t.gameObject.activeInHierarchy),
                        "Options exposes rally clearing outside the wheel");
                    var card = panel.GetComponentsInChildren<RectTransform>().First(t => t.name == clearAction.Title && t.gameObject.activeInHierarchy);
                    var panelRay = new Ray(card.TransformPoint(card.rect.center) - card.forward, card.forward);
                    var production = hq.queue.Select(q => q.id).ToArray();
                    Check(PanelFrame(panelRay, true, false) && PanelFrame(panelRay, false, true), "Console A owns both Clear rally press and release");
                    Check(!hq.hasRallyPoint && hq.queue.Select(q => q.id).SequenceEqual(production), "Options clears rally and preserves production without leaking A to the map");
                    Check(Field<float>(input, "lastActionPress") < 0, "Console action leaves no pending double-A gesture");

                    // Setting from Options arms a mode, then a fresh map trigger confirms exactly the selected group.
                    session.Select(barracks.id, true); session.Select(supply.id, true); Refresh(); Reset();
                    var setAction = SelectionActions.For(session).Items.Single(a => a.Id == "set-rally-point");
                    var setCard = panel.GetComponentsInChildren<RectTransform>().First(t => t.name == setAction.Title && t.gameObject.activeInHierarchy);
                    var setRay = new Ray(setCard.TransformPoint(setCard.rect.center) - setCard.forward, setCard.forward);
                    var groupSelection = session.Selection.ToArray(); var barracksProduction = barracks.queue.Select(q => q.id).ToArray();
                    Check(PanelFrame(setRay, true, false) && PanelFrame(setRay, false, true) && session.RallyPointMode
                        && !hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "Options owns the Set rally A press and release without assigning a destination underneath the menu");
                    Call(input, "PointBoard", RayAt(second), true, false, false, false);
                    Check(!session.RallyPointMode && hq.hasRallyPoint && barracks.hasRallyPoint && !supply.hasRallyPoint
                        && hq.rallyPoint.Distance(second) < .01f && barracks.rallyPoint.Distance(second) < .01f,
                        "A separate map trigger confirms the menu rally destination for every selected producer");
                    Call(input, "PointBoard", RayAt(second), false, true, false, false);
                    Check(session.Selection.SetEquals(groupSelection) && hq.queue.Select(q => q.id).SequenceEqual(production)
                        && barracks.queue.Select(q => q.id).SequenceEqual(barracksProduction),
                        "The targeting trigger release preserves selection and every production queue");
                    Check(!SelectionActions.For(session).Items.Any(a => a.Id == "set-rally-point")
                        && SelectionActions.For(session).Items.Single(a => a.Id == "clear-rally-point").Execute(),
                        "Assigned group rallies replace Set with Clear in Options");
                    Refresh(); Reset();
                    setAction = SelectionActions.For(session).Items.Single(a => a.Id == "set-rally-point");
                    setCard = panel.GetComponentsInChildren<RectTransform>().First(t => t.name == setAction.Title && t.gameObject.activeInHierarchy);
                    setRay = new Ray(setCard.TransformPoint(setCard.rect.center) - setCard.forward, setCard.forward);
                    Check(PanelTriggerFrame(setRay, true, false) && PanelTriggerFrame(setRay, false, true)
                        && session.RallyPointMode && !hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "The menu Trigger alias also arms rally without leaking its confirmation to the map");
                    session.CancelMode();
                    Check(!session.HasTargetingMode && !hq.hasRallyPoint && !barracks.hasRallyPoint,
                        "Cancelling a menu rally keeps every selected producer at its default exit");
                    session.Select(hq.id, false); Refresh(); Reset();
                    WheelFrame(); WheelFrame(stick: Vector2.right);
                    Check(wheel.IsOpen && !wheel.Items.Any(a => a.Id.Contains("rally-point")),
                        "The producer wheel omits Set rally when no destination is assigned");
                    Reset();

                    // Reuse the real wheel route with a map ray underneath, so a leaked confirm is visible.
                    session.Context(first, 0, 0); Refresh(); Reset();
                    WheelFrame();
                    Check(WheelFrame(stick: Vector2.right) && wheel.IsOpen && wheel.Page==WheelPage.Selection && wheel.Highlighted>=0, "Right tilt directly opens producer selection actions with its direction highlighted");
                    Check(!wheel.Items.Any(a=>a.Id.Contains("rally-point")), "Producer wheel omits both rally actions even when a destination is set");
                    string train="train:"+worker.definition;var direction=wheel.DirectionFor(train);WheelFrame(stick: direction);
                    Check(wheel.HighlightedId==train, "Right stick still highlights training with an active rally point");
                    Check(WheelFrame(stick:direction,down:true,held:true)&&wheel.IsOpen&&hq.hasRallyPoint&&hq.rallyPoint.Equals(first)
                        &&hq.queue.Count==production.Length+1, "Wheel trigger trains one unit without changing the rally point");
                    Check(WheelFrame(stick:direction,held:true)&&WheelFrame(stick:direction,up:true)&&hq.hasRallyPoint&&hq.queue.Count==production.Length+1,
                        "Holding the trigger neither repeats training nor changes rally");
                    Reset();session.Context(first,0,0);
                    var priorIds=new System.Collections.Generic.HashSet<int>(session.Match.Entities.Keys);
                    int moveEvents=0;Point bornHeading=default;
                    Action<int,Point> heading=(id,target)=>{if(!priorIds.Contains(id)){moveEvents++;bornHeading=target;}};
                    session.UnitMoveOrdered+=heading;
                    hq.queue[0].remaining=1;session.Match.Step();Call(session,"RefreshView");
                    var born=session.Match.Entities.Values.Single(e=>!priorIds.Contains(e.id));
                    Check(born.order==CommandKind.Move&&moveEvents==1&&bornHeading.Distance(born.destination)<.001f,
                        "A newly trained unit publishes rally heading before its first movement step");
                    Call(session,"RefreshView");Check(moveEvents==1,"The same spawned rally does not reset facing on every view refresh");
                    session.UnitMoveOrdered-=heading;
                    Reset(); Check(session.ChooseRallyPoint(), "Rally can be armed before leaving the match"); session.LeaveMatch();
                    Check(!session.CanSetRallyPoint && !session.RallyPointMode, "Leaving a match disables and clears rally input");

                    EntityState Add(string role, int id, Point point)
                    {
                        var type = session.Catalog.Role(faction, role);
                        var entity = new EntityState { id = id, owner = 0, definition = type.id, health = type.health, position = point };
                        session.Match.Entities.Add(id, entity); session.Match.Map.Occupy(point, type.footprint, id); return entity;
                    }
                }
                Debug.Log($"FRONTIER_RALLY_INPUT passed: {checks} checks; no player build");

                Ray RayAt(Point point) => new Ray(board.Content.TransformPoint(new Vector3(point.x, 0, point.y)) + Vector3.up, Vector3.down);
                void Refresh()
                {
                    typeof(GameSession).GetProperty("View").SetValue(session, session.Match.View(0));
                    Array.Fill(session.View.visibility, (byte)2); Call(board, "LateUpdate"); Set(panel, "nextText", 0f); Call(panel, "LateUpdate");
                }
                void Reset()
                { panel.CloseSectionHelp(); wheel.Close(); Call(input, "ClearPointer"); Set(input, "panelReleasePending", false); Set(input, "wheelReleasePending", false); }
                bool PanelFrame(Ray ray, bool down, bool up)
                {
                    bool consumed = (bool)Call(input, "RoutePanel", ray, false, false, false, down, up, down, 0f);
                    if (!consumed) Call(input, "PointBoard", RayAt(second), false, false, down, false);
                    return consumed;
                }
                bool PanelTriggerFrame(Ray ray, bool down, bool up)
                {
                    bool consumed = (bool)Call(input, "RoutePanelInput", ray, down, up, down, false, false, false, 0f, false);
                    if (!consumed) Call(input, "PointBoard", RayAt(second), down, up, false, false);
                    return consumed;
                }
                bool WheelFrame(Vector2 stick = default, bool down = false, bool held = false, bool up = false)
                {
                    clock += .05f; var ray = RayAt(second);
                    bool consumed = (bool)Call(input, "RouteWheel", ray, stick, false, false, down, held||down, false, false, false, clock, false, false);
                    if (!consumed) Call(input, "PointBoard", ray, down, up, false, false);
                    return consumed;
                }
                void Check(bool condition, string message)
                { if (!condition) throw new Exception(message); checks++; Debug.Log("RALLY_INPUT_PASS: " + message); }
            }
            finally { Object.DestroyImmediate(console); Object.DestroyImmediate(fixture); }
        }
    }
}
