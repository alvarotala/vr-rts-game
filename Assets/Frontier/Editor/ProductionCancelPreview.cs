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
    public static class ProductionCancelPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        private static void Invoke(object obj,string name)=>obj.GetType().GetMethod(name,Private).Invoke(obj,null);
        public static void ValidateConstruction()
        {
            string language=Localization.Preference;
            try{Localization.SetPreference("en");RenderChecks(false);ActionWheelPreview.Render();HudLayoutPreview.Render();}
            finally{Localization.SetPreference(language);}
        }

        [MenuItem("Frontier/Preview and check queue cancellation")]
        public static void Render()=>RenderChecks(true);
        private static void RenderChecks(bool runRelatedChecks)
        {
            Directory.CreateDirectory("TestResults/ProductionCancel");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Queue cancellation fixture");var console=new GameObject("Queue cancellation console");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");preview.AddSingleGO(console);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Panel=panel;input.Quest=quest;
                var miss=new Ray(new Vector3(100,100,100),Vector3.up);

                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    Add("supply",1000);var barracks=Add("barracks",1001);var tech=Add("tech",1002);var lab=Add("research",1003);
                    var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    session.Select(hq.id,false);Refresh();var hudSize=Field<RectTransform>(panel,"root").sizeDelta;
                    var productionPosition=Field<RectTransform>(panel,"productionStrip").anchoredPosition;
                    foreach(var type in session.Catalog.Definition.archetypes.Where(t=>t.faction==faction&&t.building))
                    {
                        var foundation=new EntityState{id=1500,owner=0,definition=type.id,health=type.health*.25f,complete=false,
                            remaining=type.duration*3/4,position=new Point(32,16)};
                        session.Match.Entities.Add(foundation.id,foundation);session.Match.Map.Occupy(foundation.position,type.footprint,foundation.id);
                        Refresh();session.Select(foundation.id,false);Refresh();
                        Check(Card("Cancel construction").IsChildOf(Field<RectTransform>(panel,"actions")),
                            "Every unfinished building exposes Cancel construction in Options: "+type.id);
                        Check(!Field<RectTransform>(panel,"productionStrip").gameObject.activeInHierarchy
                            &&Field<Array>(panel,"productionTargets").Cast<object>().All(t=>t==null),"An unfinished building keeps Production empty and non-interactive");
                        Check(Field<RectTransform>(panel,"root").sizeDelta==hudSize
                            &&Field<RectTransform>(panel,"productionStrip").anchoredPosition==productionPosition,"Foundation selection preserves HUD size and production position");
                        if(type.role=="barracks")
                        {
                            string language=Localization.Preference;Localization.SetPreference("es");Refresh();
                            Canvas.ForceUpdateCanvases();
                            foreach(var text in Card("Cancel construction").GetComponentsInChildren<Text>())
                                Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Spanish construction option fits: "+text.text);
                            Capture(faction+"-construction");Localization.SetPreference(language);Refresh();
                        }
                        int minerals=session.View.minerals,gasBefore=session.View.catalyst;
                        Click("Cancel construction",faction=="biological");
                        Check(!session.Match.Entities.ContainsKey(foundation.id)&&session.View.minerals==minerals+(int)(type.minerals*.75f)
                            &&session.View.catalyst==gasBefore+(int)(type.catalyst*.75f),"Options cancels construction and refunds its mineral and gas cost: "+type.id);
                        Check(Field<RectTransform>(panel,"root").sizeDelta==hudSize&&!Field<RectTransform>(panel,"productionStrip").gameObject.activeInHierarchy,
                            "Cancelling construction clears the card without resizing the menu");
                    }
                    var finishing=Add("barracks",1004);finishing.complete=false;Refresh();session.Select(finishing.id,false);Refresh();
                    var constructionRay=RayTo("Cancel construction");Frame(constructionRay,true,false,false);
                    finishing.complete=true;finishing.remaining=0;Refresh();Frame(constructionRay,false,true,false);Refresh();
                    Check(session.Match.Entities.ContainsKey(finishing.id)&&Field<Text>(panel,"productionName").text=="Queue empty"
                        &&!panel.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Cancel construction"&&r.gameObject.activeInHierarchy),
                        "Completing construction during a held click switches to production without cancelling anything: exists="+session.Match.Entities.ContainsKey(finishing.id)
                        +", production="+Field<Text>(panel,"productionName").text+", actions="+string.Join(",",SelectionActions.For(session).Items.Select(a=>a.Id)));
                    foreach(var producer in new[]{hq,barracks})
                    {
                        session.Select(producer.id,false);string role=producer==hq?"worker":"ranged";
                        var type=session.Catalog.Role(faction,role);
                        for(int i=0;i<5;i++)Check(session.Send(CommandKind.Train,type.id),"Queue five "+type.name);
                        Refresh();var orders=producer.queue.ToArray();int minerals=session.View.minerals,supply=session.View.supply;
                        var ray=RayTo("Queue position 3");panel.Point(ray,false,false);
                        Check(IsHighlighted("Queue position 3")&&Hints()[2].gameObject.activeSelf&&!Hints()[1].gameObject.activeSelf,"Only the hovered duplicate uses the accent and offers cancellation");
                        Refresh();Check(IsHighlighted("Queue position 3"),"Periodic progress refresh preserves the accent hover");Capture(faction+"-"+role+"-five");
                        panel.Point(miss,false,false);Check(!IsHighlighted("Queue position 3")&&Hints().All(t=>!t.gameObject.activeSelf),"Leaving the queue clears all cancel hints");
                        Click("Queue position 3",true);
                        Check(producer.queue.Select(q=>q.id).SequenceEqual(orders.Where((q,i)=>i!=2).Select(q=>q.id)),"A cancels the chosen middle duplicate only");
                        Check(session.View.minerals==minerals+type.minerals&&session.View.supply==supply-1&&panel.Page==CommandPanel.MenuPage.Actions,"One click refunds and releases supply without a confirmation page");
                        Capture(faction+"-"+role+"-cancelled");
                        Click("Current production",false);Check(producer.queue[0].id==orders[1].id&&producer.queue.Count==3,"Trigger cancels the active item");
                        Click("Queue position 3",false);Check(producer.queue.Count==2&&producer.queue.Last().id==orders[3].id,"Clicking the last occupied card cancels that item");
                        minerals=session.View.minerals;ray=RayTo("Queue position 5");panel.Point(ray,false,false);Click("Queue position 5",true);
                        Check(producer.queue.Count==2&&session.View.minerals==minerals&&!IsHighlighted("Queue position 5"),"Empty slots have no accent hover or cancellation");
                        // Same length, different identities: no stale click on the new active item.
                        ray=RayTo("Current production");Frame(ray,true,false,true);
                        producer.queue[0].remaining=1;session.Match.Step();session.Send(CommandKind.Train,type.id);Refresh();
                        var current=producer.queue.Select(q=>q.id).ToArray();Frame(RayTo("Current production"),false,true,true);Refresh();
                        Check(producer.queue.Select(q=>q.id).SequenceEqual(current),"Completion plus enqueue during A hold cannot cancel the replacement");
                        // Even before the next panel rebuild, the command itself rejects a finished ID.
                        ray=RayTo("Current production");Frame(ray,true,false,false);producer.queue[0].remaining=1;session.Match.Step();
                        typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                        current=producer.queue.Select(q=>q.id).ToArray();minerals=session.View.minerals;Frame(ray,false,true,false);Refresh();
                        Check(producer.queue.Select(q=>q.id).SequenceEqual(current)&&session.View.minerals==minerals,"Finished target never cancels another item before HUD refresh");
                        ray=RayTo("Current production");Frame(ray,true,false,true);Frame(miss,false,true,true);Refresh();
                        Check(producer.queue.Count==1,"Releasing outside the queue cancels the click");
                        // Ready but blocked units remain cancellable.
                        producer.queue[0].remaining=0;Refresh();panel.Point(RayTo("Current production"),false,false);Capture(faction+"-"+role+"-active-hover");
                        Click("Current production",true);Check(producer.queue.Count==0,"Ready-to-deploy item cancels directly");
                        minerals=session.View.minerals;Click("Current production",false);
                        Check(producer.queue.Count==0&&session.View.minerals==minerals&&!IsHighlighted("Current production"),"Idle production card is inert");
                    }
                    session.Select(tech.id,false);Check(session.Send(CommandKind.Train,session.Catalog.Role(faction,"siege").id),"Artillery trains independently");
                    session.Select(lab.id,false);
                    Check(session.Send(CommandKind.Research,"attack")&&session.Send(CommandKind.Research,"armor"),"Both upgrades queue at the research building");Refresh();
                    var armor=session.Catalog.Upgrade("armor");int gas=session.View.catalyst,money=session.View.minerals;
                    panel.Point(RayTo("Queue position 2"),false,false);Capture(faction+"-research-hover");Click("Queue position 2",true);
                    Check(lab.queue.Count==1&&lab.queue.All(q=>q.definition!="armor")&&session.View.catalyst==gas+armor.catalyst&&session.View.minerals==money+armor.minerals,"Click research refunds minerals and gas");
                    Click("Current production",false);Check(lab.queue.Count==0&&tech.queue.Count==1&&session.Match.Players[0].attackLevel==0,"Cancelling active research preserves independent artillery production");
                    session.Select(tech.id,false);Refresh();
                    panel.Point(RayTo("Current production"),false,false);panel.Back();Refresh();
                    Check(Hints().All(t=>!t.gameObject.activeSelf),"Pause clears queue hover");panel.Back();Refresh();
                    var heldRay=RayTo("Current production");Frame(heldRay,true,false,true);session.Select(hq.id,false);Refresh();Frame(RayTo("Current production"),false,true,true);
                    Check(tech.queue.Count==1&&hq.queue.Count==0,"Selection change cannot redirect a held cancellation");
                    session.Select(tech.id,false);Refresh();panel.Point(RayTo("Current production"),false,false);panel.CancelPointerInteraction();
                    Check(Hints().All(t=>!t.gameObject.activeSelf)&&!IsHighlighted("Current production"),"Pointer cancellation restores card color");
                    session.Select(hq.id,false);var workerType=session.Catalog.Role(faction,"worker");
                    session.Send(CommandKind.Train,workerType.id);session.Send(CommandKind.Train,workerType.id);session.Select(tech.id,true);Refresh();
                    var groupIds=session.Selection.OrderBy(id=>id).ToArray();int techCount=tech.queue.Count;
                    money=session.View.minerals;int nextHq=hq.queue[1].id;
                    Click("Current production",false);
                    Check(hq.queue.Count==1&&hq.queue[0].id==nextHq&&tech.queue.Count==techCount&&session.View.minerals==money+workerType.minerals
                        &&session.Selection.OrderBy(id=>id).SequenceEqual(groupIds),"Current group production cancels and refunds only its exact producer without changing selection");
                    Click("Queue position 2",true);
                    Check(tech.queue.Count==0&&hq.queue.Count==1,"A second building's current production can also be cancelled directly from its group card");
                    session.LeaveMatch();Refresh();Check(Hints().All(t=>!t.gameObject.activeInHierarchy),"Leaving match clears cancellation UI");

                    EntityState Add(string role,int id)
                    {
                        var type=session.Catalog.Role(faction,role);var e=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(22,8+(id-1000)*5)};
                        session.Match.Entities.Add(id,e);session.Match.Map.Occupy(e.position,type.footprint,id);return e;
                    }
                }
                Debug.Log($"FRONTIER_PRODUCTION_CANCEL passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("QUEUE_CANCEL_PASS: "+message);}
                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");
                }
                RectTransform Card(string name)=>panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                Ray RayTo(string name){var r=Card(name);return new Ray(r.TransformPoint(r.rect.center)-r.forward,r.forward);}
                bool IsHighlighted(string name)=>Card(name).GetComponent<Image>().color==ConsoleTheme.Accent;
                RectTransform[] Hints()=>Field<RectTransform[]>(panel,"cancelHints");
                void Frame(Ray ray,bool down,bool up,bool a)
                {Check((bool)typeof(CommanderInput).GetMethod("RoutePanel",Private).Invoke(input,new object[]{ray,!a&&down,!a&&up,!a&&down,a&&down,a&&up,a&&down,0f}),"Queue input consumed by console");}
                void Click(string name,bool a){Refresh();var ray=RayTo(name);Frame(ray,true,false,a);Frame(ray,false,true,a);Refresh();}
                void Capture(string name){HarvestingPreview.SavePanel(panel,"TestResults/ProductionCancel/"+name+".png",preview);captures++;}
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
            if(runRelatedChecks)ActionHelpPreview.Render();
        }
    }
}
