using System;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private string RankingSummary
        {
            get
            {
                var profile=Session.Ranking?.profile;
                if(profile==null)return Session.IsLoadingRanking?"Loading your rating…":"Your rating appears here after connecting to the service.";
                if(profile.games==0)return $"Rating {profile.rating} · Unranked";
                return profile.rank>0?$"Rating {profile.rating} · Rank #{profile.rank}":$"Rating {profile.rating} · Position updating…";
            }
        }

        private void BuildRanking()
        {
            var entries=Session.Ranking?.entries;
            if(entries==null||entries.Length==0)
                Label(actions,Session.IsLoadingRanking?"Loading ranking…":Session.RankingFailed?"Ranking is unavailable. Refresh to try again.":"No ranked players yet. Complete a public match to join the ranking.",
                    new Vector2(40,-88),new Vector2(960,132),25,MenuMuted,TextAnchor.MiddleCenter);
            else
                for(int i=0;i<Math.Min(10,entries.Length);i++)
                {
                    var entry=entries[i];if(entry==null)continue;
                    int column=i/5,row=i%5;
                    var rect=Section(actions,"Ranking entry "+entry.rank,new Vector2(column*528,-row*60),new Vector2(512,52),entry.isSelf?ConsoleTheme.Primary:ConsoleTheme.Card);
                    Label(rect,"#"+entry.rank,new Vector2(12,0),new Vector2(52,52),21,MenuMuted);
                    Label(rect,entry.isSelf?"You":RankingName(entry.displayName),new Vector2(74,0),new Vector2(310,52),23,ConsoleTheme.Ink);
                    Label(rect,entry.rating.ToString(),new Vector2(388,0),new Vector2(108,52),24,entry.isSelf?accent:ConsoleTheme.Ink,TextAnchor.MiddleRight).fontStyle=FontStyle.Bold;
                }
            MenuFooter("Back",()=>Show(MenuPage.OnlineSetup),0,512);
            MenuFooter("Refresh ranking",()=>_=Session.RefreshRankingAsync(),528,512,true);
        }

        private static string RankingName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Player";
            value=value.Replace("<","").Replace(">","").Replace("\n"," ").Replace("\r"," ");
            return value.Length<=22?value:value.Substring(0,21)+"…";
        }

        private void UpdateRankingHeader()
        {
            headline.text="RANKING";economy.text="PUBLIC 1V1 · TOP 10";selection.text=RankingSummary;
            var profile=Session.Ranking?.profile;
            harvesting.text=profile==null?"":$"{profile.games} games · {profile.wins} wins · {profile.losses} losses · {profile.draws} draws";
            status.text=Session.RankingStatus??"Only completed public matches affect your rating.";
        }

        private void UpdateMatchRatingHeader()
        {
            var result=Session.MatchRating;
            if(result?.status=="rated")
                harvesting.text=$"Rating: {result.ratingBefore} → {result.ratingAfter} ({(result.ratingDelta>0?"+":"")+result.ratingDelta})";
            else harvesting.text=Session.IsLoadingMatchRating?"UPDATING RATING":result?.status=="unrated"?"UNRATED MATCH":"RATING UPDATE PENDING";
            if(!string.IsNullOrEmpty(Session.MatchRatingStatus))status.text=Session.MatchRatingStatus;
        }
    }
}
