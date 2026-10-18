using System;
using System.Collections.Generic;
using System.IO;
using Frontier.Core;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Android;

namespace Frontier.Runtime
{
    public sealed class QuestBridge : MonoBehaviour
    {
        public GameSession Session;
        public BoardView Board;
        public HotkeyBindings Bindings { get; set; }
        public OVRInput.Controller ActiveController=>Bindings?.TouchController??OVRInput.Controller.RTouch;
        public Camera Camera { get; private set; }
        public bool Active { get; private set; }
        public bool Placing { get; private set; }
        public Transform TrackingSpace { get; private set; }
        private OVRCameraRig rig;
        private OVRManager manager;
        private OVRPassthroughLayer passthrough;
        private MRUK mruk;
        private OVRSpatialAnchor anchor;
        private Transform anchorRoot;
        private float manualHeight=.75f;
        private bool useScene=true,busy;
        private Transform placementParent;
        private Vector3 placementPosition;
        private Quaternion placementRotation;
        private float placementWidth;
        private bool wasPlaced;
        private int anchorGeneration;
        private string SavePath=>Path.Combine(Application.persistentDataPath,"board-placement.json");
        private void Awake()
        {
            Active=Application.platform==RuntimePlatform.Android&&!Application.isEditor;
            if(Active)
            {
                var go=new GameObject("Quest rig");rig=go.AddComponent<OVRCameraRig>();
                manager=go.AddComponent<OVRManager>();manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;manager.isInsightPassthroughEnabled=true;
                passthrough=go.AddComponent<OVRPassthroughLayer>(); // Meta's default is the background underlay.
                Camera=rig.centerEyeAnchor.GetComponent<Camera>();Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=Color.clear;Camera.nearClipPlane=.03f;Camera.farClipPlane=30;
                TrackingSpace=rig.trackingSpace;
                var room=new GameObject("Room understanding");mruk=room.AddComponent<MRUK>();
                mruk.EnableWorldLock=false; // The board has its own persistent spatial anchor.
                mruk.SceneSettings=new MRUK.MRUKSettings{LoadSceneOnStartup=false,RoomPrefabs=Array.Empty<GameObject>(),SceneJsons=Array.Empty<TextAsset>()};
                OVRManager.TrackingLost+=TrackingLost;OVRManager.TrackingAcquired+=TrackingAcquired;
                OVRManager.HMDUnmounted+=Unmounted;OVRManager.HMDMounted+=Mounted;
                OVRManager.VrFocusLost+=FocusLost;OVRManager.VrFocusAcquired+=FocusGained;
            }
            else
            {
                var go=new GameObject("Desktop camera",typeof(Camera),typeof(AudioListener));Camera=go.GetComponent<Camera>();
                Camera.transform.position=new Vector3(0,2.2f,-2.25f);Camera.transform.LookAt(new Vector3(0,1.45f,.1f));
                Camera.fieldOfView=60;Camera.nearClipPlane=.02f;Camera.farClipPlane=40;Camera.backgroundColor=new Color(.018f,.031f,.055f);Camera.clearFlags=CameraClearFlags.SolidColor;
            }
            Camera.tag="MainCamera";
        }
        private void Start()
        {
            Board.transform.position=new Vector3(0,.75f,0);Board.Placed=!Active;
            if(Active)
            {
                // Keep the setup console in front of the wearer before any anchor exists.
                var forward=Vector3.ProjectOnPlane(Camera.transform.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
                var center=Camera.transform.position+forward*1.25f;center.y=.75f;
                Board.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward));
                if(OVRManager.display!=null)OVRManager.display.displayFrequency=72;Restore();LoadRoom();
            }
        }
        public void SetManualHeight(float height){manualHeight=height;useScene=false;}
        private void Update()
        {
            if(!Active||manager==null)return;
            // Contextual boundaryless is owned by the OS and only requested during MR.
            // The board remains a child of its spatial anchor, never of the head/camera rig.
            manager.shouldBoundaryVisibilityBeSuppressed=passthrough!=null&&passthrough.isActiveAndEnabled
                &&!passthrough.hidden&&passthrough.textureOpacity>=.99f
                &&manager.isInsightPassthroughEnabled&&OVRManager.IsInsightPassthroughInitialized();
        }
        private void OnDisable(){if(manager!=null)manager.shouldBoundaryVisibilityBeSuppressed=false;}
        public async void LoadRoom()
        {
            useScene=true;if(!Active||mruk==null){Session.Announce("Desktop mode uses a manual plane.");return;}
            try
            {
                const string permission="com.oculus.permission.USE_SCENE";
                if(!Permission.HasUserAuthorizedPermission(permission))
                {
                    var callbacks=new PermissionCallbacks();callbacks.PermissionGranted+=_=>LoadRoom();callbacks.PermissionDenied+=_=>Session.Announce("Room access unavailable. You can use the manual plane.");
                    Permission.RequestUserPermission(permission,callbacks);return;
                }
                await mruk.LoadSceneFromDevice(false);Session.Announce(mruk.GetCurrentRoom()!=null?"Surfaces available. Place the board.":"No room data found. Use Table or Floor.");
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Could not load room data. Use manual placement.");}
        }
        public void BeginPlacement()
        {
            if(Placing)return;
            if(busy){Session.Announce("Wait for the anchor to finish saving.");return;}
            placementParent=Board.transform.parent;placementPosition=Board.transform.position;placementRotation=Board.transform.rotation;
            placementWidth=Board.Width;wasPlaced=Board.Placed;
            Placing=true;Board.Placed=false;Session.Pause("placement",true);Board.transform.SetParent(null,true);
            var bindings=Bindings??new HotkeyBindings();
            Session.Announce(bindings.Name(Active,"select")+": place · "+bindings.Name(Active,"back")+": cancel"+
                (Active?" · "+bindings.HandName+" stick: size/rotate · "+bindings.Name(true,"add")+" + stick: height":""));
        }
        public void CancelPlacement()
        {
            if(!Placing)return;
            Placing=false;Board.transform.SetParent(placementParent,true);
            Board.transform.SetPositionAndRotation(placementPosition,placementRotation);Board.Width=placementWidth;Board.Placed=wasPlaced;
            Session.Pause("placement",false);Session.Announce(wasPlaced?"Previous board placement kept.":"Place the board before starting a match.");
        }
        public void PlaceRay(Ray ray,float height,Vector2 sizeRotate,bool confirm)
        {
            if(!Placing)return;
            manualHeight=Mathf.Clamp(manualHeight+height*Time.unscaledDeltaTime*.3f,0,1.5f);
            Board.Width=Mathf.Clamp(Board.Width+sizeRotate.y*Time.unscaledDeltaTime*.5f,.6f,2);
            Board.transform.Rotate(Vector3.up,sizeRotate.x*Time.unscaledDeltaTime*45,Space.World);
            bool hit=false;Vector3 point=default;
            var room=mruk==null?null:mruk.GetCurrentRoom();
            if(useScene&&room!=null&&room.Raycast(ray,5,new LabelFilter(MRUKAnchor.SceneLabels.TABLE|MRUKAnchor.SceneLabels.FLOOR),out var surface,out _)&&Vector3.Dot(surface.normal,Vector3.up)>.9f){point=surface.point;hit=true;}
            if(!hit){var plane=new Plane(Vector3.up,new Vector3(0,manualHeight,0));if(plane.Raycast(ray,out float d)&&d<5){point=ray.GetPoint(d);hit=true;}}
            if(!hit)return;
            Board.transform.position=point+Vector3.up*.005f;
            if(confirm){Placing=false;Board.Placed=true;Session.Pause("placement",false);Session.Pause("tracking",false);SaveAnchor();Session.Announce("Board placed. You can start or resume the match.");}
        }
        public Ray PointerRay()
        {
            var controller=ActiveController;
            return new Ray(TrackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(controller)),TrackingSpace.TransformDirection(OVRInput.GetLocalControllerRotation(controller)*Vector3.forward));
        }
        private async void SaveAnchor()
        {
            if(!Active||busy)return;busy=true;int generation=++anchorGeneration;
            var previous=anchor;
            try
            {
                anchorRoot=new GameObject("Board spatial anchor").transform;anchorRoot.SetPositionAndRotation(Board.transform.position,Board.transform.rotation);
                anchor=anchorRoot.gameObject.AddComponent<OVRSpatialAnchor>();
                if(!await anchor.WhenCreatedAsync()||!await anchor.WhenLocalizedAsync())throw new InvalidOperationException("Could not locate the anchor.");
                if(generation!=anchorGeneration)return;
                // Keep session placement world-locked even if persistence is unavailable.
                Board.transform.SetParent(anchorRoot,true);
                var result=await anchor.SaveAnchorAsync();if(!result.Success)throw new InvalidOperationException("Could not save the anchor.");
                var placement=new BoardPlacement{anchorUuid=anchor.Uuid.ToString(),width=Board.Width};
                File.WriteAllText(SavePath,JsonUtility.ToJson(placement,true));
                if(previous!=null){await previous.EraseAnchorAsync();Destroy(previous.gameObject);}
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Board placed for this session; the anchor was not saved.");}
            finally{busy=false;}
        }
        private async void Restore()
        {
            if(!File.Exists(SavePath))return;
            int generation=anchorGeneration;
            try
            {
                var saved=JsonUtility.FromJson<BoardPlacement>(File.ReadAllText(SavePath));
                if(saved.schemaVersion!=1||!Guid.TryParse(saved.anchorUuid,out var uuid))throw new InvalidDataException("Invalid saved anchor.");
                var anchors=new List<OVRSpatialAnchor.UnboundAnchor>();var result=await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[]{uuid},anchors);
                if(!result.Success||anchors.Count==0)throw new InvalidOperationException("Anchor not found.");
                var unbound=anchors[0];if(!unbound.Localized&&!await unbound.LocalizeAsync(10))throw new InvalidOperationException("Could not locate the anchor.");
                if(generation!=anchorGeneration||Placing)return;
                anchorRoot=new GameObject("Restored board anchor").transform;anchor=anchorRoot.gameObject.AddComponent<OVRSpatialAnchor>();unbound.BindTo(anchor);
                Board.transform.SetParent(anchorRoot,false);Board.transform.localPosition=Vector3.zero;Board.transform.localRotation=Quaternion.identity;
                Board.Width=Mathf.Clamp(saved.width,.6f,2);Board.Placed=true;Session.Announce("Previous board placement restored.");
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Place the board on your table or floor again.");}
        }
        private void TrackingLost(){Board.Placed=false;Session.Pause("tracking",true);Session.Announce("Tracking lost. Restore tracking and confirm the board placement.");}
        private void TrackingAcquired(){if(anchor!=null&&anchor.Localized){Board.Placed=true;Session.Pause("tracking",false);}else BeginPlacement();}
        private void Unmounted()=>Session.Pause("headset",true);
        private void Mounted()=>Session.Pause("headset",false);
        private void FocusLost()=>Session.Pause("vrfocus",true);
        private void FocusGained()=>Session.Pause("vrfocus",false);
        private void OnDestroy()
        {
            OVRManager.TrackingLost-=TrackingLost;OVRManager.TrackingAcquired-=TrackingAcquired;OVRManager.HMDUnmounted-=Unmounted;OVRManager.HMDMounted-=Mounted;OVRManager.VrFocusLost-=FocusLost;OVRManager.VrFocusAcquired-=FocusGained;
        }
    }
}
