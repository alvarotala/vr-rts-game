using System;
using System.Globalization;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private enum ResultTab { Overview, Graphs, BuildOrder }
        private ResultTab resultTab;
        private ReportMetric resultMetric;
        private int resultBuildPlayer,resultBuildPage;
        private MatchReport shownReport;
        private int ReportPlayer=>Page==MenuPage.HistoryReport?0:Session.LocalPlayer;
        private string ReportOpponent=>Page!=MenuPage.HistoryReport&&Session.IsOnline?"OPPONENT":"AI";
        private static string ReportTime(int tick) { int seconds=tick/Simulation.TickRate;return $"{seconds/60:00}:{seconds%60:00}"; }
        private static string Stat(float value)=>value.ToString("N0",CultureInfo.InvariantCulture);

        private void ReportButton(string title,float x,float y,float width,Action click,bool selected=false)
        {
            var rect=Section(actions,title,new Vector2(x,-y),new Vector2(width,48),selected?ConsoleTheme.Primary:button);
            Label(rect,title,new Vector2(8,0),new Vector2(width-16,48),20,selected?accent:ConsoleTheme.Ink,TextAnchor.MiddleCenter).fontStyle=selected?FontStyle.Bold:FontStyle.Normal;
            targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=rect.GetComponent<Image>().color,click=click});
        }
        private void BuildMatchReport()
        {
            var report=Page==MenuPage.HistoryReport?archivedReport.Report:Session.ResultReport;
            if(report==null)
            {
                Label(actions,Session.OnlineReportUnavailable?"Final report unavailable":"Waiting for the final match report",new Vector2(24,-160),new Vector2(992,80),28,MenuMuted,TextAnchor.MiddleCenter);
                ReportButton("Main menu",0,668,336,()=>Session.LeaveMatch());
                if(Session.IsOnline)ReportButton("Multiplayer LAN",704,668,336,ReturnToOnlineSetup,true);
                return;
            }
            if(shownReport!=report)
            { shownReport=report;resultTab=ResultTab.Overview;resultMetric=ReportMetric.Workers;resultBuildPage=0;resultBuildPlayer=ReportPlayer; }
            var tabs=new[]{"Overview","Graphs","Build order"};
            for(int i=0;i<tabs.Length;i++)
            { var tab=(ResultTab)i;ReportButton(tabs[i],i*352,0,336,()=>{resultTab=tab;Rebuild();},resultTab==tab); }
            if(resultTab==ResultTab.Overview)BuildReportOverview(report);
            else if(resultTab==ResultTab.Graphs)BuildReportGraphs(report);
            else BuildReportOrder(report);
            if(Page==MenuPage.HistoryReport)
            {
                ReportButton("Back to history",0,668,336,()=>Show(MenuPage.History));
                ReportButton("Watch replay",352,668,336,()=>Quest.RequestReplay(archivedReport.Info.Id),true);
                ReportButton("Main menu",704,668,336,()=>{archivedReport=null;shownReport=null;Show(MenuPage.Main);});
            }
            else
            {
                ReportButton("Main menu",0,668,336,()=>Session.LeaveMatch());
                if(!Session.IsOnline&&Session.ArchiveFailed)ReportButton("Retry save",352,668,336,Session.RetryArchiveSave);
                if(Session.IsOnline)ReportButton("Multiplayer LAN",704,668,336,ReturnToOnlineSetup,true);
                else ReportButton("Rematch",704,668,336,Quest.RequestStartMatch,true);
            }
        }
        private void BuildReportOverview(MatchReport report)
        {
            var a=report.Players[ReportPlayer];var b=report.Players[1-ReportPlayer];
            Label(actions,"MATCH TOTALS",new Vector2(18,-68),new Vector2(430,30),18,ConsoleTheme.Ink);
            Label(actions,"YOU · "+ShortFaction(a.Faction),new Vector2(452,-68),new Vector2(280,30),21,MatchReportGraph.PlayerColor,TextAnchor.MiddleCenter);
            Label(actions,ReportOpponent+" · "+ShortFaction(b.Faction),new Vector2(746,-68),new Vector2(280,30),21,MatchReportGraph.OpponentColor,TextAnchor.MiddleCenter);
            int row=0;
            void Row(string title,string own,string enemy)
            {
                var rect=Section(actions,"Stat: "+title,new Vector2(0,-110-row++*44),new Vector2(1040,42),row%2==0?ConsoleTheme.Surface:ConsoleTheme.Card);
                Label(rect,title,new Vector2(18,0),new Vector2(430,42),20,ConsoleTheme.Ink);
                Label(rect,own,new Vector2(452,0),new Vector2(280,42),23,MatchReportGraph.PlayerColor,TextAnchor.MiddleCenter);
                Label(rect,enemy,new Vector2(746,0),new Vector2(280,42),23,MatchReportGraph.OpponentColor,TextAnchor.MiddleCenter);
            }
            Row("Workers · trained / lost / peak",$"{a.WorkersProduced} / {a.WorkersLost} / {a.PeakWorkers}",$"{b.WorkersProduced} / {b.WorkersLost} / {b.PeakWorkers}");
            Row("Army · trained / lost / peak",$"{a.ArmyProduced} / {a.ArmyLost} / {a.PeakArmy}",$"{b.ArmyProduced} / {b.ArmyLost} / {b.PeakArmy}");
            Row("Structures · completed / lost",$"{a.StructuresCompleted} / {a.StructuresLost}",$"{b.StructuresCompleted} / {b.StructuresLost}");
            Row("Minerals collected",Stat(a.MineralsCollected),Stat(b.MineralsCollected));
            Row("Gas collected",Stat(a.CatalystCollected),Stat(b.CatalystCollected));
            Row("Spent · minerals / gas",$"{Stat(a.MineralsSpent)} / {Stat(a.CatalystSpent)}",$"{Stat(b.MineralsSpent)} / {Stat(b.CatalystSpent)}");
            Row("Refunded · minerals / gas",$"{Stat(a.MineralsRefunded)} / {Stat(a.CatalystRefunded)}",$"{Stat(b.MineralsRefunded)} / {Stat(b.CatalystRefunded)}");
            Row("Final bank · minerals / gas",$"{Stat(a.Minerals)} / {Stat(a.Catalyst)}",$"{Stat(b.Minerals)} / {Stat(b.Catalyst)}");
            Row("APM · accepted orders / min",Stat(a.AverageApm),Stat(b.AverageApm));
            Row("Fastest burst · orders in 1 second",Stat(a.PeakActionsPerSecond),Stat(b.PeakActionsPerSecond));
            Label(actions,"Trained / completed exclude starting units and structures. Collected means delivered to a base.",new Vector2(18,-566),new Vector2(1004,28),17,MenuMuted);
            Label(actions,"APM counts one accepted order per action, including group orders. Automatic attacks and selection do not count.",new Vector2(18,-602),new Vector2(1004,48),17,MenuMuted,TextAnchor.UpperLeft);
        }
        private void BuildReportGraphs(MatchReport report)
        {
            var names=new[]{"Workers","Army","Minerals","Gas","Bank: M","Bank: G","APM"};
            for(int i=0;i<names.Length;i++)
            {var metric=(ReportMetric)i;ReportButton(names[i],i*150,64,140,()=>{resultMetric=metric;Rebuild();},resultMetric==metric);}
            Label(actions,"YOU ━",new Vector2(64,-122),new Vector2(250,30),20,MatchReportGraph.PlayerColor);
            Label(actions,ReportOpponent+" - - -",new Vector2(320,-122),new Vector2(300,30),20,MatchReportGraph.OpponentColor);
            var graphRect=new GameObject("Match comparison graph",typeof(RectTransform)).GetComponent<RectTransform>();graphRect.SetParent(actions,false);
            TopLeft(graphRect,new Vector2(80,-178),new Vector2(928,350));
            var graph=graphRect.gameObject.AddComponent<MatchReportGraph>();graph.Configure(report,resultMetric,ReportPlayer);
            for(int i=0;i<=4;i++)
            {
                Label(actions,Stat(graph.Maximum*(4-i)/4),new Vector2(0,-164-i*87.5f),new Vector2(70,28),17,MenuMuted,TextAnchor.MiddleRight);
                Label(actions,ReportTime(report.Tick*i/4),new Vector2(48+i*232,-536),new Vector2(76,28),17,MenuMuted,TextAnchor.MiddleCenter);
            }
            string description=resultMetric==ReportMetric.Workers?"Living workers over time.":resultMetric==ReportMetric.Army?"Living army units over time. Workers are excluded.":
                resultMetric==ReportMetric.MineralsCollected?"Total minerals delivered to a base. Starting resources and refunds are excluded.":
                resultMetric==ReportMetric.CatalystCollected?"Total gas delivered to a base. Refunds are excluded.":
                resultMetric==ReportMetric.Minerals?"Unspent minerals available at each moment.":resultMetric==ReportMetric.Catalyst?"Unspent gas available at each moment.":
                "Accepted orders per minute over the last 60 seconds (elapsed time during the first minute).";
            Label(actions,description,new Vector2(18,-580),new Vector2(1004,48),18,MenuMuted,TextAnchor.UpperLeft);
            Label(actions,"Match time · pauses excluded",new Vector2(640,-122),new Vector2(368,30),17,MenuMuted,TextAnchor.MiddleRight);
        }
        private void BuildReportOrder(MatchReport report)
        {
            for(int column=0;column<2;column++)
            {int side=column==0?ReportPlayer:1-ReportPlayer;ReportButton((column==0?"You · ":ReportOpponent+" · ")+ShortFaction(report.Players[side].Faction),column*528,64,512,()=>{resultBuildPlayer=side;resultBuildPage=0;Rebuild();},side==resultBuildPlayer);}
            var orders=report.BuildOrders[resultBuildPlayer];const int pageSize=7;
            int pages=Math.Max(1,(orders.Count+pageSize-1)/pageSize);resultBuildPage=Mathf.Clamp(resultBuildPage,0,pages-1);
            Label(actions,"TIME       SUPPLY     ORDER",new Vector2(18,-126),new Vector2(700,28),17,ConsoleTheme.Ink);
            Label(actions,"OUTCOME / TIME",new Vector2(758,-126),new Vector2(264,28),17,ConsoleTheme.Ink);
            for(int i=0;i<pageSize&&resultBuildPage*pageSize+i<orders.Count;i++)
            {
                var e=orders[resultBuildPage*pageSize+i];
                var rect=Section(actions,"Build order row",new Vector2(0,-162-i*54),new Vector2(1040,50));
                Label(rect,ReportTime(e.Tick),new Vector2(18,0),new Vector2(90,50),21,MenuMuted);
                Label(rect,e.Supply.ToString(),new Vector2(128,0),new Vector2(80,50),21,ConsoleTheme.Ink);
                string verb=e.Kind==CommandKind.Build?"Build":e.Kind==CommandKind.Research?"Research":"Train";
                Label(rect,verb+" · "+ReportDefinitionName(e.Definition),new Vector2(218,0),new Vector2(520,50),21,ConsoleTheme.Ink);
                string state=e.Status==BuildOrderStatus.Pending?(e.Kind==CommandKind.Build?"Unfinished":"In queue"):e.Status==BuildOrderStatus.Completed?"Ready":e.Status==BuildOrderStatus.Cancelled?"Cancelled":"Destroyed";
                Label(rect,state+(e.EndTick>=0?" · "+ReportTime(e.EndTick):""),new Vector2(758,0),new Vector2(264,50),19,e.Status==BuildOrderStatus.Completed?accent:MenuMuted);
            }
            if(orders.Count==0)Label(actions,"No construction, training or research orders this match.",new Vector2(40,-254),new Vector2(960,80),24,MenuMuted,TextAnchor.MiddleCenter);
            if(resultBuildPage>0)ReportButton("Previous",0,550,220,()=>{resultBuildPage--;Rebuild();});
            if(resultBuildPage+1<pages)ReportButton("Next",820,550,220,()=>{resultBuildPage++;Rebuild();});
            Label(actions,$"Page {resultBuildPage+1} / {pages} · {orders.Count} orders",new Vector2(240,-550),new Vector2(560,48),20,MenuMuted,TextAnchor.MiddleCenter);
            Label(actions,"Time = construction started or production queued. Supply includes reserved units. Starting units are excluded.",new Vector2(18,-614),new Vector2(1004,44),17,MenuMuted,TextAnchor.UpperLeft);
        }
    }
}
