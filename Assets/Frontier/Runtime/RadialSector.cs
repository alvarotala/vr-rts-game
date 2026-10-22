using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // The visual rim stays inside the original sector. Input continues to use
    // ActionWheel's angle/radius contract; this mesh is decorative only.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadialSector : MaskableGraphic
    {
        public float Angle, Sweep = 90, InnerRadius = 142, OuterRadius = 290;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float outer=Mathf.Max(0,OuterRadius),inner=Mathf.Clamp(InnerRadius,0,outer),width=outer-inner;
            float sweep=Mathf.Clamp(Sweep,0,360);
            if(width<=.001f||sweep<=.001f||color.a<=0)return;
            bool disc=inner<=.001f,closed=sweep>=359.999f;
            float feather=Mathf.Min(.7f,width*.07f),rim=Mathf.Min(1.25f,width*.12f),bevel=Mathf.Min(2.5f,width*.18f);
            float[] radii=disc?new[]{0,outer-feather-rim-bevel,outer-feather-rim,outer-feather,outer}
                :new[]{inner,inner+feather,inner+feather+rim,inner+feather+rim+bevel,outer-feather-rim-bevel,outer-feather-rim,outer-feather,outer};
            int steps=Mathf.Max(1,Mathf.CeilToInt(sweep/3)),rows=radii.Length;
            for(int i=0;i<=steps;i++)
            {
                float angle=(Angle-sweep*.5f+sweep*i/steps)*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                for(int j=0;j<rows;j++)
                {
                    var point=direction*radii[j];var tint=Body(point,radii[j]);
                    int edge=rows-1-j;
                    if(edge==0){tint=Rim(point);tint.a=0;}
                    else if(edge==1)tint=Dark(point);
                    else if(edge==2)tint=Rim(point);
                    if(!disc)
                    {
                        if(j==0){tint=Dark(point);tint.a=0;}
                        else if(j==1)tint=Dark(point);
                        else if(j==2)tint=Color.Lerp(Body(point,radii[j]),Rim(point),.35f);
                    }
                    vh.AddVert(point,tint,Vector2.zero);
                    if(i==0||j==0)continue;
                    int n=i*rows+j;vh.AddTriangle(n-rows-1,n-rows,n-1);vh.AddTriangle(n-1,n-rows,n);
                }
            }
            if(!closed)
            {
                Side(Angle-sweep*.5f,1);Side(Angle+sweep*.5f,-1);
            }
            Color Body(Vector2 point,float radius)
            {
                float vertical=Mathf.InverseLerp(-outer,outer,point.y);
                float radial=disc?1-Mathf.Clamp01(radius/outer)*.07f:Mathf.Lerp(.86f,1.02f,Mathf.InverseLerp(inner,outer,radius));
                float light=Mathf.Lerp(.84f,1.1f,vertical)*radial;
                return new Color(Mathf.Min(1,color.r*light),Mathf.Min(1,color.g*light),Mathf.Min(1,color.b*light),color.a);
            }
            Color Rim(Vector2 point)
            {
                float light=Mathf.InverseLerp(-outer,outer,point.y);
                var tint=Color.Lerp(color,MenuPalette.Highlight,Mathf.Lerp(.25f,.58f,light));tint.a=color.a;return tint;
            }
            Color Dark(Vector2 point)
            {var tint=Color.Lerp(Body(point,point.magnitude),MenuPalette.Shadow,.65f);tint.a=color.a;return tint;}
            void Side(float degrees,float direction)
            {
                // A narrow bevel along each radial edge, without expanding the
                // sector or bridging the intentional gaps between choices.
                float angle=degrees*Mathf.Deg2Rad;
                var start=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                float inset=Mathf.Min(.7f,sweep*.1f)*direction*Mathf.Deg2Rad;
                var end=new Vector2(Mathf.Sin(angle+inset),Mathf.Cos(angle+inset));
                Vector2 a=start*(inner+feather),b=start*(outer-feather),c=end*(outer-feather),d=end*(inner+feather);
                int n=vh.currentVertCount;
                vh.AddVert(a,Dark(a),Vector2.zero);vh.AddVert(b,Dark(b),Vector2.zero);
                vh.AddVert(c,Color.Lerp(Body(c,c.magnitude),Rim(c),.44f),Vector2.zero);vh.AddVert(d,Color.Lerp(Body(d,d.magnitude),Rim(d),.44f),Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
