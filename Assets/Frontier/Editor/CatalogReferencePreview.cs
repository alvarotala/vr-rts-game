using TMPro;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    // Exercises the real world-space reference UI; no match data is needed to browse it.
    public static class CatalogReferencePreview
    {
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        private static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);

        [MenuItem("Frontier/Validate and preview catalog guides")]
        public static void ValidateAndRender()
        {
            const string output="TestResults/CatalogReference";
            Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Catalog reference fixture");
            var console=new GameObject("Catalog reference console");Camera createdCamera=null;
            int checks=0,entriesChecked=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;board.Placed=true;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                createdCamera=quest.Camera;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                preview.camera.transform.SetPositionAndRotation(new Vector3(0,1.7f,-2),Quaternion.identity);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
                panel.PlaceRequested=quest.BeginPlacement;Call(panel,"Start");preview.AddSingleGO(console);
                string configuration=Configuration();
                BrowseAll(true);
                Check(!session.Running&&session.Match==null&&Configuration()==configuration,"Lobby browsing never starts or reconfigures a match");

                session.StartMatch();
                var worker=session.View.entities.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                session.Select(worker.id,false);session.Pause("menu",true);
                string matchState=MatchState();
                BrowseAll(false);
                session.Match.Step();
                Check(session.IsPaused&&session.Match.Paused&&MatchState()==matchState,"Browsing both catalogs preserves paused simulation, economy, orders and selection");
                Show(CommandPanel.MenuPage.Pause);
                Check(!Field<RectTransform>(panel,"referenceGuide").gameObject.activeInHierarchy,"Reference content closes on other menu pages");
                Debug.Log($"FRONTIER_CATALOG_REFERENCE passed: {checks} checks; {entriesChecked} entries, lobby + pause; renders in {output}");

                void BrowseAll(bool render)
                {
                    foreach(bool structures in new[]{false,true})
                    {
                        Show(structures?CommandPanel.MenuPage.StructuresGuide:CommandPanel.MenuPage.UnitsGuide);
                        foreach(var faction in session.Catalog.Definition.factions)
                        {
                            Click("Reference faction: "+faction.id);
                            Check(Configuration()==configuration,"Faction tabs do not change match configuration");
                            var entries=session.Catalog.Definition.archetypes.Where(a=>a.faction==faction.id&&a.building==structures).ToArray();
                            int count=panel.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("Reference entry: ")&&r.gameObject.activeInHierarchy);
                            Check(count==entries.Length,"Every "+faction.id+(structures?" structure":" unit")+" is available");
                            foreach(var type in entries)
                            {
                                Click("Reference entry: "+type.id);entriesChecked++;
                                Check(Field<Text>(panel,"referenceName").text==type.name,"Entry click displays "+type.id);
                                Stat("MINERALS",Number(type.minerals));Stat("GAS",Number(type.catalyst));
                                Stat(type.building?"BUILD TIME":"TRAIN TIME",Number((float)type.duration/Simulation.TickRate)+" s");
                                Stat("HEALTH",Number(type.health));Stat("VISION · CELLS",Number(type.vision));
                                Stat(type.building?"SUPPLY ADDED":"SUPPLY USED",Number(type.building?type.capacity:type.supply));
                                if(type.building)Stat("FOOTPRINT",type.footprint+" × "+type.footprint);
                                else Stat("SPEED · CELLS/S",Number(type.speed));
                                if(type.damage>0)
                                {
                                    Stat("DAMAGE / HIT",Number(type.damage));Stat("RANGE · CELLS",Number(type.range));
                                    Stat("SHOT INTERVAL",Number((float)type.attackPeriod/Simulation.TickRate)+" s");
                                    Stat("DAMAGE / SECOND",Number(type.damage*Simulation.TickRate/type.attackPeriod));
                                }
                                else Stat("WEAPON","Unarmed");
                                string requirements=Field<Text>(panel,"referenceRequirements").text;
                                string source=session.Catalog.Role(type.faction,type.building?"worker":type.producer).name;
                                Check(requirements.Contains(source),"Production source is catalog-backed for "+type.id);
                                if(!string.IsNullOrEmpty(type.requires))Check(requirements.Contains(session.Catalog.Role(type.faction,type.requires).name),"Prerequisite shown for "+type.id);
                                string details=Field<Text>(panel,"referenceExtraDetails").text;
                                foreach(string id in type.abilities)
                                {
                                    var ability=session.Catalog.Ability(id);
                                    Check(details.Contains(ability.name)&&details.Contains(ability.energyCost+" energy")
                                        &&details.Contains(Number((float)ability.cooldown/Simulation.TickRate)+" s cooldown")
                                        &&details.Contains("radius "+Number(ability.radius))&&details.Contains("range "+Number(ability.range))
                                        &&details.Contains(Number(ability.amount)),"Ability values shown for "+id);
                                }
                                foreach(var product in session.Catalog.Definition.archetypes.Where(a=>a.faction==type.faction&&!a.building&&a.producer==type.role))
                                    Check(details.Contains(product.name),"Production list includes "+product.id);
                                foreach(var unlock in session.Catalog.Definition.archetypes.Where(a=>a.faction==type.faction&&a.building&&a.requires==type.role))
                                    Check(details.Contains(unlock.name),"Unlock list includes "+unlock.id);
                                Canvas.ForceUpdateCanvases();
                                var portrait=Field<SelectionPortrait>(panel,"referencePortrait");portrait.Rebuild(CanvasUpdate.PreRender);
                                Check(portrait.canvasRenderer.GetMesh()!=null&&portrait.canvasRenderer.GetMesh().vertexCount>12,"Model portrait has geometry for "+type.id);
                                var guide=Field<RectTransform>(panel,"referenceGuide");
                                foreach(var label in guide.GetComponentsInChildren<Text>())
                                    Check(label.preferredHeight<=label.rectTransform.rect.height+.5f,"Reference text fits: "+type.id+" / "+label.text);
                                var root=Field<RectTransform>(panel,"root");
                                Check(-guide.anchoredPosition.y+guide.rect.height<=root.rect.height,"Reference content fits within panel");
                                if(render)HarvestingPreview.SavePanel(panel,output+"/"+type.id+".png",preview);
                            }
                        }
                    }
                }

                void Stat(string label,string value)
                {
                    var card=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Reference stat: "+label&&r.gameObject.activeInHierarchy);
                    Check(card.GetComponentsInChildren<Text>().Any(t=>t.text==value),label+" displays catalog value "+value);
                }
                string Configuration()=>session.Faction+"|"+session.EnemyFaction+"|"+session.Difficulty+"|"+session.MapId;
                string MatchState()=>Configuration()+"|"+session.Match.Tick+"|"+session.LastCommandSequence+"|"+string.Join(",",session.Selection.OrderBy(id=>id))
                    +"|"+string.Join(";",session.Match.Players.Select(JsonUtility.ToJson))
                    +"|"+string.Join(";",session.Match.Entities.OrderBy(e=>e.Key).Select(e=>JsonUtility.ToJson(e.Value)))
                    +"|"+string.Join(";",session.Match.Resources.Select(JsonUtility.ToJson));
                void Show(CommandPanel.MenuPage page){Call(panel,"Show",page);Refresh();}
                void Refresh(){typeof(CommandPanel).GetField("nextText",Private).SetValue(panel,0f);Call(panel,"LateUpdate");Canvas.ForceUpdateCanvases();}
                void Click(string name)
                {
                    Refresh();var rect=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name&&r.gameObject.activeInHierarchy);
                    var center=rect.TransformPoint(rect.rect.center);var ray=new Ray(center-rect.forward,rect.forward);
                    Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Pointer activates "+name);Refresh();
                }
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);
                if(createdCamera!=null)Object.DestroyImmediate(createdCamera.gameObject);
                preview.Cleanup();
            }
        }
    }
}
