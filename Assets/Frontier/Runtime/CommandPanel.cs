using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel : MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public QuestBridge Quest;
        public Action PlaceRequested;
        private Canvas canvas;
        private RectTransform root, actions;
        private Text headline, economy, selection, harvesting, status;
        private Font font;
        private readonly List<Target> targets = new List<Target>();
        private string signature;
        public enum MenuPage { Main, MatchSetup, Settings, Controls, Actions, Pause, LeaveConfirmation, Result }
        public MenuPage Page { get; private set; } = MenuPage.Main;
        public bool AcceptsGameInput => Page==MenuPage.Actions&&Session.Running&&!Session.IsPaused&&Session.Match.Winner==-1;
        public bool ShowsBoard => Session.Running||Page==MenuPage.MatchSetup||Quest.Placing;
        private MenuPage returnPage=MenuPage.Main;
        private int actionIndex;
        private Target pressed, hovered;
        private float pressedAt;
        private float nextText;
        private RectTransform moveHandle, resetHandle;
        private Image moveImage, resetImage;
        private bool moving, customPosition, positionInitialized;
        private float grabDistance;
        private Vector3 grabOffset, boardOffset;
        private const string PlacementKey="frontier.panel-placement.v1";
        [Serializable] private sealed class PanelPlacement { public Vector3 offset; }
        private sealed class Target { public RectTransform rect; public Image image; public Color normal; public Action click, hold; }
        private readonly Color background = new Color(.025f,.055f,.085f,.97f), button = new Color(.075f,.15f,.21f,1), accent = new Color(.3f,.88f,.9f,1);
        private void Start()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=new GameObject("Command console",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=10;
            root=go.GetComponent<RectTransform>();root.sizeDelta=new Vector2(1100,526);
            var image=go.AddComponent<Image>();image.color=background;
            moveHandle=Handle("Move menu",new Vector2(20,-12),new Vector2(810,44),"MOVE MENU · hold trigger and drag",out moveImage);
            resetHandle=Handle("Reset menu",new Vector2(844,-12),new Vector2(236,44),"Reset position",out resetImage);
            headline=Label(root,"FRONTIER / COMMAND",new Vector2(30,-84),new Vector2(1040,44),30,accent);
            economy=Label(root,"",new Vector2(30,-130),new Vector2(1040,36),22,Color.white);
            selection=Label(root,"",new Vector2(30,-169),new Vector2(1040,38),20,new Color(.65f,.76f,.82f));
            harvesting=Label(root,"",new Vector2(30,-207),new Vector2(1040,28),18,accent);
            status=Label(root,"",new Vector2(30,-473),new Vector2(1040,44),20,accent);
            actions=new GameObject("Actions",typeof(RectTransform)).GetComponent<RectTransform>();actions.SetParent(root,false);TopLeft(actions,new Vector2(30,-240),new Vector2(1040,225));
            CreateMatchHud();
            // Shared labels and action buttons sit above the HUD section backgrounds.
            foreach(var label in new[]{headline,economy,selection,harvesting,status})label.transform.SetAsLastSibling();
            actions.SetAsLastSibling();
            if(PlayerPrefs.HasKey(PlacementKey))
            {
                try
                {
                    var saved=JsonUtility.FromJson<PanelPlacement>(PlayerPrefs.GetString(PlacementKey));
                    if(saved!=null&&IsFinite(saved.offset)&&saved.offset.magnitude<8)
                    {boardOffset=saved.offset;customPosition=true;positionInitialized=true;}
                }
                catch(ArgumentException){PlayerPrefs.DeleteKey(PlacementKey);}
            }
            Session.NewMatch+=OnMatchStarted;Session.MatchClosed+=OnMatchClosed;
            Page=Session.Running?(Session.Match.Winner!=-1?MenuPage.Result:MenuPage.Actions):MenuPage.Main;
            Rebuild();
        }
        private void OnDestroy()
        {
            if(Session==null)return;
            Session.NewMatch-=OnMatchStarted;Session.MatchClosed-=OnMatchClosed;
        }
        private void OnMatchStarted()=>Show(MenuPage.Actions);
        private void OnMatchClosed()=>Show(MenuPage.Main);
        private void LateUpdate()
        {
            if(root==null)return;
            if(!positionInitialized||Quest.Placing)ResetPosition(false);
            root.localScale=Vector3.one*Mathf.Clamp(Board.Width/1300f,.00065f,.0012f);
            if(!moving)
            {
                root.position=Board.transform.TransformPoint(new Vector3(boardOffset.x*Board.Width,boardOffset.y,boardOffset.z*Board.Depth));
                FaceViewer();
            }
            if(Session.Running&&Session.Match.Winner!=-1&&Page!=MenuPage.Result)Show(MenuPage.Result);
            Board.SetVisible(ShowsBoard);
            string state=Page+":"+string.Join(",",Session.Selection.OrderBy(x=>x))+":"+(Session.View==null?"":string.Join(",",Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).Select(e=>e.definition+e.complete+":"+e.queue.Length)));
            if(state!=signature){signature=state;Rebuild();}
            if(Time.unscaledTime<nextText)return;nextText=Time.unscaledTime+.15f;
            if(Page!=MenuPage.Actions){UpdateMenuText();return;}
            UpdateMatchHud();
        }
        private void UpdateHarvesting(VisibleEntity[] selected)
        {
            harvesting.text="";harvesting.color=accent;
            if(Session.View==null)return;
            var view=Session.View;
            int pointedResource=Board.Pointer.HasValue?Board.ResourceAt(Board.Pointer.Value):0;
            var resource=view.resources.FirstOrDefault(r=>r.id==pointedResource);
            var building=view.entities.FirstOrDefault(e=>e.id==Board.Hovered&&e.owner==view.player);
            if(resource==null&&building==null)
            {
                resource=view.resources.FirstOrDefault(r=>r.id==Session.SelectedResource);
                if(selected?.Length==1)building=selected[0];
            }
            if(resource==null&&building!=null&&Session.Catalog[building.definition].role=="extractor")
            {
                int size=Session.Catalog[building.definition].footprint;
                resource=view.resources.FirstOrDefault(r=>r.kind=="catalyst"&&r.position.x>=building.position.x&&r.position.x<building.position.x+size
                    &&r.position.y>=building.position.y&&r.position.y<building.position.y+size);
            }
            if(resource!=null)
            {
                string name=resource.kind=="mineral"?"MINERALS":"GAS / CATALYST";
                string remaining=resource.remaining<0?"?":resource.remaining.ToString();
                int optimum=resource.remaining==0?0:Harvesting.OptimalWorkers(resource.kind);
                harvesting.text=$"{name} · {Saturation(resource.assignedWorkers,optimum)} · Remaining: {remaining}";
                if(resource.kind=="catalyst"&&resource.extractorId==0&&resource.remaining!=0)harvesting.text+=" · Requires a completed extractor";
                if(resource.assignedWorkers>resource.optimalWorkers)harvesting.color=new Color(1,.65f,.3f);
            }
            else if(building!=null&&Session.Catalog[building.definition].role=="hq")
            {
                var minerals=view.resources.Where(r=>r.baseId==building.id&&r.kind=="mineral").ToArray();
                var gas=view.resources.Where(r=>r.baseId==building.id&&r.kind=="catalyst").ToArray();
                harvesting.text="MINERALS · "+Saturation(minerals.Sum(r=>r.assignedWorkers),minerals.Sum(r=>r.optimalWorkers))+
                    "     GAS · "+Saturation(gas.Sum(r=>r.assignedWorkers),gas.Sum(r=>r.optimalWorkers));
            }
            else if(selected!=null&&selected.Any(e=>Session.Catalog[e.definition].role=="worker"))
                harvesting.text="Optimal: 2 workers per mineral patch · 3 per gas · Automatically assigned to nearby deposits";
        }
        private static string Saturation(int assigned,int optimal)=>$"Workers {assigned}/{optimal}"+
            (assigned>optimal?" (excess)":optimal>0&&assigned==optimal?" (optimal)":"");
        private void UpdateMenuText()
        {
            harvesting.text="";harvesting.color=accent;
            status.text=Session.Message;
            switch(Page)
            {
                case MenuPage.Main:
                    headline.text="FRONTIER / COMMAND";economy.text="MAIN MENU";
                    selection.text="Lead your faction from a tabletop battlefield. Play offline against the AI.";
                    status.text="Choose New Match to begin.";break;
                case MenuPage.MatchSetup:
                    headline.text="NEW MATCH";economy.text="SKIRMISH / LOCAL AI";
                    selection.text=$"{Session.Catalog.Faction(Session.Faction).name}  vs  {Session.Catalog.Faction(Session.EnemyFaction).name} · {DifficultyLabel} · {Session.Map.title}";
                    harvesting.text=Board.Placed?"BOARD READY":"PLACE YOUR BOARD BEFORE STARTING";break;
                case MenuPage.Settings:
                    headline.text="SETTINGS";economy.text="CONTROLS / PLAY SPACE";
                    selection.text="Choose your dominant hand and adjust the board's position in your room.";break;
                case MenuPage.Controls:
                    headline.text="CONTROLS";economy.text=Quest.Active?"TOUCH CONTROLLERS":"MOUSE / KEYBOARD";
                    string order=Session.LeftHanded?"X":"A",cancel=Session.LeftHanded?"Y":"B";
                    selection.text=Quest.Active?
                        $"Main trigger: select a unit / drag to select a group\nOff-hand trigger: add to selection\n{order}: move, attack, gather or repair · {cancel}: cancel / back / pause\nOff-hand stick: pan · Main stick: zoom\nPress main stick: full map · Press off-hand stick: recall menu\nMOVE MENU: hold trigger and drag · Main stick: adjust distance\nControl groups: press to recall / hold to save\nMenu button: pause / resume":
                        "Left-click: select / drag to select a group · Shift: add to selection\nRight-click: move, attack, gather or repair\nEscape: cancel / back / pause · Space: pause / resume\nWASD: pan · Mouse wheel: zoom · F: full map\nM: recall menu · Drag MOVE MENU to reposition it\nWhile dragging the menu: mouse wheel adjusts distance\n1–3: recall group · Ctrl+1–3: save group";
                    status.text="Use Back to return to the previous menu.";break;
                case MenuPage.Pause:
                    headline.text="MATCH PAUSED";economy.text=Session.Map.title;
                    selection.text="Your match is paused. Resume when you are ready.";
                    status.text="Returning to the main menu ends the current match.";break;
                case MenuPage.LeaveConfirmation:
                    headline.text="LEAVE MATCH?";economy.text="RETURN TO MAIN MENU";
                    selection.text="Leave this match and return to the main menu? Match progress will be lost.";
                    status.text="Choose Keep playing to resume the match.";break;
                case MenuPage.Result:
                    headline.text=Session.Match.Winner==0?"VICTORY":Session.Match.Winner==2?"DRAW":"DEFEAT";
                    economy.text="MATCH COMPLETE · "+Session.Map.title;
                    selection.text="Play again with the same setup or return to the main menu.";
                    status.text="Choose Rematch for a fresh start.";break;
            }
            if(Quest.Placing)status.text=Session.Message;
        }
        private void Add(string label,Action action,Action hold=null,bool primary=false)
        {
            bool grid=Page==MenuPage.Actions||Page==MenuPage.MatchSetup;
            int columns=Page==MenuPage.Main?1:grid?4:2;
            int n=actionIndex++,col=n%columns,row=n/columns;
            float width=columns==1?600:columns==2?505:246,height=grid?49:58;
            var go=new GameObject(label,typeof(RectTransform),typeof(Image));var rt=go.GetComponent<RectTransform>();rt.SetParent(actions,false);
            TopLeft(rt,new Vector2((columns==1?220:0)+col*(width+15),-row*(height+9)),new Vector2(width,height));
            var image=go.GetComponent<Image>();image.color=primary?new Color(.1f,.32f,.36f):button;
            Label(rt,label,new Vector2(9,-4),new Vector2(width-18,height-8),grid?18:22,Color.white,TextAnchor.MiddleCenter);
            targets.Add(new Target{rect=rt,image=image,normal=image.color,click=action,hold=hold});
        }
        private void Rebuild()
        {
            if(actions==null)return;
            // Hide the old controls immediately, including between pointer events in the same frame.
            for(int i=actions.childCount-1;i>=0;i--)
            {var child=actions.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}
            if(hovered!=null&&hovered.image!=null)hovered.image.color=hovered.normal;
            targets.Clear();pressed=null;hovered=null;actionIndex=0;nextText=0;
            LayoutPage();
            if(Page==MenuPage.Main)
            {
                Add("New Match",()=>Show(MenuPage.MatchSetup),primary:true);
                Add("Settings",()=>OpenSubmenu(MenuPage.Settings));
                Add("Controls",()=>OpenSubmenu(MenuPage.Controls));return;
            }
            if(Page==MenuPage.MatchSetup)
            {
                Add("Faction: "+ShortFaction(Session.Faction),()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                Add("Opponent: "+ShortFaction(Session.EnemyFaction),()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                Add("AI: "+DifficultyLabel,()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":"easy";Rebuild();});
                Add("Map: "+Session.Map.title,()=>{Session.LoadMap(Session.MapId=="rift"?"training":"rift");Board.Rebuild();Rebuild();});
                Add("Place board",()=>PlaceRequested?.Invoke());
                Add("Table / height 75 cm",()=>{Quest.SetManualHeight(.75f);PlaceRequested?.Invoke();});
                Add("Floor / height 0",()=>{Quest.SetManualHeight(0);PlaceRequested?.Invoke();});
                Add("Detect surfaces",()=>Quest.LoadRoom());
                Add("START MATCH",()=>{if(Board.Placed&&!Quest.Placing)Session.StartMatch();else Session.Announce("Confirm the board placement first.");},primary:true);
                Add("Back",Back);return;
            }
            if(Page==MenuPage.Settings)
            {
                Add("Hand: "+(Session.LeftHanded?"Left":"Right"),()=>{Session.SetLeftHanded(!Session.LeftHanded);Rebuild();});
                Add("Reposition board",()=>PlaceRequested?.Invoke());
                Add("Table / height 75 cm",()=>{Quest.SetManualHeight(.75f);PlaceRequested?.Invoke();});
                Add("Floor / height 0",()=>{Quest.SetManualHeight(0);PlaceRequested?.Invoke();});
                Add("Detect surfaces",()=>Quest.LoadRoom());Add("Back",Back);return;
            }
            if(Page==MenuPage.Controls){Add("Back",Back);return;}
            if(Page==MenuPage.Pause)
            {
                Add("Resume match",()=>Show(MenuPage.Actions),primary:true);
                Add("Settings",()=>OpenSubmenu(MenuPage.Settings));
                Add("Controls",()=>OpenSubmenu(MenuPage.Controls));
                Add("Return to main menu",()=>Show(MenuPage.LeaveConfirmation));return;
            }
            if(Page==MenuPage.LeaveConfirmation)
            {
                Add("Keep playing",()=>Show(MenuPage.Actions),primary:true);
                Add("Leave match",()=>Session.LeaveMatch());return;
            }
            if(Page==MenuPage.Result)
            {
                Add("Rematch",()=>Session.StartMatch(),primary:true);
                Add("Main menu",()=>Session.LeaveMatch());return;
            }
            BuildMatchActions();
        }
        private void Show(MenuPage page)
        {
            Page=page;signature=null;moving=false;
            // Only this menu's pause is released. Tracking and headset pauses stay authoritative.
            Session.Pause("menu",Session.Running&&page!=MenuPage.Actions&&page!=MenuPage.Result);
            Board.Hovered=0;Board.Pointer=null;Board.DrawSelection(default,default,false);
            Board.SetVisible(ShowsBoard);Rebuild();
        }
        private void OpenSubmenu(MenuPage page){returnPage=Page;Show(page);}
        public void Back()
        {
            if(Quest.Placing){Quest.CancelPlacement();return;}
            switch(Page)
            {
                case MenuPage.MatchSetup:Show(MenuPage.Main);break;
                case MenuPage.Settings:case MenuPage.Controls:Show(returnPage);break;
                case MenuPage.Actions:Show(MenuPage.Pause);break;
                case MenuPage.Pause:Show(MenuPage.Actions);break;
                case MenuPage.LeaveConfirmation:Show(MenuPage.Pause);break;
                case MenuPage.Result:Session.LeaveMatch();break;
            }
        }
        public void ToggleMenu()
        {
            if(Quest.Placing){Quest.CancelPlacement();return;}
            if(!Session.Running){Back();return;}
            if(Session.Match.Winner!=-1)return;
            Show(Page==MenuPage.Actions?MenuPage.Pause:MenuPage.Actions);
        }
        public void ResetPosition(bool forgetSaved=true)
        {
            if(forgetSaved){customPosition=false;PlayerPrefs.DeleteKey(PlacementKey);PlayerPrefs.Save();}
            if(customPosition)return;
            var viewer=Board.transform.InverseTransformPoint(Quest.Camera.transform.position);
            var away=new Vector2(-viewer.x,-viewer.z).normalized;
            if(away.sqrMagnitude<.1f)away=Vector2.up;
            boardOffset=new Vector3(away.x*.65f,Mathf.Max(.65f,viewer.y+.15f),away.y*.65f);
            positionInitialized=true;
        }
        // Called on a controller shortcut even when the console has been moved out of view.
        public void Recall(){moving=false;pressed=null;ResetPosition();Session.Announce("Menu reset above the board.");}
        private void FaceViewer()
        {
            var direction=root.position-Quest.Camera.transform.position;
            if(direction.sqrMagnitude<.001f)return;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            root.rotation=Quaternion.Euler(Mathf.Clamp(pitch,-25,25),Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
        }
        private void SavePosition()
        {
            var local=Board.transform.InverseTransformPoint(root.position);
            boardOffset=new Vector3(local.x/Board.Width,local.y,local.z/Board.Depth);
            customPosition=true;
            PlayerPrefs.SetString(PlacementKey,JsonUtility.ToJson(new PanelPlacement{offset=boardOffset}));PlayerPrefs.Save();
        }
        private void OnApplicationFocus(bool focused)
        {
            if(focused)return;
            if(moving){moving=false;SavePosition();}
            pressed=null;
        }
        private static bool IsFinite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        private static string Other(string faction)=>faction=="industrial"?"biological":"industrial";
        private static string ShortFaction(string faction)=>faction=="industrial"?"Consortium":"Symbiosis";
        private static string Cost(int m,int c)=>c>0?$"{m} M / {c} G":$"{m} M";
        private string DifficultyLabel=>Session.Difficulty=="easy"?"Easy":"Normal";
        private string QueueLabel(string id)=>Session.Catalog.Has(id)?Session.Catalog[id].name:Session.Catalog.Upgrade(id)?.name??id;
        public bool Point(Ray ray,bool down,bool up,float depth=0)
        {
            if(root==null)return false;
            if(moving)
            {
                grabDistance=Mathf.Clamp(grabDistance+depth*Time.unscaledDeltaTime,.4f,3);
                var position=ray.GetPoint(grabDistance)+grabOffset;
                position.y=Mathf.Clamp(position.y,.25f,2.5f);
                var fromBoard=position-Board.transform.position;
                root.position=Board.transform.position+Vector3.ClampMagnitude(fromBoard,3);
                if(up){moving=false;SavePosition();}
                return true;
            }
            bool onMove=Hit(moveHandle,ray,out float distance);
            bool onReset=Hit(resetHandle,ray,out _);
            moveImage.color=onMove?new Color(.12f,.35f,.39f):button;
            resetImage.color=onReset?new Color(.12f,.35f,.39f):button;
            if(down&&onMove)
            {
                moving=true;pressed=null;grabDistance=distance;grabOffset=root.position-ray.GetPoint(distance);
                return true;
            }
            if(down&&onReset){Recall();return true;}
            Target hit=null;
            foreach(var target in targets)
            {
                if(Hit(target.rect,ray,out _)){hit=target;break;}
            }
            if(hovered!=hit){if(hovered!=null&&hovered.image!=null)hovered.image.color=hovered.normal;hovered=hit;if(hovered!=null)hovered.image.color=new Color(.12f,.35f,.39f);}
            if(down&&hit!=null){pressed=hit;pressedAt=Time.unscaledTime;}
            if(up&&pressed!=null)
            {var p=pressed;pressed=null;if(hit==p){if(Time.unscaledTime-pressedAt>.65f&&p.hold!=null)p.hold();else p.click?.Invoke();}return true;}
            // Empty areas also consume the ray, preventing orders through the panel.
            return pressed!=null||Hit(root,ray,out _);
        }
        private static bool Hit(RectTransform rect,Ray ray,out float distance)
        {
            distance=0;if(rect==null)return false;
            if(!new Plane(rect.forward,rect.position).Raycast(ray,out distance)||distance>8)return false;
            var local=rect.InverseTransformPoint(ray.GetPoint(distance));return rect.rect.Contains(new Vector2(local.x,local.y));
        }
        private RectTransform Handle(string name,Vector2 position,Vector2 size,string label,out Image image)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(root,false);TopLeft(rect,position,size);
            image=go.GetComponent<Image>();image.color=button;
            Label(rect,label,new Vector2(8,0),size-new Vector2(16,0),18,accent,TextAnchor.MiddleCenter);return rect;
        }
        private Text Label(RectTransform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {var go=new GameObject("Label",typeof(RectTransform),typeof(Text));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.color=color;text.text=value;text.alignment=alignment;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;}
        private static void TopLeft(RectTransform rect,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
