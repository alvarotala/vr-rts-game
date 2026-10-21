using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class MapAudioPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object obj,string method)=>obj.GetType().GetMethod(method,Private).Invoke(obj,null);
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        [MenuItem("Frontier/Check map soundtracks")]
        public static void Run()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-map-music-"+Guid.NewGuid().ToString("N"));
            var fixture=new GameObject("Map music fixture");
            bool hadMusic=PlayerPrefs.HasKey(AudioFeedback.MusicKey),hadEffects=PlayerPrefs.HasKey(AudioFeedback.EffectsKey);
            float oldMusic=PlayerPrefs.GetFloat(AudioFeedback.MusicKey),oldEffects=PlayerPrefs.GetFloat(AudioFeedback.EffectsKey);
            AudioFeedback audio=null;
            int checks=0;
            try
            {
                PlayerPrefs.SetFloat(AudioFeedback.MusicKey,.35f);PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,.65f);
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                session.LoadMap("training");
                audio=fixture.AddComponent<AudioFeedback>();audio.Session=session;Call(audio,"Start");
                var music=Field<AudioSource>(audio,"music");
                var ids=new[]{"training","rift","desert"};
                var names=new[]{"neon-loop","forest-watch","desser-alert"};
                var lengths=new[]{119.68f,209.96f,189.88f};
                var imported=new AudioClip[ids.Length];
                for(int i=0;i<ids.Length;i++)
                {
                    var clip=imported[i]=Resources.Load<AudioClip>("Music/"+names[i]);
                    Check(clip!=null&&clip.channels==2&&clip.frequency==48000&&Mathf.Abs(clip.length-lengths[i])<.1f,
                        names[i]+": supplied stereo recording imports at its full duration");
                    var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                    var settings=importer.defaultSampleSettings;
                    Check(settings.loadType==AudioClipLoadType.Streaming&&!settings.preloadAudioData&&importer.loadInBackground,
                        names[i]+": music streams without preloading decoded PCM");
                    session.LoadMap(ids[i]);Call(audio,"LateUpdate");
                    Check(music.clip==clip&&music.loop&&music.spatialBlend==0,"Map selector switches to "+names[i]+" on the same looping source");
                    music.time=1;float at=music.time;Call(audio,"LateUpdate");
                    Check(music.clip==clip&&Mathf.Abs(music.time-at)<.1f,"An unchanged map does not restart "+names[i]);
                }
                Check(fixture.GetComponentsInChildren<AudioSource>().Length==AudioFeedback.VoiceLimit+2,
                    "Switching all maps retains one music source and the fixed sound-effect pool");
                Check(Field<AudioClip>(audio,"generatedScore")==null,"Bundled maps do not allocate the procedural fallback score");

                session.Pause("tracking",true);Call(audio,"LateUpdate");session.LoadMap("rift");Call(audio,"LateUpdate");
                Check(music.clip==imported[1]&&!music.isPlaying&&Field<bool>(audio,"systemMuted"),
                    "Changing maps during tracking loss keeps the new soundtrack paused");
                session.Pause("tracking",false);Call(audio,"LateUpdate");
                Check(!Field<bool>(audio,"systemMuted")&&music.clip==imported[1],"Restoring tracking retains the selected soundtrack");
                PlayerPrefs.SetFloat(AudioFeedback.MusicKey,0);PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,0);Call(audio,"LateUpdate");
                Check(music.volume==0&&Field<AudioSource>(audio,"ui").volume==0&&Field<AudioSource[]>(audio,"voices").All(v=>v.volume==0),
                    "Music and effects still mute independently of track selection");
                AudioFeedback.CycleMusicVolume();AudioFeedback.CycleEffectsVolume();
                Check(AudioFeedback.MusicVolume==.25f&&AudioFeedback.EffectsVolume==.25f,"Both volume controls retain their saved quarter-step behavior");

                session.LoadMap("training");session.StartMatch();
                Check(music.clip==imported[0],"Starting a match selects its soundtrack before the next rendered frame");
                session.LeaveMatch();
                var store=new MatchArchiveStore(directory);
                typeof(GameSession).GetField("history",Private).SetValue(session,store);
                var map=JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/desert").text);
                map.title="Archived desert title";
                var match=new Simulation(map,session.Catalog);var writer=store.Begin(match,"normal");writer.Flush();
                match.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();
                Check(session.StartReplay(store.List().Single().Id)&&music.clip==imported[2],
                    "An archived map with its old title selects music by stable ID when replay starts");
                session.StopReplay();
                Check(session.Map.id=="training"&&music.clip==imported[0],"Closing a replay restores the setup map's soundtrack immediately");

                var custom=JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/training").text);custom.id="custom-map";
                typeof(GameSession).GetProperty("Map").SetValue(session,custom);Call(audio,"LateUpdate");
                var fallback=Field<AudioClip>(audio,"generatedScore");
                Check(fallback!=null&&music.clip==fallback&&fallback.length>=39.9f,"Unknown map IDs receive the procedural fallback");
                session.LoadMap("desert");Call(audio,"LateUpdate");
                // This non-ExecuteAlways behaviour is driven manually in EditMode,
                // including teardown: DestroyImmediate alone does not run its lifecycle.
                Call(audio,"OnDestroy");
                Check(fallback==null,"Audio teardown releases the owned procedural fallback");
                Check(imported.All(clip=>clip!=null),"Audio teardown preserves imported soundtrack assets");
                Object.DestroyImmediate(audio);
                Debug.Log($"FRONTIER_MAP_AUDIO passed: {checks} checks; all supplied tracks, selector, pause, replay and ownership; no player build");
            }
            finally
            {
                if(audio!=null)Call(audio,"OnDestroy");
                Object.DestroyImmediate(fixture);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
                if(hadMusic)PlayerPrefs.SetFloat(AudioFeedback.MusicKey,oldMusic);else PlayerPrefs.DeleteKey(AudioFeedback.MusicKey);
                if(hadEffects)PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,oldEffects);else PlayerPrefs.DeleteKey(AudioFeedback.EffectsKey);
                PlayerPrefs.Save();
            }
            void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("MAP_AUDIO_PASS: "+message);}
        }
    }
}
