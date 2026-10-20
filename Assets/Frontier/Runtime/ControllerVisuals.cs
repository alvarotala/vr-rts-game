using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Reuses Meta's Touch Plus meshes and button animations; no input or colliders.
    public sealed class ControllerVisuals : System.IDisposable
    {
        public const float EmitterDistance=.035f;
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
