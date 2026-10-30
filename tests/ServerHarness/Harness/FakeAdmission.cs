using System;
using System.Collections;
using System.Collections.Generic;
using Frontier.Networking;

namespace Frontier.Harness
{
    // Stands in for the backend's ll_server_admit: one claim per ticket, scoped to one match.
    public sealed class FakeAdmission : IAllocatedAdmission
    {
        public string MatchId;
        public readonly Dictionary<string, AllocatedAdmissionResult> Tickets = new Dictionary<string, AllocatedAdmissionResult>();
        // Tickets refused before the first yield, exercising synchronous completion.
        public readonly HashSet<string> SynchronousFailures = new HashSet<string>();
        private readonly Dictionary<string, string> claims = new Dictionary<string, string>();
        public int Requests { get; private set; }

        public IEnumerator Admit(string matchId, string joinTicket, string requestId, Action<AllocatedAdmissionResult> success, Action<string> failure)
        {
            Requests++;
            if (SynchronousFailures.Contains(joinTicket)) { failure("Invalid join ticket"); yield break; }
            yield return null;
            if (matchId != MatchId || joinTicket == null || !Tickets.TryGetValue(joinTicket, out var result)) { failure("Invalid join ticket"); yield break; }
            if (claims.TryGetValue(joinTicket, out var claimed) && claimed != requestId) { failure("Join ticket already used"); yield break; }
            claims[joinTicket] = requestId;
            success(new AllocatedAdmissionResult { playerId = result.playerId, seat = result.seat, faction = result.faction });
        }
    }
}
