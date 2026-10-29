#if FRONTIER_BROWSER
using Frontier.Core;
using System.Linq;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class CommanderInput
    {
        private readonly MobileGesture mobileGesture=new MobileGesture();
        private readonly ScreenLasso mobileLasso=new ScreenLasso();
        private readonly System.Collections.Generic.List<int> mobileLassoSelection=new System.Collections.Generic.List<int>();
        private MobileLassoOverlay mobileLassoOverlay;
        private object mobileSession;
        private Rect mobileViewport;
        private Vector2 mobileScreenSize;

        private void RouteMobileInput()
        {
            if(!ReferenceEquals(mobileSession,Session.SessionIdentity))
            {
                mobileSession=Session.SessionIdentity;
                BrowserMobileInput.ResetShift();
                if(mobileGesture.Captured)CancelMobileGesture();
                ClearMobileLasso();ResetMapGestures();
            }
            var sample=BrowserMobileInput.Read(Panel.AcceptsGameInput&&Panel.BrowserShiftAvailable);
            var screenSize=new Vector2(Screen.width,Screen.height);
            if((mobileViewport!=Quest.Camera.pixelRect||mobileScreenSize!=screenSize)&&mobileGesture.Captured)CancelMobileGesture();
            mobileViewport=Quest.Camera.pixelRect;mobileScreenSize=screenSize;
            var position=new Vector2(sample.X,sample.Y);
            var ray=Quest.Camera.ScreenPointToRay(position);
            if(!Application.isFocused||Session.HasSystemPause)
            {BrowserMobileInput.ResetShift();CancelMobileGesture();return;}
            if((Session.IsCountingDown||countdownReleasePending)&&BlockCountdownInput(sample.Count>0))
            {mobileGesture.CancelUntilRelease();ClearMobileLasso();return;}
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
            sample.LassoSelection=Panel.AcceptsGameInput;
            // A physical threshold remains usable at both browser CSS resolution
            // and native high-density resolutions; absurd/missing DPI is clamped.
            float threshold=Mathf.Clamp(Screen.dpi>0?Screen.dpi*.075f:12,10,36);
            var result=mobileGesture.Step(sample,threshold);
            if(result.Cancelled)
            {Panel.CancelPointerInteraction(false);panelButton=PanelButton.None;panelReleasePending=false;ClearPointer();ClearMobileLasso();}
            if(result.Kind==MobileGesture.Kind.Ui)
            {
                RoutePanel(ray,result.Down,result.Up,result.Held,false,false,false,0);
                ClearPointer();return;
            }
            if(result.Kind==MobileGesture.Kind.Pinch||result.Kind==MobileGesture.Kind.Pan)
            {
                ClearPointer();ClearMobileLasso();
                Quest.Navigation?.NavigateTouch(new Vector2(result.DeltaX,result.DeltaY),result.Kind==MobileGesture.Kind.Pinch?result.ZoomRatio:1);
                return;
            }
            if(result.Kind==MobileGesture.Kind.Lasso)
            {
                ClearPointer();
                if(result.Down)mobileLasso.Begin(result.StartX,result.StartY,threshold*.3f);
                mobileLasso.Add(sample.X,sample.Y,result.Up);
                if(result.Up)
                {
                    if(mobileLasso.IsValid(threshold*2))SelectMobileLasso();
                    ClearMobileLasso();
                }
                else
                {
                    if(mobileLassoOverlay==null)mobileLassoOverlay=MobileLassoOverlay.Create(transform);
                    mobileLassoOverlay.Show(mobileLasso);
                }
                return;
            }
            if((result.Kind==MobileGesture.Kind.Tap||result.Kind==MobileGesture.Kind.LongPress)&&Panel.AcceptsGameInput&&!sample.OverUi)
            {TapMobileBoard(ray,result.Kind==MobileGesture.Kind.LongPress,result.Shift);return;}
            if(mobileGesture.Captured&&mapAllowed&&!sample.OverUi)PointReplayBoard(ray);
            else
            {
                // Removing the hover does not erase double-tap timing between
                // fingers, unlike ClearPointer which cancels an entire gesture.
                Board.Pointer=null;Board.Hovered=Board.HoveredResource=0;
            }
        }
        private void SelectMobileLasso()
        {
            mobileLassoSelection.Clear();
            if(Session.View==null)return;
            foreach(var entity in Session.View.entities)
            {
                if(entity.owner!=Session.LocalPlayer||entity.remembered||entity.health<=0
                    ||!Board.TryEntityVisualCenter(entity.id,out var center))continue;
                var screen=Quest.Camera.WorldToScreenPoint(center);
                var position=new Vector2(screen.x,screen.y);
                if(screen.z<=0||!Quest.Camera.pixelRect.Contains(position)||Panel.BrowserContainsScreenPoint(position))continue;
                if(mobileLasso.Contains(screen.x,screen.y))mobileLassoSelection.Add(entity.id);
            }
            Session.SelectVisibleEntities(mobileLassoSelection);
        }
        private void TapMobileBoard(Ray ray,bool forceOrder,bool shifted)
        {
            if(!Board.RayToMap(ray,out var point,out var hit))return;
            Board.ResolvePointerTarget(ray,point,hit,out int entity,out int resource);
            bool own=entity>0&&Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>e.id==entity&&e.owner==Session.LocalPlayer&&!e.remembered&&e.health>0);
            bool ownSelection=Session.View!=null&&System.Array.Exists(Session.View.entities,
                e=>Session.Selection.Contains(e.id)&&e.owner==Session.LocalPlayer&&!e.remembered&&e.health>0);
            var action=MobileGesture.ResolveTap(shifted,forceOrder,Session.HasTargetingMode,Session.SelectedMobileUnits.Any(),ownSelection,own);
            if(action!=MobileGesture.TapAction.Select)
            {
                // Touch orders are explicit: repeated normal taps remain moves,
                // while Shift always attacks along the route to the tapped point.
                // Explicit building/ability/rally targeting retains its priority.
                ResetMapGestures();Board.Pointer=point;Board.Hovered=entity;Board.HoveredResource=resource;
                if(action==MobileGesture.TapAction.AttackMove)Session.Send(CommandKind.AttackMove,position:point);
                else Session.Context(point,entity,resource);
                return;
            }
            // Both edges are delivered together only after a confirmed tap, so
            // dragging the camera cannot select or issue a targeting-mode order.
            PointBoard(ray,true,true,false,false);
        }
        private void CancelMobileGesture()
        {
            mobileGesture.CancelUntilRelease();ClearMobileLasso();
            Panel?.CancelPointerInteraction(false);panelButton=PanelButton.None;panelReleasePending=false;ClearPointer();
        }
        private void ClearMobileLasso(){mobileLasso.Clear();mobileLassoOverlay?.Clear();}
        private void DisposeMobileLasso()
        {
            if(mobileLassoOverlay==null)return;
            if(Application.isPlaying)Destroy(mobileLassoOverlay.gameObject);else DestroyImmediate(mobileLassoOverlay.gameObject);
            mobileLassoOverlay=null;
        }
    }
}
#endif
