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
    public static class MatchReportPreview
    {
        private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);

        [MenuItem("Frontier/Preview and check match reports")]
        public static void Run()
        {
            Directory.CreateDirectory("TestResults/Reports");
            var fixture=new GameObject("Report fixture");var console=new GameObject("Report console");var preview=new PreviewRenderUtility();
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");
                var boardObject=new GameObject("Report board");boardObject.transform.SetParent(fixture.transform);
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");board.Placed=true;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                Invoke(panel,"Start");preview.AddSingleGO(console);
                session.StartMatch();var sim=session.Match;
                var bots=new[]{new Strategist(0,AiDifficulty.Normal,session.Catalog,session.Map),new Strategist(1,AiDifficulty.Hard,session.Catalog,session.Map)};
                for(int tick=0;tick<Simulation.TickRate*360&&sim.Winner==-1;tick++)
                {
                    if(sim.Tick%Simulation.TickRate==0)for(int p=0;p<2;p++)foreach(var command in bots[p].Decide(sim.View(p)))sim.Submit(command);
                    sim.Step();
                }
                Check(sim.Report==null||sim.Winner!=-1,"Report never unlocks during a running match");
                if(sim.Winner==-1)sim.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});
                Invoke(session,"RefreshView");Refresh();
                Check(panel.Page==CommandPanel.MenuPage.Result&&!panel.AcceptsGameInput,"Result report opens automatically and disables battlefield input");
                Check(sim.Report.Samples.Count>2&&sim.Report.BuildOrders.All(o=>o.Count>7),"Preview uses simulated history for both factions, including pagination");
                Capture("01-overview");Check(Has("Stat: APM · accepted orders / min")&&Has("Stat: Workers · trained / lost / peak"),"Overview presents APM and economy / army totals");
                Click("Graphs");
                foreach(var name in new[]{"Workers","Army","Minerals","Gas","Bank: M","Bank: G","APM"})
                {
                    Click(name);Capture("02-graph-"+name.Replace(": ","-").ToLowerInvariant());
                    var graph=panel.GetComponentInChildren<MatchReportGraph>();
                    Check(graph!=null&&ReferenceEquals(graph.Report,sim.Report)&&graph.Maximum>0,"Graph displays completed match: "+name);
                    var mesh=graph.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>0&&mesh.vertexCount<65000&&mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)),"Graph has bounded finite geometry: "+name);
                }
                Click("Build order");
                foreach(string faction in new[]{"You · Consortium","AI · Symbiosis"})
                {
                    Click(faction);int rows=0,pages=0;
                    do
                    {
                        rows+=panel.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="Build order row");pages++;
                        if(pages==1)Capture("03-build-"+(faction.StartsWith("You")?"player":"ai"));
                        if(!Has("Next"))break;
                        Click("Next");
                    }while(pages<100);
                    Check(rows==sim.Report.BuildOrders[faction.StartsWith("You")?0:1].Count,"Every order is reachable without duplication: "+faction);
                    Check(Has("Previous"),"Multi-page order history supports going back");Click("Previous");
                }
                var finished=sim.Report;Click("Rematch");
                Check(session.Match.Report==null&&session.Match!=sim&&panel.Page==CommandPanel.MenuPage.Actions,"Rematch starts fresh without carrying previous statistics");
                session.Send(CommandKind.Surrender);Refresh();Capture("04-instant-result");
                Check(Has("Stat: APM · accepted orders / min"),"New result resets to overview after browsing the previous report");
                Click("Build order");Capture("05-empty-build-order");Check(!Has("Next")&&!Has("Previous"),"Empty build history has no dead pagination controls");
                Click("Graphs");Click("APM");Capture("06-zero-duration-graph");
                Click("Main menu");Check(panel.Page==CommandPanel.MenuPage.Main&&session.Match==null,"Report main-menu action closes the finished match");
                Debug.Log($"FRONTIER_MATCH_REPORT passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Debug.Log("REPORT_PASS: "+message);}
                void Refresh(){typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Invoke(panel,"LateUpdate");}
                bool Has(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(t=>t.name==name&&t.gameObject.activeInHierarchy);
                void Click(string name)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                    var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Actual menu ray selects "+name);Refresh();
                }
                void Capture(string name)
                {
                    Refresh();Canvas.ForceUpdateCanvases();
                    var root=Field<RectTransform>(panel,"root");var actions=Field<RectTransform>(panel,"actions");var footer=Field<RectTransform>(panel,"statusBanner");
                    Check(-actions.anchoredPosition.y+actions.sizeDelta.y<=-footer.anchoredPosition.y,"Report actions and footer do not overlap");
                    foreach(var text in panel.GetComponentsInChildren<Text>())
                        Check(text.preferredHeight<=text.rectTransform.rect.height+.5f,"Report text fits: "+text.text);
                    foreach(RectTransform child in actions)
                        Check(-child.anchoredPosition.y+child.sizeDelta.y<=actions.sizeDelta.y+.5f,"Report content stays inside action area: "+child.name);
                    HarvestingPreview.SavePanel(panel,"TestResults/Reports/"+name+".png",preview);captures++;
                }
            }
            finally{Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
