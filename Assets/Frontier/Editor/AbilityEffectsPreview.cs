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
            var oldBounds=Shader.GetGlobalVector("_FrontierBounds");var oldMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0;
            void Invoke(object target,string method)=>target.GetType().GetMethod(method,flags).Invoke(target,null);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("ABILITY_VISUAL_PASS: "+message);}
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                session.View.entities=Array.Empty<VisibleEntity>();session.View.resources=Array.Empty<ResourceState>();session.View.attacks=Array.Empty<AttackEvent>();
                Array.Fill(session.View.visibility,(byte)2);session.View.tick=100;Invoke(board,"LateUpdate");
                var holder=new GameObject("Isolated ability effects");holder.transform.SetParent(board.Content,false);
                var fog=(Texture)typeof(BoardView).GetField("fog",flags).GetValue(board);
                effects=new AbilityEffects(holder.transform,fog,session.Map.width,session.Map.height);
                var mesh=holder.transform.Find("Ability effects").GetComponent<MeshFilter>().sharedMesh;
                var spells=session.Catalog.Definition.abilities;
                var events=spells.Select((ability,i)=>new AbilityEvent{id=i+1,tick=100,caster=999,owner=i/2,ability=ability.id,position=new Point(20+i%2*9,20+i/2*10)}).ToArray();
                session.View.abilityEvents=events;int objects=board.Content.GetComponentsInChildren<Transform>(true).Length;
                effects.Render(board,102,preview?.camera);
                Check(effects.ActiveCount==events.Length&&events.Length==4,"Both spell types and factions render without caster entities");
                Check(mesh.vertexCount>0&&mesh.subMeshCount==1,"All spells use one batched mesh");
                var frozen=mesh.vertices;effects.Render(board,102,preview?.camera);
                Check(frozen.SequenceEqual(mesh.vertices),"Same visual tick freezes all spell animation");
                if(capture)
                {
                    preview.AddSingleGO(boardObject);preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    var center=board.Content.TransformPoint(new Vector3(24.5f,1,25));preview.camera.orthographicSize=board.CellScale*12;
                    preview.camera.transform.position=center+new Vector3(.08f,.5f,-.6f);preview.camera.transform.LookAt(center);
                    Directory.CreateDirectory("TestResults/Abilities");
                    foreach(float seconds in new[]{.08f,.3f,.6f,.85f})
                    {
                        effects.Render(board,100+seconds*Simulation.TickRate,preview.camera);
                        preview.BeginStaticPreview(new Rect(0,0,1400,1000));preview.Render(true);var image=preview.EndStaticPreview();
                        File.WriteAllBytes("TestResults/Abilities/pulse-"+Mathf.RoundToInt(seconds*100).ToString("00")+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                    }
                }
                effects.Render(board,100+.8f*Simulation.TickRate,preview?.camera);
                Check(effects.ActiveCount==2,"Damage pulse ends before the healing motes");
                effects.Render(board,100+Simulation.TickRate,preview?.camera);
                Check(effects.ActiveCount==0&&mesh.vertexCount==0,"All spell effects expire within one second");
                effects.Render(board,99,preview?.camera);Check(effects.ActiveCount==0,"Future events cannot render early");
                Array.Fill(session.View.visibility,(byte)1);effects.Render(board,102,preview?.camera);
                Check(effects.ActiveCount==0,"Explored but currently unseen terrain hides spell effects");
                Array.Fill(session.View.visibility,(byte)2);
                var invalid=events[0];invalid.ability="missing";session.View.abilityEvents=new[]{invalid};effects.Render(board,102,preview?.camera);
                Check(effects.ActiveCount==0,"Unknown spell events are ignored");
                invalid=events[0];invalid.position=new Point(-1,-1);session.View.abilityEvents=new[]{invalid};effects.Render(board,102,preview?.camera);
                Check(effects.ActiveCount==0,"Events outside the map are ignored");
                var edge=events[0];edge.position=new Point(.1f,.1f);session.View.abilityEvents=new[]{edge};effects.Render(board,104,preview?.camera);
                Check(effects.ActiveCount==1&&mesh.vertices.All(v=>v.x>=0&&v.z>=0&&v.x<=session.Map.width&&v.z<=session.Map.height),"An edge pulse stays inside the map");
                session.View.abilityEvents=Enumerable.Range(0,100).Select(i=>{var e=events[i%events.Length];e.id=1000+i;return e;}).ToArray();
                effects.Render(board,104,preview?.camera);
                Check(effects.ActiveCount==AbilityEffects.MaxEvents&&mesh.vertexCount<=AbilityEffects.MaxEvents*320,"Spell visual budget stays bounded");
                for(int frame=0;frame<30;frame++)effects.Render(board,100+frame*.3f,preview?.camera);
                Check(board.Content.GetComponentsInChildren<Transform>(true).Length==objects,"Casting allocates no per-event objects");
                board.Content.gameObject.SetActive(false);effects.Render(board,102,preview?.camera);
                Check(effects.ActiveCount==0&&mesh.vertexCount==0,"Hidden board clears spell particles");board.Content.gameObject.SetActive(true);
                effects.Render(board,102,preview?.camera);effects.Clear();Check(effects.ActiveCount==0&&mesh.vertexCount==0,"Clear removes particles for exit/rematch");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Weapons")),"Shared fog and clipping shader compiles");
                Debug.Log($"FRONTIER_ABILITY_VISUALS passed: {checks} checks, {(capture?4:0)} views; timeline, projected events, fog, pause clock, bounded batching, edges, clear and no per-cast objects; no player build");
            }
            finally
            {
                effects?.Dispose();Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview?.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",oldBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",oldMatrix);
            }
        }
    }
}
