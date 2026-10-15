using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    public sealed class BoardView : MonoBehaviour
    {
        public GameSession Session;
        public Transform Content { get; private set; }
        public float Width = 1.2f, Zoom = 1;
        public Vector2 Pan;
        public bool Placed;
        public int Hovered;
        private Transform terrainRoot;
        private MeshCollider terrainCollider;
        private readonly Dictionary<int, Transform> entities = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> resources = new Dictionary<int, Transform>();
        private readonly Dictionary<string, Stack<Transform>> pools = new Dictionary<string, Stack<Transform>>();
        private readonly Dictionary<int,string> entityTypes = new Dictionary<int,string>();
        private readonly Dictionary<Transform, VisualParts> parts = new Dictionary<Transform, VisualParts>();
        private readonly HashSet<int> alive = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private sealed class VisualParts
        {
            public Transform selection, health, flash;
            public Renderer[] body;
            public bool remembered;
        }
        private Material ground, blue, red, neutral, crystal, catalyst, highlight;
        private Texture2D fog;
        private Color32[] fogColors;
        private int fogTick = -1;
        private Mesh selectionMesh;
        private Transform selectionRect;
        private float selectionHeight;
        private readonly Vector3[] selectionVertices=new Vector3[8];
        private static readonly int[] selectionTriangles={0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
        private Transform preview;
        public Point? Pointer;
        public float CellScale => Width / Session.Map.width * Zoom;
        public float Depth => Width * Session.Map.height / Session.Map.width;
        private void Start()
        {
            Content = new GameObject("Map coordinates").transform; Content.SetParent(transform,false);
            ground=Material(new Color(.16f,.31f,.31f));ground.SetFloat("_UseFog",1);ground.SetFloat("_Grid",1);
            blue=Material(new Color(.2f,.8f,1));red=Material(new Color(1,.36f,.23f));
            neutral=Material(new Color(.2f,.28f,.35f));crystal=Material(new Color(.35f,.85f,1));catalyst=Material(new Color(.8f,.46f,1));highlight=Material(new Color(.9f,1,.6f));
            Session.NewMatch += Rebuild; Rebuild();
        }
        private Material Material(Color color) { var m=new Material(Shader.Find("Frontier/Board")){enableInstancing=true};m.SetColor("_BaseColor",color);return m; }
        public void Rebuild()
        {
            if(Content==null)return;
            if(terrainCollider!=null&&terrainCollider.sharedMesh!=null)Destroy(terrainCollider.sharedMesh);
            if(selectionMesh!=null)Destroy(selectionMesh);
            foreach(Transform child in Content)Destroy(child.gameObject);
            entities.Clear();entityTypes.Clear();resources.Clear();pools.Clear();parts.Clear();preview=null;selectionRect=null;selectionMesh=null;
            if(fog!=null)Destroy(fog);
            fog=new Texture2D(Session.Map.width,Session.Map.height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            fogColors=new Color32[Session.Map.width*Session.Map.height];ground.SetTexture("_FogTex",fog);fogTick=-1;
            var grid=new GridMap(Session.Map);
            selectionHeight=grid.SurfaceLevel.Max()*1.5f+.04f;
            terrainRoot=new GameObject("Terrain").transform;terrainRoot.SetParent(Content,false);
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();var uv=new List<Vector2>();
            for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
            {
                int i=grid.Index(x,y);float h=Height(grid,i);
                Color c=!grid.Walkable[i]?new Color(.35f,.39f,.45f):grid.Ramp[i]?new Color(.75f,.72f,.45f):grid.Elevation[i]>0?new Color(.8f,.88f,.77f):Color.white;
                float VertexHeight(float vx,float vy)
                {
                    if(!grid.Ramp[i])return h;
                    var ramp=Session.Map.terrain.Last(r=>r.ramp&&x>=r.x&&x<r.x+r.width&&y>=r.y&&y<r.y+r.height);
                    float t=ramp.rampAxis=="x"?(vx-ramp.x)/ramp.width:(vy-ramp.y)/ramp.height;
                    return (ramp.rampDirection>0?t:1-t)*1.5f;
                }
                AddQuad(new Vector3(x,VertexHeight(x,y),y),new Vector3(x,VertexHeight(x,y+1),y+1),new Vector3(x+1,VertexHeight(x+1,y+1),y+1),new Vector3(x+1,VertexHeight(x+1,y),y),c);
                if(h>0)
                {
                    AddQuad(new Vector3(x,0,y),new Vector3(x,h,y),new Vector3(x+1,h,y),new Vector3(x+1,0,y),c*.7f);
                    AddQuad(new Vector3(x+1,0,y+1),new Vector3(x+1,h,y+1),new Vector3(x,h,y+1),new Vector3(x,0,y+1),c*.7f);
                    AddQuad(new Vector3(x,0,y+1),new Vector3(x,h,y+1),new Vector3(x,h,y),new Vector3(x,0,y),c*.7f);
                    AddQuad(new Vector3(x+1,0,y),new Vector3(x+1,h,y),new Vector3(x+1,h,y+1),new Vector3(x+1,0,y+1),c*.7f);
                }
            }
            void AddQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tint)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});foreach(var v in new[]{a,b,c,d}){uv.Add(new Vector2(v.x/grid.Width,v.z/grid.Height));colors.Add(tint);}}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            terrainRoot.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;terrainRoot.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ground;
            terrainCollider=terrainRoot.gameObject.AddComponent<MeshCollider>();terrainCollider.sharedMesh=mesh;
            foreach(var d in Session.Map.decoration) { var rock=Primitive(PrimitiveType.Sphere,Content,neutral);rock.localPosition=new Vector3(d.x,1.2f,d.y);rock.localScale=new Vector3(4,2.4f,4); }
            Zoom=1;Pan=Vector2.zero;UpdateTransform();UpdateFog();
        }
        public void Fit() { Zoom=1;Pan=Vector2.zero; }
        public void Navigate(Vector2 move,float zoom,float delta)
        {
            Zoom=Mathf.Clamp(Zoom*Mathf.Exp(zoom*delta),1,4);
            Pan+=move*(Session.Map.width*.35f*delta/Zoom);
            float limitX=Session.Map.width*.5f*(1-1/Zoom),limitY=Session.Map.height*.5f*(1-1/Zoom);
            Pan=new Vector2(Mathf.Clamp(Pan.x,-limitX,limitX),Mathf.Clamp(Pan.y,-limitY,limitY));
        }
        private void UpdateTransform()
        {
            if(Content==null)return;
            Content.localScale=Vector3.one*CellScale;
            Content.localPosition=new Vector3(-(Session.Map.width*.5f+Pan.x)*CellScale,0,-(Session.Map.height*.5f+Pan.y)*CellScale);
            Shader.SetGlobalMatrix("_FrontierWorldToBoard",transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_FrontierBounds",new Vector4(Width*.5f,Depth*.5f,Session.Map.width,Session.Map.height));
        }
        private void LateUpdate()
        {
            UpdateTransform(); UpdateFog();
            if(Session.View==null)return;
            alive.Clear();
            foreach(var e in Session.View.entities)
            {
                alive.Add(e.id);var type=Session.Catalog[e.definition];
                bool newlyVisible=!entities.TryGetValue(e.id,out var visual);
                if(newlyVisible)
                {
                    string key=e.definition+"/"+e.owner;
                    if(!pools.TryGetValue(key,out var pool))pools[key]=pool=new Stack<Transform>();
                    visual=pool.Count>0?pool.Pop():MakeEntity(type,e.owner);visual.gameObject.SetActive(true);entities[e.id]=visual;entityTypes[e.id]=key;
                }
                var point=e.position;float half=type.building?type.footprint*.5f:0;
                var target=new Vector3(point.x+half,HeightAt(point),point.y+half);
                Vector3 direction=target-visual.localPosition;direction.y=0;
                if(!type.building&&direction.sqrMagnitude>.01f)visual.localRotation=Quaternion.Slerp(visual.localRotation,Quaternion.LookRotation(direction),Time.unscaledDeltaTime*12);
                visual.localPosition=type.building||newlyVisible?target:Vector3.Lerp(visual.localPosition,target,1-Mathf.Exp(-Time.unscaledDeltaTime*22));
                visual.localScale=Vector3.one*(e.complete?1:Mathf.Lerp(.25f,1,e.progress));
                var cached=parts[visual];cached.selection.gameObject.SetActive(Session.Selection.Contains(e.id)||Hovered==e.id);
                cached.health.localScale=new Vector3(Mathf.Max(.03f,e.health/type.health)*(type.building?type.footprint:1.4f),.1f,.12f);
                cached.flash.gameObject.SetActive(!e.remembered&&e.attackTick>0&&Session.View.tick-e.attackTick<3);
                if(newlyVisible||cached.remembered!=e.remembered)
                {
                    foreach(var renderer in cached.body)renderer.sharedMaterial=e.remembered?neutral:e.owner==0?blue:red;
                    cached.remembered=e.remembered;
                }
            }
            removed.Clear();foreach(int id in entities.Keys)if(!alive.Contains(id))removed.Add(id);
            foreach(int id in removed) { var v=entities[id];v.gameObject.SetActive(false);pools[entityTypes[id]].Push(v);entities.Remove(id);entityTypes.Remove(id); }
            foreach(var resource in Session.View.resources)
            {
                if(!resources.TryGetValue(resource.id,out var visual))
                {visual=Primitive(resource.kind=="mineral"?PrimitiveType.Cube:PrimitiveType.Sphere,Content,resource.kind=="mineral"?crystal:catalyst);resources[resource.id]=visual;visual.localScale=new Vector3(1.1f,1.7f,1.1f);visual.localRotation=Quaternion.Euler(0,45,resource.kind=="mineral"?20:0);}
                visual.localPosition=new Vector3(resource.position.x,HeightAt(resource.position)+.85f,resource.position.y);visual.gameObject.SetActive(resource.remaining!=0);
            }
            if(Session.BuildDefinition!=null&&Pointer.HasValue)
            {
                if(preview==null)preview=Primitive(PrimitiveType.Cube,Content,highlight);
                int size=Session.Catalog[Session.BuildDefinition].footprint;Point p=Pointer.Value;
                preview.gameObject.SetActive(true);preview.localPosition=new Vector3(Mathf.Floor(p.x)+size*.5f,HeightAt(p)+.15f,Mathf.Floor(p.y)+size*.5f);preview.localScale=new Vector3(size,.3f,size);
                bool valid=Session.Match.Map.CanPlace(new Point(Mathf.Floor(p.x),Mathf.Floor(p.y)),size)&&Session.Match.Visible(0,p);
                preview.GetComponent<Renderer>().sharedMaterial=valid?highlight:red;
            }
            else if(preview!=null)preview.gameObject.SetActive(false);
        }
        private Transform MakeEntity(Archetype type,int owner)
        {
            var root=new GameObject(type.name).transform;root.SetParent(Content,false);
            bool bio=type.faction=="biological";Material color=owner==0?blue:red;
            // The ground body fits its circular collision footprint, including rotated cube corners.
            float size=type.building?type.footprint*.85f:type.collisionRadius*(bio?2f:1.4142f);
            float height=type.building?type.role=="hq"?3.4f:2:type.role=="siege"?1:type.role=="scout"?.65f:type.role=="melee"?1.6f:1.25f;
            var body=Primitive(bio?PrimitiveType.Sphere:PrimitiveType.Cube,root,color);body.localScale=new Vector3(size,height,size);body.localPosition=Vector3.up*height*.5f;
            var top=Primitive(bio?PrimitiveType.Capsule:PrimitiveType.Cube,root,color);top.localPosition=new Vector3(0,height,0);top.localScale=new Vector3(size*.4f,type.role=="defense"||type.role=="siege"?.8f:.35f,size*.4f);
            if(!type.building)
            {
                var nose=Primitive(PrimitiveType.Cube,root,color);nose.localPosition=new Vector3(0,height*.6f,.75f);nose.localScale=new Vector3(.2f,.2f,type.role=="ranged"||type.role=="siege"?1.6f:.5f);
                if(type.role=="worker") {top.localRotation=Quaternion.Euler(0,0,45);top.localScale=Vector3.one*.6f;}
            }
            var ring=Primitive(PrimitiveType.Cylinder,root,highlight);ring.name="Selection";ring.localPosition=Vector3.up*.04f;ring.localScale=new Vector3(size*1.3f,.035f,size*1.3f);ring.gameObject.SetActive(false);
            var hp=Primitive(PrimitiveType.Cube,root,highlight);hp.name="Health";hp.localPosition=new Vector3(0,height+1,0);
            var flash=Primitive(PrimitiveType.Sphere,root,highlight);flash.name="Flash";flash.localPosition=new Vector3(0,height*.6f,size*.6f);flash.localScale=Vector3.one*.65f;flash.gameObject.SetActive(false);
            parts[root]=new VisualParts{selection=ring,health=hp,flash=flash,body=root.GetComponentsInChildren<Renderer>().Where(r=>r.transform!=ring&&r.transform!=hp&&r.transform!=flash).ToArray()};
            return root;
        }
        private static Transform Primitive(PrimitiveType type,Transform parent,Material material)
        {var go=GameObject.CreatePrimitive(type);Destroy(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        private void UpdateFog()
        {
            if(fog==null||Session.Match!=null&&fogTick==Session.View.tick/4)return;
            fogTick=Session.View==null?-1:Session.View.tick/4;
            for(int i=0;i<fogColors.Length;i++){byte v=Session.View==null?(byte)255:Session.View.visibility[i]==2?(byte)255:Session.View.visibility[i]==1?(byte)75:(byte)0;fogColors[i]=new Color32(v,v,v,255);}
            fog.SetPixels32(fogColors);fog.Apply(false);
        }
        private static float Height(GridMap map,int cell) => map.SurfaceLevel[cell]*1.5f;
        public float HeightAt(Point point)
        {if(Session.Match==null)return 0;int c=Session.Match.Map.Cell(point);return c<0?0:Height(Session.Match.Map,c);}
        public bool RayToMap(Ray ray,out Point point)
        {
            point=default;
            if(terrainCollider==null||!terrainCollider.Raycast(ray,out var hit,20))return false;
            var world=hit.point;var local=transform.InverseTransformPoint(world);
            if(Mathf.Abs(local.x)>Width*.5f||Mathf.Abs(local.z)>Depth*.5f)return false;
            var map=Content.InverseTransformPoint(world);point=new Point(map.x,map.z);
            return point.x>=0&&point.y>=0&&point.x<Session.Map.width&&point.y<Session.Map.height;
        }
        public int EntityAt(Point point)
        {
            if(Session.View==null)return 0;
            var found=Session.View.entities.Where(e=>!e.remembered).Select(e=>new{e,t=Session.Catalog[e.definition]})
                .Where(a=>new Point(a.e.position.x+(a.t.building?a.t.footprint*.5f:0),a.e.position.y+(a.t.building?a.t.footprint*.5f:0)).Distance(point)<(a.t.building?a.t.footprint*.8f:1.6f))
                .OrderBy(a=>a.e.position.Distance(point)).FirstOrDefault();return found?.e.id??0;
        }
        public int ResourceAt(Point point) => Session.View?.resources.Where(r=>r.remaining!=0&&r.position.Distance(point)<2).OrderBy(r=>r.position.Distance(point)).FirstOrDefault()?.id??0;
        public void DrawSelection(Point a,Point b,bool visible)
        {
            if(Content==null||(!visible&&selectionRect==null))return;
            if(selectionRect==null)
            {
                selectionRect=new GameObject("Selection rectangle",typeof(MeshFilter),typeof(MeshRenderer)).transform;
                selectionRect.SetParent(Content,false);
                selectionMesh=new Mesh{name="Flat selection outline"};selectionMesh.MarkDynamic();
                selectionMesh.vertices=selectionVertices;selectionMesh.triangles=selectionTriangles;
                selectionMesh.colors=new[]{Color.white,Color.white,Color.white,Color.white,Color.white,Color.white,Color.white,Color.white};
                selectionMesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up,Vector3.up};
                selectionRect.GetComponent<MeshFilter>().sharedMesh=selectionMesh;
                var renderer=selectionRect.GetComponent<MeshRenderer>();renderer.sharedMaterial=highlight;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            selectionRect.gameObject.SetActive(visible);if(!visible)return;
            // Four horizontal strips form a continuous rectangle above the highest terrain.
            // Unlike a camera-facing LineRenderer, it cannot turn into walls in stereo.
            float x0=Mathf.Min(a.x,b.x),x1=Mathf.Max(a.x,b.x),z0=Mathf.Min(a.y,b.y),z1=Mathf.Max(a.y,b.y);
            float width=Mathf.Min(.002f/CellScale,Mathf.Min(x1-x0,z1-z0)*.5f);
            selectionVertices[0]=new Vector3(x0,selectionHeight,z0);selectionVertices[1]=new Vector3(x0,selectionHeight,z1);
            selectionVertices[2]=new Vector3(x1,selectionHeight,z1);selectionVertices[3]=new Vector3(x1,selectionHeight,z0);
            selectionVertices[4]=new Vector3(x0+width,selectionHeight,z0+width);selectionVertices[5]=new Vector3(x0+width,selectionHeight,z1-width);
            selectionVertices[6]=new Vector3(x1-width,selectionHeight,z1-width);selectionVertices[7]=new Vector3(x1-width,selectionHeight,z0+width);
            selectionMesh.vertices=selectionVertices;selectionMesh.RecalculateBounds();
        }
    }
}
