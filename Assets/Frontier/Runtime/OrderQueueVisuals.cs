using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // A plan of requested destinations, not a preview of the navigation route.
    // Only one selected own unit is drawn, using information from PlayerView.
    public sealed class OrderQueueVisuals : System.IDisposable
    {
        public const int MaximumWaypoints=8;
        private static readonly Color MoveTint=new Color(.25f,.9f,1),AttackTint=new Color(1,.53f,.2f),WorkTint=new Color(.58f,1,.53f);
        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Color> colors=new List<Color>();
        private readonly List<Vector3> normals=new List<Vector3>();
        private PlayerView lastView;
        private int lastRepresentative;
        private float lastScale;
        private BoardView board;

        public OrderQueueVisuals(Transform parent)
        {
            root=new GameObject("Unit order waypoints",typeof(MeshFilter),typeof(MeshRenderer)).transform;root.SetParent(parent,false);
            mesh=new Mesh{name="Unit order plan",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
            material=new Material(Shader.Find("Frontier/Board"));material.SetColor("_BaseColor",Color.white);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            root.gameObject.SetActive(false);
        }
        public static VisibleEntity Representative(GameSession session)=>session.View?.entities
            .Where(e=>e.owner==session.View.player&&!e.remembered&&session.Selection.Contains(e.id)&&!session.Catalog[e.definition].building&&e.pendingOrders.Length>0)
            .OrderBy(e=>e.id).FirstOrDefault();

        public void Update(BoardView value)
        {
            board=value;var session=board.Session;var view=session.View;
            var unit=session.Running&&board.Content.gameObject.activeInHierarchy?Representative(session):null;
            if(unit==null){Hide();return;}
            root.gameObject.SetActive(true);
            if(ReferenceEquals(lastView,view)&&lastRepresentative==unit.id&&lastScale==board.CellScale)return;
            lastView=view;lastRepresentative=unit.id;lastScale=board.CellScale;
            vertices.Clear();triangles.Clear();colors.Clear();normals.Clear();
            Point origin=unit.position;int number=1;
            AddOrder(unit.order,unit.target,unit.destination,null,true);
            foreach(var order in unit.pendingOrders.Take(MaximumWaypoints-1))AddOrder(order.kind,order.target,order.position,order.definition,false);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetNormals(normals);mesh.RecalculateBounds();

            void AddOrder(CommandKind kind,int target,Point position,string definition,bool active)
            {
                if(!TryPosition(kind,target,position,definition,active,out var destination)){number++;return;}
                var tint=kind==CommandKind.Attack||kind==CommandKind.AttackMove?AttackTint:kind==CommandKind.Move?MoveTint:WorkTint;
                float radius=Mathf.Clamp(.007f/board.CellScale,.55f,1.1f),width=Mathf.Clamp(.00065f/board.CellScale,.035f,.11f);
                var delta=new Vector2(destination.x-origin.x,destination.y-origin.y);float distance=delta.magnitude;
                if(distance>radius*2)
                {
                    var direction=delta/distance;
                    Stroke(new Vector2(origin.x,origin.y)+direction*radius,new Vector2(destination.x,destination.y)-direction*radius,width,tint*.65f);
                }
                var center=new Vector2(destination.x,destination.y);
                for(int i=0;i<28;i++)
                {
                    float a=i*Mathf.PI*2/28,b=(i+1)*Mathf.PI*2/28;
                    Stroke(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width*1.4f,tint);
                }
                Number(number,center,radius*.6f,width*1.5f);
                origin=destination;number++;
            }
        }
        private bool TryPosition(CommandKind kind,int target,Point position,string definition,bool active,out Point result)
        {
            result=position;var session=board.Session;
            if(kind==CommandKind.Gather)
            {
                var deposit=session.View.resources.FirstOrDefault(r=>r.id==target);if(deposit==null)return false;result=deposit.position;
            }
            else if(kind==CommandKind.Attack||kind==CommandKind.Repair||kind==CommandKind.Build&&active)
            {
                var entity=session.View.entities.FirstOrDefault(e=>e.id==target&&!e.remembered);if(entity==null)return false;
                var type=session.Catalog[entity.definition];float half=type.building?type.footprint*.5f:0;result=new Point(entity.position.x+half,entity.position.y+half);
            }
            else if(kind==CommandKind.Build&&!string.IsNullOrEmpty(definition))
            {
                float half=session.Catalog[definition].footprint*.5f;result=new Point(Mathf.Floor(position.x)+half,Mathf.Floor(position.y)+half);
            }
            else if(kind!=CommandKind.Move&&kind!=CommandKind.AttackMove)return false;
            return result.x>=0&&result.y>=0&&result.x<session.Map.width&&result.y<session.Map.height;
        }
        private void Number(int value,Vector2 center,float size,float width)
        {
            // Flat seven-segment digits share the board-clipped material; no labels,
            // colliders or camera-facing walls can obscure selection at a waypoint.
            int[] masks={0,6,91,79,102,109,125,7,127};int mask=masks[value];
            var a=new Vector2(-.45f,1)*size;var b=new Vector2(.45f,1)*size;
            var c=new Vector2(.45f,0)*size;var d=new Vector2(.45f,-1)*size;
            var e=new Vector2(-.45f,-1)*size;var f=new Vector2(-.45f,0)*size;
            var starts=new[]{a,b,c,e,f,a,f};var ends=new[]{b,c,d,d,e,f,c};
            for(int bit=0;bit<7;bit++)if((mask&(1<<bit))!=0)Stroke(center+starts[bit],center+ends[bit],width,Color.white);
        }
        private void Stroke(Vector2 a,Vector2 b,float width,Color color)
        {
            var delta=b-a;float distance=delta.magnitude;if(distance<.0001f)return;
            var side=new Vector2(-delta.y,delta.x)*(width*.5f/distance);int steps=Mathf.Max(1,Mathf.CeilToInt(distance/.35f));
            for(int i=0;i<steps;i++)
            {
                var p=Vector2.Lerp(a,b,i/(float)steps);var q=Vector2.Lerp(a,b,(i+1)/(float)steps);
                var a0=Surface(p-side);var a1=Surface(p+side);var b1=Surface(q+side);var b0=Surface(q-side);
                // Do not connect different cliff levels with vertical geometry.
                float min=Mathf.Min(Mathf.Min(a0.y,a1.y),Mathf.Min(b1.y,b0.y)),max=Mathf.Max(Mathf.Max(a0.y,a1.y),Mathf.Max(b1.y,b0.y));
                if(max-min>.35f)continue;
                int first=vertices.Count;vertices.Add(a0);vertices.Add(a1);vertices.Add(b1);vertices.Add(b0);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
                for(int j=0;j<4;j++){colors.Add(color);normals.Add(Vector3.up);}
            }
        }
        private Vector3 Surface(Vector2 point)
        {
            var map=board.Session.Map;
            float x=Mathf.Clamp(point.x,0,map.width-.001f),y=Mathf.Clamp(point.y,0,map.height-.001f);
            float height=board.HeightAt(new Point(x,y));
            for(int i=map.terrain.Length-1;i>=0;i--)
            {
                var ramp=map.terrain[i];if(!ramp.ramp||x<ramp.x||x>=ramp.x+ramp.width||y<ramp.y||y>=ramp.y+ramp.height)continue;
                float t=ramp.rampAxis=="x"?(x-ramp.x)/ramp.width:(y-ramp.y)/ramp.height;
                height=(ramp.rampDirection>0?t:1-t)*1.5f;break;
            }
            return new Vector3(point.x,height+.025f,point.y);
        }
        public void Hide(){if(root!=null)root.gameObject.SetActive(false);lastView=null;lastRepresentative=0;}
        public void Dispose(){Release(root==null?null:root.gameObject);Release(mesh);Release(material);}
        private static void Release(Object item){if(item==null)return;if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);}
    }
}
