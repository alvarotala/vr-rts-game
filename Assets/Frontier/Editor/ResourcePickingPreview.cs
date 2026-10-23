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
    public static class ResourcePickingPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        public static void ValidateSelection()
        {
            Render();
            EdgeSelectionPreview.Render();
        }
        [MenuItem("Frontier/Preview mineral targeting")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/ResourcePicking");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Resource picking session");var boardObject=new GameObject("Resource picking board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");int checks=0,captures=0;
            bool hadBasePreference=PlayerPrefs.HasKey(BoardView.BaseTargetingKey);
            int basePreference=PlayerPrefs.GetInt(BoardView.BaseTargetingKey);
            try
            {
                PlayerPrefs.DeleteKey(BoardView.BaseTargetingKey);
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;
                var send=typeof(CommanderInput).GetMethod("PointBoard",Private);
                foreach(string map in new[]{"rift","training"})
                {
                    session.LoadMap(map);session.StartMatch();board.Width=1.2f;board.Fit();board.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    var patch=session.Match.Resources.First(r=>r.kind=="mineral");
                    var workers=session.Match.Entities.Values.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").ToArray();
                    for(int i=0;i<workers.Length;i++)workers[i].position=new Point(5+i,5);
                    var worker=workers[0];worker.position=new Point(patch.position.x+.6f,patch.position.y+1.25f);
                    Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                    Check(board.EntityAt(patch.position)==worker.id,"Worker selection area overlaps the mineral");
                    Send(Top(patch.position));
                    Check(board.Hovered==worker.id&&board.HoveredResource==0,"The whole unit selection area takes priority over a crystal");
                    worker.position=new Point(patch.position.x+.6f,patch.position.y+2.25f);Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                    Send(Top(patch.position));
                    Check(board.Hovered==0&&board.HoveredResource==patch.id,"Leaving the unit selection area makes the mineral selectable");
                    Invoke(board,"LateUpdate");Check(HoverAmount(patch.id)>0&&Ground().GetFloat("_HoverWidth")>0,"Mineral hover lights its model and paints a ground outline");Capture(map+"-hover",patch.position);
                    Send(Top(patch.position),down:true);Send(Top(new Point(patch.position.x+1.3f,patch.position.y)),up:true);
                    Check(session.SelectedResource==patch.id&&session.Selection.Count==0,"Small hand movement retains the pressed patch instead of selecting a neighbor or area");
                    var near=new Point(patch.position.x,patch.position.y-2.3f);
                    Check(board.ResourceAt(near)==patch.id,"Expanded mineral target accepts a near miss outside the old two-cell radius");
                    session.Select(worker.id,false);Send(Top(near),order:true);
                    Check(worker.order==CommandKind.Gather&&session.Match.Resources.Any(r=>r.id==worker.resourceId&&r.kind=="mineral"),"A near-miss order gathers instead of merely walking next to minerals");
                    Send(Top(worker.position));Check(board.Hovered==worker.id&&board.HoveredResource==0,"Direct worker aim keeps priority over mineral assistance");
                    Send(Top(worker.position),down:true);Send(Top(worker.position),up:true);Check(session.Selection.SetEquals(new[]{worker.id}),"Workers beside minerals remain selectable");

                    // Isolate one patch for shallow-angle rays from all four sides.
                    foreach(var other in session.Match.Resources.Where(r=>r.id!=patch.id))other.remaining=0;
                    worker.position=new Point(5,5);Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                    foreach(float width in new[]{.6f,1.2f,2f})
                    {
                        board.Width=width;board.transform.SetPositionAndRotation(new Vector3(.3f,.7f,-.2f),Quaternion.Euler(0,37,0));Invoke(board,"LateUpdate");
                        for(int side=0;side<4;side++)
                        {
                            var center=board.Content.TransformPoint(new Vector3(patch.position.x,board.HeightAt(patch.position)+.85f,patch.position.y));
                            var direction=Quaternion.AngleAxis(side*90,board.transform.up)*board.transform.forward;
                            var origin=center+board.transform.up*.08f+direction*.4f;var ray=new Ray(origin,center-origin);
                            Check(board.RayToMap(ray,out var projected)&&projected.Distance(patch.position)>3.5f,"Oblique ray projects well beyond the mineral's old ground target");
                            Send(ray);Check(board.HoveredResource==patch.id&&board.Hovered==0,"Crystal volume can be targeted from side "+side+" at board width "+width);
                            if(width==1.2f&&side==0)
                            {
                                worker.position=projected;Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                                Send(ray);Check(board.Hovered==worker.id&&board.HoveredResource==0,"Unit area wins over foreground minerals in body mode too");
                                board.SetBaseTargeting(true);
                                Check(board.Hovered==0&&board.HoveredResource==0&&!board.Pointer.HasValue,"Switching targeting clears stale pointer feedback");
                                Send(ray);Check(board.Hovered==worker.id&&board.HoveredResource==0,"Base mode reaches the worker behind the crystal");
                                session.Selection.Clear();Send(ray,down:true);Send(ray,up:true);
                                Check(session.Selection.SetEquals(new[]{worker.id}),"Trigger selects the worker whose base is behind a crystal");
                                Invoke(board,"LateUpdate");Capture(map+"-base-through-mineral",worker.position);
                                session.View.entities.First(e=>e.id==worker.id).remembered=true;Send(ray);
                                Check(board.Hovered!=worker.id,"Base targeting cannot select a remembered entity");Refresh();
                                worker.position=new Point(5,5);Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                                Send(ray);Check(board.Hovered==0&&board.HoveredResource==0,"Base mode leaves ground behind the crystal empty when no unit is there");
                                Send(Top(patch.position));Check(board.HoveredResource==patch.id,"Minerals remain targetable at their ground base");
                                Send(Top(patch.position),order:true);
                                Check(worker.order==CommandKind.Gather,"Context orders use the same base target as the hover");
                                board.SetBaseTargeting(false);Send(ray);
                                Check(board.HoveredResource==patch.id,"Disabling base mode restores crystal body targeting");
                            }
                            if(width==1.2f&&side==0){Invoke(board,"LateUpdate");Capture(map+"-oblique",patch.position);}
                            if(width==.6f&&side==0){Invoke(board,"LateUpdate");Capture(map+"-compact",patch.position);}
                        }
                    }
                    board.Width=.6f;Invoke(board,"LateUpdate");Send(Top(patch.position),down:true);
                    Send(Top(new Point(patch.position.x+Mathf.Min(1.8f,.012f/board.CellScale),patch.position.y)),up:true);
                    Check(session.SelectedResource==patch.id,"Small physical tremor on a compact board is not mistaken for a selection drag");
                    Send(Top(patch.position));Invoke(input,"ClearPointer");Invoke(board,"LateUpdate");
                    Check(board.HoveredResource==0&&HoverAmount(patch.id)==0&&Ground().GetFloat("_HoverWidth")==0,"Leaving the target removes resource hover and outline");
                    Send(Top(patch.position));session.Pause("tracking",true);Invoke(board,"LateUpdate");
                    Check(HoverAmount(patch.id)==0&&Ground().GetFloat("_HoverWidth")==0,"Tracking pause suppresses mineral feedback");session.Pause("tracking",false);
                    int cell=(int)patch.position.y*session.Map.width+(int)patch.position.x;byte vision=session.View.visibility[cell];
                    session.View.visibility[cell]=0;Send(Top(patch.position));
                    Check(board.HoveredResource==0&&board.ResourceAt(patch.position)==0,"Unexplored deposits are not targetable");session.View.visibility[cell]=vision;
                    patch.remaining=0;Refresh();Send(Top(patch.position));
                    Check(board.HoveredResource==0&&board.ResourceAt(patch.position)==0,"Depleted deposits are not targetable");patch.remaining=1800;Refresh();
                    board.Zoom=4;board.Pan=new Vector2(patch.position.x+session.Map.width*.125f+1-session.Map.width*.5f,patch.position.y-session.Map.height*.5f);Invoke(board,"LateUpdate");
                    Check(board.ResourceAt(new Point(patch.position.x+2,patch.position.y))==0,"Assistance cannot select a resource clipped outside the board");
                    board.Fit();Invoke(board,"LateUpdate");Send(Top(patch.position));board.SetVisible(false);
                    Check(board.HoveredResource==0,"Hiding the board clears the resource target");board.SetVisible(true);
                    CheckBuildingBase();
                    CheckMinimumTargets();
                    session.LeaveMatch();Check(board.HoveredResource==0,"Leaving the match clears the resource target");

                    Ray Top(Point point)
                    {
                        var world=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point),point.y));
                        return new Ray(world+board.transform.up*.4f,-board.transform.up);
                    }
                    void Refresh()=>typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    void CheckBuildingBase()
                    {
                        board.Width=1.2f;
                        var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                        int footprint=session.Catalog[hq.definition].footprint;
                        hq.position=new Point(patch.position.x-footprint*.5f,patch.position.y-footprint*.5f);
                        worker.position=new Point(patch.position.x,hq.position.y+footprint+.7f);
                        Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                        var target=board.Content.TransformPoint(new Vector3(worker.position.x,board.HeightAt(worker.position),worker.position.y));
                        var foreground=board.Content.TransformPoint(new Vector3(patch.position.x,board.HeightAt(hq.position)+1,patch.position.y));
                        var ray=new Ray(target+(foreground-target)*6,target-foreground);
                        Check(board.RayToMap(ray,out var projected)&&projected.Distance(worker.position)<.05f,"Building fixture points at the worker's ground base");
                        Send(ray);Check(board.Hovered==worker.id&&board.HoveredResource==0,"Unit area wins over a foreground building in body mode");
                        board.SetBaseTargeting(true);Send(ray);
                        Check(board.Hovered==worker.id&&board.HoveredResource==0,"Base mode reaches the worker behind the building");
                        session.Selection.Clear();Invoke(input,"ClearPointer");Send(ray,down:true);Send(ray,up:true);
                        Check(session.Selection.SetEquals(new[]{worker.id}),"Trigger selects the worker behind the building");
                        Invoke(board,"LateUpdate");Capture(map+"-base-through-building",worker.position);
                        Send(Top(patch.position));Check(board.Hovered==hq.id&&board.HoveredResource==0,"Building footprint remains selectable in base mode");
                        var remembered=session.View.entities.First(e=>e.id==hq.id);remembered.remembered=true;
                        int cell=(int)patch.position.y*session.Map.width+(int)patch.position.x;
                        session.View.visibility[cell]=0;Send(Top(patch.position));
                        Check(board.Hovered==0&&board.HoveredResource==0,"Base targeting excludes remembered buildings and unexplored minerals");
                        Refresh();patch.remaining=0;Refresh();remembered=session.View.entities.First(e=>e.id==hq.id);remembered.remembered=true;
                        Send(Top(patch.position));Check(board.HoveredResource==0,"Base mode excludes depleted deposits");
                        board.Zoom=4;board.Pan=new Vector2(worker.position.x+session.Map.width*.125f+.1f-session.Map.width*.5f,worker.position.y-session.Map.height*.5f);
                        Invoke(board,"LateUpdate");var edge=new Point(worker.position.x+.3f,worker.position.y);
                        Send(Top(edge));Check(board.Hovered!=worker.id,"Unit selection assistance cannot reach a base clipped outside the board");
                        board.SetBaseTargeting(false);board.Fit();
                    }
                    void CheckMinimumTargets()
                    {
                        session.Match.Entities.Clear();session.Match.Entities.Add(worker.id,worker);
                        foreach(var resource in session.Match.Resources)resource.remaining=0;
                        var center=patch.position;
                        worker.position=new Point(center.x+12,center.y+12);
                        var gas=session.Match.Resources.First(r=>r.kind=="catalyst");gas.position=center;
                        foreach(bool baseMode in new[]{false,true})
                        foreach(float width in new[]{.6f,1.2f,2f})
                        {
                            board.SetBaseTargeting(baseMode);board.Width=width;board.Fit();
                            Invoke(board,"LateUpdate");float radius=board.MinimumStaticPickRadius;
                            foreach(var resource in new[]{patch,gas})
                            {
                                resource.remaining=1800;Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                                var near=new Point(center.x+radius-.05f,center.y);
                                Send(Top(near));Check(board.HoveredResource==resource.id&&board.Hovered==0,"Minerals and gas share the minimum target at width "+width+", bases="+baseMode);
                                worker.position=center;Refresh();Invoke(board,"LateUpdate");
                                Send(Top(new Point(center.x+BoardView.UnitPickRadius-.05f,center.y)));
                                Check(board.Hovered==worker.id&&board.HoveredResource==0,"Unit assisted edge wins over "+resource.kind);
                                Send(Top(new Point(center.x+BoardView.UnitPickRadius+.05f,center.y)));
                                Check(board.Hovered==0&&board.HoveredResource==resource.id,"Crossing the unit area boundary exposes "+resource.kind);
                                worker.position=new Point(center.x+12,center.y+12);resource.remaining=0;Refresh();
                            }
                            foreach(string faction in new[]{"industrial","biological"})
                            foreach(string role in new[]{"supply","defense"})
                            {
                                var type=session.Catalog.Role(faction,role);
                                var building=new EntityState{id=1500,owner=0,definition=type.id,health=type.health,complete=true,
                                    position=new Point(center.x-type.footprint*.5f,center.y-type.footprint*.5f)};
                                session.Match.Entities.Add(building.id,building);Refresh();board.Rebuild();Invoke(board,"LateUpdate");
                                var near=new Point(center.x+radius-.05f,center.y);
                                Send(Top(near));Check(board.Hovered==building.id&&board.HoveredResource==0,"Small "+type.id+" accepts the same minimum target as deposits");
                                Send(Top(near),down:true);Send(Top(near),up:true);
                                Check(session.Selection.SetEquals(new[]{building.id}),"Trigger selects the small building through its assisted area");
                                building.complete=false;building.remaining=Mathf.RoundToInt(type.duration*.95f);Refresh();Invoke(board,"LateUpdate");
                                Send(Top(near));Check(board.Hovered==building.id,"Construction retains its full selection area before its model grows");
                                worker.position=center;Refresh();Invoke(board,"LateUpdate");
                                Send(Top(new Point(center.x+BoardView.UnitPickRadius-.05f,center.y)));
                                Check(board.Hovered==worker.id&&board.HoveredResource==0,"Unit assisted edge wins over "+type.id);
                                Send(Top(new Point(center.x+BoardView.UnitPickRadius+.05f,center.y)));
                                Check(board.Hovered==building.id&&board.HoveredResource==0,"Leaving the unit area exposes "+type.id);
                                if(baseMode)
                                {
                                    worker.position=new Point(center.x+12,center.y+12);Refresh();
                                    board.Zoom=4;board.Pan=new Vector2(center.x+.5f+session.Map.width*.125f-session.Map.width*.5f,center.y-session.Map.height*.5f);
                                    Invoke(board,"LateUpdate");Send(Top(new Point(center.x+.75f,center.y)));
                                    Check(board.Hovered==building.id,"A visible footprint remains selectable when the building center is clipped");
                                    Send(Top(new Point(center.x+2.3f,center.y)));
                                    Check(board.Hovered!=building.id,"Clipped centers cannot extend building assistance beyond their visible footprint");
                                    board.Fit();Invoke(board,"LateUpdate");
                                }
                                worker.position=new Point(center.x+12,center.y+12);session.Match.Entities.Remove(building.id);Refresh();
                            }
                        }
                        board.SetBaseTargeting(false);
                    }
                }
                Debug.Log($"FRONTIER_RESOURCE_PICKING passed: {checks} checks, {captures} views; no player build");
                void Send(Ray ray,bool down=false,bool up=false,bool order=false)=>send.Invoke(input,new object[]{ray,down,up,order,false});
                Material Ground()=>board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                float HoverAmount(int id)
                {
                    var renderers=(System.Collections.Generic.Dictionary<int,Renderer>)typeof(BoardView).GetField("resourceRenderers",Private).GetValue(board);
                    var block=new MaterialPropertyBlock();renderers[id].GetPropertyBlock(block);return block.GetFloat("_HoverAmount");
                }
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("RESOURCE_PICK_PASS: "+message);}
                void Capture(string name,Point point)
                {
                    var center=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point)+.5f,point.y));
                    float size=board.CellScale*4;
                    preview.camera.orthographic=true;preview.camera.orthographicSize=size;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+board.transform.up*size*2-board.transform.forward*size*2.4f;preview.camera.transform.LookAt(center,board.transform.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);var image=preview.EndStaticPreview();
                    File.WriteAllBytes("TestResults/ResourcePicking/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                }
            }
            finally
            {
                if(hadBasePreference)PlayerPrefs.SetInt(BoardView.BaseTargetingKey,basePreference);else PlayerPrefs.DeleteKey(BoardView.BaseTargetingKey);
                PlayerPrefs.Save();
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
