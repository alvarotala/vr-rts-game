using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Projected events only. All spells share one mesh/material and the board's
    // fog/clipping shader; visual animation never queries hidden simulation state.
    public sealed class AbilityEffects : System.IDisposable
    {
        public const int MaxEvents=32;
        public int ActiveCount {get;private set;}
        private readonly Mesh mesh=new Mesh{name="Ability particles",indexFormat=IndexFormat.UInt32};
        private readonly Material material=new Material(Shader.Find("Frontier/Weapons"));
        private readonly GameObject root;
        private readonly int width,height;
        private readonly List<Vector3> vertices=new List<Vector3>(MaxEvents*320);
        private readonly List<Vector3> uv=new List<Vector3>(MaxEvents*320);
        private readonly List<Color> colors=new List<Color>(MaxEvents*320);
        private readonly List<int> triangles=new List<int>(MaxEvents*480);
        private Vector3 right,up,eye;

        public AbilityEffects(Transform parent,Texture fog,int width,int height)
        {
            this.width=width;this.height=height;
            root=new GameObject("Ability effects",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(parent,false);
            mesh.MarkDynamic();root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            material.SetTexture("_FogTex",fog);material.SetVector("_MapSize",new Vector4(width,height,0,0));
        }

        public void Render(BoardView board,float visualTick,Camera camera)
        {
            vertices.Clear();uv.Clear();colors.Clear();triangles.Clear();ActiveCount=0;
            var view=board.Session.View;
            if(view==null||!board.Content.gameObject.activeInHierarchy){mesh.Clear();return;}
            right=camera!=null?board.Content.InverseTransformDirection(camera.transform.right).normalized:Vector3.right;
            up=camera!=null?board.Content.InverseTransformDirection(camera.transform.up).normalized:new Vector3(0,.707f,.707f);
            eye=Vector3.Cross(right,up).normalized;
            float stroke=Mathf.Clamp(.0018f/board.CellScale,.055f,.17f);
            var events=view.abilityEvents;
            // Newest events win the visual budget. Damage/healing is already authoritative.
            if(events!=null)for(int i=events.Length-1;i>=0&&ActiveCount<MaxEvents;i--)
            {
                var cast=events[i];var ability=board.Session.Catalog.Ability(cast.ability);
                if(ability==null||ability.effect!="damage"&&ability.effect!="heal"||!Visible(view,cast.position))continue;
                bool heal=ability.effect=="heal";float lifetime=heal?.9f:.72f;
                float age=(visualTick-cast.tick)/Simulation.TickRate;
                if(age<0||age>=lifetime)continue;
                ActiveCount++;float progress=age/lifetime,fade=1-progress;
                float radius=Mathf.Max(.25f,ability.radius);
                var center=Ground(board,cast.position.x,cast.position.y,.12f);
                if(heal)
                {
                    var green=new Color(.22f,1,.62f);var light=new Color(.83f,1,.88f);
                    Ring(board,cast.position,radius*(.76f+progress*.24f),stroke,Tint(green,fade*.8f),.1f);
                    Ring(board,cast.position,radius*(.3f+progress*.4f),stroke*.7f,Tint(light,fade*.5f),.22f+progress*.5f);
                    // Rising seeds with short tails communicate restoration, without a damage flash.
                    for(int mote=0;mote<8;mote++)
                    {
                        float angle=mote*Mathf.PI*.25f+cast.id*.37f;
                        float spread=radius*(mote%2==0?.42f:.73f);
                        float rise=progress*(.8f+mote%3*.2f);
                        var point=Ground(board,cast.position.x+Mathf.Cos(angle)*spread,cast.position.y+Mathf.Sin(angle)*spread,.16f+rise);
                        if(!InBounds(point))continue;
                        Beam(point-Vector3.up*.22f,point,stroke*.55f,Tint(green,0),Tint(green,fade*.8f));
                        Particle(point,stroke*2.7f,Tint(light,fade));
                    }
                }
                else
                {
                    var violet=new Color(.7f,.25f,1);var warm=new Color(1,.61f,.22f);
                    float expansion=1-Mathf.Pow(1-progress,3);
                    Ring(board,cast.position,radius*(.18f+expansion*.82f),stroke*1.5f,Tint(violet,fade),.1f);
                    Ring(board,cast.position,radius*(.1f+expansion*.62f),stroke*.65f,Tint(warm,fade*.8f),.15f);
                    Particle(center+Vector3.up*.25f,radius*(.55f+progress*.3f),Tint(violet,fade*.5f));
                    Particle(center+Vector3.up*.3f,stroke*5*(1-progress*.6f),Tint(new Color(1,.9f,.75f),fade));
                    for(int spark=0;spark<8;spark++)
                    {
                        float angle=spark*Mathf.PI*.25f+cast.id*.61f;
                        var direction=new Vector3(Mathf.Cos(angle),.35f+(spark%3)*.2f,Mathf.Sin(angle));
                        float distance=radius*(.15f+expansion*.75f);
                        Beam(center+direction*distance*.72f,center+direction*distance,stroke*.65f,Tint(violet,fade*.25f),Tint(warm,fade));
                    }
                }
            }
            mesh.Clear();
            if(vertices.Count>0){mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
        }

        private bool Visible(PlayerView view,Point p)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y);
            return x>=0&&y>=0&&x<width&&y<height&&view.visibility.Length>y*width+x&&view.visibility[y*width+x]==2;
        }
        private bool InBounds(Vector3 p)=>p.x>=0&&p.z>=0&&p.x<width&&p.z<height;
        private static Vector3 Ground(BoardView board,float x,float z,float lift)=>new Vector3(x,board.HeightAt(new Point(x,z))+lift,z);
        private static Color Tint(Color color,float alpha){color.a=alpha;return color;}
        private void Ring(BoardView board,Point center,float radius,float stroke,Color color,float lift)
        {
            const int segments=32;
            for(int segment=0;segment<segments;segment++)
            {
                float a=segment*Mathf.PI*2/segments,b=(segment+1)*Mathf.PI*2/segments;
                var from=Ground(board,center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius,lift);
                var to=Ground(board,center.x+Mathf.Cos(b)*radius,center.y+Mathf.Sin(b)*radius,lift);
                if(InBounds(from)&&InBounds(to))Beam(from,to,stroke,color,color);
            }
        }
        private void Particle(Vector3 center,float size,Color color)
            =>Quad(center-right*size*.5f-up*size*.5f,center-right*size*.5f+up*size*.5f,center+right*size*.5f+up*size*.5f,center+right*size*.5f-up*size*.5f,color,color,1);
        private void Beam(Vector3 start,Vector3 end,float stroke,Color from,Color to)
        {
            if((end-start).sqrMagnitude<.00001f)return;
            var side=Vector3.Cross(end-start,eye).normalized*stroke*.5f;
            Quad(start-side,start+side,end+side,end-side,from,to,0);
        }
        private Vector3 Clamp(Vector3 point)=>new Vector3(Mathf.Clamp(point.x,.001f,width-.001f),point.y,Mathf.Clamp(point.z,.001f,height-.001f));
        private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color from,Color to,float round)
        {
            int n=vertices.Count;vertices.Add(Clamp(a));vertices.Add(Clamp(b));vertices.Add(Clamp(c));vertices.Add(Clamp(d));
            uv.Add(new Vector3(0,0,round));uv.Add(new Vector3(0,1,round));uv.Add(new Vector3(1,1,round));uv.Add(new Vector3(1,0,round));
            colors.Add(from);colors.Add(from);colors.Add(to);colors.Add(to);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        public void Clear(){mesh.Clear();vertices.Clear();uv.Clear();colors.Clear();triangles.Clear();ActiveCount=0;}
        public void Dispose(){if(root!=null)root.SetActive(false);Release(root);Release(mesh);Release(material);}
        private static void Release(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
