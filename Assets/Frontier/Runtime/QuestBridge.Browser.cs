#if FRONTIER_BROWSER
using UnityEngine;
using UnityEngine.InputSystem;
using OVRInput = Frontier.Runtime.BrowserTouchInput;

namespace Frontier.Runtime
{
    // Desktop camera and manual placement for the reduced project, in both Editor
    // and WebGL player. The native Quest adapter remains in QuestBridge.cs.
    public sealed class QuestBridge : MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public HotkeyBindings Bindings { get; set; }
        public OVRInput.Controller ActiveController=>Bindings?.TouchController??OVRInput.Controller.RTouch;
        public bool PointerTracked=>true;
        public bool HeadTracked=>Camera!=null;
        public Camera Camera { get; private set; }
        public bool Active { get; private set; }
        public bool Placing { get; private set; }
        public Transform TrackingSpace { get; private set; }
        private float manualHeight=.75f;
        public float PlacementHeight=>manualHeight;
        private bool useScene=true,pendingStart,pendingOnline,pendingPublicMatch,pendingTutorial;
        private string pendingReplay;
        public bool StartPending=>pendingStart;
        private Transform placementParent;
        private Vector3 placementPosition;
        private Quaternion placementRotation;
        private float placementWidth,placementManualHeight;
        private bool placementUseScene,wasPlaced;

        private void Awake()
        {
            var go=new GameObject("Desktop camera",typeof(Camera),typeof(AudioListener));Camera=go.GetComponent<Camera>();
            Camera.transform.position=new Vector3(0,2.2f,-2.25f);Camera.transform.LookAt(new Vector3(0,1.45f,.1f));
            Camera.fieldOfView=60;Camera.nearClipPlane=.02f;Camera.farClipPlane=40;Camera.backgroundColor=new Color(.018f,.031f,.055f);Camera.clearFlags=CameraClearFlags.SolidColor;
            Camera.tag="MainCamera";
        }
        private void Start()
        {
            Board.transform.position=new Vector3(0,.75f,0);Board.Placed=true;
        }
        public void SetManualHeight(float height){manualHeight=Mathf.Clamp(height,0,1.5f);useScene=false;}
        public void AdjustPlacementHeight(float delta)
        {
            if(!Placing||Session.HasSystemPause||delta==0)return;
            if(useScene)manualHeight=Mathf.Clamp(Board.transform.position.y-.005f,0,1.5f);
            useScene=false;manualHeight=Mathf.Clamp(manualHeight+delta,0,1.5f);
            var position=Board.transform.position;position.y=manualHeight+.005f;Board.transform.position=position;
            AnnouncePlacement();
        }
        private void AnnouncePlacement()
        {
            string message="Height "+Mathf.RoundToInt(manualHeight*100)+" cm · Left-click: place · Escape: cancel · Page Up / Down: height";
            if(Session.Message!=message)Session.Announce(message);
        }
        public void LoadRoom()
        {
            useScene=true;Session.Announce("Desktop mode uses a manual plane.");
        }
        public void CancelStartRequest(){pendingStart=false;pendingReplay=null;pendingOnline=false;pendingPublicMatch=false;pendingTutorial=false;}
        public void RequestStartMatch(){pendingReplay=null;pendingOnline=false;pendingPublicMatch=false;pendingTutorial=false;ContinueStartRequest();}
        public void RequestTutorial()
        {
            if(Session.Running||Session.IsConnecting)return;
            pendingReplay=null;pendingOnline=false;pendingPublicMatch=false;pendingTutorial=true;ContinueStartRequest();
        }
        public void RequestOnlineMatch()
        {
            if(Session.Running||Session.IsConnecting)return;
            pendingReplay=null;pendingTutorial=false;pendingPublicMatch=false;pendingOnline=true;ContinueStartRequest();
        }
        public void RequestPublicMatch()
        {
            if(Session.Running||Session.IsConnecting)return;
            pendingReplay=null;pendingTutorial=false;pendingOnline=false;pendingPublicMatch=true;ContinueStartRequest();
        }
        public void RequestReplay(string id)
        {
            if(Session.Running){Session.Announce("Exit the current match or replay first.");return;}
            pendingOnline=false;pendingPublicMatch=false;pendingTutorial=false;pendingReplay=id;ContinueStartRequest();
        }
        private void CompleteStartRequest()
        {
            string replay=pendingReplay;bool online=pendingOnline,publicMatch=pendingPublicMatch,tutorial=pendingTutorial;CancelStartRequest();
            if(replay!=null)Session.StartReplay(replay);else if(publicMatch)_=Session.FindPublicMatchAsync();else if(online)Session.ConnectOnline();else if(tutorial)Session.StartTutorial(Active,countdown:true);else Session.StartMatch(countdown:true);
        }
        private void ContinueStartRequest()
        {
            if(Board.Placed&&!Placing){CompleteStartRequest();return;}
            BeginPlacement();pendingStart=Placing;
        }
        public void BeginPlacement()
        {
            if(Placing)return;
            placementParent=Board.transform.parent;placementPosition=Board.transform.position;placementRotation=Board.transform.rotation;
            placementWidth=Board.Width;wasPlaced=Board.Placed;
            placementManualHeight=manualHeight;placementUseScene=useScene;
            if(wasPlaced)manualHeight=Mathf.Clamp(Board.transform.position.y-.005f,0,1.5f);
            Placing=true;Board.Placed=false;Session.Pause("placement",true);Board.transform.SetParent(null,true);
            AnnouncePlacement();
        }
        public void CancelPlacement()
        {
            if(!Placing)return;
            CancelStartRequest();Placing=false;Board.transform.SetParent(placementParent,true);
            Board.transform.SetPositionAndRotation(placementPosition,placementRotation);Board.Width=placementWidth;Board.Placed=wasPlaced;
            manualHeight=placementManualHeight;useScene=placementUseScene;
            Session.Pause("placement",false);
            Session.Announce(wasPlaced?"Previous board placement kept.":Session.IsOnline&&Session.Running?"Place the board before resuming. The online match continues.":"Place the board before starting a match.");
        }
        public void PlaceRay(Ray ray,float height,Vector2 sizeRotate,bool confirm,float? deltaTime=null)
        {
            if(!Placing||Session.HasSystemPause)return;
            float delta=Mathf.Clamp(deltaTime??Time.unscaledDeltaTime,0,.05f);
            AdjustPlacementHeight(height*delta*.3f);
            Board.Width=Mathf.Clamp(Board.Width+sizeRotate.y*delta*.5f,.6f,2);
            Board.transform.Rotate(Vector3.up,sizeRotate.x*delta*45,Space.World);
            var plane=new Plane(Vector3.up,new Vector3(0,manualHeight,0));
            if(!plane.Raycast(ray,out float distance)||distance>=5)return;
            var point=ray.GetPoint(distance);manualHeight=point.y;
            Board.transform.position=point+Vector3.up*.005f;
            AnnouncePlacement();
            if(confirm)
            {
                Placing=false;Board.Placed=true;Session.Pause("placement",false);Session.Pause("tracking",false);
                Session.Announce("Board placed. You can start or resume the match.");
                if(pendingStart)CompleteStartRequest();
            }
        }
        public Ray PointerRay()=>Mouse.current!=null?Camera.ScreenPointToRay(Mouse.current.position.ReadValue()):new Ray(Camera.transform.position,Camera.transform.forward);
        public Quaternion PointerRotation()=>Camera.transform.rotation;
    }
}
#endif
