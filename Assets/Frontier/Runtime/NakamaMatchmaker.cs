using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Networking;
using Nakama;
using UnityEngine;

namespace Frontier.Runtime
{
    [Serializable] public sealed class PublicMatchConnection
    {
        public string matchId, host, joinTicket, faction;
        public int port, wsPort;
    }

    public interface IPublicMatchmaker : IDisposable
    {
        Task<PublicMatchConnection> FindAsync(string backendUrl, string faction, CancellationToken cancellation, Action<string> status);
        Task CancelAsync();
        void ConfirmJoined();
    }

    // The account token stays on HTTPS/WSS. NGO receives only a short-lived ticket
    // scoped to this allocation. No gameplay state or simulation runs in Nakama.
    public sealed class NakamaMatchmaker : IPublicMatchmaker
    {
        public const string PublicServerKey = "littlelegion";
        private IClient client;
        private ISession session;
        private ISocket socket;
        private CancellationTokenSource lifetime;
        private string searchId;
        private bool used, cancelled, joined;
        private readonly string identityOverride;
#if UNITY_WEBGL && !UNITY_EDITOR
        private double searchDeadline;
#endif

        // An explicit identity is for isolated process validation; normal clients
        // use a cryptographically random credential persisted per service endpoint.
        public NakamaMatchmaker(string identity = null) { identityOverride = identity; }

        public async Task<PublicMatchConnection> FindAsync(string backendUrl, string faction, CancellationToken cancellation, Action<string> status)
        {
            if (used) throw new InvalidOperationException("A search is already active.");
            used = true;
            var endpoint = ValidateEndpoint(backendUrl);
            if (faction != "industrial" && faction != "biological") throw new ArgumentException("Unknown faction.");
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
#if UNITY_WEBGL && !UNITY_EDITOR
            searchDeadline = Time.realtimeSinceStartupAsDouble + 180;
#else
            lifetime.CancelAfter(TimeSpan.FromMinutes(3));
#endif
            var token = lifetime.Token;
            ISocket ownedSocket = null;
            IMatchmakerTicket queueTicket = null;
            bool handedOff = false;
            try
            {
                CheckCancellation(token);
                status?.Invoke("Signing in…");
                client = new Client(endpoint.Scheme, endpoint.Host, endpoint.Port, PublicServerKey, UnityWebRequestAdapter.Instance);
                client.Timeout = 8;
                client.GlobalRetryConfiguration = new RetryConfiguration(baseDelayMs: 250, maxRetries: 0);
                // Let bounded creation calls complete so a cancellation can clean up
                // a search created on the server even if its response arrived late.
                session = await client.AuthenticateDeviceAsync(identityOverride ?? Identity(endpoint));
                CheckCancellation(token);
                var opened = await Rpc<SearchOpened>("ll_search_open", new SearchRequest
                {
                    faction = faction, protocol = MatchCompatibility.Protocol,
                    rules = MatchCompatibility.Rules, contentHash = MatchCompatibility.ContentHash
#if UNITY_WEBGL && !UNITY_EDITOR
                    ,websocket = true
#endif
                });
                searchId = opened.searchId;
                if (!IsOpaqueId(searchId)) throw new InvalidOperationException("The service returned an invalid search.");
                ValidateSearchWindow(opened.rating, opened.searchRadius, opened.queueRevision);
                CheckCancellation(token);
                ownedSocket = socket = client.NewSocket();
                await ownedSocket.ConnectAsync(session, appearOnline: false, connectTimeout: 8);
                CheckCancellation(token);
                queueTicket = await Enqueue(ownedSocket);
                CheckCancellation(token);
                string previousStatus = null;
                int queuedRevision = opened.queueRevision;
                while (true)
                {
                    CheckCancellation(token);
                    var result = await Rpc<SearchState>("ll_search_status", new SearchReference { searchId = searchId });
                    CheckCancellation(token);
                    if (result.status == "searching") ValidateSearchWindow(result.rating, result.searchRadius, result.queueRevision);
                    if (result.status != previousStatus || result.status == "searching" && result.queueRevision != queuedRevision)
                    {
                        previousStatus = result.status;
                        status?.Invoke(result.status == "searching"
                            ? $"Searching for a similar opponent (rating {result.rating}, range ±{result.searchRadius})…"
                            : StatusText(result.status));
                    }
                    if (result.status == "ready")
                    {
                        ValidateConnection(result.connection, faction);
                        handedOff = true;
                        return result.connection;
                    }
                    if (result.status == "cancelled") throw new InvalidOperationException("The search was cancelled. Please search again.");
                    if (result.status == "failed" || result.status == "finished")
                        throw new InvalidOperationException("The match is no longer available. Please search again.");
                    if (result.status != "searching" && result.status != "waitingServer" && result.status != "preparing")
                        throw new InvalidOperationException("The service returned an invalid search state.");
                    if (result.status == "searching" && !ownedSocket.IsConnected)
                        throw new InvalidOperationException("The matchmaking connection was lost. Please search again.");
                    if (result.status == "searching" && result.queueRevision != queuedRevision)
                    {
                        // Query ranges belong to the server. Refresh only the native
                        // ticket; the search ID and its server-side age remain unchanged.
                        // Matching can win the race with removal/addition. Re-poll that
                        // same search after any socket error before deciding it failed.
                        try
                        {
                            if (queueTicket != null)
                            {
                                try
                                {
#if UNITY_WEBGL && !UNITY_EDITOR
                                    await WebGlAsync.WithTimeout(ownedSocket.RemoveMatchmakerAsync(queueTicket), 10000, token);
#else
                                    await ownedSocket.RemoveMatchmakerAsync(queueTicket);
#endif
                                }
                                catch (Exception) { CheckCancellation(token); }
                            }
                            CheckCancellation(token);
                            queueTicket = await Enqueue(ownedSocket);
                            queuedRevision = result.queueRevision;
                            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-frontier-public-smoke") >= 0)
                                Debug.Log($"FRONTIER_PUBLIC_RANGE revision={queuedRevision} radius={result.searchRadius}");
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception) { CheckCancellation(token); }
                    }
#if UNITY_WEBGL && !UNITY_EDITOR
                    await WebGlAsync.Delay(1000, token);
#else
                    await Task.Delay(1000, token);
#endif
                }
            }
            catch (ApiResponseException)
            {
                CheckCancellation(token);
                throw new InvalidOperationException("The online service could not complete the search. Check the connection and try again.");
            }
            finally
            {
                if (!handedOff || cancelled || cancellation.IsCancellationRequested)
                {
                    await CancelRemoteSearch();
                    await CloseSocket(ownedSocket);
                }
            }
        }

