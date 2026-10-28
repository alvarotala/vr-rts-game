#if FRONTIER_BROWSER
using UnityEngine;
using UnityEngine.InputSystem;
using OVRInput = Frontier.Runtime.BrowserTouchInput;

namespace Frontier.Runtime
{
    // The browser uses the shared game session and board with a desktop camera.
    // The native Quest adapter and physical-board lifecycle stay in QuestBridge.cs.
    public sealed class QuestBridge : MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public HotkeyBindings Bindings { get; set; }
        public OVRInput.Controller ActiveController=>Bindings?.TouchController??OVRInput.Controller.RTouch;
        public bool PointerTracked=>true;
        public bool HeadTracked=>Camera!=null;
        public Camera Camera { get; private set; }
        public BrowserRtsCamera Navigation { get; private set; }
        public bool Active=>false;
        public bool Placing=>false;
        public bool StartPending=>false;
        public Transform TrackingSpace=>null;
        public float PlacementHeight=>0;
        private Camera backgroundCamera;

        private void Awake()
        {
            // The battlefield has a smaller viewport while the screen HUD is
            // visible. Clear the entire target first so its margins never keep
            // pixels from a previous menu or countdown frame.
            var background=new GameObject("Browser background camera",typeof(Camera));
            backgroundCamera=background.GetComponent<Camera>();
            backgroundCamera.depth=-100;backgroundCamera.cullingMask=0;
            backgroundCamera.rect=new Rect(0,0,1,1);
            backgroundCamera.clearFlags=CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor=new Color(8/255f,11/255f,16/255f,1);
            backgroundCamera.allowHDR=false;backgroundCamera.allowMSAA=false;
            backgroundCamera.useOcclusionCulling=false;
            var go=new GameObject("Browser RTS camera",typeof(Camera),typeof(AudioListener));
            Camera=go.GetComponent<Camera>();Camera.tag="MainCamera";Camera.depth=0;
            Navigation=go.AddComponent<BrowserRtsCamera>();Navigation.Quest=this;
        }
        private void Start()
        {
            Board.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            Board.Placed=true;Board.Fit();
        }
        private void OnDestroy()
        {
            if(Camera!=null)Destroy(Camera.gameObject);
            if(backgroundCamera!=null)Destroy(backgroundCamera.gameObject);
        }

        public void CancelStartRequest() { }
        public void RequestStartMatch()=>Session.StartMatch(countdown:true);
        public void RequestTutorial()
        {if(!Session.Running&&!Session.IsConnecting)Session.StartTutorial(false,countdown:true);}
        public void RequestOnlineMatch()
        {if(!Session.Running&&!Session.IsConnecting)Session.ConnectOnline();}
        public void RequestPublicMatch()
        {if(!Session.Running&&!Session.IsConnecting)_=Session.FindPublicMatchAsync();}
        public void RequestReplay(string id)=>Session.StartReplay(id);

        // Keep the shared panel API usable; browser setup never enters MR placement.
        public void SetManualHeight(float height) { }
        public void AdjustPlacementHeight(float delta) { }
        public void LoadRoom()=>Navigation.Fit();
        public void BeginPlacement()=>Navigation.Fit();
        public void CancelPlacement() { }
        public void PlaceRay(Ray ray,float height,Vector2 sizeRotate,bool confirm,float? deltaTime=null) { }
        public Ray PointerRay()=>Mouse.current!=null?Camera.ScreenPointToRay(Mouse.current.position.ReadValue()):new Ray(Camera.transform.position,Camera.transform.forward);
        public Quaternion PointerRotation()=>Camera.transform.rotation;
    }
}
#endif
