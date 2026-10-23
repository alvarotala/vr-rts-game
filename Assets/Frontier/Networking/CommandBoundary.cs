using System;
using System.Collections.Generic;
using Frontier.Core;

namespace Frontier.Networking
{
    public static class CommandBoundary
    {
        public static string Validate(GameCommand command)
        {
            if (command == null || command.sequence <= 0 || !Enum.IsDefined(typeof(CommandKind), command.kind)
                || command.replacePendingSequence < 0 || command.queueOrderId < 0 || command.unitOrderSequence < 0
                || float.IsNaN(command.position.x) || float.IsInfinity(command.position.x) || float.IsNaN(command.position.y) || float.IsInfinity(command.position.y)
                || command.definition != null && command.definition.Length > 64 || command.entities == null
                || command.entities.Length > Simulation.UnitLimit + Simulation.BuildingLimit)
                return "Invalid command.";
            return null;
        }

        public static void AssignAuthority(GameCommand command, int authenticatedSeat)
        {
            if (command == null || authenticatedSeat < 0 || authenticatedSeat > 1) throw new ArgumentException("Invalid command authority.");
            command.player = authenticatedSeat;
        }
    }

    // Bounded receipt cache. Evicted sequences stay retired, so an old retry can never execute twice.
    public sealed class CommandReceipts
    {
        private readonly int capacity;
        private readonly Dictionary<int, CommandAck> results = new Dictionary<int, CommandAck>();
        private readonly Queue<int> order = new Queue<int>();
        public int HighestSequence { get; private set; }
        public CommandReceipts(int capacity = 512)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }
        public bool TryGet(int sequence, out CommandAck result) => results.TryGetValue(sequence, out result);
        public bool WasProcessed(int sequence) => sequence <= HighestSequence;
        public void Record(CommandAck result)
        {
            if (result == null || result.sequence <= HighestSequence) throw new InvalidOperationException("A command sequence can only be processed once.");
            HighestSequence = result.sequence; results.Add(result.sequence, result); order.Enqueue(result.sequence);
            while (order.Count > capacity) results.Remove(order.Dequeue());
        }
    }
}
