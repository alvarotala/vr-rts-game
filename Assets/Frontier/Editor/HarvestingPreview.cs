using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Renders the runtime console and checks its resource interaction without building a player.
    public static class HarvestingPreview
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target,string name) => target.GetType().GetMethod(name,Private).Invoke(target,null);
        private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Private).GetValue(target);
        private static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }

        [MenuItem("Frontier/Preview harvesting panel")]
        public static void Render()=>Render(false);

        [MenuItem("Frontier/Preview full command panel")]
        public static void RenderEnglish()=>Render(true);

        [MenuItem("Frontier/Preview match HUD")]
        public static void RenderHud()=>Render(true,"TestResults/MatchHud");

        private static void Render(bool fullPanel,string outputDirectory=null)
        {
            string output=outputDirectory??(fullPanel?"TestResults/English":"TestResults/Harvesting");
            Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();
            var fixture=new GameObject("Harvesting preview fixture");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var board=fixture.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                if(fullPanel)
                {
                    Capture("setup-rift","Forest Watch","selection");
                    session.Faction="biological";session.EnemyFaction="industrial";session.Difficulty="easy";session.LoadMap("training");
                    Capture("setup-training","Neon Loop","selection");
                    session.Faction="industrial";session.EnemyFaction="biological";session.Difficulty="normal";session.LoadMap("rift");
                }
                session.StartMatch();
                var workers=session.View.entities.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").ToArray();
                var patch=session.View.resources.First(r=>r.kind=="mineral");
                foreach(var w in workers)session.Selection.Add(w.id);
                session.Context(patch.position,0,patch.id);
                Check(session.View.resources.First(r=>r.id==patch.id).assignedWorkers==2,"Context order did not distribute workers");
                session.Select(0,false,patch.id);
                Check(session.SelectedResource==patch.id&&session.Selection.Count==0,"Resource selection not retained");
                var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                Capture("mineral","Workers 2/2");
                session.Select(hq.id,false);Capture("base","Workers 6/8");
                foreach(var resource in session.Match.Resources.Where(r=>r.kind=="mineral"&&r.position.Distance(patch.position)<14&&r.id!=patch.id))resource.remaining=0;
                session.Selection.Clear();
                foreach(var w in workers)session.Selection.Add(w.id);
                session.Context(patch.position,0,patch.id);session.Select(0,false,patch.id);
                Capture("excess","Workers 6/2 (excess)");

                var gas=session.Match.Resources.First(r=>r.kind=="catalyst"&&r.position.x<30);
                session.Select(0,false,gas.id);Capture("gas-unbuilt","Workers 0/3");
                var type=session.Catalog.Role(session.Faction,"extractor");
                var extractor=new EntityState{id=1000,owner=0,definition=type.id,position=new Point(gas.position.x-type.footprint*.5f,gas.position.y-type.footprint*.5f),health=type.health};
                session.Match.Entities.Add(extractor.id,extractor);session.Match.Map.Occupy(extractor.position,type.footprint,extractor.id);
                // Stop refreshes PlayerView, then select three workers and click the extractor's edge.
                session.Selection.Clear();foreach(var w in workers)session.Selection.Add(w.id);session.Send(CommandKind.Stop);
                session.Selection.Clear();foreach(var w in workers.Take(3))session.Selection.Add(w.id);
                session.Context(extractor.position,extractor.id,0);
                Check(session.View.resources.First(r=>r.id==gas.id).assignedWorkers==3,"Extractor footprint context did not gather gas");
                session.Select(extractor.id,false);Capture("gas","Workers 3/3");
                if(fullPanel)
                {
                    foreach(string faction in new[]{"industrial","biological"})
                    {
                        session.Faction=faction;session.LoadMap("training");session.StartMatch();
                        var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                        session.Select(worker.id,false);Capture(faction+"-worker",session.Catalog.Role(faction,"barracks").name,"status");
                        var baseEntity=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                        session.Select(baseEntity.id,false);session.Send(CommandKind.Train,worker.definition);
                        Capture(faction+"-production",session.Catalog[worker.definition].name,"productionName");
                        // A completed technology building exposes the real research buttons and queue.
                        var tech=session.Catalog.Role(faction,"tech");
                        session.Match.Entities.Add(1000,new EntityState{id=1000,owner=0,definition=tech.id,position=new Point(20,20),health=tech.health});
                        session.Match.Players[0].minerals=1000;session.Match.Players[0].catalyst=1000;
                        session.Send(CommandKind.Stop);session.Select(1000,false);session.Send(CommandKind.Research,"attack");
                        Capture(faction+"-research","Firepower I","productionName");
                    }
                    var headquarters=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    session.Select(headquarters.id,false);session.Send(CommandKind.Cancel);
                    int unitId=2000;
                    foreach(string role in new[]{"scout","melee","ranged","siege"})
                    {
                        var unit=session.Catalog.Role(session.Faction,role);
                        session.Match.Entities.Add(unitId,new EntityState{id=unitId++,owner=0,definition=unit.id,position=new Point(20,20),health=unit.health});
                    }
                    session.Select(session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building).id,false);session.Send(CommandKind.Stop);
                    session.Selection.Clear();foreach(var unit in session.View.entities.Where(e=>e.owner==0&&!session.Catalog[e.definition].building))session.Selection.Add(unit.id);
                    Check(session.View.supply==session.View.capacity,"Full supply fixture not saturated");
                    Capture("mixed-army","10","selection");
                    session.Select(2002,false);session.Match.Entities[2002].health=3;session.Send(CommandKind.Stop);
                    Capture("damaged-soldier","3 / ","selectionHealth");
                    var supply=session.Catalog.Role(session.Faction,"supply");
                    session.Match.Entities.Add(2100,new EntityState{id=2100,owner=0,definition=supply.id,position=new Point(22,24),health=10,complete=false,remaining=supply.duration/2});
                    session.Send(CommandKind.Stop);session.Select(2100,false);
                    Capture("construction","UNDER CONSTRUCTION · 50%","selectionDetails");
                    session.Pause("menu",true);Capture("paused","MATCH PAUSED","headline",true);
                    session.Pause("menu",false);session.Send(CommandKind.Surrender);Capture("finished","DEFEAT","headline");
                }
                Debug.Log("FRONTIER_HARVESTING_PREVIEW passed: context allocation, resource selection, base totals, excess, extractor footprint; "+(fullPanel?18:5)+" panels rendered");

                void Capture(string name,string expected,string field="harvesting",bool settings=false)
                {
                    var panelObject=new GameObject("Console preview");
                    var panel=panelObject.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                    Invoke(panel,"Start");
                    if(!session.Running)typeof(CommandPanel).GetMethod("Show",Private).Invoke(panel,new object[]{CommandPanel.MenuPage.MatchSetup});
                    if(settings)panel.ToggleMenu();
                    Invoke(panel,"LateUpdate");
                    if(panel.Page==CommandPanel.MenuPage.Actions)
                    {
                        var selected=session.View.entities.Where(e=>session.Selection.Contains(e.id)).ToArray();
                        Check(Field<Text>(panel,"selection").text==(selected.Length>0?selected.Length.ToString():""),"Selection header count does not match the selected entities");
                        if(selected.Length>0)
                        {
                            var names=Field<Text[]>(panel,"selectionNames").Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text).OrderBy(t=>t).ToArray();
                            var expectedNames=selected.Select(e=>e.definition).Distinct().Select(id=>Localization.Text(session.Catalog[id].name)).OrderBy(t=>t).ToArray();
                            Check(names.SequenceEqual(expectedNames),"Selected item names are missing from the visible selection cards");
                        }
                        else
                        {
                            var resource=session.View.resources.FirstOrDefault(r=>r.id==(session.SelectedResource>0?session.SelectedResource:board.HoveredResource));
                            var pointed=session.View.entities.FirstOrDefault(e=>e.id==board.Hovered&&!e.remembered);
                            string title=resource!=null?(resource.kind=="mineral"?"Minerals":"Gas"):pointed!=null?session.Catalog[pointed.definition].name:null;
                            if(title!=null)
                            {
                                var fallback=Field<Text>(panel,"selectionFallbackTitle");
                                Check(fallback.gameObject.activeInHierarchy&&fallback.text==Localization.Text(title),"Pointed item name is missing from the visible fallback row");
                            }
                        }
                        Check(Field<Text>(panel,"mineralValue").text==session.View.minerals.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),"Minerals counter does not match player view");
                        Check(Field<Text>(panel,"gasValue").text==session.View.catalyst.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),"Gas counter does not match player view");
                        Check(Field<Text>(panel,"supplyValue").text==$"{session.View.supply} / {session.View.capacity}","Supply counter does not match player view");
                        Check(!Field<RectTransform>(panel,"actions").Cast<Transform>().Any(t=>t.name=="Pause"||t.name.StartsWith("Group ")),"Match and group controls mixed with context actions");
                        if(name=="base")
                        {
                            int minerals=session.View.minerals;
                            Click("Train "+session.Catalog.Role(session.Faction,"worker").name);
                            Check(session.View.entities.First(e=>e.id==selected[0].id).queue.Length==1,"HUD production button did not queue a worker");
                            Click("Current production");Check(session.View.minerals==minerals,"Queue-item cancellation did not refund production");
                        }
                        if(name=="industrial-worker")
                        {
                            string previousMessage=session.Message;
                            Click("Build "+session.Catalog.Role(session.Faction,"supply").name);
                            Check(session.BuildDefinition==session.Catalog.Role(session.Faction,"supply").id,"HUD construction button did not select its building");session.CancelMode();
                            session.Announce(previousMessage);Refresh();
                        }
                        if(name=="mixed-army")
                        {
                            var ids=session.Selection.ToArray();Click("Group 1",true);session.Select(0,false);Click("Group 1");
                            Check(session.Selection.SetEquals(ids)&&session.GroupSize(0)==10,"HUD control group did not save and restore selection");
                            Check(Field<Text[]>(panel,"groupCounts")[0].text=="10 assigned","HUD group count did not update");
                            Check(Field<Text>(panel,"supplyValue").color!=ConsoleTheme.Ink&&!Field<Text>(panel,"supplyHint").gameObject.activeInHierarchy,
                                "Full population keeps its counter warning without the removed subtitle");
                        }
                    }
                    var line=Field<Text>(panel,field);Check(line.text.Contains(expected),name+": "+line.text);
                    preview.AddSingleGO(panelObject);
                    SavePanel(panel,output+"/"+name+".png",preview);
                    void Click(string title,bool hold=false)
                    {
                        Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==title&&t.gameObject.activeInHierarchy);
                        var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                        Check(panel.Point(ray,true,false,assignGroup:hold),"HUD pointer press missed "+title);
                        Check(panel.Point(ray,false,true,assignGroup:hold),"HUD pointer release missed "+title);Refresh();
                    }
                    void Refresh(){typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");}
                    Object.DestroyImmediate(panelObject);
                }
            }
            finally { Object.DestroyImmediate(fixture);preview.Cleanup(); }
        }
        internal static void SavePanel(CommandPanel panel,string path,PreviewRenderUtility preview)
        {
            SaveCanvas(panel.transform,Field<RectTransform>(panel,"root"),path,preview);
        }
        internal static void SaveCanvas(Transform container,RectTransform root,string path,PreviewRenderUtility preview,int width=1600,int height=0,bool preservePose=false)
        {
            foreach(var label in container.GetComponentsInChildren<Text>().Where(t=>!t.resizeTextForBestFit))
                Check(label.preferredHeight<=label.rectTransform.rect.height+.5f,"Panel text overflows: "+label.text);
            int effectiveHeight=height>0?height:root.rect.height>526?960:800;
            if(!preservePose)
            {
                root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;root.localScale=Vector3.one*.001f;
                Canvas.ForceUpdateCanvases();
                var bounds=new Bounds(root.rect.center,new Vector3(root.rect.width,root.rect.height,0));
                var corners=new Vector3[4];
                foreach(var graphic in container.GetComponentsInChildren<Graphic>().Where(g=>g.isActiveAndEnabled))
                {
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach(var corner in corners)bounds.Encapsulate(root.InverseTransformPoint(corner));
                }
                var size=Vector3.Scale(bounds.size,root.lossyScale);
                float aspect=width/(float)effectiveHeight;
                preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(.30f,Mathf.Abs(size.y)*.5f+.03f,(Mathf.Abs(size.x)*.5f+.03f)/aspect);preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
                preview.camera.transform.SetPositionAndRotation(root.TransformPoint(bounds.center)+Vector3.back*2,Quaternion.identity);
            }
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.018f,.031f,.055f);
            // Preview cameras do not submit world-space Canvas batches. Draw the actual UI
            // meshes with their font textures, preserving hierarchy order and runtime layout.
            var meshes=new List<Mesh>();var materials=new List<Material>();
            preview.BeginStaticPreview(new Rect(0,0,width,effectiveHeight));Canvas.ForceUpdateCanvases();
            foreach(var graphic in container.GetComponentsInChildren<Graphic>())
            {
                graphic.Rebuild(CanvasUpdate.PreRender);
                var source=graphic.canvasRenderer.GetMesh();if(source==null)continue;
                var mesh=Object.Instantiate(source);meshes.Add(mesh);
                if(mesh.vertexCount==0)continue;
                var material=new Material(graphic.materialForRendering){mainTexture=graphic.mainTexture};materials.Add(material);
                // At an oblique angle transparent distance sorting can draw a sector
                // over its label. Reproduce Canvas hierarchy order in these mesh previews.
                if(preservePose)material.renderQueue=3000+meshes.Count;
                if(graphic is Text)material.SetVector("_TextureSampleAdd",new Vector4(1,1,1,0));
                var matrix=Matrix4x4.Translate(-preview.camera.transform.forward*.0001f*meshes.Count)*graphic.transform.localToWorldMatrix;
                preview.DrawMesh(mesh,matrix,material,0);
            }
            Check(meshes.Sum(m=>m.vertexCount)>100,"Console did not generate UI geometry");
            preview.Render(true);
            var image=preview.EndStaticPreview();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            foreach(var mesh in meshes)Object.DestroyImmediate(mesh);foreach(var material in materials)Object.DestroyImmediate(material);
        }
    }
}
