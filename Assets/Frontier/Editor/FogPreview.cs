using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class FogPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Preview and check fog relief")]
        public static void Run()
        {
            const string output = "TestResults/Fog"; const int size = 1000;
            Directory.CreateDirectory(output);
            var fixture = new GameObject("Fog preview session"); var boardObject = new GameObject("Fog preview board");
            var preview = new PreviewRenderUtility(); int checks = 0, captures = 0;
            var previousBounds = Shader.GetGlobalVector("_FrontierBounds"); var previousMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            bool hadDensity = PlayerPrefs.HasKey(BoardView.FogDensityKey);
            float previousDensity = PlayerPrefs.GetFloat(BoardView.FogDensityKey, 1);
            float previousShaderDensity = Shader.GetGlobalFloat("_FrontierFogDensity");
            bool hadLighting = PlayerPrefs.HasKey(BoardView.LightingKey); int previousLighting = PlayerPrefs.GetInt(BoardView.LightingKey);
            float previousLight = Shader.GetGlobalFloat("_FrontierEnhancedLighting"); var previousLightColor = Shader.GetGlobalColor("_FrontierLightColor");
            try
            {
                PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                PlayerPrefs.DeleteKey(BoardView.LightingKey);
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("training");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Call(board, "Start");
                Check(board.FogDensity == 1, "Fresh preferences use full-density mist");
                preview.AddSingleGO(boardObject);
                var shader = Shader.Find("Frontier/Fog");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Fog shader compiles with the URP stereo variants");
                var fog = Field<FogVisuals>(board, "fogVisuals");
                fog.Update(true, 0);
                Check(!fog.Root.gameObject.activeSelf, "A map preview before a match has no fog sheets");

                foreach (var asset in Resources.LoadAll<TextAsset>("Maps").OrderBy(a => a.name))
                {
                    session.LoadMap(asset.name); session.StartMatch();
                    var hiddenEnemy = session.Match.Entities.Values.First(e => e.owner == 1);
                    session.View.entities = Array.Empty<VisibleEntity>(); session.View.resources = Array.Empty<ResourceState>();
                    session.View.attacks = Array.Empty<AttackEvent>(); session.View.deaths = Array.Empty<DeathEvent>(); session.View.abilityEvents = Array.Empty<AbilityEvent>();
                    SetBands(); Refresh(); fog = Field<FogVisuals>(board, "fogVisuals");
                    var root = fog.Root; var mesh = root.GetComponent<MeshFilter>().sharedMesh; var material = root.GetComponent<MeshRenderer>().sharedMaterial;
                    Check(root.gameObject.activeInHierarchy && root.parent == board.Content, asset.name + ": active fog follows map coordinates");
                    Check(root.GetComponentsInChildren<Renderer>().Length == 1 && mesh.subMeshCount == 1 && mesh.vertexCount <= FogVisuals.MaxVertices,
                        asset.name + ": all three sheets share one bounded mesh and material");
                    Check(root.GetComponentsInChildren<Collider>().Length == 0 && root.GetComponentsInChildren<ParticleSystem>().Length == 0,
                        asset.name + ": fog adds no picking colliders or per-cell particles");
                    Check(mesh.uv.Select(p => p.x).Distinct().Count() == FogVisuals.LayerCount, asset.name + ": fog contains three separated depth layers");
                    var lower = mesh.vertices.Take(mesh.vertexCount / FogVisuals.LayerCount).ToArray();
                    Check(lower.All(p => p.y > board.HeightAt(new Point(Mathf.Clamp(p.x, .001f, session.Map.width - .001f), Mathf.Clamp(p.z, .001f, session.Map.height - .001f))) + .2f),
                        asset.name + ": the lowest sheet remains above the terrain and ramp samples");
                    if (session.Map.terrain.Any(t => t.elevation > 0))
                        Check(lower.Max(p => p.y) - lower.Min(p => p.y) > .75f, asset.name + ": mist follows raised terrain rather than forming a flat map-wide plane");
                    var tint = material.GetColor("_FogColor");
                    Check(session.Map.theme == "desert" ? tint.r > tint.b : session.Map.theme == "forest" ? tint.g > tint.r && tint.g > tint.b : tint.b > tint.r, asset.name + ": fog tint matches the battlefield theme");
                    var visibility = Field<Texture2D>(board, "fog");
                    Check(material.GetTexture("_FogTex") == visibility && visibility.filterMode == FilterMode.Point, "Fog uses the existing filtered player visibility texture");
                    Check(visibility.GetPixel(0, 0).r == 0 && Mathf.Abs(visibility.GetPixel(session.Map.width / 2, 0).r - 75 / 255f) < .001f
                        && visibility.GetPixel(session.Map.width - 1, 0).r == 1, "Unseen, remembered and visible cells retain distinct 0 / 75 / 255 values");
                    Check(Field<System.Collections.IDictionary>(board, "entities").Count == 0 && !session.View.entities.Any(e => e.id == hiddenEnemy.id),
                        "Fog presentation does not create an enemy omitted from PlayerView");
                    var point = new Point(session.Map.width * .84f, session.Map.height * .7f);
                    var ground = board.Content.TransformPoint(new Vector3(point.x, board.HeightAt(point), point.y));
                    board.ResolvePointerTarget(new Ray(ground + Vector3.up, Vector3.down), point, ground, out int target, out int resource);
                    Check(target == 0 && resource == 0, "Fog cannot become a unit or resource selection target");

                    CameraAt(false); fog.Update(true, 12); Snap(asset.name + "-relief");
                    CameraAt(true); fog.Update(true, 12); Snap(asset.name + "-overview");
                    var originalVertices = mesh.vertices; int originalObjects = root.GetComponentsInChildren<Transform>(true).Length;
                    fog.Update(true, 40);
                    Check(root.GetComponent<MeshFilter>().sharedMesh == mesh && root.GetComponent<MeshRenderer>().sharedMaterial == material
                        && originalVertices.SequenceEqual(mesh.vertices) && root.GetComponentsInChildren<Transform>(true).Length == originalObjects,
                        "Animation updates only the shader clock, without rebuilding meshes or adding objects");
                    Check(material.GetFloat("_FogTime") == 40, "The fog animation clock accepts a reproducible simulation time");

                    CheckMistDensity();
                    if (asset.name == "training") CheckPixels();
                    CheckTerrainReadability();
                    CheckBoardOpacity();
                    board.SetVisible(false); fog.Update(true, 60);
                    Check(!fog.Root.gameObject.activeSelf, "Hidden boards suppress the fog even if an update requests visibility");
                    board.SetVisible(true); Refresh();
                    session.Pause("fog-preview", true); Refresh(); float pausedTime = material.GetFloat("_FogTime");
                    session.Match.Step(); Refresh();
                    Check(material.GetFloat("_FogTime") == pausedTime, "Paused simulation does not advance the fog clock");
                    session.Pause("fog-preview", false);
                    board.Rebuild(); Refresh();
                    Check(root == null && mesh == null && material == null && board.Content.Cast<Transform>().Count(t => t.name == "Fog of war") == 1,
                        "Rebuild releases the old fog object, mesh and material before creating one replacement");
                    fog = Field<FogVisuals>(board, "fogVisuals"); root = fog.Root; mesh = root.GetComponent<MeshFilter>().sharedMesh;
                    session.StartMatch(); Refresh();
                    Check(root == null && mesh == null && Field<FogVisuals>(board, "fogVisuals").Root != null, "Rematch releases old fog geometry and visibility bindings");
                    session.LeaveMatch(); board.SetVisible(true); Refresh();
                    Check(!Field<FogVisuals>(board, "fogVisuals").Root.gameObject.activeSelf, "Leaving a match removes fog from the next setup preview");

                    void CheckTerrainReadability()
                    {
                        // Isolate terrain shading from mist, then render both together.
                        // Check the whole density range with both lighting modes.
                        CameraAt(false);preview.camera.backgroundColor=Color.clear;
                        bool savedLighting=board.EnhancedLighting;
                        foreach(bool enhanced in new[]{false,true})
                        {
                            board.SetEnhancedLighting(enhanced);board.SetFogDensity(0);
                            string name=asset.name+(enhanced?"-enhanced":"");
                            Array.Fill(session.View.visibility,(byte)2);Refresh();
                            var clear=SnapRgba(name+"-terrain-visible");
                            var solid=Enumerable.Range(0,clear.Length).Where(i=>i%7==0&&clear[i].a==255&&Luma(clear[i])>.025).ToArray();
                            double bright=solid.Average(i=>Luma(clear[i])),lastDark=0,lastRemembered=0;
                            Color32[] noMist=null;
                            foreach(float density in new[]{0,.25f,.5f,.75f,1})
                            {
                                board.SetFogDensity(density);
                                Check(Shader.GetGlobalFloat("_FrontierFogDensity")==density,name+": density immediately updates the terrain and scenery shader");
                                Array.Fill(session.View.visibility,(byte)0);Refresh();fog.Hide();
                                var unseen=SnapRgba(density==0?name+"-terrain-unseen-fog-off":null);
                                Array.Fill(session.View.visibility,(byte)1);Refresh();fog.Hide();
                                var explored=SnapRgba(density==0?name+"-terrain-explored-fog-off":null);
                                double dark=solid.Average(i=>Luma(unseen[i])),remembered=solid.Average(i=>Luma(explored[i]));
                                Check(dark<bright*.995&&remembered>dark&&remembered<bright*.999,name+": unseen and explored terrain remain distinct and darkened at density "+density);
                                if(density==0)
                                {
                                    noMist=unseen;
                                    Check(dark/bright>.55&&dark/bright<.9&&remembered>dark*1.06&&remembered<bright*.97,
                                        name+": mist off restores the original readable darkening; ratio="+(dark/bright).ToString("0.000"));
                                    double covariance=0,clearVariance=0,darkVariance=0;
                                    foreach(int i in solid)
                                    {
                                        double a=Luma(clear[i])-bright,b=Luma(unseen[i])-dark;
                                        covariance+=a*b;clearVariance+=a*a;darkVariance+=b*b;
                                    }
                                    Check(covariance/Math.Sqrt(clearVariance*darkVariance)>.97,name+": darkened terrain preserves relief and detail contrast");
                                }
                                else Check(dark>lastDark&&remembered>lastRemembered,name+": increasing mist reduces both terrain overlays at density "+density);
                                if(density==1)Check(dark>bright*.88&&remembered>bright*.94,name+": maximum mist leaves only faint terrain shading");
                                lastDark=dark;lastRemembered=remembered;
                                Array.Fill(session.View.visibility,(byte)2);Refresh();
                                Check(clear.SequenceEqual(SnapRgba()),name+": visible terrain is unchanged at density "+density);
                            }
                            Array.Fill(session.View.visibility,(byte)0);Refresh();
                            board.SetFogDensity(0);
                            Check(noMist.SequenceEqual(SnapRgba()),name+": turning mist off restores the same terrain pixels immediately");
                            foreach(float density in new[]{.5f,1})
                            {
                                board.SetFogDensity(density);fog.Update(true,12);
                                SnapRgba(name+"-terrain-unseen-fog-"+Mathf.RoundToInt(density*100));
                            }
                        }
                        board.SetEnhancedLighting(savedLighting);
                        Check(Field<System.Collections.IDictionary>(board,"entities").Count==0&&Field<System.Collections.IDictionary>(board,"resources").Count==0,
                            asset.name+": brighter terrain and mist settings do not reveal hidden units or deposits");
                        SetBands();Refresh();
                        double Luma(Color32 c)=>(c.r*.2126+c.g*.7152+c.b*.0722)/255;
                    }
                    void CheckBoardOpacity()
                    {
                        // RGB previews cannot reveal a passthrough leak. Read RGBA
                        // with a transparent camera background and the real terrain.
                        foreach(bool top in new[]{true,false})
                        {
                            CameraAt(top);preview.camera.backgroundColor=Color.clear;
                            board.SetFogDensity(0);var baseline=SnapRgba();
                            var solid=Enumerable.Range(0,baseline.Length).Where(i=>baseline[i].a==255).ToArray();
                            Check(solid.Length>size*size/10&&baseline[0].a==0,asset.name+": RGBA baseline contains opaque terrain and transparent surroundings");
                            foreach(float density in new[]{.25f,.5f,.75f,1f})
                            {
                                board.SetFogDensity(density);fog.Update(true,0);
                                var pixels=SnapRgba(top?asset.name+"-opaque-"+Mathf.RoundToInt(density*100):null);
                                int minimum=solid.Min(i=>(int)pixels[i].a);
                                Check(minimum>=254,asset.name+": terrain stays opaque at "+density+" density, "+(top?"top":"oblique")+" view; minimum alpha="+minimum);
                                Check(pixels[0].a==0,"Fog leaves the area outside the board transparent for passthrough");
                                if(top)
                                {
                                    Check(BandDifference(baseline,pixels,0)>.001,"Density still changes the visible mist over unseen terrain");
                                    Check(BandDifference(baseline,pixels,2)<.001,"Fog density does not change the visible terrain's colors");
                                }
                            }
                        }
                        board.SetFogDensity(1);
                    }
                    void CheckMistDensity()
                    {
                        var siblings=board.Content.Cast<Transform>().Where(t=>t!=root).Select(t=>(t.gameObject,t.gameObject.activeSelf)).ToArray();
                        foreach(var entry in siblings)entry.gameObject.SetActive(false);
                        CameraAt(true);preview.camera.backgroundColor=Color.clear;
                        double last=0;
                        try
                        {
                            foreach(float density in new[]{0,.25f,.5f,.75f,1})
                            {
                                board.SetFogDensity(density);fog.Update(true,0);var pixels=SnapRgba(asset.name+"-mist-"+Mathf.RoundToInt(density*100));
                                double unseen=BandAverage(pixels,0,c=>c.a/255.0),explored=BandAverage(pixels,1,c=>c.a/255.0);
                                if(density==0)Check(pixels.All(c=>c.a==0),asset.name+": zero density removes all mist coverage");
                                else Check(unseen>last&&unseen>explored*1.3,asset.name+": mist coverage increases with density "+density+"; unseen="+unseen.ToString("0.000"));
                                Check(BandAverage(pixels,2,c=>c.a/255.0)<.001,asset.name+": visible cells stay clear at density "+density);
                                if(density==1)
                                {
                                    Check(unseen>.72&&explored>.3,asset.name+": full density forms a thick cloud bank");
                                    Check(BandAverage(pixels,0,c=>(c.r*.2126+c.g*.7152+c.b*.0722)/255)<.45,asset.name+": full mist uses a dark thematic tint");
                                }
                                last=unseen;
                            }
                        }
                        finally{foreach(var entry in siblings)entry.gameObject.SetActive(entry.activeSelf);board.SetFogDensity(1);}
                    }
                    void CheckPixels()
                    {
                        // Isolate fog from terrain color and decorations so opacity and
                        // exact visibility clipping can be checked from the rendered pixels.
                        var siblings = board.Content.Cast<Transform>().Where(t => t != root).Select(t => (t.gameObject, t.gameObject.activeSelf)).ToArray();
                        foreach (var entry in siblings) entry.gameObject.SetActive(false);
                        try
                        {
                            CameraAt(true); fog.Hide(); var baseline = Snap();
                            fog.Update(true, 0); var first = Snap("visibility-states");
                            double unseen = BandDifference(baseline, first, 0), explored = BandDifference(baseline, first, 1), visible = BandDifference(baseline, first, 2);
                            // RGB response compresses as the dark mist becomes opaque;
                            // CheckMistDensity verifies the stronger separation in actual alpha.
                            Check(unseen > .015 && explored > .005 && unseen > explored * 1.1, "Rendered unseen mist remains stronger than explored mist: " + unseen.ToString("0.000") + " / " + explored.ToString("0.000"));
                            Check(visible < .001, "Currently visible cells receive no fog, including beside the visibility boundary");
                            var originalVisibility = (byte[])session.View.visibility.Clone();
                            board.SetFogDensity(.5f); fog.Update(true, 0); var half = Snap("density-50");
                            double halfUnseen = BandDifference(baseline, half, 0), halfExplored = BandDifference(baseline, half, 1);
                            Check(halfUnseen > .005 && halfUnseen < unseen * .85 && halfExplored > .001 && halfExplored < explored * .85,
                                "Half density visibly reduces both unseen and explored mist");
                            Check(material.GetFloat("_FogDensity") == .5f && PlayerPrefs.GetFloat(BoardView.FogDensityKey) == .5f,
                                "Density updates the shader and device preference immediately");
                            board.SetFogDensity(0); var off = Snap("density-0");
                            Check(!root.gameObject.activeSelf && baseline.SequenceEqual(off), "Zero density disables the fog draw and renders exactly the no-mist baseline");
                            Check(originalVisibility.SequenceEqual(session.View.visibility) && Field<System.Collections.IDictionary>(board, "entities").Count == 0,
                                "Disabling visual mist preserves visibility and hidden enemy filtering");
                            board.SetFogDensity(1); fog.Update(true, 0);
                            Check(root.GetComponent<MeshFilter>().sharedMesh == mesh && root.GetComponent<MeshRenderer>().sharedMaterial == material
                                && first.SequenceEqual(Snap()), "Restoring density reuses the same geometry and restores the original image");
                            fog.Update(true, 40); var later = Snap();
                            Check(BandDifference(first, later, 0) > .001, "Slow cloud drift changes the rendered mist without moving terrain");
                            fog.Update(true, 0); var repeated = Snap();
                            Check(BandDifference(first, repeated, 0) < .0001, "The same fog time renders the same mist pattern");
                            Array.Fill(session.View.visibility, (byte)2); Refresh();
                            foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            fog.Update(true, 0); var clear = Snap();
                            Check(BandDifference(baseline, clear, 0) < .001 && BandDifference(baseline, clear, 1) < .001,
                                "New visibility removes all fog from previously unseen and explored cells");
                            SetBands(); Refresh(); foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            board.Zoom = 2; board.Pan = new Vector2(session.Map.width * .15f, 0); Refresh();
                            foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            fog.Update(true, 0); var zoomed = Snap();
                            foreach (var local in new[] { new Vector3(-board.Width * .6f, 0, 0), new Vector3(board.Width * .6f, 0, 0), new Vector3(0, 0, -board.Depth * .6f), new Vector3(0, 0, board.Depth * .6f) })
                                Check(PixelDifference(baseline[Pixel(board.transform.TransformPoint(local))], zoomed[Pixel(board.transform.TransformPoint(local))]) < .001,
                                    "Zoom and pan keep fog clipped to the physical board bounds");
                        }
                        finally
                        {
                            board.Fit(); foreach (var entry in siblings) if (entry.gameObject != null) entry.gameObject.SetActive(entry.activeSelf);
                            SetBands(); Refresh();
                        }
                    }
                    void SetBands()
                    {
                        for (int y = 0; y < session.Map.height; y++) for (int x = 0; x < session.Map.width; x++)
                            session.View.visibility[y * session.Map.width + x] = (byte)Mathf.Min(2, x * 3 / session.Map.width);
                    }
                    void Refresh()
                    {
                        typeof(BoardView).GetField("fogTick", Private).SetValue(board, -1); Call(board, "LateUpdate");
                    }
                    void CameraAt(bool top)
                    {
                        preview.camera.orthographic = true; preview.camera.aspect = 1; preview.camera.orthographicSize = board.Width * .75f;
                        preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                        preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.018f, .03f, .045f);
                        var center = board.transform.position;
                        preview.camera.transform.position = center + (top ? Vector3.up * 2 : new Vector3(.4f, 1.3f, -1.6f));
                        preview.camera.transform.LookAt(center, top ? Vector3.forward : Vector3.up);
                    }
                    int Pixel(Vector3 world)
                    {
                        var viewport = preview.camera.WorldToViewportPoint(world);
                        int x = Mathf.Clamp(Mathf.FloorToInt(viewport.x * size), 0, size - 1), y = Mathf.Clamp(Mathf.FloorToInt(viewport.y * size), 0, size - 1);
                        return y * size + x;
                    }
                    double BandDifference(Color32[] a, Color32[] b, int band)
                    {
                        double total = 0; int count = 0;
                        int left = Mathf.CeilToInt(session.Map.width * band / 3f), right = Mathf.CeilToInt(session.Map.width * (band + 1) / 3f);
                        for (float x = left + .1f; x < right - .1f; x += .5f) for (float y = 3; y < session.Map.height - 3; y += 3)
                        {
                            int pixel = Pixel(board.Content.TransformPoint(new Vector3(x, 0, y)));
                            total += PixelDifference(a[pixel], b[pixel]); count++;
                        }
                        return total / count;
                    }
                    double BandAverage(Color32[] pixels,int band,Func<Color32,double> value)
                    {
                        double total=0;int count=0;
                        int left=Mathf.CeilToInt(session.Map.width*band/3f),right=Mathf.CeilToInt(session.Map.width*(band+1)/3f);
                        for(float x=left+1.5f;x<right-1.5f;x+=.5f)for(float y=3;y<session.Map.height-3;y+=3)
                        {total+=value(pixels[Pixel(board.Content.TransformPoint(new Vector3(x,0,y)))]);count++;}
                        return total/count;
                    }
                }
                Debug.Log($"FRONTIER_FOG_VISUALS passed: {checks} checks, {captures} renders; no player build or physical Quest performance validation");

                Color32[] Snap(string name = null)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, size, size)); preview.Render(true);
                    var image = preview.EndStaticPreview();
                    try
                    {
                        if (name != null) File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
                        captures++; return image.GetPixels32();
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                Color32[] SnapRgba(string name=null)
                {
                    preview.BeginPreview(new Rect(0,0,size,size),GUIStyle.none);
                    RenderTexture source;
                    try { preview.Render(true); }
                    finally { source=(RenderTexture)preview.EndPreview(); }
                    var target=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32);
                    var previous=RenderTexture.active;var image=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
                    try
                    {
                        Graphics.Blit(source,target);RenderTexture.active=target;
                        image.ReadPixels(new Rect(0,0,size,size),0,0);image.Apply();
                        if(name!=null)File.WriteAllBytes(output+"/"+name+".png",image.EncodeToPNG());
                        captures++;return image.GetPixels32();
                    }
                    finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image); }
                }
                double PixelDifference(Color32 a, Color32 b) => (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 765.0;
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("FOG_PASS: " + message); }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", previousBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", previousMatrix);
                if (hadDensity) PlayerPrefs.SetFloat(BoardView.FogDensityKey, previousDensity); else PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                Shader.SetGlobalFloat("_FrontierFogDensity", previousShaderDensity);
                if (hadLighting) PlayerPrefs.SetInt(BoardView.LightingKey, previousLighting); else PlayerPrefs.DeleteKey(BoardView.LightingKey);
                Shader.SetGlobalFloat("_FrontierEnhancedLighting", previousLight); Shader.SetGlobalColor("_FrontierLightColor", previousLightColor);
                PlayerPrefs.Save();
            }
        }
    }
}
