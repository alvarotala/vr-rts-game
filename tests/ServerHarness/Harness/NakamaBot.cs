using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Core;
using Frontier.Networking;

namespace Frontier.Harness
{
    // A public-matchmaking player that behaves like the Quest client: device session,
    // ll_search_open without a websocket requirement, native matchmaker, status polling,
    // then the NGO transport with its scoped join ticket.
    public sealed class NakamaBot
    {
        public sealed class Connection { public string matchId, host, joinTicket, faction; public int port, wsPort; }
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private readonly string backend;
        private string token;

        public NakamaBot(string backendUrl) { backend = backendUrl.TrimEnd('/'); }

        public async Task<Connection> FindAsync(string faction, CancellationToken cancel)
        {
            var auth = new HttpRequestMessage(HttpMethod.Post, backend + "/v2/account/authenticate/device?create=true")
            { Content = new StringContent(JsonSerializer.Serialize(new { id = "harness-quest-" + Guid.NewGuid().ToString("N") }), Encoding.UTF8, "application/json") };
            auth.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("littlelegion:")));
            token = JsonDocument.Parse(await (await http.SendAsync(auth, cancel)).EnsureSuccessStatusCode().Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
            string searchId = (await Rpc("ll_search_open", new { faction, protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion, contentHash = MatchProtocol.ContentHash() }, cancel))
                .GetProperty("searchId").GetString();
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(backend.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws?format=json&status=false&token=" + Uri.EscapeDataString(token)), cancel);
            string compat = MatchCompatibility.Key;
            string add = JsonSerializer.Serialize(new { cid = "1", matchmaker_add = new { min_count = 2, max_count = 2, query = "+properties.compat:\"" + compat + "\"",
                string_properties = new { compat, searchId } } });
            await socket.SendAsync(Encoding.UTF8.GetBytes(add), WebSocketMessageType.Text, true, cancel);
            _ = Drain(socket, cancel);
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                var state = await Rpc("ll_search_status", new { searchId }, cancel);
                string status = state.GetProperty("status").GetString();
                if (status == "ready" && state.TryGetProperty("connection", out var connection))
                {
                    try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch (Exception) { }
                    return JsonSerializer.Deserialize<Connection>(connection.GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                }
                if (status == "failed" || status == "cancelled" || status == "finished") throw new InvalidOperationException("Search ended: " + status);
                await Task.Delay(1000, cancel);
            }
        }

        private static async Task Drain(ClientWebSocket socket, CancellationToken cancel)
        {
            var buffer = new byte[16384];
            try { while (socket.State == WebSocketState.Open) await socket.ReceiveAsync(buffer, cancel); } catch (Exception) { }
        }

        private async Task<JsonElement> Rpc(string name, object body, CancellationToken cancel)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, backend + "/v2/rpc/" + name + "?unwrap")
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await http.SendAsync(request, cancel);
            string text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(name + " failed: " + (int)response.StatusCode + " " + text);
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }
}
