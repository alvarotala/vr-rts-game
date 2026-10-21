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
    public static class MatchHistoryPreview
    {
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        [MenuItem("Frontier/Preview and check saved match history")]
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-history-"+Guid.NewGuid().ToString("N"));
            var fixture=new GameObject("History fixture");var console=new GameObject("History console");var preview=new PreviewRenderUtility();
            Directory.CreateDirectory("TestResults/History");int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var store=new MatchArchiveStore(directory);Set(session,"history",store);Set(session,"archiveInEditor",true);
                var boardObject=new GameObject("History board");boardObject.transform.SetParent(fixture.transform);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");board.Placed=true;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                Invoke(panel,"Start");preview.AddSingleGO(console);Refresh();Capture("01-main");Click("Match history");Capture("02-empty");Click("Main menu");
                session.Difficulty="hard";session.StartMatch();var sim=session.Match;
                Check(store.List().Length==1&&store.List()[0].Progress.tick==0,"Starting a game writes a unique initial record");
                var bots=new[]{new Strategist(0,AiDifficulty.Normal,session.Catalog,session.Map),new Strategist(1,AiDifficulty.Hard,session.Catalog,session.Map)};
                while(sim.Tick<Simulation.TickRate*360&&sim.Winner==-1)
                {
                    if(sim.Tick%Simulation.TickRate==0)for(int p=0;p<2;p++)foreach(var command in bots[p].Decide(sim.View(p)))sim.Submit(command);
                    sim.Step();Invoke(session,"UpdateArchive",false);
                }
                if(sim.Winner==-1)sim.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});
                Invoke(session,"RefreshView");Refresh();
                var info=store.List().Single();string id=info.Id;
                Check(info.HasReport&&!session.ArchiveFailed&&session.ArchiveStatus.Contains("Saved"),"Victory automatically saves the report and reports success");
                Check(info.Progress.commands==sim.RecordedCommandCount&&info.Progress.segments>20,"Periodic durable segments retain the complete command history");
                var restored=new MatchArchiveStore(directory);var report=restored.LoadReport(id);
                Check(JsonUtility.ToJson(MatchReportData.From(report.Report))==JsonUtility.ToJson(MatchReportData.From(sim.Report)),"A new store instance restores every report field from disk");
                Check(restored.ReadRules(info).catalog.archetypes.Length==session.Catalog.Definition.archetypes.Length,"Recording embeds the full catalog and map independently of current assets");
                var replay=restored.CreateReplay(info);int orders=0;bool results=true,states=true;
                foreach(var segment in restored.ReadSegments(info))
                {
                    foreach(var entry in segment.commands)
                    {
                        while(replay.Tick<entry.tick&&replay.Winner==-1)replay.Step();
                        if(replay.Tick!=entry.tick)throw new Exception("Replay finished too early");
                        var result=replay.Submit(entry.command);results&=result.accepted==entry.accepted&&result.message==entry.rejection;orders++;
                    }
                    while(replay.Tick<segment.tick&&replay.Winner==-1)replay.Step();
                    states&=replay.Tick==segment.tick&&MatchArchiveStore.Hash(JsonUtility.ToJson(replay.Snapshot()))==segment.stateHash;
                }
                Check(results&&orders==info.Progress.commands,"Disk recording reproduces every command result without AI execution");
                Check(states&&MatchArchiveStore.Hash(JsonUtility.ToJson(replay.Snapshot()))==info.Progress.stateHash,"Every persisted replay checkpoint and the final world reproduce exactly");
                Check(JsonUtility.ToJson(MatchReportData.From(replay.Report))==JsonUtility.ToJson(MatchReportData.From(sim.Report)),"Disk replay reproduces the original statistics and build order");
                info.Header.replayRulesVersion++;bool rejected=false;try{restored.CreateReplay(info);}catch(NotSupportedException){rejected=true;}
                Check(rejected,"Incompatible rules versions are refused instead of silently playing a divergent match");
                info=restored.Inspect(id);
                // Leave a finished game twice: the same ID is preserved and no duplicate record appears.
                session.LeaveMatch();session.LeaveMatch();Check(store.List().Length==1,"Repeated leave does not duplicate a completed match");
                // Create more results to exercise history pagination, abandonment and same-second IDs.
                for(int i=0;i<5;i++){session.StartMatch();session.Send(CommandKind.Surrender);session.LeaveMatch();}
                session.StartMatch();session.Match.Step();session.Pause("menu",true);session.LeaveMatch();
                Check(store.List().Length==7&&store.List().Select(m=>m.Id).Distinct().Count()==7,"Rapid restarts create unique persistent match IDs");
                Check(store.List().Count(m=>m.Progress.state==ArchivedMatchState.Abandoned)==1,"Leaving a paused match saves an explicitly abandoned result");
                var interrupted=new Simulation(session.Map,session.Catalog);var writer=store.Begin(interrupted,"easy");writer.Flush();
                for(int t=0;t<Simulation.TickRate*16;t++)interrupted.Step();writer.Flush();
                Check(new MatchArchiveStore(directory).List().Count(m=>m.Progress.state==ArchivedMatchState.Interrupted)==1,"An unfinished recording survives restart and is labelled interrupted");
                string damaged=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Path.Combine(directory,damaged));File.WriteAllText(Path.Combine(directory,damaged,"header.json"),"broken");
                Check(store.List().Length==9&&store.List().Count(m=>m.Error!=null)==1,"One damaged record leaves other history entries accessible");
                Set(session,"history",new MatchArchiveStore(directory));Refresh();Click("Match history");Capture("03-history");Click("Next");Capture("04-history-page-2");
                Click("Previous");
                // Load the original detailed report via the same menu callback and frozen catalog.
                Invoke(panel,"OpenSavedReport",store.Inspect(id));Refresh();Capture("05-saved-overview");
                Check(panel.Page==CommandPanel.MenuPage.HistoryReport&&!session.Running&&session.Match==null&&!panel.AcceptsGameInput&&!board.Content.gameObject.activeSelf,"Browsing history never starts a game or reveals a live battlefield");
                Click("Graphs");Click("APM");Capture("06-saved-graph");Click("Build order");Click("AI · Symbiosis");Capture("07-saved-build-order");
                Click("Back to history");panel.Back();Refresh();Check(panel.Page==CommandPanel.MenuPage.Main,"History supports back navigation without changing setup");
                // An I/O failure is visible and recoverable; no result is marked saved prematurely.
                string blocked=Path.Combine(directory,"blocked");File.WriteAllText(blocked,"fixture");Set(session,"history",new MatchArchiveStore(blocked));
                session.StartMatch();Check(session.ArchiveFailed,"Storage failure is exposed without preventing a match");
                session.Send(CommandKind.Surrender);Refresh();Check(Has("Retry save"),"A failed result offers retry instead of claiming it was saved");
                File.Delete(blocked);Click("Retry save");Check(!session.ArchiveFailed&&new MatchArchiveStore(blocked).List().Single().HasReport,"Retry preserves the original recording and saves it after storage recovers");
                session.LeaveMatch();
                Debug.Log($"FRONTIER_MATCH_HISTORY passed: {checks} checks, {captures} panels rendered; disk recording replayed and verified; no player build");

                void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Debug.Log("HISTORY_PASS: "+message);}
                void Refresh(){Set(panel,"nextText",0f);Invoke(panel,"LateUpdate");}
                bool Has(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(t=>t.name==name&&t.gameObject.activeInHierarchy);
                void Click(string name)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                    var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Menu ray selects "+name);Refresh();
                }
                void Capture(string name)
                {
                    Refresh();Canvas.ForceUpdateCanvases();
                    foreach(var text in panel.GetComponentsInChildren<Text>())Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Text fits: "+text.text);
                    var actions=Field<RectTransform>(panel,"actions");var footer=Field<Text>(panel,"status").rectTransform;
                    Check(-actions.anchoredPosition.y+actions.sizeDelta.y<=-footer.anchoredPosition.y,"Actions do not overlap footer");
                    HarvestingPreview.SavePanel(panel,"TestResults/History/"+name+".png",preview);captures++;
                }
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
