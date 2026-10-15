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

        private static void Render(bool fullPanel)
        {
            string output=fullPanel?"TestResults/English":"TestResults/Harvesting";
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
                    Capture("setup-rift","The Rift","selection");
                    session.Faction="biological";session.EnemyFaction="industrial";session.Difficulty="easy";session.LoadMap("training");
                    Capture("setup-training","Training Grounds","selection");
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
                var extractor=new EntityState{id=1000,owner=0,definition=type.id,position=new Point(11,24),health=type.health};
                session.Match.Entities.Add(extractor.id,extractor);session.Match.Map.Occupy(extractor.position,type.footprint,extractor.id);
                // Stop refreshes PlayerView, then select three workers and click the extractor's edge.
                session.Selection.Clear();foreach(var w in workers)session.Selection.Add(w.id);session.Send(CommandKind.Stop);
                session.Selection.Clear();foreach(var w in workers.Take(3))session.Selection.Add(w.id);
                session.Context(new Point(11,24),extractor.id,0);
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
                        Capture(faction+"-production","Queue 1: "+session.Catalog[worker.definition].name,"selection");
                        // A completed technology building exposes the real research buttons and queue.
                        var tech=session.Catalog.Role(faction,"tech");
                        session.Match.Entities.Add(1000,new EntityState{id=1000,owner=0,definition=tech.id,position=new Point(20,20),health=tech.health});
                        session.Match.Players[0].minerals=1000;session.Match.Players[0].catalyst=1000;
                        session.Send(CommandKind.Stop);session.Select(1000,false);session.Send(CommandKind.Research,"attack");
                        Capture(faction+"-research","Firepower I","selection");
                    }
                    session.Pause("menu",true);Capture("paused","MATCH PAUSED","headline",true);
                    session.Pause("menu",false);session.Send(CommandKind.Surrender);Capture("finished","DEFEAT","headline");
                }
                Debug.Log("FRONTIER_HARVESTING_PREVIEW passed: context allocation, resource selection, base totals, excess, extractor footprint; "+(fullPanel?15:5)+" panels rendered");

                void Capture(string name,string expected,string field="harvesting",bool settings=false)
                {
                    var panelObject=new GameObject("Console preview");
                    var panel=panelObject.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                    Invoke(panel,"Start");
                    if(!session.Running)typeof(CommandPanel).GetMethod("Show",Private).Invoke(panel,new object[]{CommandPanel.MenuPage.MatchSetup});
                    if(settings)panel.ToggleMenu();
                    Invoke(panel,"LateUpdate");
                    var line=Field<Text>(panel,field);Check(line.text.Contains(expected),name+": "+line.text);
                    preview.AddSingleGO(panelObject);
                    SavePanel(panel,output+"/"+name+".png",preview);
                    Object.DestroyImmediate(panelObject);
                }
            }
            finally { Object.DestroyImmediate(fixture);preview.Cleanup(); }
        }
        internal static void SavePanel(CommandPanel panel,string path,PreviewRenderUtility preview)
        {
            foreach(var label in panel.GetComponentsInChildren<Text>())
                Check(label.preferredHeight<=label.rectTransform.rect.height+.5f,"Panel text overflows: "+label.text);
            var root=Field<RectTransform>(panel,"root");root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;root.localScale=Vector3.one*.001f;
            preview.camera.orthographic=true;preview.camera.orthographicSize=.30f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
            preview.camera.transform.SetPositionAndRotation(new Vector3(0,0,-2),Quaternion.identity);
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.018f,.031f,.055f);
            // Preview cameras do not submit world-space Canvas batches. Draw the actual UI
            // meshes with their font textures, preserving hierarchy order and runtime layout.
            var meshes=new List<Mesh>();var materials=new List<Material>();
            preview.BeginStaticPreview(new Rect(0,0,1600,800));Canvas.ForceUpdateCanvases();
            foreach(var graphic in panel.GetComponentsInChildren<Graphic>())
            {
                graphic.Rebuild(CanvasUpdate.PreRender);
                var source=graphic.canvasRenderer.GetMesh();if(source==null)continue;
                var mesh=Object.Instantiate(source);meshes.Add(mesh);
                if(mesh.vertexCount==0)continue;
                var material=new Material(graphic.materialForRendering){mainTexture=graphic.mainTexture};materials.Add(material);
                if(graphic is Text)material.SetVector("_TextureSampleAdd",new Vector4(1,1,1,0));
                var matrix=Matrix4x4.Translate(Vector3.back*.0001f*meshes.Count)*graphic.transform.localToWorldMatrix;
                preview.DrawMesh(mesh,matrix,material,0);
            }
            Check(meshes.Sum(m=>m.vertexCount)>100,"Console did not generate UI geometry");
            preview.Render(true);
            var image=preview.EndStaticPreview();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            foreach(var mesh in meshes)Object.DestroyImmediate(mesh);foreach(var material in materials)Object.DestroyImmediate(material);
        }
    }
}
