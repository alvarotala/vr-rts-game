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
    public static class UpgradeLevelsPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        public static void ValidateAll(){Render();CatalogReferencePreview.ValidateAndRender();ActionWheelPreview.Render();LocalizationPreview.Run();}

        [MenuItem("Frontier/Validate upgrade levels I II III")]
        public static void Render()
        {
            const string output="TestResults/UpgradeLevels";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Upgrade progression fixture");var console=new GameObject("Upgrade progression console");
            int checks=0,captures=0;string language=Localization.Preference;
            try
            {
                Localization.SetPreference("es");
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");preview.AddSingleGO(console);
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    var type=session.Catalog.Role(faction,"research");
                    var lab=new EntityState{id=1000,owner=0,definition=type.id,position=new Point(20,20),health=type.health};
                    var other=new EntityState{id=1001,owner=0,definition=type.id,position=new Point(25,20),health=type.health};
                    session.Match.Entities.Add(lab.id,lab);session.Match.Entities.Add(other.id,other);
                    Call(session,"RefreshView");session.Select(lab.id,false);Refresh();
                    var size=Field<RectTransform>(panel,"root").sizeDelta;
                    var attackPosition=Button("Firepower I").anchoredPosition;var armorPosition=Button("Armor I").anchoredPosition;
                    Check(Research().Length==2,"Fresh lab offers only Firepower I and Armor I");
                    foreach(string stat in new[]{"attack","armor"})for(int level=1;level<=3;level++)
                    {
                        var upgrade=session.Catalog.Definition.upgrades.Single(u=>u.Stat==stat&&u.level==level);
                        var action=Research().Single(a=>a.Id=="research:"+upgrade.id);var button=Button(action.Title);
                        Check(button.anchoredPosition==(stat=="attack"?attackPosition:armorPosition),"Every tier keeps the same action slot: "+upgrade.name);
                        Check(action.Detail.Contains(upgrade.minerals.ToString())&&action.Detail.Contains(upgrade.catalyst.ToString()),"Action displays the tier's higher cost: "+upgrade.name);
                        if(level==2||level==3)Capture(faction+"-"+stat+"-"+level);
                        int minerals=session.Match.Players[0].minerals,gas=session.Match.Players[0].catalyst;
                        if(stat=="armor")
                        {
                            session.Select(other.id,false);
                            Check(session.Send(CommandKind.Research,upgrade.id),"Other lab accepts the upgrade");
                            session.Select(lab.id,false);
                        }
                        else
                        {
                            var ray=new Ray(button.TransformPoint(button.rect.center)-button.forward,button.forward);
                            Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Real HUD click consumes press and release: "+upgrade.name);
                        }
                        Refresh();
                        var producer=stat=="armor"?other:lab;
                        Check(producer.queue.Single().definition==upgrade.id&&session.Match.Players[0].minerals==minerals-upgrade.minerals
                            &&session.Match.Players[0].catalyst==gas-upgrade.catalyst,"HUD command queues the exact tier and charges once");
                        if(stat=="attack")
                        {
                            Check(Field<Text>(panel,"productionName").text==Localization.Text(upgrade.name),"Production names the actual tier in Spanish");
                            Capture(faction+"-queue-"+level);
                        }
                        Check(!Research().Any(a=>a.Id=="research:"+stat+(level+1)),"Next tier remains locked while the current tier is running");
                        for(int tick=0;tick<upgrade.duration;tick++)session.Match.Step();Refresh();
                        Check(!Research().Any(a=>a.Id==action.Id),"Completed tier disappears from available actions");
                        if(level<3)
                            Check(Research().Any(a=>a.Id=="research:"+stat+(level+1))&&Button(session.Catalog.Upgrade(stat+(level+1)).name)!=null,
                                "Completion refreshes the HUD to the next tier, including at another lab");
                        Check(Field<RectTransform>(panel,"root").sizeDelta==size,"Progression never resizes the HUD");
                        if(stat=="attack")Check(Research().Any(a=>a.Id=="research:armor"),"Firepower does not force Armor to advance");
                    }
                    Check(Research().Length==0&&session.Match.Players[0].attackLevel==3&&session.Match.Players[0].armorLevel==3,"Both upgrade tracks stop at III");
                    Capture(faction+"-complete");session.LeaveMatch();
                }
                Debug.Log($"FRONTIER_UPGRADE_LEVELS passed: {checks} checks, {captures} views; both factions, independent I-II-III progression, real HUD clicks and remote-lab completion; no physical Quest test");
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
                void Refresh(){Call(session,"RefreshView");typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Call(panel,"LateUpdate");}
                SelectionAction[] Research()=>SelectionActions.For(session).Items.Where(a=>a.Category==ActionCategory.Research).ToArray();
                RectTransform Button(string name)=>Field<RectTransform>(panel,"actions").GetComponentsInChildren<RectTransform>().First(r=>r.name==name);
                void Capture(string name){Canvas.ForceUpdateCanvases();HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;}
            }
            finally{Localization.SetPreference(language);Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();}
        }
    }
}
