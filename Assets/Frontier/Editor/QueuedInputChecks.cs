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
                    session.Faction=faction;session.LoadMap("training");session.StartMatch();
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
                    Check(worker.order==CommandKind.AttackMove,"An armed Attack Move is confirmed immediately");
                    int attackSequence=worker.activeOrderSequence;
                    Click(first,70.1f,false);
                    Check(worker.order==CommandKind.AttackMove&&worker.activeOrderSequence==attackSequence,
                        "A second quick A after menu confirmation cannot downgrade Attack Move to Move");
                    Click(first,70.8f,false);Check(worker.order==CommandKind.Move,"A later A can deliberately replace the attack with movement");

                    Reset();session.AttackMoveMode=true;
                    Call(input,"OrderClick",enemy.position,enemy.id,0,false,70.9f);
                    Call(input,"OrderClick",new Point(enemy.position.x+.2f,enemy.position.y),0,0,false,71f);
                    Check(worker.order==CommandKind.AttackMove,"Menu double confirmation tolerates the enemy moving out from under the pointer");

                    Reset();Call(input,"OrderClick",enemy.position,enemy.id,0,false,71.2f);
                    Call(input,"OrderClick",new Point(enemy.position.x-.8f,enemy.position.y),0,0,false,71.3f);
                    Check(worker.order==CommandKind.Move,"A quick ground retreat after directly attacking a living enemy remains Move");

                    Reset();session.Select(worker.id,false);session.Select(other.id,true);
                    Click(first,71,false);
                    // A casualty between the taps is not a deliberate change of selection.
                    session.Match.Entities.Remove(other.id);Refresh();session.Selection.Remove(other.id);
                    Click(first,71.1f,false);
                    Check(worker.order==CommandKind.AttackMove,"A group casualty between taps does not cancel double-A attack intent");
                    session.Match.Entities.Add(other.id,other);Refresh();session.Select(worker.id,false);

                    Reset();Call(input,"OrderClick",enemy.position,enemy.id,0,false,72f);
                    session.Match.Entities.Remove(enemy.id);Refresh();
                    Call(input,"OrderClick",enemy.position,0,0,false,72.1f);
                    Check(worker.order==CommandKind.AttackMove,"A target dying between taps converts the second A into attack-move, never Move");
                    session.Match.Entities.Add(enemy.id,enemy);Refresh();

                    Reset();Click(first,73,false);
                    Call(input,"OrderClick",new Point(first.x+.2f,first.y),worker.id,0,false,73.1f);
                    Check(worker.order==CommandKind.AttackMove,"Small aim drift from ground onto an ally still recognizes double A");

                    Reset();session.AttackMoveMode=true;Click(new Point(-10,-10),74,false);
                    Check(session.AttackMoveMode,"A rejected menu attack keeps targeting armed so the destination can be corrected");
                    Click(first,74.5f,false);Check(worker.order==CommandKind.AttackMove&&!session.AttackMoveMode,"A valid retry completes the armed menu attack");

                    Reset();session.Send(CommandKind.Stop);Click(first,75,false);
                    SelectionActions.For(session).Items.Single(a=>a.Id=="attack-move").Execute();
                    Click(second,75.5f,true);Click(second,75.6f,true);
                    Check(worker.order==CommandKind.Move&&worker.pendingOrders.Count==1&&worker.pendingOrders[0].kind==CommandKind.AttackMove,
                        "Double confirmation of a queued menu attack adds exactly one attack waypoint");

                    var nextEnemy=new EntityState{id=1002,owner=1,definition=enemyType.id,position=new Point(enemy.position.x+1,enemy.position.y),health=enemyType.health};
                    session.Match.Entities.Add(nextEnemy.id,nextEnemy);Refresh();
                    Reset();session.Send(CommandKind.Stop);Click(first,76,false);
                    Call(input,"OrderClick",enemy.position,enemy.id,0,true,76.5f);
                    Call(input,"OrderClick",nextEnemy.position,nextEnemy.id,0,true,76.6f);
                    Check(worker.pendingOrders.Count==2&&worker.pendingOrders[0].target==enemy.id&&worker.pendingOrders[1].target==nextEnemy.id,
                        "Two distinct living enemies can still be queued rapidly without replacing either target");
                    session.Match.Entities.Remove(nextEnemy.id);Refresh();

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

                    var barracksType=session.Catalog.Role(faction,"barracks");
                    var barracks=new EntityState{id=2001,owner=0,definition=barracksType.id,position=new Point(18,18),health=barracksType.health};
                    var nearby=new EntityState{id=2002,owner=0,definition=barracksType.id,position=new Point(25,18),health=barracksType.health};
                    var distant=new EntityState{id=2003,owner=0,definition=barracksType.id,position=new Point(40,30),health=barracksType.health};
                    var unfinished=new EntityState{id=2004,owner=0,definition=barracksType.id,position=new Point(18,25),health=100,complete=false,remaining=100};
                    foreach(var building in new[]{barracks,nearby,distant,unfinished})session.Match.Entities.Add(building.id,building);
                    Refresh();Reset();session.Select(0,false);SelectTap(barracks.id,101,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id}),"Single trigger still selects exactly one building");
                    SelectTap(barracks.id,101.1f,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id,nearby.id,unfinished.id}),"Double trigger selects nearby same-type buildings, including foundations, without distant barracks or other types");
                    nearby.position=new Point(30,18);Refresh();Reset();SelectTap(barracks.id,101.2f,false);SelectTap(barracks.id,101.3f,false);
                    Check(session.Selection.Contains(nearby.id),"Building double click includes the same twelve-cell boundary as units");
                    nearby.position=new Point(30.1f,18);Refresh();Reset();SelectTap(barracks.id,101.4f,false);SelectTap(barracks.id,101.5f,false);
                    Check(!session.Selection.Contains(nearby.id),"Building double click excludes matches just beyond twelve cells");
                    nearby.position=new Point(25,18);Refresh();
                    Reset();session.Select(worker.id,false);SelectTap(barracks.id,102,true);SelectTap(barracks.id,102.1f,true);
                    Check(session.Selection.SetEquals(new[]{worker.id,barracks.id,nearby.id,unfinished.id}),"Modifier plus double trigger adds nearby buildings to an existing selection");
                    Reset();session.Select(barracks.id,false);SelectTap(barracks.id,103,true);SelectTap(barracks.id,103.1f,true);
                    Check(session.Selection.SetEquals(new[]{barracks.id,nearby.id,unfinished.id}),"Modified building double tap works when first tap toggles its source off");
                    Reset();SelectTap(barracks.id,104,false);SelectTap(nearby.id,104.1f,false);
                    Check(session.Selection.SetEquals(new[]{nearby.id}),"Two fast clicks on different buildings do not expand selection");
                    Reset();SelectTap(barracks.id,105,false);SelectTap(barracks.id,105.5f,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id}),"Slow building taps remain individual selection");
                    Reset();SelectTap(barracks.id,106,false);session.Select(hq.id,false);SelectTap(barracks.id,106.1f,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id}),"Changing selection interrupts pending building double tap");
                    Reset();SelectTap(barracks.id,107,false);Call(input,"OnApplicationFocus",false);SelectTap(barracks.id,107.1f,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id}),"Focus loss cancels pending building double tap");
                    Reset();session.SelectSameTypeOnMap(barracks.id,false);session.Group(0,true);session.Select(worker.id,false);session.Group(0,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id,nearby.id,unfinished.id}),"Nearby building cohort can be saved and recalled as a control group");
                    session.Match.Entities.Remove(nearby.id);Refresh();session.Group(0,false);
                    Check(session.Selection.SetEquals(new[]{barracks.id,unfinished.id})&&session.GroupSize(0)==2,"Building groups drop a destroyed member without losing survivors");
                    foreach(var building in new[]{barracks,distant,unfinished})session.Match.Entities.Remove(building.id);
                    Refresh();Reset();session.Group(0,false);
                    Check(session.Selection.Count==0&&session.GroupSize(0)==0,"Destroyed building group recalls as empty");

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
                    Call(input,"RouteWheel",ray,Vector2.zero,false,false,false,false,false,true,false,110f,false,false);
                    bool consumed=(bool)Call(input,"RouteWheel",ray,Vector2.up,false,false,false,false,false,true,false,110.05f,false,false);
                    Check(consumed&&wheel.IsOpen&&wheel.Page==WheelPage.Root&&session.Selection.SetEquals(before),"A neutral-to-direction tilt opens the unified root while Grip is held without changing selection");
                    wheel.Close();

                    // Exercise the original drag route, including starts outside the map.
                    Reset();session.Select(0,false);
                    Call(input,"PointBoard",RayAt(new Point(-2,10)),true,false,false,false);
                    Call(input,"PointBoard",RayAt(new Point(10,18)),false,true,false,false);
                    Check(session.Selection.Contains(worker.id)&&session.Selection.Contains(other.id),"Outside-board drag selection is preserved");
                    Check(Field<float>(input,"lastSelectPress")<0,"Dragging never seeds a double-trigger gesture");

                    // Both factions keep a builder on site and use the same contextual
                    // command to resume foundations or repair finished allies.
                    session.StartMatch();Reset();
                    var builders=session.Match.Entities.Values.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").ToArray();
                    foreach(var builder in builders){session.Select(builder.id,false);session.Send(CommandKind.Stop);}
                    var constructor=builders[0];var repairTarget=builders[1];
                    session.Match.Players[0].minerals=1000;session.Select(constructor.id,false);
                    var supplyType=session.Catalog.Role(faction,"supply");
                    SelectionActions.For(session).Items.Single(a=>a.Id=="build:"+supplyType.id).Execute();
                    Check(session.Context(new Point(20,14),0,0)>0,"Shared build action places a foundation for "+faction);
                    var foundation=session.Match.Entities.Values.Single(e=>e.owner==0&&e.definition==supplyType.id);
                    constructor.position=new Point(19.5f,15.5f);
                    for(int n=0;n<10;n++)session.Match.Step();
                    Check(constructor.order==CommandKind.Build&&foundation.remaining<supplyType.duration,"Builder stays assigned while construction advances for "+faction);
                    session.Send(CommandKind.Stop);int remaining=foundation.remaining;
                    for(int n=0;n<10;n++)session.Match.Step();
                    Check(foundation.remaining==remaining&&!foundation.complete,"Construction pauses when the builder stops for "+faction);
                    foundation.health=supplyType.health;
                    Refresh();Reset();Call(input,"OrderClick",foundation.position,foundation.id,0,false,120f);
                    Check(constructor.order==CommandKind.Build&&constructor.target==foundation.id,"Context order resumes unfinished construction even at full health for "+faction);
                    for(int n=0;n<supplyType.duration+10;n++)session.Match.Step();
                    Check(foundation.complete&&constructor.order!=CommandKind.Build,"Construction completes and releases its worker for "+faction);

                    constructor.position=new Point(18.5f,15.5f);repairTarget.position=new Point(17.5f,15.5f);
                    repairTarget.health=session.Catalog[repairTarget.definition].health-12;Refresh();Reset();
                    session.Send(CommandKind.Stop);Click(new Point(16.5f,15.5f),130f,false);
                    Call(input,"OrderClick",repairTarget.position,repairTarget.id,0,true,131f);
                    Check(constructor.pendingOrders.Count==1&&constructor.pendingOrders[0].kind==CommandKind.Repair,
                        "Context repair can be queued after movement for "+faction);
                    session.Send(CommandKind.Stop);Reset();Call(input,"OrderClick",repairTarget.position,repairTarget.id,0,false,132f);
                    Check(constructor.order==CommandKind.Repair&&constructor.target==repairTarget.id,"Context order repairs a damaged ally for "+faction);
                    int repairMinerals=session.Match.Players[0].minerals;float repairHealth=repairTarget.health;
                    for(int n=0;n<4;n++)session.Match.Step();
                    Check(repairTarget.health>repairHealth&&session.Match.Players[0].minerals<repairMinerals,
                        "Context repair restores health and spends minerals for "+faction);

                    foreach(string route in new[]{"direct","double-a","menu-a","menu-double-a","menu-trigger-a"})
                    {
                        // No bot or visibility overrides: execute the real session and
                        // input routes, then let a spread-out selection fight on its own.
                        var map=new MapDefinition{id="group-input",title="Group attack input",width=80,height=48,
                            spawns=new[]{new SpawnDefinition{player=0,x=2,y=2},new SpawnDefinition{player=1,x=72,y=40}},
                            resources=new[]{new ResourceDefinition{id="a",x=10,y=12,amount=10000},new ResourceDefinition{id="b",x=69,y=35,amount=10000},
                                new ResourceDefinition{id="c",kind="catalyst",x=8,y=8},new ResourceDefinition{id="d",kind="catalyst",x=74,y=35}}};
                        typeof(GameSession).GetProperty("Map").SetValue(session,map);session.StartMatch();Reset();
                        foreach(var unit in session.Match.Entities.Values.Where(e=>!session.Match.Type(e).building).ToArray())session.Match.Entities.Remove(unit.id);
                        int id=1000;
                        EntityState AddSoldier(int owner,string role,Point position)
                        {
                            var type=session.Catalog.Role(session.Match.Players[owner].faction,role);
                            var unit=new EntityState{id=id++,owner=owner,definition=type.id,position=position,health=type.health,
                                order=CommandKind.Hold,attackReadyTick=int.MaxValue};
                            session.Match.Entities.Add(unit.id,unit);return unit;
                        }
                        var front=AddSoldier(0,"ranged",new Point(35.5f,20.5f));
                        var army=new System.Collections.Generic.List<EntityState>{front};
                        for(int n=0;n<17;n++)army.Add(AddSoldier(0,n%3==0?"siege":n%3==1?"ranged":"melee",
                            new Point(13.5f+n%3*1.5f,14.5f+n/3*1.5f)));
                        var focus=AddSoldier(1,"worker",new Point(40.5f,20.5f));
                        for(int n=0;n<18;n++)AddSoldier(1,"ranged",new Point(43.5f+n%3*1.5f,13.5f+n/3*2));
                        for(int n=0;n<4;n++)session.Match.Step();
                        focus.health=1;
                        Call(session,"RefreshView");Call(board,"LateUpdate");
                        foreach(var unit in army){session.Selection.Add(unit.id);unit.attackReadyTick=0;}
                        if(route.StartsWith("menu"))SelectionActions.For(session).Items.Single(a=>a.Id=="attack-move").Execute();
                        float time=Time.unscaledTime;
                        if(route=="menu-trigger-a")
                        {
                            Call(input,"PointBoard",RayAt(focus.position),true,false,false,false);
                            Call(input,"PointBoard",RayAt(focus.position),false,true,false,false);
                        }
                        else Call(input,"OrderClick",focus.position,focus.id,0,false,time);
                        Check(army.All(unit=>unit.order==CommandKind.Attack||unit.order==CommandKind.AttackMove),
                            route+": every member of the 18-unit selection receives an attack order");
                        for(int n=0;n<4;n++)session.Match.Step();
                        Call(session,"RefreshView");Call(board,"LateUpdate");
                        if(route=="double-a"||route=="menu-double-a"||route=="menu-trigger-a")
                            Call(input,"OrderClick",focus.position,session.Match.Entities.ContainsKey(focus.id)?focus.id:0,0,false,time+.2f);
                        Check(army.Skip(1).All(unit=>unit.order==CommandKind.AttackMove),
                            route+": rear units retain the attack after confirmation or the front's first kill");
                        Check(session.Selection.SetEquals(army.Select(unit=>unit.id)),route+": confirming the attack preserves the group selection");
                        for(int n=0;n<500;n++)session.Match.Step();
                        Check(army.Skip(1).All(unit=>unit.position.x>32),route+": the full rear joins the battle without individual commands");
                    }

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
