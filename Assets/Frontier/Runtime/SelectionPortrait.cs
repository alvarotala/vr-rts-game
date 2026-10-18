using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Cache an orthographic projection of the actual model as UI triangles. No camera,
    // render texture, per-frame rendering or duplicated portrait art on Quest.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SelectionPortrait : MaskableGraphic
    {
        private struct Face { public Vector3 a,b,c; public Color tint; public float depth; }
        private static readonly Dictionary<string,Face[]> Cache=new Dictionary<string,Face[]>();
        private Face[] faces;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()=>Cache.Clear();
        public void SetType(Archetype type)
        {
            if(!Cache.TryGetValue(type.id,out var projection))
            {
                var units=new UnitVisuals();var buildings=new BuildingVisuals();
                try
                {
                    var mesh=type.building?buildings.Get(type):units.Get(type);
                    var rotation=Quaternion.Euler(20,-35,0);
                    var vertices=mesh.vertices.Select(v=>rotation*v).ToArray();
                    var min=new Vector2(vertices.Min(v=>v.x),vertices.Min(v=>v.y));
                    var max=new Vector2(vertices.Max(v=>v.x),vertices.Max(v=>v.y));
                    float scale=1/Mathf.Max(max.x-min.x,max.y-min.y);var center=(min+max)*.5f;
                    var colors=new[]{type.faction=="biological"?new Color(.34f,.77f,.46f):new Color(.24f,.65f,.82f),type.faction=="biological"?Color.white:new Color(.2f,.28f,.34f),type.building?BuildingVisuals.Accent(type.role):UnitVisuals.Accent(type.role)};
                    var tissue=mesh.colors;
                    var result=new List<Face>();
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var triangles=mesh.GetTriangles(sub);
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                            var normal=Vector3.Cross(b-a,c-a).normalized;
                            float shade=.65f+.35f*Mathf.Abs(Vector3.Dot(normal,new Vector3(-.4f,.7f,.6f).normalized));
                            var tint=colors[Mathf.Min(sub,2)]*shade;
                            if(tissue.Length==vertices.Length)tint*=(tissue[triangles[i]]+tissue[triangles[i+1]]+tissue[triangles[i+2]])/3;
                            tint.a=1;
                            result.Add(new Face{a=Normalize(a),b=Normalize(b),c=Normalize(c),depth=(a.z+b.z+c.z)/3,tint=tint});
                        }
                    }
                    projection=result.OrderBy(f=>f.depth).ToArray();Cache.Add(type.id,projection);
                    Vector3 Normalize(Vector3 v)=>new Vector3((v.x-center.x)*scale,(v.y-center.y)*scale,0);
                }
                finally{units.Dispose();buildings.Dispose();}
            }
            if(faces==projection)return;faces=projection;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();if(faces==null)return;
            var rect=GetPixelAdjustedRect();float scale=Mathf.Min(rect.width,rect.height);
            foreach(var face in faces)
            {
                int start=helper.currentVertCount;
                Add(face.a,face.tint);Add(face.b,face.tint);Add(face.c,face.tint);helper.AddTriangle(start,start+1,start+2);
            }
            void Add(Vector3 vertex,Color tint)=>helper.AddVert(new Vector3(rect.center.x+vertex.x*scale,rect.center.y+vertex.y*scale),tint,Vector2.zero);
        }
    }
}
