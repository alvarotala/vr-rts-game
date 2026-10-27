using System;
using System.Threading;
using System.Threading.Tasks;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public Func<IRankingService> RankingServiceFactory=()=>new NakamaRankingService();
        public RankSnapshot Ranking=>rankingEndpoint==PublicBackendUrl?ranking:null;
        public bool IsLoadingRanking {get;private set;}
        public bool RankingFailed {get;private set;}
        public string RankingStatus {get;private set;}
        public MatchRatingResult MatchRating {get;private set;}
        public bool IsLoadingMatchRating {get;private set;}
        public bool MatchRatingFailed {get;private set;}
        public string MatchRatingStatus {get;private set;}
        public uint RankingVersion {get;private set;}
        private RankSnapshot ranking;
        private string rankingEndpoint,rankedMatchId,rankedMatchEndpoint;
        private bool matchRatingRequested;
        private CancellationTokenSource rankingCancellation,matchRatingCancellation;
        private int rankingGeneration,matchRatingGeneration;

        public async Task RefreshRankingAsync()
        {
            string endpoint=PublicBackendUrl;
            if(IsLoadingRanking&&rankingEndpoint==endpoint)return;
            CancelRankingOverview();
            if(rankingEndpoint!=endpoint)ranking=null;
            rankingEndpoint=endpoint;
            var cancellation=new CancellationTokenSource();rankingCancellation=cancellation;
            int generation=rankingGeneration;
            IsLoadingRanking=true;RankingFailed=false;RankingStatus="Loading ranking…";RankingChanged();
            try
            {
                var provider=RankingServiceFactory();
                if(provider==null)throw new InvalidOperationException("Missing ranking service.");
                var value=await provider.GetRankingAsync(endpoint,cancellation.Token);
                if(!CurrentRanking(generation,endpoint,cancellation))return;
                if(value?.profile==null||value.entries==null)throw new InvalidOperationException("Missing ranking data.");
                ranking=value;RankingStatus=value.profile.games==0?"Complete a public match to enter the ranking.":"Ranking updates after each completed public match.";
            }
            catch(OperationCanceledException) { }
            catch(Exception)
            {
                if(CurrentRanking(generation,endpoint,cancellation))
                {RankingFailed=true;RankingStatus="Ranking is unavailable. Refresh to try again.";}
            }
            finally
            {
                if(ReferenceEquals(rankingCancellation,cancellation))
                {IsLoadingRanking=false;rankingCancellation=null;if(this!=null)RankingChanged();}
                cancellation.Dispose();
            }
        }

        private bool CurrentRanking(int generation,string endpoint,CancellationTokenSource cancellation)
            =>this!=null&&generation==rankingGeneration&&endpoint==PublicBackendUrl&&!cancellation.IsCancellationRequested;

        private void CancelRankingOverview()
        {
            rankingGeneration++;
            var cancellation=rankingCancellation;rankingCancellation=null;
            cancellation?.Cancel();IsLoadingRanking=false;
        }

        private void BeginRankedMatch(string matchId)
        {
            CancelMatchRating();
            rankedMatchId=matchId;rankedMatchEndpoint=PublicBackendUrl;
            matchRatingRequested=false;
            MatchRating=null;MatchRatingFailed=false;MatchRatingStatus=null;RankingChanged();
        }

        public async Task RefreshMatchRatingAsync()
        {
            if(!IsPublicMatch||string.IsNullOrEmpty(rankedMatchId)||Winner<0||IsLoadingMatchRating)return;
            matchRatingRequested=true;
            var cancellation=new CancellationTokenSource();matchRatingCancellation=cancellation;
            int generation=matchRatingGeneration;
            string matchId=rankedMatchId,endpoint=rankedMatchEndpoint;
            IsLoadingMatchRating=true;MatchRatingFailed=false;MatchRatingStatus="Updating your rating…";RankingChanged();
            try
            {
                var provider=RankingServiceFactory();
                if(provider==null)throw new InvalidOperationException("Missing ranking service.");
                for(int attempt=0;attempt<10;attempt++)
                {
                    var value=await provider.GetMatchResultAsync(endpoint,matchId,cancellation.Token);
                    if(!CurrentMatchRating(generation,matchId,endpoint,cancellation))return;
                    if(value==null||value.matchId!=matchId)throw new InvalidOperationException("Mismatched rating result.");
                    if(value.status=="rated"||value.status=="unrated")
                    {
                        MatchRating=value;
                        MatchRatingStatus=value.status=="rated"?"Rating updated for this match.":"This match does not affect your rating.";
                        _=RefreshRankingAsync();return;
                    }
                    if(value.status!="pending")throw new InvalidOperationException("Unknown rating state.");
                    if(attempt<9)await Task.Delay(2000,cancellation.Token);
                }
                MatchRatingStatus="This match is still being recorded. Refresh ranking to check again.";
            }
            catch(OperationCanceledException) { }
            catch(Exception)
            {
                if(CurrentMatchRating(generation,matchId,endpoint,cancellation))
                {MatchRatingFailed=true;MatchRatingStatus="Could not load the rating change. Refresh ranking to try again.";}
            }
            finally
            {
                if(ReferenceEquals(matchRatingCancellation,cancellation))
                {IsLoadingMatchRating=false;matchRatingCancellation=null;if(this!=null)RankingChanged();}
                cancellation.Dispose();
            }
        }

        private bool CurrentMatchRating(int generation,string matchId,string endpoint,CancellationTokenSource cancellation)
            =>this!=null&&IsPublicMatch&&generation==matchRatingGeneration&&matchId==rankedMatchId
                &&endpoint==rankedMatchEndpoint&&endpoint==PublicBackendUrl&&!cancellation.IsCancellationRequested;

        private void RequestMatchRatingOnce()
        {if(!matchRatingRequested)_=RefreshMatchRatingAsync();}

        private void CancelMatchRating()
        {
            matchRatingGeneration++;
            var cancellation=matchRatingCancellation;matchRatingCancellation=null;
            cancellation?.Cancel();IsLoadingMatchRating=false;
        }

        private void ResetRankingRequests()
        {
            CancelRankingOverview();CancelMatchRating();
            rankedMatchId=rankedMatchEndpoint=null;matchRatingRequested=false;MatchRating=null;MatchRatingStatus=null;MatchRatingFailed=false;
            RankingChanged();
        }

        private void RankingChanged(){RankingVersion++;Changed?.Invoke();}
    }
}
