using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Frontier.Core;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    // Uniform view of the NGO client (Quest/Unity path) and the websocket client (browser path).
    public interface IMatchLink
    {
        event Action<MatchWelcome> Started;
        event Action<PlayerView> StateReceived;
        event Action<CommandAck> CommandAcknowledged;
        event Action<MatchReport> Finished;
        event Action<string> StatusChanged;
        event Action<string> Disconnected;
        double CountdownRemaining { get; }
        bool Send(GameCommand command);
        void Leave();
        void Abort();
        string Transport { get; }
    }

    public sealed class NgoLink : IMatchLink
    {
        private readonly NgoMatchClient client;
        public NgoLink(string address, ushort port, string faction, string matchId = null, string joinTicket = null)
        {
            client = new GameObject("NGO bot client").AddComponent<NgoMatchClient>();
            if (!client.Connect(address, port, faction, matchId, joinTicket)) throw new InvalidOperationException("NGO client did not start.");
        }
        public event Action<MatchWelcome> Started { add => client.Started += value; remove => client.Started -= value; }
        public event Action<PlayerView> StateReceived { add => client.StateReceived += value; remove => client.StateReceived -= value; }
        public event Action<CommandAck> CommandAcknowledged { add => client.CommandAcknowledged += value; remove => client.CommandAcknowledged -= value; }
        public event Action<MatchReport> Finished { add => client.Finished += value; remove => client.Finished -= value; }
        public event Action<string> StatusChanged { add => client.StatusChanged += value; remove => client.StatusChanged -= value; }
        public event Action<string> Disconnected { add => client.Disconnected += value; remove => client.Disconnected -= value; }
        public double CountdownRemaining => client.CountdownRemaining;
        public bool Send(GameCommand command) => client.Send(command);
        public void Leave() { client.Disconnect(); UnityEngine.Object.Destroy(client.gameObject); }
        public void Abort() => Leave();
        public string Transport => "ngo";
    }

    public sealed class WebSocketLink : IMatchLink
    {
        private readonly WebSocketMatchClient client;
        public WebSocketLink(string url, string faction, string matchId = null, string joinTicket = null)
        {
            client = WebSocketMatchClient.Create("WebSocket bot client");
            client.Connect(url, faction, matchId, joinTicket);
        }
        public event Action<MatchWelcome> Started { add => client.Started += value; remove => client.Started -= value; }
        public event Action<PlayerView> StateReceived { add => client.StateReceived += value; remove => client.StateReceived -= value; }
        public event Action<CommandAck> CommandAcknowledged { add => client.CommandAcknowledged += value; remove => client.CommandAcknowledged -= value; }
        public event Action<MatchReport> Finished { add => client.Finished += value; remove => client.Finished -= value; }
        public event Action<string> StatusChanged { add => client.StatusChanged += value; remove => client.StatusChanged -= value; }
        public event Action<string> Disconnected { add => client.Disconnected += value; remove => client.Disconnected -= value; }
        public double CountdownRemaining => client.CountdownRemaining;
        public bool Send(GameCommand command) => client.Send(command);
        public void Leave() { client.Disconnect(); UnityEngine.Object.Destroy(client.gameObject); }
        public void Abort() { client.Abort(); UnityEngine.Object.Destroy(client.gameObject); }
        public string Transport => "websocket";
    }

    // Plays through a link exactly like MultiplayerSmokeClient: only PlayerView,
    // every order travels through the server, the payload's player field is wrong
    // on purpose, and one duplicate sequence probes idempotent acknowledgements.
    public sealed class StrategyBot
    {
        public readonly string Name;
        public readonly IMatchLink Link;
        public int Player { get; private set; } = -1;
        public bool Passed { get; private set; }
        public bool Failed { get; private set; }
        public string FailReason { get; private set; }
        public int Winner { get; private set; } = -1;
        public int FinalTick { get; private set; }
        public string Digest { get; private set; }
        public int Accepted => accepted;
        public PlayerView Latest => latest;
        public int ConcedeAtSeconds = 900;
        // Seat that concedes when the bound is reached; -1 lets either seat concede.
        public int ConcedeSeat = 1;
        public int AbortAtSeconds = -1;
        public bool RequireFullEconomy = true;
        public bool RequireReport = true;
        public string LastStatus { get; private set; }
        public readonly List<string> Statuses = new List<string>();
        private readonly AiDifficulty difficulty;
        private Strategist strategist;
        private Catalog catalog;
        private MapDefinition map;
        private MatchReport report;
        private bool reportReceived;
        private PlayerView latest;
        private readonly Dictionary<int, CommandKind> commands = new Dictionary<int, CommandKind>();
        private readonly Dictionary<int, bool> outcomes = new Dictionary<int, bool>();
        private int sequence, lastDecision = -1, lastTick = -1, accepted, builds, trains, attacks, duplicateAcks;
        private bool duplicateSent, surrenderSent, aborted;

        public StrategyBot(string name, IMatchLink link, AiDifficulty difficulty)
        {
            Name = name; Link = link; this.difficulty = difficulty;
            link.Started += OnStarted; link.StateReceived += OnState; link.CommandAcknowledged += OnAcknowledged;
            link.Finished += value => { report = value; reportReceived = true; TryFinish(); };
            link.StatusChanged += message => { LastStatus = message; Statuses.Add(message); };
            link.Disconnected += message => { if (!Passed && !aborted) Fail("Disconnected before a verified result: " + message); };
        }

        private void OnStarted(MatchWelcome welcome)
        {
            Player = welcome.player;
            catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
            map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/" + welcome.mapId).text);
            strategist = new Strategist(Player, difficulty, catalog, map);
        }

        private void OnState(PlayerView view)
        {
            if (Passed || Failed || aborted) return;
            if (strategist == null || view.player != Player || view.tick < lastTick || view.visibility == null || view.visibility.Length != map.width * map.height)
            { Fail("Invalid, misrouted or backwards player view."); return; }
            foreach (var entity in view.entities.Where(e => e.owner != Player))
                if (entity.queue.Length != 0 || entity.pendingOrders.Length != 0 || entity.activeOrderSequence != 0 || entity.energy != 0
                    || entity.abilityCooldowns.Length != 0 || entity.cargo != 0 || entity.hasRallyPoint || entity.target != 0)
                { Fail("Private enemy state in PlayerView."); return; }
            latest = view; lastTick = view.tick; attacks += view.attacks.Length;
            if (view.winner != -1) { TryFinish(); return; }
            if (Link.CountdownRemaining > 0) return;
            if (AbortAtSeconds >= 0 && view.tick >= Simulation.TickRate * AbortAtSeconds) { aborted = true; Link.Abort(); return; }
            int decision = view.tick / Simulation.TickRate;
            if (decision != lastDecision)
            {
                lastDecision = decision;
                int actualTick = view.tick; view.tick = decision * Simulation.TickRate;
                try
                {
                    foreach (var command in strategist.Decide(view).ToArray())
                    {
                        Submit(command);
                        if (!duplicateSent)
                        {
                            duplicateSent = true;
                            if (!Link.Send(command)) { Fail("Could not send the duplicate acknowledgement probe."); return; }
                        }
                    }
                }
                finally { view.tick = actualTick; }
            }
            if (!surrenderSent && ConcedeAtSeconds >= 0 && (ConcedeSeat < 0 || ConcedeSeat == Player) && view.tick >= Simulation.TickRate * ConcedeAtSeconds
                && (!RequireFullEconomy || builds > 0 && trains > 0 && attacks > 0))
            {
                surrenderSent = true;
                Submit(new GameCommand { kind = CommandKind.Surrender });
            }
        }

        private void Submit(GameCommand command)
        {
            command.sequence = ++sequence;
            // The server must derive ownership from the connection, not this field.
            command.player = 1 - Player;
            commands[command.sequence] = command.kind;
            if (!Link.Send(command)) Fail("The link rejected a local command send.");
        }

        private void OnAcknowledged(CommandAck acknowledgement)
        {
            if (Passed || Failed) return;
            if (!commands.TryGetValue(acknowledgement.sequence, out var kind)) { Fail("Acknowledgement has no sent command."); return; }
            if (outcomes.TryGetValue(acknowledgement.sequence, out bool original))
            {
                if (original != acknowledgement.accepted) { Fail("A duplicate command changed its outcome."); return; }
                duplicateAcks++; return;
            }
            outcomes.Add(acknowledgement.sequence, acknowledgement.accepted);
            if (!acknowledgement.accepted) return;
            accepted++;
            if (kind == CommandKind.Build) builds++;
            if (kind == CommandKind.Train) trains++;
        }

        private void TryFinish()
        {
            if (Passed || Failed || !reportReceived || latest == null || latest.winner < 0) return;
            if (RequireReport && report == null) { Fail("The final report was unavailable."); return; }
            if (report != null && (report.Winner != latest.winner || report.Tick != latest.tick)) { Fail("Terminal view and report disagree."); return; }
            if (RequireFullEconomy && (accepted < 10 || builds < 1 || trains < 1 || duplicateAcks < 1
                || report.Players.Any(p => p.WorkersProduced < 1 || p.StructuresCompleted < 1 || p.ArmyProduced < 1)
                || report.Players.Sum(p => p.ArmyLost) < 1))
            { Fail("Missing economy, construction, combat or duplicate acknowledgement evidence."); return; }
            Winner = latest.winner; FinalTick = latest.tick;
            if (report != null)
                using (var sha = SHA256.Create())
                    Digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(MatchReportData.From(report))))).Replace("-", "").ToLowerInvariant();
            Passed = true;
        }

        private void Fail(string reason)
        {
            if (Passed || Failed) return;
            Failed = true; FailReason = reason;
            UnityEngine.Debug.LogError("HARNESS_BOT_FAILED " + Name + " (" + Link.Transport + "): " + reason);
        }

        public string Summary => $"{Name} via {Link.Transport}: player={Player} winner={Winner} tick={FinalTick} accepted={accepted} builds={builds} trains={trains} duplicateAcks={duplicateAcks} digest={Digest}";
    }
}
