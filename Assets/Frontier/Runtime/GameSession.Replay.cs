using System;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        // Cooperative work on the main thread, leaving time for VR rendering and input.
        private const double ReplaySeekMilliseconds=2;
        public ReplayPlayback Replay {get;private set;}
        public bool IsReplay=>Replay!=null;
        public bool LastClosedWasReplay {get;private set;}
        public PlayerView[] ReplayPlayerViews {get;private set;}
        public event Action ReplayReset;
        private Catalog setupCatalog;
        private MapDefinition setupMap;
        private string setupFaction,setupEnemy,setupDifficulty,setupMapId;

        public bool StartReplay(string id)
        {
            if(Running){Announce("Exit the current match or replay first.");return false;}
            ReplayPlayback playback;
            try{playback=new ReplayPlayback(History,History.Inspect(id));}
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error))
            {Announce("Replay unavailable: "+error.Message);return false;}
            setupCatalog=Catalog;setupMap=Map;setupFaction=Faction;setupEnemy=EnemyFaction;setupDifficulty=Difficulty;setupMapId=MapId;
            Replay=playback;Match=playback.Match;Catalog=Match.Catalog;Map=Match.Map.Definition;
            Faction=playback.Info.Header.faction;EnemyFaction=playback.Info.Header.enemyFaction;Difficulty=playback.Info.Header.difficulty;MapId=Map.id;
            Selection.Clear();SelectedResource=0;foreach(var group in groups)group.Clear();
            BuildDefinition=null;AbilityDefinitionId=null;AttackMoveMode=false;LastCommandSequence=0;sequence=0;accumulator=0;bot=null;
            pauseReasons.Remove("menu");Running=true;LastClosedWasReplay=false;
            RefreshReplayView();Announce("Replay · Spectator controls active.");NewMatch?.Invoke();Changed?.Invoke();return true;
        }
        public void StopReplay()
        {
            if(!IsReplay)return;
            Replay=null;Match=null;View=null;ReplayPlayerViews=null;Running=false;LastClosedWasReplay=true;
            Catalog=setupCatalog;Map=setupMap;Faction=setupFaction;EnemyFaction=setupEnemy;Difficulty=setupDifficulty;MapId=setupMapId;
            setupCatalog=null;setupMap=null;Selection.Clear();SelectedResource=0;BuildDefinition=null;AbilityDefinitionId=null;AttackMoveMode=false;LastCommandSequence=0;
            pauseReasons.Remove("menu");Announce("Replay closed.");MatchClosed?.Invoke();Changed?.Invoke();
        }
        public void ToggleReplayPause()
        {
            if(!IsReplay||HasSystemPause||Replay.Error!=null)return;
            if(Replay.Ended){Replay.Seek(0);Replay.Paused=false;}
            else Replay.Paused=!Replay.Paused;
            Changed?.Invoke();
        }
        public void SeekReplay(int tick)
        {
            if(!IsReplay||HasSystemPause||Replay.Error!=null)return;
            Replay.Seek(tick);Changed?.Invoke();
        }
        public void SetReplaySpeed(float speed)
        {if(!IsReplay||HasSystemPause)return;Replay.Speed=speed;Changed?.Invoke();}
        private void UpdateReplay()=>AdvanceReplay(Time.unscaledDeltaTime);
        private void AdvanceReplay(float seconds)
        {
            if(pauseReasons.Count>0)return;
            long revision=Replay.StateRevision;bool seeking=Replay.Seeking;var before=Match;
            Replay.Advance(seconds,maxMilliseconds:seeking?ReplaySeekMilliseconds:double.PositiveInfinity);
            // Keep the last published projections and all presentation objects until the
            // requested tick is ready. Transport progress reads Replay directly.
            if(!Replay.Seeking&&(revision!=Replay.StateRevision||seeking||before!=Replay.Match))
            {SyncReplay(before!=Replay.Match||seeking&&!Replay.Seeking);Changed?.Invoke();}
        }
        private void SyncReplay(bool reset)
        {
            Match=Replay.Match;RefreshReplayView();
            if(reset)ReplayReset?.Invoke();
        }
        private void RefreshReplayView()
        {View=Match.ObserverView(out var perspectives);ReplayPlayerViews=perspectives;}
    }
}
