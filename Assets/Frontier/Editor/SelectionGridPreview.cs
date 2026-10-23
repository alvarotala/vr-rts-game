using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        public static void ValidateIndividuals(){Render();HudLayoutPreview.Render();}
        [MenuItem("Frontier/Preview and check selection grid")]
        public static void Render()
        {
            const string output="TestResults/SelectionGrid";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Selection fixture");var console=new GameObject("Selection console");int checks=0,captures=0;
            var previousControllers=OVRInput.controllers;
            var left=new OVRInput.OVRControllerLTouch();
            var right=(OVRInput.OVRControllerBase)Activator.CreateInstance(typeof(OVRInput).GetNestedType("OVRControllerRTouch",BindingFlags.NonPublic),true);
            Keyboard keyboard=null;var previousKeyboard=Keyboard.current;
            var manager=typeof(InputSystem).GetField("s_Manager",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var playerUpdates=manager.GetType().GetProperty("runPlayerUpdatesInEditMode",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            bool previousUpdates=(bool)playerUpdates.GetValue(manager);
            try
            {
                playerUpdates.SetValue(manager,true);keyboard=InputSystem.AddDevice<Keyboard>();
                OVRInput.controllers=new List<OVRInput.OVRControllerBase>{left,right};
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
                    int buildingTypes=session.Catalog.Definition.archetypes.Count(t=>t.faction==faction&&t.building);
                    int buildingsLeft=Simulation.BuildingLimit-session.Match.Buildings(0);
                    int extraWorkers=Simulation.UnitLimit-session.Match.Units(0)-3*session.Catalog.Definition.archetypes.Count(t=>t.faction==faction&&!t.building&&t.role!="worker");
                    foreach(var type in session.Catalog.Definition.archetypes.Where(t=>t.faction==faction))
                    {
                        int count=type.building?(buildingsLeft+buildingTypes-1)/buildingTypes:type.role=="worker"?extraWorkers:3;
                        if(type.building){buildingsLeft-=count;buildingTypes--;}
                        for(int i=0;i<count;i++)session.Match.Entities.Add(id,new EntityState{id=id++,owner=0,definition=type.id,position=new Point(20,20),health=type.health});
                    }
                    Call(session,"RefreshView");
                    var all=session.View.entities.Where(e=>e.owner==0).Select(e=>e.id).ToArray();
                    Check(all.Length==Simulation.UnitLimit+Simulation.BuildingLimit,$"Maximum roster has {Simulation.UnitLimit} units and {Simulation.BuildingLimit} buildings");
                    var workerType=session.Catalog.Role(faction,"worker");var hqType=session.Catalog.Role(faction,"hq");
                    var workers=all.Where(i=>session.Match.Entities[i].definition==workerType.id).ToArray();
                    var hqs=all.Where(i=>session.Match.Entities[i].definition==hqType.id).ToArray();
                    Select(all);Capture(faction+"-grouped");
                    var groups=session.View.entities.Where(e=>all.Contains(e.id)).GroupBy(e=>e.definition).ToArray();
                    Check(Cards().Length==groups.Length&&Cards().Length<all.Length,"One card per definition instead of one per selected entity");
                    CheckSelectionLayout();
                    foreach(var group in groups)
                    {
                        var card=Card(group.Key);
                        Check(card.rect.width>card.rect.height,"Selection cards leave horizontal room for the model and readable name");
                        Check(card.Find("Count badge").GetComponentInChildren<Text>().text=="×"+group.Count(),"Count includes every selected member of "+group.Key);
                    }
                    Check(!Has("Next selection page"),"The entire current catalog fits without paging through individual units");
                    Select(workers.Take(2).ToArray());
                    session.Match.Entities[workers[0]].health=workerType.health*.5f;Call(session,"RefreshView");Refresh();
                    Check(Cards().Length==2,"A single selected type shows each individual");
                    Check(Math.Abs(HealthRatio(Individual(workers[0]))-.5f)<.01f&&Math.Abs(HealthRatio(Individual(workers[1]))-1)<.01f,
                        "Individual cards show their own health instead of the group average");
                    Select(workers.Take(2).Concat(hqs).ToArray());
                    Check(Math.Abs(HealthRatio(Card(workerType.id))-.75f)<.01f,"Mixed selection still shows average health per type");
                    session.Match.Entities[workers[0]].health=workerType.health;Call(session,"RefreshView");
                    var mobile=all.Where(i=>!session.Catalog[session.Match.Entities[i].definition].building).ToArray();
                    var definitions=mobile.ToDictionary(i=>i,i=>session.Match.Entities[i].definition);
                    foreach(int entity in mobile)session.Match.Entities[entity].definition=workerType.id;
                    Call(session,"RefreshView");
                    for(int count=1;count<=Simulation.UnitLimit;count++)
                    {
                        Select(mobile.Take(count).ToArray());CheckSelectionLayout();
                        Check(Cards().Length==count&&Cards().Select(card=>int.Parse(card.name.Substring("Selection entity ".Length))).SequenceEqual(mobile.Take(count).OrderBy(i=>i)),
                            "Every individual has a stable ID card at selection size "+count);
                        Check(Field<RectTransform>(panel,"root").sizeDelta==new Vector2(1480,1636),"Individual selection never resizes the HUD");
                        Check(Cards().All(card=>!card.Find("Count badge").gameObject.activeSelf),"Individual cards omit redundant count badges");
                        if(new[]{1,6,7,14,15,Simulation.UnitLimit}.Contains(count))Capture(faction+"-individuals-"+count);
                    }
                    var end=mobile.Last();TapEntity(end);Check(session.Selection.SetEquals(new[]{end}),"The last card of a full army selects that exact individual");
                    Select(mobile);Grip();TapEntity(end);Grip(false);Refresh();
                    Check(session.Selection.SetEquals(mobile.Where(i=>i!=end))&&Cards().Length==mobile.Length-1,"Grip removes only the chosen individual from a full army");
                    foreach(var definition in definitions)session.Match.Entities[definition.Key].definition=definition.Value;
                    Call(session,"RefreshView");
                    var casters=all.Where(i=>session.Catalog[session.Match.Entities[i].definition].energyMax>0).Take(2).ToArray();
                    session.Match.Entities[casters[0]].energy=17;session.Match.Entities[casters[1]].energy=39;Call(session,"RefreshView");Select(casters);
                    var energy=Field<Text[]>(panel,"selectionEnergy");
                    Check(energy[0].text.StartsWith("17 /")&&energy[1].text.StartsWith("39 /"),"Same-type casters retain their individual energy values");
                    Select(hqs);Check(Cards().Length==hqs.Length&&Cards().All(card=>card.name.StartsWith("Selection entity ")),"Identical buildings also retain individual cards");
                    foreach(bool useLeft in new[]{false,true})
                    {
                        var bindings=new HotkeyBindings();bindings.SelectHand(useLeft);bindings.Assign(true,"select","touch:a",true,out _);
                        typeof(CommandPanel).GetProperty("Bindings").SetValue(panel,bindings);quest.Bindings=bindings;
                        var cohort=workers.Take(3).Concat(hqs).ToArray();
                        Select(cohort);Tap(workerType.id);
                        Check(session.Selection.SetEquals(workers.Take(3)),"One click immediately isolates selected workers, excluding other nearby workers");Refresh();
                        Check(Cards().Length==3&&Cards().All(card=>card.name.StartsWith("Selection entity ")),"Filtering a type expands its selected individuals");
                        TapEntity(workers[1]);Check(session.Selection.SetEquals(new[]{workers[1]}),"Trigger selects only the pointed individual");
                        Select(workers.Take(3).ToArray());Grip();TapEntity(workers[1]);Grip(false);Refresh();
                        Check(session.Selection.SetEquals(new[]{workers[0],workers[2]})&&Cards().Length==2,"Grip + Trigger removes only that individual");
                        Select(cohort);Tap(hqType.id);Check(session.Selection.SetEquals(hqs),"One click also filters buildings by exact definition");
                        Select(cohort);Grip();int alive=session.Match.Entities.Count;
                        var ray=RayFor("Selection type "+workerType.id);panel.Point(ray,false,false);Capture(faction+(useLeft?"-left":"-right")+"-remove",false);
                        Tap(workerType.id);
                        Check(session.Selection.SetEquals(hqs)&&session.Match.Entities.Count==alive,"Grip + click removes only that type from selection, without deleting entities");
                        Grip(false);Refresh();Check(!Has("Selection type "+workerType.id)&&Cards().Length==hqs.Length,"Removing a type expands the remaining type into its individuals");
                        Select(new[]{workers[0]});Grip();TapEntity(workers[0]);Grip(false);Refresh();
                        Check(session.Selection.Count==0&&!Grid().gameObject.activeInHierarchy,"Removing the last individual clears the selection grid");
                        Select(workers.Take(3).ToArray());var individualRay=RayFor("Selection entity "+workers[1]);panel.Point(individualRay,true,false);
                        panel.Point(new Ray(Vector3.one*100,Vector3.up),false,false);panel.Point(individualRay,false,true);
                        Check(session.Selection.SetEquals(workers.Take(3)),"Leaving an individual card also cancels the gesture");
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);
                        Set(panel,"pressedAt",Time.unscaledTime-2);panel.Point(ray,false,false);
                        Check(session.Selection.SetEquals(cohort),"Holding Trigger alone no longer removes an individual unit");
                        panel.Point(ray,false,true);Check(session.Selection.SetEquals(workers.Take(3)),"Long click still selects the complete type on release");
                        Select(cohort);Grip();ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);Grip(false);panel.Point(ray,false,true);
                        Check(session.Selection.SetEquals(hqs),"Grip captured at press is preserved if it is released before Trigger");
                        Select(cohort);Grip(true,true);Tap(workerType.id);Grip(false,true);
                        Check(session.Selection.SetEquals(workers.Take(3)),"Inactive hand Grip cannot remove a type");
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);
                        Route(ray,false,false,false,true,false,true);Route(ray,false,false,false,false,true,false);
                        Check(session.Selection.SetEquals(workers.Take(3)),"A/X alias follows the same grouped selection through the input router");
                        Select(cohort);Grip();ray=RayFor("Selection type "+workerType.id);
                        Route(ray,true,false,true,false,false,false);Route(ray,false,false,true,true,false,true);
                        Route(ray,false,true,false,false,false,true);Refresh();
                        Check(session.Selection.SetEquals(hqs),"Trigger owns a Grip removal even when A/X overlaps");
                        Route(ray,false,false,false,false,true,false);Route(ray,false,false,false,false,false,false);Grip(false);
                        Check(session.Selection.SetEquals(hqs),"Overlapping A/X release cannot select a card exposed after removal");
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);
                        panel.Point(new Ray(Vector3.one*100,Vector3.up),false,false);panel.Point(ray,false,true);
                        Check(session.Selection.SetEquals(cohort),"Leaving a pressed card cancels selection even if the pointer returns");
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);panel.CancelPointerInteraction();panel.Point(ray,false,true);
                        Check(session.Selection.SetEquals(cohort),"Focus or input cancellation cannot apply a delayed type selection");
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);panel.ToggleMenu();panel.Point(ray,false,true);Refresh();
                        Check(session.Selection.SetEquals(cohort)&&!Grid().gameObject.activeInHierarchy,"Pause cancels pressed cards");panel.ToggleMenu();Refresh();
                        Select(cohort);ray=RayFor("Selection type "+workerType.id);panel.Point(ray,true,false);session.Select(hqs[0],false);panel.Point(ray,false,true);Refresh();
                        Check(session.Selection.SetEquals(new[]{hqs[0]}),"An external selection invalidates a pressed card");
                    }
                    typeof(QuestBridge).GetProperty("Active").SetValue(quest,false);
                    Select(workers.Take(3).Concat(hqs).ToArray());Tap(workerType.id);
                    Check(session.Selection.SetEquals(workers.Take(3)),"Desktop click filters the selected type");
                    foreach(var shift in new[]{Key.LeftShift,Key.RightShift})
                    {
                        Select(workers.Take(3).Concat(hqs).ToArray());
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(shift));InputSystem.Update();Tap(workerType.id);
                        Check(session.Selection.SetEquals(hqs),"Desktop "+shift+" + click removes the complete type");
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                        Select(workers.Take(3).ToArray());InputSystem.QueueStateEvent(keyboard,new KeyboardState(shift));InputSystem.Update();TapEntity(workers[1]);
                        Check(session.Selection.SetEquals(new[]{workers[0],workers[2]}),"Desktop "+shift+" + click removes only that individual");
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    }
                    Select(workers.Take(3).ToArray());TapEntity(workers[1]);Check(session.Selection.SetEquals(new[]{workers[1]}),"Desktop click selects only the pointed individual");
                    typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                    Select(workers.Take(3).Concat(hqs).ToArray());var deathRay=RayFor("Selection type "+workerType.id);panel.Point(deathRay,true,false);
                    session.Match.Entities.Remove(workers[0]);Call(session,"RefreshView");session.Selection.Remove(workers[0]);Refresh();panel.Point(deathRay,false,true);
                    Check(Count(workerType.id)==2&&session.Selection.SetEquals(workers.Skip(1).Take(2).Concat(hqs)),"A death updates the count and cancels the old gesture without losing survivors");
                    Select(hqs.Take(1).ToArray());session.Send(CommandKind.Train,definition:workerType.id);Refresh();Capture(faction+"-production");
                    Check(Gap(Field<RectTransform>(panel,"selectedCard"),Field<RectTransform>(panel,"productionStrip"))>=HudMetric("HudGap")-.01f,
                        "Selection and production remain separate sections after nesting the type grid");
                    var lastRay=RayFor("Selection entity "+hqs[0]);panel.Point(lastRay,true,false);session.StartMatch();Refresh();panel.Point(lastRay,false,true);
                    Check(session.Selection.Count==0,"New match invalidates a press even when IDs are reused");
                    session.LeaveMatch();Refresh();Check(!Grid().gameObject.activeInHierarchy,"Leaving a match hides selection grid");
                }
                Debug.Log($"FRONTIER_SELECTION_GRID passed: {checks} checks, {captures} views; individuals and mixed type groups, synthetic Touch modifiers; no player build");
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("SELECTION_GRID_PASS: "+message);}
                void Refresh(){Set(panel,"nextText",0f);Call(panel,"LateUpdate");}
                void Select(int[] ids){Grip(false);Grip(false,true);panel.CancelPointerInteraction();session.Selection.Clear();session.Selection.UnionWith(ids);Call(panel,"Rebuild");Refresh();}
                RectTransform Grid()=>Field<RectTransform>(panel,"selectionGrid");
                RectTransform[] Cards()=>Grid().GetComponentsInChildren<RectTransform>().Where(r=>r.gameObject.activeInHierarchy&&(r.name.StartsWith("Selection type ")||r.name.StartsWith("Selection entity "))).ToArray();
                RectTransform Card(string type)=>Cards().First(r=>r.name=="Selection type "+type);
                RectTransform Individual(int id)=>Cards().First(r=>r.name=="Selection entity "+id);
                float HealthRatio(RectTransform card)=>card.Find("Health").GetComponent<RectTransform>().rect.width/card.Find("Health track").GetComponent<RectTransform>().rect.width;
                int Count(string type)=>int.Parse(Card(type).Find("Count badge").GetComponentInChildren<Text>().text.Substring(1));
                bool Has(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(r=>r.gameObject.activeInHierarchy&&r.name==name);
                Ray RayFor(string name){var rect=panel.GetComponentsInChildren<RectTransform>().First(r=>r.gameObject.activeInHierarchy&&r.name==name);return new Ray(rect.TransformPoint(rect.rect.center)-rect.forward,rect.forward);}
                void Tap(string type){var ray=RayFor("Selection type "+type);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Type card consumes press and release");}
                void TapEntity(int id){var ray=RayFor("Selection entity "+id);Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Individual card consumes press and release");}
                void Grip(bool held=true,bool inactive=false)
                {
                    bool useLeft=panel.Bindings.LeftHand!=inactive;var controller=useLeft?(OVRInput.OVRControllerBase)left:right;
                    controller.previousState=controller.currentState;controller.currentState=default;
                    if(held)controller.currentState.Buttons=(uint)(useLeft?OVRInput.RawButton.LHandTrigger:OVRInput.RawButton.RHandTrigger);
                }
                bool Route(Ray ray,bool down,bool up,bool held,bool aDown,bool aUp,bool aHeld)=>(bool)Call(input,"RoutePanel",ray,down,up,held,aDown,aUp,aHeld,0f);
                float HudMetric(string name)=>Convert.ToSingle(typeof(CommandPanel).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue());
                Rect Bounds(RectTransform rect)
                {
                    var root=Field<RectTransform>(panel,"root");var corners=new Vector3[4];rect.GetWorldCorners(corners);
                    var points=corners.Select(root.InverseTransformPoint).ToArray();
                    return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                }
                float Gap(RectTransform a,RectTransform b)=>Bounds(a).yMin-Bounds(b).yMax;
                void CheckSelectionLayout()
                {
                    var grid=Grid();var parent=Field<RectTransform>(panel,"selectedCard");
                    var bounds=Bounds(grid);var parentBounds=Bounds(parent);float padding=HudMetric("HudPadding"),gap=HudMetric("HudGap");
                    Check(grid.parent==parent&&Mathf.Abs(bounds.xMin-parentBounds.xMin-padding)<.01f
                        &&Mathf.Abs(parentBounds.xMax-bounds.xMax-padding)<.01f
                        &&Mathf.Abs(parentBounds.yMax-bounds.yMax-HudMetric("HudContentTop"))<.01f
                        &&bounds.yMin-parentBounds.yMin>=padding-.01f,
                        "Selection cards use the same inset and header clearance as the other sections");
                    int columns=Field<int>(panel,"selectionLayoutColumns");var cards=Cards();
                    Check(grid.rect.height==136&&columns==(cards.Length>14?(cards.Length+1)/2:cards.Length>6?7:6),"Selection stays within one fixed region with at most two rows");
                    float horizontalGap=cards.Length>14?6:gap;
                    float width=(grid.rect.width-(columns-1)*horizontalGap)/columns;
                    for(int i=0;i<cards.Length;i++)
                    {
                        var card=cards[i];var cardBounds=Bounds(card);
                        Check(Mathf.Abs(card.rect.width-width)<.01f&&card.rect.height==cards[0].rect.height
                            &&Mathf.Abs(card.anchoredPosition.x-(i%columns)*(width+horizontalGap))<.01f
                            &&Mathf.Abs(card.anchoredPosition.y+(i/columns)*(card.rect.height+gap))<.01f
                            &&cardBounds.xMin>=bounds.xMin-.01f&&cardBounds.xMax<=bounds.xMax+.01f
                            &&cardBounds.yMin>=bounds.yMin-.01f&&cardBounds.yMax<=bounds.yMax+.01f,
                            "Type cards share one rectangular grid without overlaps or clipped targets: "+card.name);
                    }
                }
                void Capture(string name,bool refresh=true)
                {
                    if(refresh)Refresh();Canvas.ForceUpdateCanvases();
                    foreach(var portrait in Grid().GetComponentsInChildren<SelectionPortrait>())portrait.Rebuild(CanvasUpdate.PreRender);
                    Check(Grid().GetComponentsInChildren<SelectionPortrait>().All(p=>p.canvasRenderer.GetMesh()?.vertexCount>0),"Every visible type portrait has model geometry");
                    HarvestingPreview.SavePanel(panel,output+"/"+name+".png",preview);captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();OVRInput.controllers=previousControllers;
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null)previousKeyboard.MakeCurrent();playerUpdates.SetValue(manager,previousUpdates);
            }
        }
    }
}
