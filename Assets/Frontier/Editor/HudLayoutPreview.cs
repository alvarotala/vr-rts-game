using System;
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
    public static class HudLayoutPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Preview fixed match HUD layout")]
        public static void Render()
        {
            const string output="TestResults/HudLayout";
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Fixed HUD fixture");var console=new GameObject("Fixed HUD console");
            int checks=0,captures=0;string language=Localization.Preference;
            try
            {
                Directory.CreateDirectory(output);
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");preview.AddSingleGO(console);
                var layoutFields=new[]{"selectedCard","actionCard","actions","productionStrip","orderPlanStrip","groupStrip","harvestingStrip","statusBanner","matchStrip"};
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();Refresh();
                    var root=Field<RectTransform>(panel,"root");var size=root.sizeDelta;
                    Check(size==new Vector2(1480,1636),"Every selection retains the established fixed 1480 x 1636 HUD");
                    var rectangles=layoutFields.ToDictionary(name=>name,name=>(Field<RectTransform>(panel,name).anchoredPosition,Field<RectTransform>(panel,name).sizeDelta));
                    var production=Field<RectTransform>(panel,"productionStrip");var orders=Field<RectTransform>(panel,"orderPlanStrip");
                    Check(production.anchoredPosition==orders.anchoredPosition&&production.sizeDelta==orders.sizeDelta,"Production and queued orders occupy the exact same reserved rectangle");
                    CheckStable("Empty selection");
                    var worker=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building);
                    var hq=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    Select(worker.id);CheckStable("Single unit");
                    Check(orders.gameObject.activeSelf&&!production.gameObject.activeSelf,"Unit selection shows only queued orders in the shared region");
                    Check(session.Send(CommandKind.Move,position:new Point(22,14))&&session.Send(CommandKind.Move,position:new Point(25,22),append:true),"Queued movement accepted");
                    Refresh();CheckStable("Appended orders");Capture(faction+"-orders");
                    session.Select(0,false,session.View.resources.First().id);Refresh();CheckStable("Resource selection");
                    Check(!orders.gameObject.activeSelf&&!production.gameObject.activeSelf,"Resource selection keeps the shared region empty");
                    Select(hq.id);CheckStable("Single building");
                    Check(production.gameObject.activeSelf&&!orders.gameObject.activeSelf,"Building selection shows only production in the shared region");
                    Check(session.Send(CommandKind.Train,worker.definition),"Production order accepted");Refresh();CheckStable("Enqueued production");Capture(faction+"-production");
                    session.Select(worker.id,true);Refresh();CheckStable("Mixed selection");
                    Check(orders.gameObject.activeSelf&&!production.gameObject.activeSelf,"Mixed selection retains unit orders without overlapping production");
                    int id=1000;
                    foreach(var type in session.Catalog.Definition.archetypes.Where(t=>t.faction==faction))
                        session.Match.Entities.Add(id,new EntityState{id=id++,owner=0,definition=type.id,health=type.health,energy=type.energyMax*.5f,position=new Point(20,20)});
                    Call(session,"RefreshView");
                    var representatives=session.View.entities.Where(e=>e.owner==0).GroupBy(e=>e.definition).Select(group=>group.First().id).ToArray();
                    for(int count=1;count<=representatives.Length;count++)
                    {
                        Select(representatives.Take(count).ToArray());CheckStable(count+" selected types");
                        CheckSelectionCards(count>6);
                    }
                    session.Selection.Clear();session.Selection.UnionWith(session.View.entities.Where(e=>e.owner==0).Select(e=>e.id));
                    Refresh();CheckStable("Every own unit and building type");
                    Check(Field<RectTransform>(panel,"selectionGrid").GetComponentsInChildren<SelectionPortrait>().Length==session.Catalog.Definition.archetypes.Count(t=>t.faction==faction),"Every current faction type remains visible without paging");
                    var selectedCard=Field<RectTransform>(panel,"selectedCard");var grid=Field<RectTransform>(panel,"selectionGrid");
                    Check(-grid.anchoredPosition.y+grid.rect.height<=selectedCard.rect.height-24+.01f,"The full type grid fits the fixed selection section");
                    foreach(string locale in new[]{"en","es"})
                    {
                        Localization.SetPreference(locale);Canvas.ForceUpdateCanvases();CheckSelectionCards(true);
                        HarvestingPreview.SaveCanvas(panel.transform,root,output+"/"+faction+"-all-types-"+locale+".png",preview,1800,1800);captures++;
                    }
                    Localization.SetPreference(language);
                    Select(hq.id);
                    CheckSelectionCards(false);
                    session.Announce("Order accepted.");Refresh();CheckStable("Normal feedback");
                    var status=Field<Text>(panel,"status");var textPosition=status.rectTransform.anchoredPosition;var textSize=status.rectTransform.sizeDelta;var fontSize=status.fontSize;var style=status.fontStyle;
                    var banner=Field<RectTransform>(panel,"statusBanner");var footerLabel=Field<Text>(panel,"matchMap");
                    Check(Mathf.Abs(root.InverseTransformPoint(status.rectTransform.position).x-root.InverseTransformPoint(footerLabel.rectTransform.position).x)<.01f,"Feedback shares the content inset of the match footer and section titles");
                    Check(Mathf.Abs(root.InverseTransformPoint(status.rectTransform.position).x-root.InverseTransformPoint(Field<Text>(panel,"harvesting").rectTransform.position).x)<.01f,"Feedback aligns with the harvesting Minerals line immediately above it");
                    session.Announce("Not enough minerals.",true);Refresh();CheckStable("Error feedback");
                    Check(status.rectTransform.anchoredPosition==textPosition&&status.rectTransform.sizeDelta==textSize&&status.fontSize==fontSize&&status.fontStyle==style,"Errors preserve normal message typography and bounds");
                    Check(status.color.r>status.color.g&&status.color.r>status.color.b&&banner.GetComponent<Image>().color.a==0
                        &&!Field<RectTransform>(panel,"statusAccent").gameObject.activeSelf&&!Field<Text>(panel,"statusIcon").gameObject.activeSelf,"Error feedback is only red text without extra decorations");
                    Capture(faction+"-error");
                    session.Announce("Order accepted.");Refresh();CheckStable("Success after error");
                    Check(status.color==ConsoleTheme.Muted,"Normal feedback restores its normal text color");
                    session.Pause("tracking",true);Refresh();CheckStable("Tracking pause");session.Pause("tracking",false);
                    Select();CheckStable("Cleared selection after production and feedback");
                    session.LeaveMatch();

                    void CheckStable(string context)
                    {
                        Check(root.sizeDelta==size,context+": outer HUD size is unchanged");
                        foreach(var name in layoutFields)
                        {
                            var rect=Field<RectTransform>(panel,name);var saved=rectangles[name];
                            Check(rect.anchoredPosition==saved.Item1&&rect.sizeDelta==saved.Item2,context+": "+name+" keeps its position and dimensions");
                        }
                    }
                }
                Debug.Log($"FRONTIER_HUD_LAYOUT passed: {checks} checks, {captures} views; fixed shared regions and feedback typography; no physical Quest validation");
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
                void Refresh(){if(session.Match!=null)Call(session,"RefreshView");typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Call(panel,"LateUpdate");}
                void Select(params int[] ids){session.Select(0,false);session.Selection.UnionWith(ids);Call(panel,"Rebuild");Refresh();}
                void CheckSelectionCards(bool compact)
                {
                    var grid=Field<RectTransform>(panel,"selectionGrid");
                    Check(grid.rect.height==136&&Field<int>(panel,"selectionLayoutColumns")== (compact?7:6),"Selection layout retains its fixed height and the expected column count");
                    var names=Field<Text[]>(panel,"selectionNames");var counts=Field<Text[]>(panel,"selectionCounts");var energy=Field<Text[]>(panel,"selectionEnergy");
                    var portraits=Field<SelectionPortrait[]>(panel,"selectionPortraits");var tracks=Field<RectTransform[]>(panel,"selectionHealthTracks");var health=Field<Image[]>(panel,"selectionHealthBars");
                    for(int i=0;i<names.Length;i++)
                    {
                        var name=names[i];if(!name.gameObject.activeInHierarchy)continue;
                        var card=(RectTransform)name.transform.parent;
                        Check(card.rect.height==(compact?60:136)&&name.fontSize==(compact?16:20)
                            &&portraits[i].rectTransform.rect.size==(compact?new Vector2(32,32):new Vector2(48,48)),"Compact and normal type cards restore their target, name and portrait dimensions");
                        Check(tracks[i].rect.height==(compact?2:4)&&health[i].rectTransform.rect.height==tracks[i].rect.height,"Health fills retain the track's thickness in both selection layouts");
                        foreach(var text in new[]{name,counts[i],energy[i]})
                        {
                            if(!text.gameObject.activeInHierarchy)continue;
                            var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
                            if(text.resizeTextForBestFit)settings.fontSize=text.resizeTextMinSize;
                            settings.resizeTextForBestFit=false;
                            float height=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit;
                            Check(height<=text.rectTransform.rect.height+.5f,"Selection text fits without clipping in "+Localization.Preference+": "+text.text);
                            var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                            Check(corners.Select(card.InverseTransformPoint).All(point=>point.x>=card.rect.xMin-.01f&&point.x<=card.rect.xMax+.01f&&point.y>=card.rect.yMin-.01f&&point.y<=card.rect.yMax+.01f),"Selection text remains within its type card: "+text.text);
                        }
                    }
                }
                void Capture(string name)
                {
                    Localization.SetPreference("es");Canvas.ForceUpdateCanvases();
                    HarvestingPreview.SaveCanvas(panel.transform,Field<RectTransform>(panel,"root"),output+"/"+name+".png",preview,1800,1800);captures++;
                    Localization.SetPreference(language);
                }
            }
            finally{Localization.SetPreference(language);Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
