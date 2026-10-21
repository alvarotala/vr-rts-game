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
    public static class DesertMapPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Validate and preview Desser Alert")]
        public static void Render()
        {
            const string output="TestResults/DesertMap";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Desert map session");var boardObject=new GameObject("Desert map board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("desert");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;board.Width=1.6f;
                Invoke(board,"Start");preview.AddSingleGO(boardObject);Refresh();
                var grid=new GridMap(session.Map);var ground=Field<Material>(board,"ground");
                Check(session.Map.theme=="desert"&&session.Map.width==160&&session.Map.height==160,"Desert resource loads with theme and larger bounds");
                Check(ground.GetFloat("_Desert")>.5f,"Runtime terrain activates desert sand shading");
                Check(Math.Abs(board.HeightAt(new Point(48.5f,112.5f))-3)<.001f&&Math.Abs(board.HeightAt(new Point(20.5f,20.5f)))<.001f,
                    "Desert mesas rise above the starting low ground");
                foreach(var ramp in session.Map.terrain.Where(t=>t.ramp))
                {
                    var center=new Point(ramp.x+ramp.width*.5f,ramp.y+ramp.height*.5f);
                    Check(Math.Abs(board.HeightAt(center)-1.5f)<.001f,"Continuous half-height at ramp "+ramp.x+","+ramp.y);
                    var origin=board.Content.TransformPoint(new Vector3(center.x,40,center.y));
                    Check(board.RayToMap(new Ray(origin,-board.transform.up),out var point,out var hit)
                        &&point.Distance(center)<.01f&&Math.Abs(board.Content.InverseTransformPoint(hit).y-board.HeightAt(center))<.01f,
                        "Pointer collision agrees with visible ramp surface "+ramp.x+","+ramp.y);
                    bool seamless=true;
                    int lanes=ramp.rampAxis=="x"?ramp.height:ramp.width;
                    for(int lane=0;lane<lanes;lane++)foreach(bool high in new[]{false,true})
                    {
                        bool end=high==(ramp.rampDirection>0);
                        float edge=ramp.rampAxis=="x"?ramp.x+(end?ramp.width:0):ramp.y+(end?ramp.height:0);
                        float inside=edge+(end?-.001f:.001f),outside=edge+(end?.001f:-.001f);
                        Point At(float axis)=>ramp.rampAxis=="x"?new Point(axis,ramp.y+lane+.5f):new Point(ramp.x+lane+.5f,axis);
                        seamless&=Math.Abs(board.HeightAt(At(inside))-board.HeightAt(At(outside)))<.002f;
                    }
                    Check(seamless,"Both ramp ends meet terrain continuously across all twelve lanes "+ramp.x+","+ramp.y);
                }
                CameraAt(new Point(80,80),88,true);Capture("terrain-top",1600,1600);
                CameraAt(new Point(80,80),75,false);Capture("terrain-angle",1600,1200);

                session.StartMatch();int tick=session.Match.Tick;int minerals=session.Match.Players[0].minerals;int entities=session.Match.Entities.Count;
                // An editor-only survey view exposes the authored map for visual
                // review. Runtime fog and the simulation's player views are intact.
                var survey=new PlayerView{player=0,faction=session.Faction,visibility=Enumerable.Repeat((byte)2,grid.Count).ToArray(),
                    entities=session.Match.Entities.Values.Select(e=>new VisibleEntity{id=e.id,owner=e.owner,definition=e.definition,position=e.position,health=e.health,complete=e.complete}).ToArray(),
                    resources=session.Match.Resources.Select(r=>new ResourceState{id=r.id,definition=r.definition,kind=r.kind,remaining=r.remaining,position=r.position}).ToArray()};
                SetView(survey);
                Check(Field<Dictionary<int,Transform>>(board,"resources").Count==64,"Survey renders all 48 minerals and 16 gas deposits");
                CameraAt(new Point(80,80),88,true);Capture("survey-top",1600,1600);
                CameraAt(new Point(80,80),75,false);Capture("survey-angle",1600,1200);
                CameraAt(new Point(57,105),27,false);Capture("mesa-expansion",1400,1100);
                CameraAt(new Point(80,80),30,false);Capture("central-mesa",1400,1100);
                CameraAt(new Point(80,54),18,false);Capture("north-passage",1400,1100);
                CameraAt(new Point(67,97),18,false);Capture("corner-passage",1400,1100);
                CameraAt(new Point(88,32),18,false);Capture("outer-passage",1400,1100);
                Check(session.Match.Tick==tick&&session.Match.Players[0].minerals==minerals&&session.Match.Entities.Count==entities,
                    "Map surveying leaves authoritative time, economy and entities unchanged");

                foreach(int player in new[]{0,1})
                {
                    SetView(session.Match.View(player));
                    Check(session.View.entities.All(e=>e.owner==player),"Actual starting fog hides the opponent for player"+player);
                    var spawn=session.Map.spawns.Single(s=>s.player==player);
                    CameraAt(new Point(spawn.x+2,spawn.y+2),15,false,player==0?1:-1);Capture("start-player"+player,1400,1100);
                }
                var mesh=Field<Transform>(board,"terrainRoot").GetComponent<MeshFilter>().sharedMesh;
                Debug.Log($"FRONTIER_DESERT_MAP passed: {checks} checks, {captures} views; terrain {mesh.vertexCount} vertices/{mesh.triangles.Length/3} triangles; renders in {output}");

                void SetView(PlayerView view){typeof(GameSession).GetProperty("View").SetValue(session,view);board.Rebuild();Refresh();}
                void Refresh()=>Invoke(board,"LateUpdate");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("DESERT_MAP_PASS: "+message);}
                void CameraAt(Point point,float radius,bool top,int facing=1)
                {
                    Refresh();var center=board.Content.TransformPoint(new Vector3(point.x,0,point.y));
                    preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*radius;
                    preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+(top?new Vector3(0,1.4f,-.001f*facing):new Vector3(.12f*facing,.95f,-1.1f*facing));
                    preview.camera.transform.LookAt(center,top?Vector3.forward*facing:Vector3.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.045f,.032f,.025f);
                    Refresh();
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
