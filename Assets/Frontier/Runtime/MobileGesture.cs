#if FRONTIER_BROWSER
using System;

namespace Frontier.Runtime
{
    // Screen-space gesture ownership is independent of Unity devices. Once a
    // finger starts on UI, or a gesture becomes multitouch, its release can never
    // become a map tap. The same classifier is exercised by the mobile checks.
    public sealed class MobileGesture
    {
        public enum Kind { None, Ui, Tap, LongPress, Pan, Pinch, Lasso }
        public enum TapAction { Select, Context, AttackMove }
        public const float LongPressSeconds=.55f;
        public struct Sample
        {
            public int Count,Finger,OtherFinger;
            public float X,Y,OtherX,OtherY,Time;
            public bool Down,Up,Cancelled,OverUi,SecondOverMap,AllowMap,LassoSelection,Shift;
        }
        public struct Result
        {
            public Kind Kind;
            public bool Down,Up,Held,Cancelled,Shift;
            public float DeltaX,DeltaY,ZoomRatio,StartX,StartY;
        }
        private bool captured,ui,lasso,shift,moving,multi,draining;
        private float startX,startY,lastX,lastY,lastSpan,pressedAt;
        private int firstFinger,secondFinger;
        public bool Captured=>captured;

        public static TapAction ResolveTap(bool shifted,bool forceOrder,bool targeting,bool selectedUnits,bool ownSelection,bool ownTarget)
        {
            if(targeting)return TapAction.Context;
            if(shifted&&selectedUnits)return TapAction.AttackMove;
            if(forceOrder||selectedUnits||ownSelection&&!ownTarget)return TapAction.Context;
            return TapAction.Select;
        }

        public void CancelUntilRelease()
        {captured=ui=lasso=moving=multi=false;draining=true;lastSpan=0;}

        public Result Step(Sample sample,float dragThreshold)
        {
            if(sample.Cancelled)
            {CancelUntilRelease();return new Result{Cancelled=true};}
            if(draining)
            {
                if(sample.Count==0&&!sample.Down)draining=false;
                return default;
            }
            if(!captured)
            {
                // Ignore fingers already held when the app resumes or a page opens.
                if(!sample.Down)return default;
                captured=true;ui=sample.OverUi;shift=sample.Shift;lasso=!ui&&shift&&sample.LassoSelection;
                startX=lastX=sample.X;startY=lastY=sample.Y;
                firstFinger=sample.Finger;
                pressedAt=sample.Time;
                moving=multi=false;lastSpan=0;
            }
            if(!ui&&(!sample.AllowMap||sample.OverUi))
            {CancelUntilRelease();return new Result{Cancelled=true};}
            if(!multi&&sample.Count==1&&sample.Finger!=firstFinger)
            {CancelUntilRelease();return new Result{Cancelled=true};}
            if(sample.Count>1)
            {
                if(ui||sample.Count>2||!sample.SecondOverMap)
                {CancelUntilRelease();return new Result{Cancelled=true};}
                bool cancelLasso=lasso;lasso=false;multi=true;
                float x=(sample.X+sample.OtherX)*.5f,y=(sample.Y+sample.OtherY)*.5f;
                float dx=sample.X-sample.OtherX,dy=sample.Y-sample.OtherY;
                float span=(float)Math.Sqrt(dx*dx+dy*dy);
                var result=new Result{Kind=Kind.Pinch,Held=true,Cancelled=cancelLasso,ZoomRatio=1};
                if(lastSpan>1&&span>1&&firstFinger==sample.Finger&&secondFinger==sample.OtherFinger)
                {result.DeltaX=x-lastX;result.DeltaY=y-lastY;result.ZoomRatio=lastSpan/span;}
                firstFinger=sample.Finger;secondFinger=sample.OtherFinger;
                lastX=x;lastY=y;lastSpan=span;
                return result;
            }
            if(multi)
            {CancelUntilRelease();if(sample.Count==0)draining=false;return default;}
            if(sample.Count==0&&!sample.Up)
            {CancelUntilRelease();draining=false;return new Result{Cancelled=true};}
            bool released=sample.Up;
            if(ui)
            {
                var result=new Result{Kind=Kind.Ui,Down=sample.Down,Up=released,Held=!released};
                if(released)captured=false;
                return result;
            }
            float distanceX=sample.X-startX,distanceY=sample.Y-startY;
            bool wasMoving=moving;
            moving|=distanceX*distanceX+distanceY*distanceY>=dragThreshold*dragThreshold;
            var output=new Result{Kind=moving?(lasso?Kind.Lasso:Kind.Pan):released?(sample.Time-pressedAt>=LongPressSeconds?Kind.LongPress:Kind.Tap):Kind.None,
                Down=moving&&!wasMoving,Up=released,Held=!released,Shift=shift,StartX=startX,StartY=startY,DeltaX=sample.X-lastX,DeltaY=sample.Y-lastY};
            lastX=sample.X;lastY=sample.Y;
            if(released)captured=false;
            return output;
        }
    }
}
#endif
