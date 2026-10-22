using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Authored blockers remain the only source of collision. Trees and cacti use
    // one static mesh and never extend into a playable cell or add colliders.
    public sealed class ThemedScenery : System.IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Vector4> shadowCasters=new List<Vector4>();
        public int Count { get; private set; }
        public IReadOnlyList<Vector4> ShadowCasters=>shadowCasters;

        public ThemedScenery(BoardView board,Texture visibility,GridMap grid)
        {
            bool forest=grid.Definition.theme=="forest";
            var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
            var occupied=new List<Vector2>();
            int spacing=forest?2:1;
            for(int z=1;z<grid.Height;z+=spacing)for(int x=1;x<grid.Width;x+=spacing)
            {
                if(grid.Walkable[grid.Index(x,z)])continue;
                int seed=(Mathf.Min(x,grid.Width-x)*73856093)^(Mathf.Min(z,grid.Height-z)*19349663);
                var random=new System.Random(seed&int.MaxValue);
                if(forest&&random.NextDouble()>.82)continue;
                float radius=SafeRadius(x,z,forest?1.55f:1.15f);
                if(radius<(forest?.65f:.58f))continue;
                if(!forest)
                {
                    // Sandstone occupies the center of each outcrop. Cacti live
                    // on its shoulders, outside the complete butte silhouette.
                    bool clear=true;
                    foreach(var rock in grid.Definition.decoration)
                    {
                        float rockRadius=SafeRadius(rock.x+.5f,rock.y+.5f,3.8f);
                        if(new Vector2(x-rock.x-.5f,z-rock.y-.5f).magnitude<rockRadius+radius+.35f){clear=false;break;}
                    }
                    foreach(var position in occupied)if(Vector2.Distance(position,new Vector2(x,z))<2.5f){clear=false;break;}
                    if(!clear)continue;
                    occupied.Add(new Vector2(x,z));
                }
                float y=board.HeightAt(new Point(x,z));
                if(forest)Tree(new Vector3(x,y,z),radius,random);
                else Cactus(new Vector3(x,y,z),radius,random);
                shadowCasters.Add(new Vector4(x,z,radius*(forest?1.05f:.8f),forest?.18f:.15f));
                Count++;
            }
            mesh=new Mesh{name=forest?"Forest Watch trees":"Desser Alert cacti",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            material=new Material(Shader.Find("Frontier/Board"));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_UseFog",1);material.SetTexture("_FogTex",visibility);
            root=new GameObject(forest?"Forest canopy":"Desert cacti",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(board.Content,false);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;

            float SafeRadius(float x,float z,float maximum)
            {
                float radius=maximum;
                for(int gy=Mathf.FloorToInt(z-maximum)-1;gy<=Mathf.CeilToInt(z+maximum)+1;gy++)
                    for(int gx=Mathf.FloorToInt(x-maximum)-1;gx<=Mathf.CeilToInt(x+maximum)+1;gx++)
                    {
                        if(grid.Inside(gx,gy)&&!grid.Walkable[grid.Index(gx,gy)])continue;
                        float dx=x-Mathf.Clamp(x,gx,gx+1),dz=z-Mathf.Clamp(z,gy,gy+1);
                        radius=Mathf.Min(radius,Mathf.Sqrt(dx*dx+dz*dz)-.12f);
                    }
                return Mathf.Max(0,radius);
            }
            void Tree(Vector3 origin,float radius,System.Random random)
            {
                float height=radius*(3.3f+(float)random.NextDouble()*.7f);
                float yaw=(float)random.NextDouble()*Mathf.PI*2;
                Color green=Color.Lerp(new Color(.065f,.22f,.13f),new Color(.29f,.43f,.17f),(float)random.NextDouble());
                Frustum(origin,origin+Vector3.up*(height*.72f),radius*.14f,radius*.07f,6,yaw,new Color(.27f,.16f,.09f));
                // Three overlapping faceted crowns make a pine silhouette, with
                // bright tips and darker undersides visible at tabletop scale.
                for(int tier=0;tier<3;tier++)
                {
                    float start=height*(.23f+tier*.21f),end=height*(.72f+tier*.14f);
                    float width=radius*(1-tier*.22f);
                    Frustum(origin+Vector3.up*start,origin+Vector3.up*end,width,0,7,yaw+tier*.17f,green*(.82f+tier*.15f));
                }
            }
            void Cactus(Vector3 origin,float radius,System.Random random)
            {
                float height=radius*(3.1f+(float)random.NextDouble()*.6f),stem=radius*.24f;
                float yaw=(float)random.NextDouble()*Mathf.PI*2;
                Color green=Color.Lerp(new Color(.16f,.32f,.15f),new Color(.35f,.47f,.2f),(float)random.NextDouble());
                Frustum(origin,origin+Vector3.up*(height-stem),stem,stem*.92f,8,yaw,green);
                Frustum(origin+Vector3.up*(height-stem),origin+Vector3.up*height,stem*.92f,0,8,yaw,green*1.15f);
                for(int arm=0;arm<2;arm++)
                {
                    Vector3 direction=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw))*(arm==0?1:-1);
                    float level=height*(arm==0?.43f:.59f);
                    var joint=origin+Vector3.up*level;
                    var elbow=joint+direction*(radius*.7f);
                    var tip=elbow+Vector3.up*(height*(arm==0?.33f:.24f));
                    Frustum(joint,elbow,stem*.7f,stem*.7f,6,yaw,green);
                    Frustum(elbow,tip,stem*.7f,stem*.65f,6,yaw,green);
                    Frustum(tip,tip+Vector3.up*stem*.7f,stem*.65f,0,6,yaw,green*1.12f);
                }
                // Thin vertical ribs are alternating faces, not extra geometry.
            }
            void Frustum(Vector3 from,Vector3 to,float lower,float upper,int sides,float yaw,Color tint)
            {
                Vector3 axis=(to-from).normalized;
                Vector3 tangent=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;
                Vector3 bitangent=Vector3.Cross(tangent,axis).normalized;
                for(int side=0;side<sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides+yaw,next=(side+1)*Mathf.PI*2/sides+yaw;
                    Vector3 a=tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle),b=tangent*Mathf.Cos(next)+bitangent*Mathf.Sin(next);
                    Color face=tint*(side%2==0?1:.87f);face.a=1;
                    Face(from+a*lower,to+a*upper,to+b*upper,face);
                    Face(from+a*lower,to+b*upper,from+b*lower,face);
                    if(upper>0)Face(to,to+b*upper,to+a*upper,tint*1.1f);
                    if(lower>0)Face(from,from+a*lower,from+b*lower,tint*.7f);
                }
            }
            void Face(Vector3 a,Vector3 b,Vector3 c,Color tint)
            {
                int start=vertices.Count;
                foreach(var point in new[]{a,b,c}){vertices.Add(point);colors.Add(tint);uv.Add(new Vector2(point.x/grid.Width,point.z/grid.Height));}
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
        }
        public void Dispose(){root.SetActive(false);Release(root);Release(mesh);Release(material);}
        private static void Release(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
