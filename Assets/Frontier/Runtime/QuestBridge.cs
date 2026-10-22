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
        public bool PointerTracked=>!Active||Application.isEditor||(OVRInput.GetControllerPositionTracked(ActiveController)&&OVRInput.GetControllerOrientationTracked(ActiveController));
        public bool HeadTracked=>Camera!=null&&(!Active||Application.isEditor||(headPoseUpdated&&OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)&&OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter)));
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
        public float PlacementHeight=>manualHeight;
        private bool useScene=true,busy,restoring,pendingStart,pendingOnline;
        private bool onlinePlacementRequired,onlinePlacementPending;
        private string pendingReplay;
        private bool restoredPlacement,checkingSavedBoard,savedPoseSample;
        private Vector3 savedBoardOffset;
        private float savedPoseSince;
        private const float SavedBoardDistance=3f, SavedPoseSettleTime=.75f;
        public bool StartPending => pendingStart;
        private Transform placementParent;
        private Vector3 placementPosition;
        private Quaternion placementRotation;
        private float placementWidth;
        private float placementManualHeight;
        private bool placementUseScene;
        private bool wasPlaced;
        private bool headPoseUpdated;
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
                rig.UpdatedAnchors+=HeadPoseUpdated;
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
            if(Camera.GetComponent<AudioListener>()==null)Camera.gameObject.AddComponent<AudioListener>();
        }
        private void Start()
        {
            Session.NewMatch+=EnsureOnlineBoardPlacement;Session.MatchClosed+=EnsureOnlineBoardPlacement;
            Board.transform.position=new Vector3(0,.75f,0);Board.Placed=!Active;
            if(Active)
            {
                // Provisional board position only; the setup console uses its own viewer-relative placement.
                var forward=Vector3.ProjectOnPlane(Camera.transform.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
                var center=Camera.transform.position+forward*1.25f;center.y=.75f;
                Board.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward));
                if(OVRManager.display!=null)OVRManager.display.displayFrequency=72;Restore();
            }
        }
        public void SetManualHeight(float height){manualHeight=Mathf.Clamp(height,0,1.5f);useScene=false;}
        public void AdjustPlacementHeight(float delta)
        {
            if(!Placing||Session.HasSystemPause||delta==0)return;
            // Once the player adjusts a detected surface, keep a manual plane at
            // that visible height instead of snapping back to the room's table.
            if(useScene)manualHeight=Mathf.Clamp(Board.transform.position.y-.005f,0,1.5f);
            useScene=false;manualHeight=Mathf.Clamp(manualHeight+delta,0,1.5f);
            var position=Board.transform.position;position.y=manualHeight+.005f;Board.transform.position=position;
            AnnouncePlacement();
        }
        private void AnnouncePlacement()
        {
            var bindings=Bindings??new HotkeyBindings();
            string controls=Active?bindings.MenuConfirmName+": place · "+bindings.MenuBackName+": cancel · Grip / Trigger: shrink / enlarge · Stick: height / rotate"
                :"Left-click: place · Escape: cancel · Page Up / Down: height";
            string message="Height "+Mathf.RoundToInt(manualHeight*100)+" cm · "+controls;
            if(Session.Message!=message)Session.Announce(message);
        }
        private void Update()
        {
            EnsureOnlineBoardPlacement();
            if(!Active||manager==null)return;
            // Contextual boundaryless is owned by the OS and only requested during MR.
            // The board remains a child of its spatial anchor, never of the head/camera rig.
            manager.shouldBoundaryVisibilityBeSuppressed=passthrough!=null&&passthrough.isActiveAndEnabled
                &&!passthrough.hidden&&passthrough.textureOpacity>=.99f
                &&manager.isInsightPassthroughEnabled&&OVRManager.IsInsightPassthroughInitialized();
            CheckSavedBoard(HeadTracked,Time.unscaledTime,SavePath);
        }
        private void EnsureOnlineBoardPlacement()
        {
            if(Session==null||Board==null)return;
            bool required=Active&&Session.IsOnline&&Session.Running&&Session.Winner==-1&&!Board.Placed;
            if(!required)
            {
                bool release=onlinePlacementRequired&&!Placing;
                bool cancel=onlinePlacementRequired&&Placing&&(!Session.IsOnline||!Session.Running||Session.Winner!=-1);
                onlinePlacementRequired=onlinePlacementPending=false;
                if(cancel)CancelPlacement();else if(release)Session.Pause("placement",false);
                return;
            }
            if(!onlinePlacementRequired)
            {
                onlinePlacementRequired=onlinePlacementPending=true;
                // A match can start after tracking was lost in the waiting room.
                // Block local orders immediately, even while an anchor is saving.
                Session.Pause("placement",true);
            }
            if(onlinePlacementPending&&!Placing&&!busy&&!Session.HasSystemPause&&HeadTracked)BeginPlacement();
        }
        private void OnDisable(){headPoseUpdated=false;if(manager!=null)manager.shouldBoundaryVisibilityBeSuppressed=false;}
        private void HeadPoseUpdated(OVRCameraRig cameraRig)=>headPoseUpdated=true;
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
                await mruk.LoadSceneFromDevice(false);Session.Announce(mruk.GetCurrentRoom()!=null?"Surfaces available. Place the board.":"No room data found. Adjust the height while placing the board.");
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Could not load room data. Use manual placement.");}
        }
        public void CancelStartRequest(){pendingStart=false;pendingReplay=null;pendingOnline=false;}
        public void RequestStartMatch(){pendingReplay=null;pendingOnline=false;ContinueStartRequest();}
        public void RequestOnlineMatch()
        {
            if(Session.Running||Session.IsConnecting)return;
            pendingReplay=null;pendingOnline=true;ContinueStartRequest();
        }
        public void RequestReplay(string id)
        {
            if(Session.Running){Session.Announce("Exit the current match or replay first.");return;}
            pendingOnline=false;pendingReplay=id;ContinueStartRequest();
        }
        private void CompleteStartRequest()
        {
            string replay=pendingReplay;bool online=pendingOnline;pendingStart=false;pendingReplay=null;pendingOnline=false;
            if(replay!=null)Session.StartReplay(replay);else if(online)Session.ConnectOnline();else Session.StartMatch(countdown:true);
        }
        private void ContinueStartRequest()
        {
            if(restoring){pendingStart=true;Session.Announce("Restoring your saved board…");return;}
            if(restoredPlacement&&!Placing&&Camera!=null&&SavedBoardIsDistant(Board.transform.position-Camera.transform.position))checkingSavedBoard=true;
            if(checkingSavedBoard&&!Placing){pendingStart=true;Session.Announce("Checking your saved board position…");return;}
            if(Board.Placed&&!Placing){CompleteStartRequest();return;}
            BeginPlacement();pendingStart=Placing;
        }
        public void BeginPlacement()
        {
            if(Placing)return;
            if(busy){Session.Announce("Wait for the anchor to finish saving.");return;}
            anchorGeneration++;
            placementParent=Board.transform.parent;placementPosition=Board.transform.position;placementRotation=Board.transform.rotation;
            placementWidth=Board.Width;wasPlaced=Board.Placed;
            placementManualHeight=manualHeight;placementUseScene=useScene;
            if(wasPlaced)manualHeight=Mathf.Clamp(Board.transform.position.y-.005f,0,1.5f);
            onlinePlacementPending=false;
            Placing=true;Board.Placed=false;Session.Pause("placement",true);Board.transform.SetParent(null,true);
            AnnouncePlacement();
        }
        public void CancelPlacement()
        {
            if(!Placing)return;
            CancelStartRequest();Placing=false;Board.transform.SetParent(placementParent,true);
            Board.transform.SetPositionAndRotation(placementPosition,placementRotation);Board.Width=placementWidth;Board.Placed=wasPlaced;
            manualHeight=placementManualHeight;useScene=placementUseScene;
            Session.Pause("placement",onlinePlacementRequired&&!Board.Placed);
            Session.Announce(wasPlaced?"Previous board placement kept.":Session.IsOnline&&Session.Running?"Place the board before resuming. The online match continues.":"Place the board before starting a match.");
        }
        public void PlaceRay(Ray ray,float height,Vector2 sizeRotate,bool confirm,float? deltaTime=null)
        {
            if(!Placing||Session.HasSystemPause)return;
            float delta=Mathf.Clamp(deltaTime??Time.unscaledDeltaTime,0,.05f);
            AdjustPlacementHeight(height*delta*.3f);
            Board.Width=Mathf.Clamp(Board.Width+sizeRotate.y*delta*.5f,.6f,2);
            Board.transform.Rotate(Vector3.up,sizeRotate.x*delta*45,Space.World);
            bool hit=false;Vector3 point=default;
            var room=mruk==null?null:mruk.GetCurrentRoom();
            if(useScene&&room!=null&&room.Raycast(ray,5,new LabelFilter(MRUKAnchor.SceneLabels.TABLE|MRUKAnchor.SceneLabels.FLOOR),out var surface,out _)&&Vector3.Dot(surface.normal,Vector3.up)>.9f){point=surface.point;hit=true;}
            if(!hit){var plane=new Plane(Vector3.up,new Vector3(0,manualHeight,0));if(plane.Raycast(ray,out float d)&&d<5){point=ray.GetPoint(d);hit=true;}}
            if(!hit)return;
            manualHeight=point.y;
            Board.transform.position=point+Vector3.up*.005f;
            AnnouncePlacement();
            if(confirm){restoredPlacement=checkingSavedBoard=savedPoseSample=false;Placing=false;Board.Placed=true;Session.Pause("placement",false);Session.Pause("tracking",false);SaveAnchor();Session.Announce("Board placed. You can start or resume the match.");if(pendingStart)CompleteStartRequest();}
        }
        public Ray PointerRay()
        {
            var controller=ActiveController;
            return new Ray(TrackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(controller)),PointerRotation()*Vector3.forward);
        }
        public Quaternion PointerRotation() => TrackingSpace.rotation * OVRInput.GetLocalControllerRotation(ActiveController);
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
            restoring=true;int generation=anchorGeneration;
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
                Board.Width=Mathf.Clamp(saved.width,.6f,2);BeginSavedBoardCheck();
            }
            catch(Exception e){Debug.LogWarning(e);}
            finally{restoring=false;if(pendingStart&&this!=null)ContinueStartRequest();}
        }
        private void BeginSavedBoardCheck()
        {
            restoredPlacement=checkingSavedBoard=true;savedPoseSample=false;Board.Placed=false;
        }
        private static bool SavedBoardIsDistant(Vector3 offset)
            =>new Vector2(offset.x,offset.z).magnitude>SavedBoardDistance||Mathf.Abs(offset.y)>2.5f;
        private void CheckSavedBoard(bool headTracked,float now,string savePath)
        {
            if(!restoredPlacement||Session.Running||Placing||!headTracked||Session.HasSystemPause
                ||(anchor!=null&&!anchor.Localized))
            {savedPoseSample=false;return;}
            var offset=Board.transform.position-Camera.transform.position;
            // Wait for both anchor and head coordinates to settle. Never erase a
            // nearby board based on the rig's temporary startup origin.
            if(!savedPoseSample||Vector3.Distance(offset,savedBoardOffset)>.25f)
            {savedPoseSample=true;savedBoardOffset=offset;savedPoseSince=now;return;}
            if(now-savedPoseSince<SavedPoseSettleTime)return;
            if(SavedBoardIsDistant(offset))
            {
                restoredPlacement=checkingSavedBoard=savedPoseSample=false;anchorGeneration++;
                Board.transform.SetParent(null,true);Board.Placed=false;
                var previous=anchor;var previousRoot=anchorRoot;anchor=null;anchorRoot=null;
                try{if(File.Exists(savePath))File.Delete(savePath);}
                catch(Exception e){Debug.LogWarning("Could not forget saved board placement: "+e.Message);}
                if(previous!=null)EraseForgottenAnchor(previous);
                else if(previousRoot!=null)Destroy(previousRoot.gameObject);
                Session.Announce("You moved away from the saved board. Place it again before starting.");
            }
            else if(checkingSavedBoard)
            {checkingSavedBoard=false;Board.Placed=true;Session.Announce("Previous board placement restored.");}
            if(pendingStart)ContinueStartRequest();
        }
        private async void EraseForgottenAnchor(OVRSpatialAnchor previous)
        {
            try{await previous.EraseAnchorAsync();}
            catch(Exception e){Debug.LogWarning("Could not erase the previous board anchor: "+e.Message);}
            finally{if(previous!=null)Destroy(previous.gameObject);}
        }
        private void TrackingLost(){headPoseUpdated=false;Board.Placed=false;Session.Pause("tracking",true);Session.Announce("Tracking lost. Restore tracking and confirm the board placement.");}
        private void TrackingAcquired(){if(anchor!=null&&anchor.Localized){Board.Placed=true;Session.Pause("tracking",false);}else{Session.Pause("tracking",false);if(Session.IsOnline)EnsureOnlineBoardPlacement();else if(Session.Running)BeginPlacement();}}
        private void Unmounted()=>Session.Pause("headset",true);
        private void Mounted()=>Session.Pause("headset",false);
        private void FocusLost()=>Session.Pause("vrfocus",true);
        private void FocusGained()=>Session.Pause("vrfocus",false);
        private void OnDestroy()
        {
            if(Session!=null){Session.NewMatch-=EnsureOnlineBoardPlacement;Session.MatchClosed-=EnsureOnlineBoardPlacement;}
            if(rig!=null)rig.UpdatedAnchors-=HeadPoseUpdated;
            OVRManager.TrackingLost-=TrackingLost;OVRManager.TrackingAcquired-=TrackingAcquired;OVRManager.HMDUnmounted-=Unmounted;OVRManager.HMDMounted-=Mounted;OVRManager.VrFocusLost-=FocusLost;OVRManager.VrFocusAcquired-=FocusGained;
        }
    }
}
