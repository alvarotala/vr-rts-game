using TMPro;
using System;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private SavedMatchInfo[] savedMatches=Array.Empty<SavedMatchInfo>();
        private SavedMatchReport archivedReport;
        private int historyPage;
        private string historyMessage;
        private bool IsReportPage=>Page==MenuPage.Result||Page==MenuPage.HistoryReport;
        private void OpenHistory()
        {
            archivedReport=null;shownReport=null;historyMessage=null;historyPage=0;
            try {savedMatches=Session.History.List();}
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error))
            {savedMatches=Array.Empty<SavedMatchInfo>();historyMessage="Could not read match history. Check device storage.";}
            Show(MenuPage.History);
        }
        private static string HistoryDate(string utc)=>DateTimeOffset.TryParse(utc,out var date)?date.ToLocalTime().ToString("dd MMM yyyy · HH:mm"):"Unknown date";
        private static string HistoryOutcome(SavedMatchInfo info)
        {
            if(info.Error!=null)return "Unavailable";
            if(info.Progress.state==ArchivedMatchState.Interrupted)return "Interrupted";
            if(info.Progress.state==ArchivedMatchState.Abandoned)return "Abandoned";
            return info.Progress.winner==0?"Victory":info.Progress.winner==2?"Draw":"Defeat";
        }
        private void BuildHistory()
        {
            const int pageSize=5;int pages=Math.Max(1,(savedMatches.Length+pageSize-1)/pageSize);
            historyPage=Mathf.Clamp(historyPage,0,pages-1);
            for(int i=0;i<pageSize&&historyPage*pageSize+i<savedMatches.Length;i++)
            {
                var info=savedMatches[historyPage*pageSize+i];var h=info.Header;
                string title=$"{HistoryOutcome(info)} · {h?.mapTitle??"Saved match"} · {ReportTime(info.Progress?.tick??0)}";
                string description=h==null?"This record could not be read. Your other matches are still available.":
                    $"{HistoryDate(h.startedUtc)} · {ShortFaction(h.faction)} vs {ShortFaction(h.enemyFaction)} · {h.difficulty} AI";
                MenuCard(title,description,new Vector2(0,-i*96),new Vector2(852,84),()=>OpenSavedReport(info));
                if(info.Error==null&&info.Progress!=null)ReportButton("Watch replay",868,i*96+18,172,()=>Quest.RequestReplay(info.Id));
            }
            if(savedMatches.Length==0)Label(actions,"No matches recorded yet.\nYour next matches will appear here automatically.",new Vector2(40,-120),new Vector2(960,110),25,MenuMuted,TextAlignmentOptions.Center);
            if(historyPage>0)ReportButton("Previous",0,516,220,()=>{historyPage--;Rebuild();});
            if(historyPage+1<pages)ReportButton("Next",820,516,220,()=>{historyPage++;Rebuild();});
            Label(actions,$"Page {historyPage+1} / {pages}",new Vector2(240,-516),new Vector2(560,48),20,MenuMuted,TextAlignmentOptions.Center);
            ReportButton("Main menu",0,590,336,()=>{archivedReport=null;shownReport=null;Show(MenuPage.Main);});
        }
        private void OpenSavedReport(SavedMatchInfo info)
        {
            if(!info.HasReport)
            {
                historyMessage=info.Error!=null?"This record could not be read. Other matches remain available.":"This match was interrupted. Its recording is retained up to the last save; there is no final report.";
                UpdateMenuText();return;
            }
            try {archivedReport=Session.History.LoadReport(info.Id);historyMessage=null;Show(MenuPage.HistoryReport);}
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error))
            {historyMessage="This saved report could not be opened. Other matches remain available.";UpdateMenuText();}
        }
        private string ReportDefinitionName(string id)
        {
            var catalog=Page==MenuPage.HistoryReport?archivedReport.Catalog:Session.Catalog;
            return catalog.Has(id)?catalog[id].name:catalog.Upgrade(id)?.name??id;
        }
    }
}
