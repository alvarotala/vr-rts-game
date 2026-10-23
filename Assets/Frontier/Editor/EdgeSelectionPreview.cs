using System;
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
    public static class EdgeSelectionPreview
    {
        [MenuItem("Frontier/Preview selection from outside board")]
        public static void Render()
        {
            const string output="TestResults/EdgeSelection";
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Edge selection fixture");
            var boardObject=new GameObject("Edge selection board");boardObject.transform.SetParent(fixture.transform);
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            int checks=0,captures=0;
            void Invoke(object target,string name)=>target.GetType().GetMethod(name,flags).Invoke(target,null);
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
            try
            {
                Directory.CreateDirectory(output);
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;
                var pointMethod=typeof(CommanderInput).GetMethod("PointBoard",flags);
                var ground=board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                var units=session.View.entities.Where(e=>e.owner==0&&!session.Catalog[e.definition].building).ToArray();
                Point[] positions={new Point(.6f,10),new Point(47.4f,10),new Point(10,.6f),new Point(10,47.4f),new Point(.6f,.6f),new Point(24,24)};
                for(int i=0;i<units.Length;i++)units[i].position=positions[i];
                var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                var relay=Building(90001,"supply",new Point(25,18));
                var unfinished=Building(90002,"defense",new Point(29,18),complete:false);
                var enemy=Building(90003,"hq",new Point(28,24),owner:1);
                session.View.entities=session.View.entities.Concat(new[]{relay,unfinished,enemy}).ToArray();
                Array.Fill(session.View.visibility,(byte)2);session.View.tick+=4;Invoke(board,"LateUpdate");
                boardObject.transform.SetParent(null,true);preview.AddSingleGO(boardObject);preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);

                Drag(new Point(-4,7),new Point(2,12),new[]{units[0].id},"left");
                Save("left-selected",new Point(1,10),.15f);
                Drag(new Point(52,7),new Point(46,12),new[]{units[1].id},"right");
                Drag(new Point(7,-4),new Point(12,2),new[]{units[2].id});
                Drag(new Point(7,52),new Point(12,46),new[]{units[3].id});
                Drag(new Point(-4,-4),new Point(2,2),new[]{units[4].id},"corner");
                Drag(new Point(2,12),new Point(-4,7),new[]{units[0].id});
                Drag(new Point(-4,-4),new Point(52,52),units.Select(e=>e.id).Concat(new[]{hq.id,relay.id,unfinished.id}).ToArray(),"through-board");
                var previous=session.Selection.ToArray();
                Send(new Point(-6,5),down:true);Send(new Point(-2,12),up:true);
                Check(session.Selection.SetEquals(previous),"Drag entirely outside changed selection");
                Send(new Point(-2,10),down:true);Send(new Point(-2,10),up:true);
                Check(session.Selection.SetEquals(previous),"Outside click changed selection");
                Send(units[0].position,down:true);Send(units[0].position,up:true);
                Check(session.Selection.SetEquals(new[]{units[0].id}),"Normal single-unit click regressed");
                Send(new Point(52,7),down:true);Send(new Point(46,12),up:true,add:true);
                Check(session.Selection.SetEquals(new[]{units[0].id,units[1].id}),"Additive outside drag replaced selection");

                Drag(new Point(hq.position.x-.1f,hq.position.y-.1f),new Point(hq.position.x+1.3f,hq.position.y+1.3f),Array.Empty<int>());
                Check(!session.Selection.Contains(hq.id),"Building footprint corner must not stand in for its visual center");
                float half=session.Catalog[hq.definition].footprint*.5f;
                var hqCenter=new Point(hq.position.x+half,hq.position.y+half);
                Drag(new Point(hqCenter.x-1,hqCenter.y-1),new Point(hqCenter.x+1,hqCenter.y+1),new[]{hq.id},"building-center");
                Drag(new Point(22,16),new Point(32,27),new[]{units[5].id,relay.id,unfinished.id},"mixed-buildings");
                Check(!session.Selection.Contains(enemy.id),"Visible enemy building entered rectangle selection");
                Check(session.Selection.Contains(unfinished.id),"Unfinished own building is missing from rectangle selection");
                session.Select(units[0].id,false);
                Send(new Point(24,17),down:true);Send(new Point(28,21),up:true,add:true);
                Check(session.Selection.SetEquals(new[]{units[0].id,relay.id}),"Additive building rectangle lost the previous unit selection");

                relay.remembered=true;unfinished.health=0;
                session.SelectRectangle(new Point(22,16),new Point(32,27),false);
                Check(session.Selection.SetEquals(new[]{units[5].id}),"Remembered or dead buildings entered rectangle selection");
                relay.remembered=false;unfinished.health=session.Catalog[unfinished.definition].health*.4f;

                // At 4x zoom, this rectangle also covers a unit and building clipped out of the board.
                units[0].position=new Point(20.6f,22);units[5].position=new Point(19.4f,22);
                relay.position=new Point(19.6f,21);unfinished.position=new Point(18.6f,21);
                board.Rebuild();board.Zoom=4;board.Pan=new Vector2(2,1);
                board.transform.SetPositionAndRotation(new Vector3(.4f,.7f,-.3f),Quaternion.Euler(0,70,0));board.Width=.8f;
                Drag(new Point(18,20),new Point(23,24),new[]{units[0].id,relay.id},"zoom-rotated");
                Check(!session.Selection.Contains(unfinished.id),"Rectangle selected a building whose center is clipped outside the board");
                Send(new Point(18,20),down:true);Invoke(input,"ClearPointer");Send(new Point(30,30),up:true);
                Check(session.Selection.SetEquals(new[]{units[0].id,relay.id}),"Cancelled drag completed after input was cleared");
                Send(new Point(18,20),down:true);
                pointMethod.Invoke(input,new object[]{new Ray(board.transform.position+Vector3.up,Vector3.up),false,true,false,false});
                Send(new Point(30,30),up:true);
                Check(session.Selection.SetEquals(new[]{units[0].id,relay.id})&&ground.GetFloat("_SelectionWidth")==0,"Missed plane left a stale drag");

                // Extending selection must not extend order or construction targets beyond the board.
                string message=session.Message;Send(new Point(18,20),order:true);
                Check(session.Message==message,"Outside context order was submitted");
                session.ChooseBuild(session.Catalog.Role(session.Faction,"supply").id);message=session.Message;
                Send(new Point(18,20),down:true);Send(new Point(23,24),up:true);
                Check(session.Message==message&&session.BuildDefinition!=null,"Outside build press confirmed a command or started selection");
                session.CancelMode();session.AttackMoveMode=true;message=session.Message;
                Send(new Point(18,20),down:true);Send(new Point(23,24),up:true);
                Check(session.Message==message&&session.AttackMoveMode,"Outside attack-move press submitted an order");session.CancelMode();
                Debug.Log($"FRONTIER_EDGE_SELECTION passed: {checks} checks, {captures} views; four edges, corner, unit/building mixed selection, unfinished buildings, visual centers, enemy exclusion, zoom/rotation, additive, cancel and order isolation; no player build");

                VisibleEntity Building(int id,string role,Point position,int owner=0,bool complete=true)
                {
                    var type=session.Catalog.Role(owner==0?session.Faction:session.EnemyFaction,role);
                    return new VisibleEntity{id=id,owner=owner,definition=type.id,position=position,health=type.health*(complete?1:.4f),complete=complete,progress=complete?1:.4f};
                }

                Ray RayAt(Point p)
                {
                    var local=new Vector3((p.x-session.Map.width*.5f-board.Pan.x)*board.CellScale,0,(p.y-session.Map.height*.5f-board.Pan.y)*board.CellScale);
                    var offset=board.transform.up+board.transform.forward*.35f;
                    return new Ray(board.transform.TransformPoint(local)+offset,-offset);
                }
                void Send(Point p,bool down=false,bool up=false,bool order=false,bool add=false)
                    =>pointMethod.Invoke(input,new object[]{RayAt(p),down,up,order,add});
                void Drag(Point a,Point b,int[] expected,string name=null)
                {
                    Send(a,down:true);Send(b);
                    Check(ground.GetFloat("_SelectionWidth")>0,"Crossing drag is not drawn");
                    if(name!=null)Save(name,new Point((Mathf.Max(0,a.x)+Mathf.Min(48,b.x))*.5f,(Mathf.Max(0,a.y)+Mathf.Min(48,b.y))*.5f),name=="through-board"?.8f:name=="zoom-rotated"?.15f:.18f);
                    Send(b,up:true);Check(session.Selection.SetEquals(expected),"Selection mismatch: "+(name??"edge"));
                    Check(ground.GetFloat("_SelectionWidth")==0,"Outline remains after release");
                }
                void Save(string name,Point point,float size)
                {
                    Invoke(board,"LateUpdate");var center=board.Content.TransformPoint(new Vector3(point.x,.3f,point.y));
                    preview.camera.orthographicSize=size;preview.camera.transform.position=center+board.transform.up*size*2-board.transform.forward*size*1.6f;preview.camera.transform.LookAt(center,board.transform.up);
                    preview.BeginStaticPreview(new Rect(0,0,1200,900));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
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
