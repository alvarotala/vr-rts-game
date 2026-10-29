#if FRONTIER_BROWSER
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const float BrowserReplayDockHeight=256;
        private RectTransform browserReplayDock,browserReplayHeader,browserReplayStatistics;
        private Target browserReplayStatisticsButton;
        private bool browserReplayStatisticsOpen;

        // Reparent the existing replay widgets. Playback, seek, production and
        // both players' snapshots remain owned by the shared replay panel.
        private void ConfigureBrowserReplay()
        {
            var children=actions.Cast<Transform>().Where(child=>child.gameObject.activeSelf).ToArray();
            browserReplayDock=Section(actions,"Browser replay transport",Vector2.zero,new Vector2(1104,BrowserReplayDockHeight),ConsoleTheme.Background);
            browserReplayHeader=Section(actions,"Browser replay header",Vector2.zero,new Vector2(1576,48),ConsoleTheme.Background);
            Label(browserReplayHeader,"SPECTATOR REPLAY",new Vector2(16,0),new Vector2(320,48),21,ConsoleTheme.Ink).fontStyle=FontStyle.Bold;
            Label(browserReplayHeader,Session.Replay.Info.Header.mapTitle+" · "+HistoryDate(Session.Replay.Info.Header.startedUtc),
                new Vector2(352,0),new Vector2(900,48),18,ConsoleTheme.Muted);
            browserReplayStatistics=Section(actions,"Replay statistics",Vector2.zero,new Vector2(1040,546),Color.clear);
            foreach(var child in children)
                child.SetParent(child.name.StartsWith("Replay player ")?browserReplayStatistics:browserReplayDock,false);
            browserReplayStatisticsButton=ReplayButton("Replay statistics toggle","Statistics",0,0,260,()=>
            {
                browserReplayStatisticsOpen=!browserReplayStatisticsOpen;
                LayoutBrowserReplay(((RectTransform)canvas.transform).rect.size);
            },out _);
            browserReplayStatisticsButton.rect.SetParent(browserReplayDock,false);
            TopLeft(replayTimeline,new Vector2(32,-12),new Vector2(1040,70));
            PlaceBrowserReplayButton("Replay back 15s",32,88,140);
            PlaceBrowserReplayButton("Replay play / pause",184,88,160);
            PlaceBrowserReplayButton("Replay forward 15s",356,88,140);
            PlaceBrowserReplayButton("Replay restart",508,88,140);
            PlaceBrowserReplayButton("Replay full map",660,88,140);
            PlaceBrowserReplayButton("Replay statistics toggle",812,88,260);
            for(int i=0;i<replaySpeedButtons.Length;i++)
                PlaceBrowserReplayButton(replaySpeedButtons[i],144+i*100,144,90);
            PlaceBrowserReplayButton("Exit replay",852,144,220);
            var speed=browserReplayDock.Cast<Transform>().FirstOrDefault(child=>child.GetComponent<Text>()!=null);
            if(speed!=null)TopLeft((RectTransform)speed,new Vector2(32,-144),new Vector2(100,48));
            LayoutBrowserReplay(((RectTransform)canvas.transform).rect.size);
        }

        private void PlaceBrowserReplayButton(string name,float x,float y,float width)
        {PlaceBrowserReplayButton(targets.First(target=>target.rect.name==name),x,y,width);}

        private void PlaceBrowserReplayButton(Target target,float x,float y,float width)
        {
            TopLeft(target.rect,new Vector2(x,-y),new Vector2(width,48));
            var label=target.rect.GetComponentInChildren<Text>();
            TopLeft(label.rectTransform,new Vector2(8,0),new Vector2(width-16,48));label.fontSize=18;
        }

        private void LayoutBrowserReplay(Vector2 size)
        {
            if(browserReplayDock==null||!browserReplayDock.gameObject.activeInHierarchy)return;
            root.anchorMin=root.anchorMax=root.pivot=Vector2.one*.5f;
            root.anchoredPosition=Vector2.zero;root.localRotation=Quaternion.identity;
            root.localScale=Vector3.one;root.sizeDelta=size;root.GetComponent<Image>().color=Color.clear;
            TopLeft(actions,Vector2.zero,size);
            foreach(var text in new[]{headline,economy,selection,harvesting})text.gameObject.SetActive(false);
            consoleBrand.gameObject.SetActive(false);consoleBrandSmall.gameObject.SetActive(false);
            moveHandle.gameObject.SetActive(false);resetHandle.gameObject.SetActive(false);
            matchHud.gameObject.SetActive(false);browserHud.gameObject.SetActive(false);
            var safe=BrowserSafeArea(size);
            float dockScale=BrowserMobileInput.IsTouchMode?Mathf.Min(1,(safe.width-16)/1104):1;
            float dockX=safe.center.x-1104*dockScale*.5f,dockTop=size.y-safe.y-BrowserReplayDockHeight*dockScale-12;
            TopLeft(browserReplayDock,new Vector2(dockX,-dockTop),new Vector2(1104,BrowserReplayDockHeight));
            browserReplayDock.localScale=Vector3.one*dockScale;
            TopLeft(browserReplayHeader,new Vector2(safe.x+12,-(size.y-safe.yMax+12)),new Vector2(safe.width-24,48));
            float statisticsScale=Mathf.Min(.78f,Mathf.Min((safe.width-32)/1040,(dockTop-84)/546));
            TopLeft(browserReplayStatistics,new Vector2((size.x-1040*statisticsScale)*.5f,-72),new Vector2(1040,546));
            browserReplayStatistics.localScale=Vector3.one*statisticsScale;
            browserReplayStatistics.gameObject.SetActive(browserReplayStatisticsOpen);
            browserReplayStatisticsButton.normal=browserReplayStatisticsOpen?ConsoleTheme.Primary:button;
            if(hovered!=browserReplayStatisticsButton)browserReplayStatisticsButton.image.color=browserReplayStatisticsButton.normal;
            TopLeft(statusBanner,new Vector2(dockX+32*dockScale,-dockTop-200*dockScale),new Vector2(1040*dockScale,44*dockScale));
            TopLeft(status.rectTransform,Vector2.zero,statusBanner.sizeDelta);status.fontSize=15;status.alignment=TextAnchor.MiddleCenter;
            statusBanner.GetComponent<Image>().color=Color.clear;
            statusBanner.SetAsLastSibling();
            Quest.Navigation?.SetViewport(new Rect(safe.x/size.x,(safe.y+BrowserReplayDockHeight*dockScale+24)/size.y,safe.width/size.x,
                Mathf.Max(.2f,(safe.height-BrowserReplayDockHeight*dockScale-96)/size.y)));
        }

        private bool BrowserReplayContainsPointer(Ray ray)=>(BrowserMobileInput.HasPointer
            &&!Quest.Camera.pixelRect.Contains(BrowserMobileInput.PointerPosition))||Hit(browserReplayDock,ray,out _)
            ||Hit(browserReplayHeader,ray,out _)||Hit(browserReplayStatistics,ray,out _);
    }
}
#endif
