#if FRONTIER_BROWSER
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier.Runtime
{
    // Navigation is expressed in map cells. The shared board keeps its native
    // world scale, meshes, picking and shader bounds; only this camera moves.
    public sealed class BrowserRtsCamera : MonoBehaviour
    {
        public QuestBridge Quest;
        public CommandPanel Panel;
        public bool EdgeScroll=true;
        public Vector2 Target { get; private set; }
        public float Distance { get; private set; }=34;
        public Rect Viewport=>Camera.rect;
        public Camera Camera { get; private set; }
        private const float Pitch=56,FieldOfView=38,MinimumDistance=12;
        private float smoothDistance=34,maximumDistance=95;
        private bool fitted,resetRequested=true,middleDrag;
        private MapDefinition knownMap;
        private GameSession subscribedSession;
        private Vector2 previousViewportSize;
        private GameSession Session=>Quest?.Session;
        private BoardView Board=>Quest?.Board;

        private void Awake()
        {
            Camera=GetComponent<Camera>();Camera.fieldOfView=FieldOfView;
            Camera.backgroundColor=new Color(.018f,.031f,.055f);
            Camera.clearFlags=CameraClearFlags.SolidColor;
        }
        private void Start()
        {
            subscribedSession=Session;
            if(subscribedSession!=null)
            {subscribedSession.NewMatch+=ResetView;subscribedSession.MatchClosed+=ResetView;}
        }
        private void OnDestroy()
        {
            if(subscribedSession!=null)
            {subscribedSession.NewMatch-=ResetView;subscribedSession.MatchClosed-=ResetView;}
        }
        private void ResetView(){resetRequested=true;middleDrag=false;}

        public void SetViewport(Rect normalized)
        {
            float x=Mathf.Clamp(normalized.x,0,.99f),y=Mathf.Clamp(normalized.y,0,.99f);
            Camera.rect=new Rect(x,y,Mathf.Clamp(normalized.width,.01f,1-x),Mathf.Clamp(normalized.height,.01f,1-y));
        }
        public void Fit()
        {
            if(Session?.Map==null)return;
            fitted=true;Target=new Vector2(Session.Map.width*.5f,Session.Map.height*.5f);
            Distance=maximumDistance=FitDistance();smoothDistance=Distance;
            Apply();
        }
        public void CenterOn(Point point)
        {fitted=false;Target=new Vector2(point.x,point.y);ClampTarget();Apply();}
        public void CenterOnSelection()
        {
            var selected=Session?.View?.entities.Where(e=>Session.Selection.Contains(e.id)&&!e.remembered&&e.health>0).ToArray();
            if(selected==null||selected.Length==0){CenterOnBase();return;}
            Vector2 sum=Vector2.zero;
            foreach(var entity in selected)sum+=EntityCenter(entity);
            CenterOn(new Point(sum.x/selected.Length,sum.y/selected.Length));
        }
        public void CenterOnBase()
        {
            if(Session?.Map==null)return;
            var headquarters=Session.View?.entities.FirstOrDefault(e=>e.owner==Session.LocalPlayer&&!e.remembered&&Session.Catalog[e.definition].role=="hq");
            if(headquarters==null){CenterOn(new Point(Session.Map.width*.5f,Session.Map.height*.5f));return;}
            var point=EntityCenter(headquarters);CenterOn(new Point(point.x,point.y));
        }
        private Vector2 EntityCenter(VisibleEntity entity)
        {
            var point=Session.PresentationPoint(entity);var type=Session.Catalog[entity.definition];
            float half=type.building?type.footprint*.5f:0;
            return new Vector2(point.x+half,point.y+half);
        }
        public void Navigate(Vector2 move,float zoom,float delta)
        {
            if(Session?.Map==null)return;
            if(move.sqrMagnitude>1)move.Normalize();
            if(move.sqrMagnitude>0)Pan(move*smoothDistance*.9f*Mathf.Clamp(delta,0,.1f));
            // CommanderInput already scales Input System wheel units by .04.
            // Replay also calls Navigate while hovering its panel. A wheel
            // event belongs to the battlefield only when the pointer is there.
            if(zoom!=0&&Mouse.current!=null&&PointerOverMap(Mouse.current.position.ReadValue()))
            {fitted=false;Distance=Mathf.Clamp(Distance*Mathf.Exp(-zoom*.0375f),MinimumDistance,maximumDistance);}
        }
        public void Pan(Vector2 mapDelta)
        {fitted=false;Target+=mapDelta;ClampTarget();}
        public void NavigateTouch(Vector2 screenDelta,float zoomRatio)
        {
            if(Session?.Map==null)return;
            // Finger movement pans in map space; spreading fingers decreases
            // distance. The caller owns the gesture and excludes HUD touches.
            Pan(-screenDelta*(smoothDistance*.9f/Mathf.Max(1,Camera.pixelRect.height)));
            if(zoomRatio>0&&Mathf.Abs(zoomRatio-1)>.0001f)
            {fitted=false;Distance=Mathf.Clamp(Distance*zoomRatio,MinimumDistance,maximumDistance);}
        }
        private void ClampTarget()
        {
            if(Session?.Map==null)return;
            Target=new Vector2(Mathf.Clamp(Target.x,0,Session.Map.width),Mathf.Clamp(Target.y,0,Session.Map.height));
        }
        private float FitDistance()
        {
            float pitch=Pitch*Mathf.Deg2Rad,tangent=Mathf.Tan(FieldOfView*.5f*Mathf.Deg2Rad);
            float width=Session.Map.width*.5f,height=Session.Map.height*.5f;
            float nearestDepth=height*Mathf.Cos(pitch);
            // Include the nearest corners of the perspective view, not just the
            // centre cross-section, so Fit also works in a narrow browser window.
            return Mathf.Max(95,Mathf.Max(width/(tangent*Mathf.Max(.1f,Camera.aspect)),height*Mathf.Sin(pitch)/tangent)+nearestDepth)*1.08f;
        }
        private void LateUpdate()
        {
            if(Session?.Map==null||Board?.Content==null)return;
            if(knownMap!=Session.Map||resetRequested)
            {
                knownMap=Session.Map;resetRequested=false;maximumDistance=FitDistance();
                if(Session.Running){Distance=smoothDistance=34;CenterOnBase();}
                else Fit();
            }
            var viewportSize=Camera.pixelRect.size;
            if(viewportSize!=previousViewportSize)
            {previousViewportSize=viewportSize;maximumDistance=FitDistance();if(fitted)Distance=smoothDistance=maximumDistance;}
            ReadPointerNavigation();
            smoothDistance=Mathf.Lerp(smoothDistance,Distance,1-Mathf.Exp(-12*Mathf.Min(Time.unscaledDeltaTime,.1f)));
            Apply();
        }
        private void ReadPointerNavigation()
        {
            if(BrowserMobileInput.IsTouchMode){middleDrag=false;return;}
            var mouse=Mouse.current;
            bool enabled=Application.isFocused&&Panel!=null&&!Panel.IsDragging
                &&(Panel.AcceptsGameInput||Panel.AcceptsReplayInput||Panel.Page==CommandPanel.MenuPage.MatchSetup);
            if(!enabled||mouse==null){middleDrag=false;return;}
            var position=mouse.position.ReadValue();var viewport=Camera.pixelRect;
            bool overMap=PointerOverMap(position);
            bool primaryHeld=mouse.leftButton.isPressed||mouse.rightButton.isPressed;
            if(mouse.middleButton.wasPressedThisFrame)middleDrag=overMap&&!primaryHeld;
            if(!mouse.middleButton.isPressed||primaryHeld)middleDrag=false;
            if(middleDrag)
            {
                var delta=mouse.delta.ReadValue();
                Pan(new Vector2(-delta.x,-delta.y)*(smoothDistance*.9f/Mathf.Max(400,viewport.height)));
                return;
            }
            if(!EdgeScroll||!overMap||mouse.leftButton.isPressed||mouse.rightButton.isPressed)return;
            const float margin=6;
            var edge=new Vector2(position.x<=viewport.xMin+margin?-1:position.x>=viewport.xMax-margin?1:0,
                position.y<=viewport.yMin+margin?-1:position.y>=viewport.yMax-margin?1:0);
            if(edge.sqrMagnitude>0)Navigate(edge,0,Time.unscaledDeltaTime);
        }
        private bool PointerOverMap(Vector2 position)
            =>Application.isFocused&&Camera.pixelRect.Contains(position)
                &&(Panel==null||!Panel.ContainsPointer(Camera.ScreenPointToRay(position)));
        private void Apply()
        {
            if(Session?.Map==null||Board?.Content==null)return;
            var point=new Point(Target.x,Target.y);
            var target=Board.Content.TransformPoint(new Vector3(point.x,Board.HeightAt(point),point.y));
            float distance=smoothDistance*Board.CellScale,pitch=Pitch*Mathf.Deg2Rad;
            Camera.nearClipPlane=Mathf.Max(.0001f,Board.CellScale*.05f);
            Camera.farClipPlane=Mathf.Max(10,Board.CellScale*600);
            transform.SetPositionAndRotation(target+new Vector3(0,Mathf.Sin(pitch),-Mathf.Cos(pitch))*distance,Quaternion.Euler(Pitch,0,0));
        }
    }
}
#endif
