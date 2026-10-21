using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public enum ArchivedMatchState { Interrupted, Completed, Abandoned }
    [Serializable] public sealed class MatchArchiveHeader
    {
        public int schemaVersion=1, replayRulesVersion=Simulation.ReplayRulesVersion, tickRate=Simulation.TickRate;
        public string id, startedUtc, mapId, mapTitle, faction, enemyFaction, difficulty;
        public string appVersion, buildGuid, unityVersion, platform, rulesHash;
    }
    [Serializable] public sealed class MatchArchiveRules
    {
        public MapDefinition map;
        public CatalogDefinition catalog;
        // Diagnostic initial state, plus frozen constructor inputs. This is not a resumable snapshot.
        public string initialStateJson;
    }
    [Serializable] public sealed class MatchArchiveProgress
    {
        public int schemaVersion=1,tick,commands,segments,winner=-1;
        public ArchivedMatchState state;
        public string savedUtc,stateHash,reportHash;
    }
    [Serializable] public sealed class MatchArchiveSegment
    {
        public int schemaVersion=1,firstOrder,tick;
        public RecordedCommand[] commands;
        public string stateHash;
    }
    public sealed class SavedMatchInfo
    {
        public string Id,Error;
        public MatchArchiveHeader Header;
        public MatchArchiveProgress Progress;
        public bool HasReport=>Error==null&&Progress!=null&&Progress.state!=ArchivedMatchState.Interrupted;
    }
    public sealed class SavedMatchReport
    {
        public SavedMatchInfo Info;
        public MatchReport Report;
        public Catalog Catalog;
    }

    public sealed class MatchArchiveStore
    {
        public readonly string Root;
        public MatchArchiveStore(string root){Root=root;}
        internal string Folder(string id)
        {
            if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid match ID.");
            return Path.Combine(Root,id);
        }
        internal static string ReadText(string path)
        {
            if(new FileInfo(path).Length>32*1024*1024)throw new IOException("Match file exceeds the supported size.");
            return File.ReadAllText(path);
        }
        internal static T Read<T>(string path)=>JsonUtility.FromJson<T>(ReadText(path));
        internal static void AtomicWrite(string path,string text)
        {
            string temporary=path+".tmp";
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {var bytes=Encoding.UTF8.GetBytes(text);file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        internal static void Write(string path,object data)=>AtomicWrite(path,JsonUtility.ToJson(data));
        public static string Hash(string text)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        public MatchArchiveWriter Begin(Simulation match,string difficulty)
        {
            match.BeginRecording();
            string id=Guid.NewGuid().ToString("N"),folder=Folder(id);
            var rules=new MatchArchiveRules{map=match.Map.Definition,catalog=match.Catalog.Definition,initialStateJson=JsonUtility.ToJson(match.Snapshot())};
            string json=JsonUtility.ToJson(rules);
            var header=new MatchArchiveHeader{id=id,startedUtc=DateTime.UtcNow.ToString("O"),mapId=rules.map.id,mapTitle=rules.map.title,
                faction=match.Players[0].faction,enemyFaction=match.Players[1].faction,difficulty=difficulty,
                appVersion=Application.version,buildGuid=Application.buildGUID,unityVersion=Application.unityVersion,platform=Application.platform.ToString(),rulesHash=Hash(json)};
            return new MatchArchiveWriter(folder,match,header,json);
        }
        public SavedMatchInfo[] List()
        {
            if(!Directory.Exists(Root))return Array.Empty<SavedMatchInfo>();
            var matches=new List<SavedMatchInfo>();
            foreach(string folder in Directory.EnumerateDirectories(Root))
            {
                string id=Path.GetFileName(folder);if(!Guid.TryParseExact(id,"N",out _))continue;
                matches.Add(Inspect(id));
            }
            return matches.OrderByDescending(m=>m.Header?.startedUtc??"").ThenBy(m=>m.Id).ToArray();
        }
        public SavedMatchInfo Inspect(string id)
        {
            var info=new SavedMatchInfo{Id=id};
            try
            {
                string folder=Folder(id);info.Header=Read<MatchArchiveHeader>(Path.Combine(folder,"header.json"));
                if(info.Header==null||info.Header.schemaVersion!=1||info.Header.id!=id)throw new ArgumentException("Unsupported match archive.");
                info.Progress=Read<MatchArchiveProgress>(Path.Combine(folder,"progress.json"));
                if(info.Progress==null||info.Progress.schemaVersion!=1||info.Progress.tick<0||info.Progress.commands<0||info.Progress.segments<1
                    ||!Enum.IsDefined(typeof(ArchivedMatchState),info.Progress.state))throw new ArgumentException("Invalid match progress.");
            }
            catch(Exception error) when(IsStorageError(error)){info.Error=error.Message;}
            return info;
        }
        public MatchArchiveRules ReadRules(SavedMatchInfo info)
        {
            string json=ReadText(Path.Combine(Folder(info.Id),"rules.json"));
            if(Hash(json)!=info.Header.rulesHash)throw new ArgumentException("Saved rules are damaged.");
            var rules=JsonUtility.FromJson<MatchArchiveRules>(json);
            if(rules?.map==null||rules.catalog==null||string.IsNullOrEmpty(rules.initialStateJson))throw new ArgumentException("Missing saved rules.");
            return rules;
        }
        public SavedMatchReport LoadReport(string id)
        {
            var info=Inspect(id);
            if(!info.HasReport)throw new ArgumentException(info.Error??"This match was interrupted. Replay data is retained, but no final report is available.");
            string json=ReadText(Path.Combine(Folder(id),"report.json"));
            if(Hash(json)!=info.Progress.reportHash)throw new ArgumentException("Saved report is damaged.");
            var report=JsonUtility.FromJson<MatchReportData>(json)?.Restore();
            if(report==null||report.Tick!=info.Progress.tick||report.Winner!=info.Progress.winner)throw new ArgumentException("Saved report does not match its record.");
            return new SavedMatchReport{Info=info,Report=report,Catalog=new Catalog(ReadRules(info).catalog)};
        }
        public Simulation CreateReplay(SavedMatchInfo info)
        {
            if(info.Error!=null||info.Header==null||info.Progress==null)throw new ArgumentException("Replay record is unavailable.");
            if(info.Header.replayRulesVersion!=Simulation.ReplayRulesVersion||info.Header.tickRate!=Simulation.TickRate)
                throw new NotSupportedException("This recording needs a different simulation version.");
            var rules=ReadRules(info);
            var replay=new Simulation(rules.map,new Catalog(rules.catalog),info.Header.faction,info.Header.enemyFaction);
            if(Hash(JsonUtility.ToJson(replay.Snapshot()))!=Hash(rules.initialStateJson))
                throw new NotSupportedException("The recorded starting state is incompatible with this simulation.");
            return replay;
        }
        public IEnumerable<MatchArchiveSegment> ReadSegments(SavedMatchInfo info)
        {
            int order=0,tick=0;
            for(int i=0;i<info.Progress.segments;i++)
            {
                var segment=Read<MatchArchiveSegment>(Path.Combine(Folder(info.Id),$"orders-{i:000000}.json"));
                if(segment==null||segment.schemaVersion!=1||segment.firstOrder!=order||segment.tick<tick||segment.commands==null)
                    throw new ArgumentException("Incomplete replay segment.");
                foreach(var command in segment.commands)
                {
                    if(command==null||command.command==null||command.order!=order++||command.tick<tick||command.tick>segment.tick)
                        throw new ArgumentException("Invalid replay command order.");
                    tick=command.tick;
                }
                tick=segment.tick;yield return segment;
            }
            if(order!=info.Progress.commands||tick!=info.Progress.tick)throw new ArgumentException("Incomplete replay history.");
        }
        public static bool IsStorageError(Exception e)=>e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException||e is InvalidOperationException;
    }

    public sealed class MatchArchiveWriter
    {
        private readonly string folder;
        private readonly Simulation match;
        private readonly MatchArchiveHeader header;
        private readonly string rulesJson;
        private bool initialized;
        private int commands,segments,lastTick=-1;
        private MatchArchiveProgress progress;
        public bool Finished {get;private set;}
        public bool Due=>match.Tick-lastTick>=Simulation.TickRate*15;
        internal MatchArchiveWriter(string folder,Simulation match,MatchArchiveHeader header,string rulesJson)
        {this.folder=folder;this.match=match;this.header=header;this.rulesJson=rulesJson;}
        public void Flush()
        {
            if(Finished||lastTick==match.Tick&&commands==match.RecordedCommandCount)return;
            if(!initialized)
            {
                Directory.CreateDirectory(folder);
                MatchArchiveStore.AtomicWrite(Path.Combine(folder,"rules.json"),rulesJson);
                MatchArchiveStore.Write(Path.Combine(folder,"header.json"),header);initialized=true;
            }
            var segment=new MatchArchiveSegment{firstOrder=commands,tick=match.Tick,commands=match.CopyRecordedCommands(commands),stateHash=MatchArchiveStore.Hash(JsonUtility.ToJson(match.Snapshot()))};
            // A segment and its progress marker are committed independently. The marker is
            // authoritative: orphan/temp segments after interruption are never replayed.
            MatchArchiveStore.Write(Path.Combine(folder,$"orders-{segments:000000}.json"),segment);
            var next=new MatchArchiveProgress{tick=match.Tick,commands=match.RecordedCommandCount,segments=segments+1,savedUtc=DateTime.UtcNow.ToString("O"),stateHash=segment.stateHash};
            MatchArchiveStore.Write(Path.Combine(folder,"progress.json"),next);
            progress=next;commands=next.commands;segments=next.segments;lastTick=next.tick;
        }
        public void Finish(bool abandoned=false)
        {
            if(Finished)return;
            if(match.Report==null)throw new InvalidOperationException("The match must finish before its report is archived.");
            Flush();string json=JsonUtility.ToJson(MatchReportData.From(match.Report));
            MatchArchiveStore.AtomicWrite(Path.Combine(folder,"report.json"),json);
            var completed=new MatchArchiveProgress{tick=progress.tick,commands=progress.commands,segments=progress.segments,savedUtc=DateTime.UtcNow.ToString("O"),
                stateHash=progress.stateHash,reportHash=MatchArchiveStore.Hash(json),winner=match.Winner,state=abandoned?ArchivedMatchState.Abandoned:ArchivedMatchState.Completed};
            MatchArchiveStore.Write(Path.Combine(folder,"progress.json"),completed);progress=completed;Finished=true;
        }
    }
}
