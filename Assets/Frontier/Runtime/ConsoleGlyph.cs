using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // Small console symbols built from flat UI triangles. Graphic.color supplies the
    // primary ink; the second tone stays flat, with no texture, outline or text glyph.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ConsoleGlyph : MaskableGraphic
    {
        public enum Kind { Mineral, Gas, Supply, Reset, Pause, Group }

        [SerializeField] private Kind glyph;
        public Kind Glyph
        {
            get=>glyph;
            set{if(glyph==value)return;glyph=value;SetVerticesDirty();}
        }

        protected override void Awake(){base.Awake();raycastTarget=false;}

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds=GetPixelAdjustedRect();float size=Mathf.Min(bounds.width,bounds.height);
            if(size<=0)return;
            var ink=color;var shade=new Color(ink.r*.58f,ink.g*.58f,ink.b*.58f,ink.a);
            switch(glyph)
            {
                case Kind.Mineral:
                    var top=new Vector2(0,.45f);var upperLeft=new Vector2(-.29f,.18f);
                    var upperRight=new Vector2(.29f,.18f);var lowerLeft=new Vector2(-.23f,-.25f);
                    var lowerRight=new Vector2(.23f,-.25f);var bottom=new Vector2(0,-.45f);
                    var seamTop=new Vector2(0,.09f);var seamBottom=new Vector2(0,-.25f);
                    Triangle(top,upperLeft,seamTop,ink);Triangle(top,seamTop,upperRight,shade);
                    Quad(upperLeft,lowerLeft,seamBottom,seamTop,shade);
                    Quad(seamTop,seamBottom,lowerRight,upperRight,ink);
                    Triangle(lowerLeft,bottom,seamBottom,ink);Triangle(seamBottom,bottom,lowerRight,shade);
                    break;
                case Kind.Gas:
                    var first=new Vector2(-.17f,.24f);var second=new Vector2(.24f,.05f);var third=new Vector2(-.10f,-.24f);
                    Stroke(first,second,.10f,shade);Stroke(second,third,.10f,shade);Stroke(third,first,.10f,shade);
                    Disc(first,.17f,ink);Disc(second,.16f,shade);Disc(third,.19f,ink);
                    break;
                case Kind.Supply:
                    Person(new Vector2(.20f,.02f),.86f,shade);
                    Person(new Vector2(-.18f,-.02f),1,ink);
                    break;
                case Kind.Group:
                    Person(new Vector2(-.26f,.12f),.66f,shade);
                    Person(new Vector2(.26f,.12f),.66f,shade);
                    Person(new Vector2(0,-.09f),.90f,ink);
                    break;
                case Kind.Reset:
                    const float radius=.30f,thickness=.12f;
                    const int segments=28;
                    for(int i=0;i<segments;i++)
                    {
                        float from=(135-270f*i/segments)*Mathf.Deg2Rad;
                        float to=(135-270f*(i+1)/segments)*Mathf.Deg2Rad;
                        Quad(Radial(from,radius-thickness*.5f),Radial(to,radius-thickness*.5f),
                            Radial(to,radius+thickness*.5f),Radial(from,radius+thickness*.5f),ink);
                    }
                    float angle=135*Mathf.Deg2Rad;var end=Radial(angle,radius);
                    var tangent=new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle));var normal=Radial(angle,1);
                    Triangle(end+tangent*.17f,end-tangent*.055f+normal*.15f,end-tangent*.055f-normal*.15f,ink);
                    break;
                case Kind.Pause:
                    Box(-.29f,-.36f,-.09f,.36f,ink);Box(.09f,-.36f,.29f,.36f,ink);
                    break;
            }

            Vector2 Radial(float angle,float radius)=>new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
            void Vertex(Vector2 point,Color tint)=>mesh.AddVert(bounds.center+point*size,tint,Vector2.zero);
            void Triangle(Vector2 a,Vector2 b,Vector2 c,Color tint)
            {
                int start=mesh.currentVertCount;Vertex(a,tint);Vertex(b,tint);Vertex(c,tint);
                mesh.AddTriangle(start,start+1,start+2);
            }
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {
                int start=mesh.currentVertCount;Vertex(a,tint);Vertex(b,tint);Vertex(c,tint);Vertex(d,tint);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            void Box(float left,float bottom,float right,float top,Color tint)
                =>Quad(new Vector2(left,bottom),new Vector2(right,bottom),new Vector2(right,top),new Vector2(left,top),tint);
            void Stroke(Vector2 from,Vector2 to,float width,Color tint)
            {
                var delta=to-from;var side=new Vector2(-delta.y,delta.x).normalized*(width*.5f);
                Quad(from-side,to-side,to+side,from+side,tint);
            }
            void Disc(Vector2 center,float radius,Color tint)
            {
                const int sides=12;
                for(int i=0;i<sides;i++)Triangle(center,center+Radial(2*Mathf.PI*i/sides,radius),
                    center+Radial(2*Mathf.PI*(i+1)/sides,radius),tint);
            }
            void Person(Vector2 position,float scale,Color tint)
            {
                Disc(position+new Vector2(0,.22f)*scale,.105f*scale,tint);
                Quad(position+new Vector2(-.11f,.055f)*scale,position+new Vector2(-.18f,-.10f)*scale,
                    position+new Vector2(.18f,-.10f)*scale,position+new Vector2(.11f,.055f)*scale,tint);
                Quad(position+new Vector2(-.18f,-.10f)*scale,position+new Vector2(-.18f,-.31f)*scale,
                    position+new Vector2(.18f,-.31f)*scale,position+new Vector2(.18f,-.10f)*scale,tint);
            }
        }
    }
}
