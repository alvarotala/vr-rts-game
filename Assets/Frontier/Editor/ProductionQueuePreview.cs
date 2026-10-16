using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    // Exercises real command buttons and production ticks, then renders the runtime canvas.
    public static class ProductionQueuePreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Preview production queue")]
        public static void Render()
        {
            const string output="TestResults/ProductionQueue";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Production fixture");var console=new GameObject("Production console");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=fixture.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                Invoke(panel,"Start");preview.AddSingleGO(console);
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.LoadMap("training");session.StartMatch();
                    session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    var supply=AddBuilding("supply",1000,new Point(20,8));
                    var barracks=AddBuilding("barracks",1001,new Point(22,12));
                    var tech=AddBuilding("tech",1002,new Point(22,17));
                    var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    Refresh();session.Select(hq.id,false);Refresh();
                    Check(QueueShown()&&Text("productionName")=="Queue empty","Producer shows its empty queue before clicking Train");
                    Capture(faction+"-idle");float height=Field<RectTransform>(panel,"root").rect.height;
                    var worker=session.Catalog.Role(faction,"worker");Click("Train "+worker.name);
                    Check(hq.queue.Count==1&&Text("productionName")==worker.name,"Training click immediately shows the actual queued worker");
                    Check(Field<RectTransform>(panel,"root").rect.height==height,"Enqueue does not move the production controls");
                    for(int i=0;i<worker.duration/2;i++)session.Match.Step();Refresh();
                    Check(Mathf.Abs(Field<Image>(panel,"productionFill").rectTransform.rect.width-160)<.1f&&Text("productionProgress").Contains("50%"),"Progress bar follows simulation ticks");
                    int seconds=(hq.queue[0].remaining+Simulation.TickRate-1)/Simulation.TickRate;
                    Check(Text("productionProgress").Contains(seconds+"s remaining"),"Countdown rounds partial seconds up using the simulation tick rate");Capture(faction+"-worker");

                    session.Select(barracks.id,false);
                    var roles=new[]{"scout","melee","ranged","melee","scout"};
                    foreach(var role in roles)Click("Train "+session.Catalog.Role(faction,role).name);
                    Check(Text("productionCount")=="5 / 5 · FULL"&&barracks.queue.Count==5,"Full queue is visible");
                    Check(Field<Text[]>(panel,"queuedNames").Select(t=>t.text).SequenceEqual(roles.Skip(1).Select(r=>session.Catalog.Role(faction,r).name)),"All four pending items preserve FIFO order, including duplicates");
                    Capture(faction+"-full");Click("Train "+session.Catalog.Role(faction,"scout").name);
                    Check(barracks.queue.Count==5&&Text("productionCount").Contains("FULL"),"Rejected sixth order leaves the displayed queue unchanged");
                    int minerals=session.View.minerals;Click("Cancel / refund");
                    Check(barracks.queue.Count==4&&session.View.minerals==minerals+session.Catalog.Role(faction,"scout").minerals&&Field<Text[]>(panel,"queuedNames")[3].text=="Empty","Cancel refunds only the last item and clears its slot");
                    // Finish and enqueue between HUD refreshes: the length is unchanged, the active item is not.
                    barracks.queue[0].remaining=1;session.Match.Step();
                    Check(session.Send(CommandKind.Train,session.Catalog.Role(faction,"ranged").id),"Replacement order accepted");Refresh();
                    Check(barracks.queue.Count==4&&Text("productionName")==session.Catalog.Role(faction,"melee").name,"Active name advances even when queue length stays the same");Capture(faction+"-next");

                    session.Select(tech.id,false);Click("Firepower I");Click("Train "+session.Catalog.Role(faction,"siege").name);Click("Armor I");
                    for(int i=0;i<175;i++)session.Match.Step();Refresh();
                    Check(Text("productionName")=="Firepower I"&&Text("productionState").Contains("RESEARCHING")&&Text("productionProgress").Contains("25%"),"Research shares the ordered queue and advances its progress");
                    Capture(faction+"-research");
                    session.Pause("tracking",true);int remaining=tech.queue[0].remaining;session.Match.Step();Refresh();
                    Check(Text("productionState").Contains("PAUSED")&&tech.queue[0].remaining==remaining,"External pause freezes production and labels it paused");Capture(faction+"-paused");session.Pause("tracking",false);
                    session.Select(0,false);board.Hovered=tech.id;Refresh();
                    Check(!QueueShown()&&Text("selectionDetails").Contains("Select to view production"),"Hover invites selection instead of referring to a hidden queue");board.Hovered=0;
                    var unit=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building);
                    session.Select(unit.id,false);Refresh();Check(!QueueShown(),"Units hide production queue");
                    session.Select(supply.id,false);Refresh();Check(!QueueShown(),"Non-producing buildings hide production queue");
                    session.Select(tech.id,false);session.Select(hq.id,true);Refresh();Check(!QueueShown(),"Multiple buildings do not show a misleading combined queue");
                    session.Select(tech.id,false);tech.complete=false;Refresh();Check(!QueueShown(),"Unfinished producer hides production queue");tech.complete=true;Refresh();
                    panel.ToggleMenu();Refresh();Check(!QueueShown(),"Pause menu hides match production queue");panel.ToggleMenu();Refresh();
                    Check(QueueShown()&&Text("productionName")=="Firepower I","Resuming restores the selected building queue");

                    // Isolate deployment feedback with a real finished order and occupied perimeter.
                    hq.queue.Clear();tech.queue.Clear();barracks.queue.Clear();session.Select(barracks.id,false);Refresh();
                    Click("Train "+session.Catalog.Role(faction,"scout").name);
                    int size=session.Catalog[barracks.definition].footprint;
                    Border(9999);barracks.queue[0].remaining=1;session.Match.Step();Refresh();
                    Check(barracks.queue.Count==1&&Text("productionProgress")=="100% · Clear the building exit","Finished unit with blocked exit shows deployment feedback");Capture(faction+"-blocked-exit");
                    Border(0);session.Match.Step();Refresh();
                    Check(barracks.queue.Count==0&&Text("productionName")=="Queue empty","Opening the exit deploys the unit and clears the display");
                    Click("Train "+session.Catalog.Role(faction,"scout").name);
                    // Lost capacity after accepting an order: existing units, not reservations, determine the warning.
                    session.Match.Entities.Remove(supply.id);session.Match.Map.Occupy(supply.position,session.Catalog[supply.definition].footprint,0);
                    for(int id=2000;session.Match.Units(0)<session.Match.Capacity(0);id++)
                        session.Match.Entities.Add(id,new EntityState{id=id,owner=0,definition=worker.id,health=worker.health,position=new Point(7,id-1995)});
                    barracks.queue[0].remaining=1;session.Match.Step();Refresh();
                    Check(Text("productionProgress")=="100% · Needs more supply","Lost capacity is distinguished from a blocked exit");Capture(faction+"-blocked-supply");
                    session.LeaveMatch();Refresh();Check(!QueueShown(),"Leaving clears and hides the production HUD");

                    EntityState AddBuilding(string role,int id,Point position)
                    {
                        var type=session.Catalog.Role(faction,role);var entity=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=position};
                        session.Match.Entities.Add(id,entity);session.Match.Map.Occupy(position,type.footprint,id);return entity;
                    }
                    void Border(int occupant)
                    {for(int y=-1;y<=size;y++)for(int x=-1;x<=size;x++)if(x==-1||y==-1||x==size||y==size)session.Match.Map.Occupy(new Point(barracks.position.x+x,barracks.position.y+y),1,occupant);}
                }
                Debug.Log($"FRONTIER_PRODUCTION_QUEUE passed: {checks} checks, {captures} panels rendered; no player build");
                HarvestingPreview.RenderHud();MenuFlowPreview.Render();

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
                string Text(string field)=>Field<Text>(panel,field).text;
                bool QueueShown()=>Field<RectTransform>(panel,"productionStrip").gameObject.activeInHierarchy;
                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");
                }
                void Click(string title)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.gameObject.activeInHierarchy&&t.name==title);
                    var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Pointer missed "+title);Refresh();
                }
                void Capture(string name){HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;}
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
