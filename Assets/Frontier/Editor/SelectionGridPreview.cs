using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class SelectionGridPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        public static void RunValidation(){Render();ControlGroupPreview.RunValidation();ProductionQueuePreview.Render();}
        [MenuItem("Frontier/Preview and check selection grid")]
        public static void Render()
        {
            const string output="TestResults/SelectionGrid";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Selection fixture");var console=new GameObject("Selection console");int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Call(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");preview.AddSingleGO(console);
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Panel=panel;input.Board=board;input.Quest=quest;
                Check(!Grid().gameObject.activeInHierarchy,"No selection grid before a match");
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();
                    int id=1000;
                    foreach(var type in session.Catalog.Definition.archetypes.Where(t=>t.faction==faction))
                    {
                        int count=type.building?(type.role=="barracks"?4:3):type.role=="worker"?32:3;
                        for(int i=0;i<count;i++)session.Match.Entities.Add(id,new EntityState{id=id++,owner=0,definition=type.id,position=new Point(20,20),health=type.health});
                    }
                    Call(session,"RefreshView");
                    var all=session.View.entities.Where(e=>e.owner==0).Select(e=>e.id).ToArray();
                    Check(all.Length==70,"Maximum roster has 50 units and 20 buildings");
                    var workers=session.View.entities.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="worker").Select(e=>e.id).ToArray();
                    var hqs=session.View.entities.Where(e=>e.owner==0&&session.Catalog[e.definition].role=="hq").Select(e=>e.id).ToArray();
                    Select(all);Capture(faction+"-mixed");
                    var seen=new System.Collections.Generic.HashSet<int>();
                    do
                    {
                        foreach(var rect in Cards()){int member=int.Parse(rect.name.Substring("Selection item ".Length));Check(seen.Add(member),"Each selection member appears on exactly one page");Check(rect.rect.width==rect.rect.height,"Portrait targets are square");}
                        if(!Has("Next selection page"))break;Click("Next selection page");
                    }while(true);
                    Check(seen.SetEquals(all),"Every selected unit and building is reachable");Capture(faction+"-last-page");
                    Check(session.Selection.SetEquals(all),"Paging does not change selection");
                    Select(workers.Take(3).Concat(hqs).ToArray());
                    int worker=workers[0];Tap(worker);Check(session.Selection.Count==3+hqs.Length,"First tap preserves the cohort during double-click window");
                    Flush();Check(session.Selection.SetEquals(new[]{worker}),"Single tap isolates its entity");Refresh();Capture(faction+"-single");
                    // More than one page, plus unselected allies of the same class nearby.
                    var cohort=workers.Skip(1).Concat(hqs).ToArray();Select(cohort);worker=workers[1];ShowCard(worker);Tap(worker);Tap(worker);Refresh();
                    Check(session.Selection.SetEquals(workers.Skip(1)),"Double tap selects same definition across all pages, excludes unselected nearby allies");
                    Select(workers.Take(2).Concat(hqs).ToArray());ShowCard(hqs[0]);Tap(hqs[0]);Tap(hqs[0]);Refresh();
                    Check(session.Selection.SetEquals(hqs),"Double tap also filters buildings by exact definition");Capture(faction+"-buildings");
                    Select(workers.Take(3).ToArray());var ray=RayFor("Selection item "+workers[0]);panel.Point(ray,true,false);
                    Set(panel,"pressedAt",Time.unscaledTime-.4f);panel.Point(ray,false,false);Capture(faction+"-remove-progress",false);ray=RayFor("Selection item "+workers[0]);
                    Set(panel,"pressedAt",Time.unscaledTime-.7f);panel.Point(ray,false,false);Refresh();
                    Check(!session.Selection.Contains(workers[0])&&session.Selection.Count==2,"Long hold removes one member before release");
                    panel.Point(ray,false,true);Flush();Check(session.Selection.SetEquals(workers.Skip(1).Take(2)),"Hold release never fires the short click");
                    Select(new[]{workers[0]});ray=RayFor("Selection item "+workers[0]);panel.Point(ray,true,false);Set(panel,"pressedAt",Time.unscaledTime-.7f);panel.Point(ray,false,true);Refresh();
                    Check(session.Selection.Count==0&&!Grid().gameObject.activeInHierarchy,"Removing final member hides the grid");
                    Select(workers.Take(3).ToArray());Tap(workers[0]);session.Select(hqs[0],false);Refresh();Flush();Check(session.Selection.SetEquals(new[]{hqs[0]}),"External selection cancels delayed click");
                    Select(workers.Take(3).ToArray());Tap(workers[0]);panel.ToggleMenu();Flush();Check(session.Selection.Count==3&&!Grid().gameObject.activeInHierarchy,"Pause cancels pending selection");panel.ToggleMenu();Refresh();
                    Select(workers.Take(3).ToArray());Tap(workers[0]);panel.CancelPointerInteraction();Flush();Check(session.Selection.Count==3,"Focus/input cancellation clears pending click");
                    Select(workers.Take(3).ToArray());ray=RayFor("Selection item "+workers[0]);panel.Point(ray,true,false);
                    panel.Point(new Ray(Vector3.one*100,Vector3.up),false,false);panel.Point(ray,false,true);Flush();Check(session.Selection.Count==3,"Leaving a pressed card cancels the gesture");
                    Select(workers.Take(3).ToArray());Tap(workers[0]);session.Match.Entities.Remove(workers[0]);Call(session,"RefreshView");session.Selection.Remove(workers[0]);Refresh();Flush();
                    Check(session.Selection.SetEquals(workers.Skip(1).Take(2)),"Death cancels a pending click without losing survivors");
                    Select(workers.Skip(1).Take(3).ToArray());ray=RayFor("Selection item "+workers[1]);
                    Route(ray,false,false,false,true,false,true);Route(ray,false,false,false,false,true,false);Flush();Refresh();
                    Check(session.Selection.SetEquals(new[]{workers[1]}),"A alias reaches the same single-click selection");
                    Select(workers.Skip(1).Take(3).ToArray());ray=RayFor("Selection item "+workers[1]);
                    Route(ray,true,false,true,false,false,false);Route(ray,false,false,true,true,false,true);Set(panel,"pressedAt",Time.unscaledTime-.7f);
                    Route(ray,false,false,true,false,false,true);Route(ray,false,true,false,false,false,true);Route(ray,false,false,false,false,true,false);
                    Check(session.Selection.SetEquals(workers.Skip(2).Take(2)),"Trigger owns its hold despite overlapping A; release does not click through");
                    Select(hqs.Take(1).ToArray());session.Send(CommandKind.Train,definition:session.Catalog.Role(faction,"worker").id);Refresh();Capture(faction+"-production");
                    Check(Gap(Grid(),Field<RectTransform>(panel,"productionStrip"))>=11,"Selection grid stays separate from production queue");
                    Tap(hqs[0]);session.StartMatch();Refresh();Flush();Check(session.Selection.Count==0,"New match invalidates pending selection even if IDs are reused");
                    session.LeaveMatch();Refresh();Check(!Grid().gameObject.activeInHierarchy,"Leaving a match hides selection grid");
                }
                Debug.Log($"FRONTIER_SELECTION_GRID passed: {checks} checks, {captures} views; no player build");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("SELECTION_GRID_PASS: "+message);}
                void Refresh(){Set(panel,"nextText",0f);Call(panel,"LateUpdate");}
                void Select(int[] ids){panel.CancelPointerInteraction();session.Selection.Clear();session.Selection.UnionWith(ids);Call(panel,"Rebuild");Refresh();}
                RectTransform Grid()=>Field<RectTransform>(panel,"selectionGrid");
                RectTransform[] Cards()=>Grid().GetComponentsInChildren<RectTransform>().Where(r=>r.gameObject.activeInHierarchy&&r.name.StartsWith("Selection item ")).ToArray();
                bool Has(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(r=>r.gameObject.activeInHierarchy&&r.name==name);
                Ray RayFor(string name){var rect=panel.GetComponentsInChildren<RectTransform>().First(r=>r.gameObject.activeInHierarchy&&r.name==name);return new Ray(rect.TransformPoint(rect.rect.center)-rect.forward,rect.forward);}
                void Click(string name){var ray=RayFor(name);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Pointer hits "+name);Refresh();}
                void ShowCard(int member){while(!Has("Selection item "+member)){if(!Has("Next selection page"))throw new Exception("Missing member "+member);Click("Next selection page");}}
                void Tap(int member){var ray=RayFor("Selection item "+member);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Selection card consumes press and release");}
                void Flush(){Call(panel,"TickSelectionClick",Time.unscaledTime+1);Refresh();}
                bool Route(Ray ray,bool down,bool up,bool held,bool aDown,bool aUp,bool aHeld)=>(bool)Call(input,"RoutePanel",ray,down,up,held,aDown,aUp,aHeld,0f);
                float Gap(RectTransform a,RectTransform b)=>-b.anchoredPosition.y-(-a.anchoredPosition.y+a.rect.height);
                void Capture(string name,bool refresh=true)
                {
                    if(refresh)Refresh();Canvas.ForceUpdateCanvases();
                    foreach(var portrait in Grid().GetComponentsInChildren<SelectionPortrait>())portrait.Rebuild(CanvasUpdate.PreRender);
                    Check(Grid().GetComponentsInChildren<SelectionPortrait>().All(p=>p.canvasRenderer.GetMesh()?.vertexCount>0),"Every visible portrait has model geometry");
                    HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;
                }
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
