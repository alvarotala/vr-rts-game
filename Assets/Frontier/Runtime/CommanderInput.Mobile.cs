#if FRONTIER_BROWSER
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class CommanderInput
    {
        private readonly MobileGesture mobileGesture=new MobileGesture();
        private object mobileSession;

        private void RouteMobileInput()
        {
            var sample=BrowserMobileInput.Read();
            if(!ReferenceEquals(mobileSession,Session.SessionIdentity))
            {
                mobileSession=Session.SessionIdentity;
                if(mobileGesture.Captured)CancelMobileGesture();
                BrowserMobileInput.AreaSelection=false;ResetMapGestures();
            }
            var position=new Vector2(sample.X,sample.Y);
            var ray=Quest.Camera.ScreenPointToRay(position);
            if(!Application.isFocused||Session.HasSystemPause)
            {CancelMobileGesture();return;}
            if((Session.IsCountingDown||countdownReleasePending)&&BlockCountdownInput(sample.Count>0))
            {mobileGesture.CancelUntilRelease();return;}
            if(HotkeyInput.Read("key:Escape").Down)
            {
                if(Panel.AcceptsGameInput&&Session.HasTargetingMode)Session.CancelMode();else Panel.Back();
                CancelMobileGesture();return;
            }
            bool mapAllowed=Panel.AcceptsGameInput||Panel.AcceptsReplayInput||Panel.Page==CommandPanel.MenuPage.MatchSetup;
            sample.OverUi=Panel.ContainsPointer(ray)||!Quest.Camera.pixelRect.Contains(position);
            sample.AllowMap=mapAllowed&&!Panel.SectionHelpOpen;
            var secondPosition=new Vector2(sample.OtherX,sample.OtherY);
            sample.SecondOverMap=Quest.Camera.pixelRect.Contains(secondPosition)&&!Panel.BrowserContainsScreenPoint(secondPosition);
            sample.AreaSelection=BrowserMobileInput.AreaSelection&&Panel.AcceptsGameInput&&!Session.HasTargetingMode;
            // A physical threshold remains usable at both browser CSS resolution
            // and native high-density resolutions; absurd/missing DPI is clamped.
            float threshold=Mathf.Clamp(Screen.dpi>0?Screen.dpi*.075f:12,10,36);
            var result=mobileGesture.Step(sample,threshold);
            if(result.Cancelled)
            {Panel.CancelPointerInteraction(false);panelButton=PanelButton.None;panelReleasePending=false;ClearPointer();BrowserMobileInput.AreaSelection=false;}
            if(result.Kind==MobileGesture.Kind.Ui)
            {
                RoutePanel(ray,result.Down,result.Up,result.Held,false,false,false,0);
                ClearPointer();return;
            }
            if(result.Kind==MobileGesture.Kind.Pinch||result.Kind==MobileGesture.Kind.Pan)
            {
                ClearPointer();
                Quest.Navigation?.NavigateTouch(new Vector2(result.DeltaX,result.DeltaY),result.Kind==MobileGesture.Kind.Pinch?result.ZoomRatio:1);
                return;
            }
            if(result.Kind==MobileGesture.Kind.Area)
            {
                PointBoard(ray,result.Down,result.Up,false,false);
                if(result.Up)BrowserMobileInput.AreaSelection=false;
                return;
            }
            if((result.Kind==MobileGesture.Kind.Tap||result.Kind==MobileGesture.Kind.LongPress)&&Panel.AcceptsGameInput&&!sample.OverUi)
            {TapMobileBoard(ray,result.Kind==MobileGesture.Kind.LongPress);return;}
            if(mobileGesture.Captured&&mapAllowed&&!sample.OverUi)PointReplayBoard(ray);
            else
            {
                // Removing the hover does not erase double-tap timing between
                // fingers, unlike ClearPointer which cancels an entire gesture.
                Board.Pointer=null;Board.Hovered=Board.HoveredResource=0;
            }
        }
        private void TapMobileBoard(Ray ray,bool forceOrder)
        {
            if(!Board.RayToMap(ray,out var point,out var hit))return;
            Board.ResolvePointerTarget(ray,point,hit,out int entity,out int resource);
            bool own=entity>0&&Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>e.id==entity&&e.owner==Session.LocalPlayer&&!e.remembered&&e.health>0);
            bool ownSelection=Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>Session.Selection.Contains(e.id)&&e.owner==Session.LocalPlayer&&!e.remembered&&e.health>0);
            // Holding and releasing explicitly orders even on a friendly target:
            // workers can repair/resume structures without replacing selection.
            if(forceOrder||Session.HasTargetingMode||ownSelection&&!own)
            {PointBoard(ray,false,false,true,false);return;}
            // Both edges are delivered together only after a confirmed tap, so
            // dragging the camera cannot select or issue a targeting-mode order.
            PointBoard(ray,true,true,false,false);
        }
        private void CancelMobileGesture()
        {
            mobileGesture.CancelUntilRelease();BrowserMobileInput.AreaSelection=false;
            Panel?.CancelPointerInteraction(false);panelButton=PanelButton.None;panelReleasePending=false;ClearPointer();
        }
    }
}
#endif
