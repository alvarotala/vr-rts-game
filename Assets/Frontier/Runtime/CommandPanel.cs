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
        private Text headline, economy, selection, status;
        private Font font;
        private readonly List<Target> targets = new List<Target>();
        private string signature;
        private bool settings;
        private Target pressed, hovered;
        private float pressedAt;
        private float nextText;
        private sealed class Target { public RectTransform rect; public Image image; public Action click, hold; }
        private readonly Color background = new Color(.025f,.055f,.085f,.97f), button = new Color(.075f,.15f,.21f,1), accent = new Color(.3f,.88f,.9f,1);
        private void Start()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=new GameObject("Command console",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=10;
            root=go.GetComponent<RectTransform>();root.sizeDelta=new Vector2(1100,460);
            var image=go.AddComponent<Image>();image.color=background;
            headline=Label(root,"FRONTIER / COMMAND",new Vector2(30,-18),new Vector2(1040,44),30,accent);
            economy=Label(root,"",new Vector2(30,-64),new Vector2(1040,36),22,Color.white);
            selection=Label(root,"",new Vector2(30,-103),new Vector2(1040,38),20,new Color(.65f,.76f,.82f));
            status=Label(root,"",new Vector2(30,-407),new Vector2(1040,44),20,accent);
            actions=new GameObject("Actions",typeof(RectTransform)).GetComponent<RectTransform>();actions.SetParent(root,false);TopLeft(actions,new Vector2(30,-154),new Vector2(1040,245));
            Rebuild();
        }
        private void LateUpdate()
        {
            if(root==null)return;
            root.position=Board.transform.TransformPoint(new Vector3(0,.08f,-Board.Depth*.5f-.18f));
            root.rotation=Board.transform.rotation*Quaternion.Euler(60,0,0);root.localScale=Vector3.one*Mathf.Clamp(Board.Width/1300f,.00065f,.0012f);
            string state=(!Session.Running?"setup":settings?"settings":Session.Match.Winner!=-1?"finished":string.Join(",",Session.Selection.OrderBy(x=>x)))+":"+(Session.View==null?"":string.Join(",",Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).Select(e=>e.definition+e.complete)));
            if(state!=signature){signature=state;Rebuild();}
            if(Time.unscaledTime<nextText)return;nextText=Time.unscaledTime+.15f;
            headline.text=!Session.Running?"FRONTIER / COMMAND":Session.Match.Winner!=-1?(Session.Match.Winner==0?"VICTORIA":"PARTIDA TERMINADA"):Session.IsPaused?"PARTIDA EN PAUSA":"FRONTIER / "+Session.Catalog.Faction(Session.Faction).name.ToUpperInvariant();
            economy.text=Session.View==null?"Estrategia sobre tu mesa · Quest 3":$"MINERAL  {Session.View.minerals}      CATALIZADOR  {Session.View.catalyst}      SUMINISTRO  {Session.View.supply}/{Session.View.capacity}      {Session.View.tick/1200:00}:{Session.View.tick/20%60:00}";
            var selected=Session.View?.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            if(selected==null||selected.Length==0)selection.text=!Session.Running?$"{Session.Catalog.Faction(Session.Faction).name}  vs  {Session.Catalog.Faction(Session.EnemyFaction).name} · {Session.Difficulty} · {Session.Map.title}":"Seleccioná unidades o un edificio para dar órdenes.";
            else if(selected.Length==1)
            {
                var e=selected[0];selection.text=Session.Catalog[e.definition].name+" · "+Mathf.CeilToInt(e.health)+" PV";
                if(!e.complete)selection.text+=$" · Construcción {e.progress*100:0}%";
                if(e.queue.Length>0)selection.text+=$" · Cola {e.queue.Length}: {e.queue[0].definition.Split('.').Last()} {100*(1-e.queue[0].remaining/(float)e.queue[0].total):0}%";
            }
            else selection.text=$"{selected.Length} unidades seleccionadas";
            status.text=Session.Message;
        }
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
                Add("Facción: "+ShortFaction(Session.Faction),()=>{Session.Faction=Other(Session.Faction);Rebuild();});
                Add("Rival: "+ShortFaction(Session.EnemyFaction),()=>{Session.EnemyFaction=Other(Session.EnemyFaction);Rebuild();});
                Add("IA: "+(Session.Difficulty=="easy"?"Fácil":"Normal"),()=>{Session.Difficulty=Session.Difficulty=="easy"?"normal":"easy";Rebuild();});
                Add(Session.MapId=="rift"?"Mapa: La Fractura":"Mapa: Entrenamiento",()=>{Session.LoadMap(Session.MapId=="rift"?"training":"rift");Board.Rebuild();Rebuild();});
                Add("Ubicar tablero",()=>PlaceRequested?.Invoke());
                Add("Mesa / altura 75 cm",()=>{Quest.SetManualHeight(.75f);PlaceRequested?.Invoke();});
                Add("Piso / altura 0",()=>{Quest.SetManualHeight(0);PlaceRequested?.Invoke();});
                Add("Mano: "+(Session.LeftHanded?"Izquierda":"Derecha"),()=>{Session.SetLeftHanded(!Session.LeftHanded);Rebuild();});
                Add("COMENZAR PARTIDA",()=>{if(Board.Placed)Session.StartMatch();else Session.Announce("Primero confirmá la ubicación del tablero.");});
                Add("Detectar superficies",()=>Quest.LoadRoom());
                Add("Ayuda de controles",Help);
                return;
            }
            if(settings||Session.Match.Winner!=-1)
            {
                Add("Volver a la partida",()=>{settings=false;Session.Pause("menu",false);Rebuild();});
                Add("Revancha",()=>{settings=false;Session.StartMatch();});
                Add("Reubicar tablero",()=>PlaceRequested?.Invoke());
                Add("Rendirse",()=>{Session.Pause("menu",false);Session.Send(CommandKind.Surrender);settings=false;});
                Add("Mano: "+(Session.LeftHanded?"Izquierda":"Derecha"),()=>{Session.SetLeftHanded(!Session.LeftHanded);Rebuild();});
                Add("Ayuda de controles",Help);return;
            }
            var selected=Session.View.entities.Where(e=>Session.Selection.Contains(e.id)).ToArray();
            if(selected.Length==1&&Session.Catalog[selected[0].definition].building)
            {
                var e=selected[0];string role=Session.Catalog[e.definition].role;
                if(e.complete)
                {
                    foreach(var type in Session.Catalog.Definition.archetypes.Where(t=>t.faction==Session.Faction&&!t.building&&t.producer==role))
                    {var captured=type;Add(type.name+" · "+Cost(type.minerals,type.catalyst),()=>Session.Send(CommandKind.Train,captured.id));}
                    if(role=="tech")foreach(var upgrade in Session.Catalog.Definition.upgrades)
                    {var captured=upgrade;Add(upgrade.name+" · "+Cost(upgrade.minerals,upgrade.catalyst),()=>Session.Send(CommandKind.Research,captured.id));}
                }
                Add("Cancelar / devolver",()=>Session.Send(CommandKind.Cancel));
            }
            else if(selected.Length>0)
            {
                if(selected.All(e=>Session.Catalog[e.definition].role=="worker"))
                    foreach(var type in Session.Catalog.Definition.archetypes.Where(t=>t.faction==Session.Faction&&t.building))
                    {var captured=type;Add(type.name+" · "+Cost(type.minerals,type.catalyst),()=>Session.ChooseBuild(captured.id));}
                Add("Avanzar y atacar",()=>{Session.AttackMoveMode=true;Session.Announce("Confirmá el destino del ataque.");});
                Add("Detener",()=>Session.Send(CommandKind.Stop));Add("Mantener posición",()=>Session.Send(CommandKind.Hold));
            }
            for(int g=0;g<3;g++){int n=g;Add("Grupo "+(g+1)+" · mantener: guardar",()=>Session.Group(n,false),()=>Session.Group(n,true));}
            Add("Ver mapa completo",()=>Board.Fit());Add("Pausa / opciones",()=>{settings=true;Session.Pause("menu",true);Rebuild();});
        }
        public void ToggleMenu(){settings=!settings;Session.Pause("menu",settings);Rebuild();}
        private static string Other(string faction)=>faction=="industrial"?"biological":"industrial";
        private static string ShortFaction(string faction)=>faction=="industrial"?"Consorcio":"Simbiosis";
        private static string Cost(int m,int c)=>c>0?$"{m}M {c}C":$"{m}M";
        private void Help()=>Session.Announce(Quest.Active?"Gatillo: seleccionar/arrastrar · A: ordenar · B: cancelar · sticks: desplazar/zoom":"Clic/arrastre: seleccionar · derecho: ordenar · WASD: desplazar · rueda: zoom · Espacio: pausa");
        public bool Point(Ray ray,bool down,bool up)
        {
            Target hit=null;
            foreach(var target in targets)
            {
                Vector3 origin=target.rect.InverseTransformPoint(ray.origin),direction=target.rect.InverseTransformDirection(ray.direction);
                if(Mathf.Abs(direction.z)<.00001f)continue;float t=-origin.z/direction.z;if(t<0)continue;
                Vector3 local=origin+direction*t;if(target.rect.rect.Contains(new Vector2(local.x,local.y))){hit=target;break;}
            }
            if(hovered!=hit){if(hovered!=null&&hovered.image!=null)hovered.image.color=button;hovered=hit;if(hovered!=null)hovered.image.color=new Color(.12f,.35f,.39f);}
            if(down&&hit!=null){pressed=hit;pressedAt=Time.unscaledTime;}
            if(up&&pressed!=null)
            {var p=pressed;pressed=null;if(hit==p){if(Time.unscaledTime-pressedAt>.65f&&p.hold!=null)p.hold();else p.click?.Invoke();}return true;}
            return hit!=null;
        }
        private Text Label(RectTransform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {var go=new GameObject("Label",typeof(RectTransform),typeof(Text));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);TopLeft(rect,position,size);var text=go.GetComponent<Text>();text.font=font;text.fontSize=fontSize;text.color=color;text.text=value;text.alignment=alignment;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;}
        private static void TopLeft(RectTransform rect,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
    }
}
