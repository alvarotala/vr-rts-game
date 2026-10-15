// Explicitly enabled by a command-line flag; never activated on Quest.
#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed class DesktopSmokeTest:MonoBehaviour
    {
        private static int errors;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ObserveStartup()
        {
            errors=0;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--frontier-smoke")>=0)
                Application.logMessageReceived+=OnLog;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--frontier-smoke");
            if(index<0||index+1>=args.Length)return;
            var runner=new GameObject("Desktop smoke test").AddComponent<DesktopSmokeTest>();runner.output=args[index+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            for(int i=0;i<60;i++)yield return null;
            var game=FindFirstObjectByType<GameSession>();var board=FindFirstObjectByType<BoardView>();var panel=FindFirstObjectByType<CommandPanel>();var quest=FindFirstObjectByType<QuestBridge>();
            game.Group(0,false); // Shortcuts before starting must be harmless.
            yield return Capture("01-setup.png");
            var button=GameObject.Find("COMENZAR PARTIDA");
            if(button==null){Debug.LogError("Start button not found");Application.Quit(1);yield break;}
            game.Pause("tracking",true);
            var rect=button.GetComponent<RectTransform>();Vector3 center=rect.TransformPoint(rect.rect.center);var ray=new Ray(quest.Camera.transform.position,(center-quest.Camera.transform.position).normalized);
            panel.Point(ray,true,false);panel.Point(ray,false,true);
            if(!game.IsPaused)Debug.LogError("Starting a match cleared a tracking pause");
            game.Pause("tracking",false);
            for(int i=0;i<90;i++)yield return null;
            if(!game.Running){Debug.LogError("UI did not start the match");Application.Quit(1);yield break;}
            yield return Capture("02-match.png");
            var worker=game.View.entities.First(e=>e.owner==0&&game.Catalog[e.definition].role=="worker");game.Select(worker.id,false);
            board.Zoom=2;board.Pan=new Vector2(-game.Map.width*.25f,-game.Map.height*.25f);
            for(int i=0;i<60;i++)yield return null;
            yield return Capture("03-command-panel.png");
            bool moved=game.Send(CommandKind.Move,position:new Point(22,18));
            Point beforeMove=game.View.entities.First(e=>e.id==worker.id).position;
            for(int i=0;i<180;i++)yield return null;
            bool travelled=game.View.entities.First(e=>e.id==worker.id).position.Distance(beforeMove)>1;
            bool invariants=game.Match.Players.All(p=>p.minerals>=0&&p.catalyst>=0)&&game.Match.Tick>0;
            File.WriteAllText(Path.Combine(output,"smoke-result.json"),"{\"errors\":"+errors+",\"tick\":"+game.Match.Tick+",\"moveAccepted\":"+moved.ToString().ToLowerInvariant()+",\"travelled\":"+travelled.ToString().ToLowerInvariant()+",\"invariants\":"+invariants.ToString().ToLowerInvariant()+"}");
            Debug.Log("FRONTIER_DESKTOP_SMOKE "+errors+" errors; tick "+game.Match.Tick);
            Application.logMessageReceived-=OnLog;Application.Quit(errors==0&&moved&&travelled&&invariants?0:1);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());Destroy(texture);
        }
        private static void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    }
}
#endif
