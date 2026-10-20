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
    // Exercises the real command panel, wheel, controller router and simulation together.
    public static class CasterAbilityPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);

        [MenuItem("Frontier/Preview caster commands")]
        public static void Render()
        {
            var fixture=new GameObject("Caster input fixture");var boardObject=new GameObject("Caster board");var console=new GameObject("Caster console");
            var preview=new PreviewRenderUtility();int checks=0,captures=0;
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            Directory.CreateDirectory("TestResults/CasterControls");
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Call(board,"Start");preview.AddSingleGO(boardObject);
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest,preview.camera);
                var panel=console.AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;Call(panel,"Start");
                var input=fixture.AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Quest=quest;input.Panel=panel;
                var wheel=fixture.AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;input.Wheel=wheel;
                foreach(string faction in new[]{"industrial","biological"})
                {
                    session.Faction=faction;session.StartMatch();board.Width=1.2f;board.Fit();
                    var casterType=session.Catalog.Role(faction,"caster");var academyType=session.Catalog.Role(faction,"academy");
                    var damage=session.Catalog.Ability(casterType.abilities.Single(id=>session.Catalog.Ability(id).effect=="damage"));
                    var heal=session.Catalog.Ability(casterType.abilities.Single(id=>session.Catalog.Ability(id).effect=="heal"));
                    var caster=Add(casterType,0,new Point(15.5f,14.5f),1001);var other=Add(casterType,0,new Point(14.5f,18.5f),1002);
                    var soldier=Add(session.Catalog.Role(faction,"melee"),0,new Point(17.5f,14.5f),1003);
                    var enemy=Add(session.Catalog.Role(session.EnemyFaction,"siege"),1,new Point(21.5f,14.5f),1004);
                    var academy=Add(academyType,0,new Point(8,20),1005);session.Match.Map.Occupy(academy.position,academyType.footprint,academy.id);
                    enemy.order=CommandKind.Hold;session.Match.Players[0].minerals=3000;session.Match.Players[0].catalyst=3000;
                    Refresh();
                    var worker=session.Match.Entities.Values.First(e=>e.owner==0&&session.Catalog[e.definition].role=="worker");
                    session.Select(worker.id,false);Call(panel,"Rebuild");
                    Check(SelectionActions.For(session).Items.Count(a=>a.Building)==7,"Worker offers all seven buildings: "+faction);
                    var actions=Field<RectTransform>(panel,"actions");
                    foreach(var action in SelectionActions.For(session).Items.Where(a=>a.Building))
                    {
                        var button=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.gameObject.activeInHierarchy&&t.name==action.Title);
                        var corners=new Vector3[4];button.GetWorldCorners(corners);
                        Check(corners.All(c=>actions.rect.Contains((Vector2)actions.InverseTransformPoint(c))),"Building button remains inside panel: "+action.Title);
                    }
                    CapturePanel(faction+"-worker-menu");
                    session.Select(academy.id,false);Call(panel,"Rebuild");
                    Check(SelectionActions.For(session).Items.Any(a=>a.Title=="Train "+casterType.name)&&session.CanSetRallyPoint,"Academy exposes caster production and rally: "+faction);
                    Click("Train "+casterType.name);Check(academy.queue.Count==1&&academy.queue[0].definition==casterType.id,"Actual training button queues the caster: "+faction);

                    SelectArmy();session.Send(CommandKind.Hold);Refresh();Call(panel,"Rebuild");
                    Check(SelectionActions.For(session).Items.Count(a=>a.Category==ActionCategory.Abilities)==2,"Mixed army offers both caster abilities once: "+faction);
                    Check(Field<Text>(panel,"casterStatus").text.Contains("200 / 200")&&session.AbilityStatus(damage.id).Contains("2 ready"),"Mixed selection shows energy and ready casters: "+faction);
                    CapturePanel(faction+"-caster-menu");
                    var heldButton=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.gameObject.activeInHierarchy&&t.name==damage.name);
                    var heldCenter=heldButton.TransformPoint(heldButton.rect.center);var heldRay=new Ray(heldCenter-heldButton.forward,heldButton.forward);
                    Check(panel.Point(heldRay,true,false),"Ability button starts a held press: "+faction);
                    caster.energy=90;Refresh();
                    Check(Field<Text>(panel,"casterStatus").text.Contains("190 / 200")&&heldButton!=null&&session.PendingAbility==null,"Live energy updates without firing or replacing a held button: "+faction);
                    Check(panel.Point(heldRay,false,true)&&session.PendingAbility==damage,"Releasing the same held button still arms its ability: "+faction);
                    caster.energy=100;board.Pointer=enemy.position;Refresh();
                    Check(session.PendingAbility==damage&&caster.order==CommandKind.Hold&&other.order==CommandKind.Hold,"Menu arms a spell without changing orders: "+faction);
                    Check(Close(board.PointerColor,BoardView.DamageAbilityColor)&&Ground().GetVector("_AbilityArea").z==damage.radius&&Ground().GetVector("_AbilityRange").z==damage.range,"Damage cursor previews its area and cast range: "+faction);Capture(faction+"-damage-target");
                    session.Pause("caster-check",true);Refresh();Check(!session.AbilityPointerActive&&Ground().GetVector("_AbilityArea").z==0,"Pause hides targeting: "+faction);
                    session.Pause("caster-check",false);Refresh();Check(session.AbilityPointerActive,"Resume restores targeting: "+faction);
                    Check(session.Context(new Point(45,45),0,0)==0&&session.PendingAbility==damage&&caster.energy==100,"Fog target rejects without consuming energy or mode: "+faction);
                    session.CancelMode();Refresh();Check(!session.HasTargetingMode&&Ground().GetVector("_AbilityArea").z==0,"Cancel clears spell preview: "+faction);

                    Click(damage.name);Call(input,"OrderClick",enemy.position,enemy.id,0,false,10f);
                    Check(caster.order==CommandKind.Cast&&other.order==CommandKind.Hold&&soldier.order==CommandKind.Hold&&!session.HasTargetingMode,"A confirms one caster and leaves the army intact: "+faction);
                    Call(input,"OrderClick",enemy.position,enemy.id,0,false,10.1f);
                    Check(caster.order==CommandKind.Cast&&other.order==CommandKind.Hold,"Quick second A does not overwrite the spell with movement: "+faction);
                    float before=enemy.health;session.Match.Step();Refresh();
                    Check(caster.energy<51&&other.energy==100&&enemy.health<=before-damage.amount&&caster.order==CommandKind.Hold,"Damage applies once, spends once, resumes Hold: "+faction);
                    Check(session.View.abilityEvents.Length==1&&Field<AbilityEffects>(board,"abilityEffects").ActiveCount==1,"Accepted cast reaches the board effect renderer: "+faction);
                    Check(session.AbilityStatus(damage.id).Contains("1 ready"),"Readiness updates after first cast: "+faction);

                    var wheelRay=new Ray(new Vector3(0,1,-1),Vector3.forward);
                    wheel.ProcessUnified(wheelRay,Vector2.zero,false,false,false,false,100f);
                    wheel.ProcessUnified(wheelRay,Vector2.right,false,false,false,false,100.05f);
                    Check(wheel.HighlightedId=="ability:"+heal.id&&session.PendingAbility==null,"Opening direction immediately highlights healing without arming it: "+faction);
                    int index=wheel.Items.ToList().FindIndex(a=>a.Id=="ability:"+heal.id);
                    Check(index>=0&&wheel.Page==WheelPage.Selection,"Unified Selection submenu exposes healing: "+faction);
                    var direction=wheel.DirectionFor("ability:"+heal.id);wheel.ProcessUnified(wheelRay,direction,false,false,false,false,100.25f);
                    Check(wheel.HighlightedId=="ability:"+heal.id,"Wheel selects healing: "+faction);
                    caster.energy=0;Refresh();wheel.ProcessUnified(wheelRay,direction,false,false,false,false,100.30f);
                    Check(wheel.HighlightedId=="ability:"+heal.id&&wheel.Items[index].Detail.Contains("1 ready"),"Live wheel updates readiness without moving selection: "+faction);
                    wheel.ProcessUnified(wheelRay,direction,true,true,false,false,100.35f);Refresh();
                    Check(wheel.IsOpen&&session.PendingAbility==heal,"Wheel keeps Selection open with healing armed: "+faction);
                    wheel.ProcessUnified(wheelRay,Vector2.zero,false,false,true,false,100.40f);
                    wheel.ProcessUnified(wheelRay,Vector2.zero,false,false,false,false,101.25f);Refresh();
                    Check(!wheel.IsOpen&&session.PendingAbility==heal&&Close(board.PointerColor,BoardView.HealingAbilityColor),"Releasing the stick closes Selection and keeps the green healing cursor armed: "+faction);
                    soldier.health=session.Catalog[soldier.definition].health-20;board.Pointer=soldier.position;Refresh();Capture(faction+"-heal-target");
                    var groundRay=new Ray(board.Content.TransformPoint(new Vector3(soldier.position.x,0,soldier.position.y))+Vector3.up,Vector3.down);
                    Call(input,"PointBoard",groundRay,true,false,false,false);Call(input,"PointBoard",groundRay,false,true,false,false);
                    Check(other.order==CommandKind.Cast&&!session.HasTargetingMode&&session.Selection.Contains(soldier.id),"Trigger confirms healing without selecting the pointed ally: "+faction);
                    session.Match.Entities.Remove(enemy.id);session.Match.Step();Refresh();
                    Check(soldier.health==session.Catalog[soldier.definition].health&&other.energy<61,"Healing caps at full health and spends caster energy: "+faction);
                    Call(panel,"UpdateMatchActionDetails");
                    Check(!session.ChooseAbility(heal.id)&&(session.Message.Contains("cooldown")||session.Message.Contains("energy")),"Cooling down caster rejects recast with readable feedback: "+faction);

                    caster.energy=100;caster.abilityCooldowns.Clear();Refresh();session.ChooseAbility(damage.id);
                    session.Select(worker.id,false);Refresh();Check(session.PendingAbility==null,"Selecting away from casters cancels targeting: "+faction);
                    session.Select(caster.id,false);session.ChooseAbility(damage.id);session.ChooseAttackMove();Check(session.PendingAbility==null&&session.AttackMoveMode,"Attack mode replaces spell targeting: "+faction);
                    session.ChooseAbility(damage.id);session.ChooseBuild(academyType.id);Check(session.PendingAbility==null&&session.BuildDefinition==academyType.id,"Construction replaces spell targeting: "+faction);
                    session.CancelMode();session.ChooseAbility(damage.id);session.LeaveMatch();Check(!session.HasTargetingMode&&!session.AbilityPointerActive,"Leaving clears ability state: "+faction);

                    EntityState Add(Archetype type,int owner,Point position,int id)
                    {var entity=new EntityState{id=id,owner=owner,definition=type.id,position=position,health=type.health,energy=type.energyMax};session.Match.Entities.Add(id,entity);return entity;}
                    void SelectArmy(){session.Select(caster.id,false);session.Select(other.id,true);session.Select(soldier.id,true);}
                }
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Board")),"Board, models and targeting shader compiles");
                Debug.Log($"FRONTIER_CASTER_CONTROLS passed: {checks} checks, {captures} views; no player build");
                void Refresh(){Call(session,"RefreshView");Call(board,"LateUpdate");Call(panel,"UpdateMatchHud");}
                Material Ground()=>board.Content.Find("Terrain").GetComponent<Renderer>().sharedMaterial;
                bool Close(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.01f;
                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("CASTER_CONTROL_PASS: "+message);}
                void Click(string title)
                {
                    Call(panel,"Rebuild");var button=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.gameObject.activeInHierarchy&&t.name==title);
                    var center=button.TransformPoint(button.rect.center);var ray=new Ray(center-button.forward,button.forward);
                    Check(panel.Point(ray,true,false)&&panel.Point(ray,false,true),"Real panel button accepts click: "+title);
                }
                void Capture(string name)
                {
                    var point=board.Pointer.Value;var center=board.Content.TransformPoint(new Vector3(point.x,board.HeightAt(point),point.y));float size=board.CellScale*10;
                    preview.camera.orthographic=true;preview.camera.orthographicSize=size;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.transform.position=center+board.transform.up*size*2-board.transform.forward*size*2.4f;preview.camera.transform.LookAt(center,board.transform.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,900));preview.Render(true);var snapshot=preview.EndStaticPreview();
                    File.WriteAllBytes("TestResults/CasterControls/"+name+".png",snapshot.EncodeToPNG());Object.DestroyImmediate(snapshot);captures++;
                }
                void CapturePanel(string name)
                {
                    Refresh();
                    boardObject.SetActive(false);
                    try{HarvestingPreview.SavePanel(panel,"TestResults/CasterControls/"+name+".png",preview);captures++;}
                    finally{boardObject.SetActive(true);}
                }
            }
            finally
            {
                Object.DestroyImmediate(console);Object.DestroyImmediate(fixture);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
            CasterVisualPreview.Render();AbilityEffectsPreview.Render();AttackPointerPreview.Render();
        }

        [MenuItem("Frontier/Validate expanded roster layouts")]
        public static void ValidateLayouts()
        {
            SelectionGridPreview.Render();ActionHelpPreview.Render();RallyPointInputChecks.Run();
        }
    }
}
