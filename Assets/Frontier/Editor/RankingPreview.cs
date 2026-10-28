using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class RankingPreview
    {
        [MenuItem("Frontier/Preview ranked multiplayer in Spanish")]
        public static void Run()
        {
            bool had=PlayerPrefs.HasKey(Localization.PreferenceKey);string saved=PlayerPrefs.GetString(Localization.PreferenceKey);
            try
            {
                Localization.SetPreference("es");
                using(var fixture=new UnifiedWheelFixture("Ranking"))
                {
                    typeof(QuestBridge).GetProperty("Active").SetValue(fixture.Quest,false);
                    fixture.Board.Placed=true;
                    fixture.Session.PublicBackendUrl=GameSession.DefaultPublicBackendUrl;
                    fixture.Session.RankingServiceFactory=()=>new PreviewService();
                    fixture.Session.RefreshRankingAsync().GetAwaiter().GetResult();
                    Capture(CommandPanel.MenuPage.OnlineSetup,"es-multiplayer");
                    Capture(CommandPanel.MenuPage.Ranking,"es-top10");
                    Capture(CommandPanel.MenuPage.OnlineConnection,"es-production-connection");
                    fixture.Session.StartMatch();
                    fixture.Session.Match.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});
                    typeof(GameSession).GetProperty("View").SetValue(fixture.Session,fixture.Session.Match.View(0));
                    typeof(GameSession).GetProperty("IsOnline").SetValue(fixture.Session,true);
                    typeof(GameSession).GetProperty("IsPublicMatch").SetValue(fixture.Session,true);
                    UnifiedWheelFixture.Call(fixture.Session,"BeginRankedMatch","preview-match");
                    fixture.Session.RefreshMatchRatingAsync().GetAwaiter().GetResult();
                    Capture(CommandPanel.MenuPage.Result,"es-rating-result");

                    void Capture(CommandPanel.MenuPage page,string name)
                    {
                        UnifiedWheelFixture.Call(fixture.Panel,"Show",page);
                        UnifiedWheelFixture.Call(fixture.Panel,"UpdateMenuText");
                        Canvas.ForceUpdateCanvases();
                        HarvestingPreview.SavePanel(fixture.Panel,"TestResults/Ranking/"+name+".png",
                            UnifiedWheelFixture.Field<PreviewRenderUtility>(fixture,"preview"));
                    }
                }
                Debug.Log("FRONTIER_RANKING_PREVIEW passed: 4 Spanish menu captures with text-fit checks; synthetic ranking data, no backend or headset proof");
            }
            finally
            {
                if(had)PlayerPrefs.SetString(Localization.PreferenceKey,saved);else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                PlayerPrefs.Save();Localization.ReloadPreference();
            }
        }

        private sealed class PreviewService:IRankingService
        {
            public Task<RankSnapshot> GetRankingAsync(string backendUrl,CancellationToken cancellation)=>Task.FromResult(new RankSnapshot
            {
                profile=new RankProfile{playerId="self",displayName="Player SELF",rating=1242,games=28,wins=18,losses=8,draws=2,rank=2},
                entries=Enumerable.Range(1,10).Select(rank=>new RankEntry{playerId="player-"+rank,displayName="Player "+(104920+rank),
                    rank=rank,rating=1274-rank*16,games=28,wins=18,losses=8,draws=2,isSelf=rank==2}).ToArray()
            });
            public Task<MatchRatingResult> GetMatchResultAsync(string backendUrl,string matchId,CancellationToken cancellation)
                =>Task.FromResult(new MatchRatingResult{matchId=matchId,status="rated",ratingBefore=1226,ratingAfter=1242,ratingDelta=16,opponentRating=1226});
        }
    }
}
