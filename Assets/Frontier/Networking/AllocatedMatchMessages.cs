using System;
using System.Collections;

namespace Frontier.Networking
{
    [Serializable] public sealed class AllocatedPlayer
    {
        public string playerId, faction;
        public int seat;
    }
    [Serializable] public sealed class AllocatedMatchAssignment
    {
        public string matchId, mapId, status;
        public long expiresAtUnix;
        public AllocatedPlayer[] players;
    }
    [Serializable] public sealed class AllocatedAdmissionResult
    {
        public string playerId, faction;
        public int seat;
    }
    [Serializable] public sealed class AllocatedServerEvent
    {
        public string matchId, @event, reason;
        public int winner = -1, tick;
    }
    public interface IAllocatedAdmission
    {
        IEnumerator Admit(string matchId, string joinTicket, string requestId,
            Action<AllocatedAdmissionResult> success, Action<string> failure);
    }
}