        private Task<IMatchmakerTicket> Enqueue(ISocket value)
        {
            string compatibility = MatchCompatibility.Key;
            var request = value.AddMatchmakerAsync("+properties.compat:\"" + compatibility + "\"", 2, 2,
                new Dictionary<string, string> { { "compat", compatibility }, { "searchId", searchId } });
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebGlAsync.WithTimeout(request, 10000, lifetime.Token);
#else
            return request;
#endif
        }

        internal static void ValidateSearchWindow(int rating, int radius, int revision)
        {
            if (rating < 0 || radius < 100 || radius > 400 || radius % 100 != 0 || revision != radius / 100 - 1)
                throw new InvalidOperationException("The service returned an invalid rating range.");
        }

        public async Task CancelAsync()
        {
            cancelled = true;
            lifetime?.Cancel();
            await CancelRemoteSearch();
            await CloseSocket(socket);
        }

        public void ConfirmJoined()
        {
            joined = true;
            searchId = null;
            // Stop the search timeout after a successful NGO welcome.
#if UNITY_WEBGL && !UNITY_EDITOR
            searchDeadline = double.PositiveInfinity;
#else
            lifetime?.CancelAfter(Timeout.InfiniteTimeSpan);
#endif
            _ = CloseSocket(socket);
        }

        public void Dispose() { _ = CancelAsync(); }

        private void CheckCancellation(CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Time.realtimeSinceStartupAsDouble >= searchDeadline) lifetime?.Cancel();
#endif
            if (cancelled) throw new OperationCanceledException(token);
            token.ThrowIfCancellationRequested();
        }

