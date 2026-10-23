using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class TutorialBoardFocusPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string method,params object[] arguments)
            =>target.GetType().GetMethod(method,Private).Invoke(target,arguments);

        [MenuItem("Frontier/Preview tutorial board reading focus")]
        public static void Run()
        {
            const string output="TestResults/TutorialBoardFocus";const int size=1100;
            Directory.CreateDirectory(output);
            var fixture=new GameObject("Tutorial focus session");var boardObject=new GameObject("Tutorial focus board");
            var preview=new PreviewRenderUtility();int checks=0;
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            float density=Shader.GetGlobalFloat("_FrontierFogDensity"),lighting=Shader.GetGlobalFloat("_FrontierEnhancedLighting"),focus=Value();
            var lightColor=Shader.GetGlobalColor("_FrontierLightColor");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Call(board,"Start");
                preview.AddSingleGO(boardObject);session.StartTutorial();Call(board,"LateUpdate");
                Call(board,"ResetTutorialFocus");
                var visibility=(byte[])session.View.visibility.Clone();int tick=session.View.tick;
                int[] observed=session.View.entities.Select(e=>e.id).ToArray();
                Check(session.IsPaused&&session.Tutorial.Briefing,"The fixture is a real tutorial reading pause");
                var target=board.Content.TransformPoint(new Vector3(14,0,15));
                preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*19;
                preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
                preview.camera.transform.position=target+new Vector3(.15f,.7f,-.45f);preview.camera.transform.LookAt(target);
                var normal=Capture("01-normal");
                Advance(.07f);Check(Value()>0&&Value()<1,"Desaturation begins gradually with real time while simulation is paused");
                float partial=Value();Advance(.07f);Check(Value()>partial&&Value()<1,"The transition continues without a one-frame jump");
                Advance(1);Check(Value()==1,"A reading pause reaches the subdued map presentation");
                var reading=Capture("02-reading");
                var solid=Enumerable.Range(0,normal.Length).Where(i=>normal[i].a==255&&Chroma(normal[i])>.025).ToArray();
                Check(solid.Length>size*size/10,"The rendered board contains terrain, units and resources for comparison");
                Check(solid.Average(i=>Chroma(reading[i]))<solid.Average(i=>Chroma(normal[i]))*.55,
                    "The actual map rendering loses most color while the lesson is waiting");
                Check(solid.Average(i=>Luma(reading[i]))>solid.Average(i=>Luma(normal[i]))*.65,
                    "Reading focus preserves terrain visibility rather than blacking out the map");
                Check(normal.Zip(reading,(a,b)=>a.a==b.a).All(same=>same),"The focus effect preserves coverage, including transparent MR surroundings");
                Check(session.View.tick==tick&&visibility.SequenceEqual(session.View.visibility)&&observed.SequenceEqual(session.View.entities.Select(e=>e.id)),
                    "The effect does not advance simulation, alter vision or reveal entities");
                session.ContinueTutorial();session.ContinueTutorial();
                Check(!session.Tutorial.Briefing&&!session.IsPaused,"Continue returns to an active map objective");
                Advance(.07f);Check(Value()>0&&Value()<1,"Map colors return smoothly when Continue is pressed");
                Advance(1);Check(Value()==0,"Active map objectives restore normal color");
                var restored=Capture("03-restored");
                Check(normal.SequenceEqual(restored),"Restored map pixels exactly match the original rendering");
                session.Pause("menu",true);Advance(1);
                Check(Value()==0,"An ordinary menu pause does not gray the map");session.Pause("menu",false);
                session.RestartTutorial();Advance(1);Check(Value()==1,"Restarting the lesson restores its reading state");
                board.SetVisible(false);Check(Value()==0,"Hiding the board immediately releases its shader global");
                board.SetVisible(true);Advance(1);Check(Value()==1,"Showing a paused lesson restores its focus");
                board.enabled=false;
                // BoardView is not ExecuteAlways: this edit-mode fixture drives
                // its actual lifecycle methods, just as it drives Start/LateUpdate.
                // In play mode Unity remains responsible for invoking them.
                if(!Application.isPlaying)Call(board,"OnDisable");
                Check(Value()==0,"The disable callback releases reading focus before another scene");
                board.enabled=true;Advance(1);Check(Value()==1,"Re-enabling the lesson restores its intended appearance");
                session.LeaveMatch();Check(Value()==0,"Leaving the tutorial resets focus during board rebuild");
                session.StartMatch();session.Pause("menu",true);Advance(1);
                Check(Value()==0,"A normal match remains unchanged even when paused");
                session.LeaveMatch();session.StartTutorial();Advance(1);
                if(!Application.isPlaying)Call(board,"OnDestroy");
                Object.DestroyImmediate(boardObject);Check(Value()==0,"The destroy callback resets the shared shader value");
                foreach(string shader in new[]{"Board","Fog","Weapons","Impact","Remains","ContactShadows"})
                    Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/"+shader)),shader+" shader compiles");
                Debug.Log($"FRONTIER_TUTORIAL_BOARD_FOCUS passed: {checks} checks, 3 views; paused reading desaturation, smooth restore, unchanged fog/coverage, lifecycle cleanup; no physical Quest validation");

                void Advance(float delta)=>Call(board,"AdvanceTutorialFocus",delta);
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
                Color32[] Capture(string name)
                {
                    preview.BeginPreview(new Rect(0,0,size,size),GUIStyle.none);
                    RenderTexture source;try{preview.Render(true);}finally{source=(RenderTexture)preview.EndPreview();}
                    var targetTexture=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32);
                    var previous=RenderTexture.active;var image=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
                    try
                    {
                        Graphics.Blit(source,targetTexture);RenderTexture.active=targetTexture;
                        image.ReadPixels(new Rect(0,0,size,size),0,0);image.Apply();
                        File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();
                    }
                    finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(targetTexture);Object.DestroyImmediate(image);}
                }
            }
            finally
            {
                if(boardObject!=null)Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
                Shader.SetGlobalFloat("_FrontierFogDensity",density);Shader.SetGlobalFloat("_FrontierEnhancedLighting",lighting);
                Shader.SetGlobalColor("_FrontierLightColor",lightColor);Shader.SetGlobalFloat("_FrontierTutorialFocus",focus);
            }
        }
        private static float Value()=>Shader.GetGlobalFloat("_FrontierTutorialFocus");
        private static double Chroma(Color32 c)=>(Math.Max(c.r,Math.Max(c.g,c.b))-Math.Min(c.r,Math.Min(c.g,c.b)))/255.0;
        private static double Luma(Color32 c)=>(c.r*.2126+c.g*.7152+c.b*.0722)/255.0;
    }
}
