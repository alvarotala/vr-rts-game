using System.Text.Json;
using Frontier.Core;

static class MatchRecordingChecks
{
    public static void Run(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        var json=new JsonSerializerOptions{IncludeFields=true};
        var match=new Simulation(map,catalog);match.BeginRecording();
        int worker=match.Entities.Values.First(e=>e.owner==0&&match.Type(e).role=="worker").id;
        var command=new GameCommand{player=0,sequence=1,kind=CommandKind.Hold,entities=new[]{worker}};
        check(match.Submit(command).accepted,"record a group command at tick zero");
        command.entities[0]=99999;command.kind=CommandKind.Surrender;
        check(match.CopyRecordedCommands(0)[0].command.entities[0]==worker&&match.CopyRecordedCommands(0)[0].command.kind==CommandKind.Hold,"recording detaches the caller's command and selection array");
        match.Submit(new GameCommand{player=0,sequence=10,kind=CommandKind.Train,entities=new[]{worker},definition="missing"});
        match.Submit(new GameCommand{player=0,sequence=9,kind=CommandKind.Hold,entities=new[]{worker}});
        check(match.CopyRecordedCommands(0).Skip(1).All(c=>!c.accepted),"failed commands that consume sequences remain in exact replay order");
        match.Paused=true;int count=match.RecordedCommandCount;match.Submit(new GameCommand{player=0,sequence=99,kind=CommandKind.Hold,entities=new[]{worker}});match.Step();match.Paused=false;
        check(match.RecordedCommandCount==count&&match.Tick==0,"paused inputs and wall time never enter the simulation recording");
        var bots=new[]{new Strategist(0,AiDifficulty.Normal,catalog,map),new Strategist(1,AiDifficulty.Hard,catalog,map)};
        while(match.Tick<Simulation.TickRate*360&&match.Winner==-1)
        {
            if(match.Tick%Simulation.TickRate==0)for(int p=0;p<2;p++)foreach(var order in bots[p].Decide(match.View(p)))match.Submit(order);
            match.Step();
        }
        if(match.Winner==-1)match.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});
        string recording=JsonSerializer.Serialize(match.CopyRecordedCommands(0),json);
        var commands=JsonSerializer.Deserialize<RecordedCommand[]>(recording,json);
        check(commands.Select(c=>c.order).SequenceEqual(Enumerable.Range(0,commands.Length))&&commands.Any(c=>c.command.player==1)&&commands.Any(c=>c.command.kind==CommandKind.Build),"serialized chronology contains both sides, construction and a stable global order");
        var detached=match.CopyRecordedCommands(0);detached[0].command.entities[0]=0;
        check(match.CopyRecordedCommands(0)[0].command.entities[0]==worker,"archive readers cannot mutate the recording");
        var replay=new Simulation(map,catalog);bool outcomes=true;
        foreach(var entry in commands)
        {
            while(replay.Tick<entry.tick&&replay.Winner==-1)replay.Step();
            if(replay.Tick!=entry.tick)throw new Exception("Replay ended before its recorded command");
            var result=replay.Submit(entry.command);outcomes&=result.accepted==entry.accepted&&result.message==entry.rejection;
        }
        while(replay.Tick<match.Tick&&replay.Winner==-1)replay.Step();
        check(outcomes,"all accepted and rejected results reproduce without running the AI again");
        check(JsonSerializer.Serialize(match.Snapshot(),json)==JsonSerializer.Serialize(replay.Snapshot(),json),"six-minute replay reproduces the final authoritative world exactly");
        string savedReport=JsonSerializer.Serialize(MatchReportData.From(match.Report),json);
        var loaded=JsonSerializer.Deserialize<MatchReportData>(savedReport,json).Restore();
        check(JsonSerializer.Serialize(MatchReportData.From(loaded),json)==savedReport,"report serialization retains all samples, totals and build orders");
        check(JsonSerializer.Serialize(MatchReportData.From(replay.Report),json)==savedReport,"recorded orders reproduce the original match report exactly");
        var invalid=JsonSerializer.Deserialize<MatchReportData>(savedReport,json);invalid.schemaVersion=99;
        bool rejected=false;try{invalid.Restore();}catch(ArgumentException){rejected=true;}
        check(rejected,"unsupported report formats are rejected explicitly");
        invalid=JsonSerializer.Deserialize<MatchReportData>(savedReport,json);invalid.samples[1].tick=0;rejected=false;
        try{invalid.Restore();}catch(ArgumentException){rejected=true;}
        check(rejected,"damaged report timelines cannot be rendered as valid history");
    }
}
