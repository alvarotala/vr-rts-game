using Frontier.Core;

namespace Frontier.Networking
{
    // Public matchmaking groups only clients which the dedicated protocol can accept.
    public static class MatchCompatibility
    {
        public static int Protocol => MatchProtocol.Version;
        public static int Rules => Simulation.ReplayRulesVersion;
        public static string ContentHash => MatchProtocol.ContentHash();
        public static string Key => Protocol + ":" + Rules + ":" + ContentHash;
    }
}
