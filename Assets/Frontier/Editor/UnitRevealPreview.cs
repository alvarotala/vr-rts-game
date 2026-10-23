using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class UnitRevealPreview
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private|BindingFlags.Public).GetValue(value);
        private static void Set(object value,string name,object field)=>value.GetType().GetField(name,Private|BindingFlags.Public).SetValue(value,field);
        private static void Call(object value,string method)=>value.GetType().GetMethod(method,Private).Invoke(value,null);

        [MenuItem("Frontier/Validate unit vision fade")]
        public static void Run()
        {
            const string output="TestResults/UnitReveal";Directory.CreateDirectory(output);
            var preview=new PreviewRenderUtility();var fixture=new GameObject("Unit reveal fixture");var boardObject=new GameObject("Unit reveal board");
            bool hadFog=PlayerPrefs.HasKey(BoardView.FogDensityKey),hadLight=PlayerPrefs.HasKey(BoardView.LightingKey);
            float savedFog=PlayerPrefs.GetFloat(BoardView.FogDensityKey,1);int savedLight=PlayerPrefs.GetInt(BoardView.LightingKey);
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            float lighting=Shader.GetGlobalFloat("_FrontierEnhancedLighting");var lightColor=Shader.GetGlobalColor("_FrontierLightColor");
            int checks=0,captures=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");session.LoadMap("training");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;board.Viewer=preview.camera;Call(board,"Start");preview.AddSingleGO(boardObject);
                var visuals=Field<Dictionary<int,Transform>>(board,"entities");var parts=Field<IDictionary>(board,"parts");
                var block=new MaterialPropertyBlock();
                foreach(string faction in new[]{"industrial","biological"})foreach(bool enhanced in new[]{false,true})
                {
                    session.StartMatch();board.SetEnhancedLighting(enhanced);board.SetFogDensity(0);
                    session.View.entities=Array.Empty<VisibleEntity>();session.View.resources=Array.Empty<ResourceState>();
                    session.View.attacks=Array.Empty<AttackEvent>();session.View.deaths=Array.Empty<DeathEvent>();session.View.abilityEvents=Array.Empty<AbilityEvent>();
                    Array.Fill(session.View.visibility,(byte)2);
                    var actors=new[]{"worker","scout","melee","ranged","siege","caster"}.Select((role,i)=>
                    {
                        var type=session.Catalog.Role(faction,role);return new VisibleEntity{id=1000+i,owner=1,definition=type.id,health=type.health*.8f,
                            complete=true,position=new Point(20+i%3*4,21+i/3*5),damageTick=-100};
                    }).ToArray();
                    string name=faction+(enhanced?"-enhanced":"-current");
                    Refresh();CameraAt();var empty=Capture(null);
                    session.View.entities=actors;Refresh();session.Pause("reveal-preview",true);
                    Check(actors.All(e=>Opacity(e)==0),name+": enemy units start fully transparent on their first visible frame");
                    var first=Capture(name+"-000");Check(Difference(empty,first)<.00001,name+": zero-opacity units and health bars do not pop into the image");
                    Check(actors.All(e=>Bodies(e).All(r=>r.sharedMaterials.All(m=>m.renderQueue==(int)RenderQueue.Transparent&&m.GetFloat("_ZWrite")==0))),
                        "Only revealing units use shared transparent materials without depth writes");
                    var startingMaterials=Bodies(actors[0])[0].sharedMaterials;
                    int materialCount=Field<IDictionary>(board,"revealMaterials").Count;
                    string snapshot=JsonUtility.ToJson(session.Match.Snapshot());
                    Ages(.075f);Refresh();var quarter=Capture(name+"-075");
                    Check(actors.All(e=>Mathf.Abs(Opacity(e)-.15625f)<.001),"Smooth fade has a gentle start");
                    Ages(.15f);Refresh();var half=Capture(name+"-150");
                    Check(actors.All(e=>Mathf.Abs(Opacity(e)-.5f)<.001),"Halfway through 0.3 seconds all model parts share 50 percent opacity");
                    foreach(var actor in actors)
                    {
                        foreach(var renderer in Bodies(actor))Check(Mathf.Abs(Alpha(renderer)-.5f)<.001,"Body, weapon and legs share reveal opacity");
                        Check(Mathf.Abs(Alpha(visuals[actor.id].Find("Health").GetComponent<Renderer>())-.5f)<.001,"Health bar fades with its unit");
                    }
                    Refresh();Check(actors.All(e=>Mathf.Abs(Opacity(e)-.5f)<.001),"Local pause holds the reveal instead of making units blink");
                    board.Hovered=actors[0].id;actors[0].damageTick=session.View.tick;Refresh();
                    Bodies(actors[0])[0].GetPropertyBlock(block);
                    Check(block.GetFloat("_HoverAmount")==1&&block.GetFloat("_DamageAmount")>0&&Mathf.Abs(block.GetFloat("_RevealOpacity")-.5f)<.001,
                        "Hover and damage do not replace the fade property block");
                    Check(Mathf.Abs(Alpha(visuals[actors[0].id].Find("Damage impact").GetComponent<Renderer>())-.5f)<.001,"Hit marker shares unit opacity");
                    var resource=session.Match.View(0).resources.First();session.View.resources=new[]{resource};Refresh();
                    var resourceRenderer=Field<Dictionary<int,Renderer>>(board,"resourceRenderers")[resource.id];
                    Check(Alpha(resourceRenderer)==1,"Resource highlights cannot inherit the last unit's opacity");
                    session.View.resources=Array.Empty<ResourceState>();resourceRenderer.gameObject.SetActive(false);
                    board.Hovered=0;actors[0].damageTick=-100;
                    Ages(BoardView.UnitRevealSeconds);Refresh();var full=Capture(name+"-300");
                    Check(actors.All(e=>Opacity(e)==1&&Bodies(e).All(r=>r.sharedMaterials.All(m=>m.renderQueue<(int)RenderQueue.Transparent&&m.GetFloat("_ZWrite")==1))),
                        "Finished reveals restore the original opaque materials and depth writes");
                    Check(Difference(empty,quarter)>0&&Difference(empty,half)>Difference(empty,quarter)&&Difference(empty,full)>Difference(empty,half),
                        name+": rendered silhouettes emerge progressively without dithering");
                    Check(Enumerable.Range(0,empty.Length).Where(i=>empty[i].a==255).All(i=>half[i].a>=254),"Fade preserves opaque board coverage for passthrough");
                    Check(Field<IDictionary>(board,"revealMaterials").Count==materialCount,"Fade progression never allocates per-frame or per-unit materials");
                    Check(JsonUtility.ToJson(session.Match.Snapshot())==snapshot,"Reveal changes no authoritative state");

                    session.Pause("reveal-preview",false);
                    var old=visuals[actors[0].id];session.View.entities=Array.Empty<VisibleEntity>();Refresh();
                    Check(visuals.Count==0&&!old.gameObject.activeSelf,"Units leave vision immediately, with no fading hidden silhouettes");
                    session.View.entities=new[]{actors[0]};Refresh();
                    Check(visuals[actors[0].id]==old&&Opacity(actors[0])==0&&Bodies(actors[0])[0].sharedMaterials.SequenceEqual(startingMaterials),
                        "Re-entry resets the fade and reuses the pooled object and materials");
                    session.View.entities=Array.Empty<VisibleEntity>();Refresh();
                    var replacement=new VisibleEntity{id=2000,owner=1,definition=actors[0].definition,health=actors[0].health,complete=true,position=actors[0].position,damageTick=-100};
                    session.View.entities=new[]{replacement};Refresh();Check(visuals[replacement.id]==old&&Opacity(replacement)==0,"A different pooled unit gets its own fresh reveal");
                    session.Pause("reveal-preview",true);Ages(.15f);Refresh();
                    var held=Capture(null);board.SetFogDensity(0);Refresh();Check(Difference(held,Capture(null))<.00001,"Fog density changes do not reset unit reveals");
                    session.View.entities=Array.Empty<VisibleEntity>();Refresh();session.View.entities=new[]{replacement};Refresh();
                    Check(Opacity(replacement)==1,"Initial presentation while paused is immediately readable");
                    session.Pause("reveal-preview",false);session.View.entities=Array.Empty<VisibleEntity>();Refresh();replacement.owner=0;
                    session.View.entities=new[]{replacement};Refresh();Check(Opacity(replacement)==1,"Own units remain immediately visible");
                    session.LeaveMatch();Check(visuals.Count==0,"Leaving clears all reveal state with its pooled entities");

                    void Ages(float seconds){foreach(Transform visual in visuals.Values)Set(parts[visual],"revealAge",seconds);}
                    float Opacity(VisibleEntity entity)=>Field<float>(parts[visuals[entity.id]],"revealOpacity");
                    Renderer[] Bodies(VisibleEntity entity)=>Field<Renderer[]>(parts[visuals[entity.id]],"body");
                }
                foreach(string shader in new[]{"Frontier/Board","Frontier/Impact"})Check(!ShaderUtil.ShaderHasError(Shader.Find(shader)),shader+": compiles without shader errors");
                Debug.Log($"FRONTIER_UNIT_REVEAL passed: {checks} checks, {captures} renders; both factions, all six roles, both lighting modes; no physical Quest validation");
                float Alpha(Renderer renderer){renderer.GetPropertyBlock(block);return block.GetFloat("_RevealOpacity");}
                void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
                void Refresh()=>Call(board,"LateUpdate");
                void CameraAt()
                {
                    var center=board.Content.TransformPoint(new Vector3(24,0,24));
                    preview.camera.orthographic=true;preview.camera.orthographicSize=.17f;preview.camera.aspect=1.4f;
                    preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=Color.clear;
                    preview.camera.transform.position=center+new Vector3(.03f,.36f,-.42f);preview.camera.transform.LookAt(center);
                }
                Color32[] Capture(string name)
                {
                    preview.BeginPreview(new Rect(0,0,1120,800),GUIStyle.none);
                    RenderTexture source;
                    try{preview.Render(true);}finally{source=(RenderTexture)preview.EndPreview();}
                    var texture=RenderTexture.GetTemporary(1120,800,0,RenderTextureFormat.ARGB32);
                    var previous=RenderTexture.active;var image=new Texture2D(1120,800,TextureFormat.RGBA32,false,true);
                    try
                    {
                        Graphics.Blit(source,texture);RenderTexture.active=texture;
                        image.ReadPixels(new Rect(0,0,1120,800),0,0);image.Apply();
                        if(name!=null){File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());captures++;}
                        return image.GetPixels32();
                    }
                    finally{RenderTexture.active=previous;Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(texture);}
                }
                double Difference(Color32[] a,Color32[] b)=>a.Select((p,i)=>(Math.Abs(p.r-b[i].r)+Math.Abs(p.g-b[i].g)+Math.Abs(p.b-b[i].b))/765.0).Average();
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(fixture);preview.Cleanup();
                if(hadFog)PlayerPrefs.SetFloat(BoardView.FogDensityKey,savedFog);else PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                if(hadLight)PlayerPrefs.SetInt(BoardView.LightingKey,savedLight);else PlayerPrefs.DeleteKey(BoardView.LightingKey);PlayerPrefs.Save();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
                Shader.SetGlobalFloat("_FrontierEnhancedLighting",lighting);Shader.SetGlobalColor("_FrontierLightColor",lightColor);
            }
        }
    }
}
