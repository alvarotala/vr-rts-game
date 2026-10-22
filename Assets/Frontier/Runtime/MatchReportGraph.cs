using System;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public enum ReportMetric { Workers, Army, MineralsCollected, CatalystCollected, Minerals, Catalyst, Apm }

    // Drawn directly in the world-space canvas; no cameras, textures or per-frame updates.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MatchReportGraph : MaskableGraphic
    {
        public static readonly Color PlayerColor=ConsoleTheme.Accent, OpponentColor=new Color(1,.65f,.38f);
        public MatchReport Report { get; private set; }
        public ReportMetric Metric { get; private set; }
        public float Maximum { get; private set; }
        public int Player { get; private set; }
        public static float Value(PlayerMatchStats p,ReportMetric metric)
        {
            switch(metric)
            {
                case ReportMetric.Workers:return p.Workers;
                case ReportMetric.Army:return p.Army;
                case ReportMetric.MineralsCollected:return p.MineralsCollected;
                case ReportMetric.CatalystCollected:return p.CatalystCollected;
                case ReportMetric.Minerals:return p.Minerals;
                case ReportMetric.Catalyst:return p.Catalyst;
                default:return p.RecentApm;
            }
        }
        public void Configure(MatchReport report,ReportMetric metric,int player=0)
        {
            Report=report;Metric=metric;Player=player==1?1:0;raycastTarget=false;
            float max=1;
            foreach(var sample in report.Samples)foreach(var p in sample.Players)max=Mathf.Max(max,Value(p,metric));
            float step=Mathf.Pow(10,Mathf.Floor(Mathf.Log10(max)));
            Maximum=Mathf.Ceil(max/step)*step;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(Report==null)return;
            var r=rectTransform.rect;var grid=Color.Lerp(ConsoleTheme.Surface,ConsoleTheme.Muted,.18f);
            for(int i=0;i<=4;i++)
            {
                float y=r.yMin+r.height*i/4;Line(mesh,new Vector2(r.xMin,y),new Vector2(r.xMax,y),1,grid);
                float x=r.xMin+r.width*i/4;Line(mesh,new Vector2(x,r.yMin),new Vector2(x,r.yMax),1,grid);
            }
            for(int p=0;p<2;p++)
            {
                var tint=p==Player?PlayerColor:OpponentColor;
                Vector2 Point(MatchSample s)=>new Vector2(r.xMin+r.width*s.Tick/Math.Max(1,Report.Tick),r.yMin+r.height*Value(s.Players[p],Metric)/Maximum);
                int stride=Math.Max(1,(Report.Samples.Count+510)/511);
                for(int previous=0;previous<Report.Samples.Count-1;)
                {
                    int i=Math.Min(Report.Samples.Count-1,previous+stride);
                    Vector2 from=Point(Report.Samples[previous]),to=Point(Report.Samples[i]);
                    previous=i;
                    // Step values reflect the sampled counts without inventing fractional units.
                    var elbow=new Vector2(to.x,from.y);
                    Draw(mesh,from,elbow,p!=Player,tint);Draw(mesh,elbow,to,p!=Player,tint);
                }
                var end=Point(Report.Samples[Report.Samples.Count-1]);
                Line(mesh,end-Vector2.right*3,end+Vector2.right*3,6,tint);
            }
        }
        private static void Draw(VertexHelper mesh,Vector2 a,Vector2 b,bool dashed,Color color)
        {
            if(!dashed){Line(mesh,a,b,3,color);return;}
            float distance=Vector2.Distance(a,b);var direction=(b-a).normalized;
            for(float d=0;d<distance;d+=18)Line(mesh,a+direction*d,a+direction*Mathf.Min(distance,d+10),3,color);
        }
        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color)
        {
            if((b-a).sqrMagnitude<.0001f)return;
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int start=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
