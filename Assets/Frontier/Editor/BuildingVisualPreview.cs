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
    public static class BuildingVisualPreview
    {
        [MenuItem("Frontier/Preview building roles")]
        public static void Render()
        {
            const string output="TestResults/BuildingRoles";const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var preview=new PreviewRenderUtility();var sessionObject=new GameObject("Building preview session");var boardObject=new GameObject("Building preview board");
            var oldBounds=Shader.GetGlobalVector("_FrontierBounds");var oldMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0;
            void Invoke(object o,string name)=>o.GetType().GetMethod(name,flags).Invoke(o,null);
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            try
            {
                Directory.CreateDirectory(output);
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var actors=new List<VisibleEntity>();var labels=new List<Transform>();
                string[] roles={"hq","barracks","extractor","supply","tech","defense","academy"};
                string[] titles={"HEADQUARTERS","BARRACKS","GAS","SUPPLY","TECHNOLOGY","DEFENSE","CASTERS"};
                for(int faction=0;faction<2;faction++)for(int role=0;role<roles.Length;role++)
                {
                    var type=session.Catalog.Role(faction==0?"industrial":"biological",roles[role]);float half=type.footprint*.5f;
                    var center=new Point(6+role*7,16+faction*15);
                    actors.Add(new VisibleEntity{id=1000+actors.Count,owner=0,definition=type.id,position=new Point(center.x-half,center.y-half),health=type.health,complete=true});
                    // Gallery annotations only; these labels are not part of the in-game models.
                    var label=new GameObject("Preview label",typeof(TextMesh));label.transform.SetParent(board.Content,false);
                    label.transform.localPosition=new Vector3(center.x,.05f,center.y-3.6f);
                    var text=label.GetComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=text.font.material;
                    text.text=type.name+"\n"+titles[role];text.fontSize=48;text.characterSize=.12f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;labels.Add(label.transform);
                }
                session.View.entities=actors.ToArray();session.View.resources=Array.Empty<ResourceState>();Array.Fill(session.View.visibility,(byte)2);session.View.tick=100;
                preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                var visuals=(Dictionary<int,Transform>)typeof(BoardView).GetField("entities",flags).GetValue(board);
                Capture("friendly",new Point(27,23),.46f,new Vector3(0,1.3f,-1.7f));
                int vertices=0;
                foreach(var e in actors)
                {
                    var type=session.Catalog[e.definition];var model=Model(e);var mesh=model.GetComponent<MeshFilter>().sharedMesh;vertices+=mesh.vertexCount;
                    Check(mesh.bounds.min.y>=-.001f&&mesh.bounds.extents.x<=type.footprint*.5f&&mesh.bounds.extents.z<=type.footprint*.5f,"Model extends outside footprint: "+e.definition);
                    Check(visuals[e.id].Find("Health").localPosition.y>mesh.bounds.max.y&&visuals[e.id].Find("Damage impact").localPosition.y>mesh.bounds.max.y,"Health/impact hidden inside building: "+e.definition);
                }
                Capture("top",new Point(27,23),.46f,new Vector3(0,2,-.001f));
                Capture("opposite-side",new Point(27,23),.46f,new Vector3(0,1.3f,1.7f));
                Capture("headquarters-and-barracks",new Point(9.5f,16),.15f,new Vector3(0,1.3f,-1.7f));
                Capture("biological-headquarters-and-barracks",new Point(9.5f,31),.15f,new Vector3(0,1.3f,-1.7f));

                var friendlyHull=Model(actors[0]).GetComponent<Renderer>().sharedMaterials[0];
                session.View.entities=Array.Empty<VisibleEntity>();Invoke(board,"LateUpdate");
                foreach(var e in actors)e.owner=1;session.View.entities=actors.ToArray();
                Capture("enemy",new Point(27,23),.46f,new Vector3(0,1.3f,-1.7f));
                Check(Model(actors[0]).GetComponent<Renderer>().sharedMaterials[0]!=friendlyHull,"Enemy hull lost team color");
                session.View.entities=Array.Empty<VisibleEntity>();Invoke(board,"LateUpdate");
                foreach(var e in actors)e.owner=0;session.View.entities=actors.ToArray();Invoke(board,"LateUpdate");
                var normal=Model(actors[0]).GetComponent<Renderer>().sharedMaterials;
                actors[0].remembered=true;actors[0].damageTick=100;board.Hovered=actors[0].id;
                Capture("remembered-headquarters",new Point(9.5f,16),.15f,new Vector3(0,1.3f,-1.7f));
                var remembered=Model(actors[0]).GetComponent<Renderer>().sharedMaterials;
                Check(remembered[0]!=normal[0]&&remembered[2]!=normal[2],"Fog retains role accent or team color");
                Check(!visuals[actors[0].id].Find("Damage impact").gameObject.activeSelf,"Remembered building emits hit feedback");
                actors[0].remembered=false;actors[0].damageTick=-1;board.Hovered=0;Invoke(board,"LateUpdate");
                Check(Model(actors[0]).GetComponent<Renderer>().sharedMaterials.SequenceEqual(normal),"Restored vision loses building materials");

                var turret=actors[5];var turretType=session.Catalog[turret.definition];
                var origin=new Point(turret.position.x+turretType.footprint*.5f,turret.position.y+turretType.footprint*.5f);
                session.View.attacks=new[]{new AttackEvent{id=1,tick=100,attacker=turret.id,owner=turret.owner,definition=turret.definition,targetDefinition="biological.worker",origin=origin,destination=new Point(origin.x+5,origin.y)}};
                Capture("defense-firing",origin,.14f,new Vector3(0,1.3f,-1.7f));
                Check(Vector3.Dot(visuals[turret.id].forward,board.Content.right)>.99f,"Turret barrel does not face its firing direction");
                session.View.attacks=Array.Empty<AttackEvent>();

                actors[1].complete=false;actors[1].progress=.4f;
                actors[0].damageTick=100;actors[0].health-=25;board.Hovered=actors[0].id;session.Selection.Add(actors[0].id);
                Capture("construction-and-hit",new Point(9.5f,16),.15f,new Vector3(0,1.3f,-1.7f));
                Check(visuals[actors[0].id].Find("Selection").gameObject.activeSelf&&visuals[actors[0].id].Find("Damage impact").gameObject.activeSelf,"Building identity removes selection or damage");
                Check(visuals[actors[1].id].localScale.x<1,"Building growth no longer scales model");
                // Reused buildings retain role geometry but must clear previous fog/hit state.
                var original=actors[0];var originalMesh=Model(original).GetComponent<MeshFilter>().sharedMesh;
                session.View.entities=actors.Skip(1).ToArray();Invoke(board,"LateUpdate");
                var fresh=new VisibleEntity{id=3000,owner=0,definition=original.definition,position=original.position,health=session.Catalog[original.definition].health,complete=true};
                session.View.entities=actors.Skip(1).Append(fresh).ToArray();Invoke(board,"LateUpdate");
                Check(Model(fresh).GetComponent<MeshFilter>().sharedMesh==originalMesh&&!visuals[fresh.id].Find("Damage impact").gameObject.activeSelf,"Pooled building lost shared mesh or inherited hit");
                board.Zoom=3;board.Pan=new Vector2(-14,-8);board.transform.rotation=Quaternion.Euler(0,25,0);
                Capture("zoom-and-rotation",new Point(9.5f,16),.44f,new Vector3(0,1.3f,-1.7f));
                session.LeaveMatch();Check(visuals.Count==0,"Building models survive leaving match");
                Debug.Log($"FRONTIER_BUILDING_PREVIEW passed: {checks} checks, 10 views, {roles.Length*2} building models, {vertices} vertices; footprint, height markers, fog/reveal, construction, hit/hover/selection, turret aiming, pool, zoom/rotation, exit; no player build");
                // Reuse existing focused regressions after changing the shared entity renderer.
                HitFeedbackPreview.Render();WeaponPreview.Render();

                Transform Model(VisibleEntity e)=>visuals[e.id].Find("Building silhouette");
                void Capture(string name,Point center,float size,Vector3 angle)
                {
                    Invoke(board,"LateUpdate");var world=board.Content.TransformPoint(new Vector3(center.x,1,center.y));
                    preview.camera.orthographicSize=size;preview.camera.transform.position=world+board.transform.TransformDirection(angle*size);preview.camera.transform.LookAt(world,board.transform.up);
                    foreach(var label in labels)if(label!=null)
                    {
                        var p=label.localPosition;bool show=size>=.2f||new Vector2(p.x-center.x,p.z+3.6f-center.y).magnitude<6.5f;
                        label.gameObject.SetActive(show);label.rotation=preview.camera.transform.rotation;
                    }
                    preview.BeginStaticPreview(new Rect(0,0,1800,1100));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(sessionObject);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",oldBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",oldMatrix);
            }
        }
    }
}
