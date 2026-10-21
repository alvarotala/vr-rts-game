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
    public static class ComfortAudioPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
        public static void Run()
        {
            RenderControllerWheel();
            MenuFlowPreview.Render();
            AttackPointerPreview.Render();
            Debug.Log("FRONTIER_COMFORT_AUDIO_BATCH passed; no player build");
        }
        public static void RenderControllerWheel()
        {
            ControllerAssets.Prepare();
            Render();
            UnifiedWheelPreview.Render();
        }
        public static void Render()
        {
            const string output="TestResults/ComfortAudio";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Comfort audio fixture");
            var boardObject=new GameObject("Comfort board");var console=new GameObject("Comfort console");
            var tracking=new GameObject("Comfort tracking");ControllerVisuals visual=null;Camera createdCamera=null;
            bool hadMusic=PlayerPrefs.HasKey(AudioFeedback.MusicKey),hadEffects=PlayerPrefs.HasKey(AudioFeedback.EffectsKey);
            float oldMusic=PlayerPrefs.GetFloat(AudioFeedback.MusicKey),oldEffects=PlayerPrefs.GetFloat(AudioFeedback.EffectsKey);
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                if(quest.Camera==null)Call(quest,"Awake");createdCamera=quest.Camera;Check(createdCamera.GetComponents<AudioListener>().Length==1,"The camera has exactly one audio listener");
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                preview.camera.transform.position=new Vector3(0,1.2f,-.5f);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                board.Placed=false;Call(quest,"TrackingAcquired");
                Check(!quest.Placing&&!session.Running&&!session.IsPaused,"Tracking acquired on the main menu does not request placement");
                quest.RequestStartMatch();Check(quest.Placing&&quest.StartPending&&!session.Running,"Missing placement opens only when starting a match");
                quest.CancelPlacement();Check(!quest.Placing&&!quest.StartPending&&!session.Running,"Cancel leaves the setup without a match");
                quest.RequestStartMatch();quest.PlaceRay(new Ray(new Vector3(0,2,0),Vector3.down),0,Vector2.zero,true);
                Check(board.Placed&&!quest.Placing&&!quest.StartPending&&session.Running&&!session.IsPaused,"Confirming the requested placement starts the match once");
                var position=board.transform.position;session.LeaveMatch();quest.RequestStartMatch();
                Check(session.Running&&!quest.Placing&&board.transform.position==position,"A retained board starts the next match directly");
                session.LeaveMatch();Call(panel,"Show",CommandPanel.MenuPage.MatchSetup);board.Placed=false;Set(quest,"restoring",true);quest.RequestStartMatch();
                Check(quest.StartPending&&!quest.Placing&&!session.Running,"Starting waits for restoration without competing placement");
                panel.Back();Set(quest,"restoring",false);
                Check(!quest.StartPending&&!session.Running,"Leaving setup cancels a start waiting for restoration");
                board.Placed=true;quest.RequestStartMatch();

                typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(quest,tracking.transform);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                visual=new ControllerVisuals(fixture.transform);visual.Root.SetParent(null,true);preview.AddSingleGO(visual.Root.gameObject);
                var wheelObject=new GameObject("Comfort wheel");wheelObject.transform.SetParent(fixture.transform);
                var wheel=wheelObject.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;
                var rayObject=new GameObject("Visible laser");rayObject.transform.SetParent(visual.Root,false);
                var laser=rayObject.AddComponent<LineRenderer>();laser.positionCount=2;laser.widthMultiplier=.0015f;
                var rayMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));rayMaterial.color=BoardView.NormalPointerColor;laser.sharedMaterial=rayMaterial;
                foreach(bool left in new[]{false,true})
                {
                    panel.Bindings.SelectHand(left);var controller=quest.ActiveController;
                    var rotation=Quaternion.identity;
                    tracking.transform.rotation=rotation*Quaternion.Inverse(OVRInput.GetLocalControllerRotation(controller));
                    tracking.transform.position=new Vector3(0,.8f,0)-tracking.transform.rotation*OVRInput.GetLocalControllerPosition(controller);
                    visual.Update(quest,BoardView.NormalPointerColor);var ray=quest.PointerRay();
                    Check(visual.Root.gameObject.activeSelf&&Vector3.Distance(visual.Root.position,ray.origin)<.0001f,"The chosen Touch model follows its tracked hand");
                    Check(visual.Root.GetComponentsInChildren<Renderer>().Length>1&&visual.Root.GetComponentsInChildren<Collider>(true).Length==0,"SDK controller has renderable geometry and no input-blocking colliders");
                    Check(visual.Root.GetChild(left?1:0).gameObject.activeSelf&&!visual.Root.GetChild(left?0:1).gameObject.activeSelf,"Only the active hand model is shown");
                    Check(Vector3.Distance(visual.EmitterPosition,ray.GetPoint(ControllerVisuals.EmitterDistance))<.0001f,"The rendered laser begins at the controller emitter");
                    var renderers=visual.Root.GetChild(left?1:0).GetComponentsInChildren<Renderer>();var modelBounds=renderers[0].bounds;
                    var body=visual.Root.GetChild(left?1:0).GetComponentInChildren<SkinnedMeshRenderer>();
                    Check(AssetDatabase.GetAssetPath(body.sharedMaterial.mainTexture).Contains("TouchPlus_"+(left?"Left":"Right")+"_BaseColor"),
                        "Each controller body uses its own Touch Plus color texture, not the battery indicator");
                    foreach(var renderer in renderers)modelBounds.Encapsulate(renderer.bounds);
                    Check(modelBounds.size.magnitude>.08f&&modelBounds.size.magnitude<.3f,"Touch model has controller-scale physical dimensions: "+modelBounds.size);
                    laser.SetPosition(0,visual.EmitterPosition);laser.SetPosition(1,ray.GetPoint(.22f));
                    wheel.TickHint(ray,true);
                    var hint=Field<RectTransform>(wheel,"hintRoot");
                    var stick=visual.Root.GetChild(left?1:0).GetComponentsInChildren<Transform>(true).Single(part=>part.name.EndsWith("_b_thumbstick"));
                    Check(Vector3.ProjectOnPlane(hint.position-stick.position,stick.up).magnitude<.0001f
                        &&Vector3.Dot(hint.forward,stick.up)<-.999f,"Callout leaders remain anchored over the visible controller stick");
                    Check(hint.GetComponentsInChildren<UnityEngine.UI.Text>().All(label=>
                            Mathf.Abs(label.rectTransform.anchoredPosition.x)*hint.lossyScale.x>.05f),
                        "All directional and button hints sit beside the active controller");
                    var anchors=new[]{"touch:trigger","touch:b","touch:a","touch:grip"}.Select(button=>ControllerVisuals.ButtonPosition(left,button)).ToArray();
                    Check(anchors.Distinct().Count()==4&&anchors.All(anchor=>Vector3.Distance(anchor,ControllerVisuals.StickSurface(left).position)>.01f),
                        "Button leaders have distinct physical anchors, separate from the stick");
                    preview.camera.orthographic=false;preview.camera.fieldOfView=48;preview.camera.nearClipPlane=.01f;
                    preview.camera.transform.position=new Vector3(left?-.07f:.07f,1.08f,-.28f);preview.camera.transform.LookAt(new Vector3(0,.81f,.01f));
                    HarvestingPreview.SaveCanvas(wheel.transform,hint,output+"/"+(left?"left":"right")+"-controller.png",preview,1100,900,preservePose:true);
                    wheel.ProcessUnified(ray,Vector2.zero,false,false,false,false,10);
                    wheel.ProcessUnified(ray,Vector2.up,false,false,false,false,10.1f);
                    var opened=Field<RectTransform>(wheel,"root");
                    var eye=(preview.camera.transform.position-opened.position).normalized;
                    Check(wheel.InGroupMenu&&!wheel.HintVisible&&Vector3.Distance(opened.position,hint.position)<.025f
                        &&Vector3.Angle(-opened.forward,stick.up)<=35.1f&&Vector3.Dot(-opened.forward,eye)>Vector3.Dot(stick.up,eye),
                        "The open wheel stays close to the controller and tilts toward the viewing position");
                    laser.enabled=false;
                    HarvestingPreview.SaveCanvas(wheel.transform,opened,output+"/"+(left?"left":"right")+"-controller-wheel.png",preview,1100,900,preservePose:true);
                    wheel.Close();laser.enabled=true;
                }
                session.Pause("tracking",true);visual.Update(quest,Color.white);Check(!visual.Root.gameObject.activeSelf,"System interruptions hide the controller");session.Pause("tracking",false);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);visual.Update(quest,Color.white);Check(!visual.Root.gameObject.activeSelf,"Desktop does not show a floating VR controller");
                Object.DestroyImmediate(rayMaterial);

                PlayerPrefs.SetFloat(AudioFeedback.MusicKey,.35f);PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,.65f);
                var audio=fixture.AddComponent<AudioFeedback>();audio.Session=session;audio.Board=board;Call(audio,"Start");
                var score=Field<AudioClip>(audio,"score");var clips=Field<AudioClip[]>(audio,"clips");
                Check(score==Resources.Load<AudioClip>("Music/neon-loop")&&score.length>100&&score.channels==2,
                    "Neon Loop uses the supplied stereo soundtrack");
                var fallback=SoundPalette.Music();
                try
                {
                    Check(fallback.length>=39.9f&&fallback.channels==2,"Custom maps retain the forty-second fallback score");
                    var samples=new float[fallback.samples*fallback.channels];fallback.GetData(samples,0);
                    Check(samples.All(v=>!float.IsNaN(v)&&Mathf.Abs(v)<=.81f)&&samples.Any(v=>Mathf.Abs(v)>.1f),"Fallback music contains finite audible samples with headroom");
                    Check(Mathf.Abs(samples[0]-samples[samples.Length-2])<.001f&&Mathf.Abs(samples[1]-samples[samples.Length-1])<.001f,"Fallback loop joins without a sample discontinuity");
                    WriteWave(output+"/quiet-orbit.wav",fallback);
                }
                finally{Object.DestroyImmediate(fallback);}
                foreach(var clip in clips)
                {
                    var data=new float[clip.samples];clip.GetData(data,0);
                    Check(data.Any(v=>Mathf.Abs(v)>.02f)&&data.All(v=>!float.IsNaN(v)&&Mathf.Abs(v)<1),clip.name+" is audible and does not clip");
                }
                var worker=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building);session.Select(worker.id,false);
                Set(audio,"lastUi",-1f);session.Send(CommandKind.Move,position:new Point(22,15));Check(audio.LastCue==SoundCue.Move,"Move has its own accepted-order sound");
                Set(audio,"lastUi",-1f);session.Send(CommandKind.AttackMove,position:new Point(22,15));Check(audio.LastCue==SoundCue.Attack,"Attack has a distinct order sound");
                Set(audio,"lastUi",-1f);session.Send(CommandKind.Move,position:new Point(-50,-50));Check(audio.LastCue==SoundCue.Reject,"Rejected orders produce the rejection cue");
                board.SetVisible(true);var view=session.Match.View(0);view.tick=10;
                view.attacks=Enumerable.Range(1,20).Select(id=>new AttackEvent{id=id,tick=10,definition=worker.definition,origin=worker.position}).ToArray();
                typeof(GameSession).GetProperty("View").SetValue(session,view);Call(audio,"LateUpdate");
                Check(audio.PlayedBattleSounds==3&&Field<AudioSource[]>(audio,"voices").Length==AudioFeedback.VoiceLimit,"A twenty-shot frame is limited to three sounds and a fixed voice pool");
                Call(audio,"LateUpdate");Check(audio.PlayedBattleSounds==3,"Repeated rendering cannot replay the same shots");
                session.Pause("menu",true);view.attacks=new[]{new AttackEvent{id=21,tick=10,definition=worker.definition,origin=worker.position}};Call(audio,"LateUpdate");
                session.Pause("menu",false);view=session.Match.View(0);view.tick=10;view.attacks=new[]{new AttackEvent{id=21,tick=10,definition=worker.definition,origin=worker.position}};
                typeof(GameSession).GetProperty("View").SetValue(session,view);Call(audio,"LateUpdate");Check(audio.PlayedBattleSounds==3,"Pausing consumes queued sounds without replaying them on resume");
                PlayerPrefs.SetFloat(AudioFeedback.MusicKey,0);PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,0);Call(audio,"LateUpdate");
                Check(Field<AudioSource>(audio,"music").volume==0&&Field<AudioSource>(audio,"ui").volume==0&&Field<AudioSource[]>(audio,"voices").All(v=>v.volume==0),"Music and effects can both be muted");
                AudioFeedback.CycleMusicVolume();AudioFeedback.CycleEffectsVolume();Check(AudioFeedback.MusicVolume==.25f&&AudioFeedback.EffectsVolume==.25f,"Separate volume controls persist their values");
                session.LeaveMatch();Call(audio,"LateUpdate");Check(Field<AudioSource[]>(audio,"voices").All(v=>!v.isPlaying),"Leaving the match stops all battlefield voices");
                Debug.Log($"FRONTIER_COMFORT_AUDIO passed: {checks} checks, 4 controller views, stereo WAV; no player build");
            }
            finally
            {
                if(hadMusic)PlayerPrefs.SetFloat(AudioFeedback.MusicKey,oldMusic);else PlayerPrefs.DeleteKey(AudioFeedback.MusicKey);
                if(hadEffects)PlayerPrefs.SetFloat(AudioFeedback.EffectsKey,oldEffects);else PlayerPrefs.DeleteKey(AudioFeedback.EffectsKey);PlayerPrefs.Save();
                visual?.Dispose();Object.DestroyImmediate(console);Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);Object.DestroyImmediate(tracking);
                if(createdCamera!=null)Object.DestroyImmediate(createdCamera.gameObject);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
            void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("COMFORT_AUDIO_PASS: "+message);}
        }
        private static void WriteWave(string path,AudioClip clip)
        {
            var data=new float[clip.samples*clip.channels];clip.GetData(data,0);
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                void Tag(string tag)=>stream.Write(System.Text.Encoding.ASCII.GetBytes(tag));
                Tag("RIFF");stream.Write(36+data.Length*2);Tag("WAVE");Tag("fmt ");stream.Write(16);stream.Write((short)1);stream.Write((short)clip.channels);
                stream.Write(clip.frequency);stream.Write(clip.frequency*clip.channels*2);stream.Write((short)(clip.channels*2));stream.Write((short)16);Tag("data");stream.Write(data.Length*2);
                foreach(float sample in data)stream.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
            }
        }
    }
}
