using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Editor
{
    public static class ControllerAssets
    {
        public static void Prepare()
        {
            const string folder="Assets/Resources/Controllers";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var helper=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRControllerPrefab.prefab").GetComponent<OVRControllerHelper>();
            foreach(bool left in new[]{false,true})
            {
                string name="TouchPlus"+(left?"Left":"Right");
                var source=left?helper.m_modelMetaTouchPlusLeftController:helper.m_modelMetaTouchPlusRightController;
                var model=Object.Instantiate(source);model.name=name;model.SetActive(true);
                model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                // The SDK model carries its centimeters-to-meters scale on this root.
                model.transform.localScale=source.transform.localScale;
                try
                {
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    var renderer=model.GetComponentInChildren<Renderer>(true);
                    material.SetTexture("_BaseMap",renderer.sharedMaterial.mainTexture);material.SetFloat("_Smoothness",.3f);
                    string materialPath=folder+"/"+name+".mat";
                    var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(existing!=null){EditorUtility.CopySerialized(material,existing);Object.DestroyImmediate(material);material=existing;}
                    else AssetDatabase.CreateAsset(material,materialPath);
                    foreach(var part in model.GetComponentsInChildren<Renderer>(true))
                    {part.sharedMaterial=material;part.shadowCastingMode=ShadowCastingMode.Off;part.receiveShadows=false;}
                    foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                    PrefabUtility.SaveAsPrefabAsset(model,folder+"/"+name+".prefab");
                }
                finally{Object.DestroyImmediate(model);}
            }
            AssetDatabase.SaveAssets();Debug.Log("FRONTIER_CONTROLLER_ASSETS prepared: SDK Touch Plus left/right, URP materials, no colliders");
        }
    }
}
