using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class LocalizationPreview
    {
        [MenuItem("Frontier/Validate languages and preview Spanish")]
        public static void Run()=>RunChecks(true);
        public static void Scan()=>RunChecks(false);
        private static void RunChecks(bool strict)
        {
            bool had=PlayerPrefs.HasKey(Localization.PreferenceKey);string saved=PlayerPrefs.GetString(Localization.PreferenceKey);
            var missing=new HashSet<string>();var clipped=new HashSet<string>();var rendered=new SortedDictionary<string,string>();int checks=0;
            string archivePath=Path.Combine(Path.GetTempPath(),"frontier-language-"+Guid.NewGuid().ToString("N"));
            try
            {
                Check(Localization.ResolveCode("system",SystemLanguage.Spanish)=="es","System Spanish resolves to Spanish");
                Check(Localization.ResolveCode("system",SystemLanguage.English)=="en","System English resolves to English");
                Check(Localization.ResolveCode("system",SystemLanguage.Japanese)=="en","Unsupported system languages fall back to English");
                Check(Localization.ResolveCode("en",SystemLanguage.Spanish)=="en"&&Localization.ResolveCode("es",SystemLanguage.English)=="es","Explicit choice overrides system language");
                Localization.SetPreference("es");Localization.ReloadPreference();Check(Localization.Code=="es","Explicit language survives preference reload");
                foreach(var language in Localization.Languages){Localization.Validate(language);Check(true,"Valid catalog "+language.code);}
                foreach(var entry in Localization.Languages.First(l=>l.code=="es").entries.Where(e=>e.template))
                {
                    int count=Regex.Matches(entry.key,@"\{\d+\}").Count;
                    var args=Enumerable.Range(0,count).Select(i=>(object)(17+i*13).ToString()).ToArray();
                    string source=string.Format(entry.key,args),expected=string.Format(entry.value,args);
                    string translated=Localization.Text(source);
                    Check(translated==expected,"Template preserves values: "+entry.key+" => "+translated);
                }
                Check(Localization.Text("Build Barracks")=="Construir Cuartel","Composed action localizes catalog name");
                Check(Localization.Text("Group 3 selected.")=="Grupo 3 seleccionado.","Group feedback keeps its number");
                Check(Localization.Text("B · Back")=="B · Volver","Physical B button is never treated as a producer number");
                Check(Localization.Text("0 queued orders")=="0 órdenes en cola"&&Localization.Text("1 queued order")=="1 orden en cola","Queued orders use natural singular and plural");
                Check(Localization.Text("unknown.future.key")=="unknown.future.key","Missing translations retain readable source");
                var labelObject=new GameObject("Language binding",typeof(RectTransform),typeof(LocalizedText));
                try
                {
                    var text=labelObject.GetComponent<LocalizedText>();text.SetKey("Workers {0}/{1}",6,10);
                    Check(text.text=="Trabajadores 6/10","Explicit template binding formats Spanish values");
                    labelObject.SetActive(false);Localization.SetPreference("en");labelObject.SetActive(true);
                    Check(text.text=="Workers 6/10","Hidden bindings refresh on enable without losing arguments");Localization.SetPreference("es");
                    text.SetKey("{0} · {1}","Trigger","Select / place\nHold: drag · Double: same type");
                    Check(text.text=="Gatillo · Seleccionar / colocar\nMantener: área · Doble: mismo tipo","Controller hints preserve complete action phrases");
                    text.SetKey("{0} · {1}","B","Cancel / pause");Check(text.text=="B · Cancelar / pausar","Explicit binding preserves the physical B button");
                }
                finally{Object.DestroyImmediate(labelObject);}
                using(var f=new UnifiedWheelFixture("Localization"))
                {
                    foreach(var caster in f.Session.Catalog.Definition.archetypes.Where(t=>t.role=="caster"))
                    {
                        string details=Localization.Text(CatalogReference.ExtraDetails(f.Session.Catalog,caster));
                        Check(!Regex.IsMatch(details,@"\b(energy|cooldown|radius|range|visible|enemies|allied|units|health)\b")&&details.Contains("unidades aliadas")&&details.Contains("enemigos visibles"),"Both ability cards translate separately: "+caster.id+" => "+details);
                    }
                    // This fixture normally emulates Touch; desktop avoids waiting for tracking in the lobby.
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest,false);
                    Show(CommandPanel.MenuPage.Main);Capture("es-main");
                    Show(CommandPanel.MenuPage.Settings);
                    foreach(string tab in new[]{"Controls","Audio","Visuals","Play space","Language"}){Click(tab);Capture("es-settings-"+tab);}
                    Capture("es-settings-language");
                    Click("Language: Español");Check(Localization.Preference=="system"&&!PlayerPrefs.HasKey(Localization.PreferenceKey),"Settings cycles back to system and removes explicit override");
                    Localization.SetPreference("es");Show(CommandPanel.MenuPage.MatchSetup);Capture("es-new-match");
                    foreach(var page in new[]{CommandPanel.MenuPage.Guide,CommandPanel.MenuPage.UnitsGuide,CommandPanel.MenuPage.StructuresGuide})
                    {
                        Show(page);
                        if(page==CommandPanel.MenuPage.Guide)
                        {
                            foreach(var topic in DefaultControlCatalog.For(false)) {Click("Help: "+topic.Name);Capture("es-guide-"+topic.Name);}
                        }
                        else foreach(string faction in new[]{"industrial","biological"})
                        {
                            Click("Reference faction: "+faction);
                            foreach(var type in f.Session.Catalog.Definition.archetypes.Where(t=>t.faction==faction&&t.building==(page==CommandPanel.MenuPage.StructuresGuide)))
                            {Click("Reference entry: "+type.id);Audit();}
                            Capture("es-"+page+"-"+faction);
                        }
                    }
                    Show(CommandPanel.MenuPage.Hotkeys);Capture("es-hotkeys");
                    f.Start("industrial");var worker=f.Session.Match.Entities.Values.First(e=>e.owner==0&&f.Session.Match.Type(e).role=="worker");
                    f.Session.Select(worker.id,false);Show(CommandPanel.MenuPage.Actions);Capture("es-worker-actions");
                    string snapshot=JsonUtility.ToJson(f.Session.Match.Snapshot());int[] selection=f.Session.Selection.ToArray();
                    Localization.SetPreference("en");Check(f.Panel.GetComponentsInChildren<LocalizedText>().Any(t=>t.text=="BUILD STRUCTURES"),"Existing labels update immediately to English");
                    Localization.SetPreference("es");Check(JsonUtility.ToJson(f.Session.Match.Snapshot())==snapshot&&f.Session.Selection.SetEquals(selection),"Language changes preserve authoritative state and selection");
                    Show(CommandPanel.MenuPage.Pause);Capture("es-pause");Show(CommandPanel.MenuPage.Settings);Click("Language");Capture("es-paused-language");
                    Check(f.Session.IsPaused,"Language settings preserve match pause");
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest,true);
                    Show(CommandPanel.MenuPage.Guide);
                    foreach(var topic in DefaultControlCatalog.For(true))
                    {
                        Click("Help: "+topic.Name);Capture("es-touch-guide-"+topic.Name);
                        if(topic.Name=="Selection"){Click("Page 1 / 2 · Next");Capture("es-touch-guide-Selection-2");}
                    }
                    Show(CommandPanel.MenuPage.Actions);
                    f.Wheel.TickHint(f.Ray,true);Audit(f.Wheel);if(strict)f.Capture("es-hints","hintRoot",1700,700);
                    f.Dismiss();f.Frame(Vector2.down);Audit(f.Wheel);if(strict)f.Capture("es-quick-select");
                    f.Dismiss();f.Quest.BeginPlacement();f.Wheel.TickHint(f.Ray,true);Audit(f.Wheel);if(strict)f.Capture("es-placement-hints","hintRoot",1700,700);f.Quest.CancelPlacement();
                    f.Session.Send(CommandKind.Surrender);Show(CommandPanel.MenuPage.Result);
                    foreach(string tab in new[]{"Overview","Graphs","Build order"}){Click(tab);Capture("es-result-"+tab);}
                    f.Session.LeaveMatch();typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest,false);
                    var store=new MatchArchiveStore(archivePath);typeof(GameSession).GetField("history",UnifiedWheelFixture.Private).SetValue(f.Session,store);
                    var recorded=new Simulation(f.Session.Map,f.Session.Catalog);var writer=store.Begin(recorded,"easy");writer.Flush();
                    for(int i=0;i<120;i++)recorded.Step();recorded.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();
                    UnifiedWheelFixture.Call(f.Panel,"OpenHistory");Capture("es-history");
                    Check(f.Session.StartReplay(store.List().Single().Id),"Replay opens with Spanish UI");Capture("es-replay");
                    f.Session.SeekReplay(100);Capture("es-replay-seeking");f.Session.StopReplay();

                    void Show(CommandPanel.MenuPage page){UnifiedWheelFixture.Call(f.Panel,"Show",page);Refresh();}
                    void Refresh(){typeof(CommandPanel).GetField("nextText",UnifiedWheelFixture.Private).SetValue(f.Panel,0f);UnifiedWheelFixture.Call(f.Panel,"LateUpdate");}
                    void Click(string name)
                    {
                        Refresh();var rect=f.Panel.GetComponentsInChildren<RectTransform>().First(r=>r.gameObject.activeInHierarchy&&r.name==name);
                        var point=rect.TransformPoint(rect.rect.center);var ray=new Ray(point-rect.forward,rect.forward);
                        Check(f.Panel.Point(ray,true,false)&&f.Panel.Point(ray,false,true),"Stable ray target: "+name);Refresh();
                    }
                    void Capture(string name)
                    {
                        Refresh();Audit();
                        if(!strict&&f.Panel.GetComponentsInChildren<Text>().Any(t=>!t.resizeTextForBestFit&&t.preferredHeight>t.rectTransform.rect.height+.5f))return;
                        HarvestingPreview.SavePanel(f.Panel,"TestResults/Localization/"+name+".png",UnifiedWheelFixture.Field<PreviewRenderUtility>(f,"preview"));
                    }
                    void Audit(Component component=null)
                    {
                        Canvas.ForceUpdateCanvases();
                        foreach(var t in (component??f.Panel).GetComponentsInChildren<LocalizedText>())
                        {
                            rendered[t.Source]=t.text;
                            if(Regex.IsMatch(t.Source,@"[A-Za-z]{3}")&&t.text==t.Source)missing.Add(t.Source);
                            if(t.preferredHeight>t.rectTransform.rect.height+.5f)clipped.Add(t.Source+" => "+t.text);
                        }
                    }
                }
                Directory.CreateDirectory("TestResults/Localization");File.WriteAllLines("TestResults/Localization/untranslated.txt",missing.OrderBy(s=>s));File.WriteAllLines("TestResults/Localization/clipped.txt",clipped.OrderBy(s=>s));
                File.WriteAllLines("TestResults/Localization/rendered.txt",rendered.Select(pair=>pair.Key.Replace("\n","\\n")+" => "+pair.Value.Replace("\n","\\n")));
                if(strict)
                {
                    Check(clipped.Count==0,"All Spanish text fits its layout");
                    var unchanged=new HashSet<string>{"Arsenal","Audio","Ctrl + 1 / 2 / 3 / 4","Escape","Gas","GAS","FRONTIER / COMMAND","APM"};
                    Check(missing.All(unchanged.Contains),"Unchanged visible strings are only shared terms, key names and the game title");
                }
                Localization.SetPreference("en");if(strict){MenuFlowPreview.Render();ReplaySessionChecks.Run();SpectatorPanelChecks.Run();}
                Debug.Log($"FRONTIER_LOCALIZATION passed: {checks} checks; {missing.Count} shared strings and {clipped.Count} layout overflows; no player build");
            }
            finally{if(had)PlayerPrefs.SetString(Localization.PreferenceKey,saved);else PlayerPrefs.DeleteKey(Localization.PreferenceKey);PlayerPrefs.Save();Localization.ReloadPreference();if(Directory.Exists(archivePath))Directory.Delete(archivePath,true);}
            void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("LOCALIZATION_PASS: "+message);}
        }
    }
}
