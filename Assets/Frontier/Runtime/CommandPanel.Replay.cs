using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const int ReplayProductionRows=13;
        private readonly Text[] replayMinerals=new Text[2],replayGas=new Text[2],replayWorkers=new Text[2],replayArmy=new Text[2],replaySupply=new Text[2],replayProductionTitle=new Text[2],replayEmptyProduction=new Text[2];
        private readonly RectTransform[,] replayProductionRows=new RectTransform[2,ReplayProductionRows];
        private readonly Text[,] replayProductionNames=new Text[2,ReplayProductionRows],replayProductionCounts=new Text[2,ReplayProductionRows],replayProductionProgress=new Text[2,ReplayProductionRows];
        private readonly RectTransform[,] replayProductionFills=new RectTransform[2,ReplayProductionRows];
        private readonly int[] replayProductionPage=new int[2];
        private readonly RectTransform[] replayProductionNavigation=new RectTransform[2];
        private readonly Text[] replayProductionPageLabel=new Text[2];
        private readonly Target[] replaySpeedButtons=new Target[4];
        private static readonly float[] ReplaySpeeds={.5f,1,2,4};
        private RectTransform replayTimeline,replayTimelineFill,replayTimelineMarker;
        private Text replayTime,replayState,replayPauseLabel;
        private bool replayScrubbing,replayScrubResume;
        private int replayScrubTick;
        private ReplayPlayback replayScrubSource;
        private PlayerView replayPanelView;
        public bool AcceptsReplayInput=>Page==MenuPage.Replay&&Session.IsReplay&&!Session.HasSystemPause&&!Quest.Placing;

        private Target ReplayButton(string name,string title,float x,float y,float width,Action click,out Text label)
        {
            var rect=Section(actions,name,new Vector2(x,-y),new Vector2(width,48),button);
            label=Label(rect,title,new Vector2(8,0),new Vector2(width-16,48),20,name=="Replay play / pause"?accent:ConsoleTheme.Ink,TextAnchor.MiddleCenter);
            var target=new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=click};targets.Add(target);return target;
        }
        private void BuildReplayPanel()
        {
            if(!Session.IsReplay)return;
            replayPanelView=null;
            for(int p=0;p<2;p++)
            {
                var tint=p==0?MatchReportGraph.PlayerColor:MatchReportGraph.OpponentColor;
                var card=Section(actions,"Replay player "+p,new Vector2(p*528,0),new Vector2(512,546));
                Label(card,(p==0?"YOU · ":"AI · ")+ShortFaction(Session.Match.Players[p].faction),new Vector2(16,-10),new Vector2(480,30),24,tint).fontStyle=FontStyle.Bold;
                Label(card,"MINERALS",new Vector2(16,-49),new Vector2(232,20),15,MineralTint);
                Label(card,"GAS",new Vector2(264,-49),new Vector2(232,20),15,GasTint);
                replayMinerals[p]=Label(card,"0",new Vector2(16,-69),new Vector2(232,40),30,MineralTint);
                replayGas[p]=Label(card,"0",new Vector2(264,-69),new Vector2(232,40),30,GasTint);
                replayMinerals[p].fontStyle=replayGas[p].fontStyle=FontStyle.Bold;
                replayWorkers[p]=Label(card,"Workers 0",new Vector2(16,-118),new Vector2(232,30),21,ConsoleTheme.Ink);
                replayArmy[p]=Label(card,"Army 0",new Vector2(264,-118),new Vector2(232,30),21,ConsoleTheme.Ink);
                replaySupply[p]=Label(card,"",new Vector2(16,-150),new Vector2(480,26),18,MenuMuted);
                replayProductionTitle[p]=Label(card,"PRODUCTION",new Vector2(16,-183),new Vector2(232,26),16,tint);
                Label(card,"active + queued   /   next ready",new Vector2(240,-183),new Vector2(256,26),14,MenuMuted,TextAnchor.MiddleRight);
                replayEmptyProduction[p]=Label(card,"No production in progress",new Vector2(16,-252),new Vector2(480,70),21,MenuMuted,TextAnchor.MiddleCenter);
                for(int row=0;row<ReplayProductionRows;row++)
                {
                    var line=Section(card,"Production type "+row,new Vector2(16,-215-row*21),new Vector2(480,21),Color.clear);
                    replayProductionRows[p,row]=line;
                    replayProductionNames[p,row]=Label(line,"",Vector2.zero,new Vector2(256,21),16,ConsoleTheme.Ink);
                    replayProductionCounts[p,row]=Label(line,"",new Vector2(260,0),new Vector2(76,21),16,tint,TextAnchor.MiddleCenter);
                    var track=Section(line,"Progress",new Vector2(344,-8),new Vector2(88,5),ConsoleTheme.Track);
                    replayProductionFills[p,row]=Section(track,"Fill",Vector2.zero,new Vector2(0,5),tint);
                    replayProductionProgress[p,row]=Label(line,"",new Vector2(438,0),new Vector2(42,21),15,MenuMuted,TextAnchor.MiddleRight);
                }
                int player=p;
                var navigation=Section(card,"Production pages "+p,new Vector2(16,-497),new Vector2(480,34),Color.clear);
                replayProductionNavigation[p]=navigation;
                void PageButton(string title,float x,int delta)
                {
                    var rect=Section(navigation,title,new Vector2(x,0),new Vector2(100,34),button);
                    Label(rect,title,new Vector2(4,0),new Vector2(92,34),16,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                    targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=()=>{replayProductionPage[player]+=delta;replayPanelView=null;UpdateReplayPanel();}});
                }
                PageButton("Previous",0,-1);PageButton("Next",380,1);
                replayProductionPageLabel[p]=Label(navigation,"",new Vector2(108,0),new Vector2(264,34),16,MenuMuted,TextAnchor.MiddleCenter);
            }
            replayTimeline=Section(actions,"Replay timeline",new Vector2(0,-558),new Vector2(1040,70),Color.clear);
            replayTime=Label(replayTimeline,"",Vector2.zero,new Vector2(510,30),23,ConsoleTheme.Ink);
            replayState=Label(replayTimeline,"",new Vector2(530,0),new Vector2(510,30),20,ConsoleTheme.Ink,TextAnchor.MiddleRight);
            Section(replayTimeline,"Timeline track",new Vector2(0,-38),new Vector2(1040,24),ConsoleTheme.Track);
            replayTimelineFill=Section(replayTimeline,"Timeline played",new Vector2(0,-38),new Vector2(0,24),accent);
            replayTimelineMarker=Section(replayTimeline,"Timeline playhead",new Vector2(0,-34),new Vector2(4,32),ConsoleTheme.Ink);
            ReplayButton("Replay back 15s","−15 seconds",0,642,170,()=>Session.SeekReplay(ReplayTransportTick-Simulation.TickRate*15),out _);
            ReplayButton("Replay play / pause","Pause",182,642,220,Session.ToggleReplayPause,out replayPauseLabel);
            ReplayButton("Replay forward 15s","+15 seconds",414,642,170,()=>Session.SeekReplay(ReplayTransportTick+Simulation.TickRate*15),out _);
            ReplayButton("Replay restart","Restart",596,642,200,()=>Session.SeekReplay(0),out _);
            ReplayButton("Replay full map","Full map",808,642,232,()=>
            {
#if FRONTIER_BROWSER
                Quest.Navigation.Fit();
#else
                Board.Fit();
#endif
            },out _);
            Label(actions,"SPEED",new Vector2(0,-708),new Vector2(100,48),18,ConsoleTheme.Ink);
            for(int i=0;i<ReplaySpeeds.Length;i++)
            {
                float speed=ReplaySpeeds[i];string title=speed==.5f?"0.5×":speed+"×";
                replaySpeedButtons[i]=ReplayButton("Replay speed "+speed,title,112+i*130,708,118,()=>Session.SetReplaySpeed(speed),out _);
            }
            ReplayButton("Exit replay","Back to history",704,708,336,Session.StopReplay,out _);
