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
    public static class ResourceLayoutPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        [MenuItem("Frontier/Preview concentric resource camps")]
        public static void Render()
        {
            const string output="TestResults/ResourceLayout";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Resource layout session");var boardObject=new GameObject("Resource layout board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                foreach(string map in new[]{"rift","training"})
                {
                    session.LoadMap(map);session.StartMatch();
                    for(int player=0;player<2;player++)
                    {
                        Show(player);Capture(map+"-player"+player+"-top",player,true);Capture(map+"-player"+player+"-angled",player,false);
                        var hq=session.Match.Entities.Values.Single(e=>e.owner==player&&session.Catalog[e.definition].role=="hq");
                        var center=session.Match.Center(hq);var gas=session.View.resources.Single(r=>r.definition=="camp"+player+".catalyst");
                        if(Math.Abs(gas.position.y-center.y)>=1||Math.Abs(gas.position.x-center.x)<6)throw new Exception("Gas must remain beside HQ");
                    }
                    // Place extractors using the same command path as the player,
                    // then capture mining and building access in the real match.
                    for(int player=0;player<2;player++)
                    {
                        var worker=session.Match.Entities.Values.First(e=>e.owner==player&&session.Catalog[e.definition].role=="worker");
                        var gas=session.Match.Resources.Single(r=>r.definition=="camp"+player+".catalyst");var type=session.Catalog.Role(session.Match.Players[player].faction,"extractor");
                        var result=session.Match.Submit(new GameCommand{player=player,kind=CommandKind.Build,entities=new[]{worker.id},definition=type.id,position=new Point(gas.position.x-type.footprint*.5f,gas.position.y-type.footprint*.5f)});
                        if(!result.accepted)throw new Exception("Lateral extractor rejected");
                    }
                    for(int tick=0;tick<1000;tick++)session.Match.Step();
                    if(session.Match.Entities.Values.Count(e=>session.Catalog[e.definition].role=="extractor"&&e.complete)!=2)throw new Exception("Both lateral extractors must finish");
                    for(int player=0;player<2;player++){Show(player);Capture(map+"-player"+player+"-extractor",player,false);}
                    session.LeaveMatch();
                }
                Debug.Log($"FRONTIER_RESOURCE_LAYOUT passed: {captures} views; both maps, both players, top/angled view and real extractor construction");
                void Show(int player)
                {
                    // Each player's view needs its own fog/creep memory even when
                    // both snapshots have the same simulation tick.
                    typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(player));board.Rebuild();Invoke(board,"LateUpdate");
                }
                void Capture(string name,int player,bool top)
                {
                    var spawn=session.Map.spawns.Single(s=>s.player==player);int facing=player==0?1:-1;
                    var center=board.Content.TransformPoint(new Vector3(spawn.x+2,0,spawn.y+2+facing*2));
                    preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*10;
                    preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+(top?new Vector3(0,.6f,-.001f*facing):new Vector3(0,.55f,-.62f*facing));
                    preview.camera.transform.LookAt(center,top?Vector3.forward*facing:Vector3.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    Invoke(board,"LateUpdate");preview.BeginStaticPreview(new Rect(0,0,1200,1000));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                }
            }
            finally{Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);}
        }
        public static void Validate(){Render();ResourceVisualPreview.Render();HarvestingMarkersPreview.Render();HarvestingPreview.Render();WorkerVisualPreview.Render();}
    }
}
