using System;
using System.IO;
using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class ReplayPlaybackChecks
    {
        [MenuItem("Frontier/Check replay playback")]
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-playback-"+Guid.NewGuid().ToString("N"));
            int checks=0;
            try
            {
                var catalog=new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
                var map=JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/rift").text);
                var store=new MatchArchiveStore(Path.Combine(directory,"original"));
                var match=new Simulation(map,catalog);var writer=store.Begin(match,"hard");writer.Flush();
                int worker=match.Entities.Values.First(e=>e.owner==0&&match.Type(e).role=="worker").id;
                match.Submit(new GameCommand{player=0,sequence=1,kind=CommandKind.Hold,entities=new[]{worker}});writer.Flush();
                match.Submit(new GameCommand{player=0,sequence=2,kind=CommandKind.Train,entities=new[]{worker},definition="missing"});writer.Flush();
                var bots=new[]{new Strategist(0,AiDifficulty.Hard,catalog,map),new Strategist(1,AiDifficulty.Hard,catalog,map)};
                while(match.Tick<Simulation.TickRate*180&&match.Winner==-1)
                {
                    if(match.Tick%Simulation.TickRate==0)for(int p=0;p<2;p++)foreach(var command in bots[p].Decide(match.View(p)))match.Submit(command);
                    match.Step();if(writer.Due)writer.Flush();
                }
                if(match.Winner==-1)match.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();
                var info=store.List().Single();
                var replay=new ReplayPlayback(store,info);
                Check(replay.Error==null&&!replay.Ended&&replay.Tick==0,"initial checkpoints and later same-tick commands retain their exact ordering");
                Check(replay.Match.RecordedCommandCount==0,"replay simulation never starts a new recording");
                replay.Paused=true;replay.Advance(.25f);Check(replay.Tick==0,"pausing playback freezes the world");
                replay.Seek(37);DrainSeek(replay,7);
                Check(replay.Tick==37&&replay.Paused&&!replay.Seeking,"forward seek reaches an exact tick and preserves pause");
                replay.Advance(.25f);Check(replay.Tick==37,"finishing a paused seek does not resume playback");
                replay.Paused=false;
                foreach(float speed in new[]{.5f,1,2,4})
                {
                    replay.Speed=speed;int tick=replay.Tick;replay.Advance(.1f);
                    Check(replay.Tick-tick==(int)(Simulation.TickRate*.1f*speed),"playback respects speed "+speed);
                }
                bool invalid=false;try{replay.Speed=3;}catch(ArgumentOutOfRangeException){invalid=true;}
                Check(invalid,"playback accepts only the documented speed steps");
                replay.Speed=1;int before=replay.Tick;replay.Advance(100,2);int after=replay.Tick;replay.Advance(0,2);
                Check(after-before<=2&&replay.Tick==after,"long frames obey step budget and leave no catch-up burst");
                var originalWorld=replay.Match;replay.Seek(20);DrainSeek(replay,5);
                Check(replay.Match!=originalWorld&&replay.Tick==20&&!replay.Paused,"backward seek reconstructs an independent world and preserves playing intent");
                replay.Seek(replay.EndTick);DrainSeek(replay,19);
                Check(replay.Ended&&replay.Paused&&replay.Error==null&&replay.Tick==match.Tick&&replay.VerifiedTick==match.Tick,"playback verifies every checkpoint and pauses at the recorded end");
                Check(JsonUtility.ToJson(replay.Match.Snapshot())==JsonUtility.ToJson(match.Snapshot()),"playback exactly reproduces the final authoritative world without running an AI");
                Check(JsonUtility.ToJson(MatchReportData.From(replay.Match.Report))==JsonUtility.ToJson(MatchReportData.From(match.Report)),"playback reproduces the complete original statistics and build orders");
                int endTick=replay.Tick;replay.Paused=false;replay.Advance(.25f);Check(replay.Tick==endTick&&replay.Paused,"the end cannot advance past recorded data");
                replay.Restart();DrainSeek(replay,3);Check(replay.Tick==0&&!replay.Ended&&replay.Paused,"restart reconstructs the beginning without forcing playback");
                replay.Paused=false;replay.Advance(.1f);Check(replay.Tick==2,"a restarted replay can play again");
                replay.Seek(replay.EndTick);long timedRevision=replay.StateRevision;before=replay.Tick;
                replay.Advance(0,240,0);
                Check(replay.Tick==before&&replay.StateRevision==timedRevision&&replay.Seeking,"An exhausted elapsed-time budget performs no simulation work");
                int timedFrames=0;
                while(replay.Seeking&&timedFrames++<10000)replay.Advance(0,120,2);
                Check(replay.Ended&&replay.Error==null&&timedFrames>1&&JsonUtility.ToJson(replay.Match.Snapshot())==JsonUtility.ToJson(match.Snapshot()),"Two-millisecond seek batches yield across frames and reproduce the exact final world");

                var bad=Clone("incompatible");var header=Read<MatchArchiveHeader>(bad,"header.json");header.replayRulesVersion++;Write(bad,"header.json",header);
                Check(Rejects(()=>new ReplayPlayback(bad,bad.Inspect(info.Id))),"incompatible simulation rules fail before playback begins");
                bad=Clone("missing");File.Delete(Path.Combine(bad.Root,info.Id,"orders-000001.json"));
                Check(Rejects(()=>new ReplayPlayback(bad,bad.Inspect(info.Id))),"a missing committed command segment cannot be played");
                bad=Clone("truncated");var progress=Read<MatchArchiveProgress>(bad,"progress.json");progress.commands++;Write(bad,"progress.json",progress);
                Check(Rejects(()=>new ReplayPlayback(bad,bad.Inspect(info.Id))),"truncated command counts cannot be mistaken for a complete replay");
                bad=Clone("final-hash");progress=Read<MatchArchiveProgress>(bad,"progress.json");progress.stateHash=new string('0',64);Write(bad,"progress.json",progress);
                Check(Rejects(()=>new ReplayPlayback(bad,bad.Inspect(info.Id))),"the final progress hash must match its committed checkpoint");
                bad=Clone("desync");
                string segmentName=Directory.GetFiles(Path.Combine(bad.Root,info.Id),"orders-*.json").OrderBy(x=>x)
                    .First(path=>JsonUtility.FromJson<MatchArchiveSegment>(File.ReadAllText(path)).tick>0);
                var changed=JsonUtility.FromJson<MatchArchiveSegment>(File.ReadAllText(segmentName));changed.stateHash=new string('0',64);File.WriteAllText(segmentName,JsonUtility.ToJson(changed));
                var diverged=new ReplayPlayback(bad,bad.Inspect(info.Id));diverged.Seek(diverged.EndTick);DrainSeek(diverged,17,false);
                Check(diverged.Error!=null&&diverged.Paused&&!diverged.Ended&&diverged.Tick==changed.tick,"checkpoint divergence stops playback at the first mismatched state");
                before=diverged.Tick;diverged.Seek(0);diverged.Paused=false;diverged.Advance(.25f);
                Check(diverged.Tick==before&&diverged.Paused,"a failed replay cannot continue or silently rewind around corruption");

                bad=Clone("command-result");segmentName=Directory.GetFiles(Path.Combine(bad.Root,info.Id),"orders-*.json").OrderBy(x=>x)
                    .First(path=>JsonUtility.FromJson<MatchArchiveSegment>(File.ReadAllText(path)).commands.Any(c=>c.tick>0));
                changed=JsonUtility.FromJson<MatchArchiveSegment>(File.ReadAllText(segmentName));var altered=changed.commands.First(c=>c.tick>0);altered.accepted=!altered.accepted;File.WriteAllText(segmentName,JsonUtility.ToJson(changed));
                diverged=new ReplayPlayback(bad,bad.Inspect(info.Id));diverged.Seek(diverged.EndTick);DrainSeek(diverged,31,false);
                Check(diverged.Error!=null&&diverged.Tick==altered.tick&&diverged.Paused,"a command-result mismatch fails before later checkpoints");

                var shortStore=new MatchArchiveStore(Path.Combine(directory,"zero"));var instant=new Simulation(map,catalog);var instantWriter=shortStore.Begin(instant,"easy");instantWriter.Flush();
                instant.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});instantWriter.Finish();
                var zero=new ReplayPlayback(shortStore,shortStore.List().Single());
                Check(zero.Ended&&zero.Paused&&zero.Tick==0&&zero.Error==null,"zero-duration matches finish without a seek loop or invalid timing");
                shortStore=new MatchArchiveStore(Path.Combine(directory,"interrupted"));var unfinished=new Simulation(map,catalog);var unfinishedWriter=shortStore.Begin(unfinished,"easy");unfinishedWriter.Flush();
                for(int i=0;i<80;i++)unfinished.Step();unfinishedWriter.Flush();
                var interrupted=new ReplayPlayback(shortStore,shortStore.List().Single());interrupted.Seek(int.MaxValue);DrainSeek(interrupted,5);
                Check(interrupted.Ended&&interrupted.Tick==80&&interrupted.Match.Winner==-1&&interrupted.Error==null,"interrupted recordings stop at the last committed tick without inventing a winner");

                shortStore=new MatchArchiveStore(Path.Combine(directory,"command-budget"));var burst=new Simulation(map,catalog);var burstWriter=shortStore.Begin(burst,"easy");burstWriter.Flush();
                for(int i=1;i<=2500;i++)burst.Submit(new GameCommand{player=0,sequence=i,kind=CommandKind.Hold,entities=new[]{999999}});
                burst.Submit(new GameCommand{player=0,kind=CommandKind.Surrender});burstWriter.Finish();
                var bounded=new ReplayPlayback(shortStore,shortStore.List().Single());
                Check(bounded.Seeking&&!bounded.Ended&&bounded.Tick==0,"a large same-tick command burst is not processed in one unbounded load");
                long exhaustedRevision=bounded.StateRevision;bounded.Advance(0,240,double.Epsilon);
                Check(bounded.StateRevision==exhaustedRevision&&bounded.Seeking,"An elapsed deadline also yields before processing a same-tick command burst");
                long revision=bounded.StateRevision;bounded.Advance(0,1);
                Check(bounded.StateRevision>revision&&bounded.Tick==0&&bounded.Seeking,"same-tick command batches publish state changes without advancing the clock");
                DrainSeek(bounded,1);Check(bounded.Ended&&bounded.Error==null,"bounded command batches eventually reproduce a same-tick final result");
                Debug.Log($"FRONTIER_REPLAY_PLAYBACK passed: {checks} checks; bounded playback, seeking, disk integrity and deterministic reconstruction; no player build");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("REPLAY_PASS: "+message);}
                void DrainSeek(ReplayPlayback player,int budget,bool expectSuccess=true)
                {
                    int loops=0;
                    while(player.Seeking&&player.Error==null&&loops++<10000)
                    {
                        int tick=player.Tick;player.Advance(0,budget);
                        if(player.Tick-tick>budget)throw new Exception("Seek exceeded its per-frame step budget");
                    }
                    if(loops>=10000||expectSuccess&&player.Error!=null)throw new Exception(player.Error??"Seek did not finish");
                }
                MatchArchiveStore Clone(string name)
                {
                    var target=new MatchArchiveStore(Path.Combine(directory,name));string folder=Path.Combine(target.Root,info.Id);Directory.CreateDirectory(folder);
                    foreach(string source in Directory.GetFiles(Path.Combine(store.Root,info.Id)))File.Copy(source,Path.Combine(folder,Path.GetFileName(source)));
                    return target;
                }
                T Read<T>(MatchArchiveStore source,string name)=>JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(source.Root,info.Id,name)));
                void Write(MatchArchiveStore target,string name,object value)=>File.WriteAllText(Path.Combine(target.Root,info.Id,name),JsonUtility.ToJson(value));
                bool Rejects(Action action)
                {try{action();return false;}catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){return true;}}
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
