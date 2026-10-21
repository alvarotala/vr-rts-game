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
    public static class AbilityEffectsPreview
    {
        [MenuItem("Frontier/Validate spell effects")]
        public static void Validate()=>Run(false);
        [MenuItem("Frontier/Preview spell effects")]
        public static void Render()=>Run(true);

        private static void Run(bool capture)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var fixture=new GameObject("Ability effects session");var boardObject=new GameObject("Ability effects board");
            var preview=capture?new PreviewRenderUtility():null;AbilityEffects effects=null;
            var oldBounds=Shader.GetGlobalVector("_FrontierBounds");var oldMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0,captures=0;
            void Invoke(object target,string method)=>target.GetType().GetMethod(method,flags).Invoke(target,null);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("ABILITY_VISUAL_PASS: "+message);}
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var camera=preview?.camera??fixture.AddComponent<Camera>();board.Viewer=camera;
                camera.orthographic=true;camera.nearClipPlane=.001f;camera.farClipPlane=10;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.06f);
                PrepareView();Mesh mesh=null;BindEffects();
                var spells=session.Catalog.Definition.abilities;
                var events=spells.Select((ability,i)=>new AbilityEvent{id=i+1,tick=100,caster=1000+i,owner=i/2,ability=ability.id,position=new Point(20+i%2*9,20+i/2*10)}).ToArray();
                LookAt(new Point(24.5f,25),12,false);
                session.View.abilityEvents=events;int objects=board.Content.GetComponentsInChildren<Transform>(true).Length;
                string world=JsonUtility.ToJson(session.Match.Snapshot());
                effects.Render(board,102,camera);
                Check(effects.ActiveCount==events.Length&&events.Length==4,"Both spell types and factions render without caster entities");
                Check(mesh.vertexCount>0&&mesh.subMeshCount==1,"All spells use one batched mesh");
                var frozen=mesh.vertices;var frozenColors=mesh.colors;effects.Render(board,102,camera);
                Check(frozen.SequenceEqual(mesh.vertices)&&frozenColors.SequenceEqual(mesh.colors),"Same visual tick freezes spell shape and opacity");
                effects.Render(board,100+.6f*Simulation.TickRate,camera);effects.Render(board,102,camera);
                Check(frozen.SequenceEqual(mesh.vertices)&&frozenColors.SequenceEqual(mesh.colors),"Rewinding recreates exactly the same geometry and color without accumulated state");
                foreach(var cast in events)
                {
                    session.View.abilityEvents=new[]{cast};Vector3[] previous=null;
                    foreach(float seconds in new[]{.04f,.18f,.42f,.72f})
                    {
                        effects.Render(board,100+seconds*Simulation.TickRate,camera);CheckGeometry(cast.ability+" at "+seconds);
                        Check(effects.ActiveCount==1&&mesh.vertexCount<=AbilityEffects.MaxVerticesPerEvent,"One spell stays within its geometry budget: "+cast.ability);
                        if(previous!=null)Check(!previous.SequenceEqual(mesh.vertices),"The spell evolves between visible animation phases: "+cast.ability);
                        previous=mesh.vertices;
                    }
                    LookAt(cast.position,6,true);effects.Render(board,103.6f,camera);CheckGeometry(cast.ability+" directly above");
                    var vertices=mesh.vertices;var triangles=mesh.triangles;float area=0;
                    for(int index=0;index<triangles.Length;index+=3)
                        area+=Mathf.Abs(Vector3.Dot(Vector3.Cross(vertices[triangles[index+1]]-vertices[triangles[index]],vertices[triangles[index+2]]-vertices[triangles[index]]),Vector3.up));
                    Check(area>.01f,"Spell retains visible projected area when viewed directly from above: "+cast.ability);
                    LookAt(new Point(24.5f,25),12,false);
                }
                session.View.abilityEvents=events;
                effects.Render(board,100+(AbilityEffects.DamageLifetime+.02f)*Simulation.TickRate,camera);
                Check(effects.ActiveCount==2,"Damage pulse ends before the healing motes");
                effects.Render(board,100+(AbilityEffects.HealingLifetime+.02f)*Simulation.TickRate,camera);
                Check(effects.ActiveCount==0&&mesh.vertexCount==0&&AbilityEffects.HealingLifetime<1,"All spell effects expire before their recorded event leaves the one-second history");
                effects.Render(board,99,camera);Check(effects.ActiveCount==0,"Future events cannot render early");
                Array.Fill(session.View.visibility,(byte)1);effects.Render(board,102,camera);
                Check(effects.ActiveCount==0,"Explored but currently unseen terrain hides spell effects");
                Array.Fill(session.View.visibility,(byte)2);
                var invalid=events[0];invalid.ability="missing";session.View.abilityEvents=new[]{invalid};effects.Render(board,102,camera);
                Check(effects.ActiveCount==0,"Unknown spell events are ignored");
                invalid=events[0];invalid.position=new Point(-1,-1);session.View.abilityEvents=new[]{invalid};effects.Render(board,102,camera);
                Check(effects.ActiveCount==0,"Events outside the map are ignored");
                foreach(var size in new[]{(.6f,1f),(2f,4f)})
                {
                    board.Width=size.Item1;board.Zoom=size.Item2;Invoke(board,"UpdateTransform");
                    foreach(var corner in new[]{new Point(.1f,.1f),new Point(session.Map.width-.1f,.1f),new Point(.1f,session.Map.height-.1f),new Point(session.Map.width-.1f,session.Map.height-.1f)})
                        foreach(var cast in events)
                        {
                            var edge=cast;edge.position=corner;session.View.abilityEvents=new[]{edge};effects.Render(board,104,camera);
                            Check(effects.ActiveCount==1&&mesh.vertices.All(v=>Finite(v)&&v.x>=0&&v.z>=0&&v.x<=session.Map.width&&v.z<=session.Map.height),"Spell remains finite and inside a map corner at board scale "+board.Width+" / zoom "+board.Zoom+": "+cast.ability);
                        }
                }
                board.Width=1.2f;board.Zoom=1;Invoke(board,"UpdateTransform");LookAt(new Point(24.5f,25),12,false);
                session.View.abilityEvents=Enumerable.Range(0,100).Select(i=>{var e=events[i%events.Length];e.id=1000+i;return e;}).ToArray();
                for(int frame=0;frame<30;frame++)
                {
                    effects.Render(board,100+frame*.3f,camera);
                    Check(mesh.vertexCount<=AbilityEffects.MaxEvents*AbilityEffects.MaxVerticesPerEvent,"Simultaneous spells obey the mesh budget throughout their animation");
                }
                effects.Render(board,104,camera);
                Check(effects.ActiveCount==AbilityEffects.MaxEvents,"Only the newest bounded set of simultaneous spell events is presented");
                Check(board.Content.GetComponentsInChildren<Transform>(true).Length==objects,"Casting allocates no per-event objects");
                board.Content.gameObject.SetActive(false);effects.Render(board,102,camera);
                Check(effects.ActiveCount==0&&mesh.vertexCount==0,"Hidden board clears spell particles");board.Content.gameObject.SetActive(true);
                effects.Render(board,102,camera);effects.Clear();Check(effects.ActiveCount==0&&mesh.vertexCount==0,"Clear removes particles for exit/rematch");
                Check(JsonUtility.ToJson(session.Match.Snapshot())==world,"Animating and rewinding spells never changes the authoritative world");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Weapons")),"Shared fog and clipping shader compiles");
                if(capture)
                {
                    preview.AddSingleGO(boardObject);Directory.CreateDirectory("TestResults/Abilities");session.View.abilityEvents=events;
                    foreach(float seconds in new[]{.04f,.18f,.42f,.72f,.92f})Capture("pulse-"+Mathf.RoundToInt(seconds*100).ToString("00"),seconds);
                    LookAt(new Point(24.5f,25),12,true);Capture("top-down",.18f);
                    // Models come from the projected view, not hidden simulation entities.
                    // Keep the dedicated spell mesh isolated from BoardView's own renderer.
                    AddVisibleModels(events);LookAt(new Point(24.5f,20),6.6f,false);Capture("industrial-with-units",.18f);
                    LookAt(new Point(24.5f,30),6.6f,false);Capture("biological-with-units",.42f);
                }
                effects.Dispose();effects=null;session.LeaveMatch();session.LoadMap("rift");session.StartMatch();PrepareView();BindEffects();
                var rampEvents=new[]{events[0],events[1]};rampEvents[0].position=new Point(52,40);rampEvents[1].position=new Point(76,40);
                session.View.abilityEvents=rampEvents;LookAt(new Point(64,40),13,false);effects.Render(board,103.6f,camera);CheckGeometry("spells on opposite ramps");
                Check(board.HeightAt(new Point(50,40))<board.HeightAt(new Point(53.9f,40))&&mesh.vertices.Any(v=>v.y>1.5f),"Both spell effects remain finite above real sloped terrain");
                if(capture)Capture("rift-ramps",.18f);
                Debug.Log($"FRONTIER_ABILITY_VISUALS passed: {checks} checks, {captures} views; timeline, projected events, fog, pause/replay, bounded batching, top-down, ramp, zoom, edges, clear and no per-cast objects; no player build");

                void PrepareView()
                {
                    session.View.entities=Array.Empty<VisibleEntity>();session.View.resources=Array.Empty<ResourceState>();session.View.attacks=Array.Empty<AttackEvent>();
                    session.View.abilityEvents=Array.Empty<AbilityEvent>();Array.Fill(session.View.visibility,(byte)2);session.View.tick=100;Invoke(board,"LateUpdate");
                }
                void BindEffects()
                {
                    var holder=new GameObject("Isolated ability effects");holder.transform.SetParent(board.Content,false);
                    var fog=(Texture)typeof(BoardView).GetField("fog",flags).GetValue(board);
                    effects=new AbilityEffects(holder.transform,fog,session.Map.width,session.Map.height);
                    mesh=holder.transform.Find("Ability effects").GetComponent<MeshFilter>().sharedMesh;
                }
                void LookAt(Point point,float cells,bool top)
                {
                    var center=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point)+.6f,point.y));
                    camera.orthographicSize=board.CellScale*cells;
                    camera.transform.position=center+(top?new Vector3(0,.65f,0):new Vector3(.08f,.5f,-.6f));
                    camera.transform.LookAt(center,top?Vector3.forward:Vector3.up);
                }
                void CheckGeometry(string context)
                {
                    Check(mesh.vertexCount>0&&mesh.vertices.All(Finite)&&mesh.triangles.All(index=>index>=0&&index<mesh.vertexCount),"Finite renderable geometry: "+context);
                }
                bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);
                void AddVisibleModels(AbilityEvent[] casts)
                {
                    var visible=new System.Collections.Generic.List<VisibleEntity>();int id=2000;
                    foreach(var cast in casts)
                    {
                        var ability=session.Catalog.Ability(cast.ability);string faction=ability.faction;
                        var caster=session.Catalog.Role(faction,"caster");
                        visible.Add(new VisibleEntity{id=cast.caster,owner=cast.owner,definition=caster.id,position=new Point(cast.position.x-3.4f,cast.position.y-1.1f),health=caster.health,complete=true,order=CommandKind.Hold});
                        for(int i=0;i<3;i++)
                        {
                            var type=session.Catalog.Role(faction,i==2?"ranged":"melee");
                            visible.Add(new VisibleEntity{id=id++,owner=ability.effect=="heal"?cast.owner:1-cast.owner,definition=type.id,position=new Point(cast.position.x-1.1f+i*1.05f,cast.position.y+(i%2==0?.8f:-.8f)),health=type.health,complete=true,order=CommandKind.Hold});
                        }
                    }
                    session.View.entities=visible.ToArray();session.View.abilityEvents=Array.Empty<AbilityEvent>();Invoke(board,"LateUpdate");session.View.abilityEvents=casts;
                }
                void Capture(string name,float seconds)
                {
                    effects.Render(board,100+seconds*Simulation.TickRate,camera);
                    preview.BeginStaticPreview(new Rect(0,0,1400,1000));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes("TestResults/Abilities/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                }
            }
            finally
            {
                effects?.Dispose();Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview?.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",oldBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",oldMatrix);
            }
        }
    }
}
