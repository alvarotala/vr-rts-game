using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A small vector wordmark, independent of installed fonts or texture resolution.
    // The squared strokes and clipped corners follow the Little Legion menu concept.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LegionLogo : MaskableGraphic
    {
        public const float AspectRatio = 9.82f;
        public float ContentAspect => AspectRatio;
        [SerializeField] private Color littleColor = new Color(.97f, .975f, .95f, 1);
        [SerializeField] private Color legionColor = MenuPalette.Accent;
        public Color LittleColor { get => littleColor; set { littleColor = value; SetVerticesDirty(); } }
        public Color LegionColor { get => legionColor; set { legionColor = value; SetVerticesDirty(); } }

        protected override void Awake() { base.Awake(); raycastTarget = false; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = rectTransform.rect;
            // Reserve a sliver for the lower edge and its soft shadow. The same
            // proportions apply to the compact HUD mark and the large masthead.
            float scale = Mathf.Min(rect.width / AspectRatio, rect.height) / 1.06f;
            if (scale <= 0 || color.a <= 0) return;
            Vector2 origin = rect.center - new Vector2(AspectRatio, 1) * (scale * .5f) + Vector2.up * (.01f * scale);
            float cursor = 0; bool shadow = true;
            DrawWords(); shadow = false; cursor = 0; DrawWords();

            void DrawWords() { DrawWord("LITTLE", littleColor); cursor += .34f; DrawWord("LEGION", legionColor); }
            void DrawWord(string word, Color tint)
            {
                for (int i = 0; i < word.Length; i++)
                {
                    char letter = word[i];
                    if (letter == 'O')
                    {
                        var outer = Points(0, .08f, .08f, 0, .76f, 0, .84f, .08f, .84f, .92f, .76f, 1, .08f, 1, 0, .92f);
                        var inner = Points(.25f, .26f, .27f, .24f, .57f, .24f, .59f, .26f, .59f, .74f, .57f, .76f, .27f, .76f, .25f, .74f);
                        for (int j = 0; j < outer.Length; j++)
                        {
                            int next = (j + 1) % outer.Length;
                            Polygon(new[] { outer[j], outer[next], inner[next], inner[j] }, tint, false);
                        }
                        Edge(outer, tint, false); Edge(inner, tint, true);
                    }
                    else Polygon(Outline(letter), tint, true);
                    cursor += Width(letter); if (i < word.Length - 1) cursor += .1f;
                }
            }
            void Polygon(Vector2[] points, Color tint, bool edge)
            {
                int start = mesh.currentVertCount;
                foreach (var point in points) Vertex(point, Tint(tint, point.y));
                var triangles = Triangulate(points);
                for (int i = 0; i < triangles.Count; i += 3)
                    mesh.AddTriangle(start + triangles[i], start + triangles[i + 1], start + triangles[i + 2]);
                if (edge) Edge(points, tint, false);
            }
            Color Tint(Color tint,float y)
            {
                if(shadow) return new Color(.008f,.02f,.028f,.48f * tint.a * color.a);
                var result=tint*color;float shade=Mathf.Lerp(.88f,1.025f,y);
                result.r=Mathf.Min(1,result.r*shade);result.g=Mathf.Min(1,result.g*shade);result.b=Mathf.Min(1,result.b*shade);return result;
            }
            void Vertex(Vector2 point,Color tint)
            {
                var offset=shadow?new Vector2(.003f,-.019f):Vector2.zero;
                mesh.AddVert(origin+(new Vector2(cursor+point.x,point.y)+offset)*scale,tint,Vector2.zero);
            }
            void Edge(Vector2[] points,Color tint,bool hole)
            {
                float area=0;for(int i=0;i<points.Length;i++)area+=Cross(points[i],points[(i+1)%points.Length]);
                float sign=(area>0?1:-1)*(hole?-1:1);
                float feather=Mathf.Min((shadow?1.1f:.65f)/scale,.018f);int start=mesh.currentVertCount;
                for(int i=0;i<points.Length;i++)
                {
                    var a=(points[i]-points[(i+points.Length-1)%points.Length]).normalized;var b=(points[(i+1)%points.Length]-points[i]).normalized;
                    var n0=new Vector2(a.y,-a.x)*sign;var n1=new Vector2(b.y,-b.x)*sign;var normal=(n0+n1).normalized;
                    float length=feather/Mathf.Max(.55f,Vector2.Dot(normal,n1));var solid=Tint(tint,points[i].y);var clear=solid;clear.a=0;
                    Vertex(points[i],solid);Vertex(points[i]+normal*length,clear);
                }
                for(int i=0;i<points.Length;i++){int a=start+i*2,b=start+((i+1)%points.Length)*2;mesh.AddTriangle(a,a+1,b+1);mesh.AddTriangle(a,b+1,b);}
                if(shadow)return;
                // A hairline on upward-facing edges supplies the restrained bevel
                // of the concept; downward edges keep the darker body tint.
                for(int i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];var d=(b-a).normalized;var normal=new Vector2(d.y,-d.x)*sign;
                    if(normal.y<.5f)continue;
                    float width=Mathf.Min(.008f,.65f/scale);var bright=tint*color;bright.r=Mathf.Lerp(bright.r,1,.17f);bright.g=Mathf.Lerp(bright.g,1,.17f);bright.b=Mathf.Lerp(bright.b,1,.17f);
                    int first=mesh.currentVertCount;Vertex(a,bright);Vertex(b,bright);Vertex(b-normal*width,Tint(tint,b.y));Vertex(a-normal*width,Tint(tint,a.y));
                    mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
                }
            }
        }

        private static float Width(char letter) => letter == 'I' ? .28f : letter == 'E' ? .78f
            : letter == 'G' || letter == 'O' ? .84f : letter == 'N' ? .88f : .76f;
        private static Vector2[] Outline(char letter)
        {
            switch (letter)
            {
                case 'L': return Points(0, 0, 0, 1, .25f, 1, .25f, .24f, .76f, .24f, .72f, 0);
                case 'I': return Points(0, 0, 0, 1, .28f, 1, .28f, 0);
                case 'T': return Points(.255f, 0, .255f, .75f, 0, .75f, 0, 1, .76f, 1, .76f, .75f, .505f, .75f, .505f, 0);
                case 'E': return Points(0, 0, 0, 1, .78f, 1, .72f, .76f, .25f, .76f, .25f, .62f,
                    .71f, .62f, .65f, .39f, .25f, .39f, .25f, .24f, .78f, .24f, .72f, 0);
                case 'G': return Points(.08f, 0, 0, .08f, 0, .92f, .08f, 1, .84f, 1, .84f, .75f,
                    .26f, .75f, .26f, .24f, .60f, .24f, .60f, .40f, .44f, .40f, .44f, .62f, .84f, .62f, .84f, .08f, .76f, 0);
                case 'N': return Points(0, 0, 0, 1, .24f, 1, .63f, .42f, .63f, 1, .88f, 1,
                    .88f, 0, .64f, 0, .25f, .58f, .25f, 0);
                default: return new Vector2[0];
            }
        }
        private static Vector2[] Points(params float[] values)
        {
            var points = new Vector2[values.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            return points;
        }
        private static List<int> Triangulate(Vector2[] points)
        {
            var remaining = new List<int>(); var triangles = new List<int>(); float area = 0;
            for (int i = 0; i < points.Length; i++) area += Cross(points[i], points[(i + 1) % points.Length]);
            for (int i = 0; i < points.Length; i++) remaining.Add(area > 0 ? i : points.Length - 1 - i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[b]) <= .000001f) continue;
                    bool occupied = false;
                    foreach (int p in remaining)
                    {
                        if (p == a || p == b || p == c) continue;
                        if (Cross(points[b] - points[a], points[p] - points[a]) >= -.000001f
                            && Cross(points[c] - points[b], points[p] - points[b]) >= -.000001f
                            && Cross(points[a] - points[c], points[p] - points[c]) >= -.000001f) { occupied = true; break; }
                    }
                    if (occupied) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c); remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            return triangles;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
