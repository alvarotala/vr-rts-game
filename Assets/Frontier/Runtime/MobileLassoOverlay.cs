#if FRONTIER_BROWSER
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    // A separate pixel-space canvas avoids changing the HUD's scaling/safe area.
    // Its graphic never intercepts touches and stays below the interface canvas.
    public sealed class MobileLassoOverlay : MaskableGraphic
    {
        private ScreenLasso path;
        public static MobileLassoOverlay Create(Transform parent)
        {
            var root=new GameObject("Touch selection lasso",typeof(RectTransform),typeof(Canvas),typeof(MobileLassoOverlay));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=8;
            var overlay=root.GetComponent<MobileLassoOverlay>();overlay.raycastTarget=false;
            return overlay;
        }
        public void Show(ScreenLasso value){path=value;SetVerticesDirty();}
        public void Clear(){path=null;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(path==null||path.Points.Count<2)return;
            float width=Mathf.Clamp(Screen.dpi/96,1,3)*2;
            var shadow=new Color32(0,12,24,230);var stroke=new Color32(94,255,213,255);
            for(int i=1;i<path.Points.Count;i++)
            {
                var a=Local(path.Points[i-1]);var b=Local(path.Points[i]);
                Line(mesh,a,b,width+3,shadow);Line(mesh,a,b,width,stroke);
            }
            if(path.Points.Count<3)return;
            var end=Local(path.Points[path.Points.Count-1]);var start=Local(path.Points[0]);
            var delta=start-end;float length=delta.magnitude;if(length<.1f)return;
            var direction=delta/length;
            for(float distance=0;distance<length;distance+=width*5)
                Line(mesh,end+direction*distance,end+direction*Mathf.Min(length,distance+width*3),width,new Color32(94,255,213,155));
        }
        private Vector2 Local(ScreenLasso.Point point)
        {RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,new Vector2(point.X,point.Y),null,out var local);return local;}
        private static void Line(VertexHelper mesh,Vector2 start,Vector2 end,float width,Color32 tint)
        {
            var delta=end-start;if(delta.sqrMagnitude<.01f)return;
            var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;int index=mesh.currentVertCount;
            mesh.AddVert(start-normal,tint,Vector2.zero);mesh.AddVert(start+normal,tint,Vector2.zero);
            mesh.AddVert(end+normal,tint,Vector2.zero);mesh.AddVert(end-normal,tint,Vector2.zero);
            mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
        }
    }
}
#endif
