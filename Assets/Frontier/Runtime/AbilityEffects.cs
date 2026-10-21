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
        public const int MaxVerticesPerEvent=768,MaxVertices=MaxEvents*MaxVerticesPerEvent;
        // Core retains projected casts for one second. Finish the visual before that
        // boundary, including the soft healing tail, without changing spell timing.
        public const float DamageLifetime=.8f,HealingLifetime=.98f;
        public int ActiveCount {get;private set;}
        private readonly Mesh mesh=new Mesh{name="Ability particles",indexFormat=IndexFormat.UInt32};
        private readonly Material material=new Material(Shader.Find("Frontier/Weapons"));
        private readonly GameObject root;
        private readonly int width,height;
        private readonly List<Vector3> vertices=new List<Vector3>(MaxVertices);
        private readonly List<Vector3> uv=new List<Vector3>(MaxVertices);
        private readonly List<Color> colors=new List<Color>(MaxVertices);
        private readonly List<int> triangles=new List<int>(MaxVertices*3/2);
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
                bool heal=ability.effect=="heal";float lifetime=heal?HealingLifetime:DamageLifetime;
                float age=(visualTick-cast.tick)/Simulation.TickRate;
                if(age<0||age>=lifetime)continue;
                ActiveCount++;
                float radius=Mathf.Max(.25f,ability.radius);
                bool biological=ability.faction=="biological";
                if(heal)Healing(board,cast,age,radius,stroke,biological);
                else Impact(board,cast,age,radius,stroke,biological);
            }
            mesh.Clear();
            if(vertices.Count>0){mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
        }

        private void Healing(BoardView board,AbilityEvent cast,float age,float radius,float stroke,bool biological)
        {
            float t=age/HealingLifetime,fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,HealingLifetime,age));
            var green=biological?new Color(.38f,1,.48f):new Color(.14f,.96f,.76f);
            var light=biological?new Color(.88f,1,.64f):new Color(.72f,1,.96f);
            float seed=cast.id*.618034f;
            // A stable perimeter shows the real area. A second pulse flows inward;
            // neither is an opaque disc, so units and their health remain visible.
            Ring(board,cast.position,radius,stroke,Tint(green,fade*.65f),.1f,0,0);
            Ring(board,cast.position,radius*Mathf.Lerp(.92f,.32f,Mathf.SmoothStep(0,1,t)),stroke*.72f,Tint(light,fade*.65f),.16f,biological?0:6,t*.16f);
            // Three open ribbons spiral upward, with separated heads readable from
            // above. Symbiosis curls; Consortium keeps a cleaner scanning pattern.
            for(int ribbon=0;ribbon<3;ribbon++)
            {
                float phase=seed+ribbon*Mathf.PI*2/3+t*(biological?1.5f:.9f);
                const int segments=10;
                for(int step=0;step<segments;step++)
                {
                    float a=step/(float)segments,b=(step+1)/(float)segments;
                    Vector3 Curve(float u)
                    {
                        float angle=phase+u*(biological?2.2f:1.45f),spread=radius*(.68f-u*.26f)*(1-t*.16f);
                        return Ground(board,cast.position.x+Mathf.Cos(angle)*spread,cast.position.y+Mathf.Sin(angle)*spread,
                            .14f+t*.45f+u*(.65f+t*.35f));
                    }
                    Beam(Curve(a),Curve(b),stroke*(biological?.8f:.65f),Tint(green,fade*a*.55f),Tint(light,fade*b*.7f));
                }
                float head=phase+(biological?2.2f:1.45f),distance=radius*.42f*(1-t*.16f);
                Particle(Ground(board,cast.position.x+Mathf.Cos(head)*distance,cast.position.y+Mathf.Sin(head)*distance,.79f+t*.8f),stroke*3.1f,Tint(light,fade));
            }
            for(int mote=0;mote<9;mote++)
            {
                float phase=seed+mote*2.39996f;
                float flight=Mathf.Clamp01((t-mote*.018f)/.84f);
                float spread=radius*(.25f+(mote%3)*.22f)*(1-flight*.2f);
                var point=Ground(board,cast.position.x+Mathf.Cos(phase+flight*.35f)*spread,cast.position.y+Mathf.Sin(phase+flight*.35f)*spread,
                    .16f+flight*(.8f+mote%3*.23f));
                float alpha=fade*Mathf.SmoothStep(0,1,flight*5)*.9f;
                if(biological)
                {
                    Beam(point-Vector3.up*.24f,point,stroke*.55f,Tint(green,0),Tint(green,alpha));
                    Particle(point,stroke*2.8f,Tint(light,alpha));
                }
                else Cross(point,stroke*3.4f,stroke*.68f,Tint(light,alpha));
            }
        }

        private void Impact(BoardView board,AbilityEvent cast,float age,float radius,float stroke,bool biological)
        {
            float t=age/DamageLifetime,fade=1-Mathf.SmoothStep(0,1,t),expansion=1-Mathf.Exp(-age*13);
            var energy=biological?new Color(.9f,.26f,.72f):new Color(.57f,.4f,1);
            var hot=biological?new Color(1,.76f,.34f):new Color(.66f,.91f,1);
            var center=Ground(board,cast.position.x,cast.position.y,.2f);
            float flash=1-Mathf.Clamp01(age/.2f);
            // Impact begins immediately, in the same tick as authoritative damage.
            // The bright core is compact and short; most of the area stays open.
            Particle(center+Vector3.up*.4f,radius*(.44f+expansion*.3f),Tint(energy,flash*.42f));
            Particle(center+Vector3.up*.4f,stroke*(6+flash*4),Tint(hot,flash));
            Ring(board,cast.position,radius*(.18f+expansion*.82f),stroke*1.65f,Tint(energy,fade*.9f),.1f,biological?-5:0,cast.id*.31f);
            Ring(board,cast.position,radius*(.12f+expansion*.65f),stroke*.75f,Tint(hot,fade*.65f),.19f,biological?-3:6,-t*.2f);
            // Ion Burst fractures into angular electrical spokes; Spore Burst
            // throws curved organic tendrils. Both dissipate instead of flickering.
            for(int arc=0;arc<6;arc++)
            {
                float angle=arc*Mathf.PI/3+cast.id*.37f;
                var previous=center;
                for(int step=1;step<=4;step++)
                {
                    float fraction=step*.25f,turn=angle+(biological?Mathf.Sin(fraction*3+t*2)*.22f:(step%2==0?-.12f:.12f));
                    float distance=radius*(.12f+expansion*.72f)*fraction;
                    var point=Ground(board,cast.position.x+Mathf.Cos(turn)*distance,cast.position.y+Mathf.Sin(turn)*distance,
                        .16f+Mathf.Sin(fraction*Mathf.PI)*(.3f+expansion*.35f));
                    float alpha=Mathf.Clamp01(1-age/.42f)*(.9f-fraction*.35f);
                    Beam(previous,point,stroke*.85f,Tint(energy,alpha),Tint(hot,alpha));previous=point;
                }
            }
            for(int spark=0;spark<12;spark++)
            {
                float angle=spark*2.39996f+cast.id*.61f,reach=radius*(.2f+expansion*(.48f+spark%3*.1f));
                float loft=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t*1.12f))*(.65f+spark%3*.3f);
                var point=Ground(board,cast.position.x+Mathf.Cos(angle)*reach,cast.position.y+Mathf.Sin(angle)*reach,.14f+loft);
                var tail=Ground(board,cast.position.x+Mathf.Cos(angle)*reach*.82f,cast.position.y+Mathf.Sin(angle)*reach*.82f,.14f+loft*.87f);
                Beam(tail,point,stroke*(biological?.8f:.6f),Tint(energy,fade*.18f),Tint(hot,fade*.85f));
                Particle(point,stroke*(biological?3.8f:2.3f),Tint(biological?energy:hot,fade*.8f));
            }
        }

        private bool Visible(PlayerView view,Point p)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y);
            return x>=0&&y>=0&&x<width&&y<height&&view.visibility.Length>y*width+x&&view.visibility[y*width+x]==2;
        }
        private bool InBounds(Vector3 p)=>p.x>=0&&p.z>=0&&p.x<width&&p.z<height;
        private static Vector3 Ground(BoardView board,float x,float z,float lift)=>new Vector3(x,board.HeightAt(new Point(x,z))+lift,z);
        private static Color Tint(Color color,float alpha){color.a=alpha;return color;}
        private void Ring(BoardView board,Point center,float radius,float stroke,Color color,float lift,int facets,float rotation)
        {
            const int segments=30;
            Vector3 Position(float a)
            {
                float x=Mathf.Cos(a),z=Mathf.Sin(a);
                if(facets>0)
                {
                    float sector=Mathf.PI*2/facets,u=Mathf.Repeat(a,Mathf.PI*2)/sector,index=Mathf.Floor(u);
                    x=Mathf.Lerp(Mathf.Cos(index*sector),Mathf.Cos((index+1)*sector),u-index);
                    z=Mathf.Lerp(Mathf.Sin(index*sector),Mathf.Sin((index+1)*sector),u-index);
                }
                float wave=facets<0?1+.055f*Mathf.Sin(a*-facets+rotation):1;
                float cos=Mathf.Cos(rotation),sin=Mathf.Sin(rotation);
                return Ground(board,center.x+(x*cos-z*sin)*radius*wave,center.y+(x*sin+z*cos)*radius*wave,lift);
            }
            for(int segment=0;segment<segments;segment++)
            {
                float a=segment*Mathf.PI*2/segments,b=(segment+1)*Mathf.PI*2/segments;
                var from=Position(a);var to=Position(b);
                if(InBounds(from)&&InBounds(to))Beam(from,to,stroke,color,color);
            }
        }
        private void Cross(Vector3 center,float size,float stroke,Color color)
        {Beam(center-right*size*.5f,center+right*size*.5f,stroke,color,color);Beam(center-up*size*.5f,center+up*size*.5f,stroke,color,color);}
        private void Particle(Vector3 center,float size,Color color)
            =>Quad(center-right*size*.5f-up*size*.5f,center-right*size*.5f+up*size*.5f,center+right*size*.5f+up*size*.5f,center+right*size*.5f-up*size*.5f,color,color,1);
        private void Beam(Vector3 start,Vector3 end,float stroke,Color from,Color to)
        {
            if((end-start).sqrMagnitude<.00001f)return;
            var cross=Vector3.Cross((end-start).normalized,eye);
            if(cross.sqrMagnitude<.0001f){Particle((start+end)*.5f,stroke*1.8f,to);return;}
            var side=cross.normalized*stroke*.5f;
            Quad(start-side,start+side,end+side,end-side,from,to,0);
        }
        private Vector3 Clamp(Vector3 point)=>new Vector3(Mathf.Clamp(point.x,.001f,width-.001f),point.y,Mathf.Clamp(point.z,.001f,height-.001f));
        private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color from,Color to,float round)
        {
            if(vertices.Count+4>MaxVertices)return;
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
