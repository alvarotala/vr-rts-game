using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class ReplaySessionChecks
    {
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static void Invoke(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
        private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Private).SetValue(value,data);
        public static void Run()
        {
            int checks=0;string root=Path.Combine(Path.GetTempPath(),"frontier-replay-session-"+Guid.NewGuid().ToString("N"));
            var fixture=new GameObject("Replay session fixture");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var store=new MatchArchiveStore(root);Set(session,"history",store);Set(session,"archiveInEditor",true);
                var originalCatalog=session.Catalog;var originalMap=session.Map;
                session.Faction="biological";session.EnemyFaction="industrial";session.Difficulty="easy";
                var recorded=new Simulation(originalMap,originalCatalog,"industrial","biological");var writer=store.Begin(recorded,"hard");writer.Flush();
                int hq=recorded.Entities.Values.First(e=>e.owner==0&&recorded.Type(e).role=="hq").id;
                recorded.Submit(new GameCommand{player=0,kind=CommandKind.Train,definition="industrial.worker",entities=new[]{hq}});
                for(int t=0;t<Simulation.TickRate*32;t++){recorded.Step();if(writer.Due)writer.Flush();}
                recorded.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();
                string id=store.List().Single().Id;
                string[] files=Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(p=>p).ToArray();
                string[] hashes=files.Select(p=>MatchArchiveStore.Hash(File.ReadAllText(p))).ToArray();
                var boardObject=new GameObject("Replay session board");boardObject.transform.SetParent(fixture.transform);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var cameraObject=new GameObject("Replay session camera");cameraObject.transform.SetParent(fixture.transform);var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,1.7f,-2);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;typeof(QuestBridge).GetProperty("Camera").SetValue(quest,camera);
                quest.RequestReplay(id);Check(quest.Placing&&quest.StartPending&&!session.IsReplay&&!session.Running,"Replay waits for a missing board instead of starting a match");
                quest.CancelPlacement();Check(!quest.StartPending&&!session.IsReplay,"Cancelling replay placement cancels its pending start");
                quest.RequestReplay(id);quest.PlaceRay(new Ray(new Vector3(0,2,0),Vector3.down),0,Vector2.zero,true);
                Check(session.IsReplay&&session.Running&&session.Replay.Info.Id==id&&!quest.StartPending,"Confirming board placement starts the requested replay");
                board.transform.SetParent(fixture.transform,true);
                Check(session.Match!=recorded&&session.Catalog!=originalCatalog&&session.Difficulty=="hard"&&session.Faction=="industrial","Replay uses its archived map, rules and factions");
                Check(session.View.visibility.All(v=>v==2)&&session.View.entities.Any(e=>e.owner==1&&session.Catalog[e.definition].role=="hq"),"Spectator board reveals the complete map and both sides");
                Check(session.ReplayPlayerViews[0].visibility.Any(v=>v==0)&&!session.ReplayPlayerViews[0].entities.Any(e=>e.owner==1),"Normal player perspectives keep their fog and hidden opponent information");
                Check(session.View.entities.Where(e=>session.Catalog[e.definition].building).All(e=>e.queue!=null),"Observer projection carries both players' production queues");
                Invoke(board,"LateUpdate");
                Check(session.View.entities.All(e=>board.TryEntityTop(e.id,out _)),"The spectator board renders both armies and their structures from the observer view");
                int resets=0;session.ReplayReset+=()=>resets++;
                Invoke(session,"AdvanceReplay",.25f);Check(session.Replay.Tick>0&&session.View.tick==session.Replay.Tick,"Replay session advances and refreshes the rendered view");
                var tick=session.Match.Tick;var world=JsonUtility.ToJson(session.Match.Snapshot());
                session.Selection.Add(hq);
                Check(!session.Send(CommandKind.Surrender)&&!session.Send(CommandKind.Train,"industrial.worker"),"Spectator session rejects gameplay orders including surrender and production");
                session.ChooseBuild("industrial.supply");session.ChooseAttackMove();
                Check(!session.HasTargetingMode&&JsonUtility.ToJson(session.Match.Snapshot())==world,"Gameplay targeting and rejected sends cannot mutate the replay world");
                session.ToggleReplayPause();Invoke(session,"AdvanceReplay",.25f);
                Check(session.IsPaused&&session.Match.Tick==tick&&!session.Match.Paused,"Transport pause freezes playback without changing the simulation's command acceptance");
                board.Zoom=2;board.Pan=new Vector2(4,-3);var terrain=board.Content.Find("Terrain");
                session.SeekReplay(Simulation.TickRate*25);
                for(int i=0;i<100&&session.Replay.Seeking;i++)Invoke(session,"AdvanceReplay",.25f);
                Check(session.Replay.Tick==Simulation.TickRate*25&&session.Replay.Paused&&session.View.tick==session.Replay.Tick,"Seek reaches the exact target and preserves the user's paused state");
                Invoke(board,"LateUpdate");
                session.SeekReplay(Simulation.TickRate*5);for(int i=0;i<100&&session.Replay.Seeking;i++)Invoke(session,"AdvanceReplay",.25f);
                Check(session.Match==session.Replay.Match&&session.View.tick==Simulation.TickRate*5&&resets>=2,"Backward seek replaces the world, refreshes views and resets transient visuals");
                Check(board.Zoom==2&&board.Pan==new Vector2(4,-3)&&board.Content.Find("Terrain")==terrain,"Seeking preserves spectator framing and reuses the terrain mesh");
                Invoke(board,"LateUpdate");
                Check(session.View.entities.All(e=>board.TryEntityTop(e.id,out _)),"Backward seeking reuses pooled visuals and renders every current entity without stale references");
                session.ToggleReplayPause();session.Pause("headset",true);tick=session.Match.Tick;
                Invoke(session,"AdvanceReplay",.25f);Check(session.Match.Tick==tick&&!session.Replay.Paused,"Headset pause stops playback and preserves the requested transport state");
                session.Pause("headset",false);Invoke(session,"AdvanceReplay",.25f);Check(session.Match.Tick>tick,"Playback resumes when headset pause clears");
                session.SeekReplay(session.Replay.EndTick);for(int i=0;i<100&&session.Replay.Seeking;i++)Invoke(session,"AdvanceReplay",.25f);
                Check(session.Replay.Ended&&session.IsReplay&&session.Running&&session.Match.Winner==0,"End of replay retains spectator mode and its final world");
                Check(session.Replay.Error==null&&JsonUtility.ToJson(session.Match.Snapshot())==JsonUtility.ToJson(recorded.Snapshot()),"Session playback reaches the verified recorded world without divergence");
                session.Pause("headset",true);session.StopReplay();
                Check(!session.Running&&!session.IsReplay&&session.Match==null&&session.View==null&&session.LastClosedWasReplay&&session.IsPaused,"Exit closes only replay state while preserving external pause reasons");
                Check(ReferenceEquals(session.Catalog,originalCatalog)&&ReferenceEquals(session.Map,originalMap)&&session.Faction=="biological"&&session.EnemyFaction=="industrial"&&session.Difficulty=="easy","Exit restores new-match preferences and current game catalog");
                Check(files.SequenceEqual(Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(p=>p))&&hashes.SequenceEqual(files.Select(p=>MatchArchiveStore.Hash(File.ReadAllText(p)))),"Playback, seeking and exit never alter or append to the archive");
                Check(!session.StartReplay(Guid.NewGuid().ToString("N"))&&!session.Running&&ReferenceEquals(session.Catalog,originalCatalog),"Missing recording fails without changing setup or starting a session");
                session.Pause("headset",false);session.StartMatch();Check(!session.IsReplay&&!session.LastClosedWasReplay&&session.Match.Players[0].faction=="biological","Next live match uses the restored setup and normal lifecycle");
                session.LeaveMatch();Debug.Log($"FRONTIER_REPLAY_SESSION passed: {checks} checks; no player build");
                void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("REPLAY_SESSION_PASS: "+message);}
            }
            finally{Object.DestroyImmediate(fixture);if(Directory.Exists(root))Directory.Delete(root,true);}
        }
    }
}
