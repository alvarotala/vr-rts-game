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
            Check(Feed(gesture,held).Cancelled,"moving after a long hold cancels contextual order without moving camera");
            held=Finger(125,100,up:true);held.Time=12.1f;
            Check(Feed(gesture,held).Cancelled,"long hold drag release never emits contextual order");
            held=Finger(down:true);held.Time=13;Feed(gesture,held);
            held=Finger(up:true);held.Time=13.1f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Tap,"short tap after long hold remains an ordinary tap");
            held=Finger(down:true);held.Time=13.2f;Feed(gesture,held);
            held=Finger(up:true);held.Time=13.3f;
            Check(Feed(gesture,held).Kind==MobileGesture.Kind.Tap,"second short tap remains a separate confirmed tap");

            gesture=new MobileGesture();Feed(gesture,Finger(down:true));
            var drag=Feed(gesture,Finger(125,100));
            Check(drag.Kind==MobileGesture.Kind.None&&drag.Cancelled,"ordinary one-finger map swipe does not pan or select");
            Check(Feed(gesture,Finger(126,100,up:true)).Cancelled,"map swipe release cannot become a selection/order");

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
            Check(Feed(gesture,Finger(130,100)).Cancelled,"ordinary gameplay drag does not pan even when lasso selection is available");
            Check(Feed(gesture,Finger(130,100,up:true)).Cancelled,"ordinary swipe release never selects units");

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
            Check(Feed(gesture,lateShift).Cancelled,"pressing Shift after a map swipe starts cannot change it into selection");
            var lateRelease=Finger(130,100,up:true);lateRelease.Shift=true;
            Check(!Feed(gesture,lateRelease).Shift,"modifier is captured at initial contact only");

            CheckShiftRouting();
            CheckShiftModes();
            CheckCameraFooter();
            CheckTapIntent();
            CheckLassoGeometry();

            Console.WriteLine($"Mobile input checks passed: {checks} (shared gesture classifier, no Unity/device rendering implied).");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }

    private static void CheckCameraFooter()
    {
        MobileGesture.Sample Footer(bool down=false,bool up=false)
        {var sample=Finger(down:down,up:up);sample.OverUi=true;sample.CameraPanHandle=true;return sample;}
        var gesture=new MobileGesture();
        var result=Feed(gesture,Footer(down:true));
        Check(result.Kind==MobileGesture.Kind.Pan&&result.DeltaX==0,"footer owns contact immediately without a camera jump");
        result=Feed(gesture,Finger(105,103));
        Check(result.Kind==MobileGesture.Kind.Pan&&result.DeltaX==0&&result.DeltaY==0,"footer ignores finger jitter below drag threshold");
        result=Feed(gesture,Finger(130,110));
        Check(result.Kind==MobileGesture.Kind.Pan&&result.Down&&result.DeltaX==25&&result.DeltaY==7,"footer drag moves camera after crossing threshold outside the footer");
        var overButton=Finger(145,120);overButton.OverUi=true;
        result=Feed(gesture,overButton);
        Check(result.Kind==MobileGesture.Kind.Pan&&!result.Cancelled&&result.DeltaX==15,"footer retains ownership across HUD without clicking controls");
        result=Feed(gesture,Finger(145,120,up:true));
        Check(result.Kind==MobileGesture.Kind.Pan&&result.Up,"footer release cannot select or issue an order");
        Feed(gesture,Footer(down:true));result=Feed(gesture,Footer(up:true));
        Check(result.Kind==MobileGesture.Kind.Pan&&result.DeltaX==0,"footer tap is consumed without a gameplay action");
        var shifted=Footer(down:true);shifted.Shift=true;shifted.LassoSelection=true;
        Feed(gesture,shifted);
        Check(Feed(gesture,Finger(140,100)).Kind==MobileGesture.Kind.Pan,"Shift cannot turn footer navigation into a lasso");
        Check(Feed(gesture,Pair()).Cancelled,"second footer contact cancels instead of jumping into map pinch");
        Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"cancelled footer release is drained");
        Feed(gesture,Footer(down:true));var paused=Finger(140,100);paused.AllowMap=false;
        Check(Feed(gesture,paused).Cancelled,"pause or help cancels captured footer navigation");
        Feed(gesture,Finger(up:true));Feed(gesture,Footer(down:true));gesture.CancelUntilRelease();
        Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.None,"orientation or focus cancellation cannot send a footer release to map");
        Feed(gesture,Finger(down:true));result=Feed(gesture,Footer());
        Check(result.Cancelled&&result.Kind==MobileGesture.Kind.None,"a map contact cannot acquire the footer by sliding onto it");
        Feed(gesture,Finger(up:true));Feed(gesture,Finger(down:true));Feed(gesture,Finger(140,100));
        result=Feed(gesture,Pair(140,240));
        Check(result.Kind==MobileGesture.Kind.Pinch&&result.ZoomRatio==1,"adding second finger to suppressed map swipe starts navigation without a jump");
        result=Feed(gesture,Pair(160,260));
        Check(result.Kind==MobileGesture.Kind.Pinch&&result.DeltaX==20&&result.ZoomRatio==1,"two-finger translation pans without changing zoom");
        Feed(gesture,Finger(up:true));Feed(gesture,Finger(down:true));
        Check(Feed(gesture,Finger(up:true)).Kind==MobileGesture.Kind.Tap,"normal selection still works after camera gestures");
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
        Check(MobileGesture.ResolveTap(true,false,false,true,true,false,true)==MobileGesture.TapAction.Context,"queue mode on terrain keeps contextual movement instead of attack-move");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,true,true)==MobileGesture.TapAction.Context,"queue mode on a friendly building orders repair without replacing selection");
        Check(MobileGesture.ResolveTap(true,false,true,true,true,true,true)==MobileGesture.TapAction.Context,"queue mode respects explicit build and ability targeting");
        Check(MobileGesture.ResolveTap(true,false,false,false,false,true,true)==MobileGesture.TapAction.Select,"queue mode without units still allows selection");
        Check(MobileGesture.ResolveTap(false,false,false,false,false,true)==MobileGesture.TapAction.Select,"tap with no selection selects a friendly target");
        Check(MobileGesture.ResolveTap(true,false,false,false,false,true)==MobileGesture.TapAction.Select,"Shift without selected units can still select a target");
        Check(MobileGesture.ResolveTap(false,false,false,true,true,false)==MobileGesture.TapAction.Context,"selected units tap ground or enemy issues contextual order");
        Check(MobileGesture.ResolveTap(false,false,false,true,true,true)==MobileGesture.TapAction.Select,"ordinary tap on another friendly target replaces the current unit selection");
        Check(MobileGesture.ResolveTap(false,false,false,true,true,true,workerActionTarget:true)==MobileGesture.TapAction.Context,"workers tap an actionable friendly building to gather or repair without replacing selection");
        Check(MobileGesture.ResolveTap(false,false,false,false,false,true,workerActionTarget:true)==MobileGesture.TapAction.Select,"actionable building still selects when there are no workers selected");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,true,workerActionTarget:true)==MobileGesture.TapAction.AttackMove,"explicit attack mode takes priority over a worker building action");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,true,append:true,workerActionTarget:true)==MobileGesture.TapAction.Context,"queue mode retains the contextual worker action");
        Check(MobileGesture.ResolveTap(false,false,true,true,true,true,workerActionTarget:true)==MobileGesture.TapAction.Context,"explicit targeting retains priority over automatic worker actions");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,false)==MobileGesture.TapAction.AttackMove,"Shift tap with units attacks along route to tapped point");
        Check(MobileGesture.ResolveTap(true,false,false,true,true,true)==MobileGesture.TapAction.AttackMove,"Shift tap ally remains an attack-move destination");
        Check(MobileGesture.ResolveTap(true,false,true,true,true,false)==MobileGesture.TapAction.Context,"explicit build ability or rally targeting has priority over Shift");
        Check(MobileGesture.ResolveTap(false,false,false,false,true,true)==MobileGesture.TapAction.Select,"building-only selection can switch to another friendly target");
        Check(MobileGesture.ResolveTap(false,false,false,false,true,false)==MobileGesture.TapAction.Context,"building-only selection retains ground rally command");
        Check(MobileGesture.ResolveTap(false,true,false,false,true,true)==MobileGesture.TapAction.Context,"held tap retains explicit context order on friendly target");
        Check(MobileGesture.ResolveTap(false,true,false,false,false,true)==MobileGesture.TapAction.Select,"slow tap without selection still selects the friendly target");
        Check(MobileGesture.ResolveTap(false,true,true,false,false,true)==MobileGesture.TapAction.Context,"explicit targeting still wins over a slow tap with no selection");
        Check(MobileGesture.ResolveTap(false,true,false,true,true,true)==MobileGesture.TapAction.Context,"holding a friendly target with selected workers still orders repair or assist");
        Check(MobileGesture.ResolveTap(false,false,true,true,true,true)==MobileGesture.TapAction.Context,"explicit ability or construction targeting still takes priority on friendly targets");

        // Reproduce select A -> move on terrain -> tap B with real release
        // classification; issuing the movement must not prevent selecting B.
        var gesture=new MobileGesture();
        MobileGesture.TapAction Tap(float x,float time,bool selected,bool ownTarget)
        {
            var press=Finger(x,100,down:true);press.Time=time;Feed(gesture,press);
            var release=Finger(x,100,up:true);release.Time=time+.1f;
            var result=Feed(gesture,release);
            Check(result.Kind==MobileGesture.Kind.Tap,"selection/move sequence releases an ordinary tap");
            return MobileGesture.ResolveTap(result.Shift,result.Kind==MobileGesture.Kind.LongPress,false,selected,selected,ownTarget);
        }
        Check(Tap(100,20,false,true)==MobileGesture.TapAction.Select,"sequence selects unit A");
        Check(Tap(300,21,true,false)==MobileGesture.TapAction.Context,"sequence sends unit A to terrain");
        Check(Tap(200,22,true,true)==MobileGesture.TapAction.Select,"sequence selects unit B instead of moving A toward B");
    }

    private static void CheckShiftModes()
    {
        var router=new MobileTouchRouter();
        void Tap(float start,bool allowQueue=true)
        {
            var down=router.Read(new[]{Contact(5,down:true,shift:true)},start,true,allowQueue);
            var up=router.Read(new[]{Contact(5,up:true,shift:true)},start+.05f,true,allowQueue);
            Check(down.Count==0&&!down.Down&&up.Count==0&&!up.Up,"Shift taps never send map edges");
        }
        Tap(1);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Attack,"single Shift tap selects attack mode");
        Tap(1.15f);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Queue,"double Shift tap selects persistent contextual queue");
        var sample=router.Read(new[]{Contact(8,down:true)},1.3f,true);
        Check(sample.Append&&sample.Shift,"latched queue supplies append and lasso modifier to map contacts");
        router.Read(new[]{Contact(8,up:true)},1.4f,true);
        Tap(2);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"single tap leaves queue mode");
        Tap(3);Tap(3.6f);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"two separated taps toggle attack off instead of activating queue");
        Tap(4);
        router.Read(new[]{Contact(8,down:true)},4.1f,true);
        router.Read(new[]{Contact(8,up:true)},4.15f,true);Tap(4.2f);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"using the map between Shift taps invalidates the double-tap history");

        router.Read(new[]{Contact(5,down:true,shift:true)},5,true);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Queue,"held Shift gives immediate temporary queue feedback");
        router.Read(new[]{Contact(5,up:true,shift:true)},5.7f,true);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"unused long hold does not latch an attack or queue mode");
        Tap(6);
        router.Read(new[]{Contact(5,down:true,shift:true)},7,true);
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,down:true)},7.1f,true);
        Check(sample.Append,"held Shift appends tasks even when attack mode was latched");
        var gesture=new MobileGesture();sample.AllowMap=true;sample.LassoSelection=true;Feed(gesture,sample);
        sample=router.Read(new[]{Contact(5,up:true,shift:true),Contact(8)},7.2f,true);
        sample.AllowMap=true;sample.LassoSelection=true;Feed(gesture,sample);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Attack,"releasing held Shift restores its earlier attack mode");
        sample=router.Read(new[]{Contact(8,up:true)},7.25f,true);sample.AllowMap=true;
        var release=Feed(gesture,sample);
        Check(release.Kind==MobileGesture.Kind.Tap&&release.Append&&release.Shift,
            "map release keeps queue captured at press after the Shift finger is released");
        router.ResetShift();
        gesture=new MobileGesture();sample=router.Read(new[]{Contact(8,down:true)},8,true);sample.AllowMap=true;Feed(gesture,sample);
        sample=router.Read(new[]{Contact(8),Contact(5,down:true,shift:true)},8.1f,true);sample.AllowMap=true;Feed(gesture,sample);
        sample=router.Read(new[]{Contact(8,up:true),Contact(5,shift:true)},8.2f,true);sample.AllowMap=true;
        release=Feed(gesture,sample);
        Check(!release.Append&&!release.Shift,"pressing Shift halfway through a map tap cannot turn replacement into queued order");
        router.Read(new[]{Contact(5,up:true,shift:true)},8.3f,true);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"temporary queue returns to normal after use");

        Tap(9);Tap(9.15f);
        router.Read(Array.Empty<MobileTouchRouter.Contact>(),10,true,false);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Attack,"tutorial entry replaces queued mode with supported attack and lasso");
        router.ResetShift();Tap(11,false);Tap(11.15f,false);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"tutorial double tap cannot latch unavailable queue");
        router.Read(new[]{Contact(5,down:true,shift:true)},12,true,false);
        sample=router.Read(new[]{Contact(5,shift:true),Contact(8,down:true)},12.1f,true,false);
        Check(sample.Shift&&!sample.Append,"held Shift in tutorial preserves attack and lasso without appending");

        router.ResetShift();Tap(13);Tap(13.15f);
        router.Read(Array.Empty<MobileTouchRouter.Contact>(),14,false);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"menu and input blocking clear persistent queue");
        Tap(15);router.Read(new[]{Contact(5,down:true,shift:true)},15.1f,true);
        var cancelled=Contact(5,up:true,shift:true);cancelled.Cancelled=true;
        sample=router.Read(new[]{cancelled},15.2f,true);
        Check(sample.Cancelled&&router.Mode==MobileTouchRouter.ShiftMode.Attack,"cancelled second Shift tap cannot lock queue or lose the earlier mode");
        Tap(15.25f);
        Check(router.Mode==MobileTouchRouter.ShiftMode.Normal,"cancelled modifier clears double-tap timing");
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
