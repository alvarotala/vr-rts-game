using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // Shared, low-poly meshes: team hull, dark structure, and role accent in three submeshes.
    public sealed class UnitVisuals
    {
        private readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        // Hammer's narrow chassis needs a larger multiplier to match Bombardier's
        // broad body/legs. Keep rigs, muzzles and remains on the same visual scale.
        public static float ModelScale(Archetype type)
            => type.collisionRadius / .45f * (type.role == "siege" ? type.faction == "industrial" ? 1.6f : .9f : 1f);
        public static string RoleName(string role)
        {
            switch(role)
            {
                case "worker":return "Worker";
                case "scout":return "Scout";
                case "melee":return "Melee";
                case "ranged":return "Ranged";
                case "siege":return "Artillery";
                case "caster":return "Caster";
                default:return role;
            }
        }
        public static Color Accent(string role)
        {
            switch(role)
            {
                case "worker":return new Color(1,.76f,.18f);
                case "scout":return new Color(.45f,1,.65f);
                case "melee":return new Color(1,.86f,.65f);
                case "ranged":return new Color(.9f,.96f,1);
                case "caster":return new Color(.76f,.38f,1);
                default:return new Color(.84f,.55f,1);
            }
        }
        public Mesh Get(Archetype type, bool animatedWorker = false, bool animatedCombat = false, bool remains = false)
        {
            string key=type.id+(remains?"/remains":animatedWorker?"/animated":animatedCombat?"/combat":"");
            if(meshes.TryGetValue(key,out var mesh))return mesh;
            if(type.faction=="biological")
            {
                var badge=new Builder();if(!animatedWorker&&!remains)badge.Badge(type.role,BiologicalVisuals.BadgeHeight(type.role));
                mesh=BiologicalVisuals.WithBadge(BiologicalVisuals.Unit(type,animatedWorker,animatedCombat),badge.Finish(key+"/badge",1),ModelScale(type));
                meshes.Add(key,mesh);return mesh;
            }
            var b=new Builder();bool bio=type.faction=="biological";
            void Part(float x,float y,float z,float sx,float sy,float sz,int material=0)
            {if(bio)b.Lobe(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);else b.Box(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);}
            float height;
            switch(type.role)
            {
                case "worker": // Low cargo carrier with two gathering claws; no gun.
                    Part(0,.3f,-.04f,.56f,.4f,.52f);
                    Part(0,.62f,-.07f,.44f,.3f,.38f,2);
                    if(!animatedWorker)
                    {Part(-.2f,.16f,.25f,.1f,.14f,.26f,1);Part(.2f,.16f,.25f,.1f,.14f,.26f,1);}
                    height=.85f;break;
                case "scout": // Narrow, low silhouette with lateral fins and a sensor mast.
                    Part(0,.25f,0,.26f,.28f,.62f);
                    Part(0,.19f,-.08f,.7f,.09f,.26f,2);
                    Part(0,.48f,-.18f,.06f,.3f,.06f,1);
                    Part(0,.65f,-.18f,.16f,.12f,.16f,2);
                    height=.78f;break;
                case "melee": // Tall shield bearer, broad shoulders and two separate legs.
                    if(!animatedCombat){Part(-.15f,.24f,0,.18f,.48f,.25f,1);Part(.15f,.24f,0,.18f,.48f,.25f,1);}
                    Part(0,.82f,0,.5f,.66f,.34f);
                    Part(0,1.04f,0,.7f,.22f,.28f);
                    Part(0,1.32f,0,.26f,.26f,.26f);
                    Part(0,.8f,.24f,.44f,.6f,.12f,2);
                    height=1.5f;break;
                case "ranged": // Slim infantry with a single long rifle/spitter.
                    if(!animatedCombat){Part(-.12f,.22f,0,.14f,.44f,.2f,1);Part(.12f,.22f,0,.14f,.44f,.2f,1);}
                    Part(0,.7f,0,.32f,.56f,.28f);
                    Part(0,1.12f,-.04f,.25f,.26f,.25f);
                    if(!animatedCombat){Part(.17f,.74f,.27f,.13f,.15f,.8f,1);Part(.17f,.74f,.65f,.16f,.17f,.14f,2);}
                    height=1.32f;break;
                case "caster": // Hovering conduit: split mantle and an open crown, with no rifle or tracks.
                    Part(0,.42f,-.08f,.32f,.48f,.3f,1);
                    Part(-.2f,.52f,-.03f,.18f,.62f,.37f);Part(.2f,.52f,-.03f,.18f,.62f,.37f);
                    Part(0,.87f,-.06f,.36f,.34f,.3f);Part(0,1.14f,-.04f,.22f,.22f,.24f,1);
                    Part(0,1.16f,.09f,.16f,.08f,.04f,2);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Part(side*.28f,1.02f,-.04f,.13f,.25f,.25f);
                        Part(side*.28f,1.3f,-.04f,.075f,.33f,.1f,2);
                        Part(side*.2f,.82f,.2f,.12f,.12f,.38f);
                        Part(side*.2f,.82f,.4f,.14f,.13f,.12f,1);
                    }
                    if(!animatedCombat)b.Lobe(new Vector3(0,.82f,.45f),new Vector3(.27f,.27f,.27f),2);
                    height=1.5f;break;
                default: // Low artillery platform with paired barrels/spines.
                    Part(0,.22f,-.04f,.64f,.3f,.58f);
                    Part(-.28f,.15f,0,.14f,.24f,.65f,1);Part(.28f,.15f,0,.14f,.24f,.65f,1);
                    if(!animatedCombat)
                    {
                        Part(0,.5f,-.06f,.46f,.3f,.36f);
                        Part(-.15f,.55f,.3f,.12f,.15f,.88f,1);Part(.15f,.55f,.3f,.12f,.15f,.88f,1);
                        Part(-.15f,.55f,.7f,.17f,.18f,.14f,2);Part(.15f,.55f,.7f,.17f,.18f,.14f,2);
                    }
                    height=.88f;break;
            }
            // The chassis should remain visible from above. Keep its role
            // marker the same size as Bombardier's, just above the turret.
            if(!animatedWorker&&!remains)b.Badge(type.role,type.role=="siege"?.78f:height+.12f,type.role=="siege"?.9f/1.6f:1);
            mesh=b.Finish(key,ModelScale(type));meshes.Add(key,mesh);return mesh;
        }
        public Mesh CombatPart(Archetype type, bool leg)
        {
            string key=type.id+(leg?"/leg":"/weapon");if(meshes.TryGetValue(key,out var mesh))return mesh;
            if(type.faction=="biological"){mesh=BiologicalVisuals.CombatPart(type,leg);meshes.Add(key,mesh);return mesh;}
            var b=new Builder();
            void Part(float x,float y,float z,float sx,float sy,float sz,int material=1)
            {if(type.faction=="biological")b.Lobe(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);else b.Box(new Vector3(x,y,z),new Vector3(sx,sy,sz),material);}
            if(leg)
            {
                float length=type.role=="melee"?.48f:.44f;
                Part(0,-length*.5f,0,.17f,length,.22f);Part(0,-length+.07f,.08f,.2f,.14f,.32f,0);
            }
            else switch(type.role)
            {
                case "melee":
                    Part(.04f,-.12f,.1f,.18f,.28f,.28f,0);Part(.07f,-.22f,.3f,.13f,.12f,.42f);
                    if(type.faction=="biological")
                    {Part(-.04f,-.19f,.62f,.1f,.12f,.55f,2);Part(.17f,-.19f,.6f,.1f,.12f,.5f,2);}
                    else
                    {Part(.07f,-.22f,.66f,.14f,.08f,.64f,2);Part(.07f,-.22f,.41f,.36f,.12f,.08f);}
                    break;
                case "ranged":
                    Part(0,0,.12f,.13f,.15f,.8f);Part(0,0,.5f,.16f,.17f,.14f,2);Part(-.03f,-.08f,-.04f,.2f,.16f,.25f,0);break;
                case "siege":
                    Part(0,0,0,.46f,.3f,.36f,0);
                    for(int side=-1;side<=1;side+=2){Part(side*.15f,.05f,.36f,.12f,.15f,.88f);Part(side*.15f,.05f,.76f,.17f,.18f,.14f,2);}break;
                case "caster":
                    b.Lobe(new Vector3(0,0,.21f),new Vector3(.27f,.27f,.27f),2);break;
                default: Part(0,0,0,.14f,.12f,.4f);Part(0,0,.2f,.16f,.14f,.12f,2);break;
            }
            mesh=b.Finish(key,ModelScale(type));meshes.Add(key,mesh);return mesh;
        }
        public void Dispose()
        {
            foreach(var mesh in meshes.Values)
                if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
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
                {
                    float latitude=ring*Mathf.PI/4,angle=side*Mathf.PI/4;
                    return center+Vector3.Scale(new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle)),size)*.5f;
                }
                for(int ring=0;ring<4;ring++)for(int side=0;side<8;side++)
                    Quad(V(ring,side),V(ring,side+1),V(ring+1,side+1),V(ring+1,side),material);
            }
            private void Polygon(float height,Vector2[] points,int material)
            {
                Vector3 center=Vector3.zero;foreach(var p in points)center+=new Vector3(p.x,height,p.y);center/=points.Length;
                for(int i=0;i<points.Length;i++)
                {
                    var a=new Vector3(points[i].x,height,points[i].y);var q=points[(i+1)%points.Length];var b=new Vector3(q.x,height,q.y);
                    if(Vector3.Cross(a-center,b-center).y<0){var swap=a;a=b;b=swap;}
                    int n=vertices.Count;vertices.AddRange(new[]{center,a,b});triangles[material].AddRange(new[]{n,n+1,n+2});
                }
            }
            private void Disc(float height,float radius,int material)
            {
                var points=new Vector2[16];for(int i=0;i<points.Length;i++){float angle=i*2*Mathf.PI/points.Length;points[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
                Polygon(height,points,material);
            }
            private void Stroke(float height,Vector2 a,Vector2 b,float width)
            {
                var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;
                Polygon(height,new[]{a+n,b+n,b-n,a-n},2);
            }
            public void Badge(string role,float h,float size=1)
            {
                int start=vertices.Count;
                Disc(h,.62f,0);Disc(h+.008f,.53f,1);h+=.016f;
                switch(role)
                {
                    case "worker":
                        Stroke(h,new Vector2(-.2f,-.3f),new Vector2(.15f,.22f),.12f);
                        Stroke(h,new Vector2(-.16f,.29f),new Vector2(.33f,.04f),.18f);break;
                    case "scout":
                        for(int i=0;i<2;i++){float z=i*.3f-.25f;Stroke(h,new Vector2(-.28f,z),new Vector2(0,z+.22f),.12f);Stroke(h,new Vector2(0,z+.22f),new Vector2(.28f,z),.12f);}break;
                    case "melee":
                        Polygon(h,new[]{new Vector2(-.3f,.3f),new Vector2(.3f,.3f),new Vector2(.27f,-.05f),new Vector2(0,-.36f),new Vector2(-.27f,-.05f)},2);break;
                    case "ranged":
                        Stroke(h,new Vector2(-.37f,0),new Vector2(.37f,0),.08f);Stroke(h,new Vector2(0,-.37f),new Vector2(0,.37f),.08f);
                        for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=a+Mathf.PI/6;Stroke(h,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.24f,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.24f,.07f);}break;
                    case "caster":
                        Polygon(h,new[]{new Vector2(0,.35f),new Vector2(.1f,.1f),new Vector2(.32f,0),new Vector2(.1f,-.1f),new Vector2(0,-.35f),new Vector2(-.1f,-.1f),new Vector2(-.32f,0),new Vector2(-.1f,.1f)},2);
                        Stroke(h,new Vector2(-.31f,.23f),new Vector2(-.2f,.32f),.07f);Stroke(h,new Vector2(.2f,-.32f),new Vector2(.31f,-.23f),.07f);break;
                    default:
                        for(int i=-1;i<=1;i++){float x=i*.25f;Polygon(h,new[]{new Vector2(x-.07f,-.27f),new Vector2(x+.07f,-.27f),new Vector2(x+.07f,.16f),new Vector2(x,.32f),new Vector2(x-.07f,.16f)},2);}break;
                }
                // Stay inside the collision footprint, so crowded workers' badges never overlap.
                for(int i=start;i<vertices.Count;i++){var v=vertices[i];vertices[i]=new Vector3(v.x*.7f*size,v.y,v.z*.7f*size);}
            }
            public Mesh Finish(string name,float scale)
            {
                for(int i=0;i<vertices.Count;i++)vertices[i]*=scale;
                var mesh=new Mesh{name="Unit silhouette / "+name,subMeshCount=3};mesh.SetVertices(vertices);
                var colors=new Color[vertices.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;
                for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
