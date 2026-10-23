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
    // Real console targets and input routing, with controlled hover time and controller edges.
    public static class ActionHelpPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
        private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
        private static void Invoke(object obj,string method)=>obj.GetType().GetMethod(method,Private).Invoke(obj,null);

        [MenuItem("Frontier/Preview and check action help")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/ActionHelp");
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Action help fixture");var console=new GameObject("Action help console");
            const string placementKey="frontier.panel-placement.v1";
            bool savedPlacement=PlayerPrefs.HasKey(placementKey);string placement=PlayerPrefs.GetString(placementKey);
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.LoadMap("training");
                var board=fixture.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                typeof(QuestBridge).GetProperty("Active").SetValue(quest,true);
                preview.camera.transform.position=new Vector3(0,1.7f,-2);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Invoke(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                var wheel=fixture.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;input.Wheel=wheel;
                preview.AddSingleGO(console);Refresh();
                var offPanel=new Ray(new Vector3(100,100,100),Vector3.up);

                var newMatch=RayTo("New Match");
                Check(Frame(newMatch,aDown:true,aHeld:true)&&panel.Page==CommandPanel.MenuPage.Main,"A starts a menu press without firing early");
                Check(Frame(newMatch,aUp:true)&&panel.Page==CommandPanel.MenuPage.MatchSetup,"A release opens setup exactly like trigger");
                panel.Back();Refresh();

                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();session.Match.Players[0].minerals=5000;session.Match.Players[0].catalyst=5000;
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    var hq=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="hq");
                    var barracks=Add("barracks",1001);var tech=Add("tech",1002);var lab=Add("research",1003);
                    Select(worker.id);
                    if(faction=="industrial")
                    {
                        CheckEmptyOptions(worker.id,hq.id);
                        string workerSelectionHelp=Field<LocalizedText>(panel,"selectionGridHint").Source+
                            "\n\nOptimal: 2 workers per mineral patch · 3 per gas · Automatically assigned to nearby deposits";
                        CheckHudHelp("Selection help","SELECTION",Localization.Text(workerSelectionHelp));
                        CheckHudHelp("Actions help","OPTIONS","Hover an action to learn more");
                        CheckHudHelp("Control groups help","CONTROL GROUPS","Trigger: select · Grip + Trigger: assign");
                        CheckHudHelpLanguage("Selection help","selectionGridHint",workerSelectionHelp);
                        CheckMoveHandle();
                    }
                    var build=SelectionActions.For(session).Items.First(a=>a.Category==ActionCategory.Build);
                    var buildRay=RayTo(build.Title);
                    panel.Point(buildRay,false,false);AgeHover(1.49f);panel.Point(buildRay,false,false);
                    Check(!HelpVisible(),"Quick hover does not open help");
                    AgeHover(1.51f);panel.Point(buildRay,false,false);
                    Check(HelpVisible()&&Field<Text>(panel,"helpTitle").text==build.Label,"Sustained hover describes the pointed building");
                    Check(session.BuildDefinition==null&&session.Match.Buildings(0)==4,"Hover never selects construction or spends resources");
                    Capture(faction+"-building-help");
                    panel.Point(RayTo("Stop"),false,false);Check(!HelpVisible(),"Switching buttons restarts the delay");
                    AgeHover(2);panel.Point(RayTo("Stop"),false,false);Check(HelpVisible(),"Orders also explain their behavior");
                    panel.Point(offPanel,false,false);Check(!HelpVisible(),"Leaving actions restores the selection card");
                    CheckLayout();
                    var buildColor=Button(build.Title).GetComponent<Image>().color;
                    Check(buildColor!=Button("Stop").GetComponent<Image>().color,"Build and unit orders use different button colors");
                    CheckHelpForSelection(faction+"-build");
                    Hover(build.Title);panel.CancelPointerInteraction();Check(!HelpVisible(),"Cancelling pointer interaction hides stale help");
                    Hover(build.Title);panel.Back();Refresh();Check(!HelpVisible(),"Pause hides help");panel.Back();Refresh();
                    Hover(build.Title);session.Pause("tracking",true);Refresh();Check(!HelpVisible(),"Tracking pause hides help");session.Pause("tracking",false);
                    Select(hq.id);Check(!HelpVisible(),"Changing selection clears the previous tooltip");CheckHelpForSelection(faction+"-worker");
                    var workerHelp=SelectionActions.For(session).Items.Single(a=>a.Id=="train:"+worker.definition).Description;
                    Check(workerHelp.Contains("Stays on site")&&workerHelp.Contains("resume unfinished construction")&&workerHelp.Contains("repair damaged allies"),
                        "Worker help explains construction commitment and repair for "+faction);
                    if(faction=="industrial")
                    {
                        Check(session.Context(new Point(24,14),0,0)>0,"A terrain order still sets a rally point");Refresh();
                        var clearRally=SelectionActions.For(session).Items.Single(a=>a.Id=="clear-rally-point");
                        Check(hq.hasRallyPoint&&HasButton(clearRally.Title),"Clear Rally remains available in Options");
                        Check(clearRally.Execute(),"Shared Clear Rally remains executable from the menu");Refresh();
                    }
                    Select(barracks.id);CheckHelpForSelection(faction+"-army");
                    Select(tech.id);CheckHelpForSelection(faction+"-tech");
                    Select(lab.id);CheckHelpForSelection(faction+"-research");

                    Select(hq.id);string train="Train "+session.Catalog[worker.definition].name;
                    var trainRay=RayTo(train);int minerals=session.View.minerals;
                    Check(Frame(trainRay,aDown:true,aHeld:true)&&hq.queue.Count==0,"Holding A does not repeat production");
                    Frame(trainRay,aHeld:true);Frame(trainRay,aUp:true);
                    Check(hq.queue.Count==1&&session.View.minerals==minerals-session.Catalog[worker.definition].minerals,"A queues one unit and pays the actual cost");Refresh();
                    if(faction=="industrial")
                    {
                        string productionHelp=Field<LocalizedText>(panel,"productionHint").Source;
                        CheckHudHelp("Production help","PRODUCTION",Localization.Text(productionHelp));
                        CheckHudHelpLanguage("Production help","productionHint",productionHelp);
                    }
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Frame(offPanel,aUp:true);
                    Check(hq.queue.Count==1,"Releasing A off the button cancels its click");
                    Check(!Frame(offPanel),"Capture ends after A releases away from the panel");
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Frame(trainRay,down:true,held:true,aHeld:true);Frame(trainRay,up:true,aHeld:true);
                    Check(hq.queue.Count==1,"Trigger cannot prematurely release an A-owned press");Frame(trainRay,aUp:true);
                    Check(hq.queue.Count==2,"Combined aliases execute only once");Refresh();
                    trainRay=RayTo(train);Frame(trainRay,down:true,held:true);Frame(trainRay,aDown:true,aHeld:true,held:true);Frame(trainRay,up:true,aHeld:true);
                    Check(hq.queue.Count==3&&Frame(offPanel,aHeld:true)&&Frame(offPanel,aUp:true),"After trigger confirmation, the remaining A hold and release are consumed");
                    Check(!Frame(offPanel),"Normal map routing resumes after both aliases release");Refresh();
                    trainRay=RayTo(train);Frame(trainRay,aDown:true,aHeld:true);Select(worker.id);Frame(RayTo("Attack Move"),aUp:true);
                    Check(!session.AttackMoveMode,"Selection changes cannot redirect a held menu press to another action");
                    Frame(RayTo("Attack Move"),aDown:true,aHeld:true);Frame(RayTo("Attack Move"),aUp:true);
                    Check(session.AttackMoveMode&&worker.order!=CommandKind.AttackMove,"A arms Attack Move without sending its press through to the map");session.CancelMode();
                    var groupRay=RayTo("Group 1");Frame(groupRay,aDown:true,aHeld:true,assignGroup:true);Frame(groupRay,aUp:true);
                    Check(session.GroupSize(0)==1,"Grip plus A assigns a control group like Grip plus trigger");session.Select(0,false);Refresh();
                    groupRay=RayTo("Group 1");Frame(groupRay,aDown:true,aHeld:true);Frame(groupRay,aUp:true);
                    Check(session.Selection.SetEquals(new[]{worker.id}),"Tapping A recalls a control group");Refresh();
                    var moveRay=RayTo("Move menu");Frame(moveRay,aDown:true,aHeld:true);Check(panel.IsDragging,"A can grab the menu handle");
                    Frame(moveRay,aUp:true);Check(!panel.IsDragging,"Releasing A releases the menu handle");
                    Hover(build.Title);var focus=typeof(CommanderInput).GetMethod("OnApplicationFocus",Private);focus.Invoke(input,new object[]{false});
                    Check(!HelpVisible()&&Frame(offPanel,aHeld:true)&&Frame(offPanel,aUp:true),"Focus loss cancels hover and consumes a held A");Frame(offPanel);

                    Select(lab.id);Frame(RayTo("Firepower I"),aDown:true,aHeld:true);Frame(RayTo("Firepower I"),aUp:true);Refresh();
                    Check(lab.queue.Count==1&&lab.queue[0].research,"A starts research through the shared command path");
                    CheckLayout();Hover("Firepower I");Capture(faction+"-grouped-research");
                    Check(SelectionActions.For(session).Items.Any(a=>a.Id=="cancel")&&!HasButton("Cancel / refund"),
                        "Cancel remains in shared actions while the HUD uses production cards");
                    Frame(RayTo("Current production"),aDown:true,aHeld:true);Check(!HelpVisible(),"Pressing a production card dismisses help");Frame(RayTo("Current production"),aUp:true);
                    Check(lab.queue.Count==0,"A on the production card cancels and refunds its queued item");Refresh();

                    // An A press that begins on the map is still a contextual order, never a menu click.
                    Select(worker.id);var destination=new Point(12,12);var mapRay=new Ray(board.Content.TransformPoint(new Vector3(destination.x,0,destination.y))+Vector3.up,Vector3.down);
                    Check(!Frame(mapRay,aDown:true,aHeld:true),"A on terrain is not captured by the menu");
                    typeof(CommanderInput).GetMethod("PointBoard",Private).Invoke(input,new object[]{mapRay,false,false,true,false});
                    Check(worker.order==CommandKind.Move,"Outside menus A still issues contextual movement");
                    Frame(RayTo("Hold Position"),aUp:true);Check(worker.order==CommandKind.Move,"An A release arriving from the map does not click a menu action");
                    Hover("Hold Position");session.LeaveMatch();Refresh();Check(!HelpVisible()&&panel.Page==CommandPanel.MenuPage.Main,"Leaving the match removes action help");

                    EntityState Add(string role,int id)
                    {
                        var type=session.Catalog.Role(faction,role);var entity=new EntityState{id=id,owner=0,definition=type.id,health=type.health,position=new Point(22,8+(id-1000)*5)};
                        session.Match.Entities.Add(id,entity);return entity;
                    }
                }
                Debug.Log($"FRONTIER_ACTION_HELP passed: {checks} checks, {captures} panels rendered; no player build");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("ACTION_HELP_PASS: "+message);}
                void Refresh()
                {
                    if(session.Running)typeof(GameSession).GetProperty("View").SetValue(session,session.Match.View(0));
                    Set(panel,"nextText",0f);Invoke(panel,"LateUpdate");
                }
                void Select(int id){session.Select(id,false);Refresh();}
                RectTransform Button(string name)=>panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name&&t.gameObject.activeInHierarchy);
                Ray RayTo(string name){var rect=Button(name);return new Ray(rect.TransformPoint(rect.rect.center)-rect.forward,rect.forward);}
                bool Frame(Ray ray,bool down=false,bool up=false,bool held=false,bool aDown=false,bool aUp=false,bool aHeld=false,bool assignGroup=false)
                    =>(bool)typeof(CommanderInput).GetMethod("RoutePanelInput",Private).Invoke(input,new object[]{ray,down,up,held,aDown,aUp,aHeld,0f,assignGroup});
                bool HelpVisible()=>Field<RectTransform>(panel,"actionHelp").gameObject.activeSelf;
                void AgeHover(float age)=>Set(panel,"hoverStartedAt",Time.unscaledTime-age);
                void Hover(string name){var ray=RayTo(name);panel.Point(ray,false,false);AgeHover(2);panel.Point(ray,false,false);}
                bool HasButton(string name)=>panel.GetComponentsInChildren<RectTransform>().Any(t=>t.name==name&&t.gameObject.activeInHierarchy);
                float HudMetric(string name)=>(float)typeof(CommandPanel).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
                void CheckEmptyOptions(int workerId,int buildingId)
                {
                    var card=Field<RectTransform>(panel,"actionCard");var area=Field<RectTransform>(panel,"actions");
                    var cardSize=card.rect.size;var areaSize=area.rect.size;
                    Select(0);CheckEmpty("No selection");
                    session.Select(workerId,false);session.Select(buildingId,true);Refresh();CheckEmpty("Mixed selection");
                    Select(workerId);
                    void CheckEmpty(string state)
                    {
                        CheckLayout();
                        Check(card.gameObject.activeInHierarchy&&card.rect.size==cardSize&&area.rect.size==areaSize
                            &&!area.Cast<RectTransform>().Any(t=>t.gameObject.activeInHierarchy&&t.GetComponent<Image>()!=null)
                            &&!Field<Text>(panel,"actionHint").gameObject.activeInHierarchy,
                            state+": Options retains its full area with no commands or placeholder copy");
                    }
                }
                void OpenSectionHelp(string target)
                {
                    var ray=RayTo(target);
                    Check(Frame(ray,aDown:true,aHeld:true)&&Frame(ray,aUp:true)&&panel.SectionHelpOpen,
                        target+": a complete A click opens its guide");
                }
                bool HelpTextFits(Text label)
                {
                    var settings=label.GetGenerationSettings(label.rectTransform.rect.size);
                    settings.resizeTextForBestFit=false;
                    return label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit<=label.rectTransform.rect.height+.5f;
                }
                void CheckHudHelp(string target,string title,string hint)
                {
                    CheckHudHelpTargets();
                    var selection=session.Selection.ToArray();int tick=session.Match.Tick;
                    string world=JsonUtility.ToJson(session.Match.Snapshot());int commands=session.LastCommandSequence;
                    panel.CancelPointerInteraction();var ray=RayTo(target);
                    panel.Point(ray,false,false);AgeHover(2);panel.Point(ray,false,false);
                    Check(!HelpVisible()&&!panel.SectionHelpOpen,target+": hovering highlights the question mark without opening a guide");
                    Check(Frame(ray,aDown:true,aHeld:true)&&!HelpVisible(),target+": A press is captured without firing early");
                    Check(Frame(ray,aUp:true)&&panel.SectionHelpOpen&&HelpVisible()&&Field<Text>(panel,"helpCategory").text=="HELP"
                        &&Field<Text>(panel,"helpTitle").text==title&&!string.IsNullOrWhiteSpace(hint)
                        &&Field<Text>(panel,"helpDetail").text==hint,
                        target+": A release opens detailed help with the section's current instruction");
                    var dialog=Field<RectTransform>(panel,"actionHelp");var root=Field<RectTransform>(panel,"root");
                    var description=Field<Text>(panel,"helpDescription");var detail=Field<Text>(panel,"helpDetail");
                    Check(Vector2.Distance(PanelBounds(dialog).center,root.rect.center)<.01f&&dialog.rect.size==new Vector2(1040,620),
                        target+": the guide is centered in the game console");
                    Check(description.text.Length>100&&description.text!=hint&&description.fontSize==24
                        &&HelpTextFits(description)&&HelpTextFits(detail),target+": detailed text fits at readable sizes");
                    panel.Point(offPanel,false,false);
                    Check(panel.ContainsPointer(offPanel)&&panel.SectionHelpOpen&&HelpVisible(),
                        target+": leaving the question mark keeps help open and captures the map ray");
                    Invoke(panel,"Rebuild");Refresh();
                    Check(panel.SectionHelpOpen&&HelpVisible(),target+": queue and selection refreshes preserve the open guide");
                    var centerRay=new Ray(dialog.TransformPoint(dialog.rect.center)-dialog.forward,dialog.forward);
                    Check(Frame(centerRay,aDown:true,aHeld:true)&&Frame(centerRay,aUp:true)&&panel.SectionHelpOpen,
                        target+": clicks inside the guide are consumed without activating covered controls");
                    bool wheelConsumed=(bool)typeof(CommanderInput).GetMethod("RouteWheel",Private).Invoke(input,
                        new object[]{offPanel,Vector2.right,false,false,false,false,false,false,false,Time.unscaledTime,false,false});
                    Check(wheelConsumed&&!wheel.IsOpen&&panel.SectionHelpOpen,target+": the command wheel cannot open behind a guide");
                    Check(Button("Close help").GetComponent<Image>().color!=dialog.GetComponent<Image>().color,
                        target+": Close has a distinct button fill against the dialog");
                    Capture("industrial-section-help-"+title.ToLowerInvariant().Replace(' ','-'));
                    var closeRay=RayTo("Close help");
                    panel.Point(closeRay,false,false);
                    Check(Button("Close help").GetComponent<Image>().color==ConsoleTheme.Accent
                        &&Button("Close help").GetComponentsInChildren<Text>().All(t=>t.color==ConsoleTheme.HoverInk),
                        target+": modal Close uses the same readable accent hover as all other buttons");
                    Check(Frame(closeRay,aDown:true,aHeld:true)&&Frame(closeRay,aUp:true)&&!panel.SectionHelpOpen&&!HelpVisible()&&!session.IsPaused,
                        target+": Close returns to the match without pausing it");
                    ray=RayTo(target);
                    Check(Frame(ray,down:true,held:true,aDown:true,aHeld:true)&&!panel.SectionHelpOpen,
                        target+": simultaneous Trigger and A share one pending help click");
                    Check(Frame(ray,up:true,aHeld:true)&&panel.SectionHelpOpen,
                        target+": releasing the owning Trigger opens help while A remains held");
                    Check(Frame(offPanel,aHeld:true)&&Frame(offPanel,aUp:true)&&panel.SectionHelpOpen&&HelpVisible(),
                        target+": draining the remaining A alias preserves help without treating its release as an outside click");
                    panel.Back();
                    OpenSectionHelp(target);panel.Back();Refresh();
                    Check(!panel.SectionHelpOpen&&!HelpVisible()&&!session.IsPaused&&panel.Page==CommandPanel.MenuPage.Actions,
                        target+": Back dismisses help before opening Pause");
                    OpenSectionHelp(target);
                    Check(Frame(offPanel,aDown:true,aHeld:true)&&Frame(offPanel,aUp:true)&&!panel.SectionHelpOpen&&!HelpVisible(),
                        target+": an outside click closes help and owns its release without issuing a map order");
                    OpenSectionHelp(target);panel.CancelPointerInteraction();
                    Check(!panel.SectionHelpOpen&&!HelpVisible(),target+": cancelling pointer interaction dismisses help");
                    OpenSectionHelp(target);session.Pause("tracking",true);Refresh();
                    Check(!panel.SectionHelpOpen&&!HelpVisible(),target+": tracking interruption dismisses the guide");
                    session.Pause("tracking",false);Refresh();
                    if(title=="OPTIONS")
                    {
                        session.ChooseAttackMove();Refresh();OpenSectionHelp(target);panel.Back();
                        Check(session.AttackMoveMode&&!session.IsPaused&&!panel.SectionHelpOpen,
                            "Options help: dismissing the guide preserves the pending targeting mode");
                        session.CancelMode();Refresh();
                    }
                    Check(session.Selection.SetEquals(selection)&&session.Match.Tick==tick&&session.LastCommandSequence==commands
                        &&JsonUtility.ToJson(session.Match.Snapshot())==world,
                        target+": reading help preserves selection, production queues and simulation state");
                }
                void CheckHudHelpLanguage(string target,string hiddenField,string source)
                {
                    string preference=Localization.Preference;
                    var hidden=Field<LocalizedText>(panel,hiddenField);
                    bool active=hidden.gameObject.activeSelf;
                    try
                    {
                        Localization.SetPreference("es");
                        // Seed the same cached label as a match created in Spanish,
                        // then leave it hidden while the player changes languages.
                        hidden.gameObject.SetActive(true);hidden.gameObject.SetActive(false);
                        OpenSectionHelp(target);
                        var description=Field<LocalizedText>(panel,"helpDescription");var detail=Field<LocalizedText>(panel,"helpDetail");
                        Check(HelpVisible()&&detail.Source==source&&detail.text==Localization.Text(source)&&detail.text!=source,
                            target+": Spanish help retains the untranslated live instruction binding");
                        Check(description.text!=description.Source&&description.text==Localization.Text(description.Source)
                            &&HelpTextFits(description)&&HelpTextFits(detail),target+": detailed Spanish help is translated and fits the dialog");
                        Capture("industrial-section-help-es-"+target.ToLowerInvariant().Replace(' ','-'));
                        panel.CancelPointerInteraction();Localization.SetPreference("en");
                        OpenSectionHelp(target);
                        Check(HelpVisible()&&detail.Source==source&&detail.text==source&&description.text==description.Source,
                            target+": reopening help after Spanish to English ignores the hidden label's cached translation");
                    }
                    finally
                    {
                        panel.CancelPointerInteraction();hidden.gameObject.SetActive(active);Localization.SetPreference(preference);
                    }
                }
                void CheckMoveHandle()
                {
                    var root=Field<RectTransform>(panel,"root");var position=root.position;
                    var selection=session.Selection.ToArray();string message=session.Message;
                    string world=JsonUtility.ToJson(session.Match.Snapshot());int commands=session.LastCommandSequence;
                    var handle=Field<RectTransform>(panel,"moveHandle");var affordance=Field<RectTransform>(panel,"moveAffordance");
                    var caption=Field<Text>(panel,"moveCaption");var reset=Field<RectTransform>(panel,"resetHandle");
                    panel.CancelPointerInteraction();panel.Point(offPanel,false,false);
                    var idleSize=affordance.rect.size;var hitSize=handle.rect.size;var bounds=PanelBounds(handle);
                    Check(handle.gameObject.activeInHierarchy&&hitSize==new Vector2(320,80)
                        &&bounds.yMax<root.rect.yMin&&Mathf.Abs(bounds.center.x-root.rect.center.x)<.01f,
                        "Move handle: the stable target sits centered below the panel");
                    Check(affordance.name=="Move affordance"&&!caption.gameObject.activeInHierarchy&&!HelpVisible(),
                        "Move handle: idle shows the small bar without a caption or tooltip");
                    var ray=RayTo("Move menu");
                    Check(panel.ContainsPointer(ray)&&panel.Point(ray,false,false),
                        "Move handle: a first hover outside the panel body is captured by the panel");
                    Check(affordance.rect.width>idleSize.x&&affordance.rect.height>idleSize.y
                        &&caption.gameObject.activeInHierarchy&&((LocalizedText)caption).Source=="Move window"&&handle.rect.size==hitSize,
                        "Move handle: hover expands the visible bar and shows Move window without resizing its target");
                    Check(!reset.gameObject.activeInHierarchy,"Move handle: the old Reset control is hidden during a match");
                    // Move the inactive legacy rect away from live controls, so a
                    // ghost hit cannot be mistaken for a click on the header Pause.
                    var resetPosition=reset.anchoredPosition;
                    try
                    {
                        reset.anchoredPosition=new Vector2(-reset.rect.width-100,-root.rect.height-120);
                        var center=reset.TransformPoint(reset.rect.center);var ghostRay=new Ray(center-reset.forward,reset.forward);
                        bool contains=panel.ContainsPointer(ghostRay),down=panel.Point(ghostRay,true,false),up=panel.Point(ghostRay,false,true);
                        Check(!contains&&!down&&!up&&!panel.IsDragging,"Move handle: the inactive Reset rect has no ghost hit area");
                    }
                    finally{reset.anchoredPosition=resetPosition;}
                    Check(root.position==position&&session.Selection.SetEquals(selection)&&session.Message==message
                        &&session.LastCommandSequence==commands&&JsonUtility.ToJson(session.Match.Snapshot())==world,
                        "Move handle: hovering leaves menu position, selection, production and simulation unchanged");
                    panel.Point(ray,false,false);Capture("industrial-move-hover");
                    bool offCaptured=panel.Point(offPanel,false,false);
                    Check(!offCaptured&&!panel.ContainsPointer(offPanel)&&affordance.rect.size==idleSize
                        &&handle.rect.size==hitSize&&!caption.gameObject.activeInHierarchy,
                        "Move handle: leaving the bar restores its idle appearance and releases off-panel input");
                }
                void CheckHudHelpTargets()
                {
                    var bound=Field<System.Collections.IEnumerable>(panel,"targets").Cast<object>().ToArray();
                    RectTransform RectOf(object target)=>(RectTransform)target.GetType().GetField("rect").GetValue(target);
                    var visible=bound.Where(target=>RectOf(target).gameObject.activeInHierarchy).ToArray();
                    var actionable=visible.Select(RectOf).ToList();
                    // Empty queue cards become cancellation targets when occupied;
                    // verify all five positions without altering production state.
                    if(Field<RectTransform>(panel,"productionStrip").gameObject.activeInHierarchy)
                    {
                        actionable.Add(Field<Image>(panel,"productionCard").rectTransform);
                        actionable.AddRange(Field<Image[]>(panel,"queuedCards").Select(card=>card.rectTransform));
                    }
                    foreach(var help in visible.Where(target=>target.GetType().GetField("hint").GetValue(target)!=null))
                    {
                        var rect=RectOf(help);var bounds=PanelBounds(rect);
                        var overlap=actionable.Distinct().FirstOrDefault(other=>other!=rect&&Overlaps(bounds,PanelBounds(other)));
                        Check(overlap==null,rect.name+": help target does not overlap another action"+(overlap==null?"":" ("+overlap.name+")"));
                    }
                }
                Rect PanelBounds(RectTransform rect)
                {
                    var root=Field<RectTransform>(panel,"root");var corners=new Vector3[4];rect.GetWorldCorners(corners);
                    var points=corners.Select(root.InverseTransformPoint).ToArray();
                    return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                }
                bool Overlaps(Rect a,Rect b)=>Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>.01f
                    &&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>.01f;
                void CheckHelpForSelection(string prefix)
                {
                    foreach(var action in SelectionActions.For(session).Items.Where(a=>a.Id!="cancel"&&a.Id!="clear-rally-point"))
                    {
                        Hover(action.Title);
                        Check(HelpVisible()&&Field<Text>(panel,"helpDescription").text==action.Description&&action.Description.Length>30,"Descriptive help for "+action.Id);
                        Capture(prefix+"-"+action.Id.Replace(':','-'));
                    }
                    panel.CancelPointerInteraction();
                }
                void CheckLayout()
                {
                    CheckHudHelpTargets();
                    var area=Field<RectTransform>(panel,"actions");
                    var card=Field<RectTransform>(panel,"actionCard");
                    var title=Field<Text>(panel,"optionsTitle");var selectedTitle=Field<Text>(panel,"selectedKind");
                    Check(title.gameObject.activeInHierarchy&&title.text=="OPTIONS"&&selectedTitle.gameObject.activeInHierarchy&&selectedTitle.text=="SELECTION",
                        "Options and Selection keep their section titles for every selection");
                    float padding=HudMetric("HudPadding");var cardBounds=PanelBounds(card);var areaBounds=PanelBounds(area);
                    Check(Mathf.Abs(areaBounds.xMin-cardBounds.xMin-padding)<.01f
                        &&Mathf.Abs(cardBounds.yMax-areaBounds.yMax-HudMetric("HudContentTop"))<.01f,
                        "Options content uses the shared section padding and header clearance");
                    foreach(var heading in new[]{title,selectedTitle})
                    {
                        var parentBounds=PanelBounds((RectTransform)heading.transform.parent);var headingBounds=PanelBounds(heading.rectTransform);
                        Check(heading.fontSize==HudMetric("HudTitleSize")&&Mathf.Abs(headingBounds.xMin-parentBounds.xMin-padding)<.01f
                            &&Mathf.Abs(parentBounds.yMax-headingBounds.yMax-padding)<.01f,
                            "Section heading uses consistent size and margins: "+heading.text);
                    }
                    var categoryHeaders=new[]{"BUILD STRUCTURES","TRAIN UNITS","RESEARCH","UNIT ORDERS","CANCEL / REFUND","ABILITIES"};
                    Check(!area.GetComponentsInChildren<Text>().Any(t=>categoryHeaders.Contains(t.text)),"Options contains no repeated category subtitles");
                    var buttons=area.Cast<RectTransform>().Where(t=>t.GetComponent<Image>()!=null).ToArray();
                    foreach(var b in buttons)
                        Check(b.anchoredPosition.x>=0&&b.anchoredPosition.x+b.rect.width<=area.rect.width&&-b.anchoredPosition.y+b.rect.height<=area.rect.height,"Action stays within its section: "+b.name);
                    Check(buttons.SelectMany((b,i)=>buttons.Skip(i+1).Select(other=>new Rect(b.anchoredPosition-new Vector2(0,b.rect.height),b.rect.size).Overlaps(new Rect(other.anchoredPosition-new Vector2(0,other.rect.height),other.rect.size)))).All(overlap=>!overlap),"Action buttons do not overlap");
                }
                void Capture(string name){HarvestingPreview.SavePanel(panel,"TestResults/ActionHelp/"+name+".png",preview);captures++;}
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
                if(savedPlacement)PlayerPrefs.SetString(placementKey,placement);else PlayerPrefs.DeleteKey(placementKey);PlayerPrefs.Save();
            }
            ProductionQueuePreview.Render();
            ActionWheelPreview.Render();
        }
    }
}