        private async Task CancelRemoteSearch()
        {
            if (joined || client == null || session == null || string.IsNullOrEmpty(searchId)) return;
            string id = searchId;
            try
            {
                await Rpc<SearchCancelled>("ll_search_cancel", new SearchReference { searchId = id });
                if (searchId == id) searchId = null;
            }
            catch (Exception)
            {
                // Backend expiry is the fallback if connectivity is already lost.
                // Never log account tokens, credential-bearing URLs or HTTP bodies.
            }
        }

        private async Task CloseSocket(ISocket value)
        {
            if (value == null) return;
            try { await value.CloseAsync(); } catch (Exception) { }
            if (ReferenceEquals(socket, value)) socket = null;
        }

        private async Task<T> Rpc<T>(string name, object body)
        {
            var result = await client.RpcAsync(session, name, JsonUtility.ToJson(body));
            if (string.IsNullOrEmpty(result.Payload) || result.Payload.Length > 16384)
                throw new InvalidOperationException("The online service returned an invalid response.");
            var decoded = JsonUtility.FromJson<T>(result.Payload);
            if (decoded == null) throw new InvalidOperationException("The online service returned an empty response.");
            return decoded;
        }

        public static Uri ValidateEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || uri.AbsolutePath != "/" || uri.Port < 1 || uri.Port > 65535)
                throw new ArgumentException("Enter a valid online service URL.");
            if (uri.Scheme == "http" && !IsLocalHost(uri.Host))
                throw new ArgumentException("Public online services require HTTPS.");
            return uri;
        }

        private static bool IsLocalHost(string host)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            if (!IPAddress.TryParse(host, out var ip)) return false;
            if (IPAddress.IsLoopback(ip)) return true;
            byte[] bytes = ip.GetAddressBytes();
            return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
        }

        private static void ValidateConnection(PublicMatchConnection value, string faction)
        {
            if (value == null || !IsOpaqueId(value.matchId) || string.IsNullOrEmpty(value.host)
                || Uri.CheckHostName(value.host) == UriHostNameType.Unknown || value.host == "0.0.0.0"
                || value.port < 1 || value.port > 65535 || value.faction != faction
                || string.IsNullOrEmpty(value.joinTicket) || value.joinTicket.Length < 24 || value.joinTicket.Length > 256)
                throw new InvalidOperationException("The service returned an invalid match connection.");
#if UNITY_WEBGL && !UNITY_EDITOR
            if (value.wsPort < 1 || value.wsPort > 65535)
                throw new InvalidOperationException("The assigned server does not accept browser players yet.");
#endif
        }

        internal static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 24 || value.Length > 64) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return false;
            return true;
        }

        internal static string Identity(Uri endpoint)
        {
            string suffix;
            using (var sha = SHA256.Create())
                suffix = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(endpoint.GetLeftPart(UriPartial.Authority)))).Replace("-", "");
            string key = "frontier.online.identity.v1." + suffix;
            string value = PlayerPrefs.GetString(key, "");
            if (value.Length != 64)
            {
                var bytes = new byte[32];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                value = BitConverter.ToString(bytes).Replace("-", "");
                PlayerPrefs.SetString(key, value); PlayerPrefs.Save();
            }
            return value;
        }

        private static string StatusText(string state)
        {
            switch (state)
            {
                case "searching": return "Searching for an opponent…";
                case "waitingServer": return "Waiting for an available server…";
                case "preparing": return "Preparing your match…";
                case "ready": return "Connecting to your match…";
                default: return "Updating search…";
            }
        }

        [Serializable] private sealed class SearchRequest
        {
            public string faction, contentHash; public int protocol, rules;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Keep this field absent in native clients: older native-only backends
            // reject unknown JSON fields even when the value is false.
            public bool websocket;
#endif
        }
        [Serializable] private sealed class SearchReference { public string searchId; }
        [Serializable] private sealed class SearchOpened { public string searchId; public int rating, searchRadius, queueRevision; }
        [Serializable] private sealed class SearchState { public string status, message; public int rating, searchRadius, queueRevision; public PublicMatchConnection connection; }
        [Serializable] private sealed class SearchCancelled { public bool ok; }
    }
}
