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
    public static class WeaponPreview
    {
        [MenuItem("Frontier/Preview weapon fire")]
        public static void Render()
        {
            const string output="TestResults/Weapons";const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var preview=new PreviewRenderUtility();var sessionObject=new GameObject("Weapon preview session");var boardObject=new GameObject("Weapon preview board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0;
            void Invoke(object o,string name)=>o.GetType().GetMethod(name,flags).Invoke(o,null);
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            try
            {
                Directory.CreateDirectory(output);
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var actors=new List<VisibleEntity>();var shots=new List<AttackEvent>();
                string[] roles={"ranged","siege","melee","defense"};
                for(int faction=0;faction<2;faction++)for(int row=0;row<4;row++)
                {
                    Point from=new Point(17+faction*19,18+row*9),to=new Point(from.x+(row==2?1.6f:row==1?9:6),from.y);
                    var shooter=Add(faction,roles[row],from);var target=Add(1-faction,"melee",to);
                    shots.Add(new AttackEvent{id=shots.Count+1,tick=100,attacker=shooter.id,target=target.id,owner=faction,definition=shooter.definition,targetDefinition=target.definition,origin=from,destination=to});
                }
                session.View.entities=actors.ToArray();session.View.attacks=shots.ToArray();Array.Fill(session.View.visibility,(byte)2);session.View.tick=100;
                preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                WeaponEffects Effects()=>(WeaponEffects)typeof(BoardView).GetField("weapons",flags).GetValue(board);
                var visuals=(Dictionary<int,Transform>)typeof(BoardView).GetField("entities",flags).GetValue(board);
                Capture("muzzle-flashes",new Point(30,31),.23f,.06f);
                Check(Effects().ActiveCount==8,"Missing simultaneous shots or faction/weapon variants");
                Check(Vector3.Dot(visuals[actors[0].id].forward,board.Content.right)>.99f,"Auto attacker is not facing actual target");
                Capture("fire-overview",new Point(30,31),.23f,.16f);
                Capture("industrial-tracer",new Point(20,18),.049f,.13f);
                Capture("biological-projectile",new Point(39,18),.049f,.17f);
                Capture("artillery-arcs",new Point(31,27),.18f,.28f);
                Capture("melee-strikes",new Point(27,36),.14f,.09f);
                Capture("defense-towers",new Point(30,45),.18f,.15f);
                Capture("artillery-impacts",new Point(31,27),.18f,.62f);
                Check(Effects().ActiveCount==2,"Only artillery should still be visible at 0.62 seconds");
                int objects=board.Content.GetComponentsInChildren<Transform>(true).Length;
                for(int frame=0;frame<120;frame++)Effects().Render(board,100+frame*.15f,preview.camera);
                Check(objects==board.Content.GetComponentsInChildren<Transform>(true).Length,"Shots allocate objects over time");
                Capture("expired",new Point(30,31),.23f,1);
                Check(Effects().ActiveCount==0,"Weapon trails never expire");
                // Dead targets must not be needed to render the saved endpoint.
                session.View.entities=actors.Where((e,i)=>i%2==0).ToArray();Invoke(board,"LateUpdate");Effects().Render(board,102,preview.camera);
                Check(Effects().ActiveCount==8,"Removing target suppresses killing shot");
                session.View.entities=actors.ToArray();
                // Resume/paused updates at the same tick must not recreate or advance shots.
                session.Pause("weapon-preview",true);Invoke(board,"LateUpdate");
                var mesh=board.Content.Find("Weapon effects").GetComponent<MeshFilter>().sharedMesh;
                var frozen=mesh.vertices;Invoke(board,"LateUpdate");
                Check(frozen.SequenceEqual(mesh.vertices),"Paused weapon particles keep moving");session.Pause("weapon-preview",false);
                Array.Fill(session.View.visibility,(byte)1);session.View.tick=104;
                Capture("fog-hides-shots",new Point(30,31),.23f,.13f);
                Check(Effects().ActiveCount==0,"Shots remain in explored but unseen terrain");
                Array.Fill(session.View.visibility,(byte)2);session.View.tick=108;
                board.Zoom=4;board.Pan=new Vector2(-43,-32);board.transform.rotation=Quaternion.Euler(0,28,0);
                Capture("zoom-clipped",new Point(26,27),.26f,.13f);
                Check(mesh.vertexCount>0,"Zoom unexpectedly disables weapon mesh");
                board.SetVisible(false);Check(Effects().ActiveCount==0&&mesh.vertexCount==0,"Hiding board retains weapon particles");board.SetVisible(true);
                // Bound worst-case rendering independently from simulation unit limits.
                session.View.attacks=Enumerable.Range(0,300).Select(i=>{var shot=shots[i%shots.Count];shot.id=1000+i;return shot;}).ToArray();
                Effects().Render(board,102,preview.camera);Check(Effects().ActiveCount==WeaponEffects.MaxShots,"Weapon visual budget is unbounded");
                Check(board.Content.GetComponentsInChildren<Transform>(true).Length==objects,"Stress allocates per-shot objects");
                session.LeaveMatch();Check(Effects().ActiveCount==0,"Weapon particles survive leaving match");
                session.StartMatch();Check(Effects().ActiveCount==0&&session.View.attacks.Length==0,"Rematch replays previous shots");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Weapons")),"Weapon shader contains compilation errors");
                Debug.Log($"FRONTIER_WEAPON_PREVIEW passed: {checks} checks, 11 views; timeline, aiming, both factions, melee/ranged/siege/towers, lethal shot, pause, fog, zoom, expiry, bounded mesh, hide/exit/rematch; no player build");

                VisibleEntity Add(int owner,string role,Point center)
                {
                    var type=session.Catalog.Role(owner==0?"industrial":"biological",role);float half=type.building?type.footprint*.5f:0;
                    // Automatic fire is a Stop/Hold action; enum default Move
                    // correctly cancels an old shot's facing and firing pose.
                    var actor=new VisibleEntity{id=1000+actors.Count,owner=owner,definition=type.id,position=new Point(center.x-half,center.y-half),health=type.health,complete=true,order=CommandKind.Stop};actors.Add(actor);return actor;
                }
                void Capture(string name,Point center,float size,float seconds)
                {
                    Invoke(board,"LateUpdate");var world=board.Content.TransformPoint(new Vector3(center.x,1,center.y));
                    preview.camera.orthographicSize=size;preview.camera.transform.position=world+board.transform.TransformDirection(new Vector3(0,size*2,-size*2.1f));preview.camera.transform.LookAt(world,board.transform.up);
                    Effects().Render(board,100+seconds*Simulation.TickRate,preview.camera);
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
