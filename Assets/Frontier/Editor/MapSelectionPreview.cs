using System;
using System.Linq;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class MapSelectionPreview
    {
        [MenuItem("Frontier/Validate map selector descriptions")]
        public static void Run()
        {
            bool had=PlayerPrefs.HasKey(Localization.PreferenceKey);
            string saved=PlayerPrefs.GetString(Localization.PreferenceKey);
            int checks=0;
            try
            {
                using(var f=new UnifiedWheelFixture("MapSelection"))
                {
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest,false);
                    foreach(string language in new[]{"en","es"})
                    {
                        Localization.SetPreference(language);
                        foreach(var item in new[]{
                            ("training","Neon Loop","48 × 48 · 4 zones · 1v1\n16m/4g · Futuristic neon","48 × 48 · 4 zonas · 1v1\n16m/4g · Neón futurista"),
                            ("rift","Forest Watch","128 × 128 · 6 zones · 1v1\n24m/6g · Forest","128 × 128 · 6 zonas · 1v1\n24m/6g · Bosque"),
                            ("desert","Desser Alert","160 × 160 · 8 zones · 1v1\n48m/16g · Desert","160 × 160 · 8 zonas · 1v1\n48m/16g · Desierto")})
                        {
                            f.Session.LoadMap(item.Item1);f.Board.Rebuild();
                            UnifiedWheelFixture.Call(f.Panel,"Show",CommandPanel.MenuPage.MatchSetup);
                            UnifiedWheelFixture.Call(f.Panel,"Rebuild");
                            UnifiedWheelFixture.Call(f.Panel,"LateUpdate");
                            Canvas.ForceUpdateCanvases();
                            var labels=f.Panel.GetComponentsInChildren<LocalizedText>();
                            string expected=language=="es"?item.Item4:item.Item3;
                            Check(labels.Any(t=>t.text==(language=="es"?"Mapa: ":"Map: ")+item.Item2),"Map keeps its authored name in "+language);
                            var detail=labels.SingleOrDefault(t=>t.text==expected);
                            Check(detail!=null,"Actual map counts and consistent fields in "+language+": "+item.Item2);
                            Check(detail.preferredHeight<=detail.rectTransform.rect.height+.5f,"Two description lines fit the card");
                            HarvestingPreview.SavePanel(f.Panel,"TestResults/MapSelection/"+language+"-"+item.Item1+".png",UnifiedWheelFixture.Field<PreviewRenderUtility>(f,"preview"));
                        }
                    }
                }
                Debug.Log($"FRONTIER_MAP_SELECTION passed: {checks} checks, 6 views; no player build");
            }
            finally
            {
                if(had)PlayerPrefs.SetString(Localization.PreferenceKey,saved);else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                PlayerPrefs.Save();Localization.ReloadPreference();
            }
            void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("MAP_SELECTION_PASS: "+message);}
        }
    }
}
