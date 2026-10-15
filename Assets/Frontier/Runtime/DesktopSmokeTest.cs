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
        private const string PanelKey="frontier.panel-placement.v1";
        private string previousPanelPlacement;
        private bool hadPanelPlacement;
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
            // The scripted rays own input during the smoke run; desktop mouse activity must not interfere.
            FindFirstObjectByType<CommanderInput>().enabled=false;
            hadPanelPlacement=PlayerPrefs.HasKey(PanelKey);previousPanelPlacement=PlayerPrefs.GetString(PanelKey);
            panel.ResetPosition();yield return null;
            game.Group(0,false); // Shortcuts before starting must be harmless.
            yield return Capture("01-setup.png");
            var button=GameObject.Find("START MATCH");
            if(button==null){Debug.LogError("Start button not found");Application.Quit(1);yield break;}
            game.Pause("tracking",true);
            var rect=button.GetComponent<RectTransform>();Vector3 center=rect.TransformPoint(rect.rect.center);var ray=new Ray(quest.Camera.transform.position,(center-quest.Camera.transform.position).normalized);
            panel.Point(ray,true,false);panel.Point(ray,false,true);
            if(!game.IsPaused)Debug.LogError("Starting a match cleared a tracking pause");
            game.Pause("tracking",false);
            for(int i=0;i<90;i++)yield return null;
            if(!game.Running){Debug.LogError("UI did not start the match");Application.Quit(1);yield break;}
            yield return Capture("02-match.png");
            var console=GameObject.Find("Command console").transform;
            var handle=GameObject.Find("Move menu").GetComponent<RectTransform>();
            Vector3 originalPanel=console.position;
            var handleCenter=handle.TransformPoint(handle.rect.center);
            var grabRay=new Ray(quest.Camera.transform.position,(handleCenter-quest.Camera.transform.position).normalized);
            panel.Point(grabRay,true,false);
            var movedRay=new Ray(grabRay.origin+new Vector3(.25f,.12f,0),grabRay.direction);
            panel.Point(movedRay,false,false);panel.Point(movedRay,false,true);
            for(int i=0;i<5;i++)yield return null;
            bool menuMoved=Vector3.Distance(originalPanel,console.position)>.2f;
            Vector3 menuOffset=board.transform.InverseTransformPoint(console.position);
            // World placement must survive board relocation; view zoom must not move the menu.
            var boardPosition=board.transform.position;var boardRotation=board.transform.rotation;
            board.transform.SetPositionAndRotation(boardPosition+new Vector3(.2f,0,.1f),Quaternion.Euler(0,35,0));
            yield return null;yield return null;
            bool menuAnchored=Vector3.Distance(menuOffset,board.transform.InverseTransformPoint(console.position))<.001f;
            board.transform.SetPositionAndRotation(boardPosition,boardRotation);
            yield return null;yield return null;
            var worker=game.View.entities.First(e=>e.owner==0&&game.Catalog[e.definition].role=="worker");game.Select(worker.id,false);
            board.Zoom=2;board.Pan=new Vector2(-game.Map.width*.25f,-game.Map.height*.25f);
            for(int i=0;i<60;i++)yield return null;
            menuAnchored &= Vector3.Distance(menuOffset,board.transform.InverseTransformPoint(console.position))<.001f;
            yield return Capture("03-command-panel.png");
            board.Fit();panel.Recall();yield return null;yield return null;
            board.DrawSelection(new Point(44,32),new Point(84,72),true);
            yield return Capture("04-flat-selection.png");
            board.Zoom=2;board.Pan=new Vector2(0,-12);yield return null;yield return null;
            yield return Capture("04-selection-ramps.png");
            board.Zoom=4;board.Pan=new Vector2(-47,-40);
            board.DrawSelection(new Point(8,18),new Point(26,30),true);yield return null;yield return null;
            yield return Capture("04-selection-ground.png");
            board.Fit();board.DrawSelection(new Point(44,32),new Point(84,72),true);yield return null;yield return null;
            var cameraPosition=quest.Camera.transform.position;var cameraRotation=quest.Camera.transform.rotation;
            bool orbitHit=true;
            for(int side=0;side<4;side++)
            {
                float angle=side*Mathf.PI*.5f;
                quest.Camera.transform.position=boardPosition+new Vector3(Mathf.Sin(angle)*1.65f,1.25f,-Mathf.Cos(angle)*1.65f);
                quest.Camera.transform.LookAt(boardPosition+Vector3.up*.65f);
                yield return null;yield return null;
                Physics.SyncTransforms();
                var mapPoint=new Point(22,18);
                var worldPoint=board.Content.TransformPoint(new Vector3(mapPoint.x,board.HeightAt(mapPoint),mapPoint.y));
                var pickRay=new Ray(quest.Camera.transform.position,(worldPoint-quest.Camera.transform.position).normalized);
                orbitHit &= board.RayToMap(pickRay,out var hit)&&hit.Distance(mapPoint)<.1f;
                orbitHit &= Vector3.Distance(boardPosition,board.transform.position)<.001f;
                yield return Capture($"05-orbit-{side}.png");
            }
            quest.Camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
            board.DrawSelection(default,default,false);
            if(!menuMoved||!menuAnchored||!orbitHit)Debug.LogError($"Visual interaction regression: move={menuMoved}, anchor={menuAnchored}, orbit={orbitHit}");
            game.Select(worker.id,false);
            bool moved=game.Send(CommandKind.Move,position:new Point(30,24));
            if(!moved)Debug.LogError("Movement command rejected: "+game.Message);
            Point beforeMove=game.View.entities.First(e=>e.id==worker.id).position;
            float moveDeadline=Time.realtimeSinceStartup+12;
            bool travelled=false;
            while(Time.realtimeSinceStartup<moveDeadline&&!travelled)
            {
                yield return null;
                travelled=game.View.entities.First(e=>e.id==worker.id).position.Distance(beforeMove)>1;
            }
            if(!travelled)Debug.LogError($"Worker did not travel from {beforeMove.x},{beforeMove.y}; {game.Message}");
            bool invariants=game.Match.Players.All(p=>p.minerals>=0&&p.catalyst>=0)&&game.Match.Tick>0;
            File.WriteAllText(Path.Combine(output,"smoke-result.json"),"{\"errors\":"+errors+",\"tick\":"+game.Match.Tick+",\"moveAccepted\":"+moved.ToString().ToLowerInvariant()+",\"travelled\":"+travelled.ToString().ToLowerInvariant()+",\"invariants\":"+invariants.ToString().ToLowerInvariant()+",\"menuMoved\":"+menuMoved.ToString().ToLowerInvariant()+",\"menuAnchored\":"+menuAnchored.ToString().ToLowerInvariant()+",\"orbitPicking\":"+orbitHit.ToString().ToLowerInvariant()+"}");
            Debug.Log("FRONTIER_DESKTOP_SMOKE "+errors+" errors; tick "+game.Match.Tick);
            Application.logMessageReceived-=OnLog;Application.Quit(errors==0&&moved&&travelled&&invariants?0:1);
        }
        private void OnDestroy()
        {
            if(hadPanelPlacement)PlayerPrefs.SetString(PanelKey,previousPanelPlacement);else PlayerPrefs.DeleteKey(PanelKey);
            PlayerPrefs.Save();
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
