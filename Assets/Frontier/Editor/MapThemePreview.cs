using System;
using System.Collections.Generic;
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
    public static class MapThemePreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Validate and preview map themes")]
        public static void Render()
        {
            const string output="TestResults/MapThemes";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Map theme session");var boardObject=new GameObject("Map theme board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;board.Width=1.6f;
                Invoke(board,"Start");preview.AddSingleGO(boardObject);
                foreach(string id in new[]{"training","rift","desert"})
                {
                    session.LoadMap(id);session.StartMatch();Refresh();
                    var grid=new GridMap(session.Map);var ground=Field<Material>(board,"ground");
                    string flag=id=="training"?"_Neon":id=="rift"?"_Forest":"_Desert";
                    Check(ground.GetFloat(flag)==1,id+": terrain selects its own theme");
                    Check(new[]{"_Neon","_Forest","_Desert"}.Where(f=>f!=flag).All(f=>ground.GetFloat(f)==0),id+": map cycling clears other terrain themes");
                    Check(session.View.entities.All(e=>e.owner==0),id+": starting view still hides enemy entities");
                    var visible=Field<Dictionary<int,Transform>>(board,"entities");
                    Check(visible.Keys.All(key=>session.View.entities.Any(e=>e.id==key)),id+": only filtered PlayerView entities enter the scene");
                    var scenery=Field<ThemedScenery>(board,"themedScenery");
                    if(id=="training")Check(scenery==null&&board.Content.Find("Sandstone outcrops")==null,"Neon: circuit markings add no scenery colliders or path obstructions");
                    else
                    {
                        string rootName=id=="rift"?"Forest canopy":"Desert cacti";
                        var root=board.Content.Find(rootName);
                        Check(scenery!=null&&scenery.Count>=(id=="rift"?80:8),id+": recognizable vegetation count = "+scenery?.Count);
                        var mesh=root.GetComponent<MeshFilter>().sharedMesh;var material=root.GetComponent<MeshRenderer>().sharedMaterial;
                        Check(root.GetComponentsInChildren<Renderer>().Length==1&&mesh.subMeshCount==1&&mesh.vertexCount<150000,id+": all vegetation shares one bounded mesh and draw");
                        Check(root.GetComponentsInChildren<Collider>().Length==0,id+": vegetation cannot intercept picking or change navigation");
                        Check(mesh.vertices.All(v=>{int cell=grid.Cell(new Point(v.x,v.z));return cell>=0&&!grid.Walkable[cell];}),id+": every trunk, branch and canopy vertex stays over blocked terrain");
                        Check(material.GetFloat("_UseFog")==1&&material.GetTexture("_FogTex")==Field<Texture2D>(board,"fog"),id+": all vegetation uses current player fog");
                        Check(mesh.uv.Zip(mesh.vertices,(uv,v)=>Mathf.Abs(uv.x-v.x/grid.Width)<.00001f&&Mathf.Abs(uv.y-v.z/grid.Height)<.00001f).All(v=>v),id+": vegetation fog coordinates match their actual positions");
                        board.Rebuild();Refresh();
                        Check(root==null&&mesh==null&&material==null&&board.Content.Cast<Transform>().Count(t=>t.name==rootName)==1,id+": rebuild releases scenery and creates one replacement");
                    }
                    int tick=session.Match.Tick,entityCount=session.Match.Entities.Count;
                    var survey=new PlayerView{player=0,faction=session.Faction,visibility=Enumerable.Repeat((byte)2,grid.Count).ToArray(),
                        entities=session.Match.Entities.Values.Select(e=>new VisibleEntity{id=e.id,owner=e.owner,definition=e.definition,position=e.position,health=e.health,complete=e.complete}).ToArray(),
                        resources=session.Match.Resources.Select(r=>new ResourceState{id=r.id,definition=r.definition,kind=r.kind,remaining=r.remaining,position=r.position}).ToArray()};
                    typeof(GameSession).GetProperty("View").SetValue(session,survey);board.Rebuild();Refresh();
                    Check(Field<Dictionary<int,Transform>>(board,"resources").Count==session.Map.resources.Length,id+": all resource deposits render in editor survey");
                    CameraAt(new Point(grid.Width*.5f,grid.Height*.5f),grid.Width*.55f,true);Capture(id+"-overview",1400,1400);
                    CameraAt(new Point(grid.Width*.5f,grid.Height*.5f),grid.Width*.49f,false);Capture(id+"-angle",1500,1100);
                    Point detail=id=="training"?new Point(24,24):id=="rift"?new Point(14,46):new Point(108,14);
                    CameraAt(detail,id=="training"?25:14,false);Capture(id+"-detail",1200,1000);
                    Check(session.Match.Tick==tick&&session.Match.Entities.Count==entityCount,id+": visual survey does not alter authoritative state");
                    Check(!ShaderUtil.ShaderHasError(ground.shader),id+": board shader compiles and renders without errors");
                    session.LeaveMatch();
                }
                Debug.Log($"FRONTIER_MAP_THEMES passed: {checks} checks, {captures} views; output {output}; no physical Quest performance validation");

                void Refresh()=>Invoke(board,"LateUpdate");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("MAP_THEME_PASS: "+message);}
                void CameraAt(Point point,float radius,bool top)
                {
                    Refresh();var center=board.Content.TransformPoint(new Vector3(point.x,0,point.y));
                    preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*radius;
                    preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+(top?new Vector3(0,1.5f,-.001f):new Vector3(.12f,.95f,-1.1f));
                    preview.camera.transform.LookAt(center,top?Vector3.forward:Vector3.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.018f,.025f,.035f);
                }
                void Capture(string name,int width,int height)
                {
                    preview.BeginStaticPreview(new Rect(0,0,width,height));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
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
