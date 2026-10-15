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
    public static class HitFeedbackPreview
    {
        [MenuItem("Frontier/Preview received damage")]
        public static void Render()
        {
            const string output="TestResults/Hits";
            var preview=new PreviewRenderUtility();var sessionObject=new GameObject("Hit preview session");var boardObject=new GameObject("Hit preview board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            void Invoke(object target,string name)=>target.GetType().GetMethod(name,flags).Invoke(target,null);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);}
            try
            {
                Directory.CreateDirectory(output);
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var actors=new List<VisibleEntity>();string[] roles={"worker","scout","melee","ranged","siege"};
                for(int faction=0;faction<2;faction++)for(int role=0;role<5;role++)
                    Add(faction,roles[role],new Point(19+role*3,18+faction*4));
                Add(0,"hq",new Point(18,27));Add(1,"hq",new Point(29,27));
                session.View.entities=actors.ToArray();Array.Fill(session.View.visibility,(byte)2);session.View.tick=100;
                preview.AddSingleGO(boardObject);
                preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                var visuals=(Dictionary<int,Transform>)typeof(BoardView).GetField("entities",flags).GetValue(board);
                var properties=new MaterialPropertyBlock();
                Capture("before",new Point(26,25),.13f);
                foreach(int index in new[]{0,3,6,10,11}){actors[index].damageTick=100;actors[index].health-=10;}
                session.Selection.Add(actors[3].id);board.Hovered=actors[3].id;
                Capture("simultaneous-hits",new Point(26,25),.13f);
                Check(Impact(actors[0]).gameObject.activeSelf&&!Impact(actors[1]).gameObject.activeSelf,"Unhit neighbor incorrectly marked");
                Check(Flash(actors[0])>.8f&&Flash(actors[1])==0,"Body flash does not identify hit target");
                Check(visuals[actors[3].id].Find("Selection").gameObject.activeSelf,"Damage removed selection");
                Capture("worker-hit",actors[0].position,.026f);Capture("soldier-hover-hit",actors[3].position,.028f);
                Capture("building-hit",new Point(21,30),.06f);Capture("enemy-building-hit",new Point(32,30),.06f);
                session.View.tick=106;Capture("fading",new Point(26,25),.13f);
                Check(Flash(actors[0])==0&&Impact(actors[0]).gameObject.activeSelf,"Flash and impact lifetime not separated");
                session.View.tick=112;Capture("expired",new Point(26,25),.13f);
                Check(actors.All(e=>!Impact(e).gameObject.activeSelf&&Flash(e)==0),"Old hits remain visible");

                // A fresh hit restarts the existing marker, even with multiple attackers.
                int count=board.Content.GetComponentsInChildren<Renderer>(true).Length;
                actors[0].damageTick=112;Invoke(board,"LateUpdate");
                Impact(actors[0]).GetComponent<Renderer>().GetPropertyBlock(properties);
                Check(Impact(actors[0]).gameObject.activeSelf&&properties.GetFloat("_Progress")==0&&count==board.Content.GetComponentsInChildren<Renderer>(true).Length,"Repeated hit stacks or fails to refresh marker");
                // Pool reuse must not give a newly spawned unit its predecessor's flash.
                var retired=actors[0];session.View.entities=actors.Skip(1).ToArray();Invoke(board,"LateUpdate");
                var fresh=new VisibleEntity{id=5000,owner=retired.owner,definition=retired.definition,position=retired.position,health=session.Catalog[retired.definition].health,complete=true};
                session.View.entities=actors.Skip(1).Append(fresh).ToArray();Invoke(board,"LateUpdate");
                Check(!Impact(fresh).gameObject.activeSelf&&Flash(fresh)==0,"Pooled unit inherited damage effect");
                actors[11].remembered=true;actors[11].damageTick=112;Invoke(board,"LateUpdate");
                Check(!Impact(actors[11]).gameObject.activeSelf&&Flash(actors[11])==0,"Fog memory emits hit feedback");
                session.LeaveMatch();Check(visuals.Count==0,"Hit visuals survive leaving match");
                Debug.Log("FRONTIER_HIT_PREVIEW passed: simultaneous targets, unit/building, both factions, hover/selection, expiry, repeat, pool reuse, fog memory, exit; 8 views rendered; no player build");

                void Add(int owner,string role,Point position)
                {
                    var type=session.Catalog.Role(owner==0?"industrial":"biological",role);
                    actors.Add(new VisibleEntity{id=1000+actors.Count,owner=owner,definition=type.id,position=position,health=type.health,complete=true});
                }
                Transform Impact(VisibleEntity entity)=>visuals[entity.id].Find("Damage impact");
                float Flash(VisibleEntity entity)
                {
                    var root=visuals[entity.id];var body=session.Catalog[entity.definition].building?root.GetChild(0):root.Find("Role silhouette");
                    body.GetComponent<Renderer>().GetPropertyBlock(properties);return properties.GetFloat("_DamageAmount");
                }
                void Capture(string name,Point center,float size)
                {
                    Invoke(board,"LateUpdate");var world=board.Content.TransformPoint(new Vector3(center.x,1.5f,center.y));
                    preview.camera.orthographicSize=size;preview.camera.transform.position=world+new Vector3(0,size*2,-size*2.1f);preview.camera.transform.LookAt(world);
                    preview.BeginStaticPreview(new Rect(0,0,1200,900));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(sessionObject);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
