using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // All shots share one mesh/material. No objects, materials or particle systems are allocated per shot.
    public sealed class WeaponEffects : System.IDisposable
    {
        public const int MaxShots=192;
        public int ActiveCount {get;private set;}
        private readonly Mesh mesh=new Mesh {name="Weapon particles",indexFormat=IndexFormat.UInt32};
        private readonly Material material=new Material(Shader.Find("Frontier/Weapons"));
        private readonly GameObject root;
        private readonly List<Vector3> vertices=new List<Vector3>(MaxShots*160);
        private readonly List<Vector3> uv=new List<Vector3>(MaxShots*160);
        private readonly List<Color> colors=new List<Color>(MaxShots*160);
        private readonly List<int> triangles=new List<int>(MaxShots*240);
        private Vector3 right,up,eye;
        private float clock;
        private int lastTick=-1;

        public WeaponEffects(Transform parent,Texture fog,int width,int height)
        {
            root=new GameObject("Weapon effects",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(parent,false);
            mesh.MarkDynamic();root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            material.SetTexture("_FogTex",fog);material.SetVector("_MapSize",new Vector4(width,height,0,0));
        }
        public void Update(BoardView board)
        {
            Render(board,AdvanceClock(board),board.Viewer!=null?board.Viewer:Camera.main);
        }
        public float AdvanceClock(BoardView board)
        {
            var session=board.Session;
            if(session.View==null||!board.Content.gameObject.activeInHierarchy){Clear();return 0;}
            if(lastTick!=session.View.tick){lastTick=session.View.tick;clock=lastTick;}
            else if(!session.IsPaused||session.IsOnline)
                clock=Mathf.Min(clock+Time.unscaledDeltaTime*Simulation.TickRate,lastTick+(session.View.winner>=0?Simulation.TickRate:1));
            return clock;
        }
        public void Render(BoardView board,float visualTick,Camera camera)
        {
            vertices.Clear();uv.Clear();colors.Clear();triangles.Clear();ActiveCount=0;
            right=camera!=null?board.Content.InverseTransformDirection(camera.transform.right).normalized:Vector3.right;
            up=camera!=null?board.Content.InverseTransformDirection(camera.transform.up).normalized:new Vector3(0,.707f,.707f);
            eye=Vector3.Cross(right,up).normalized;
            var view=board.Session.View;
            // Prefer newest shots if the visual budget is exhausted; this never changes damage.
            if(view!=null)for(int i=view.attacks.Length-1;i>=0&&ActiveCount<MaxShots;i--)
            {
                var shot=view.attacks[i];float age=(visualTick-shot.tick)/Simulation.TickRate;
                var type=board.Session.Catalog[shot.definition];bool bio=type.faction=="biological",siege=type.role=="siege";
                bool melee=type.role=="worker"||type.role=="melee";
                float flight=melee?.06f:siege?.5f:bio?.32f:.2f,lifetime=flight+(siege?.3f:.18f);
                if(age<0||age>=lifetime||!Visible(view,board.Session.Map,shot.origin)||!Visible(view,board.Session.Map,shot.destination))continue;
                ActiveCount++;
                var targetType=board.Session.Catalog[shot.targetDefinition];
                var direction=new Vector3(shot.destination.x-shot.origin.x,0,shot.destination.y-shot.origin.y).normalized;
                var side=Vector3.Cross(Vector3.up,direction);
                float scale=type.building?1:UnitVisuals.ModelScale(type);
                float muzzleHeight=type.building?2.45f:(type.role=="ranged"?.74f:siege?.55f:melee?.8f:.5f)*scale;
                float muzzleForward=type.building?type.footprint*.46f:(type.role=="ranged"||siege?.7f:.4f)*scale;
                Vector3 origin=new Vector3(shot.origin.x,board.HeightAt(shot.origin)+muzzleHeight,shot.origin.y)+direction*muzzleForward;
                if(type.role=="ranged")origin+=side*(.17f*scale);
                if(!melee&&board.TryWeaponMuzzle(shot.attacker,shot.id,out var muzzle)&&new Vector2(muzzle.x-shot.origin.x,muzzle.z-shot.origin.y).magnitude<2*scale)
                    origin=muzzle;
                Vector3 targetCenter=new Vector3(shot.destination.x,board.HeightAt(shot.destination),shot.destination.y);
                if(!melee&&board.TryCombatCenter(shot.target,out var liveTarget))targetCenter=liveTarget;
                float targetRadius=targetType.building?targetType.footprint*.4f:UnitVisuals.ModelScale(targetType)*.45f*.7f;
                Vector3 end=targetCenter+Vector3.up*(targetType.building?1.2f:.7f*UnitVisuals.ModelScale(targetType))-direction*targetRadius;
                // Keep traces legible on a full table, without growing excessively when zoomed in.
                float width=Mathf.Clamp(.0015f/board.CellScale,.06f,.18f)*(siege?1.6f:1);
                Color team=shot.owner==view.player?new Color(.25f,.85f,1):new Color(1,.32f,.22f);
                Color tint=Color.Lerp(bio?new Color(.55f,1,.12f):new Color(1,.68f,.18f),team,.55f);
                Color core=bio?new Color(.9f,1,.65f):new Color(1,.97f,.8f);
                float arc=siege?Mathf.Min(3,Vector3.Distance(origin,end)*.28f):bio&&!melee?.45f:0;
                Vector3 Path(float t)=>Vector3.Lerp(origin,end,t)+Vector3.up*(4*t*(1-t)*arc);

                bool inspected=board.Session.Selection.Contains(shot.attacker)||board.Hovered==shot.attacker;
                float markerFade=Mathf.Clamp01((lifetime-age)/.15f);
                if(age<.2f)Ring(new Vector3(shot.origin.x,board.HeightAt(shot.origin)+.07f,shot.origin.y),type.building?type.footprint*.55f:type.collisionRadius+.12f,width*.6f,Tint(team,(1-age/.2f)*.65f));
                Ring(targetCenter+Vector3.up*.09f,targetType.building?targetType.footprint*.6f:targetType.collisionRadius+.3f,width*(inspected?1.1f:.65f),Tint(team,markerFade*(inspected?.95f:.65f)));

                if(!melee&&age<.12f)
                {
                    float burst=1-age/.12f;
                    Particle(origin,width*(melee?5:7)*(.5f+burst*.5f),Tint(tint,burst*.8f));
                    Particle(origin,width*3, Tint(core,burst));
                    Beam(origin-side*width*3,origin+side*width*3,width*.75f,Tint(core,burst),Tint(tint,0));
                }
                if(melee)
                {
                    // A short crescent at arm's reach, never a ranged beam. Body
                    // lunge and weapon sweep share this strike's authoritative tick.
                    float t=Mathf.Clamp01(age/.14f),fade=1-Mathf.Clamp01(age/lifetime);
                    var pivot=new Vector3(shot.origin.x,board.HeightAt(shot.origin)+.8f*scale,shot.origin.y);
                    float reach=Mathf.Min(1.35f*scale,Vector3.Distance(pivot,end));
                    float head=Mathf.Lerp(-65,65,t)*Mathf.Deg2Rad;
                    for(int segment=0;segment<8;segment++)
                    {
                        float a=head-.85f+segment*.85f/8,b=head-.85f+(segment+1)*.85f/8;
                        Vector3 Swing(float angle)=>pivot+(direction*Mathf.Cos(angle)+side*Mathf.Sin(angle))*reach;
                        Beam(Swing(a),Swing(b),width*1.3f,Tint(tint,fade*segment/8),Tint(core,fade*(segment+1)/8));
                    }
                }
                else
                {
                    // Dim lingering path connects source and target; the bright head/tail carries direction.
                    float fade=Mathf.Clamp01((lifetime-age)/.22f);
                    int segments=arc>0?12:1;
                    float head=Mathf.Clamp01(age/flight);
                    for(int segment=0;segment<segments;segment++)
                    {
                        float a=segment/(float)segments,b=(segment+1f)/segments;
                        Beam(Path(a),Path(b),width*(inspected?.8f:.5f),Tint(team,(inspected?.5f:.2f)*fade),Tint(team,(inspected?.5f:.2f)*fade));
                    }
                    if(age<flight)
                    {
                        float tail=Mathf.Max(0,head-(siege?.25f:bio?.3f:.45f));
                        for(int segment=0;segment<5;segment++)
                        {
                            float a=segment/5f,b=(segment+1)/5f;
                            Beam(Path(Mathf.Lerp(tail,head,a)),Path(Mathf.Lerp(tail,head,b)),width*(bio?2:1),Tint(Color.Lerp(tint,core,a),a),Tint(Color.Lerp(tint,core,b),b));
                        }
                        Particle(Path(head),width*(bio?4:2.8f),core);
                    }
                }
                if(age>=flight)
                {
                    float progress=(age-flight)/(lifetime-flight),fade=1-progress;
                    Particle(end,width*(siege?12:5)*(1+progress),Tint(tint,fade*.65f));
                    Particle(end,width*2.5f,Tint(core,fade));
                    for(int spark=0;spark<(siege?7:4);spark++)
                    {
                        float angle=spark*2.39996f+shot.id;
                        var spread=new Vector3(Mathf.Cos(angle),.3f+(spark%3)*.25f,Mathf.Sin(angle));
                        float radius=(siege?1.5f:.65f)*progress;
                        Beam(end+spread*radius*.5f,end+spread*radius,width*.65f,Tint(tint,fade*.2f),Tint(core,fade));
                    }
                }
            }
            mesh.Clear();
            if(vertices.Count>0){mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
        }
        private static bool Visible(PlayerView view,MapDefinition map,Point p)
        {int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y);return x>=0&&y>=0&&x<map.width&&y<map.height&&view.visibility[y*map.width+x]==2;}
        private static Color Tint(Color color,float alpha){color.a=alpha;return color;}
        private void Ring(Vector3 center,float radius,float width,Color color)
        {
            // Four separated brackets stay readable around the victim's feet.
            for(int quadrant=0;quadrant<4;quadrant++)for(int segment=0;segment<4;segment++)
            {
                float a=(quadrant*90+segment*12)*Mathf.Deg2Rad,b=a+12*Mathf.Deg2Rad;
                Beam(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius,width,color,color);
            }
        }
        private void Particle(Vector3 center,float size,Color color)
        {Quad(center-right*size*.5f-up*size*.5f,center-right*size*.5f+up*size*.5f,center+right*size*.5f+up*size*.5f,center+right*size*.5f-up*size*.5f,color,color,1);}
        private void Beam(Vector3 start,Vector3 end,float width,Color from,Color to)
        {
            if((end-start).sqrMagnitude<.00001f)return;
            var side=Vector3.Cross(end-start,eye).normalized*width*.5f;
            Quad(start-side,start+side,end+side,end-side,from,to,0);
        }
        private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color from,Color to,float round)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uv.Add(new Vector3(0,0,round));uv.Add(new Vector3(0,1,round));uv.Add(new Vector3(1,1,round));uv.Add(new Vector3(1,0,round));
            colors.Add(from);colors.Add(from);colors.Add(to);colors.Add(to);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        public void Clear(){mesh.Clear();ActiveCount=0;lastTick=-1;clock=0;}
        public void Dispose(){if(root!=null)root.SetActive(false);Release(root);Release(mesh);Release(material);}
        private static void Release(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
