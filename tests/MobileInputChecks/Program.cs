using System;
using Frontier.Runtime;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition,string label)
    {if(!condition)throw new Exception(label);checks++;}
    private static MobileGesture.Sample Finger(float x=100,float y=100,bool down=false,bool up=false)
        =>new MobileGesture.Sample{Count=up?0:1,Finger=1,X=x,Y=y,Down=down,Up=up,AllowMap=true,SecondOverMap=true};
    private static MobileGesture.Result Feed(MobileGesture gesture,MobileGesture.Sample sample)=>gesture.Step(sample,12);
    private static MobileGesture.Sample Pair(float first=100,float second=200)
        =>new MobileGesture.Sample{Count=2,Finger=1,OtherFinger=2,X=first,Y=100,OtherX=second,OtherY=100,AllowMap=true,SecondOverMap=true};

    public static int Main()
    {
        try
        {
            var gesture=new MobileGesture();
            Check(Feed(gesture,Finger(down:true)).Kind==MobileGesture.Kind.None,"press defers map action until release");
            Check(Feed(gesture,Finger(105,103)).Kind==MobileGesture.Kind.None,"finger jitter does not drag");
            Check(Feed(gesture,Finger(105,103,up:true)).Kind==MobileGesture.Kind.Tap,"short tap selects exactly on release");
            Check(Feed(gesture,new MobileGesture.Sample()).Kind==MobileGesture.Kind.None,"idle never repeats a tap");
            Check(Feed(gesture,Finger(down:true,up:true)).Kind==MobileGesture.Kind.Tap,"quick press/release in one input update is a tap");

            gesture=new MobileGesture();var held=Finger(down:true);held.Time=10;Feed(gesture,held);
            held=Finger();held.Time=10.6f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.None,"long hold defers order until release so camera drag can cancel");
            held=Finger(104,103,up:true);held.Time=10.7f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.LongPress,"hold and release yields explicit contextual order despite slight jitter");
            held=Finger(down:true);held.Time=11;Feed(gesture,held);
            held=Finger(125,100);held.Time=12;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Pan,"moving after a long hold cancels contextual order");
            held=Finger(125,100,up:true);held.Time=12.1f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Pan,"long hold drag release never emits contextual order");
            held=Finger(down:true);held.Time=13;Feed(gesture,held);
            held=Finger(up:true);held.Time=13.1f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Tap,"short tap after long hold remains an ordinary tap");
            held=Finger(down:true);held.Time=13.2f;Feed(gesture,held);
            held=Finger(up:true);held.Time=13.3f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Tap,"second short tap remains eligible for shared double-tap selection");

            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            var drag=Feed(gesture,Finger(125,100));
            Check(drag.Kind==MobileGesture.Kind.Pan&&drag.DeltaX==25,"drag pans from the previous position");
            Check(Feed(gesture,Finger(126,100,up:true)).Kind==MobileGesture.Kind.Pan,"pan release cannot become a selection/order");

            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            var pinch=Feed(gesture,Pair());
            Check(pinch.Kind==MobileGesture.Kind.Pinch&&pinch.ZoomRatio==1&&pinch.DeltaX==0,"second finger establishes pinch baseline");
            pinch=Feed(gesture,Pair(90,290));
            Check(pinch.ZoomRatio==.5f&&pinch.DeltaX==40,"spread zooms in and midpoint pans");
            var changed=Pair(110,310);changed.OtherFinger=3;
            pinch=Feed(gesture,changed);
            Check(pinch.ZoomRatio==1&&pinch.DeltaX==0,"replaced finger never jumps the camera");
            Check(Feed(gesture,Finger()).Kind==MobileGesture.Kind.None,"one remaining finger after pinch is consumed");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"pinch final release never selects/orders");
            Feed(gesture,Finger(down:true));
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.Tap,"fresh tap works after all pinch fingers lift");

            gesture=new MobileGesture();var ui=Finger(down:true);ui.OverUi=true;
            var result=Feed(gesture,ui);
            Check(result.Kind==MobileGesture.Kind.Ui&&result.Down,"HUD receives press");
            result=Feed(gesture,Finger(500,500));
            Check(result.Kind==MobileGesture.Kind.Ui&&result.Held,"HUD owns drag outside its bounds");
            result=Feed(gesture,Finger(500,500,up:true));
            Check(result.Kind==MobileGesture.Kind.Ui&&result.Up,"HUD release cannot leak to map");
            Feed(gesture,ui);
            Check(Feed(gesture,Pair()).Cancelled,"multitouch on HUD cancels its button");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"cancelled HUD gesture never taps map");

            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            var blockedPair=Pair();blockedPair.SecondOverMap=false;
            Check(Feed(gesture,blockedPair).Cancelled,"second finger on HUD cannot zoom map");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"mixed HUD/map release is consumed");
            Feed(gesture,Finger(down:true));var three=Pair();three.Count=3;
            Check(Feed(gesture,three).Cancelled,"third finger cancels ambiguous gesture");

            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            var paused=Finger();paused.AllowMap=false;
            Check(Feed(gesture,paused).Cancelled,"pause during contact cancels map gesture");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"paused release never sends an order");
            Feed(gesture,Finger(down:true));gesture.CancelUntilRelease();
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"focus loss drains the held contact");
            Check(Feed(gesture,Finger()).Kind==MobileGesture.Kind.None,"held contact without a press never starts on resume");
            var cancelled=Finger(down:true);cancelled.Cancelled=true;
            Check(Feed(gesture,cancelled).Cancelled,"OS cancelled touch is consumed");
            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            Check(Feed(gesture,new MobileGesture.Sample{AllowMap=true}).Cancelled,"device disappearance without a release edge cannot send a tap/order");

            gesture=new MobileGesture();var area=Finger(down:true);area.AreaSelection=true;
            result=Feed(gesture,area);
            Check(result.Kind==MobileGesture.Kind.Area&&result.Down,"explicit area tool begins selection rectangle");
            result=Feed(gesture,Finger(300,300,up:true));
            Check(result.Kind==MobileGesture.Kind.Area&&result.Up,"area tool retains ownership through release");
            Feed(gesture,area);result=Feed(gesture,Pair());
            Check(result.Kind==MobileGesture.Kind.Pinch&&result.Cancelled,"pinch cancels rectangle before camera navigation");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"cancelled rectangle cannot select on release");

            Console.WriteLine($"Mobile input checks passed: {checks} (shared gesture classifier, no Unity/device rendering implied).");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
