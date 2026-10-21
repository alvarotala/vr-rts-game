using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    [Serializable]
    public sealed class RecordedCommand
    {
        public int tick, order;
        public GameCommand command;
        public bool accepted;
        public string rejection;
        public static GameCommand Copy(GameCommand c)=>c==null?null:new GameCommand
        {
            kind=c.kind, player=c.player, sequence=c.sequence, append=c.append, replacePendingSequence=c.replacePendingSequence,
            entities=c.entities==null?null:(int[])c.entities.Clone(), target=c.target, queueOrderId=c.queueOrderId, position=c.position, definition=c.definition
        };
        internal RecordedCommand Copy()=>new RecordedCommand{tick=tick,order=order,command=Copy(command),accepted=accepted,rejection=rejection};
    }

    public sealed partial class Simulation
    {
        // Bump when simulation/order semantics change. Saved catalogs/maps do not preserve old code.
        public const int ReplayRulesVersion=1;
        private readonly List<RecordedCommand> recordedCommands=new List<RecordedCommand>();
        private bool recordingCommands;
        public int RecordedCommandCount=>recordedCommands.Count;
        public void BeginRecording()
        {
            if(Tick!=0||Winner!=-1||lastSequences.Any(s=>s!=0)||matchCounters.Any(c=>c.actions!=0))
                throw new InvalidOperationException("Record from the start of a fresh simulation.");
            recordingCommands=true;
        }
        // Archival API for the authoritative session only; never part of PlayerView.
        public RecordedCommand[] CopyRecordedCommands(int fromOrder)
        {
            if(fromOrder<0||fromOrder>recordedCommands.Count)throw new ArgumentOutOfRangeException(nameof(fromOrder));
            return recordedCommands.Skip(fromOrder).Select(c=>c.Copy()).ToArray();
        }
        public CommandResult Submit(GameCommand command)
        {
            bool record=recordingCommands&&!Paused&&Winner==-1&&command!=null;
            var copy=record?RecordedCommand.Copy(command):null;
            var result=SubmitCommand(command);
            // Failed active commands can consume sequences or affect partial movement plans.
            // Retain them too, while APM still counts accepted commands only.
            if(record)recordedCommands.Add(new RecordedCommand{tick=Tick,order=recordedCommands.Count,command=copy,accepted=result.accepted,rejection=result.message});
            return result;
        }
    }
}
