using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // One cached mesh per building, with team hull, structure and role accent submeshes.
    public sealed class BuildingVisuals
    {
        private readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        public static Color Accent(string role)
        {
            switch(role)
            {
                case "hq":return new Color(1,.85f,.25f);
                case "barracks":return new Color(1,.55f,.18f);
                case "extractor":return new Color(.82f,.5f,1);
                case "supply":return new Color(.5f,1,.55f);
                case "tech":return new Color(.7f,.94f,1);
                default:return new Color(1,.9f,.77f);
            }
        }
        public Mesh Get(Archetype type)
        {
            if(meshes.TryGetValue(type.id,out var mesh))return mesh;
            var b=new Builder();bool bio=type.faction=="biological";
            void Part(float x,float y,float z,float sx,float sy,float sz,int material=0)
            {if(bio)b.Lobe(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);else b.Box(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);}
            // All horizontal extents, including the role plate, stay inside the logical footprint.
            if(bio)b.Lobe(new Vector3(0,.08f,0),new Vector3(.96f,.16f,.96f),1);
            else b.Prism(new Vector3(0,.05f,0),new Vector3(.96f,.1f,.96f),1,8);
            float badgeHeight;
            switch(type.role)
            {
                case "hq": // Stepped citadel / crown of organs: the largest, most prominent structure.
                    Part(0,.19f,0,.82f,.25f,.82f);
                    Part(0,.41f,.12f,.52f,.38f,.5f);
                    Part(0,.66f,.12f,.3f,.2f,.3f,1);
                    for(int i=-1;i<=1;i++)Part(i*.13f,i==0?.8f:.75f,.12f,.085f,i==0?.2f:.12f,.12f,2);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                        Part(x*.32f,.25f,z*.32f,.18f,.3f,.18f);
                    badgeHeight=.335f;break;
                case "barracks": // Broad low hangar / three brood eggs, with an obvious wide entrance.
                    Part(0,.22f,.06f,.84f,.27f,.72f);
                    if(bio)
                    {
                        for(int i=-1;i<=1;i++)Part(i*.24f,.4f,.16f,.23f,.35f,.37f,2);
                        Part(-.32f,.26f,-.25f,.16f,.36f,.25f);Part(.32f,.26f,-.25f,.16f,.36f,.25f);
                    }
                    else
                    {
                        b.Box(new Vector3(0,.39f,.12f),new Vector3(.9f,.13f,.65f),0);
                        b.Box(new Vector3(0,.23f,-.308f),new Vector3(.6f,.27f,.028f),1);
                        for(int i=-1;i<=1;i++)b.Box(new Vector3(i*.23f,.47f,.12f),new Vector3(.09f,.04f,.5f),2);
                    }
                    // Entry ramp remains recognizable from the opposite side by its roof chevrons.
                    b.Box(new Vector3(0,.12f,-.36f),new Vector3(.58f,.05f,.22f),1);
                    badgeHeight=.51f;break;
                case "extractor": // Twin exhaust stacks / open siphons; purple is the gas cue.
                    Part(0,.2f,0,.77f,.25f,.72f);
                    for(int i=-1;i<=1;i+=2)
                    {
                        Part(i*.23f,.47f,.12f,.29f,.63f,.31f);
                        b.Tube(new Vector3(i*.23f,.76f,.12f),.16f,.11f,.1f,2);
                    }
                    Part(0,.24f,-.29f,.58f,.19f,.22f,2);
                    badgeHeight=.35f;break;
                case "supply": // Low stack of stores / connected storage sacs, never a tall tower.
                    for(int i=-1;i<=1;i++)
                    {
                        Part(i*.27f,.23f,.04f,.24f,.29f,.58f);
                        Part(i*.27f,.4f,.13f,.21f,.08f,.34f,2);
                    }
                    badgeHeight=.46f;break;
                case "tech": // Vertical reactor in a fork / tall chrysalis, with two orbit rings.
                    Part(-.29f,.41f,.11f,.15f,.65f,.28f);Part(.29f,.41f,.11f,.15f,.65f,.28f);
                    b.Lobe(new Vector3(0,.54f,.11f),new Vector3(.31f,.83f,.31f),2);
                    b.Tube(new Vector3(0,.42f,.11f),.29f,.24f,.07f,1);
                    b.Tube(new Vector3(0,.7f,.11f),.27f,.22f,.05f,0);
                    badgeHeight=.24f;break;
                default: // Defensive turret / stinger; barrel at the existing weapon-effect height.
                    Part(0,.37f,.06f,.38f,.61f,.38f);
                    Part(0,.87f,.06f,.24f,.51f,.25f);
                    Part(0,1.16f,.06f,.55f,.22f,.45f);
                    Part(0,1.225f,.24f,.13f,.13f,.35f,1);
                    Part(0,1.225f,.41f,.16f,.16f,.1f,2);
                    badgeHeight=.22f;break;
            }
            b.Badge(type.role,badgeHeight);
            mesh=b.Finish(type.id,type.footprint);meshes.Add(type.id,mesh);return mesh;
        }
        public void Dispose()
        {
            foreach(var mesh in meshes.Values)if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);
            meshes.Clear();
        }
        private sealed class Builder
        {
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public void Box(Vector3 center,Vector3 size,int material)
            {
                Vector3 V(float x,float y,float z)=>center+Vector3.Scale(new Vector3(x,y,z),size)*.5f;
                Quad(V(-1,1,-1),V(-1,1,1),V(1,1,1),V(1,1,-1),material);
                Quad(V(-1,-1,1),V(-1,-1,-1),V(1,-1,-1),V(1,-1,1),material);
                Quad(V(-1,-1,-1),V(-1,1,-1),V(1,1,-1),V(1,-1,-1),material);
                Quad(V(1,-1,1),V(1,1,1),V(-1,1,1),V(-1,-1,1),material);
                Quad(V(-1,-1,1),V(-1,1,1),V(-1,1,-1),V(-1,-1,-1),material);
                Quad(V(1,-1,-1),V(1,1,-1),V(1,1,1),V(1,-1,1),material);
            }
            public void Lobe(Vector3 center,Vector3 size,int material)
            {
                Vector3 V(int ring,int side)
                {float lat=ring*Mathf.PI/4,angle=side*Mathf.PI/4;return center+Vector3.Scale(new Vector3(Mathf.Sin(lat)*Mathf.Cos(angle),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(angle)),size)*.5f;}
                for(int ring=0;ring<4;ring++)for(int side=0;side<8;side++)Quad(V(ring,side),V(ring,side+1),V(ring+1,side+1),V(ring+1,side),material);
            }
            public void Prism(Vector3 center,Vector3 size,int material,int sides)
            {
                Vector3 V(int side,float y){float a=side*2*Mathf.PI/sides;return center+Vector3.Scale(new Vector3(Mathf.Cos(a),y,Mathf.Sin(a)),size)*.5f;}
                for(int i=0;i<sides;i++)
                {
                    Quad(V(i,-1),V(i,1),V(i+1,1),V(i+1,-1),material);
                    Quad(center+Vector3.up*size.y*.5f,V(i+1,1),V(i,1),center+Vector3.up*size.y*.5f,material);
                }
            }
            public void Tube(Vector3 center,float outer,float inner,float height,int material)
            {
                Vector3 V(int i,float r,float y){float a=i*Mathf.PI/4;return center+new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);}
                for(int i=0;i<8;i++)
                {
                    Quad(V(i,outer,0),V(i,outer,height),V(i+1,outer,height),V(i+1,outer,0),material);
                    Quad(V(i+1,inner,0),V(i+1,inner,height),V(i,inner,height),V(i,inner,0),1);
                    Quad(V(i,inner,height),V(i+1,inner,height),V(i+1,outer,height),V(i,outer,height),material);
                }
            }
            private void Polygon(float height,Vector2[] points,int material)
            {
                Vector3 center=Vector3.zero;foreach(var p in points)center+=new Vector3(p.x,height,p.y);center/=points.Length;
                for(int i=0;i<points.Length;i++)
                {
                    Vector3 a=new Vector3(points[i].x,height,points[i].y),b=new Vector3(points[(i+1)%points.Length].x,height,points[(i+1)%points.Length].y);
                    if(Vector3.Cross(a-center,b-center).y<0){var swap=a;a=b;b=swap;}
                    int n=vertices.Count;vertices.AddRange(new[]{center,a,b});triangles[material].AddRange(new[]{n,n+1,n+2});
                }
            }
            public void Badge(string role,float height)
            {
                const float z=-.25f,r=.21f;
                for(int layer=0;layer<2;layer++)
                {
                    var points=new Vector2[12];float radius=r-layer*.025f;
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;points[i]=new Vector2(Mathf.Cos(a)*radius,z+Mathf.Sin(a)*radius);}
                    Polygon(height+layer*.003f,points,layer==0?0:1);
                }
                float h=height+.008f;
                void Stroke(float ax,float ay,float bx,float by,float width=.035f)
                {
                    var a=new Vector2(ax,ay+z);var b=new Vector2(bx,by+z);var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
                    Polygon(h,new[]{a+normal,b+normal,b-normal,a-normal},2);
                }
                switch(role)
                {
                    case "hq":
                        Stroke(-.12f,-.09f,.12f,-.09f,.045f);
                        for(int i=-1;i<=1;i++)Stroke(i*.1f,-.08f,i*.1f,i==0?.12f:.07f,.045f);break;
                    case "barracks":
                        for(int i=0;i<2;i++){float y=i*.12f-.09f;Stroke(-.12f,y,0,y+.08f);Stroke(0,y+.08f,.12f,y);}break;
                    case "extractor":
                        Polygon(h,new[]{new Vector2(0,z+.14f),new Vector2(.1f,z-.03f),new Vector2(.07f,z-.1f),new Vector2(-.07f,z-.1f),new Vector2(-.1f,z-.03f)},2);break;
                    case "supply":
                        Stroke(-.13f,0,.13f,0,.055f);Stroke(0,-.13f,0,.13f,.055f);break;
                    case "tech":
                        Stroke(0,.14f,.12f,0);Stroke(.12f,0,0,-.14f);Stroke(0,-.14f,-.12f,0);Stroke(-.12f,0,0,.14f);
                        Stroke(-.05f,0,.05f,0);break;
                    default:
                        for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=a+Mathf.PI/4;Stroke(Mathf.Cos(a)*.095f,Mathf.Sin(a)*.095f,Mathf.Cos(b)*.095f,Mathf.Sin(b)*.095f,.025f);}
                        Stroke(-.15f,0,.15f,0,.025f);Stroke(0,-.15f,0,.15f,.025f);break;
                }
            }
            public Mesh Finish(string name,float scale)
            {
                for(int i=0;i<vertices.Count;i++)vertices[i]*=scale;
                var mesh=new Mesh{name="Building silhouette / "+name,subMeshCount=3};mesh.SetVertices(vertices);
                var colors=new Color[vertices.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;
                for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
