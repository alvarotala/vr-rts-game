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
    public static class RefineryPlacementPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Validate gas construction snap")]
        public static void Run()
        {
            const string output="TestResults/RefineryPlacement";Directory.CreateDirectory(output);
            var fixture=new GameObject("Refinery placement fixture");var boardObject=new GameObject("Refinery placement board");
            var preview=new PreviewRenderUtility();int checks=0,captures=0;
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            float density=Shader.GetGlobalFloat("_FrontierFogDensity"),lighting=Shader.GetGlobalFloat("_FrontierEnhancedLighting");
            var lightColor=Shader.GetGlobalColor("_FrontierLightColor");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Call(board,"Start");preview.AddSingleGO(boardObject);
                foreach(string map in new[]{"training","rift","desert"})foreach(string faction in new[]{"industrial","biological"})
                {
                    session.LoadMap(map);session.Faction=faction;session.StartMatch();
                    var type=session.Catalog.Role(faction,"extractor");var gas=session.View.resources.First(r=>r.kind=="catalyst");
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    session.Select(worker.id,false);session.ChooseBuild(type.id);
                    var expected=new Point(gas.position.x-type.footprint*.5f,gas.position.y-type.footprint*.5f);
                    foreach(var offset in new[]{new Point(0,0),new Point(-1.2f,-1.2f),new Point(-1.2f,1.2f),new Point(1.2f,-1.2f),new Point(1.2f,1.2f),new Point(2.4f,0)})
                    {
                        var aim=new Point(gas.position.x+offset.x,gas.position.y+offset.y);
                        Check(Same(session.BuildPlacement(aim),expected),"Nearby aim centers on gas: "+map+" / "+faction);
                        board.Pointer=aim;board.HoveredResource=0;Call(board,"LateUpdate");
                        var ghost=Field<Transform>(board,"preview");
                        Check(Mathf.Abs(ghost.localPosition.x-gas.position.x)<.001f&&Mathf.Abs(ghost.localPosition.z-gas.position.y)<.001f,
                            "Placement ghost is centered on the deposit from every direction");
                        Check(ghost.GetComponent<Renderer>().sharedMaterial==Field<Material>(board,"highlight"),"Centered gas pad previews as valid");
                    }
                    var far=new Point(gas.position.x+5.2f,gas.position.y+4.7f);
                    Check(Same(session.BuildPlacement(far),Floor(far)),"Snap releases when the pointer leaves the deposit");
                    Check(!session.CanPreviewBuild(Floor(far),type.footprint),"Extractor without gas previews as invalid");
                    Check(Same(session.BuildPlacement(far,gas.id),expected),"Direct gas model hits center even when the terrain intersection is farther away");
                    board.Pointer=far;board.HoveredResource=gas.id;Call(board,"LateUpdate");
                    Check(Mathf.Abs(Field<Transform>(board,"preview").localPosition.x-gas.position.x)<.001f,"Direct-hit ghost uses the same snap as confirmation");
                    Capture(map+"-"+faction);
                    var observed=session.View.resources;
                    session.View.resources=observed.Where(r=>r.id!=gas.id).ToArray();
                    Check(Same(session.BuildPlacement(gas.position,gas.id),Floor(gas.position)),"Unobserved gas cannot attract the placement from authoritative data");
                    session.View.resources=observed;int remaining=gas.remaining;gas.remaining=0;
                    Check(Same(session.BuildPlacement(gas.position,gas.id),Floor(gas.position)),"Depleted gas does not attract placement");gas.remaining=remaining;
                    session.ChooseBuild(session.Catalog.Role(faction,"supply").id);
                    Check(Same(session.BuildPlacement(gas.position,gas.id),Floor(gas.position)),"Other buildings retain ordinary grid placement");
                    session.ChooseBuild(type.id);int minerals=session.Match.Players[0].minerals;
                    Check(session.Context(far,0,gas.id)>0,"Direct hit confirms a real centered extractor: "+session.Message);
                    var building=session.Match.Entities.Values.Single(e=>e.owner==0&&e.definition==type.id);
                    Check(Same(building.position,expected)&&session.Match.Players[0].minerals==minerals-type.minerals,"Confirmed foundation matches the ghost and charges once");
                    Call(session,"RefreshView");session.ChooseBuild(type.id);
                    Check(!session.CanPreviewBuild(expected,type.footprint),"Occupied gas stays invalid instead of snapping elsewhere");
                    minerals=session.Match.Players[0].minerals;
                    Check(session.Context(gas.position,0,gas.id)==0&&session.Match.Players[0].minerals==minerals,"Occupied deposit cannot create or pay for another refinery");

                    session.StartMatch();worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    session.Select(worker.id,false);
                    Check(session.Send(CommandKind.Move,position:new Point(gas.position.x+4,gas.position.y)),"Worker starts a movement before queued construction");
                    session.ChooseBuild(type.id);
                    Check(session.Context(new Point(gas.position.x-1.2f,gas.position.y+1.2f),0,0,append:true)>0,"Queued construction accepts nearby aim");
                    Check(worker.pendingOrders.Any(o=>o.kind==CommandKind.Build&&Same(o.position,expected)),"Queued order stores the centered origin");
                    for(int tick=0;tick<800&&!session.Match.Entities.Values.Any(e=>e.owner==0&&e.definition==type.id);tick++)session.Match.Step();
                    Check(session.Match.Entities.Values.Any(e=>e.owner==0&&e.definition==type.id&&Same(e.position,expected)),"Queued construction eventually starts at the same centered origin");
                    session.LeaveMatch();

                    void Capture(string name)
                    {
                        var focus=board.Content.TransformPoint(new Vector3(gas.position.x,board.HeightAt(gas.position),gas.position.y));
                        preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*5;
                        preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                        preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                        preview.camera.transform.position=focus+new Vector3(.2f,.6f,-.5f);preview.camera.transform.LookAt(focus);
                        preview.BeginStaticPreview(new Rect(0,0,1000,1000));preview.Render(true);
                        var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                    }
                }
                Debug.Log($"FRONTIER_REFINERY_PLACEMENT passed: {checks} checks, {captures} views; both factions and all maps, ghost/confirmation/queue/privacy; no physical Quest validation");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
                bool Same(Point a,Point b)=>a.Distance(b)<.001f;
                Point Floor(Point p)=>new Point(Mathf.Floor(p.x),Mathf.Floor(p.y));
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
                Shader.SetGlobalFloat("_FrontierFogDensity",density);Shader.SetGlobalFloat("_FrontierEnhancedLighting",lighting);Shader.SetGlobalColor("_FrontierLightColor",lightColor);
            }
        }
    }
}
