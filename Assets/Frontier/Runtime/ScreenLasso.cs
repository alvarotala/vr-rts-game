#if FRONTIER_BROWSER
using System;
using System.Collections.Generic;

namespace Frontier.Runtime
{
    // A bounded screen-space polygon. The final segment closes automatically;
    // selection follows the actual path, including concavities, not its bounds.
    public sealed class ScreenLasso
    {
        public readonly struct Point
        {
            public readonly float X,Y;
            public Point(float x,float y){X=x;Y=y;}
        }
        public const int MaximumPoints=256;
        private readonly List<Point> points=new List<Point>();
        public IReadOnlyList<Point> Points=>points;
        private float sampleSpacing=3;

        public void Begin(float x,float y,float spacing)
        {points.Clear();sampleSpacing=Math.Max(1,spacing);Add(x,y,true);}
        public void Clear()=>points.Clear();
        public void Add(float x,float y,bool force=false)
        {
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y))return;
            if(points.Count>0)
            {
                var last=points[points.Count-1];float dx=x-last.X,dy=y-last.Y;
                if(dx*dx+dy*dy<(force ? .01f : sampleSpacing*sampleSpacing))return;
            }
            if(points.Count==MaximumPoints)
            {
                // Thin old samples instead of truncating the current stroke.
                // Both its beginning and its current end survive long circles.
                int write=1;
                for(int read=2;read<points.Count;read+=2)points[write++]=points[read];
                points.RemoveRange(write,points.Count-write);
                sampleSpacing*=1.5f;
            }
            points.Add(new Point(x,y));
        }
        public bool IsValid(float minimumExtent)
        {
            if(points.Count<3)return false;
            double twiceArea=0,perimeter=0;
            float minX=points[0].X,maxX=minX,minY=points[0].Y,maxY=minY;
            for(int i=0;i<points.Count;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Count];
                minX=Math.Min(minX,a.X);maxX=Math.Max(maxX,a.X);minY=Math.Min(minY,a.Y);maxY=Math.Max(maxY,a.Y);
                twiceArea+=(double)a.X*b.Y-(double)b.X*a.Y;
                double dx=a.X-b.X,dy=a.Y-b.Y;perimeter+=Math.Sqrt(dx*dx+dy*dy);
            }
            double area=Math.Abs(twiceArea)*.5;
            // Reject taps, straight swipes and narrow wobbles. A roughly drawn
            // open circle still qualifies without returning to its first pixel.
            return maxX-minX>=minimumExtent&&maxY-minY>=minimumExtent
                &&area>=minimumExtent*minimumExtent*.5&&perimeter>0&&2*area/perimeter>=minimumExtent*.25;
        }
        public bool Contains(float x,float y)
        {
            if(points.Count<3||float.IsNaN(x)||float.IsNaN(y)||float.IsInfinity(x)||float.IsInfinity(y))return false;
            bool inside=false;
            for(int i=0,j=points.Count-1;i<points.Count;j=i++)
            {
                var a=points[j];var b=points[i];
                double dx=b.X-a.X,dy=b.Y-a.Y,px=x-a.X,py=y-a.Y;
                double length=dx*dx+dy*dy;
                if(length>0)
                {
                    double along=(px*dx+py*dy)/length;
                    if(along>=0&&along<=1)
                    {
                        double cross=px*dy-py*dx;
                        if(cross*cross<=length*.25)return true;
                    }
                }
                if((a.Y>y)!=(b.Y>y)&&x<(double)(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
            }
            return inside;
        }
    }
}
#endif
