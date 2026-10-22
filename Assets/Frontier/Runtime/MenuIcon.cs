using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A coherent, optically sized set of vector UI symbols. The transparent edge
    // fringe avoids relying on texture filtering or a particular MSAA setting.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuIcon : MaskableGraphic
    {
        public enum Symbol
        {
            Play, Network, Book, Settings, History, Pause, Back, Home, Close,
            Chevron, Grip, Reset, Minerals, Gas, Population, Orders, Build, Army,
            Language, Audio, Visuals, Controls, Board, Check
        }
        [SerializeField] private Symbol symbol;
        public Symbol Icon { get => symbol; set { if (symbol == value) return; symbol = value; SetVerticesDirty(); } }
        protected override void Awake() { base.Awake(); raycastTarget = false; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = rectTransform.rect;
            float scale = Mathf.Min(rect.width, rect.height);
            if (scale <= 0 || color.a <= 0) return;
            Vector2 origin = rect.center - Vector2.one * (scale * .5f);
            float feather = Mathf.Min(.8f / scale, .035f);
            const float weight = .075f;
            switch (symbol)
            {
                case Symbol.Play:
                    Polygon(V(.23f, .12f), V(.23f, .88f), V(.86f, .5f)); break;
                case Symbol.Network:
                    Line(.5f, .62f, .5f, .43f); Line(.17f, .43f, .83f, .43f);
                    foreach (float x in new[] { .17f, .5f, .83f })
                    { Line(x, .43f, x, .34f); Disc(V(x, .26f), .079f); RoundedBox(x - .105f, .065f, x + .105f, .165f, .025f); }
                    Disc(V(.5f, .84f), .091f); RoundedBox(.38f, .61f, .62f, .73f, .028f);
                    break;
                case Symbol.Book:
                    Stroke(new[] { V(.12f, .2f), V(.12f, .82f), V(.29f, .865f), V(.5f, .8f), V(.71f, .865f), V(.88f, .82f), V(.88f, .2f), V(.71f, .245f), V(.5f, .18f), V(.29f, .245f), V(.12f, .2f) }, .072f);
                    Line(.5f, .18f, .5f, .8f, .065f); break;
                case Symbol.Settings:
                    const int teeth = 64; var outside = new Vector2[teeth]; var inside = new Vector2[teeth];
                    for (int i = 0; i < teeth; i++)
                    { float radius = i % 8 < 2 || i % 8 > 5 ? .35f : .44f; outside[i] = Polar(V(.5f,.5f), radius, i * 360f / teeth); inside[i] = Polar(V(.5f,.5f), .195f, i * 360f / teeth); }
                    Ring(outside, inside); break;
                case Symbol.History:
                    RoundedBox(.13f, .12f, .32f, .47f, .012f); RoundedBox(.405f, .12f, .595f, .88f, .012f); RoundedBox(.68f, .12f, .87f, .68f, .012f); break;
                case Symbol.Pause: RoundedBox(.22f, .16f, .4f, .84f, .022f); RoundedBox(.6f, .16f, .78f, .84f, .022f); break;
                case Symbol.Back: Line(.17f, .5f, .82f, .5f, .09f); Stroke(new[] { V(.45f,.79f), V(.16f,.5f), V(.45f,.21f) }, .09f); break;
                case Symbol.Chevron: Stroke(new[] { V(.36f,.78f), V(.64f,.5f), V(.36f,.22f) }, .095f); break;
                case Symbol.Home:
                    Polygon(V(.08f, .5f), V(.5f, .9f), V(.92f, .5f));
                    Box(.21f, .12f, .42f, .55f); Box(.58f, .12f, .79f, .55f); Box(.42f, .38f, .58f, .55f); break;
                case Symbol.Close: Line(.23f, .23f, .77f, .77f, .09f); Line(.23f, .77f, .77f, .23f, .09f); break;
                case Symbol.Grip:
                    foreach(float x in new[]{.35f,.65f}) foreach(float y in new[]{.23f,.5f,.77f}) Disc(V(x,y),.06f);
                    break;
                case Symbol.Reset:
                    Arc(V(.5f,.5f), .32f, 58, 335, .078f); Polygon(V(.71f,.88f),V(.68f,.59f),V(.43f,.77f)); break;
                case Symbol.Minerals:
                    Crystal(.44f,.09f,.22f,.87f); Crystal(.16f,.16f,.16f,.54f); Crystal(.72f,.12f,.15f,.62f); break;
                case Symbol.Gas:
                    Disc(V(.37f,.34f),.21f); Disc(V(.64f,.37f),.22f); Disc(V(.5f,.59f),.23f);
                    Arc(V(.35f,.85f), .1f, 190, 310, .06f); Arc(V(.67f,.84f), .11f, 185, 300, .06f); break;
                case Symbol.Population:
                    Person(.22f,.37f,.72f); Person(.78f,.37f,.72f); Person(.5f,.39f,1); break;
                case Symbol.Orders:
                    Disc(V(.19f,.22f), .1f); Stroke(new[]{V(.19f,.34f),V(.19f,.65f),V(.75f,.65f)}, .08f);
                    Stroke(new[]{V(.57f,.84f),V(.77f,.65f),V(.57f,.46f)}, .085f); break;
                case Symbol.Build:
                    Line(.27f,.18f,.69f,.72f,.14f); Polygon(V(.43f,.77f),V(.58f,.9f),V(.88f,.65f),V(.75f,.49f)); break;
                case Symbol.Army:
                    Stroke(new[]{V(.17f,.84f),V(.5f,.92f),V(.83f,.84f),V(.76f,.39f),V(.5f,.13f),V(.24f,.39f),V(.17f,.84f)},.065f);
                    Stroke(new[]{V(.34f,.68f),V(.5f,.55f),V(.66f,.68f)},.065f); Stroke(new[]{V(.35f,.49f),V(.5f,.36f),V(.65f,.49f)},.065f); break;
                case Symbol.Language:
                    Arc(V(.5f,.5f),.38f,0,360,.06f); Line(.13f,.5f,.87f,.5f,.055f);
                    Ellipse(V(.5f,.5f),.19f,.38f,.055f); break;
                case Symbol.Audio:
                    RoundedBox(.13f,.36f,.33f,.64f,.022f); Polygon(V(.31f,.36f),V(.57f,.15f),V(.57f,.85f),V(.31f,.64f));
                    Arc(V(.52f,.5f),.24f,-52,52,.065f); Arc(V(.52f,.5f),.39f,-52,52,.065f); break;
                case Symbol.Visuals:
                    Disc(V(.5f,.5f), .2f); for(int i=0;i<8;i++){var a=Polar(V(.5f,.5f),.31f,i*45);var b=Polar(V(.5f,.5f),.42f,i*45);Line(a.x,a.y,b.x,b.y,.065f);} break;
                case Symbol.Controls:
                    Stroke(new[]{V(.1f,.23f),V(.17f,.66f),V(.28f,.76f),V(.72f,.76f),V(.83f,.66f),V(.9f,.23f),V(.75f,.18f),V(.61f,.38f),V(.39f,.38f),V(.25f,.18f),V(.1f,.23f)},.067f);
                    Line(.23f,.55f,.41f,.55f,.059f); Line(.32f,.46f,.32f,.64f,.059f); Disc(V(.69f,.61f),.043f); Disc(V(.76f,.48f),.043f); break;
                case Symbol.Board:
                    Stroke(new[]{V(.1f,.59f),V(.5f,.84f),V(.9f,.59f),V(.5f,.33f),V(.1f,.59f)},.065f);
                    Stroke(new[]{V(.12f,.37f),V(.5f,.13f),V(.88f,.37f)},.065f); break;
                case Symbol.Check: Stroke(new[]{V(.17f,.5f),V(.39f,.27f),V(.84f,.76f)},.1f); break;
            }

            void Polygon(params Vector2[] points)
            {
                int start = mesh.currentVertCount;
                foreach (var point in points) Vertex(point, color);
                for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
                Fringe(points, false);
            }
            void Ring(Vector2[] outside, Vector2[] inside)
            {
                int start = mesh.currentVertCount;
                for(int i=0;i<outside.Length;i++){Vertex(outside[i],color);Vertex(inside[i],color);}
                for(int i=0;i<outside.Length;i++){int a=start+i*2,b=start+((i+1)%outside.Length)*2;mesh.AddTriangle(a,b,b+1);mesh.AddTriangle(a,b+1,a+1);}
                Fringe(outside,false); Fringe(inside,true);
            }
            void Fringe(Vector2[] points, bool hole)
            {
                float area=0;for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];area+=a.x*b.y-a.y*b.x;}
                float direction=(area>=0?1:-1)*(hole?-1:1);int start=mesh.currentVertCount;var clear=color;clear.a=0;
                for(int i=0;i<points.Length;i++)
                {
                    var a=(points[i]-points[(i+points.Length-1)%points.Length]).normalized;var b=(points[(i+1)%points.Length]-points[i]).normalized;
                    var n0=V(a.y,-a.x)*direction;var n1=V(b.y,-b.x)*direction;var normal=(n0+n1).normalized;
                    float length=feather/Mathf.Max(.55f,Vector2.Dot(normal,n1));Vertex(points[i],color);Vertex(points[i]+normal*length,clear);
                }
                for(int i=0;i<points.Length;i++){int a=start+i*2,b=start+((i+1)%points.Length)*2;mesh.AddTriangle(a,a+1,b+1);mesh.AddTriangle(a,b+1,b);}
            }
            void Vertex(Vector2 point,Color tint)=>mesh.AddVert(origin+point*scale,tint,Vector2.zero);
            void Box(float x0,float y0,float x1,float y1)=>Polygon(V(x0,y0),V(x0,y1),V(x1,y1),V(x1,y0));
            void RoundedBox(float x0,float y0,float x1,float y1,float radius)
            {
                var points=new Vector2[24];
                for(int corner=0;corner<4;corner++)for(int i=0;i<6;i++)
                {var center=V(corner==0||corner==3?x1-radius:x0+radius,corner<2?y1-radius:y0+radius);points[corner*6+i]=Polar(center,radius,corner*90+i*18);}
                Polygon(points);
            }
            void Disc(Vector2 center,float radius)
            {var points=new Vector2[24];for(int i=0;i<24;i++)points[i]=Polar(center,radius,i*15);Polygon(points);}
            void Line(float x0,float y0,float x1,float y1,float width=weight)
            {
                var a=V(x0,y0);var b=V(x1,y1);var n=V(-(b-a).y,(b-a).x).normalized*width*.5f;
                Polygon(a-n,a+n,b+n,b-n);Disc(a,width*.5f);Disc(b,width*.5f);
            }
            void Stroke(Vector2[] points,float width)
            {for(int i=1;i<points.Length;i++)Line(points[i-1].x,points[i-1].y,points[i].x,points[i].y,width);}
            void Arc(Vector2 center,float radius,float from,float to,float width)
            {
                int steps=Mathf.CeilToInt((to-from)/10);var outer=new Vector2[steps+1];var inner=new Vector2[steps+1];
                for(int i=0;i<=steps;i++){float angle=Mathf.Lerp(from,to,i/(float)steps);outer[i]=Polar(center,radius+width*.5f,angle);inner[i]=Polar(center,radius-width*.5f,angle);}
                // One joined contour keeps the anti-aliased edge continuous.
                var outline=new Vector2[(steps+1)*2];for(int i=0;i<=steps;i++){outline[i]=outer[i];outline[steps+1+i]=inner[steps-i];}
                int first=mesh.currentVertCount;for(int i=0;i<=steps;i++){Vertex(outer[i],color);Vertex(inner[i],color);}
                for(int i=0;i<steps;i++){int a=first+i*2;mesh.AddTriangle(a,a+2,a+3);mesh.AddTriangle(a,a+3,a+1);}Fringe(outline,false);
            }
            void Ellipse(Vector2 center,float rx,float ry,float width)
            {
                var outer=new Vector2[40];var inner=new Vector2[40];for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;var d=V(Mathf.Cos(a),Mathf.Sin(a));outer[i]=center+V(d.x*(rx+width*.5f),d.y*(ry+width*.5f));inner[i]=center+V(d.x*(rx-width*.5f),d.y*(ry-width*.5f));}Ring(outer,inner);
            }
            void Person(float x,float y,float s)
            {Disc(V(x,y+.29f*s),.105f*s);RoundedBox(x-.16f*s,y-.22f*s,x+.16f*s,y+.11f*s,.07f*s);}
            void Crystal(float x,float y,float w,float h)
            {Polygon(V(x,y),V(x-w*.5f,y+h*.18f),V(x-w*.5f,y+h*.77f),V(x,y+h),V(x+w*.5f,y+h*.77f),V(x+w*.5f,y+h*.18f));}
        }
        private static Vector2 V(float x,float y)=>new Vector2(x,y);
        private static Vector2 Polar(Vector2 center,float radius,float angle)=>center+new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*radius;
    }
}
