using UnityEngine;
using UnityEngine.Rendering;
#if FRONTIER_BROWSER
using OVRInput = Frontier.Runtime.BrowserTouchInput;
#endif

namespace Frontier.Runtime
{
    // Reuses Meta's Touch Plus meshes and button animations; no input or colliders.
    public sealed class ControllerVisuals : System.IDisposable
    {
        public const float EmitterDistance=.035f;
        private static readonly Pose?[] stickSurfaces=new Pose?[2];
        // The prefab supplies the neutral stick pose, so tilting its animated cap
        // while navigating never tilts the labels or the open menu.
        public static Pose StickSurface(bool left)
        {
            int hand=left?1:0;
            if(stickSurfaces[hand].HasValue)return stickSurfaces[hand].Value;
            var surface=new Pose(new Vector3(0,.008f,0),Quaternion.Euler(90,0,0));
            var model=Resources.Load<GameObject>("Controllers/TouchPlus"+(left?"Left":"Right"));
            if(model!=null)foreach(var part in model.GetComponentsInChildren<Transform>(true))
            {
                if(!part.name.EndsWith("_b_thumbstick"))continue;
                var normal=model.transform.InverseTransformDirection(part.up).normalized;
                var position=Vector3.Scale(model.transform.InverseTransformPoint(part.position),model.transform.localScale);
                // Lift from the pivot to the cap; keep text's up direction toward
                // the front of the controller, independent of the bone's mesh yaw.
                surface=new Pose(position+normal*.008f,Quaternion.LookRotation(-normal,Vector3.ProjectOnPlane(Vector3.forward,normal)));
                break;
            }
            stickSurfaces[hand]=surface;return surface;
        }
        public static Vector3 ButtonPosition(bool left,string input)
        {
            string suffix=input=="touch:trigger"?"_b_trigger_front":input=="touch:grip"?"_b_trigger_grip":
                input=="touch:a"?"b_button_"+(left?"x":"a"):input=="touch:b"?"b_button_"+(left?"y":"b"):"_b_thumbstick";
            var model=Resources.Load<GameObject>("Controllers/TouchPlus"+(left?"Left":"Right"));
            if(model!=null)foreach(var part in model.GetComponentsInChildren<Transform>(true))
                if(part.name.EndsWith(suffix))
                    return Vector3.Scale(model.transform.InverseTransformPoint(part.position),model.transform.localScale);
            return StickSurface(left).position;
        }
        private readonly Transform root;
        private readonly Material emitterMaterial;
        private readonly GameObject[] models=new GameObject[2];
        private readonly Animator[] animators=new Animator[2];
        public Transform Root=>root;
        public Vector3 EmitterPosition=>root.TransformPoint(0,0,EmitterDistance);
        public ControllerVisuals(Transform parent)
        {
            root=new GameObject("Commander Touch controller").transform;root.SetParent(parent,false);
            for(int i=0;i<2;i++)
            {
                var prefab=Resources.Load<GameObject>("Controllers/TouchPlus"+(i==0?"Right":"Left"));
                if(prefab==null)continue;
                models[i]=Object.Instantiate(prefab,root);models[i].SetActive(false);
                animators[i]=models[i].GetComponentInChildren<Animator>(true);
            }
            var emitter=GameObject.CreatePrimitive(PrimitiveType.Sphere);emitter.name="Pointer emitter";
            emitter.transform.SetParent(root,false);emitter.transform.localPosition=new Vector3(0,0,EmitterDistance);
            emitter.transform.localScale=Vector3.one*.006f;
            var collider=emitter.GetComponent<Collider>();collider.enabled=false;Release(collider);
            emitterMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var renderer=emitter.GetComponent<Renderer>();renderer.sharedMaterial=emitterMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
            root.gameObject.SetActive(false);
        }
        public void Update(QuestBridge quest,Color color)
        {
            bool visible=quest!=null&&quest.Active&&quest.PointerTracked&&quest.TrackingSpace!=null&&!quest.Session.HasSystemPause;
            root.gameObject.SetActive(visible);if(!visible)return;
            var ray=quest.PointerRay();root.SetPositionAndRotation(ray.origin,quest.PointerRotation());
            emitterMaterial.color=color;
            int hand=quest.ActiveController==OVRInput.Controller.LTouch?1:0;
            for(int i=0;i<2;i++)if(models[i]!=null)models[i].SetActive(i==hand);
            var animator=animators[hand];if(animator==null)return;
            var controller=quest.ActiveController;var stick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,controller);
            animator.SetFloat("Button 1",OVRInput.Get(OVRInput.Button.One,controller)?1:0);
            animator.SetFloat("Button 2",OVRInput.Get(OVRInput.Button.Two,controller)?1:0);
            animator.SetFloat("Button 3",OVRInput.Get(OVRInput.Button.Start,controller)?1:0);
            animator.SetFloat("Joy X",stick.x);animator.SetFloat("Joy Y",stick.y);
            animator.SetFloat("Trigger",OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,controller));
            animator.SetFloat("Grip",OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger,controller));
        }
        public void Hide(){if(root!=null)root.gameObject.SetActive(false);}
        public void Dispose(){if(root!=null)Release(root.gameObject);Release(emitterMaterial);}
        private static void Release(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
