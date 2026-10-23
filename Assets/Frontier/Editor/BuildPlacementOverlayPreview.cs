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
    public static class BuildPlacementOverlayPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        public static void RunBatch(){BuildPlacementValidityChecks.Run();Run();RefineryPlacementPreview.Run();}

        [MenuItem("Frontier/Validate construction placement zones")]
        public static void Run()
        {
            const string output="TestResults/BuildPlacementOverlay";Directory.CreateDirectory(output);
            var fixture=new GameObject("Construction zone fixture");var boardObject=new GameObject("Construction zone board");
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
                    var worker=session.View.entities.First(e=>e.owner==session.View.player&&session.Catalog[e.definition].role=="worker");
                    var home=session.View.entities.First(e=>e.owner==session.View.player&&session.Catalog[e.definition].role=="hq");
                    var mineral=session.View.resources.First(r=>r.kind=="mineral");
                    var gas=session.View.resources.First(r=>r.kind=="catalyst");
                    var hq=session.Catalog.Role(faction,"hq");var extractor=session.Catalog.Role(faction,"extractor");
                    session.Select(worker.id,false);board.Pointer=null;Frame();
                    Check(!Active(),"No placement overlay before choosing a building");
                    foreach(var type in session.Catalog.Definition.archetypes.Where(a=>a.faction==faction&&a.building))
                    {
                        board.Pointer=null;
                        session.ChooseBuild(type.id);Frame();
                        Check(!Active(),"Choosing "+type.id+" leaves distant terrain untouched until a building is aimed");
                        board.Pointer=mineral.position;Frame();
                        Check(Active(),"Aiming "+type.id+" activates local placement zones");
                        Check(AreaFollowsGhost(type.footprint),"The local zone follows the building center and covers its corners with a bounded fade: "+type.id);
                        var overlay=Overlay();var texture=overlay.Texture;
                        Check(Ground().GetTexture("_BuildPlacementTex")==texture&&texture.width==session.Map.width&&texture.height==session.Map.height,
                            "Terrain receives this map's origin-cell texture: "+map+" / "+type.id);
                        Check(TextureMatchesPreview(type.footprint),"Every visible origin matches ghost legality and fog stays unmarked: "+map+" / "+type.id);
                        Check(!overlay.Update(session),"An unchanged observed snapshot does not upload the placement texture again");
                        if(type.role!="extractor")
                        {
                            var aim=new Point(mineral.position.x+.37f,mineral.position.y-.21f);
                            Check(Same(session.BuildPlacement(aim,mineral.id),Floor(aim)),"Ordinary construction keeps the chosen grid origin without deposit snap: "+type.id);
                        }
                    }

                    // Expose the fixture terrain to isolate geometry from the player's
                    // opening vision; production code still only sees PlayerView.
                    var naturalVisibility=(byte[])session.View.visibility.Clone();
                    for(int i=0;i<session.View.visibility.Length;i++)session.View.visibility[i]=2;
                    session.View.tick++;session.ChooseBuild(hq.id);Frame();
                    var blocked=MineralOrigin();var legal=OpenNear(mineral.position,hq.footprint);
                    Check(Pixel(blocked).g==255&&!session.CanPreviewBuild(blocked,hq.footprint),"Main base marks mineral overlap before confirmation: "+map+" / "+faction);
                    Check(Pixel(legal).r==255&&Pixel(legal).a==255,"Main base shows a nearby legal origin");
                    Check(Pixel(home.position).g==255,"Existing headquarters blocks the new footprint");
                    Check(Pixel(new Point(session.Map.width-1,session.Map.height-1)).g==255,"Visible map edges reserve the entire building footprint");
                    board.Pointer=new Point(blocked.x+.75f,blocked.y+.25f);Frame();
                    var ghost=Field<Transform>(board,"preview");
                    Check(Same(session.BuildPlacement(board.Pointer.Value,mineral.id),blocked)
                        &&Mathf.Abs(ghost.localPosition.x-blocked.x-hq.footprint*.5f)<.001f
                        &&Mathf.Abs(ghost.localPosition.z-blocked.y-hq.footprint*.5f)<.001f,
                        "Invalid base footprint remains where aimed rather than jumping away from minerals");
                    Check(ghost.GetComponent<Renderer>().sharedMaterial==Field<Material>(board,"red"),"The same mineral restriction colors the placement ghost red");
                    Check(AreaFollowsGhost(hq.footprint),"Local placement guidance follows the blocked base ghost");
                    board.Pointer=new Point(legal.x+.35f,legal.y+.65f);Frame();
                    Check(ghost.GetComponent<Renderer>().sharedMaterial==Field<Material>(board,"highlight"),"Crossing the marked boundary makes the same base ghost valid");
                    Check(AreaFollowsGhost(hq.footprint),"Moving the base moves the local guidance without leaving the old origin highlighted");
                    if(faction=="industrial")Capture(map+"-base-zones",mineral.position,16);

                    var entities=session.View.entities;
                    var enemyType=session.Catalog.Role(faction=="industrial"?"biological":"industrial","worker");
                    var intruder=new VisibleEntity{id=19001,owner=1,definition=enemyType.id,health=enemyType.health,complete=true,
                        position=new Point(legal.x+hq.footprint*.5f,legal.y+hq.footprint*.5f)};
                    session.View.entities=entities.Concat(new[]{intruder}).ToArray();ChangedView();
                    Check(Pixel(legal).g==255,"An observed enemy entering the footprint refreshes the overlay");
                    intruder.owner=session.View.player;ChangedView();
                    Check(Pixel(legal).r==255,"Friendly units keep a construction site available for automatic clearance");
                    intruder.owner=1;intruder.remembered=true;ChangedView();
                    Check(Pixel(legal).r==255,"A stale mobile enemy observation does not invent a current blocker");
                    session.View.entities=entities;ChangedView();
                    Check(Pixel(legal).r==255,"Removing the observed blocker restores the legal zone");

                    int origin=Cell(legal),corner=Cell(new Point(legal.x+hq.footprint-1,legal.y+hq.footprint-1));
                    session.View.visibility[corner]=1;ChangedView();
                    Check(Pixel(legal).g==255,"A visible origin becomes blocked when the full footprint loses current vision");
                    session.View.visibility[origin]=1;ChangedView();
                    Check(Pixel(legal).a==0,"Explored fog is unmarked rather than revealing hidden occupancy");
                    session.View.visibility[origin]=0;ChangedView();
                    Check(Pixel(legal).a==0,"Unexplored origins remain unmarked");
                    session.View.visibility[origin]=session.View.visibility[corner]=2;ChangedView();
                    Check(Pixel(legal).r==255,"Regaining vision refreshes the permitted zone");

                    session.ChooseBuild(extractor.id);board.Pointer=gas.position;board.HoveredResource=gas.id;Frame();
                    var gasOrigin=Floor(new Point(gas.position.x-extractor.footprint*.5f,gas.position.y-extractor.footprint*.5f));
                    Check(Same(session.BuildPlacement(gas.position,gas.id),gasOrigin),"The existing refinery-only gas snap is preserved");
                    Check(Pixel(gasOrigin).r==255&&TextureMatchesPreview(extractor.footprint),"Changing building role immediately replaces the mask with extractor zones");
                    Check(Pixel(gas.position).r==255,"Aiming at the gas itself is marked legal when its snapped refinery footprint is legal");
                    Check(AreaFollowsGhost(extractor.footprint),"Refinery guidance follows the snapped building center");
                    if(faction=="industrial")Capture(map+"-extractor-zones",gas.position,14);
                    session.View.winner=0;Frame();
                    Check(!Active()&&!Field<Transform>(board,"preview").gameObject.activeSelf,"A match result hides both placement zones and the active construction ghost");
                    session.View.winner=-1;Frame();
                    Check(Active()&&Field<Transform>(board,"preview").gameObject.activeSelf,"Restoring an active observed match restores placement zones and ghost");
                    session.ChooseBuild(hq.id);board.Pointer=null;Frame();
                    Check(!Active()&&!Field<Transform>(board,"preview").gameObject.activeSelf,"Clearing the terrain pointer hides local guidance and the unconfirmed building");
                    board.Pointer=gas.position;Frame();
                    Check(Active()&&Pixel(gasOrigin).g==255,"Aiming the base again restores local guidance with its resource exclusions");
                    session.Pause("menu",true);Frame();Check(!Active(),"Pause hides placement zones");
                    session.Pause("menu",false);Frame();Check(Active(),"Resuming restores the selected blueprint's zones");
                    board.SetVisible(false);Frame();Check(!Active(),"Hidden board suppresses placement zones");
                    board.SetVisible(true);Frame();Check(!Active(),"Showing the board waits for a fresh terrain pointer before restoring local guidance");
                    board.Pointer=legal;Frame();Check(Active(),"A fresh terrain aim restores local guidance after showing the board");
                    session.CancelMode();Frame();Check(!Active(),"Cancelling the blueprint removes placement zones");
                    session.ChooseBuild(hq.id);Frame();Check(Active(),"A new blueprint reactivates zones after cancellation");
                    session.View.visibility=naturalVisibility;ChangedView();
                    Check(TextureMatchesPreview(hq.footprint),"Restoring the real observed footprint cannot retain full-map fixture visibility");
                    session.LeaveMatch();Frame();Check(!Active(),"Leaving the match removes placement zones");

                    void Frame()=>Call(board,"LateUpdate");
                    void ChangedView(){session.View.tick++;Frame();}
                    Material Ground()=>Field<Material>(board,"ground");
                    bool Active()=>Ground().GetFloat("_BuildPlacementActive")>.5f;
                    BuildPlacementOverlay Overlay()=>Field<BuildPlacementOverlay>(board,"buildPlacementOverlay");
                    int Cell(Point p)=>(int)p.y*session.Map.width+(int)p.x;
                    Color32 Pixel(Point p)=>Overlay().Texture.GetPixels32()[Cell(p)];
                    bool AreaFollowsGhost(int size)
                    {
                        var area=Ground().GetVector("_BuildPlacementArea");var current=Field<Transform>(board,"preview").localPosition;
                        float cornerDistance=size*Mathf.Sqrt(.5f);
                        return Mathf.Abs(area.x-current.x)<.001f&&Mathf.Abs(area.y-current.z)<.001f
                            &&area.z>=cornerDistance&&area.z<=cornerDistance+1&&area.w>area.z&&area.w-area.z<=4.01f;
                    }
                    bool TextureMatchesPreview(int size)
                    {
                        var pixels=Overlay().Texture.GetPixels32();
                        bool snapsToGas=session.Catalog[session.BuildDefinition].role=="extractor";
                        for(int y=0;y<session.Map.height;y++)for(int x=0;x<session.Map.width;x++)
                        {
                            int i=y*session.Map.width+x;var color=pixels[i];
                            if(session.View.visibility[i]!=2){if(color.a!=0)return false;continue;}
                            var placement=snapsToGas?session.BuildPlacement(new Point(x+.5f,y+.5f)):new Point(x,y);
                            bool valid=session.CanPreviewBuild(placement,size);
                            if(color.a!=255||color.b!=0||color.r!=(valid?255:0)||color.g!=(valid?0:255))return false;
                        }
                        return true;
                    }
                    Point MineralOrigin()
                    {
                        var terrain=new GridMap(session.Map);
                        for(int dy=0;dy<hq.footprint;dy++)for(int dx=0;dx<hq.footprint;dx++)
                        {
                            var originPoint=new Point(Mathf.Floor(mineral.position.x)-dx,Mathf.Floor(mineral.position.y)-dy);
                            if(!terrain.CanPlace(originPoint,hq.footprint))continue;
                            bool overlaps=session.View.entities.Any(e=>session.Catalog[e.definition].building
                                &&originPoint.x<e.position.x+session.Catalog[e.definition].footprint&&originPoint.x+hq.footprint>e.position.x
                                &&originPoint.y<e.position.y+session.Catalog[e.definition].footprint&&originPoint.y+hq.footprint>e.position.y);
                            if(!overlaps)return originPoint;
                        }
                        throw new Exception("Fixture needs an otherwise buildable mineral footprint: "+map);
                    }
                    Point OpenNear(Point target,int size)
                    {
                        Point best=default;float distance=float.MaxValue;
                        for(int y=0;y<session.Map.height;y++)for(int x=0;x<session.Map.width;x++)
                        {
                            var candidate=new Point(x,y);float next=candidate.Distance(target);
                            if(next<distance&&session.CanPreviewBuild(candidate,size)){best=candidate;distance=next;}
                        }
                        if(distance==float.MaxValue)throw new Exception("Fixture needs a legal construction origin: "+map);
                        return best;
                    }
                    void Capture(string name,Point point,float radius)
                    {
                        var focus=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point),point.y));
                        preview.camera.orthographic=true;preview.camera.orthographicSize=board.CellScale*radius;
                        preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                        preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                        preview.camera.transform.position=focus+new Vector3(.15f,.7f,-.45f);preview.camera.transform.LookAt(focus);
                        preview.BeginStaticPreview(new Rect(0,0,1300,1000));preview.Render(true);
                        var image=preview.EndStaticPreview();File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);captures++;
                    }
                }
                Debug.Log($"FRONTIER_BUILD_PLACEMENT_OVERLAY passed: {checks} checks, {captures} views; all maps, factions and buildings; texture/ghost agreement, live blockers, fog privacy, lifecycle and unchanged snap; no physical Quest validation");
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
