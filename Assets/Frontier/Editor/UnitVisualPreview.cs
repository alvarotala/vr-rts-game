using System.IO;
using System.Collections.Generic;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    // A quick visual review of the actual runtime meshes, without building a player or APK.
    public static class UnitVisualPreview
    {
        [MenuItem("Frontier/Preview unit roles")]
        public static void Render()
        {
            var catalog=new Catalog(JsonUtility.FromJson<CatalogDefinition>(File.ReadAllText("Assets/Frontier/Resources/catalog.json")));
            var models=new UnitVisuals();var preview=new PreviewRenderUtility();var materials=new List<Material>();
            var oldBounds=Shader.GetGlobalVector("_FrontierBounds");var oldMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            Material ColorMaterial(Color color)
            {var m=new Material(Shader.Find("Frontier/Board"));m.SetColor("_BaseColor",color);materials.Add(m);return m;}
            try
            {
                Shader.SetGlobalVector("_FrontierBounds",new Vector4(100,100,128,128));Shader.SetGlobalMatrix("_FrontierWorldToBoard",Matrix4x4.identity);
                var friendly=ColorMaterial(new Color(.2f,.8f,1));var enemy=ColorMaterial(new Color(1,.36f,.23f));
                var dark=ColorMaterial(new Color(.12f,.17f,.22f));
                var tissue=ColorMaterial(Color.white);
                string[] roles={"worker","scout","melee","ranged","siege","caster"};
                var accents=new Material[roles.Length];for(int i=0;i<roles.Length;i++)accents[i]=ColorMaterial(UnitVisuals.Accent(roles[i]));
                preview.camera.orthographic=true;preview.camera.orthographicSize=3.7f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                Directory.CreateDirectory("TestResults/UnitRoles");
                for(int view=0;view<3;view++)
                {
                    preview.camera.transform.position=view==2?new Vector3(0,15,-.01f):new Vector3(0,8,-12);
                    preview.camera.transform.LookAt(new Vector3(0,.5f,0));
                    preview.BeginStaticPreview(new Rect(0,0,1600,800));
                    int vertices=0;
                    for(int faction=0;faction<2;faction++)for(int role=0;role<roles.Length;role++)
                    {
                        var type=catalog.Role(faction==0?"industrial":"biological",roles[role]);var mesh=models.Get(type);vertices+=mesh.vertexCount;
                        var matrix=Matrix4x4.TRS(new Vector3((role-(roles.Length-1)*.5f)*2.2f,0,(faction-.5f)*3),Quaternion.Euler(0,210,0),Vector3.one);
                        preview.DrawMesh(mesh,matrix,view==1?enemy:friendly,0);
                        preview.DrawMesh(mesh,matrix,faction==1?tissue:dark,1);preview.DrawMesh(mesh,matrix,accents[role],2);
                    }
                    preview.Render(true);
                    var image=preview.EndStaticPreview();string name=view==0?"friendly":view==1?"enemy":"top";
                    File.WriteAllBytes("TestResults/UnitRoles/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                    Debug.Log($"FRONTIER_UNIT_PREVIEW {name}: {roles.Length*2} models, {vertices} vertices, 3 submeshes per model");
                }
            }
            finally
            {
                preview.Cleanup();models.Dispose();foreach(var material in materials)Object.DestroyImmediate(material);
                Shader.SetGlobalVector("_FrontierBounds",oldBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",oldMatrix);
            }
        }
    }
}
