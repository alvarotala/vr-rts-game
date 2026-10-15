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
            useScene=true;if(!Active||mruk==null){Session.Announce("En escritorio se usa un plano manual.");return;}
            try
            {
                const string permission="com.oculus.permission.USE_SCENE";
                if(!Permission.HasUserAuthorizedPermission(permission))
                {
                    var callbacks=new PermissionCallbacks();callbacks.PermissionGranted+=_=>LoadRoom();callbacks.PermissionDenied+=_=>Session.Announce("Sin acceso a la habitación. Podés usar el plano manual.");
                    Permission.RequestUserPermission(permission,callbacks);return;
                }
                await mruk.LoadSceneFromDevice(false);Session.Announce(mruk.GetCurrentRoom()!=null?"Superficies disponibles. Ubicá el tablero.":"No hay habitación registrada. Usá Mesa o Piso.");
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("No se pudo leer la habitación. Usá colocación manual.");}
        }
        public void BeginPlacement()
        {
            if(busy){Session.Announce("Esperá a que termine de guardarse el ancla.");return;}
            Placing=true;Board.Placed=false;Session.Pause("placement",true);Board.transform.SetParent(null,true);
            Session.Announce("Apuntá y confirmá con gatillo. Stick izq.: altura. Der.: tamaño y giro.");
        }
        public void PlaceRay(Ray ray,Vector2 left,Vector2 right,bool confirm)
        {
            if(!Placing)return;
            manualHeight=Mathf.Clamp(manualHeight+left.y*Time.unscaledDeltaTime*.3f,0,1.5f);
            Board.Width=Mathf.Clamp(Board.Width+right.y*Time.unscaledDeltaTime*.5f,.6f,2);
            Board.transform.Rotate(Vector3.up,right.x*Time.unscaledDeltaTime*45,Space.World);
            bool hit=false;Vector3 point=default;
            var room=mruk==null?null:mruk.GetCurrentRoom();
            if(useScene&&room!=null&&room.Raycast(ray,5,new LabelFilter(MRUKAnchor.SceneLabels.TABLE|MRUKAnchor.SceneLabels.FLOOR),out var surface,out _)&&Vector3.Dot(surface.normal,Vector3.up)>.9f){point=surface.point;hit=true;}
            if(!hit){var plane=new Plane(Vector3.up,new Vector3(0,manualHeight,0));if(plane.Raycast(ray,out float d)&&d<5){point=ray.GetPoint(d);hit=true;}}
            if(!hit)return;
            Board.transform.position=point+Vector3.up*.005f;
            if(confirm){Placing=false;Board.Placed=true;Session.Pause("placement",false);Session.Pause("tracking",false);SaveAnchor();Session.Announce("Tablero colocado. Podés comenzar o continuar.");}
        }
        public Ray PointerRay(bool left)
        {
            var controller=left?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
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
                if(!await anchor.WhenCreatedAsync()||!await anchor.WhenLocalizedAsync())throw new InvalidOperationException("No se localizó el ancla.");
                if(generation!=anchorGeneration)return;
                // Keep session placement world-locked even if persistence is unavailable.
                Board.transform.SetParent(anchorRoot,true);
                var result=await anchor.SaveAnchorAsync();if(!result.Success)throw new InvalidOperationException("No se pudo guardar el ancla.");
                var placement=new BoardPlacement{anchorUuid=anchor.Uuid.ToString(),width=Board.Width};
                File.WriteAllText(SavePath,JsonUtility.ToJson(placement,true));
                if(previous!=null){await previous.EraseAnchorAsync();Destroy(previous.gameObject);}
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Tablero colocado para esta sesión; el ancla no se guardó.");}
            finally{busy=false;}
        }
        private async void Restore()
        {
            if(!File.Exists(SavePath))return;
            int generation=anchorGeneration;
            try
            {
                var saved=JsonUtility.FromJson<BoardPlacement>(File.ReadAllText(SavePath));
                if(saved.schemaVersion!=1||!Guid.TryParse(saved.anchorUuid,out var uuid))throw new InvalidDataException("Ancla guardada inválida.");
                var anchors=new List<OVRSpatialAnchor.UnboundAnchor>();var result=await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[]{uuid},anchors);
                if(!result.Success||anchors.Count==0)throw new InvalidOperationException("No se encontró el ancla.");
                var unbound=anchors[0];if(!unbound.Localized&&!await unbound.LocalizeAsync(10))throw new InvalidOperationException("No se localizó el ancla.");
                if(generation!=anchorGeneration||Placing)return;
                anchorRoot=new GameObject("Restored board anchor").transform;anchor=anchorRoot.gameObject.AddComponent<OVRSpatialAnchor>();unbound.BindTo(anchor);
                Board.transform.SetParent(anchorRoot,false);Board.transform.localPosition=Vector3.zero;Board.transform.localRotation=Quaternion.identity;
                Board.Width=Mathf.Clamp(saved.width,.6f,2);Board.Placed=true;Session.Announce("Ubicación anterior recuperada.");
            }
            catch(Exception e){Debug.LogWarning(e);Session.Announce("Volvé a colocar el tablero en tu mesa o piso.");}
        }
        private void TrackingLost(){Board.Placed=false;Session.Pause("tracking",true);Session.Announce("Seguimiento perdido. Recuperá el espacio y confirmá la ubicación.");}
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
