using TMPro;
using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Frontier.Editor
{
    public static class QuickSelectionPreview
    {
        [MenuItem("Frontier/Check quick unit selection")]
        public static void Render()
        {
            using(var f=new UnifiedWheelFixture("QuickSelection"))
            {
                f.Quest.Bindings=f.Panel.Bindings;
                foreach(string faction in new[]{"industrial","biological"})
                foreach(bool left in new[]{false,true})
                {
                    f.Panel.Bindings.SelectHand(left);f.Start(faction);
                    string prefix=faction+(left?"-left":"-right");
                    var workers=f.Session.Match.Entities.Values.Where(e=>e.owner==0&&f.Session.Catalog[e.definition].role=="worker").OrderBy(e=>e.id).ToArray();
                    var hq=f.Session.Match.Entities.Values.First(e=>e.owner==0&&f.Session.Catalog[e.definition].role=="hq");
                    var orders=new[]{CommandKind.Stop,CommandKind.Hold,CommandKind.Move,CommandKind.Gather,CommandKind.Build,CommandKind.Repair};
                    for(int i=0;i<workers.Length;i++)workers[i].order=orders[i];
                    var queued=f.Add("worker",1000,new Point(12,12));queued.pendingOrders.Add(new UnitOrder{kind=CommandKind.Move,position=new Point(15,15)});
                    var delivering=f.Add("worker",1001,new Point(12,13));delivering.order=CommandKind.Gather;delivering.cargo=8;
                    var army=new[]{"scout","melee","ranged","siege","caster"}.Select((role,i)=>f.Add(role,1100+i,
                        new Point(f.Session.Map.width-4-i,f.Session.Map.height-4))).ToArray();
                    foreach(var unit in army){unit.order=CommandKind.AttackMove;unit.destination=new Point(20,20);}
                    var dead=f.Add("ranged",1200,new Point(15,15));dead.health=0;
                    var unfinished=f.Add("caster",1201,new Point(15,16));unfinished.complete=false;
                    f.Refresh();
                    f.Check(f.Session.IdleWorkers.Select(e=>e.id).OrderBy(id=>id).SequenceEqual(workers.Take(2).Select(e=>e.id)),
                        "Idle workers exclude walking, mining, building, repairing, returning cargo and queued work");
                    f.Check(f.Session.Army.Select(e=>e.id).OrderBy(id=>id).SequenceEqual(army.Select(e=>e.id)),
                        "Army includes all five military roles across the map, excluding workers, structures, dead and incomplete units");
                    f.Session.Select(0,false,f.Session.View.resources.First().id);
                    f.Session.ChooseBuild(f.Session.Catalog.Role(faction,"supply").id);
                    int selectedResource=f.Session.SelectedResource;
                    f.Dismiss();f.Frame(Vector2.down);
                    f.Check(f.Wheel.Page==WheelPage.QuickSelect&&f.Wheel.HighlightedId=="idle-workers"&&f.Session.Selection.Count==0&&f.Session.SelectedResource==selectedResource,
                        "Down enters Quick select with idle workers already aimed, without selecting or clearing the pending blueprint");
                    f.Check(f.Wheel.Items.Single(a=>a.Id=="idle-workers").Label.Contains("2 available")&&f.Wheel.Items.Single(a=>a.Id=="all-army").Label.Contains("5 units"),
                        "Both choices show current totals before confirmation");
                    f.Capture(prefix+"-idle-workers");
                    f.Frame(Vector2.down,down:true,held:true);
                    f.Check(!f.Wheel.IsOpen&&f.Session.Selection.SetEquals(workers.Take(2).Select(e=>e.id))&&f.Session.BuildDefinition==null&&f.Session.SelectedResource==0,
                        "Trigger selects idle workers without recentering, closes the wheel and clears targeting/resource selection");
                    f.Check(UnifiedWheelFixture.Field<RectTransform>(f.Wheel,"confirmationRoot").gameObject.activeSelf,
                        "Quick selection receives visible confirmation");
                    f.Check(f.Frame(Vector2.down,held:true)&&f.Frame(Vector2.down,up:true)&&f.Session.Selection.SetEquals(workers.Take(2).Select(e=>e.id)),
                        "Held trigger and release cannot leak into map selection");
                    f.Check(workers.Select(e=>e.order).SequenceEqual(orders)&&queued.pendingOrders.Count==1,
                        "Quick selection never issues or clears a unit's orders");
                    f.Dismiss();f.Frame(Vector2.down);f.Frame(Vector2.up);
                    f.Check(f.Wheel.HighlightedId=="all-army","Sweep straight up chooses the army without returning to center");
                    f.Capture(prefix+"-all-army");f.Frame(Vector2.up,down:true,held:true);
                    f.Check(f.Session.Selection.SetEquals(army.Select(e=>e.id))&&army.All(e=>e.order==CommandKind.AttackMove),
                        "Confirm selects the complete army and leaves every combat order intact");
                    f.Frame(Vector2.up,held:true);f.Frame(Vector2.up,up:true);f.Frame(Vector2.up);
                    f.Check(!f.Wheel.IsOpen&&f.Session.Selection.SetEquals(army.Select(e=>e.id)),"A held direction after confirming cannot reopen Groups");

                    f.Dismiss();f.Session.Select(hq.id,false);f.Frame(Vector2.down);
                    workers[0].order=CommandKind.Move;f.Refresh();f.Frame(Vector2.down);
                    f.Check(f.Wheel.HighlightedId=="idle-workers"&&f.Wheel.Items.Single(a=>a.Id=="idle-workers").Label.Contains("1 available"),
                        "Starting work refreshes the count without moving the highlighted choice");
                    workers[1].health=0;f.Refresh();f.Frame(Vector2.down);
                    int failures=0;System.Action<bool> feedback=ok=>{if(!ok)failures++;};f.Session.Feedback+=feedback;
                    f.Frame(Vector2.down,down:true,held:true);f.Session.Feedback-=feedback;
                    f.Check(f.Wheel.IsOpen&&f.Session.Selection.SetEquals(new[]{hq.id})&&f.Session.Message=="No idle workers available."&&failures==1,
                        "If the last idle worker disappears before Trigger, preserve selection and report the empty result");
                    f.Frame(Vector2.down,up:true);f.Capture(prefix+"-no-idle-workers");
                    workers[0].order=CommandKind.Stop;f.Refresh();f.Frame(Vector2.down);
                    f.Check(!UnifiedWheelFixture.Field<Text>(f.Wheel,"detail").text.Contains("No idle"),"A newly idle worker replaces the stale empty-result message");
                    f.Frame(Vector2.down,down:true,held:true);
                    f.Check(f.Session.Selection.SetEquals(new[]{workers[0].id}),"A later confirmation selects the fresh candidate set");

                    f.Dismiss();f.Session.Select(hq.id,false);foreach(var unit in army)f.Session.Match.Entities.Remove(unit.id);
                    f.Refresh();f.Frame(Vector2.down);f.Frame(Vector2.up,down:true,held:true);
                    f.Check(f.Wheel.IsOpen&&f.Session.Selection.SetEquals(new[]{hq.id})&&f.Session.Message=="No army units available.",
                        "An empty army never clears the current selection");
                    f.Frame(Vector2.down,b:true);
                    f.Check(f.Wheel.Page==WheelPage.Root&&f.Wheel.HighlightedId=="quick-select-menu","B returns to root keeping the down direction on Quick select");
                    f.Frame(Vector2.down,down:true,held:true);f.Frame();f.Frame(elapsed:.41f);
                    f.Check(!f.Wheel.IsOpen&&f.Session.Selection.SetEquals(new[]{hq.id}),"Centering closes Quick select after the normal delay without selecting");
                    f.Drain();f.Frame(Vector2.down);f.Session.Pause("tracking",true);f.Frame(Vector2.down,down:true,held:true);
                    f.Check(!f.Wheel.IsOpen&&f.Session.Selection.SetEquals(new[]{hq.id}),"Tracking pause prevents quick selection");
                    f.Session.Pause("tracking",false);f.Drain();

                    // Candidate filtering also rejects remembered and enemy entities if
                    // present in the view; it must never inspect hidden simulation state.
                    var view=f.Session.View;var visible=view.entities;
                    view.entities=new[]{
                        new VisibleEntity{id=2000,owner=1,definition=army[0].definition,health=100,complete=true},
                        new VisibleEntity{id=2001,owner=0,definition=army[1].definition,health=100,complete=true,remembered=true},
                        new VisibleEntity{id=2002,owner=1,definition=workers[0].definition,health=100,complete=true},
                        new VisibleEntity{id=2003,owner=0,definition=workers[0].definition,health=100,complete=true,remembered=true}};
                    f.Check(!f.Session.IdleWorkers.Any()&&!f.Session.Army.Any(),"Quick selection reads only living, current, owned candidates from PlayerView");
                    view.entities=visible;
                    f.Check(f.Session.GroupSize(2)==0,"Down quick selection never assigns control group 3");
                    f.Session.LeaveMatch();f.Drain();
                }
                f.Report("QUICK_SELECTION");
            }
            UnifiedWheelPreview.Render();ControlGroupPreview.Render();ActionWheelPreview.Render();MenuFlowPreview.Render();
        }
    }
}
