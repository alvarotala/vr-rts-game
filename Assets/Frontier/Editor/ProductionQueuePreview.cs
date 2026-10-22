using TMPro;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
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
                    CheckQueueModels();
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
                    session.Select(tech.id,false);Refresh();
                    var pendingCard=Field<Image[]>(panel,"queuedCards")[1].rectTransform;
                    var pendingCenter=pendingCard.TransformPoint(pendingCard.rect.center);
                    var pendingRay=new Ray(pendingCenter-pendingCard.forward,pendingCard.forward);
                    Check(panel.Point(pendingRay,true,false),"Pending item can be pressed before changing to a building group");
                    int techOrders=tech.queue.Count,hqOrders=hq.queue.Count;
                    session.Select(hq.id,true);Refresh();
                    Check(QueueShown()&&Text("productionTitle")=="GROUP PRODUCTION"&&Text("productionName")=="Firepower I",
                        "Multiple producers show their actual production orders");
                    Check(Text("productionCount")==$"{techOrders+hqOrders} / 10"&&Field<Text[]>(panel,"queuedNames")[0].text==session.Catalog.Role(faction,"siege").name,
                        "Group fills subsequent slots with actual orders and totals combined capacity");
                    Check(Text("productionHint")=="2 BUILDINGS · SELECT ONE TO CANCEL"&&Field<Array>(panel,"productionTargets").Cast<object>().All(t=>t==null),
                        "Group production cards do not expose ambiguous cancel targets");
                    panel.Point(pendingRay,false,true);Refresh();
                    Check(tech.queue.Count==techOrders&&hq.queue.Count==hqOrders,"Changing selection invalidates a pressed individual queue item");
                    Capture(faction+"-group");
                    session.Pause("tracking",true);Refresh();
                    Check(Text("productionState").Contains("PAUSED"),"Building group production displays the paused state");session.Pause("tracking",false);
                    var foundation=AddBuilding("barracks",1003,new Point(28,12));foundation.complete=false;Refresh();
                    session.Select(barracks.id,false);session.Select(foundation.id,true);Refresh();
                    Check(QueueShown()&&Text("productionTitle")=="GROUP PRODUCTION"&&Text("productionName")==session.Catalog[barracks.queue[0].definition].name&&
                        Text("productionCount")==$"{barracks.queue.Count} / 5",
                        "A completed barracks and unfinished barracks show the available producer's actual queue");
                    Check(Text("productionHint")=="1 BUILDING · SELECT ONE TO CANCEL"&&Field<Array>(panel,"productionTargets").Cast<object>().All(t=>t==null),
                        "One eligible producer in a larger building selection never exposes individual cancellation");
                    session.Select(supply.id,true);Refresh();
                    Check(QueueShown()&&Text("productionCount")==$"{barracks.queue.Count} / 5"&&Text("productionTitle")=="GROUP PRODUCTION",
                        "Nonproducing buildings do not hide or inflate a building group's production summary");
                    session.Select(unit.id,true);Refresh();Check(!QueueShown(),"A mixed unit and building selection hides the producer summary");
                    session.Match.Entities.Remove(foundation.id);session.Match.Map.Occupy(foundation.position,session.Catalog[foundation.definition].footprint,0);
                    session.Select(tech.id,false);Refresh();
                    Check(Text("productionTitle")=="PRODUCTION QUEUE"&&Text("productionHint")=="CLICK AN ITEM TO CANCEL"&&
                        Field<Text[]>(panel,"queuedPositions").Select(t=>t.text).SequenceEqual(new[]{"2","3","4","5"}),
                        "Returning to one building restores its individual queue labels");
                    pendingCenter=pendingCard.TransformPoint(pendingCard.rect.center);pendingRay=new Ray(pendingCenter-pendingCard.forward,pendingCard.forward);
                    Check(panel.Point(pendingRay,true,false)&&panel.Point(pendingRay,false,true),"Returning to one building restores its real cancellation target");Refresh();
                    Check(tech.queue.Count==techOrders-1&&hq.queue.Count==hqOrders&&tech.queue[0].definition=="attack",
                        "Individual cancellation after a group affects only the chosen item and producer");
                    session.Select(tech.id,false);tech.complete=false;Refresh();Check(!QueueShown(),"Unfinished producer hides production queue");tech.complete=true;Refresh();
                    panel.ToggleMenu();Refresh();Check(!QueueShown(),"Pause menu hides match production queue");panel.ToggleMenu();Refresh();
                    Check(QueueShown()&&Text("productionName")=="Firepower I","Resuming restores the selected building queue");

                    // Every producer role plus rally clearing exercises the densest building action layout.
                    var academy=AddBuilding("academy",1004,new Point(28,18));
                    var allProducers=new[]{hq,barracks,tech,academy};
                    foreach(var producer in allProducers){producer.hasRallyPoint=true;producer.rallyPoint=new Point(20,20);}
                    Refresh();session.Select(0,false);foreach(var producer in allProducers)session.Select(producer.id,true);Refresh();
                    var groupActions=SelectionActions.For(session).Items;
                    Check(groupActions.Count==9&&groupActions.Any(a=>a.Id=="clear-rally-point"),
                        "All four producer roles expose six train actions, two upgrades and rally clearing");
                    Check(Text("productionState").Contains("TRAINING")&&Text("productionHint")=="4 BUILDINGS · SELECT ONE TO CANCEL",
                        "Producer group shows current production without suggesting group cancellation");
                    Canvas.ForceUpdateCanvases();
                    var actionArea=Field<RectTransform>(panel,"actions");
                    var actionCards=actionArea.Cast<RectTransform>().Where(t=>t.GetComponent<Image>()!=null).ToArray();
                    Check(actionCards.Length==9&&Inside(Field<RectTransform>(panel,"root"),actionArea)&&actionCards.All(card=>Inside(actionArea,card)),
                        "All nine production actions remain within the existing action section and panel");
                    Check(actionCards.All(card=>card.GetComponentsInChildren<Text>().All(text=>Inside(card,text.rectTransform)&&Fits(text))),
                        "Both text lines fit completely inside every compact production action");
                    Check(actionCards.SelectMany((card,i)=>actionCards.Skip(i+1).Select(other=>
                        new Rect(card.anchoredPosition-new Vector2(0,card.rect.height),card.rect.size).Overlaps(
                        new Rect(other.anchoredPosition-new Vector2(0,other.rect.height),other.rect.size)))).All(overlap=>!overlap),
                        "Compact production actions do not overlap");
                    Check(Fits(Field<Text>(panel,"productionState"))&&Fits(Field<Text>(panel,"productionHint")),
                        "Group dispatch and cancellation guidance fit in the production summary");
                    Capture(faction+"-all-producers");
                    int totalOrders=allProducers.Sum(producer=>producer.queue.Count);
                    Click("Train "+session.Catalog.Role(faction,"caster").name);
                    Check(academy.queue.Count==1&&allProducers.Sum(producer=>producer.queue.Count)==totalOrders+1,
                        "The compact caster button queues exactly one order at the compatible selected producer");
                    Click("Clear Rally Points");
                    Check(allProducers.All(producer=>!producer.hasRallyPoint),"The compact clear-rally action remains usable for every selected producer");
                    session.Match.Entities.Remove(academy.id);session.Match.Map.Occupy(academy.position,session.Catalog[academy.definition].footprint,0);

                    // Isolate deployment feedback with a real finished order and occupied perimeter.
                    hq.queue.Clear();tech.queue.Clear();barracks.queue.Clear();session.Select(barracks.id,false);session.Select(tech.id,true);Refresh();
                    Check(Text("productionCount")=="0 / 10"&&Text("productionName")=="Queue empty"&&Text("productionState")=="IDLE"&&
                        Field<Text[]>(panel,"queuedNames").All(t=>t.text=="Empty"),
                        "An idle producer group clears all five slots and its active state");
                    CheckQueueModels();

                    // Real training orders in two and three barracks fill every card,
                    // instead of replacing the second card with a total-order counter.
                    var second=AddBuilding("barracks",1005,new Point(30,12));
                    var third=AddBuilding("barracks",1006,new Point(36,12));
                    var groupSupply=AddBuilding("supply",1007,new Point(34,8));
                    session.Select(barracks.id,false);
                    foreach(string role in new[]{"scout","melee","ranged"})Click("Train "+session.Catalog.Role(faction,role).name);
                    Refresh();session.Select(second.id,false);
                    foreach(string role in new[]{"melee","scout"})Click("Train "+session.Catalog.Role(faction,role).name);
                    session.Select(barracks.id,true);Refresh();
                    Check(ShownNames().SequenceEqual(new[]{"scout","melee","melee","scout","ranged"}.Select(r=>session.Catalog.Role(faction,r).name)),
                        "Two barracks show both active units first, followed by each building's pending orders without merging duplicates");
                    Check(Text("productionCount")=="5 / 10"&&Field<Text[]>(panel,"queuedPositions")[3].text=="5",
                        "Exactly five group orders fill all five slots without an overflow count");
                    Check(Field<Text[]>(panel,"queuedKinds")[0].text=="B2 · 0%"&&Field<Text[]>(panel,"queuedKinds")[1].text=="B1 · QUEUED",
                        "Group cards distinguish a second active producer from pending production");
                    Capture(faction+"-group-five");
                    for(int i=0;i<3;i++)Click("Train "+session.Catalog.Role(faction,"ranged").name);
                    Check(barracks.queue.Count+second.queue.Count==8&&Text("productionCount")=="8 / 10"&&
                        ShownNames().All(n=>n!="Empty")&&Field<Text[]>(panel,"queuedPositions")[3].text=="5 · +3 MORE",
                        "Two barracks retain five real order cards and show only additional orders in the last card");
                    Capture(faction+"-group-overflow");
                    session.Select(third.id,false);
                    foreach(string role in new[]{"scout","ranged","scout","ranged"})Click("Train "+session.Catalog.Role(faction,role).name);
                    session.Select(second.id,true);session.Select(barracks.id,true);Refresh();
                    Check(ShownNames().Take(3).SequenceEqual(new[]{"scout","melee","scout"}.Select(r=>session.Catalog.Role(faction,r).name))&&
                        Text("productionCount")=="12 / 15"&&Field<Text[]>(panel,"queuedPositions")[3].text=="5 · +7 MORE",
                        "Three barracks show all three active orders before pending orders and keep the correct overflow total");
                    Check(Field<Text[]>(panel,"queuedKinds")[1].text=="B3 · 0%"&&
                        Field<Array>(panel,"productionTargets").Cast<object>().All(t=>t==null),
                        "Third active order identifies its building and aggregate cards never cancel unrelated orders");
                    second.queue[0].remaining=second.queue[0].total/2;Refresh();
                    Check(Field<Text[]>(panel,"queuedKinds")[0].text=="B2 · 50%"&&
                        Mathf.Abs(Field<Image[]>(panel,"queuedProgress")[0].rectTransform.rect.width-68)<.1f&&
                        Field<Image[]>(panel,"queuedProgress")[2].rectTransform.rect.width==0,
                        "Small active cards show their own progress while waiting orders have no active progress bar");
                    session.Pause("tracking",true);Refresh();
                    Check(Text("productionState").Contains("PAUSED")&&Field<Text[]>(panel,"queuedKinds")[0].text=="B2 · PAUSED",
                        "Pausing marks both primary and secondary active production cards");
                    session.Pause("tracking",false);Refresh();
                    int groupCount=barracks.queue.Count+second.queue.Count+third.queue.Count;
                    barracks.queue[0].remaining=1;session.Match.Step();
                    Check(session.Send(CommandKind.Train,session.Catalog.Role(faction,"scout").id),"Group replacement order accepted after completing a unit");Refresh();
                    Check(barracks.queue.Count+second.queue.Count+third.queue.Count==groupCount&&Text("productionName")==session.Catalog.Role(faction,"melee").name&&
                        Field<Text[]>(panel,"queuedPositions")[3].text=="5 · +7 MORE",
                        "Completing and replacing a group order updates its card even when total queue length is unchanged");
                    for(int i=0;i<3;i++)Click("Train "+session.Catalog.Role(faction,"scout").name);
                    Check(Text("productionCount")=="15 / 15 · FULL"&&Field<Text[]>(panel,"queuedPositions")[3].text=="5 · +10 MORE",
                        "Three full barracks keep all five cards and a two-digit additional-order count");
                    Canvas.ForceUpdateCanvases();
                    Check(Field<Text[]>(panel,"queuedPositions").Concat(Field<Text[]>(panel,"queuedKinds")).All(Fits)&&Fits(Field<Text>(panel,"productionHint")),
                        "Overflow count, building state and group hint fit their card labels");
                    Capture(faction+"-three-barracks-full");
                    session.Select(second.id,false);Refresh();
                    Check(Field<Text[]>(panel,"queuedPositions")[3].text=="5"&&Field<Image[]>(panel,"queuedProgress").All(p=>p.rectTransform.rect.width==0)&&
                        Field<Array>(panel,"productionTargets").Cast<object>().Count(t=>t!=null)==second.queue.Count,
                        "Returning from overflow to one barracks removes group progress and restores all individual cancellation targets");
                    foreach(var extra in new[]{second,third,groupSupply})
                    {session.Match.Entities.Remove(extra.id);session.Match.Map.Occupy(extra.position,session.Catalog[extra.definition].footprint,0);}
                    barracks.queue.Clear();
                    session.Select(barracks.id,false);Refresh();
                    Click("Train "+session.Catalog.Role(faction,"scout").name);
                    int size=session.Catalog[barracks.definition].footprint;
                    Border(9999);barracks.queue[0].remaining=1;session.Match.Step();Refresh();
                    Check(barracks.queue.Count==1&&Text("productionProgress")=="100% · Clear the building exit","Finished unit with blocked exit shows deployment feedback");Capture(faction+"-blocked-exit");
                    Border(0);session.Match.Step();Refresh();
                    Check(barracks.queue.Count==0&&Text("productionName")=="Queue empty","Opening the exit deploys the unit and clears the display");
                    CheckQueueModels();
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
                string[] ShownNames()=>new[]{Text("productionName")}.Concat(Field<Text[]>(panel,"queuedNames").Select(t=>t.text)).ToArray();
                bool QueueShown()=>Field<RectTransform>(panel,"productionStrip").gameObject.activeInHierarchy;
                bool Inside(RectTransform parent,RectTransform child)
                {
                    var corners=new Vector3[4];child.GetWorldCorners(corners);var rect=parent.rect;
                    return corners.Select(parent.InverseTransformPoint).All(p=>p.x>=rect.xMin-.1f&&p.x<=rect.xMax+.1f&&p.y>=rect.yMin-.1f&&p.y<=rect.yMax+.1f);
                }
                bool Fits(Text text)
                {
                    text.ForceMeshUpdate();
                    return !text.isTextOverflowing && (!text.enableAutoSizing || text.fontSize >= text.fontSizeMin);
                }
                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");
                }
                void Click(string title)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.gameObject.activeInHierarchy&&t.name==title);
                    var hit=rect.GetComponentInChildren<SelectionPortrait>()?.rectTransform??rect;
                    var center=hit.TransformPoint(hit.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    if(!panel.Point(ray,true,false)||!panel.Point(ray,false,true))throw new Exception("Pointer missed "+title);Refresh();
                }
                void CheckQueueModels()
                {
                    Canvas.ForceUpdateCanvases();
                    var portraits=new[]{Field<SelectionPortrait>(panel,"productionPortrait")}.Concat(Field<SelectionPortrait[]>(panel,"queuedPortraits")).ToArray();
                    var names=ShownNames();
                    for(int i=0;i<portraits.Length;i++)
                    {
                        var type=session.Catalog.Definition.archetypes.SingleOrDefault(t=>!t.building&&t.name==names[i]);
                        Check(type==null?!portraits[i].gameObject.activeSelf:MatchesModel(portraits[i],type),
                            "Queue portrait matches the displayed order, or clears for empty/research: "+names[i]);
                        var card=(RectTransform)portraits[i].transform.parent;
                        Check(card.GetComponentsInChildren<Text>().All(text=>Inside(card,text.rectTransform)&&Fits(text)),
                            "Queue name, status and progress remain readable with its model: "+names[i]);
                        if(type!=null)Check(Inside(card,portraits[i].rectTransform),"Queue model stays inside its card");
                    }
                }
                void Capture(string name)
                {
                    CheckActionModels(panel,session,Check);CheckQueueModels();
                    HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;
                }
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
        internal static bool MatchesModel(SelectionPortrait portrait,Archetype type)
        {
            if(portrait==null||!portrait.gameObject.activeInHierarchy||portrait.raycastTarget)return false;
            var expected=new GameObject("Expected model",typeof(RectTransform),typeof(SelectionPortrait));
            try
            {
                var reference=expected.GetComponent<SelectionPortrait>();reference.SetType(type);
                portrait.Rebuild(CanvasUpdate.PreRender);
                return ReferenceEquals(Field<object>(portrait,"faces"),Field<object>(reference,"faces"))&&portrait.canvasRenderer.GetMesh().vertexCount>0;
            }
            finally{Object.DestroyImmediate(expected);}
        }
        internal static void CheckActionModels(CommandPanel panel,GameSession session,Action<bool,string> check)
        {
            Canvas.ForceUpdateCanvases();var area=Field<RectTransform>(panel,"actions");
            foreach(var action in SelectionActions.For(session).Items.Where(a=>a.Category==ActionCategory.Build||a.Category==ActionCategory.Train))
            {
                var type=session.Catalog[action.Id.Substring(action.Id.IndexOf(':')+1)];
                var card=area.Cast<RectTransform>().Single(t=>t.name==action.Title);
                var portrait=card.GetComponentInChildren<SelectionPortrait>();
                check(MatchesModel(portrait,type),"Action shows the actual model: "+action.Title);
                var modelRect=new Rect(portrait.rectTransform.anchoredPosition-new Vector2(0,portrait.rectTransform.rect.height),portrait.rectTransform.rect.size);
                foreach(var label in card.GetComponentsInChildren<Text>())
                {
                    var rect=label.rectTransform;
                    label.ForceMeshUpdate();
                    check(!label.isTextOverflowing&&
                        !modelRect.Overlaps(new Rect(rect.anchoredPosition-new Vector2(0,rect.rect.height),rect.rect.size)),
                        "Action text fits beside the model: "+action.Title+" / "+label.text);
                }
                check(card.GetComponentsInChildren<Text>().Any(t=>t.text.Replace(" ","").Contains(type.minerals+"M")&&(type.catalyst==0||t.text.Replace(" ","").Contains(type.catalyst+"G"))),
                    "Action keeps its mineral and gas costs: "+action.Title);
            }
        }
    }
}
