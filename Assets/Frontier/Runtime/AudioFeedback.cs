using System;
using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed class AudioFeedback:MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public const string MusicKey="frontier.audio.music.v1",EffectsKey="frontier.audio.effects.v1";
        public const int VoiceLimit=12;
        public static float MusicVolume=>Volume(MusicKey,.35f);
        public static float EffectsVolume=>Volume(EffectsKey,.65f);
        private static float Volume(string key,float fallback){float value=PlayerPrefs.GetFloat(key,fallback);return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);}
        public static void CycleMusicVolume()=>Cycle(MusicKey,MusicVolume);
        public static void CycleEffectsVolume()=>Cycle(EffectsKey,EffectsVolume);
        private static void Cycle(string key,float value){PlayerPrefs.SetFloat(key,value>=.99f?0:Mathf.Min(1,(Mathf.Floor(value*4)+1)/4));PlayerPrefs.Save();}
        private AudioSource ui,music;
        private readonly AudioSource[] voices=new AudioSource[VoiceLimit];
        private AudioClip[] clips;
        private AudioClip score;
        private readonly Dictionary<int,bool> owned=new Dictionary<int,bool>();
        private PlayerView lastView;
        private int lastAttack,lastAbility,lastCommand,researchCount,result=-1;
        private float lastUi=-1,lastNavigation=-1;
        private bool systemMuted;
        public int PlayedBattleSounds {get;private set;}
        public SoundCue LastCue {get;private set;}
        private void Start()
        {
            clips=new AudioClip[Enum.GetValues(typeof(SoundCue)).Length];
            for(int i=0;i<clips.Length;i++)clips[i]=SoundPalette.Effect((SoundCue)i);
            ui=Source("Interface audio",false);music=Source("Background score",false);
            score=SoundPalette.Music();music.clip=score;music.loop=true;music.volume=0;music.Play();
            for(int i=0;i<voices.Length;i++)voices[i]=Source("Battle voice "+i,true);
            Session.Feedback+=Feedback;Session.NewMatch+=BeginMatch;Session.MatchClosed+=CloseMatch;
            SeedView();
        }
        private AudioSource Source(string name,bool spatial)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=spatial?.85f:0;source.dopplerLevel=0;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=.3f;source.maxDistance=5;source.priority=spatial?160:64;
            return source;
        }
        private void BeginMatch(){lastAttack=lastAbility=lastCommand=0;result=-1;StopBattle();SeedView();PlayUi(SoundCue.Start);}
        private void CloseMatch(){StopBattle();lastView=null;owned.Clear();result=-1;}
        private void SeedView()
        {
            owned.Clear();lastView=Session.View;
            if(lastView==null)return;
            foreach(var e in lastView.entities)if(e.owner==lastView.player)owned[e.id]=e.complete;
            researchCount=lastView.researched.Length;
        }
        private void Feedback(bool success)
        {
            if(Time.unscaledTime-lastUi<.045f)return;lastUi=Time.unscaledTime;
            var cue=SoundCue.Select;
            if(!success)cue=SoundCue.Reject;
            else if(Session.LastCommandSequence>0&&Session.LastCommandSequence!=lastCommand)
            {lastCommand=Session.LastCommandSequence;cue=Session.LastCommandKind==CommandKind.Move?SoundCue.Move:SoundCue.Attack;}
            PlayUi(cue);
        }
        public void Navigation(bool confirm)
        {
            if(Time.unscaledTime-lastNavigation<.065f)return;lastNavigation=Time.unscaledTime;
            PlayUi(confirm?SoundCue.Confirm:SoundCue.Hover);
        }
        private void PlayUi(SoundCue cue)
        {
            if(ui==null||Session.HasSystemPause)return;LastCue=cue;
            ui.volume=EffectsVolume*.65f;
            // One short interface voice: rapid input cannot accumulate a wall of beeps.
            ui.Stop();ui.clip=clips[(int)cue];ui.Play();
        }
        private void LateUpdate()
        {
            if(music==null)return;
            ui.volume=EffectsVolume*.65f;
            bool muted=Session.HasSystemPause;
            if(muted!=systemMuted)
            {systemMuted=muted;if(muted){music.Pause();ui.Stop();StopBattle();}else music.UnPause();}
            float level=MusicVolume*(Session.Running?(Session.IsPaused?.18f:.32f):.24f);
            music.volume=MusicVolume<=0?0:Mathf.MoveTowards(music.volume,level,Time.unscaledDeltaTime*.15f);
            foreach(var voice in voices)voice.volume=EffectsVolume*.32f;
            var view=Session.View;
            if(view==null||!Session.Running)return;
            if(Session.IsPaused)
            {StopBattle();ConsumeEvents(view);SeedView();return;}
            if(ReferenceEquals(lastView,view))return;
            lastView=view;
            if(view.winner>=0&&result!=view.winner)
            {result=view.winner;StopBattle();PlayUi(view.winner==view.player?SoundCue.Victory:SoundCue.Defeat);return;}
            if(view.winner>=0)return;
            int budget=3;
            foreach(var shot in view.attacks)
            {
                if(shot.id<=lastAttack)continue;lastAttack=shot.id;
                if(view.tick-shot.tick>6||budget<=0)continue;
                var type=Session.Catalog[shot.definition];
                var cue=type.role=="siege"?SoundCue.Siege:type.role=="worker"||type.role=="melee"?SoundCue.Melee:type.faction=="biological"?SoundCue.Organic:SoundCue.Kinetic;
                if(PlayBattle(cue,shot.origin))budget--;
            }
            foreach(var ability in view.abilityEvents)
            {
                if(ability.id<=lastAbility)continue;lastAbility=ability.id;
                if(view.tick-ability.tick<=6&&budget>0&&PlayBattle(SoundCue.Ability,ability.position))budget--;
            }
            bool ready=view.researched.Length>researchCount;researchCount=view.researched.Length;
            foreach(var e in view.entities)
            {
                if(e.owner!=view.player)continue;
                if(e.complete&&(!owned.TryGetValue(e.id,out bool complete)||!complete))ready=true;
                owned[e.id]=e.complete;
            }
            if(ready)PlayUi(SoundCue.Ready);
        }
        private bool PlayBattle(SoundCue cue,Point point)
        {
            if(Board?.Content==null||!Board.Content.gameObject.activeInHierarchy||EffectsVolume<=0)return false;
            foreach(var voice in voices)if(!voice.isPlaying)
            {
                voice.transform.position=Board.Content.TransformPoint(new Vector3(point.x,Board.HeightAt(point)+.4f,point.y));
                voice.clip=clips[(int)cue];voice.pitch=1;voice.Play();PlayedBattleSounds++;return true;
            }
            return false;
        }
        private void ConsumeEvents(PlayerView view)
        {foreach(var shot in view.attacks)lastAttack=Mathf.Max(lastAttack,shot.id);foreach(var ability in view.abilityEvents)lastAbility=Mathf.Max(lastAbility,ability.id);}
        private void StopBattle(){foreach(var voice in voices)if(voice!=null)voice.Stop();}
        private void OnDestroy()
        {
            if(Session!=null){Session.Feedback-=Feedback;Session.NewMatch-=BeginMatch;Session.MatchClosed-=CloseMatch;}
            if(ui!=null)ui.Stop();if(music!=null)music.Stop();StopBattle();
            if(clips!=null)foreach(var clip in clips)Release(clip);Release(score);
        }
        private static void Release(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
