using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class RankingPresentationChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

        [MenuItem("Frontier/Multiplayer/Check ranking presentation lifecycle")]
        public static void Run()
        {
            var previousContext=SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            var fixture=new GameObject("Ranking presentation checks");
            int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                session.PublicBackendUrl="http://192.168.50.2:7350";
                var provider=new FakeRanking();session.RankingServiceFactory=()=>provider;
                Task first=session.RefreshRankingAsync();
                Check(session.IsLoadingRanking&&!session.IsOnline&&!session.Running&&provider.OverviewCalls==1,
                    "Reading ranking does not enter or block a game session.");
                Check(session.RefreshRankingAsync().IsCompleted&&provider.OverviewCalls==1,"Repeated refresh coalesces while the same endpoint is pending.");
                var old=provider;session.PublicBackendUrl="http://192.168.50.3:7350";
                provider=new FakeRanking();Task replacement=session.RefreshRankingAsync();
                Check(old.OverviewToken.IsCancellationRequested,"Changing endpoint cancels its previous ranking request.");
                provider.Overview.SetResult(Snapshot(1210));old.Overview.SetResult(Snapshot(999));
                Check(first.IsCompleted&&replacement.IsCompleted&&session.Ranking.profile.rating==1210&&!session.IsLoadingRanking,
                    "A response from the previous endpoint cannot replace the current account's ranking.");

                provider=new FakeRanking();Task failed=session.RefreshRankingAsync();
                provider.Overview.SetException(new Exception("private-token-diagnostic"));
                Check(failed.IsCompleted&&!failed.IsFaulted&&session.RankingFailed&&!session.RankingStatus.Contains("private-token"),
                    "Ranking failures remain recoverable and never expose raw authentication diagnostics.");
                Check(!session.IsOnline&&!session.Running,"Unavailable ranking does not start or alter offline play.");

                provider=new FakeRanking();Task interrupted=session.RefreshRankingAsync();
                session.StartMatch();var offline=session.Match;
                provider.Overview.SetResult(Snapshot(1400));
                Check(interrupted.IsCompleted&&provider.OverviewToken.IsCancellationRequested&&session.Running&&session.Match==offline
                    &&session.Ranking.profile.rating==1210,"Starting offline invalidates a pending ranking response without affecting the match.");
                session.LeaveMatch();

                PrepareResult("first-result");provider=new FakeRanking();Task earlierResult=session.RefreshMatchRatingAsync();var earlier=provider;
                PrepareResult("replacement-result");provider=new FakeRanking();Task laterResult=session.RefreshMatchRatingAsync();
                provider.Overview.SetResult(Snapshot(1242));provider.Result.SetResult(Rated("replacement-result",1210,1242));
                earlier.Result.SetResult(Rated("first-result",1000,1016));
                Check(earlierResult.IsCompleted&&laterResult.IsCompleted&&session.MatchRating.matchId=="replacement-result"
                    &&session.MatchRating.ratingDelta==32,"A late result from a previous match cannot overwrite the current rating change.");
                Check(earlier.ResultToken.IsCancellationRequested&&session.Ranking.profile.rating==1242,
                    "Replacing a match cancels its rating request and a current result refreshes the profile.");

                PrepareResult("terminal-view");provider=new FakeRanking();
                var terminal=new PlayerView{player=0,winner=0};Call(session,"OnlineState",terminal);
                Check(provider.ResultCalls==1&&session.IsLoadingMatchRating,
                    "The terminal player view starts rating retrieval even when the report has not arrived.");
                Call(session,"OnlineFinished",(object)null);Call(session,"OnlineState",terminal);
                Check(provider.ResultCalls==1,"Finished and repeated terminal views cannot duplicate the automatic rating request.");
                Call(session,"DisconnectOnline",true);
                provider.Overview.SetResult(Snapshot(1258));provider.Result.SetResult(Rated("terminal-view",1242,1258));
                Check(session.MatchRating.matchId=="terminal-view"&&!session.IsLoadingMatchRating,
                    "A terminal-view rating request survives report loss and completed transport closure.");

                PrepareResult("transport-closed");provider=new FakeRanking();Task afterDisconnect=session.RefreshMatchRatingAsync();
                Call(session,"DisconnectOnline",true);
                provider.Overview.SetResult(Snapshot(1226));provider.Result.SetResult(Rated("transport-closed",1242,1226));
                Check(afterDisconnect.IsCompleted&&session.MatchRating.matchId=="transport-closed"&&session.MatchRating.ratingDelta==-16,
                    "Closing the completed NGO transport preserves its pending rating result.");

                PrepareResult("bad-result");provider=new FakeRanking();Task malformed=session.RefreshMatchRatingAsync();
                provider.Result.SetResult(Rated("other-result",1000,1020));
                Check(malformed.IsCompleted&&!malformed.IsFaulted&&session.MatchRating==null&&session.MatchRatingFailed,
                    "A rating result for the wrong match is rejected before display.");

                PrepareResult("pending-result");provider=new FakeRanking();provider.Result.SetResult(new MatchRatingResult{matchId="pending-result",status="pending"});
                Task pending=session.RefreshMatchRatingAsync();session.LeaveMatch();
                Check(pending.Wait(TimeSpan.FromSeconds(2))&&!pending.IsFaulted&&!session.IsLoadingMatchRating&&session.MatchRating==null,
                    "Leaving a result page cancels a pending polling delay and clears its display state.");

                provider=new FakeRanking();provider.Overview.SetResult(Snapshot(1242,true));session.RefreshRankingAsync().GetAwaiter().GetResult();
                var panel=fixture.AddComponent<CommandPanel>();panel.Session=session;
                var actionsObject=new GameObject("Ranking actions",typeof(RectTransform));actionsObject.transform.SetParent(fixture.transform);
                var actions=actionsObject.GetComponent<RectTransform>();actions.sizeDelta=new Vector2(1040,396);
                Set(panel,"actions",actions);Set(panel,"font",Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                Call(panel,"BuildRanking");
                var rows=actions.Cast<Transform>().Where(t=>t.name.StartsWith("Ranking entry ",StringComparison.Ordinal)).Cast<RectTransform>().ToArray();
                Check(rows.Length==10&&rows.Select(t=>t.anchoredPosition).Distinct().Count()==10,
                    "The top ten uses ten separate visible positions.");
                Check(rows.All(t=>t.anchoredPosition.x>=0&&t.anchoredPosition.x+t.sizeDelta.x<=1040
                    &&t.anchoredPosition.y<=0&&-t.anchoredPosition.y+t.sizeDelta.y<=292),
                    "Ranking rows fit inside the menu above its footer actions.");
                Check(actions.GetComponentsInChildren<LocalizedText>().Any(t=>t.Source=="You"),
                    "The player's own row is labelled without exposing the account identifier.");

                provider=new FakeRanking();Task destroyed=session.RefreshRankingAsync();Call(session,"OnDestroy");
                Object.DestroyImmediate(fixture);fixture=null;provider.Overview.SetResult(Snapshot(1700));
                Check(destroyed.IsCompleted&&!destroyed.IsFaulted&&provider.OverviewToken.IsCancellationRequested,
                    "Destroying a session cancels ranking reads and ignores their late completion.");
                Debug.Log($"FRONTIER_RANKING_PRESENTATION passed: {checks} checks; controlled service responses and menu bounds, no backend or headset proof");

                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
                void PrepareResult(string matchId)
                {
                    typeof(GameSession).GetProperty("IsOnline").SetValue(session,true);
                    typeof(GameSession).GetProperty("IsPublicMatch").SetValue(session,true);
                    typeof(GameSession).GetProperty("View").SetValue(session,new PlayerView{player=0,winner=0});
                    Call(session,"BeginRankedMatch",matchId);
                }
            }
            finally
            {
                if(fixture!=null)Object.DestroyImmediate(fixture);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        private static MatchRatingResult Rated(string matchId,int before,int after)=>new MatchRatingResult
            {matchId=matchId,status="rated",ratingBefore=before,ratingAfter=after,ratingDelta=after-before,opponentRating=1000};
        private static RankSnapshot Snapshot(int rating,bool full=false)=>new RankSnapshot
        {
            profile=new RankProfile{playerId="self",displayName="Player SELF",rating=rating,games=10,wins=6,losses=3,draws=1,rank=2},
            entries=full?Enumerable.Range(1,10).Select(rank=>new RankEntry{playerId="player-"+rank,displayName="Player ABC"+rank,
                rank=rank,rating=1400-rank*16,games=10,wins=6,losses=3,draws=1,isSelf=rank==2}).ToArray():Array.Empty<RankEntry>()
        };

        private sealed class FakeRanking:IRankingService
        {
            public readonly TaskCompletionSource<RankSnapshot> Overview=new TaskCompletionSource<RankSnapshot>();
            public readonly TaskCompletionSource<MatchRatingResult> Result=new TaskCompletionSource<MatchRatingResult>();
            public int OverviewCalls,ResultCalls;
            public CancellationToken OverviewToken,ResultToken;
            public Task<RankSnapshot> GetRankingAsync(string backendUrl,CancellationToken cancellation)
            {OverviewCalls++;OverviewToken=cancellation;return Overview.Task;}
            public Task<MatchRatingResult> GetMatchResultAsync(string backendUrl,string matchId,CancellationToken cancellation)
            {ResultCalls++;ResultToken=cancellation;return Result.Task;}
        }
    }
}
