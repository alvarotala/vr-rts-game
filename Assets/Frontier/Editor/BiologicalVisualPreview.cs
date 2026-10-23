using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class BiologicalVisualPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        [MenuItem("Frontier/Preview biological faction and creep")]
        public static void Render()
        {
            const string output="TestResults/Biological";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Biological session");var boardObject=new GameObject("Biological board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                session.Faction="biological";session.EnemyFaction="industrial";session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var visuals=(Dictionary<int,Transform>)typeof(BoardView).GetField("entities",Private).GetValue(board);
                var actors=new List<VisibleEntity>();
                string[] roles={"hq","barracks","extractor","supply","tech","defense","academy","research"};
                for(int i=0;i<roles.Length;i++)
                {
                    var type=session.Catalog.Role("biological",roles[i]);
                    actors.Add(new VisibleEntity{id=100+i,owner=0,definition=type.id,position=new Point(12+i%4*10,18+i/4*12),health=type.health,complete=true});
                }
                session.View.entities=actors.ToArray();session.View.resources=Array.Empty<ResourceState>();session.View.attacks=Array.Empty<AttackEvent>();Array.Fill(session.View.visibility,(byte)2);session.View.tick=0;
                CameraAt(new Point(27,25),19);Refresh();Capture("living-buildings");
                foreach(var entity in actors)
                {
                    var type=session.Catalog[entity.definition];var model=visuals[entity.id].Find("Building silhouette");var mesh=model.GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh.bounds.min.y>=-.001f&&mesh.bounds.min.x>=-type.footprint*.5f&&mesh.bounds.max.x<=type.footprint*.5f&&mesh.bounds.min.z>=-type.footprint*.5f&&mesh.bounds.max.z<=type.footprint*.5f,"Living building stays inside footprint: "+type.role);
                    Check(visuals[entity.id].Find("Health").localPosition.y>mesh.bounds.max.y*1.018f,"Breathing cannot cover health: "+type.role);
                }
                var creep=(CreepVisuals)typeof(BoardView).GetField("creepVisuals",Private).GetValue(board);
                int start=Coverage();int simulationTick=session.Match.Tick,minerals=session.Match.Players[0].minerals,entities=session.Match.Entities.Count;
                int colliders=board.Content.GetComponentsInChildren<Collider>().Length;
                for(int frame=0;frame<48;frame++){session.View.tick=frame*8;Refresh();Capture("creep-"+frame.ToString("00"));}
                Check(Coverage()>start*2,"Creep visibly spreads over time around living structures");
                Check(session.Match.Tick==simulationTick&&session.Match.Players[0].minerals==minerals&&session.Match.Entities.Count==entities,"Visual spread never changes simulation time, economy or entities");
                Check(board.Content.GetComponentsInChildren<Collider>().Length==colliders,"Creep adds no collision surfaces");
                CameraAt(new Point(16,20),6);Capture("hive-closeup");CameraAt(new Point(27,25),19);
                var frozen=creep.Texture.GetPixels32();session.Pause("creep-preview",true);Refresh();Refresh();
                Check(frozen.SequenceEqual(creep.Texture.GetPixels32()),"Pause freezes coverage");session.Pause("creep-preview",false);
                foreach(var actor in actors)actor.remembered=true;Array.Fill(session.View.visibility,(byte)1);session.View.tick+=400;Refresh();
                Check(frozen.SequenceEqual(creep.Texture.GetPixels32()),"Fog remembers exact last observed coverage");Capture("remembered-creep");
                session.View.entities=Array.Empty<VisibleEntity>();session.View.tick+=400;Refresh();
                Check(frozen.SequenceEqual(creep.Texture.GetPixels32()),"Unseen destruction cannot reveal itself through creep");
                Array.Fill(session.View.visibility,(byte)2);session.View.tick+=4;Refresh();int beforeDecay=Coverage();
                session.View.tick+=200;Refresh();Check(Coverage()<beforeDecay,"Observed destruction makes creep recede");
                session.View.tick+=200;Refresh();Check(Coverage()==0,"Abandoned visible creep eventually disappears");
                session.LeaveMatch();session.StartMatch();creep=(CreepVisuals)typeof(BoardView).GetField("creepVisuals",Private).GetValue(board);
                session.View.entities=new[]{new VisibleEntity{id=501,owner=0,definition=session.Catalog.Role("industrial","hq").id,position=new Point(18,18),health=1000,complete=true}};
                session.View.resources=Array.Empty<ResourceState>();Array.Fill(session.View.visibility,(byte)2);session.View.tick=0;Refresh();session.View.tick=400;Refresh();Check(Coverage()==0,"Industrial buildings produce no creep");
                var hidden=new VisibleEntity{id=502,owner=1,definition=session.Catalog.Role("biological","hq").id,position=new Point(28,28),complete=true,remembered=true};
                session.View.entities=new[]{hidden};session.View.tick+=4;Refresh();Check(Coverage()==0,"A newly loaded memory cannot create unseen creep");
                hidden.remembered=false;Array.Fill(session.View.visibility,(byte)0);session.View.tick+=200;Refresh();Check(Coverage()==0,"Unexplored cells never receive creep knowledge");
                Array.Fill(session.View.visibility,(byte)2);hidden.owner=0;session.View.tick+=4;Refresh();Check(Coverage()>0,"Revealing an observed source reveals its creep");
                hidden.complete=false;hidden.progress=.4f;session.View.tick+=4;Refresh();
                Check(visuals[hidden.id].Find("Building silhouette").GetComponent<Renderer>().sharedMaterials.All(m=>m.GetFloat("_Desaturate")>.9f),"Construction desaturates tissue as well as team paint");
                CameraAt(new Point(30,30),6);Capture("organic-construction");
                // Actual runtime rigs: six legs (eight on artillery), with real
                // movement-driven gait and no motion while standing or paused.
                session.LeaveMatch();session.StartMatch();actors.Clear();
                string[] units={"worker","scout","melee","ranged","siege","caster"};
                for(int i=0;i<units.Length;i++)
                {
                    var type=session.Catalog.Role("biological",units[i]);actors.Add(new VisibleEntity{id=600+i,owner=0,definition=type.id,position=new Point(18+i*4,24),health=type.health,complete=true,order=CommandKind.Stop});
                }
                session.View.entities=actors.ToArray();session.View.resources=Array.Empty<ResourceState>();Array.Fill(session.View.visibility,(byte)2);session.View.tick=0;CameraAt(new Point(28,24),13);Refresh();Capture("creatures");
                for(int i=1;i<actors.Count;i++)Check(visuals[actors[i].id].GetComponentsInChildren<Transform>().Count(t=>t.name.Contains("stride"))==(units[i]=="siege"?8:6),"Articulated insect limbs: "+units[i]);
                var scoutLeg=visuals[601].Find("Left stride");var rest=scoutLeg.localRotation;
                session.View.tick=4;actors[1].position=new Point(22.3f,24);Refresh();Check(Quaternion.Angle(rest,scoutLeg.localRotation)>1,"Skitter gait follows actual displacement");
                session.View.tick=8;Refresh();Check(Quaternion.Angle(rest,scoutLeg.localRotation)<.001f,"Standing insect settles its legs");
                Capture("creatures-walking");
                Debug.Log($"FRONTIER_BIOLOGICAL_VISUALS passed: {checks} checks, {captures} views; organic meshes, articulated limbs, growing/retreating creep, visibility memory, pause, construction and no simulation changes");
                int Coverage()=>creep.Texture.GetPixels32().Count(p=>p.r>128);
                void Refresh()=>Invoke(board,"LateUpdate");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("BIOLOGICAL_PASS: "+message);}
                void CameraAt(Point point,float radius)
                {
                    Refresh();var center=board.Content.TransformPoint(new Vector3(point.x,1,point.y));
                    preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*radius;
                    preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+new Vector3(.06f,.55f,-.6f);preview.camera.transform.LookAt(center);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                }
                void Capture(string name)
                {
                    preview.BeginStaticPreview(new Rect(0,0,1400,1000));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
        public static void Validate()
        {
            Render();SoldierVisualPreview.Render();WorkerVisualPreview.Render();ConstructionPreview.Render();
        }
    }
}
