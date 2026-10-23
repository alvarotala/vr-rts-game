using System;
using System.Collections.Generic;
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
                    var options=Field<RectTransform>(panel,"actionCard");var optionsPosition=options.anchoredPosition;
                    var producerPosition=Field<RectTransform>(panel,"productionStrip").anchoredPosition;
                    var mobile=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building);
                    session.Select(0,false);Refresh();
                    Check(options.gameObject.activeInHierarchy&&options.rect.height==468&&options.anchoredPosition==optionsPosition&&ActionCards().Length==0,
                        "Empty selection retains the fixed Options area without action cards");
                    session.Select(hq.id,false);session.Select(mobile.id,true);Refresh();
                    Check(options.gameObject.activeInHierarchy&&options.rect.height==468&&options.anchoredPosition==optionsPosition&&ActionCards().Length==0,
                        "Mixed units and buildings retain the same empty Options area");
                    var producerLayouts=new Dictionary<int,Dictionary<string,Vector2>>();
                    foreach(var producer in new[]{hq,barracks,tech})
                    {
                        session.Select(producer.id,false);Refresh();
                        producerLayouts[producer.id]=ActionLayout();
                        if(producer==barracks)CheckBarracksRoster();
                        session.Match.Players[0].minerals-=7;Refresh();
                        CheckSameActions(producerLayouts[producer.id],"Resource updates preserve the selected producer's action positions");
                        session.Match.Players[0].minerals+=7;
                    }
                    session.Select(hq.id,false);session.Select(barracks.id,true);session.Select(tech.id,true);Refresh();
                    var groupedCards=ActionCards();
                    var expectedGroupActions=producerLayouts.Values.SelectMany(layout=>layout.Keys).Distinct().OrderBy(name=>name);
                    Check(groupedCards.Select(card=>card.name).OrderBy(name=>name).SequenceEqual(expectedGroupActions),
                        "Combined producers expose every distinct option without duplicating cards");
                    Check(options.rect.height==468&&options.anchoredPosition==optionsPosition,"Grouping producers keeps Options in the same fixed position");
                    session.Select(hq.id,false);Refresh();
                    Check(QueueShown()&&Text("productionName")=="Queue empty","Producer shows its empty queue before clicking Train");
                    Capture(faction+"-idle");float height=Field<RectTransform>(panel,"root").rect.height;
                    var worker=session.Catalog.Role(faction,"worker");Click("Train "+worker.name);
                    Check(hq.queue.Count==1&&Text("productionName")==worker.name,"Training click immediately shows the actual queued worker");
                    CheckSameActions(producerLayouts[hq.id],"Enqueue preserves the headquarters action layout");
                    Check(Field<RectTransform>(panel,"root").rect.height==height,"Enqueue does not move the production controls");
                    Check(options.rect.height==468&&options.anchoredPosition==optionsPosition
                        &&Field<RectTransform>(panel,"productionStrip").anchoredPosition==producerPosition,
                        "Options and production targets keep their positions when the first order is queued");
                    for(int i=0;i<worker.duration/2;i++)session.Match.Step();Refresh();
                    Check(Mathf.Abs(Field<Image>(panel,"productionFill").rectTransform.rect.width-160)<.1f&&Text("productionProgress").Contains("50%"),"Progress bar follows simulation ticks");
                    int seconds=(hq.queue[0].remaining+Simulation.TickRate-1)/Simulation.TickRate;
                    Check(Text("productionProgress").Contains(seconds+"s remaining"),"Countdown rounds partial seconds up using the simulation tick rate");Capture(faction+"-worker");

                    session.Select(barracks.id,false);
                    var roles=new[]{"scout","melee","ranged","melee","scout"};
                    foreach(var role in roles)Click("Train "+session.Catalog.Role(faction,role).name);
                    CheckBarracksRoster();CheckSameActions(producerLayouts[barracks.id],"A full queue preserves all three barracks positions");
                    Check(Text("productionCount")=="5 / 5 · FULL"&&barracks.queue.Count==5,"Full queue is visible");
                    CheckActiveBorders(false,false,false,true);
                    Check(Field<Text[]>(panel,"queuedNames").Select(t=>t.text).SequenceEqual(roles.Skip(1).Select(r=>session.Catalog.Role(faction,r).name)),"All four pending items preserve FIFO order, including duplicates");
                    Capture(faction+"-full");Click("Train "+session.Catalog.Role(faction,"scout").name);
                    Check(barracks.queue.Count==5&&Text("productionCount").Contains("FULL"),"Rejected sixth order leaves the displayed queue unchanged");
                    int minerals=session.View.minerals;Click("Queue position 5");
                    Check(barracks.queue.Count==4&&session.View.minerals==minerals+session.Catalog.Role(faction,"scout").minerals&&Field<Text[]>(panel,"queuedNames")[3].text=="Empty","Cancel refunds only the last item and clears its slot");
                    CheckActiveBorders(false,false,false,false);
                    CheckQueueModels();
                    // Finish and enqueue between HUD refreshes: the length is unchanged, the active item is not.
                    barracks.queue[0].remaining=1;session.Match.Step();
                    Check(session.Send(CommandKind.Train,session.Catalog.Role(faction,"ranged").id),"Replacement order accepted");Refresh();
                    Check(barracks.queue.Count==4&&Text("productionName")==session.Catalog.Role(faction,"melee").name,"Active name advances even when queue length stays the same");Capture(faction+"-next");
                    var soldier=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building&&session.Catalog[e.definition].role!="worker");
                    session.Select(mobile.id,false);Refresh();
                    var orderTitles=new[]{"Attack Move","Stop","Hold Position"};
                    var workerOrders=ActionCards().Where(card=>orderTitles.Contains(card.name)).ToDictionary(card=>card.name,card=>card.anchoredPosition);
                    session.Select(soldier.id,false);Refresh();
                    var soldierOrders=ActionCards().Where(card=>orderTitles.Contains(card.name)).ToArray();
                    Check(workerOrders.Count==3&&soldierOrders.Length==3&&soldierOrders.All(card=>workerOrders[card.name]==card.anchoredPosition),
                        "Attack Move, Stop and Hold keep their slots when switching from a worker to a soldier");

                    session.Select(tech.id,false);Click("Firepower I");Click("Train "+session.Catalog.Role(faction,"siege").name);Click("Armor I");
                    CheckSameActions(producerLayouts[tech.id],"Training and research preserve the technology building's compact layout");
                    for(int i=0;i<175;i++)session.Match.Step();Refresh();
                    Check(Text("productionName")=="Firepower I"&&Text("productionState").Contains("RESEARCHING")&&Text("productionProgress").Contains("25%"),"Research shares the ordered queue and advances its progress");
                    Capture(faction+"-research");
                    session.Pause("tracking",true);int remaining=tech.queue[0].remaining;session.Match.Step();Refresh();
                    Check(Text("productionState").Contains("PAUSED")&&tech.queue[0].remaining==remaining,"External pause freezes production and labels it paused");Capture(faction+"-paused");session.Pause("tracking",false);
                    session.Select(0,false);board.Hovered=tech.id;Refresh();
                    Check(!QueueShown()&&Text("selectionDetails").Contains("Select to view production"),"Hover invites selection instead of referring to a hidden queue");
                    Check(Field<Text>(panel,"selectionFallbackTitle").gameObject.activeInHierarchy&&Text("selectionFallbackTitle")==session.Catalog[tech.definition].name
                        &&Text("selection")=="","Hover shows the producer name in the fallback row while the selection count stays empty");
                    board.Hovered=0;
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
                    Check(QueueShown()&&Text("productionTitle")=="PRODUCTION"&&Text("productionName")=="Firepower I",
                        "Multiple producers show their actual production orders");
                    Check(Text("productionCount")==$"{techOrders+hqOrders} / 10"&&Field<Text[]>(panel,"queuedNames")[0].text==session.Catalog.Role(faction,"siege").name,
                        "Group fills subsequent slots with actual orders and totals combined capacity");
                    Check(Text("productionHint").Contains("including the current one")&&Field<Array>(panel,"productionTargets").Cast<object>().Count(t=>t!=null)==Math.Min(5,techOrders+hqOrders),
                        "Each occupied group card has an exact producer cancellation target");
                    panel.Point(pendingRay,false,true);Refresh();
                    Check(tech.queue.Count==techOrders&&hq.queue.Count==hqOrders,"Changing selection invalidates a pressed individual queue item");
                    Capture(faction+"-group");
                    session.Pause("tracking",true);Refresh();
                    Check(Text("productionState").Contains("PAUSED"),"Building group production displays the paused state");session.Pause("tracking",false);
                    var foundation=AddBuilding("barracks",1003,new Point(28,12));foundation.complete=false;Refresh();
                    session.Select(barracks.id,false);session.Select(foundation.id,true);Refresh();
                    Check(QueueShown()&&Text("productionTitle")=="PRODUCTION"&&Text("productionName")==session.Catalog[barracks.queue[0].definition].name&&
                        Text("productionCount")==$"{barracks.queue.Count} / 5",
                        "A completed barracks and unfinished barracks show the available producer's actual queue");
                    Check(Text("productionHint").Contains("including the current one")&&Field<Array>(panel,"productionTargets").Cast<object>().Count(t=>t!=null)==barracks.queue.Count,
                        "One eligible producer in a larger selection retains exact item cancellation");
                    session.Select(supply.id,true);Refresh();
                    Check(QueueShown()&&Text("productionCount")==$"{barracks.queue.Count} / 5"&&Text("productionTitle")=="PRODUCTION",
                        "Nonproducing buildings do not hide or inflate a building group's production summary");
                    session.Select(unit.id,true);Refresh();Check(!QueueShown(),"A mixed unit and building selection hides the producer summary");
                    session.Match.Entities.Remove(foundation.id);session.Match.Map.Occupy(foundation.position,session.Catalog[foundation.definition].footprint,0);
                    session.Select(tech.id,false);Refresh();
                    Check(Text("productionTitle")=="PRODUCTION"&&Text("productionHint").Contains("including the current one")&&
                        Field<Text[]>(panel,"queuedPositions").Select(t=>t.text).SequenceEqual(new[]{"2","3","4","5"}),
                        "Returning to one building restores its individual queue labels");
                    pendingCenter=pendingCard.TransformPoint(pendingCard.rect.center);pendingRay=new Ray(pendingCenter-pendingCard.forward,pendingCard.forward);
                    Check(panel.Point(pendingRay,true,false)&&panel.Point(pendingRay,false,true),"Returning to one building restores its real cancellation target");Refresh();
                    Check(tech.queue.Count==techOrders-1&&hq.queue.Count==hqOrders&&tech.queue[0].definition=="attack",
                        "Individual cancellation after a group affects only the chosen item and producer");
                    session.Select(tech.id,false);tech.complete=false;Refresh();Check(!QueueShown(),"Unfinished producer hides production queue");tech.complete=true;Refresh();
                    panel.ToggleMenu();Refresh();Check(!QueueShown(),"Pause menu hides match production queue");panel.ToggleMenu();Refresh();
                    Check(QueueShown()&&Text("productionName")=="Firepower I","Resuming restores the selected building queue");

                    // Shared actions retain rally clearing; the HUD only renders training and research.
                    var academy=AddBuilding("academy",1004,new Point(28,18));
                    var allProducers=new[]{hq,barracks,tech,academy};
                    foreach(var producer in allProducers){producer.hasRallyPoint=true;producer.rallyPoint=new Point(20,20);}
                    Refresh();session.Select(0,false);foreach(var producer in allProducers)session.Select(producer.id,true);Refresh();
                    var groupActions=SelectionActions.For(session).Items;
                    Check(groupActions.Count==9&&groupActions.Any(a=>a.Id=="clear-rally-point"),
                        "All four producer roles expose six train actions, two upgrades and rally clearing");
                    Check(Text("productionState").Contains("TRAINING")&&Text("productionHint").Contains("including the current one"),
                        "Producer group shows current production and specific card cancellation");
                    Canvas.ForceUpdateCanvases();
                    var actionArea=Field<RectTransform>(panel,"actions");
                    var actionCards=actionArea.Cast<RectTransform>().Where(t=>t.GetComponent<Image>()!=null).ToArray();
                    Check(actionCards.Length==8&&actionCards.All(card=>card.name!="Clear Rally Points"&&card.name!="Cancel / refund")
                        &&Inside(Field<RectTransform>(panel,"root"),actionArea)&&actionCards.All(card=>Inside(actionArea,card)),
                        "All eight training and research actions fit in Options without redundant cancellation or rally controls");
                    Check(actionCards.All(card=>card.GetComponentsInChildren<Text>().All(text=>Inside(card,text.rectTransform)&&Fits(text))),
                        "Names and costs fit completely inside every production action");
                    Check(actionCards.SelectMany((card,i)=>actionCards.Skip(i+1).Select(other=>
                        new Rect(card.anchoredPosition-new Vector2(0,card.rect.height),card.rect.size).Overlaps(
                        new Rect(other.anchoredPosition-new Vector2(0,other.rect.height),other.rect.size)))).All(overlap=>!overlap),
                        "Compact production actions do not overlap");
                    Check(Fits(Field<Text>(panel,"productionState"))&&!Field<Text>(panel,"productionHint").gameObject.activeSelf,
                        "Group status fits its card and detailed cancellation guidance stays in the help dialog");
                    var matchRoot=Field<RectTransform>(panel,"root");
                    Check(matchRoot.sizeDelta==new Vector2(1480,1636),
                        "The fixed HUD preserves its previous design dimensions independently of the user's saved window scale");
                    Capture(faction+"-all-producers");
                    int totalOrders=allProducers.Sum(producer=>producer.queue.Count);
                    var allProducerLayout=ActionLayout();
                    Click("Train "+session.Catalog.Role(faction,"caster").name);
                    CheckSameActions(allProducerLayout,"Enqueue preserves the combined producer roster's positions");
                    Check(academy.queue.Count==1&&allProducers.Sum(producer=>producer.queue.Count)==totalOrders+1,
                        "The compact caster button queues exactly one order at the compatible selected producer");
                    if(hq.queue.Count==0)Click("Train "+session.Catalog.Role(faction,"worker").name);
                    Check(allProducers.All(producer=>producer.queue.Count>0),"Four producers have concurrent real orders for active-card feedback");
                    CheckActiveBorders(true,true,true,true,true);
                    Capture(faction+"-parallel-active");
                    Check(groupActions.Single(action=>action.Id=="clear-rally-point").Execute(),"Shared clear-rally action remains available outside the HUD");Refresh();
                    Check(allProducers.All(producer=>!producer.hasRallyPoint),"Shared clear-rally action still clears every selected producer");
                    session.Match.Entities.Remove(academy.id);session.Match.Map.Occupy(academy.position,session.Catalog[academy.definition].footprint,0);

                    // Isolate deployment feedback with a real finished order and occupied perimeter.
                    hq.queue.Clear();tech.queue.Clear();barracks.queue.Clear();session.Select(barracks.id,false);session.Select(tech.id,true);Refresh();
                    Check(Text("productionCount")=="0 / 10"&&Text("productionName")=="Queue empty"&&Text("productionState")=="IDLE"&&
                        Field<Text[]>(panel,"queuedNames").All(t=>t.text=="Empty"),
                        "An idle producer group clears all five slots and its active state");
                    CheckActiveBorders(false,false,false,false);
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
                    CheckActiveBorders(true,false,false,true);
                    Capture(faction+"-group-five");
                    Click("Train "+session.Catalog.Role(faction,"ranged").name);
                    Check(Field<Text[]>(panel,"queuedPositions")[3].text=="5 · +1 MORE","The sixth order appears as +1 on the fifth card");
                    CheckActiveBorders(true,false,false,true,true);
                    Capture(faction+"-group-plus-one");
                    for(int i=0;i<2;i++)Click("Train "+session.Catalog.Role(faction,"ranged").name);
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
                        Field<Array>(panel,"productionTargets").Cast<object>().Count(t=>t!=null)==5,
                        "Third active order identifies its building and all five visible orders retain exact cancellation targets");
                    CheckActiveBorders(true,true,false,true,true);
                    second.queue[0].remaining=second.queue[0].total/2;Refresh();
                    Check(Field<Text[]>(panel,"queuedKinds")[0].text=="B2 · 50%"&&
                        Mathf.Abs(Field<Image[]>(panel,"queuedProgress")[0].rectTransform.rect.width-68)<.1f&&
                        Field<Image[]>(panel,"queuedProgress")[2].rectTransform.rect.width==0,
                        "Small active cards show their own progress while waiting orders have no active progress bar");
                    session.Pause("tracking",true);Refresh();
                    Check(Text("productionState").Contains("PAUSED")&&Field<Text[]>(panel,"queuedKinds")[0].text=="B2 · PAUSED",
                        "Pausing marks both primary and secondary active production cards");
                    CheckActiveBorders(true,true,false,true,true);
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
                    Check(Field<Text[]>(panel,"queuedPositions").Concat(Field<Text[]>(panel,"queuedKinds")).All(Fits),
                        "Overflow count and building state fit their visible card labels");
                    Capture(faction+"-three-barracks-full");
                    session.Select(second.id,false);Refresh();
                    Check(Field<Text[]>(panel,"queuedPositions")[3].text=="5"&&Field<Image[]>(panel,"queuedProgress").All(p=>p.rectTransform.rect.width==0)&&
                        Field<Array>(panel,"productionTargets").Cast<object>().Count(t=>t!=null)==second.queue.Count,
                        "Returning from overflow to one barracks removes group progress and restores all individual cancellation targets");
                    CheckActiveBorders(false,false,false,true);
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
                void CheckActiveBorders(bool second,bool third,bool fourth,bool fifth,bool overflow=false)
                {
                    var expected=new[]{second,third,fourth,fifth};
                    var borders=Field<Image[]>(panel,"queuedActiveBorders");
                    Check(borders.Select(border=>border.gameObject.activeSelf).SequenceEqual(expected),
                        "Parallel producers and the occupied fifth card have borders: "+string.Join(",",expected));
                    for(int i=0;i<borders.Length;i++)
                    {
                        var border=borders[i];if(!border.gameObject.activeSelf)continue;
                        var interior=border.rectTransform.GetChild(0).GetComponent<Image>();
                        bool correctColor=i==3&&overflow?border.color.r>border.color.b&&border.color.g>border.color.b&&border.color.r>=border.color.g
                            :border.color.g>border.color.r&&border.color.g>border.color.b;
                        Check(!border.raycastTarget&&!interior.raycastTarget&&border.type==Image.Type.Sliced
                            &&correctColor
                            &&interior.rectTransform.anchoredPosition==new Vector2(4,-4)
                            &&interior.rectTransform.sizeDelta==border.rectTransform.sizeDelta-Vector2.one*8,
                            "Production keeps its native edge green, or yellow for overflow, without covering content or pointer targets");
                    }
                }
                RectTransform[] ActionCards()=>Field<RectTransform>(panel,"actions").Cast<RectTransform>().Where(t=>t.GetComponent<Image>()!=null).ToArray();
                Dictionary<string,Vector2> ActionLayout()=>ActionCards().ToDictionary(card=>card.name,card=>card.anchoredPosition);
                void CheckSameActions(Dictionary<string,Vector2> expected,string message)
                {
                    var actual=ActionCards();
                    Check(actual.Length==expected.Count&&actual.All(card=>expected.TryGetValue(card.name,out var position)&&position==card.anchoredPosition),message);
                }
                void CheckBarracksRoster()
                {
                    var cards=ActionCards();
                    var expected=new[]{"scout","melee","ranged"}.Select(role=>"Train "+session.Catalog.Role(session.Faction,role).name).ToArray();
                    var ordered=cards.OrderBy(card=>card.anchoredPosition.x).ToArray();
                    Check(ordered.Length==3&&ordered.Select(card=>card.name).SequenceEqual(expected)
                        &&ordered[0].anchoredPosition==Vector2.zero
                        &&ordered.All(card=>Mathf.Abs(card.anchoredPosition.y)<.01f),
                        "Barracks starts with Scout, Melee and Ranged in the first row, without an empty leading slot");
                    float gap=ordered[1].anchoredPosition.x-ordered[0].rect.width;
                    Check(gap>0&&Mathf.Abs(ordered[2].anchoredPosition.x-ordered[1].anchoredPosition.x-ordered[1].rect.width-gap)<.01f,
                        "The three barracks actions occupy adjacent equally spaced columns");
                }
                void CheckHeaderCounters()
                {
                    Canvas.ForceUpdateCanvases();
                    foreach(var fields in new[]{new[]{"selectedKind","selection","selectedCard"},new[]{"productionTitle","productionCount","productionStrip"}})
                    {
                        var title=Field<Text>(panel,fields[0]);var count=Field<Text>(panel,fields[1]);var section=Field<RectTransform>(panel,fields[2]);
                        if(!section.gameObject.activeInHierarchy||string.IsNullOrEmpty(count.text))continue;
                        float Left(Text label)=>section.InverseTransformPoint(label.rectTransform.TransformPoint(new Vector3(label.rectTransform.rect.xMin,label.rectTransform.rect.yMax,0))).x;
                        float gap=Left(count)-Left(title)-title.preferredWidth;
                        Check(gap>=8&&gap<=24&&count.alignment==TextAnchor.MiddleLeft&&Fits(count),
                            "Section counter follows its translated title without overlap: "+title.text+" "+count.text);
                    }
                }
                bool Inside(RectTransform parent,RectTransform child)
                {
                    var corners=new Vector3[4];child.GetWorldCorners(corners);var rect=parent.rect;
                    return corners.Select(parent.InverseTransformPoint).All(p=>p.x>=rect.xMin-.1f&&p.x<=rect.xMax+.1f&&p.y>=rect.yMin-.1f&&p.y<=rect.yMax+.1f);
                }
                bool Fits(Text text)
                {
                    var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
                    if(text.resizeTextForBestFit)settings.fontSize=text.resizeTextMinSize;
                    settings.resizeTextForBestFit=false;
                    return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit<=text.rectTransform.rect.height+.5f;
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
                    var portraits=rect.GetComponentsInChildren<SelectionPortrait>(true).ToDictionary(p=>p,p=>p.color);
                    var border=rect.GetComponentsInChildren<Image>(true).FirstOrDefault(image=>image.name=="Active producer border");
                    var borderColor=border==null?Color.clear:border.color;
                    panel.Point(ray,false,false);
                    Check(rect.GetComponent<Image>().color==ConsoleTheme.Accent
                        &&rect.GetComponentsInChildren<Text>(true).All(text=>text.color==ConsoleTheme.HoverInk)
                        &&portraits.All(p=>p.Key.color==p.Value),"Production and option hovers use accent with readable text while preserving models");
                    if(border!=null)
                        Check(border.color==borderColor&&border.transform.GetChild(0).GetComponent<Image>().color==ConsoleTheme.Accent,
                            "The fifth production edge retains its meaning while its interior follows the shared hover");
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
                    CheckActionModels(panel,session,Check);CheckQueueModels();CheckHeaderCounters();
                    HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;
                    if(name!="industrial-all-producers"&&name!="industrial-full"&&name!="industrial-parallel-active"&&name!="industrial-group-plus-one")return;
                    string language=Localization.Preference;
                    try
                    {
                        Localization.SetPreference("es");CheckHeaderCounters();
                        HarvestingPreview.SaveCanvas(panel.transform,Field<RectTransform>(panel,"root"),output+"/"+name+"-es.png",preview,1800,1600);captures++;
                    }
                    finally{Localization.SetPreference(language);Canvas.ForceUpdateCanvases();}
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
            var available=SelectionActions.For(session).Items.Where(action=>action.Category!=ActionCategory.Cancel).ToArray();
            var cards=area.Cast<RectTransform>().Where(card=>card.GetComponent<Image>()!=null).ToArray();
            check(cards.Length==available.Length&&cards.Select(card=>card.name).OrderBy(name=>name).SequenceEqual(available.Select(action=>action.Title).OrderBy(name=>name)),
                "HUD contains every eligible action and excludes cancellation and rally clearing");
            check(cards.All(card=>card.rect.size==new Vector2(328,112)),"All Options actions use the same readable target size");
            foreach(var action in available.Where(a=>a.Category==ActionCategory.Build||a.Category==ActionCategory.Train))
            {
                var type=session.Catalog[action.Id.Substring(action.Id.IndexOf(':')+1)];
                var card=area.Cast<RectTransform>().Single(t=>t.name==action.Title);
                var portrait=card.GetComponentInChildren<SelectionPortrait>();
                check(MatchesModel(portrait,type),"Action shows the actual model: "+action.Title);
                var modelRect=new Rect(portrait.rectTransform.anchoredPosition-new Vector2(0,portrait.rectTransform.rect.height),portrait.rectTransform.rect.size);
                foreach(var label in card.GetComponentsInChildren<Text>())
                {
                    var rect=label.rectTransform;
                    var settings=label.GetGenerationSettings(rect.rect.size);settings.resizeTextForBestFit=false;
                    if(label.resizeTextForBestFit)settings.fontSize=label.resizeTextMinSize;
                    check(label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit<=rect.rect.height+.5f&&
                        !modelRect.Overlaps(new Rect(rect.anchoredPosition-new Vector2(0,rect.rect.height),rect.rect.size)),
                        "Action text fits beside the model: "+action.Title+" / "+label.text);
                }
                check(card.GetComponentsInChildren<Text>().Any(t=>t.text.Replace(" ","").Contains(type.minerals+"M")&&(type.catalyst==0||t.text.Replace(" ","").Contains(type.catalyst+"G"))),
                    "Action keeps its mineral and gas costs: "+action.Title);
            }
        }
    }
}
