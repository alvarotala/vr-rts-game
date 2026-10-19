using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class CasterVisualPreview
    {
        private static Catalog LoadCatalog() => new Catalog(JsonUtility.FromJson<CatalogDefinition>(File.ReadAllText("Assets/Frontier/Resources/catalog.json")));

        [MenuItem("Frontier/Validate caster models")]
        public static void Validate()
        {
            var catalog=LoadCatalog();var units=new UnitVisuals();var buildings=new BuildingVisuals();int checks=0;
            try
            {
                foreach(string faction in new[]{"industrial","biological"})
                {
                    var caster=catalog.Role(faction,"caster");var academy=catalog.Role(faction,"academy");
                    var unit=units.Get(caster);var animated=units.Get(caster,false,true);var focus=units.CombatPart(caster,false);
                    var building=buildings.Get(academy);
                    Check(unit==units.Get(caster)&&animated==units.Get(caster,false,true)&&focus==units.CombatPart(caster,false)&&building==buildings.Get(academy),faction+" caches all caster and academy meshes");
                    foreach(var mesh in new[]{unit,animated,focus,building})
                    {
                        Check(mesh.subMeshCount==3&&mesh.vertexCount<12000&&mesh.colors.Length==mesh.vertexCount&&mesh.vertices.All(Finite),faction+" valid bounded mesh: "+mesh.name);
                        Check(mesh.GetTriangles(2).Length>0,faction+" keeps violet role geometry: "+mesh.name);
                    }
                    float scale=caster.collisionRadius/.45f;
                    Check(unit.bounds.max.y>1.4f*scale&&unit.bounds.min.y>=0,faction+" caster retains upright crowned silhouette");
                    Check(animated.vertexCount<unit.vertexCount&&focus.bounds.size.z<.5f*scale,faction+" animated focus is a compact orb, not an artillery barrel");
                    Check(unit.vertexCount!=units.Get(catalog.Role(faction,"siege")).vertexCount,faction+" caster has its own body geometry");
                    Check(building.bounds.min.y>=-.001f&&building.bounds.extents.x<=academy.footprint*.5f&&building.bounds.extents.z<=academy.footprint*.5f,faction+" academy remains inside physical footprint");
                    Check(building.vertexCount!=buildings.Get(catalog.Role(faction,"tech")).vertexCount&&building.vertexCount!=buildings.Get(catalog.Role(faction,"barracks")).vertexCount,faction+" academy is distinct from existing production buildings");
                }
                Check(UnitVisuals.RoleName("caster")=="Caster"&&UnitVisuals.Accent("caster")==BuildingVisuals.Accent("academy"),"Caster and academy share a readable role and violet accent");
                Debug.Log($"FRONTIER_CASTER_VISUALS passed: {checks} checks; cached models, distinct silhouettes, animated focus, role material and building footprints; no player build");
            }
            finally{units.Dispose();buildings.Dispose();}
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("CASTER_VISUAL_PASS: "+message);}
            bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        }

        [MenuItem("Frontier/Preview caster models")]
        public static void Render()
        {
            Validate();var catalog=LoadCatalog();var units=new UnitVisuals();var buildings=new BuildingVisuals();
            var preview=new PreviewRenderUtility();var materials=new List<Material>();
            var oldBounds=Shader.GetGlobalVector("_FrontierBounds");var oldMatrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            Material Make(Color color){var m=new Material(Shader.Find("Frontier/Board"));m.SetColor("_BaseColor",color);materials.Add(m);return m;}
            try
            {
                Shader.SetGlobalVector("_FrontierBounds",new Vector4(100,100,128,128));Shader.SetGlobalMatrix("_FrontierWorldToBoard",Matrix4x4.identity);
                var friendly=Make(new Color(.2f,.8f,1));var enemy=Make(new Color(1,.36f,.23f));var dark=Make(new Color(.12f,.17f,.22f));
                var tissue=Make(Color.white);var violet=Make(UnitVisuals.Accent("caster"));
                preview.camera.orthographic=true;preview.camera.orthographicSize=4.4f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                Directory.CreateDirectory("TestResults/Casters");
                for(int view=0;view<3;view++)
                {
                    preview.camera.transform.position=view==2?new Vector3(0,15,-.01f):new Vector3(0,9,-13);
                    preview.camera.transform.LookAt(new Vector3(0,.6f,0));preview.BeginStaticPreview(new Rect(0,0,1500,1100));
                    for(int faction=0;faction<2;faction++)for(int role=0;role<2;role++)
                    {
                        var type=catalog.Role(faction==0?"industrial":"biological",role==0?"caster":"academy");
                        var mesh=role==0?units.Get(type):buildings.Get(type);
                        var transform=Matrix4x4.TRS(new Vector3(role==0?-2:1.5f,0,(faction-.5f)*4.2f),Quaternion.Euler(0,180,0),Vector3.one);
                        preview.DrawMesh(mesh,transform,view==1?enemy:friendly,0);preview.DrawMesh(mesh,transform,faction==1?tissue:dark,1);preview.DrawMesh(mesh,transform,violet,2);
                    }
                    preview.Render(true);var image=preview.EndStaticPreview();string name=view==0?"friendly":view==1?"enemy":"top";
                    File.WriteAllBytes("TestResults/Casters/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                Debug.Log("FRONTIER_CASTER_PREVIEW rendered: 3 views, 2 caster models and 2 academy models; no player build");
            }
            finally
            {
                preview.Cleanup();units.Dispose();buildings.Dispose();foreach(var material in materials)Object.DestroyImmediate(material);
                Shader.SetGlobalVector("_FrontierBounds",oldBounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",oldMatrix);
            }
        }
    }
}
