using System;
using System.IO;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Exercise terrain raycasts and render the runtime marker without building a player.
    public static class PointerPreview
    {
        [MenuItem("Frontier/Preview map pointer")]
        public static void Render()
        {
            const string output="TestResults/Pointer";
            var preview=new PreviewRenderUtility();
            var sessionObject=new GameObject("Pointer preview session");
            var boardObject=new GameObject("Pointer preview board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            Material laserMaterial=null;
            void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);}
            try
            {
                Directory.CreateDirectory(output);
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var input=boardObject.AddComponent<CommanderInput>();input.Board=board;
                var ground=board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                // Reveal terrain for the surface examples only; a separate capture uses real fog.
                Array.Fill(session.View.visibility,(byte)2);session.View.tick+=4;
                Invoke(board,"LateUpdate");preview.AddSingleGO(boardObject);
                var laserObject=new GameObject("Example input ray");laserObject.transform.SetParent(boardObject.transform,false);
                var laser=laserObject.AddComponent<LineRenderer>();laser.positionCount=2;laser.widthMultiplier=.0012f;
                laserMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laserMaterial.color=new Color(.3f,.95f,.94f);laser.sharedMaterial=laserMaterial;
                preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);

                Capture("flat",new Point(24.3f,18.2f),0);
                Capture("ramp",new Point(51.2f,40.3f),.45f);
                Capture("height",new Point(58.3f,40.3f),1.5f);
                board.DrawSelection(new Point(21,16),new Point(24.3f,18.2f),true);
                Capture("selection",new Point(24.3f,18.2f),0);board.DrawSelection(default,default,false);
                Capture("edge",new Point(.15f,40.3f),0);

                // Change zoom, pan and room pose immediately before the raycast, as input does.
                board.transform.SetPositionAndRotation(new Vector3(.4f,.7f,-.3f),Quaternion.Euler(0,55,0));
                board.Navigate(new Vector2(.2f,0),Mathf.Log(4),1);
                Capture("zoom-rotated",new Point(68.2f,64.3f),1.5f);
                Check(Mathf.Abs(ground.GetVector("_PointerArea").z*board.CellScale-.0018f)<.00001f,"Pointer size changed with zoom");

                board.Fit();board.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                Array.Fill(session.View.visibility,(byte)0);session.View.tick+=4;
                Capture("fog",new Point(85.3f,62.2f),0);
                Invoke(input,"ClearPointer");Check(ground.GetVector("_PointerArea").w==0,"Pointer remains after input clears it");
                laser.enabled=false;Save("cleared");
                board.Pointer=new Point(85.3f,62.2f);session.Pause("tracking",true);Invoke(board,"LateUpdate");
                Check(ground.GetVector("_PointerArea").w==0,"Pointer remains while tracking is paused");session.Pause("tracking",false);
                board.SetVisible(false);Check(board.Pointer==null,"Hidden board retains a pointer");board.SetVisible(true);
                Check(!board.RayToMap(new Ray(new Vector3(2,1,2),Vector3.down),out _),"Ray outside board accepted");
                // An intersection on a vertical cliff must not move the marker onto its top.
                var cliff=board.Content.TransformPoint(new Vector3(51.5f,.5f,32.5f));
                Check(!board.RayToMap(new Ray(cliff,board.transform.right),out _),"Cliff face treated as top surface");
                session.LeaveMatch();Check(board.Pointer==null&&ground.GetVector("_PointerArea").w==0,"Pointer remains after leaving match");
                Debug.Log("FRONTIER_POINTER_PREVIEW passed: exact hits on ground/ramp/height, zoom/pan/rotation, clipping, fog, selection, clear/pause/exit; 8 views rendered; no player build");

                void Capture(string name,Point point,float height)
                {
                    // Compute the expected world point independently of the transform awaiting LateUpdate.
                    var local=new Vector3((point.x-session.Map.width*.5f-board.Pan.x)*board.CellScale,height*board.CellScale,(point.y-session.Map.height*.5f-board.Pan.y)*board.CellScale);
                    var expected=board.transform.TransformPoint(local);
                    var origin=expected+board.transform.up*.25f-board.transform.forward*.15f;
                    Check(board.RayToMap(new Ray(origin,expected-origin),out var hit,out var world),name+": no terrain hit");
                    Check(hit.Distance(point)<.001f&&Vector3.Distance(world,expected)<.00001f,name+": laser endpoint differs from actual surface");
                    board.Pointer=hit;Invoke(board,"LateUpdate");
                    Check(ground.GetVector("_PointerArea").w>0,name+": pointer not drawn");
                    laser.SetPosition(0,world+(origin-world).normalized*.09f);laser.SetPosition(1,world);
                    preview.camera.orthographicSize=.055f;preview.camera.transform.position=world+board.transform.up*.14f-board.transform.forward*.11f;
                    preview.camera.transform.LookAt(world,board.transform.up);Save(name);
                }
                void Save(string name)
                {
                    preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(sessionObject);if(laserMaterial!=null)Object.DestroyImmediate(laserMaterial);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
