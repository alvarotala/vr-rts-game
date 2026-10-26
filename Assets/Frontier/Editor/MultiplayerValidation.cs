using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Frontier.Core;
using Frontier.Networking;
using Frontier.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Frontier.Editor
{
    public static class MultiplayerValidation
    {
        private const string Scene="Assets/Frontier/Scenes/Frontier.unity";

        [MenuItem("Frontier/Multiplayer/Run session and offline regressions")]
        public static void RunRegressionChecks()
        {
            RunSessionChecks();
            OnlinePresentationChecks.Run();
            OnlinePlacementChecks.Run();
            HotkeyValidationBatch.Run();
            ReplayValidation.Run();
            Debug.Log("FRONTIER_MULTIPLAYER_REGRESSION passed; adapter, offline input, menus, archives and replay; no transport implied");
        }

        [MenuItem("Frontier/Multiplayer/Build macOS dedicated server")]
        public static void BuildMacServer()=>BuildMac("Builds/ServerMac/FrontierServer.app",StandaloneBuildSubtarget.Server);

        // Local validation fallback on a Mac without the Linux Server module. This
        // is a distinct standalone process launched with -batchmode -nographics.
        // It is not proof that a Linux Dedicated Server artifact compiles/runs.
        [MenuItem("Frontier/Multiplayer/Build macOS headless server")]
        public static void BuildMacHeadlessServer()=>BuildMac("Builds/HeadlessMac/FrontierServer.app",StandaloneBuildSubtarget.Player);

        private static void BuildMac(string output,StandaloneBuildSubtarget subtarget)
        {
            string platform=BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            string previous=EditorUserBuildSettings.GetPlatformSettings(platform,"Architecture");
            try
            {
                EditorUserBuildSettings.SetPlatformSettings(platform,"Architecture","ARM64");
                Build(output,BuildTarget.StandaloneOSX,(int)subtarget);
            }
            finally{EditorUserBuildSettings.SetPlatformSettings(platform,"Architecture",previous);}
        }

        [MenuItem("Frontier/Multiplayer/Check local and remote sessions")]
        public static void RunSessionChecks()
        {
            // Adapter tests inject already-decoded server views. Actual NGO transport
            // and independent processes are checked by scripts/check-multiplayer.py.
            var fixture=new GameObject("Multiplayer session checks");
            string directory=Path.Combine(Path.GetTempPath(),"frontier-network-session-"+Guid.NewGuid().ToString("N"));
            int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();
                if(session.Catalog==null)Call("Awake");
                session.LoadMap("training");
                var store=new MatchArchiveStore(directory);Field("history",store);Field("archiveInEditor",true);
                session.StartMatch();var offline=session.Match;object offlineIdentity=session.SessionIdentity;
                Check(!session.IsOnline&&session.LocalPlayer==0&&session.CanIssueOrders,"Offline starts with an authoritative local world and player zero.");
                int ownWorker=offline.Entities.Values.First(e=>e.owner==0&&offline.Type(e).role=="worker").id;
                session.Select(ownWorker,false);
                Check(session.Send(CommandKind.Hold)&&offline.Entities[ownWorker].order==CommandKind.Hold,"Offline commands still execute synchronously.");
                for(int i=0;i<8;i++)offline.Step();Call("RefreshView");
                session.Pause("menu",true);Check(offline.Paused,"Offline menu still pauses its local simulation.");
                session.Pause("menu",false);session.Send(CommandKind.Surrender);
                Check(session.Winner==1&&session.ResultReport!=null,"Offline concession produces its usual result.");
                session.LeaveMatch();
                var recording=store.List().Single();
                var originalFiles=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);

                var savedSetupMap=session.Map;string savedFaction=session.Faction,savedEnemy=session.EnemyFaction;
                BeginRemote();
                var authority=new Simulation(session.Map,session.Catalog);
                var first=authority.View(1);
                var observedEnemy=new VisibleEntity{id=90000,owner=0,definition=session.Catalog.Role("industrial","worker").id,position=new Point(64,64),complete=true,health=100};
                first.entities=first.entities.Concat(new[]{observedEnemy}).ToArray();Call("OnlineState",first);
                Check(session.IsOnline&&session.Running&&session.Match==null&&session.LocalPlayer==1&&!session.IsConnecting,"Remote player one starts without a second authoritative simulation.");
                Check(session.SessionIdentity!=offlineIdentity,"Remote and local matches use different input gesture identities.");
                int remoteWorker=first.entities.First(e=>e.owner==1&&!session.Catalog[e.definition].building).id;
                session.Select(remoteWorker,false);
                Check(session.Selection.SetEquals(new[]{remoteWorker}),"Remote player one can select its own units.");
                session.Select(observedEnemy.id,false);
                Check(session.Selection.Count==0,"A visible opponent unit cannot enter player one's selection.");
                session.SelectRectangle(new Point(0,0),new Point(session.Map.width,session.Map.height),false);
                Check(session.Selection.Count>0&&session.Selection.All(id=>first.entities.Any(e=>e.id==id&&e.owner==1)),"Rectangle selection respects the assigned remote seat.");
                string before=JsonUtility.ToJson(first);
                Check(!session.Send(CommandKind.Train,"missing")&&JsonUtility.ToJson(session.View)==before,"An unconnected remote order cannot spend resources or mutate a local world.");

                var pending=(Dictionary<int,GameCommand>)typeof(GameSession).GetField("pendingCommands",Private).GetValue(session);
                var move=new GameCommand{sequence=40,player=1,kind=CommandKind.Move,position=new Point(4,4)};
                pending.Add(40,move);Call("MarkOnlineCommand",move);
                int feedback=0;bool accepted=false;session.Feedback+=result=>{feedback++;accepted=result;};
                Call("OnlineAcknowledged",new CommandAck{sequence=40,accepted=false,message="Test rejection"});
                Check(pending.Count==0&&feedback==1&&!accepted&&session.LastCommandSequence==0,"Rejected asynchronous orders clear their pending pulse and deliver feedback.");
                Call("OnlineAcknowledged",new CommandAck{sequence=40,accepted=true});
                Check(feedback==1,"A duplicate acknowledgement does not replay UI feedback.");

                var second=Clone(first);second.tick=2;
                var moving=second.entities.Single(e=>e.id==remoteWorker);Point origin=moving.position;
                moving.position=new Point(origin.x+1,origin.y);Call("OnlineState",second);
                var presentation=(OnlinePresentation)typeof(GameSession).GetField("remotePresentation",Private).GetValue(session);
                double presentationNow=Time.unscaledTimeAsDouble;
                presentation.Reset();presentation.Receive(first,presentationNow-.2);presentation.Receive(second,presentationNow-.1);
                Point presented=session.PresentationPoint(moving);
                Check(Mathf.Abs(presented.x-origin.x-.5f)<.03f,"Regular remote frames interpolate between known positions.");
                moving.remembered=true;
                Check(session.PresentationPoint(moving).Distance(moving.position)<.001f,"Remembered fog positions are never interpolated.");
                moving.remembered=false;
                var hidden=Clone(second);hidden.tick=4;hidden.entities=hidden.entities.Where(e=>e.id!=remoteWorker).ToArray();Call("OnlineState",hidden);
                var returned=Clone(second);returned.tick=6;returned.entities.Single(e=>e.id==remoteWorker).position=new Point(origin.x+3,origin.y);Call("OnlineState",returned);
                moving=returned.entities.Single(e=>e.id==remoteWorker);
                Check(session.PresentationPoint(moving).Distance(moving.position)<.001f,"Reappearing entities snap to observed positions instead of crossing hidden territory.");
                var gap=Clone(returned);gap.tick=20;gap.entities.Single(e=>e.id==remoteWorker).position=new Point(origin.x+4,origin.y);Call("OnlineState",gap);
                moving=gap.entities.Single(e=>e.id==remoteWorker);
                Check(session.PresentationPoint(moving).Distance(moving.position)<.001f,"A large missing-state gap snaps rather than extrapolating.");

                Point? buildPoint=null;
                for(int y=0;y<session.Map.height&&!buildPoint.HasValue;y++)for(int x=0;x<session.Map.width;x++)
                    if(session.CanPreviewBuild(new Point(x,y),1)){buildPoint=new Point(x,y);break;}
                Check(buildPoint.HasValue,"Remote build preview finds visible public terrain without Session.Match.");
                var hq=gap.entities.First(e=>e.owner==1&&session.Catalog[e.definition].role=="hq");
                Check(!session.CanPreviewBuild(hq.position,1),"Remote build preview blocks known building occupancy.");
                var fogged=Clone(gap);fogged.tick=22;Array.Clear(fogged.visibility,0,fogged.visibility.Length);Call("OnlineState",fogged);
                Check(!session.CanPreviewBuild(buildPoint.Value,1),"Remote construction preview rejects unexplored cells.");
                session.Pause("menu",true);
                var whilePaused=Clone(gap);whilePaused.tick=24;Call("OnlineState",whilePaused);
                Check(session.IsPaused&&session.Tick==24&&session.Match==null,"Local menus block controls but continue accepting authoritative remote progress.");
                session.Pause("menu",false);

                for(int i=0;i<24;i++)authority.Step();authority.Submit(new GameCommand{player=0,kind=CommandKind.Surrender});
                var finalView=authority.View(1);
                var fallback=NetworkWire.PackResult(Encoding.UTF8.GetBytes(JsonUtility.ToJson(finalView)),null,out int fallbackLength,out bool reportUnavailable);
                string fallbackJson=Encoding.UTF8.GetString(NetworkWire.Decompress(fallback,fallbackLength,NetworkWire.MaxResultBytes));
                // Decode the real networking DTO through Unity, not a substitute JSON serializer.
                var envelopeType=typeof(NgoMatchClient).Assembly.GetType("Frontier.Networking.MatchResultEnvelope",true);
                var envelope=JsonUtility.FromJson(fallbackJson,envelopeType);
                var decodedView=(PlayerView)envelopeType.GetField("view").GetValue(envelope);
                bool decodedUnavailable=(bool)envelopeType.GetField("reportUnavailable").GetValue(envelope);
                var restoreReport=typeof(NgoMatchClient).GetMethod("RestoreFinalReport",BindingFlags.Static|BindingFlags.NonPublic);
                var unavailableReport=(MatchReport)restoreReport.Invoke(null,new[]{envelope});
                Check(reportUnavailable&&decodedUnavailable&&fallbackJson.Contains("\"report\":null")&&unavailableReport==null
                    &&decodedView.player==1&&decodedView.winner==1&&decodedView.tick==authority.Tick,
                    "The real client result validator accepts Unity's decoded unavailable-report envelope and preserves the terminal seat, winner and tick.");
                bool missingFlagRejected=false;
                try{restoreReport.Invoke(null,new[]{JsonUtility.FromJson(fallbackJson.Replace("\"reportUnavailable\":true","\"reportUnavailable\":false"),envelopeType)});}
                catch(TargetInvocationException error) when(error.InnerException is InvalidOperationException){missingFlagRejected=true;}
                Check(missingFlagRejected,"A missing report without the explicit unavailable flag is rejected by the actual client validator.");
                var complete=NetworkWire.PackResult(Encoding.UTF8.GetBytes(JsonUtility.ToJson(finalView)),Encoding.UTF8.GetBytes(JsonUtility.ToJson(MatchReportData.From(authority.Report))),out int completeLength,out _);
                var completeEnvelope=JsonUtility.FromJson(Encoding.UTF8.GetString(NetworkWire.Decompress(complete,completeLength,NetworkWire.MaxResultBytes)),envelopeType);
                var completeReport=(MatchReport)restoreReport.Invoke(null,new[]{completeEnvelope});
                Check(completeReport!=null&&JsonUtility.ToJson(MatchReportData.From(completeReport))==JsonUtility.ToJson(MatchReportData.From(authority.Report)),
                    "The actual client validator restores an available Unity-decoded report without changing its data.");
                Call("OnlineState",decodedView);object terminalIdentity=session.SessionIdentity;Call("OnlineFinished",(object)unavailableReport);
                Check(session.IsOnline&&session.Running&&ReferenceEquals(session.View,decodedView)&&session.SessionIdentity==terminalIdentity
                    &&session.LocalPlayer==1&&session.Winner==1&&session.Tick==authority.Tick&&session.OnlineReportUnavailable&&session.ResultReport==null,
                    "A null final report preserves the authoritative terminal view and session identity while marking statistics unavailable.");
                Debug.Log("FRONTIER_MULTIPLAYER_RESULT_FALLBACK passed: 4 checks; Unity JsonUtility, client result validator and GameSession adapter, no transport implied");
                session.LeaveMatch();BeginRemote();
                Call("OnlineState",authority.View(1));Call("OnlineDisconnected","Test loss before final report");
                Check(session.IsOnline&&session.Running&&session.Winner==1&&session.View!=null&&session.OnlineReportUnavailable&&session.ResultReport==null,
                    "A disconnect after terminal state preserves the outcome and marks the unavailable report.");
                session.LeaveMatch();
                Check(!session.OnlineReportUnavailable&&session.Map==savedSetupMap&&session.MapId=="training"&&session.Faction==savedFaction&&session.EnemyFaction==savedEnemy,
                    "Leaving remote play restores the saved offline map and both faction choices.");
                BeginRemote();Call("OnlineState",authority.View(1));Call("OnlineFinished",authority.Report);
                Check(session.Winner==1&&session.ResultReport==authority.Report,"Remote result is the server's report for the assigned player.");
                Call("OnlineDisconnected","Test loss after final report");
                Check(session.Running&&session.IsOnline&&!session.OnlineReportUnavailable&&session.ResultReport==authority.Report,
                    "A completed remote report remains readable after server disconnection.");
                session.LeaveMatch();
                Check(originalFiles.All(pair=>File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))&&Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Length==originalFiles.Count,"Remote play does not alter offline archives or create a fake replay.");
                Check(session.StartReplay(recording.Id)&&session.IsReplay&&!session.IsOnline,"The preserved offline recording still opens after remote play.");
                session.StopReplay();session.StartMatch();
                Check(!session.IsOnline&&session.Match!=null&&session.Match!=offline&&session.LocalPlayer==0&&session.CanIssueOrders,"Offline restarts with a fresh local simulation after remote play and replay.");
                session.LeaveMatch();
                Debug.Log($"FRONTIER_MULTIPLAYER_SESSION passed: {checks} adapter checks; no transport or player build implied");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Debug.Log("NETWORK_SESSION_PASS: "+message);}
                object Call(string method,params object[] args)=>typeof(GameSession).GetMethod(method,Private).Invoke(session,args);
                void Field(string name,object value)=>typeof(GameSession).GetField(name,Private).SetValue(session,value);
                void Property(string name,object value)=>typeof(GameSession).GetProperty(name).SetValue(session,value);
                PlayerView Clone(PlayerView view)=>JsonUtility.FromJson<PlayerView>(JsonUtility.ToJson(view));
                void BeginRemote()
                {
                    Field("onlineSetupMap",session.Map);Field("onlineSetupFaction",session.Faction);Field("onlineSetupEnemy",session.EnemyFaction);
                    Property("IsOnline",true);Property("IsConnecting",true);Field("remoteIdentity",new object());
                    Call("OnlineStarted",new MatchWelcome{mapId="rift",player=1,faction0="industrial",faction1="biological"});
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        }
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

        private static void Build(string output,BuildTarget target,int subtarget)
        {
            if(!File.Exists(Scene))throw new BuildFailedException("Configure Frontier's scene before building multiplayer.");
            string directory=Path.GetDirectoryName(output);Directory.CreateDirectory(directory);
            // Meta's build callback creates this sidecar without overwrite.
            string bindings=Path.Combine(directory,"RuntimeActionBindings.json");
            if(File.Exists(bindings))File.Delete(bindings);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{Scene},locationPathName=output,target=target,subtarget=subtarget,options=BuildOptions.Development
            });
            if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Multiplayer server build failed: "+result.summary.result);
            Debug.Log($"FRONTIER_SERVER_BUILT target={target} subtarget={subtarget} path={output}");
        }
    }
}
