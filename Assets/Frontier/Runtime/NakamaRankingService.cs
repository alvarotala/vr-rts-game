using System;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace Frontier.Runtime
{
    [Serializable] public class RankProfile
    {
        public string playerId, displayName;
        public int rating, games, wins, losses, draws, rank;
    }
    [Serializable] public sealed class RankEntry : RankProfile { public bool isSelf; }
    [Serializable] public sealed class RankSnapshot { public RankProfile profile; public RankEntry[] entries; }
    [Serializable] public sealed class MatchRatingResult
    {
        public string matchId, status;
        public int ratingBefore, ratingAfter, ratingDelta, opponentRating;
    }

    public interface IRankingService
    {
        Task<RankSnapshot> GetRankingAsync(string backendUrl, CancellationToken cancellation);
        Task<MatchRatingResult> GetMatchResultAsync(string backendUrl, string matchId, CancellationToken cancellation);
    }

    // Read-only account service. Rating changes are produced exclusively by the
    // trusted dedicated server's result; neither method submits a score.
    public sealed class NakamaRankingService : IRankingService
    {
        private readonly string identityOverride;
        public NakamaRankingService(string identity = null) { identityOverride = identity; }

        public async Task<RankSnapshot> GetRankingAsync(string backendUrl, CancellationToken cancellation)
        {
            var connection = await Authenticate(backendUrl, cancellation);
            var profile = await Rpc<RankProfile>(connection, "ll_rank_profile", new EmptyRequest(), cancellation);
            ValidateProfile(profile);
            var top = await Rpc<Leaderboard>(connection, "ll_rank_top", new EmptyRequest(), cancellation);
            var entries = top.entries ?? Array.Empty<RankEntry>();
            if (entries.Length > 10) throw new InvalidOperationException("Invalid leaderboard response.");
            foreach (var entry in entries) ValidateProfile(entry);
            return new RankSnapshot { profile = profile, entries = entries };
        }

        public async Task<MatchRatingResult> GetMatchResultAsync(string backendUrl, string matchId, CancellationToken cancellation)
        {
            if (!NakamaMatchmaker.IsOpaqueId(matchId)) throw new ArgumentException("Invalid match identity.");
            var connection = await Authenticate(backendUrl, cancellation);
            var result = await Rpc<MatchRatingResult>(connection, "ll_rank_result", new ResultRequest { matchId = matchId }, cancellation);
            if (result.matchId != matchId || result.status != "pending" && result.status != "rated" && result.status != "unrated")
                throw new InvalidOperationException("Invalid match rating response.");
            if (result.status == "rated" && (result.ratingBefore < 0 || result.ratingAfter < 0 || result.opponentRating < 0
                || (long)result.ratingAfter - result.ratingBefore != result.ratingDelta))
                throw new InvalidOperationException("Inconsistent match rating response.");
            return result;
        }

        private async Task<Connection> Authenticate(string backendUrl, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var endpoint = NakamaMatchmaker.ValidateEndpoint(backendUrl);
            var client = new Client(endpoint.Scheme, endpoint.Host, endpoint.Port, NakamaMatchmaker.PublicServerKey, UnityWebRequestAdapter.Instance);
            client.Timeout = 8;
            client.GlobalRetryConfiguration = new RetryConfiguration(baseDelayMs: 250, maxRetries: 0);
            var session = await client.AuthenticateDeviceAsync(identityOverride ?? NakamaMatchmaker.Identity(endpoint));
            cancellation.ThrowIfCancellationRequested();
            return new Connection { Client = client, Session = session };
        }

        private static async Task<T> Rpc<T>(Connection connection, string name, object body, CancellationToken cancellation) where T : class
        {
            cancellation.ThrowIfCancellationRequested();
            var response = await connection.Client.RpcAsync(connection.Session, name, JsonUtility.ToJson(body));
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(response.Payload) || response.Payload.Length > 32768)
                throw new InvalidOperationException("Invalid ranking response.");
            var result = JsonUtility.FromJson<T>(response.Payload);
            if (result == null) throw new InvalidOperationException("Empty ranking response.");
            return result;
        }

        internal static void ValidateProfile(RankProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.playerId) || profile.rating < 0 || profile.rank < 0
                || profile.games < 0 || profile.wins < 0 || profile.losses < 0 || profile.draws < 0
                || (long)profile.wins + profile.losses + profile.draws != profile.games)
                throw new InvalidOperationException("Invalid rating profile.");
        }

        private sealed class Connection { public IClient Client; public ISession Session; }
        [Serializable] private sealed class EmptyRequest { }
        [Serializable] private sealed class ResultRequest { public string matchId; }
        [Serializable] private sealed class Leaderboard { public RankEntry[] entries; }
    }
}
