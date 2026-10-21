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
    public static class SpectatorPanelChecks
    {
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static void Invoke(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
        private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Private).SetValue(value,data);
        [MenuItem("Frontier/Preview and check replay spectator panel")]
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-spectator-ui-"+Guid.NewGuid().ToString("N"));
            var fixture=new GameObject("Spectator fixture");var console=new GameObject("Spectator console");var preview=new PreviewRenderUtility();
            Directory.CreateDirectory("TestResults/Spectator");int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var store=new MatchArchiveStore(directory);Set(session,"history",store);
                var recorded=new Simulation(session.Map,session.Catalog);var writer=store.Begin(recorded,"easy");writer.Flush();
                for(int p=0;p<2;p++)
                {
                    var hq=recorded.Entities.Values.First(e=>e.owner==p&&recorded.Type(e).role=="hq");
                    string worker=recorded.Catalog.Role(recorded.Players[p].faction,"worker").id;
                    for(int q=0;q<3;q++)Check(recorded.Submit(new GameCommand{player=p,kind=CommandKind.Train,entities=new[]{hq.id},definition=worker}).accepted,"Fixture queues workers for player "+p);
                }
                while(recorded.Tick<Simulation.TickRate*64){recorded.Step();if(writer.Due)writer.Flush();}
                recorded.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();var info=store.List().Single();
                var boardObject=new GameObject("Spectator board");boardObject.transform.SetParent(fixture.transform);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");board.Placed=true;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");preview.AddSingleGO(console);
                Refresh();Click("Match history");Capture("01-history");Click("Watch replay");
                Check(panel.Page==CommandPanel.MenuPage.Replay&&panel.AcceptsReplayInput&&!panel.AcceptsGameInput,"Watch replay opens an interactive spectator panel and blocks game orders");
                Check(!session.Replay.Paused&&!session.IsPaused,"Visible spectator panel does not apply a gameplay menu pause");
                Check(Field<Text[]>(panel,"replayWorkers").All(t=>t.text=="Workers  6"),"Both player worker counts are visible at the same recorded tick");
                Check(Field<Text[]>(panel,"replayProductionTitle").All(t=>t.text=="PRODUCTION · 3"),"Both player queues show all active and waiting orders");
                Check(Field<Text[,]>(panel,"replayProductionCounts")[0,0].text=="1 + 2","Simultaneous producer view distinguishes active production from queued orders");
                Capture("02-spectator-playing");Click("Replay play / pause");
                Check(session.Replay.Paused&&panel.AcceptsReplayInput,"Pause leaves the transport and spectator controls available");
                Click("Replay speed 2");Check(session.Replay.Speed==2,"Ray control sets playback speed while paused");
                Click("Replay forward 15s");FinishSeek();Check(session.Replay.Tick==Simulation.TickRate*15&&session.Replay.Paused,"Forward transport seeks exactly fifteen seconds without unpausing");
                Click("Replay back 15s");FinishSeek();Check(session.Replay.Tick==0,"Backward transport seeks exactly fifteen seconds");
                Click("Replay speed 0.5");Check(session.Replay.Speed==.5f,"Half speed is selectable with the ray");
                Click("Replay speed 4");Check(session.Replay.Speed==4,"Four-times speed is selectable with the ray");
                Scrub(.5f);FinishSeek();Check(session.Replay.Tick==Simulation.TickRate*32&&session.Replay.Paused,"Dragging the timeline seeks precisely and preserves pause");
                Check(Field<Text[]>(panel,"replayWorkers")[0].text!="Workers  6","Spectator counters refresh after seeking");
                Click("Replay play / pause");Scrub(.25f);FinishSeek();Check(session.Replay.Tick==Simulation.TickRate*16&&!session.Replay.Paused,"Timeline temporarily freezes playback then resumes the previous playing state");
                Click("Replay play / pause");Scrub(1);FinishSeek();Refresh();
                Check(session.Replay.Ended&&panel.Page==CommandPanel.MenuPage.Replay&&Field<Text>(panel,"replayState").text=="REPLAY FINISHED","The match winner stays in the spectator view with final counters and transport visible");
                Capture("03-finished");Scrub(.5f);FinishSeek();Check(session.Replay.Tick==Simulation.TickRate*32&&session.Replay.Paused,"Seeking from the end stays paused instead of unexpectedly restarting playback");
                Click("Replay restart");FinishSeek();Check(session.Replay.Tick==0&&session.Replay.Paused,"Restart returns to the beginning and preserves pause");
                // Exercise a dense future recording without mutating an archive: these local
                // fixture-only entities expose every current construction/train/research type.
                int id=2000;
                for(int p=0;p<2;p++)
                {
                    string faction=session.Match.Players[p].faction;
                    foreach(var type in session.Catalog.Definition.archetypes.Where(t=>t.faction==faction))
                    {
                        var e=new EntityState{id=id++,owner=p,definition=type.building?type.id:session.Catalog.Role(faction,"hq").id,position=new Point(22+p*25,22),health=type.health,complete=!type.building,remaining=type.duration/2};
                        if(!type.building)for(int q=0;q<5;q++)e.queue.Add(new ProductionOrder{id=id++,definition=type.id,total=100,remaining=50});
                        session.Match.Entities.Add(e.id,e);
                    }
                    foreach(var upgrade in session.Catalog.Definition.upgrades)
                    {
                        var e=new EntityState{id=id++,owner=p,definition=session.Catalog.Role(faction,"tech").id,position=new Point(23+p*25,23),health=300};
                        e.queue.Add(new ProductionOrder{id=id++,definition=upgrade.id,research=true,total=100,remaining=25});session.Match.Entities.Add(e.id,e);
                    }
                    session.Match.Players[p].minerals=12345+p;session.Match.Players[p].catalyst=6789+p;
                }
                Refresh();Capture("04-production-full");
                var navigation=Field<RectTransform[]>(panel,"replayProductionNavigation");
                Check(navigation.All(n=>n.gameObject.activeInHierarchy),"Production overflow exposes independent paging controls for each player");
                Check(Field<Text[]>(panel,"replayMinerals")[0].text=="12,345"&&Field<Text[]>(panel,"replayGas")[1].text=="6,790","Both resource banks remain legible with large values");
                Check(Field<Text[,]>(panel,"replayProductionNames").Cast<Text>().Any(t=>t.text.StartsWith("Research · "))&&Field<Text[,]>(panel,"replayProductionProgress").Cast<Text>().Any(t=>t.text=="75%"),"Research and its progress share the always-visible production view");
                ClickRect(navigation[0].GetComponentsInChildren<RectTransform>().First(r=>r.name=="Next"));Refresh();Capture("05-production-page-2");
                Check(Field<int[]>(panel,"replayProductionPage")[0]==1&&Field<int[]>(panel,"replayProductionPage")[1]==0,"Production pagination changes only the requested player's column");
                Check(Field<Text[,]>(panel,"replayProductionNames")[0,0].text.StartsWith("Train · "),"Production overflow remains reachable without hiding transport controls");
                typeof(ReplayPlayback).GetProperty("Error").SetValue(session.Replay,"Replay stopped: A checkpoint diverged at 00:32. This recording may need its original game version.");
                Refresh();Check(Field<Text>(panel,"status").text==session.Replay.Error&&Field<Text>(panel,"replayState").text=="REPLAY STOPPED","Replay verification errors stay visible above the exit controls");Capture("06-error");
                Click("Exit replay");Check(panel.Page==CommandPanel.MenuPage.History&&!session.IsReplay&&!session.Running,"Exit replay returns directly to saved match history");
                Invoke(panel,"OpenSavedReport",info);Refresh();Click("Watch replay");Check(panel.Page==CommandPanel.MenuPage.Replay,"A saved report also launches its own replay");
                panel.Back();Refresh();Check(panel.Page==CommandPanel.MenuPage.History,"Controller Back exits replay to history");
                Debug.Log($"FRONTIER_SPECTATOR_PANEL passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("SPECTATOR_PANEL_PASS: "+message);}
                void Refresh(){Set(panel,"nextText",0f);Invoke(panel,"LateUpdate");}
                void FinishSeek(){for(int i=0;i<100&&session.Replay.Seeking;i++)Invoke(session,"AdvanceReplay",0f);Refresh();Check(!session.Replay.Seeking&&session.Replay.Error==null,"Timeline reaches a verified recorded tick");}
                void Click(string name){Refresh();ClickRect(panel.GetComponentsInChildren<RectTransform>().First(r=>r.name==name&&r.gameObject.activeInHierarchy));Refresh();}
                void ClickRect(RectTransform rect){string name=rect.name;var point=rect.TransformPoint(rect.rect.center);var ray=new Ray(point-rect.forward,rect.forward);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Ray selects "+name);}
                void Scrub(float fraction)
                {
                    Refresh();var timeline=Field<RectTransform>(panel,"replayTimeline");
                    var point=timeline.TransformPoint(new Vector3(timeline.rect.width*Mathf.Clamp(fraction,.001f,.999f),-50,0));
                    // The right edge is exclusive in Rect.Contains: use the last half pixel,
                    // which still rounds to the exact final tick of this recording.
                    if(fraction==1)point=timeline.TransformPoint(new Vector3(timeline.rect.width-.25f,-50,0));
                    var ray=new Ray(point-timeline.forward,timeline.forward);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Ray drags timeline to "+fraction);
                }
                void Capture(string name)
                {
                    Refresh();Canvas.ForceUpdateCanvases();
                    foreach(var text in panel.GetComponentsInChildren<Text>())Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Text fits: "+text.text);
                    var area=Field<RectTransform>(panel,"actions");var footer=Field<Text>(panel,"status").rectTransform;
                    Check(-area.anchoredPosition.y+area.sizeDelta.y<=-footer.anchoredPosition.y,"Actions and transport do not overlap footer");
                    Check(-Field<Text>(panel,"harvesting").rectTransform.anchoredPosition.y+Field<Text>(panel,"harvesting").rectTransform.sizeDelta.y<=-area.anchoredPosition.y,"Header does not overlap spectator cards");
                    HarvestingPreview.SavePanel(panel,"TestResults/Spectator/"+name+".png",preview);captures++;
                }
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
