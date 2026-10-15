using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed class CommandPanel : MonoBehaviour
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
        private bool settings;
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
        private sealed class Target { public RectTransform rect; public Image image; public Action click, hold; }
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
            Rebuild();
        }
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
            string state=(!Session.Running?"setup":settings?"settings":Session.Match.Winner!=-1?"finished":string.Join(",",Session.Selection.OrderBy(x=>x)))+":"+(Session.View==null?"":string.Join(",",Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).Select(e=>e.definition+e.complete)));
            if(state!=signature){signature=state;Rebuild();}
            if(Time.unscaledTime<nextText)return;nextText=Time.unscaledTime+.15f;
            headline.text=!Session.Running?"FRONTIER / COMMAND":Session.Match.Winner!=-1?(Session.Match.Winner==0?"VICTORY":"MATCH OVER"):Session.IsPaused?"MATCH PAUSED":"FRONTIER / "+Session.Catalog.Faction(Session.Faction).name.ToUpperInvariant();
            economy.text=Session.View==null?"Tabletop strategy · Quest 3":$"MINERALS  {Session.View.minerals}      CATALYST  {Session.View.catalyst}      SUPPLY  {Session.View.supply}/{Session.View.capacity}      {Session.View.tick/1200:00}:{Session.View.tick/20%60:00}";
            var selected=Session.View?.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            if(selected==null||selected.Length==0)
            {
                var pointed=Session.View?.entities.FirstOrDefault(e=>e.id==Board.Hovered&&!e.remembered);
                selection.text=!Session.Running?$"{Session.Catalog.Faction(Session.Faction).name}  vs  {Session.Catalog.Faction(Session.EnemyFaction).name} · {DifficultyLabel} · {Session.Map.title}":
                    pointed!=null?UnitLabel(Session.Catalog[pointed.definition])+" · "+(pointed.owner==0?"Ally":"Enemy"):
                    Session.SelectedResource!=0?"Deposit selected · use a context order to assign workers.":"Select units or a building to give orders.";
            }
            else if(selected.Length==1)
            {
                var e=selected[0];selection.text=UnitLabel(Session.Catalog[e.definition])+" · "+Mathf.CeilToInt(e.health)+" HP";
                if(!e.complete)selection.text+=$" · Building {e.progress*100:0}%";
                if(e.queue.Length>0)selection.text+=$" · Queue {e.queue.Length}: {QueueLabel(e.queue[0].definition)} {100*(1-e.queue[0].remaining/(float)e.queue[0].total):0}%";
            }
            else selection.text=string.Join(" · ",selected.GroupBy(e=>
                {var type=Session.Catalog[e.definition];return type.building?type.name:UnitVisuals.RoleName(type.role);}).Select(g=>$"{g.Count()}× {g.Key}"));
            UpdateHarvesting(selected);
            status.text=Session.Message;
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
        private void Rebuild()
        {
            if(actions==null)return;
            foreach(Transform child in actions)Destroy(child.gameObject);targets.Clear();pressed=null;hovered=null;
            int index=0;
            void Add(string label,Action action,Action hold=null)
            {
                int n=index++;int col=n%4,row=n/4;
                var go=new GameObject(label,typeof(RectTransform),typeof(Image));var rt=go.GetComponent<RectTransform>();rt.SetParent(actions,false);TopLeft(rt,new Vector2(col*260,-row*58),new Vector2(246,49));
                var image=go.GetComponent<Image>();image.color=button;Label(rt,label,new Vector2(9,-4),new Vector2(228,41),18,Color.white,TextAnchor.MiddleCenter);
                targets.Add(new Target{rect=rt,image=image,click=action,hold=hold});
            }
            if(!Session.Running)
            {
                Add("Faction: "+ShortFaction(Session.Faction),()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                Add("Opponent: "+ShortFaction(Session.EnemyFaction),()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                Add("AI: "+DifficultyLabel,()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":"easy";Rebuild();});
                Add("Map: "+Session.Map.title,()=>{Session.LoadMap(Session.MapId=="rift"?"training":"rift");Board.Rebuild();Rebuild();});
                Add("Place board",()=>PlaceRequested?.Invoke());
                Add("Table / height 75 cm",()=>{Quest.SetManualHeight(.75f);PlaceRequested?.Invoke();});
                Add("Floor / height 0",()=>{Quest.SetManualHeight(0);PlaceRequested?.Invoke();});
                Add("Hand: "+(Session.LeftHanded?"Left":"Right"),()=>{Session.SetLeftHanded(!Session.LeftHanded);Rebuild();});
                Add("START MATCH",()=>{if(Board.Placed)Session.StartMatch();else Session.Announce("Confirm the board placement first.");});
                Add("Detect surfaces",()=>Quest.LoadRoom());
                Add("Controls",Help);
                return;
            }
            if(settings||Session.Match.Winner!=-1)
            {
                Add("Resume match",()=>{settings=false;Session.Pause("menu",false);Rebuild();});
                Add("Rematch",()=>{settings=false;Session.StartMatch();});
                Add("Reposition board",()=>PlaceRequested?.Invoke());
                Add("Surrender",()=>{Session.Pause("menu",false);Session.Send(CommandKind.Surrender);settings=false;});
                Add("Hand: "+(Session.LeftHanded?"Left":"Right"),()=>{Session.SetLeftHanded(!Session.LeftHanded);Rebuild();});
                Add("Controls",Help);return;
            }
            var selected=Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            if(selected.Length==1&&Session.Catalog[selected[0].definition].building)
            {
                var e=selected[0];string role=Session.Catalog[e.definition].role;
                if(e.complete)
                {
                    foreach(var type in Session.Catalog.Definition.archetypes.Where(t=>t.faction==Session.Faction&&!t.building&&t.producer==role))
                    {var captured=type;Add(UnitLabel(type)+" · "+Cost(type.minerals,type.catalyst),()=>Session.Send(CommandKind.Train,captured.id));}
                    if(role=="tech")foreach(var upgrade in Session.Catalog.Definition.upgrades)
                    {var captured=upgrade;Add(upgrade.name+" · "+Cost(upgrade.minerals,upgrade.catalyst),()=>Session.Send(CommandKind.Research,captured.id));}
                }
                Add("Cancel / refund",()=>Session.Send(CommandKind.Cancel));
            }
            else if(selected.Length>0)
            {
                if(selected.All(e=>Session.Catalog[e.definition].role=="worker"))
                    foreach(var type in Session.Catalog.Definition.archetypes.Where(t=>t.faction==Session.Faction&&t.building))
                    {var captured=type;Add(type.name+" · "+Cost(type.minerals,type.catalyst),()=>Session.ChooseBuild(captured.id));}
                Add("Attack Move",()=>{Session.AttackMoveMode=true;Session.Announce("Confirm the attack destination.");});
                Add("Stop",()=>Session.Send(CommandKind.Stop));Add("Hold Position",()=>Session.Send(CommandKind.Hold));
            }
            for(int g=0;g<3;g++){int n=g;Add("Group "+(g+1)+" · hold to save",()=>Session.Group(n,false),()=>Session.Group(n,true));}
            Add("Full map",()=>Board.Fit());Add("Pause / options",()=>{settings=true;Session.Pause("menu",true);Rebuild();});
        }
        public void ToggleMenu(){settings=!settings;Session.Pause("menu",settings);Rebuild();}
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
        private static string Cost(int m,int c)=>c>0?$"{m}M {c}C":$"{m}M";
        private static string UnitLabel(Archetype type)=>type.building?type.name:UnitVisuals.RoleName(type.role)+" / "+type.name;
        private string DifficultyLabel=>Session.Difficulty=="easy"?"Easy":"Normal";
        private string QueueLabel(string id)=>Session.Catalog.Has(id)?Session.Catalog[id].name:Session.Catalog.Upgrade(id)?.name??id;
        private void Help()
        {
            string order=Session.LeftHanded?"X":"A",cancel=Session.LeftHanded?"Y":"B";
            Session.Announce(Quest.Active?$"Trigger: select/drag · {order}: order · {cancel}: cancel · sticks: pan/zoom":"Click/drag: select · right-click: order · WASD: pan · wheel: zoom · Space: pause");
        }
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
            if(hovered!=hit){if(hovered!=null&&hovered.image!=null)hovered.image.color=button;hovered=hit;if(hovered!=null)hovered.image.color=new Color(.12f,.35f,.39f);}
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
