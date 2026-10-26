using System;
using Frontier.Core;

namespace Frontier.Networking
{
    [Serializable] public sealed class MatchWelcome
    {
        public int protocol, player;
        public string matchId, mapId, faction0, faction1;
        public double startServerTime;
    }
    [Serializable] public sealed class CommandAck
    {
        public int sequence, tick;
        public bool accepted;
        public string message;
    }
    [Serializable] internal sealed class ConnectionHello
    {
        public int protocol, rules;
        public string contentHash, faction;
        public string matchId, joinTicket;
    }
    [Serializable] internal sealed class MatchResultEnvelope
    {
        public PlayerView view;
        public MatchReportData report;
        public bool reportUnavailable;
    }
    [Serializable] internal sealed class NetworkStatus { public string message; }
}
