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
    public static class ResourcePickingPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        [MenuItem("Frontier/Preview mineral targeting")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/ResourcePicking");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Resource picking session");var boardObject=new GameObject("Resource picking board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;
                var send=typeof(CommanderInput).GetMethod("PointBoard",Private);
                foreach(string map in new[]{"rift","training"})
                {
                    session.LoadMap(map);session.StartMatch();board.Width=1.2f;board.Fit();board.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    var patch=session.Match.Resources.First(r=>r.kind=="mineral");
                    var workers=session.Match.Entities.Values.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").ToArray();
                    for(int i=0;i<workers.Length;i++)workers[i].position=new Point(5+i,5);
                    var worker=workers[0];worker.position=new Point(patch.position.x+.6f,patch.position.y+1.25f);
                    Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                    Check(board.EntityAt(patch.position)==worker.id,"Regression fixture reproduces worker selection halo over the mineral");
                    Send(Top(patch.position));
                    Check(board.Hovered==0&&board.HoveredResource==patch.id,"Pointing at a crystal resolves the patch instead of the nearby worker");
                    Invoke(board,"LateUpdate");Check(HoverAmount(patch.id)>0&&Ground().GetFloat("_HoverWidth")>0,"Mineral hover lights its model and paints a ground outline");Capture(map+"-hover",patch.position);
                    Send(Top(patch.position),down:true);Send(Top(new Point(patch.position.x+1.3f,patch.position.y)),up:true);
                    Check(session.SelectedResource==patch.id&&session.Selection.Count==0,"Small hand movement retains the pressed patch instead of selecting a neighbor or area");
                    var near=new Point(patch.position.x,patch.position.y-2.3f);
                    Check(board.ResourceAt(near)==patch.id,"Expanded mineral target accepts a near miss outside the old two-cell radius");
                    session.Select(worker.id,false);Send(Top(near),order:true);
                    Check(worker.order==CommandKind.Gather&&session.Match.Resources.Any(r=>r.id==worker.resourceId&&r.kind=="mineral"),"A near-miss order gathers instead of merely walking next to minerals");
                    Send(Top(worker.position));Check(board.Hovered==worker.id&&board.HoveredResource==0,"Direct worker aim keeps priority over mineral assistance");
                    Send(Top(worker.position),down:true);Send(Top(worker.position),up:true);Check(session.Selection.SetEquals(new[]{worker.id}),"Workers beside minerals remain selectable");

                    // Isolate one patch for shallow-angle rays from all four sides.
                    foreach(var other in session.Match.Resources.Where(r=>r.id!=patch.id))other.remaining=0;
                    worker.position=new Point(5,5);Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                    foreach(float width in new[]{.6f,1.2f,2f})
                    {
                        board.Width=width;board.transform.SetPositionAndRotation(new Vector3(.3f,.7f,-.2f),Quaternion.Euler(0,37,0));Invoke(board,"LateUpdate");
                        for(int side=0;side<4;side++)
                        {
                            var center=board.Content.TransformPoint(new Vector3(patch.position.x,board.HeightAt(patch.position)+.85f,patch.position.y));
                            var direction=Quaternion.AngleAxis(side*90,board.transform.up)*board.transform.forward;
                            var origin=center+board.transform.up*.08f+direction*.4f;var ray=new Ray(origin,center-origin);
                            Check(board.RayToMap(ray,out var projected)&&projected.Distance(patch.position)>3.5f,"Oblique ray projects well beyond the mineral's old ground target");
                            Send(ray);Check(board.HoveredResource==patch.id&&board.Hovered==0,"Crystal volume can be targeted from side "+side+" at board width "+width);
                            if(width==1.2f&&side==0){Invoke(board,"LateUpdate");Capture(map+"-oblique",patch.position);}
                            if(width==.6f&&side==0){Invoke(board,"LateUpdate");Capture(map+"-compact",patch.position);}
                        }
                    }
                    board.Width=.6f;Invoke(board,"LateUpdate");Send(Top(patch.position),down:true);
                    Send(Top(new Point(patch.position.x+Mathf.Min(1.8f,.012f/board.CellScale),patch.position.y)),up:true);
                    Check(session.SelectedResource==patch.id,"Small physical tremor on a compact board is not mistaken for a selection drag");
                    Send(Top(patch.position));Invoke(input,"ClearPointer");Invoke(board,"LateUpdate");
                    Check(board.HoveredResource==0&&HoverAmount(patch.id)==0&&Ground().GetFloat("_HoverWidth")==0,"Leaving the target removes resource hover and outline");
                    Send(Top(patch.position));session.Pause("tracking",true);Invoke(board,"LateUpdate");
                    Check(HoverAmount(patch.id)==0&&Ground().GetFloat("_HoverWidth")==0,"Tracking pause suppresses mineral feedback");session.Pause("tracking",false);
                    int cell=(int)patch.position.y*session.Map.width+(int)patch.position.x;byte vision=session.View.visibility[cell];
                    session.View.visibility[cell]=0;Send(Top(patch.position));
                    Check(board.HoveredResource==0&&board.ResourceAt(patch.position)==0,"Unexplored deposits are not targetable");session.View.visibility[cell]=vision;
                    patch.remaining=0;Refresh();Send(Top(patch.position));
                    Check(board.HoveredResource==0&&board.ResourceAt(patch.position)==0,"Depleted deposits are not targetable");patch.remaining=1800;Refresh();
                    board.Zoom=4;board.Pan=new Vector2(patch.position.x+session.Map.width*.125f+1-session.Map.width*.5f,patch.position.y-session.Map.height*.5f);Invoke(board,"LateUpdate");
                    Check(board.ResourceAt(new Point(patch.position.x+2,patch.position.y))==0,"Assistance cannot select a resource clipped outside the board");
                    board.Fit();Invoke(board,"LateUpdate");Send(Top(patch.position));board.SetVisible(false);
                    Check(board.HoveredResource==0,"Hiding the board clears the resource target");board.SetVisible(true);
                    session.LeaveMatch();Check(board.HoveredResource==0,"Leaving the match clears the resource target");

                    Ray Top(Point point)
                    {
                        var world=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point),point.y));
                        return new Ray(world+board.transform.up*.4f,-board.transform.up);
                    }
                    void Refresh()=>typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                }
                Debug.Log($"FRONTIER_RESOURCE_PICKING passed: {checks} checks, {captures} views; no player build");
                void Send(Ray ray,bool down=false,bool up=false,bool order=false)=>send.Invoke(input,new object[]{ray,down,up,order,false});
                Material Ground()=>board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                float HoverAmount(int id)
                {
                    var renderers=(System.Collections.Generic.Dictionary<int,Renderer>)typeof(BoardView).GetField("resourceRenderers",Private).GetValue(board);
                    var block=new MaterialPropertyBlock();renderers[id].GetPropertyBlock(block);return block.GetFloat("_HoverAmount");
                }
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("RESOURCE_PICK_PASS: "+message);}
                void Capture(string name,Point point)
                {
                    var center=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point)+.5f,point.y));
                    float size=board.CellScale*4;
                    preview.camera.orthographic=true;preview.camera.orthographicSize=size;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+board.transform.up*size*2-board.transform.forward*size*2.4f;preview.camera.transform.LookAt(center,board.transform.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes("TestResults/ResourcePicking/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
