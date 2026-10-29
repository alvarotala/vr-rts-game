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
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Tap,"second short tap remains a separate confirmed tap");

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

            gesture=new MobileGesture();var ordinaryPress=Finger(down:true);ordinaryPress.LassoSelection=true;
            Feed(gesture,ordinaryPress);
            Check(Feed(gesture,Finger(130,100)).Kind==MobileGesture.Kind.Pan,"ordinary gameplay drag pans even when lasso selection is available");
            Check(Feed(gesture,Finger(130,100,up:true)).Kind==MobileGesture.Kind.Pan,"ordinary pan release never selects units");

            gesture=new MobileGesture();var lassoPress=Finger(down:true);lassoPress.LassoSelection=true;lassoPress.Shift=true;
            result=Feed(gesture,lassoPress);
            Check(result.Kind==MobileGesture.Kind.None,"Shift contact waits to distinguish attack tap from lasso");
            result=Feed(gesture,Finger(130,100));
            Check(result.Kind==MobileGesture.Kind.Lasso&&result.Down&&result.Shift&&result.StartX==100&&result.StartY==100,"Shift drag begins lasso at original contact and keeps modifier if it is released");
            result=Feed(gesture,Finger(300,300,up:true));
            Check(result.Kind==MobileGesture.Kind.Lasso&&result.Up,"lasso completes only on release");
            Feed(gesture,lassoPress);Feed(gesture,Finger(130,100));
            result=Feed(gesture,Finger(100,100,up:true));
            Check(result.Kind==MobileGesture.Kind.Lasso,"returning to start never converts a closed circle into a tap");
            Feed(gesture,lassoPress);result=Feed(gesture,Finger(up:true));
            Check(result.Kind==MobileGesture.Kind.Tap&&result.Shift,"Shift tap retains attack modifier through release");
            lassoPress.Time=20;Feed(gesture,lassoPress);held=Finger(up:true);held.Time=20.7f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.LongPress,"Shift lasso mode still distinguishes a held contact");
            Feed(gesture,lassoPress);Feed(gesture,Finger(130,100));result=Feed(gesture,Pair());
            Check(result.Kind==MobileGesture.Kind.Pinch&&result.Cancelled,"second finger cancels lasso before camera navigation");
            Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"cancelled lasso cannot select on release");
            Feed(gesture,lassoPress);Feed(gesture,Finger(130,100));var crossing=Finger(140,100);crossing.OverUi=true;
            Check(Feed(gesture,crossing).Cancelled,"crossing HUD or leaving viewport cancels lasso");
            Check(Feed(gesture,Finger(140,100,up:true)).Kind==MobileGesture.Kind.None,"release over HUD cannot confirm lasso or activate button");
            Feed(gesture,lassoPress);Feed(gesture,Finger(130,100));gesture.CancelUntilRelease();
            Check(Feed(gesture,Finger(140,100,up:true)).Kind==MobileGesture.Kind.None,"focus cancellation cannot complete existing lasso");
            Feed(gesture,lassoPress);Feed(gesture,Pair());var uiPinch=Pair(140,240);uiPinch.OverUi=true;
            Check(Feed(gesture,uiPinch).Cancelled,"camera gesture also stops when its primary finger enters HUD");
            Feed(gesture,Finger(up:true));Feed(gesture,lassoPress);var replaced=Finger(300,300);replaced.Finger=9;
            Check(Feed(gesture,replaced).Cancelled,"replacement contact cannot jump or complete an earlier finger's lasso");

            gesture=new MobileGesture();Feed(gesture,ordinaryPress);var lateShift=Finger(130,100);lateShift.Shift=true;lateShift.LassoSelection=true;
            Check(Feed(gesture,lateShift).Kind==MobileGesture.Kind.Pan,"pressing Shift after a pan starts cannot change it into selection");
            var lateRelease=Finger(130,100,up:true);lateRelease.Shift=true;
            Check(!Feed(gesture,lateRelease).Shift,"modifier is captured at initial contact only");

            CheckShiftRouting();
            CheckTapIntent();
            CheckLassoGeometry();

            Console.WriteLine($"Mobile input checks passed: {checks} (shared gesture classifier, no Unity/device rendering implied).");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static MobileTouchRouter.Contact Contact(int id,float x=100,float y=100,bool down=false,bool up=false,bool shift=false)
        =>new MobileTouchRouter.Contact{Id=id,X=x,Y=y,Held=!up,Down=down,Up=up,OnShift=shift};

    private static void CheckShiftRouting()
    {
        var router=new MobileTouchRouter();
        var sample=router.Read(new[]{Contact(5,down:true,shift:true)},1,true);
        Check(router.ShiftActive&&sample.Count==0&&!sample.Down,"Shift button contact is active without becoming a map/UI press");
        sample=router.Read(new[]{Contact(5,up:true,shift:true)},1.1f,true);
        Check(router.ShiftActive&&sample.Count==0&&!sample.Up,"Shift tap latches without emitting a map release");
        router.Read(new[]{Contact(5,down:true,shift:true)},2,true);
        router.Read(new[]{Contact(5,up:true,shift:true)},2.1f,true);
        Check(!router.ShiftActive,"second Shift tap disables the latch");

        router.Read(new[]{Contact(5,down:true,shift:true)},3,true);
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,250,300,down:true)},3.1f,true);
        Check(sample.Count==1&&sample.Finger==8&&sample.X==250&&sample.Down&&sample.Shift,"held Shift plus map finger produces a shifted single contact at map coordinates");
        var gesture=new MobileGesture();sample.AllowMap=true;sample.LassoSelection=true;Feed(gesture,sample);
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,280,300)},3.2f,true);
        sample.AllowMap=true;sample.LassoSelection=true;
        Check(Feed(gesture,sample).Kind==MobileGesture.Kind.Lasso,"held Shift plus map drag enters lasso, never pinch");
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,280,330,up:true)},3.3f,true);
        sample.AllowMap=true;sample.LassoSelection=true;
        var released=Feed(gesture,sample);
        Check(sample.Up&&sample.Count==0&&sample.X==280&&sample.Y==330&&released.Kind==MobileGesture.Kind.Lasso&&released.Up,"map finger release completes lasso while Shift finger remains held");
        router.Read(new[]{Contact(5,up:true,shift:true)},3.4f,true);
        Check(!router.ShiftActive,"momentary Shift does not latch after being used with another finger");

        router.ShiftActive=true;
        router.Read(new[]{Contact(5,down:true,shift:true),Contact(8,250,300,down:true)},4,true);
        router.Read(new[]{Contact(5,up:true,shift:true),Contact(8,250,300,up:true)},4.1f,true);
        Check(router.ShiftActive,"simultaneous modifier use preserves an existing latch");
        router.ResetShift();
        router.Read(new[]{Contact(5,down:true,shift:true)},5,true);
        router.Read(new[]{Contact(5,200,200,up:true)},5.1f,true);
        Check(!router.ShiftActive,"sliding off Shift before releasing does not toggle it");
        var cancelled=Contact(5,up:true,shift:true);cancelled.Cancelled=true;
        router.Read(new[]{Contact(5,down:true,shift:true)},6,true);
        sample=router.Read(new[]{cancelled},6.1f,true);
        Check(sample.Cancelled&&!router.ShiftActive,"OS-cancelled Shift never toggles and cancels dependent gestures");
        router.ShiftActive=true;
        router.Read(Array.Empty<MobileTouchRouter.Contact>(),7,false);
        Check(!router.ShiftActive,"hidden or disabled Shift clears its state on menus or blocked gameplay");

        router.Read(new[]{Contact(5,down:true,shift:true)},8,true);
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,250,300,down:true),Contact(9,350,300,down:true)},8.1f,true);
        Check(sample.Count==2&&sample.Finger==8&&sample.OtherFinger==9,"Shift is excluded while two real map fingers remain available for pinch");
        sample=router.Read(new[]{Contact(9,360,300),Contact(5,shift:true),Contact(8,240,300)},8.2f,true);
        Check(sample.Finger==8&&sample.OtherFinger==9&&sample.X==240,"device contact order cannot switch the logical primary finger");
        router.Read(Array.Empty<MobileTouchRouter.Contact>(),8.3f,true);
        sample=router.Read(new[]{Contact(8,200,250,down:true,up:true)},9,true);
        Check(sample.Down&&sample.Up&&sample.Count==0&&sample.X==200,"very quick map tap preserves both edges and final coordinates");
    }

    private static void CheckTapIntent()
    {
        Check(MobileGesture.ResolveTap(false,false,false,false,false,true)==MobileGesture.TapAction.Select,"tap with no selection selects a friendly target");
        Check(MobileGesture.ResolveTap(true,false,false,false,false,true)==MobileGesture.TapAction.Select,"Shift without selected units can still select a target");
        Check(MobileGesture.ResolveTap(false,false,false,true,true,false)==MobileGesture.TapAction.Context,"selected units tap ground or enemy issues contextual order");
        Check(MobileGesture.ResolveTap(false,false,false,true,true,true)==MobileGesture.TapAction.Context,"selected units tap ally orders movement or worker repair instead of replacing selection");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,false)==MobileGesture.TapAction.AttackMove,"Shift tap with units attacks along route to tapped point");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,true)==MobileGesture.TapAction.AttackMove,"Shift tap ally remains an attack-move destination");
        Check(MobileGesture.ResolveTap(true,false,true,true,true,false)==MobileGesture.TapAction.Context,"explicit build ability or rally targeting has priority over Shift");
        Check(MobileGesture.ResolveTap(false,false,false,false,true,true)==MobileGesture.TapAction.Select,"building-only selection can switch to another friendly target");
        Check(MobileGesture.ResolveTap(false,false,false,false,true,false)==MobileGesture.TapAction.Context,"building-only selection retains ground rally command");
        Check(MobileGesture.ResolveTap(false,true,false,false,true,true)==MobileGesture.TapAction.Context,"held tap retains explicit context order on friendly target");
    }

    private static void CheckLassoGeometry()
    {
        var lasso=new ScreenLasso();lasso.Begin(0,0,1);lasso.Add(100,0);lasso.Add(100,100);lasso.Add(0,100);
        Check(lasso.IsValid(24),"open four-point outline automatically closes as a valid lasso");
        Check(lasso.Contains(50,50),"enclosed center is selected");
        Check(lasso.Contains(0,50)&&lasso.Contains(100,0),"centers on closing edge and vertex are included");
        Check(!lasso.Contains(110,50)&&!lasso.Contains(-10,50),"outside centers are excluded");
        lasso.Add(0,20);
        Check(lasso.IsValid(24)&&lasso.Contains(50,50),"roughly closed circle need not end on first pixel");

        lasso.Begin(0,0,1);lasso.Add(100,0);lasso.Add(100,100);lasso.Add(70,100);lasso.Add(70,30);
        lasso.Add(30,30);lasso.Add(30,100);lasso.Add(0,100);
        Check(lasso.IsValid(24),"concave lasso remains valid");
        Check(lasso.Contains(15,70)&&lasso.Contains(50,15),"concave arms and base select their contained centers");
        Check(!lasso.Contains(50,70),"bounding-box center outside actual concave polygon is excluded");

        lasso.Begin(0,0,1);lasso.Add(0,100);lasso.Add(100,100);lasso.Add(100,0);
        Check(lasso.IsValid(24)&&lasso.Contains(50,50),"clockwise and counterclockwise circles select equally");
        lasso.Begin(1000,1800,1);lasso.Add(1100,1800);lasso.Add(1100,1900);lasso.Add(1000,1900);
        Check(lasso.Contains(1050,1850)&&!lasso.Contains(50,50),"screen coordinates work away from the origin");

        lasso.Begin(10,10,1);lasso.Add(14,14);lasso.Add(8,15);
        Check(!lasso.IsValid(24),"tap jitter cannot clear or replace selection");
        lasso.Begin(0,0,1);lasso.Add(100,100);lasso.Add(200,200);
        Check(!lasso.IsValid(24),"diagonal swipe is not a lasso despite wide bounds");
        lasso.Begin(0,0,1);lasso.Add(500,500);lasso.Add(499,501);lasso.Add(0,2);
        Check(!lasso.IsValid(24),"long narrow finger wobble cannot select a strip");
        lasso.Begin(10,10,1);lasso.Add(float.NaN,20);lasso.Add(20,float.PositiveInfinity);
        Check(lasso.Points.Count==1&&!lasso.Contains(float.NaN,10),"invalid coordinates never poison the path or selection");
        lasso.Clear();Check(lasso.Points.Count==0&&!lasso.IsValid(24),"cancellation removes the full visible path");

        lasso.Begin(10000,5000,1);
        for(int i=1;i<=4000;i++)
        {
            double angle=i*Math.PI*2/4000;
            lasso.Add((float)(5000+5000*Math.Cos(angle)),(float)(5000+5000*Math.Sin(angle)),i==4000);
        }
        Check(lasso.Points.Count<=ScreenLasso.MaximumPoints,"long gesture has bounded memory and overlay mesh size");
        Check(lasso.Points[0].X==10000&&lasso.IsValid(24)&&lasso.Contains(5000,5000),"resampling preserves long-circle origin and enclosed center");
    }
}
