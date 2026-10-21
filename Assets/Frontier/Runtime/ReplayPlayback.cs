using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // A separate, read-only playback world. Strategist and live-session input never run here.
    public sealed class ReplayPlayback
    {
        private readonly MatchArchiveStore store;
        private readonly MatchArchiveSegment[] segments;
        private int segmentIndex,commandIndex;
        private double tickCredit;
        private bool paused;
        private float speed=1;
        public Simulation Match {get;private set;}
        public SavedMatchInfo Info {get;}
        public int Tick=>Match.Tick;
        public int EndTick {get;}
        public bool Seeking {get;private set;}
        public bool Ended {get;private set;}
        public int SeekTarget {get;private set;}
        public int VerifiedTick {get;private set;}
        // Commands can change the world while Tick stays fixed, including across bounded batches.
        public long StateRevision {get;private set;}
        public string Error {get;private set;}
        public bool Paused
        {
            get=>paused;
            set {paused=value||Error!=null||Ended;tickCredit=0;}
        }
        public float Speed
        {
            get=>speed;
            set
            {
                if(value!=.5f&&value!=1&&value!=2&&value!=4)throw new ArgumentOutOfRangeException(nameof(value),"Replay speed must be 0.5, 1, 2 or 4.");
                speed=value;tickCredit=0;
            }
        }
        public ReplayPlayback(MatchArchiveStore store,SavedMatchInfo info)
        {
            this.store=store??throw new ArgumentNullException(nameof(store));
            if(info==null)throw new ArgumentNullException(nameof(info));
            // Re-read the committed marker so stale history rows cannot hide missing/truncated data.
            Info=store.Inspect(info.Id);
            if(Info.Error!=null)throw new ArgumentException(Info.Error);
            EndTick=Info.Progress.tick;
            Match=store.CreateReplay(Info);
            segments=store.ReadSegments(Info).ToArray();
            if(segments.Length==0||segments.Any(s=>!ValidHash(s.stateHash))||!ValidHash(Info.Progress.stateHash)
                ||segments[segments.Length-1].stateHash!=Info.Progress.stateHash)
                throw new ArgumentException("Replay checkpoints are missing or inconsistent.");
            if(Info.HasReport&&(Info.Progress.winner<0||Info.Progress.winner>2))throw new ArgumentException("Invalid replay result.");
            Seeking=true;SeekTarget=0;
            Advance(0);
            if(Error!=null)throw new ArgumentException(Error);
        }
        private static bool ValidHash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        public void Seek(int tick)
        {
            if(Error!=null)return;
            int target=Math.Max(0,Math.Min(EndTick,tick));
            tickCredit=0;
            if(target<Tick)
            {
                try {Match=store.CreateReplay(Info);segmentIndex=0;commandIndex=0;VerifiedTick=0;Ended=false;StateRevision++;}
                catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){Fail(error.Message);return;}
            }
            SeekTarget=target;Seeking=!Ended;
        }
        public void Restart()=>Seek(0);
        public void Advance(float seconds,int maxSteps=120)
        {
            if(Error!=null||Ended||maxSteps<=0||paused&&!Seeking)return;
            int stepBudget=Math.Min(maxSteps,240),steps=0,operations=stepBudget*8+64;
            if(!Seeking&&seconds>0&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds))
                tickCredit=Math.Min(stepBudget,tickCredit+Math.Min(seconds,.25f)*Simulation.TickRate*speed);
            try
            {
                while(Error==null&&!Ended)
                {
                    if(!ProcessCurrentTick(ref operations))return;
                    if(Error!=null||Ended)return;
                    if(Seeking&&Tick>=SeekTarget){Seeking=false;tickCredit=0;return;}
                    if(steps>=stepBudget||!Seeking&&tickCredit<1)return;
                    if(Match.Winner!=-1){Fail("The simulation ended before the recorded timeline.");return;}
                    int previous=Tick;Match.Step();
                    StateRevision++;
                    if(Tick!=previous+1){Fail("The replay simulation could not advance.");return;}
                    steps++;if(!Seeking)tickCredit--;
                }
            }
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){Fail(error.Message);}
        }
        // A checkpoint is ordered after its segment's commands, but before the next segment's
        // commands even if they share the same tick. Flattening by tick alone loses this boundary.
        private bool ProcessCurrentTick(ref int operations)
        {
            while(operations>0)
            {
                if(segmentIndex==segments.Length)
                {
                    if(Tick!=EndTick||Info.HasReport&&Match.Winner!=Info.Progress.winner)
                    {Fail("The replay result does not match its recording.");return false;}
                    Ended=true;Seeking=false;paused=true;tickCredit=0;StateRevision++;return true;
                }
                var segment=segments[segmentIndex];
                if(commandIndex<segment.commands.Length)
                {
                    var entry=segment.commands[commandIndex];
                    if(entry.tick<Tick){Fail("A recorded command was missed.");return false;}
                    if(entry.tick>Tick)return true;
                    if(Match.Winner!=-1){Fail("Commands remain after the recorded match ended.");return false;}
                    var result=Match.Submit(RecordedCommand.Copy(entry.command));operations--;StateRevision++;
                    if(result.accepted!=entry.accepted||result.message!=entry.rejection)
                    {Fail("A command diverged at "+FormatTick(Tick)+". This recording may need its original game version.");return false;}
                    commandIndex++;continue;
                }
                if(segment.tick<Tick){Fail("A replay checkpoint was missed.");return false;}
                if(segment.tick>Tick)return true;
                operations--;
                string hash=MatchArchiveStore.Hash(JsonUtility.ToJson(Match.Snapshot()));
                StateRevision++;
                if(hash!=segment.stateHash)
                {Fail("A checkpoint diverged at "+FormatTick(Tick)+". This recording may need its original game version.");return false;}
                VerifiedTick=Tick;segmentIndex++;commandIndex=0;
            }
            return false;
        }
        private static string FormatTick(int tick){int seconds=tick/Simulation.TickRate;return $"{seconds/60:00}:{seconds%60:00}";}
        private void Fail(string reason){Error="Replay stopped: "+reason;Seeking=false;paused=true;tickCredit=0;StateRevision++;}
    }
}
