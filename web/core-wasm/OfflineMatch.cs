using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frontier.Core;

namespace Frontier.Web
{
    // JavaScript bridge for offline play. The page renders the same PlayerView
    // contract it receives online; the AI uses the same Strategist as Unity.
    public static partial class OfflineMatch
    {
        private static Catalog catalog;
        private static Simulation match;
        private static Strategist bot;
        private static readonly List<string> notices = new List<string>();

        [JSExport]
        public static string Start(string catalogJson, string mapJson, string faction, string enemyFaction, string difficulty)
        {
            try
            {
                catalog = new Catalog(JsonSerializer.Deserialize(catalogJson, BridgeJson.Default.CatalogDefinition));
                var map = JsonSerializer.Deserialize(mapJson, BridgeJson.Default.MapDefinition);
                match = new Simulation(map, catalog, faction, enemyFaction);
                match.BeginRecording();
                notices.Clear();
                match.Notice += (player, message) => { if (player == 0 && notices.Count < 64) notices.Add(message); };
                var level = difficulty == "hard" ? AiDifficulty.Hard : difficulty == "easy" ? AiDifficulty.Easy : AiDifficulty.Normal;
                bot = new Strategist(1, level, catalog, map);
                return "";
            }
            catch (Exception error) { match = null; bot = null; return error.Message; }
        }

        [JSExport]
        public static string Submit(string commandJson)
        {
            if (match == null) return Result(false, "The match is not active.");
            GameCommand command;
            try { command = JsonSerializer.Deserialize(commandJson, BridgeJson.Default.GameCommand); }
            catch (Exception) { return Result(false, "Invalid command."); }
            if (command == null) return Result(false, "Invalid command.");
            command.player = 0;
            command.entities = command.entities ?? Array.Empty<int>();
            // Leaving from the pause menu concedes even though the simulation is paused.
            bool wasPaused = match.Paused;
            if (command.kind == CommandKind.Surrender) match.Paused = false;
            var result = match.Submit(command);
            match.Paused = wasPaused && match.Winner == -1;
            return Result(result.accepted, result.message ?? "");
        }

        // Advances up to `ticks` simulation steps, running the AI at the same cadence as Unity's GameSession.
        [JSExport]
        public static int Step(int ticks)
        {
            if (match == null) return -1;
            for (int i = 0; i < ticks && match.Winner == -1 && !match.Paused; i++)
            {
                if (bot != null && match.Tick % Simulation.TickRate == 0)
                    foreach (var command in bot.Decide(match.View(1)).ToArray()) match.Submit(command);
                match.Step();
            }
            return match.Tick;
        }

        [JSExport] public static void SetPaused(bool paused) { if (match != null) match.Paused = paused; }

        [JSExport]
        public static string View(int player) => match == null ? "" : JsonSerializer.Serialize(match.View(player), BridgeJson.Default.PlayerView);

        [JSExport]
        public static string Report() => match?.Report == null ? "" : JsonSerializer.Serialize(MatchReportData.From(match.Report), BridgeJson.Default.MatchReportData);

        [JSExport]
        public static string TakeNotices()
        {
            if (notices.Count == 0) return "";
            string text = JsonSerializer.Serialize(notices.ToArray(), BridgeJson.Default.StringArray);
            notices.Clear(); return text;
        }

        [JSExport] public static void Stop() { match = null; bot = null; notices.Clear(); }

        private static string Result(bool accepted, string message)
            => JsonSerializer.Serialize(new CommandOutcome { accepted = accepted, message = message }, BridgeJson.Default.CommandOutcome);
    }

    public sealed class CommandOutcome { public bool accepted; public string message; }

    [JsonSourceGenerationOptions(IncludeFields = true)]
    [JsonSerializable(typeof(CatalogDefinition))]
    [JsonSerializable(typeof(MapDefinition))]
    [JsonSerializable(typeof(GameCommand))]
    [JsonSerializable(typeof(PlayerView))]
    [JsonSerializable(typeof(MatchReportData))]
    [JsonSerializable(typeof(CommandOutcome))]
    [JsonSerializable(typeof(string[]))]
    internal sealed partial class BridgeJson : JsonSerializerContext { }

    public static class Program { public static void Main() { } }
}
