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
    // Real session commands and pointer routing with synthetic edges and controlled tap times.
    public static class QueuedInputChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Check queued controller input")]
        public static void Run()
        {
            var fixture=new GameObject("Queued input fixture");var console=new GameObject("Queued input console");
            int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                var cameraObject=new GameObject("Queued input camera",typeof(Camera));cameraObject.transform.SetParent(fixture.transform);
                cameraObject.transform.position=new Vector3(0,1.7f,-2);
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,cameraObject.GetComponent<Camera>());
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                var wheel=fixture.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;input.Wheel=wheel;

                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();
                    var workers=session.Match.Entities.Values.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").ToArray();
                    var worker=workers[0];var other=workers[1];var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    worker.position=new Point(8.5f,12.5f);other.position=new Point(8.5f,16.5f);workers[2].position=new Point(40.5f,40.5f);
                    Refresh();session.Select(worker.id,false);session.Send(CommandKind.Stop);
                    var first=new Point(20.5f,15.5f);var second=new Point(25.5f,15.5f);var third=new Point(25.5f,20.5f);

                    Click(first,10,true);
                    Check(worker.order==CommandKind.Move&&worker.pendingOrders.Count==0,"First Grip + A executes immediately when idle");
                    int active=worker.activeOrderSequence;
                    Click(second,10.1f,true);
                    Check(worker.pendingOrders.Count==1&&worker.pendingOrders[0].kind==CommandKind.Move,"Rapid A at a different target appends a separate waypoint");
                    Check(worker.activeOrderSequence==active,"Appending preserves the active movement order");
                    Click(new Point(second.x+.15f,second.y),10.2f,true);
                    Check(worker.pendingOrders.Count==1&&worker.pendingOrders[0].kind==CommandKind.AttackMove,"Double A converts only its queued waypoint into Attack Move");
                    Click(third,10.3f,true);Click(third,10.8f,true);
                    Check(worker.pendingOrders.Count==3&&worker.pendingOrders.Skip(1).All(o=>o.kind==CommandKind.Move),"Two slow presses remain separate queued moves");

                    Reset();Click(first,20,false);
                    Check(worker.order==CommandKind.Move&&worker.pendingOrders.Count==0,"Plain A replaces the old queue immediately");
                    Click(first,20.1f,false);
                    Check(worker.order==CommandKind.AttackMove&&worker.pendingOrders.Count==0,"Double A without Grip replaces movement with Attack Move");
                    Click(second,20.2f,false);
                    Check(worker.order==CommandKind.Move,"A third tap begins a fresh gesture");

                    var enemyType=session.Catalog.Role(session.EnemyFaction,"worker");
                    var enemy=new EntityState{id=1001,owner=1,definition=enemyType.id,position=new Point(20.5f,12.5f),health=enemyType.health};
                    session.Match.Entities.Add(enemy.id,enemy);Refresh();Reset();
                    Call(input,"OrderClick",enemy.position,enemy.id,0,false,25f);
                    Call(input,"OrderClick",enemy.position,enemy.id,0,false,25.1f);
                    Check(worker.order==CommandKind.Attack&&worker.target==enemy.id,"Double A on a visible enemy keeps a direct attack target");

                    Reset();Click(first,30,true);Call(input,"ValidateMapGestures",false);Call(input,"ValidateMapGestures",true);Click(first,30.1f,true);
                    Check(worker.pendingOrders.Last().kind==CommandKind.Move,"Releasing Grip breaks a pending double A");
                    Reset();Click(first,40,false);session.Select(other.id,false);Call(input,"ValidateMapGestures",false);session.Select(worker.id,false);Click(first,40.1f,false);
                    Check(worker.order==CommandKind.Move,"Changing selection and restoring it breaks a pending double A");
                    Reset();Click(first,50,false);session.Pause("gesture-check",true);Call(input,"ValidateMapGestures",false);session.Pause("gesture-check",false);Click(first,50.1f,false);
                    Check(worker.order==CommandKind.Move,"Pause and recovery discard the old tap");
                    Reset();Click(first,60,false);Call(input,"OnApplicationFocus",false);Click(first,60.1f,false);
                    Check(worker.order==CommandKind.Move,"Focus loss discards the old tap");
                    Reset();session.AttackMoveMode=true;Click(first,70,false);
                    Check(worker.order==CommandKind.AttackMove&&Field<float>(input,"lastActionPress")<0,"An armed Attack Move does not seed a double A");
                    Click(first,70.1f,false);Check(worker.order==CommandKind.Move,"The following A remains an ordinary contextual order");

                    // The same selection code is used by desktop double-left-click and Quest trigger.
                    Reset();session.Select(0,false);SelectTap(worker.id,80,false);SelectTap(worker.id,80.1f,false);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Double trigger selects nearby matching units");
                    Check(!session.Selection.Contains(workers[2].id)&&!session.Selection.Contains(hq.id),"Type selection excludes distant units and buildings");
                    Reset();session.Select(hq.id,false);SelectTap(worker.id,90,true);SelectTap(worker.id,90.1f,true);
                    Check(session.Selection.Contains(hq.id)&&session.Selection.Contains(other.id),"Grip + double trigger preserves the earlier selection");
                    Reset();session.Select(worker.id,false);SelectTap(worker.id,95,true);SelectTap(worker.id,95.1f,true);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Grip + double trigger also works when its first tap toggles the unit off");
                    Reset();session.Select(0,false);SelectTap(worker.id,100,false);SelectTap(other.id,100.1f,false);
                    Check(session.Selection.SetEquals(new[]{other.id}),"Two quick clicks on different units do not select a type");

                    Reset();session.Select(worker.id,false);session.Select(other.id,true);Refresh();
                    board.Hovered=other.id;
                    var ray=RayAt(worker.position);
                    Call(input,"PointBoard",ray,true,false,false,true);
                    Set(input,"selectPressedAt",Time.unscaledTime-.25f);Call(input,"PointBoard",ray,false,false,false,true);
                    Check(session.Selection.Contains(worker.id),"A grip-trigger hold shorter than half a second keeps the selected unit");
                    Set(input,"selectPressedAt",Time.unscaledTime-.51f);Call(input,"PointBoard",ray,false,false,false,true);
                    Check(!session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Holding Grip + trigger removes the currently pointed unit, ignoring stale hover");
                    Call(input,"PointBoard",ray,false,false,false,true);Call(input,"PointBoard",ray,false,true,false,true);
                    Check(!session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"A long hold removes once and release does not select the unit again");
                    Check(Field<float>(input,"lastActionPress")<0&&Field<float>(input,"lastSelectPress")<0,"Removal clears pending map gestures");
                    Reset();session.Select(worker.id,false);Call(input,"PointBoard",ray,true,false,false,true);
                    Set(input,"selectPressedAt",Time.unscaledTime-.6f);Call(input,"PointBoard",ray,false,true,false,true);
                    Check(!session.Selection.Contains(worker.id)&&!Field<bool>(input,"deselectHoldConsumed"),"A long hold released on its threshold frame removes once without leaving input captured");

                    Reset();session.Select(worker.id,false);Call(input,"PointBoard",ray,true,false,false,true);
                    Set(input,"selectPressedAt",Time.unscaledTime-.6f);
                    Call(input,"PointBoard",RayAt(new Point(10,18)),false,false,false,true);
                    Check(session.Selection.Contains(worker.id),"Dragging beyond the threshold cancels removal even after the hold delay");
                    Call(input,"PointBoard",RayAt(new Point(10,18)),false,true,false,true);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Grip + trigger drag still adds the units inside the rectangle");

                    Reset();session.Select(other.id,false);Call(input,"PointBoard",ray,true,false,false,true);
                    Set(input,"selectPressedAt",Time.unscaledTime-.6f);Call(input,"PointBoard",ray,false,false,false,true);
                    Check(session.Selection.SetEquals(new[]{other.id}),"Holding over an unselected unit does not remove or select it early");
                    Call(input,"PointBoard",ray,false,true,false,true);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Releasing over an unselected unit preserves additive selection");

                    Reset();var before=session.Selection.ToArray();
                    bool consumed=(bool)Call(input,"RouteWheel",ray,Vector2.zero,true,false,false,false,false,true,false,110f);
                    Check(consumed&&wheel.IsOpen&&session.Selection.SetEquals(before),"Right stick always opens the wheel, including while Grip is held");
                    wheel.Close();

                    // Exercise the original drag route, including starts outside the map.
                    Reset();session.Select(0,false);
                    Call(input,"PointBoard",RayAt(new Point(-2,10)),true,false,false,false);
                    Call(input,"PointBoard",RayAt(new Point(10,18)),false,true,false,false);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Outside-board drag selection is preserved");
                    Check(Field<float>(input,"lastSelectPress")<0,"Dragging never seeds a double-trigger gesture");

                    void Click(Point point,float now,bool add)=>Call(input,"OrderClick",point,0,0,add,now);
                    void SelectTap(int id,float now,bool add){Set(input,"pressedEntity",id);Call(input,"SelectClick",id,add,now);}
                    void Reset(){Call(input,"ClearPointer");Set(input,"panelReleasePending",false);Set(input,"wheelReleasePending",false);}
                    void Refresh(){typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));Array.Fill(session.View.visibility,(byte)2);Call(board,"LateUpdate");}
                    Ray RayAt(Point point)=>new Ray(board.Content.TransformPoint(new Vector3(point.x,0,point.y))+Vector3.up,Vector3.down);
                }
                Debug.Log($"FRONTIER_QUEUED_INPUT passed: {checks} checks; no player build");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("QUEUED_INPUT_PASS: "+message);}
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);}
        }
    }
}