#if FRONTIER_BROWSER
            ConfigureBrowserReplay();
#endif
            UpdateReplayPanel();
        }
        private sealed class ReplayProductionGroup
        {
            public string key,name;
            public int active,queued;
            public float progress;
        }
        private int ReplayTransportTick=>Session.Replay.Seeking?Session.Replay.SeekTarget:Session.Replay.Tick;
        private void UpdateReplayPanel()
        {
            if(!Session.IsReplay||replayTime==null)return;
            // The same published snapshots drive the board and the economy. Never read
            // the mutable simulation while it is reconstructing an intermediate seek tick.
            bool refresh=!ReferenceEquals(replayPanelView,Session.View);replayPanelView=Session.View;
            for(int p=0;refresh&&p<2;p++)
            {
                // Omniscient data belongs exclusively to recorded spectator sessions.
                var view=Session.ReplayPlayerViews[p];
                var entities=view.entities.Where(e=>e.owner==p&&e.health>0).ToArray();
                int workers=entities.Count(e=>Session.Catalog[e.definition].role=="worker"),army=entities.Count(e=>!Session.Catalog[e.definition].building&&Session.Catalog[e.definition].role!="worker");
                replayMinerals[p].text=Stat(view.minerals);replayGas[p].text=Stat(view.catalyst);
                replayWorkers[p].text="Workers  "+workers;replayArmy[p].text="Army  "+army;
                replaySupply[p].text=$"Supply {view.supply} / {view.capacity}    ·    Structures {entities.Count(e=>Session.Catalog[e.definition].building&&e.complete)}";
                var groups=new Dictionary<string,ReplayProductionGroup>();
                void Add(string key,string name,bool active,float progress)
                {
                    if(!groups.TryGetValue(key,out var group)){group=new ReplayProductionGroup{key=key,name=name};groups.Add(key,group);}
                    if(active){group.active++;group.progress=Mathf.Max(group.progress,Mathf.Clamp01(progress));}else group.queued++;
                }
                foreach(var entity in entities)
                {
                    var type=Session.Catalog[entity.definition];
                    if(type.building&&!entity.complete)
                        Add("B:"+type.id,"Build · "+type.name,true,entity.progress);
                    for(int q=0;q<entity.queue.Length;q++)
                    {
                        var order=entity.queue[q];
                        Add((order.research?"R:":"T:")+order.definition,(order.research?"Research · ":"Train · ")+QueueLabel(order.definition),q==0,1-order.remaining/(float)Math.Max(1,order.total));
                    }
                }
                var ordered=groups.Values.OrderBy(g=>g.key,StringComparer.Ordinal).ToArray();
                int pages=Math.Max(1,(ordered.Length+ReplayProductionRows-1)/ReplayProductionRows);
                replayProductionPage[p]=Mathf.Clamp(replayProductionPage[p],0,pages-1);
                replayProductionNavigation[p].gameObject.SetActive(pages>1);
                replayProductionPageLabel[p].text=$"{replayProductionPage[p]+1} / {pages} · {ordered.Length} types";
                replayProductionTitle[p].text="PRODUCTION · "+ordered.Sum(g=>g.active+g.queued);
                replayEmptyProduction[p].gameObject.SetActive(ordered.Length==0);
                for(int row=0;row<ReplayProductionRows;row++)
                {
                    int index=replayProductionPage[p]*ReplayProductionRows+row;
                    bool visible=index<ordered.Length;replayProductionRows[p,row].gameObject.SetActive(visible);if(!visible)continue;
                    var group=ordered[index];
                    replayProductionNames[p,row].text=group.name;
                    replayProductionCounts[p,row].text=$"{group.active} + {group.queued}";
                    replayProductionFills[p,row].sizeDelta=new Vector2(88*group.progress,5);
                    replayProductionProgress[p,row].text=group.active>0?Mathf.FloorToInt(group.progress*100)+"%":"—";
                }
            }
            var replay=Session.Replay;int shownTick=replayScrubbing?replayScrubTick:replay.Tick;
            replayTime.text=ReportTime(shownTick)+" / "+ReportTime(replay.EndTick);
            string speedText=replay.Speed==.5f?"0.5×":replay.Speed+"×";
            replayState.text=replay.Error!=null?"REPLAY STOPPED":replayScrubbing?"RELEASE TO SEEK":replay.Seeking?"SEEKING · "+ReportTime(replay.SeekTarget):replay.Ended?"REPLAY FINISHED":(replay.Paused?"PAUSED":"PLAYING")+" · "+speedText;
            replayState.color=replay.Error!=null?WarningTint:ConsoleTheme.Ink;
            replayPauseLabel.text=replay.Ended?"Play again":replay.Paused?"Play":"Pause";
            float fraction=replay.EndTick>0?shownTick/(float)replay.EndTick:0;
            replayTimelineFill.sizeDelta=new Vector2(1040*fraction,24);
            replayTimelineMarker.anchoredPosition=new Vector2(Mathf.Clamp(1040*fraction,0,1036),-34);
            for(int i=0;i<replaySpeedButtons.Length;i++)
            {
                bool selected=Mathf.Approximately(replay.Speed,ReplaySpeeds[i]);
                var target=replaySpeedButtons[i];target.normal=selected?ConsoleTheme.Primary:button;
                if(hovered!=target)
                {
                    target.image.color=target.normal;
                    target.rect.GetComponentInChildren<Text>().color=selected?accent:ConsoleTheme.Ink;
                }
            }
        }
        private bool PointReplayTimeline(Ray ray,bool down,bool up)
        {
            if(!AcceptsReplayInput||replayTimeline==null)return false;
            bool inside=Hit(replayTimeline,ray,out float distance);
            if(!replayScrubbing)
            {
                if(!down||!inside)return false;
                replayScrubbing=true;replayScrubSource=Session.Replay;
                replayScrubResume=!Session.Replay.Paused&&!Session.Replay.Ended&&Session.Replay.Error==null;
                if(replayScrubResume)Session.ToggleReplayPause();
            }
            if(inside)
            {
#if FRONTIER_BROWSER
                RectTransformUtility.ScreenPointToLocalPointInRectangle(replayTimeline,BrowserMobileInput.PointerPosition,null,out var point);
#else
                var point=replayTimeline.InverseTransformPoint(ray.GetPoint(distance));
#endif
                replayScrubTick=Mathf.RoundToInt(Mathf.Clamp01(point.x/replayTimeline.rect.width)*Session.Replay.EndTick);
            }
            UpdateReplayPanel();
            if(up)
            {
                int tick=replayScrubTick;bool resume=replayScrubResume;replayScrubbing=false;replayScrubResume=false;replayScrubSource=null;
                Session.SeekReplay(tick);
                if(resume&&Session.Replay.Paused)Session.ToggleReplayPause();
            }
            return true;
        }
        private void CancelReplayScrub()
        {
            bool resume=replayScrubbing&&replayScrubResume&&Session.IsReplay&&Session.Replay==replayScrubSource&&Session.Replay.Paused;
            replayScrubbing=false;replayScrubResume=false;replayScrubSource=null;
            if(resume)Session.ToggleReplayPause();
        }
        private void UpdateReplayHeader()
        {
            if(!Session.IsReplay)return;
            headline.text="SPECTATOR REPLAY";economy.text=Session.Replay.Info.Header.mapTitle+" · "+HistoryDate(Session.Replay.Info.Header.startedUtc);
            selection.text="Full battlefield · Both players' resources and production are visible.";
            harvesting.text=Session.Replay.Info.Progress.state==ArchivedMatchState.Interrupted?"INTERRUPTED RECORDING · PLAYING THE SAVED PORTION":"RECORDED MATCH";
            status.fontSize=16;status.color=Session.Replay.Error!=null?WarningTint:ConsoleTheme.Ink;
            status.text=Session.Replay.Error??(Quest.Active
                ?$"{Bindings.MenuConfirmName}: play / pause · Stick left / right: ±15s · up / down: speed · {Bindings.MenuBackName}: history\nGrip + stick: pan · Grip + {Bindings.MenuConfirmName} / {Bindings.MenuBackName}: zoom · Trigger: point and select"
                :"Space: play / pause · Left / right: ±15s · Up / down: speed · Esc: history\nWASD / middle drag: pan · Scroll: zoom · Point at the timeline to seek");
            UpdateReplayPanel();
        }
    }
}
