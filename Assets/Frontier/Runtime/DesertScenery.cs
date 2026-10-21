using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Static sandstone details share one mesh/material. Every rock stays within
    // impassable terrain, so scenery never obstructs an otherwise usable route.
    public sealed class DesertScenery : System.IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;

        public DesertScenery(BoardView board,Texture visibility,GridMap grid)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(var decoration in grid.Definition.decoration)
            {
                float x=decoration.x+.5f,z=decoration.y+.5f;
                var random=new System.Random(decoration.x*73856093^decoration.y*19349663);
                float radius=SafeRadius(x,z,3.8f);
                float height=Mathf.Min(8,radius*(1.65f+(float)random.NextDouble()*.6f));
                Butte(x,z,radius,height,decoration.rotation,random);
                for(int chip=0;chip<5;chip++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2;
                    float distance=radius*(.8f+(float)random.NextDouble()*.45f);
                    float cx=x+Mathf.Cos(angle)*distance,cz=z+Mathf.Sin(angle)*distance;
                    int cell=grid.Cell(new Point(cx,cz));if(cell<0||grid.Walkable[cell])continue;
                    float size=SafeRadius(cx,cz,.35f+(float)random.NextDouble()*.4f);
                    if(size>.18f)Butte(cx,cz,size,size*1.2f,(float)random.NextDouble()*360,random);
                }
            }
            mesh=new Mesh{name="Desser Alert sandstone",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            material=new Material(Shader.Find("Frontier/Board"));material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_UseFog",1);material.SetTexture("_FogTex",visibility);
            root=new GameObject("Sandstone outcrops",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(board.Content,false);
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
            void Butte(float x,float z,float radius,float height,float yaw,System.Random random)
            {
                if(radius<.12f)return;
                const int sides=7,layers=5;
                var rings=new Vector3[layers+1,sides];
                var stretch=new float[sides];for(int side=0;side<sides;side++)stretch[side]=.77f+(float)random.NextDouble()*.23f;
                float ground=board.HeightAt(new Point(x,z));
                for(int layer=0;layer<=layers;layer++)
                {
                    float t=layer/(float)layers;
                    float taper=layer==0?1:layer==1?.94f:layer==2?.75f:layer==3?.8f:layer==4?.58f:.5f;
                    for(int side=0;side<sides;side++)
                    {
                        float angle=side*Mathf.PI*2/sides+yaw*Mathf.Deg2Rad;
                        rings[layer,side]=new Vector3(x+Mathf.Cos(angle)*radius*taper*stretch[side],ground+height*t,z+Mathf.Sin(angle)*radius*taper*stretch[side]);
                    }
                }
                for(int layer=0;layer<layers;layer++)for(int side=0;side<sides;side++)
                {
                    int next=(side+1)%sides;
                    var tint=layer%2==0?new Color(.57f,.29f,.13f):new Color(.79f,.48f,.24f);
                    tint*=.88f+(float)random.NextDouble()*.2f;tint.a=1;
                    Face(rings[layer,side],rings[layer+1,side],rings[layer+1,next],tint);
                    Face(rings[layer,side],rings[layer+1,next],rings[layer,next],tint);
                }
                var cap=new Vector3(x,ground+height,z);
                for(int side=0;side<sides;side++)Face(cap,rings[layers,(side+1)%sides],rings[layers,side],new Color(.86f,.62f,.35f));
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
