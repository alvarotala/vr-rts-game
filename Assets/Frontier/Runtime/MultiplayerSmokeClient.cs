using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Frontier.Core;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Runtime
{
    // An opt-in player process, not a server bot: every order travels through NGO.
    // It consumes only PlayerView, exactly like a human player's presentation.
    public sealed class MultiplayerSmokeClient : MonoBehaviour
    {
        private NgoMatchClient client;
        private Strategist strategist;
        private Catalog catalog;
        private MapDefinition map;
        private MatchReport report;
        private PlayerView latest;
        private readonly Dictionary<int,CommandKind> commands=new Dictionary<int,CommandKind>();
        private readonly Dictionary<int,bool> outcomes=new Dictionary<int,bool>();
        private int player=-1,sequence,lastDecision=-1,lastTick=-1,nextProgress,accepted,builds,trains,attacks,duplicateAcks;
        private bool duplicateSent,surrenderSent,finished,verifyingRank;
        private float startedAt;
        private IPublicMatchmaker publicMatchmaker;
        private string expectedMatchId;
        private IRankingService rankingService;
        private string rankingBackend;

        public static async void LaunchPublic(string backendUrl,string faction)
        {
            var smoke=Create();
            string identity=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
            smoke.publicMatchmaker=new NakamaMatchmaker(identity);
            smoke.rankingService=new NakamaRankingService(identity);smoke.rankingBackend=backendUrl;
            try
            {
                var rank=await smoke.rankingService.GetRankingAsync(backendUrl,System.Threading.CancellationToken.None);
                if(rank.profile.rating!=1000||rank.profile.games!=0||rank.profile.rank!=0)
                {smoke.Fail("A new ranked account did not start with the expected rating.");return;}
                var connection=await smoke.publicMatchmaker.FindAsync(backendUrl,faction,System.Threading.CancellationToken.None,
                    message=>Debug.Log("FRONTIER_PUBLIC_SEARCH "+message));
                if(smoke==null||smoke.finished)return;
                smoke.expectedMatchId=connection.matchId;
                Debug.Log("FRONTIER_PUBLIC_ALLOCATED match="+connection.matchId+" host="+connection.host+" port="+connection.port);
                if(!smoke.client.Connect(connection.host,(ushort)connection.port,connection.faction,connection.matchId,connection.joinTicket))
                    smoke.Fail("Could not start allocated NGO client.");
            }
            catch(Exception){if(smoke!=null&&!smoke.finished)smoke.Fail("Public matchmaking did not complete.");}
        }

        public static void Launch(string address,ushort port,string faction)
        {
            var smoke=Create();
            if(!smoke.client.Connect(address,port,faction))smoke.Fail("Could not start NGO client.");
        }

        private static MultiplayerSmokeClient Create()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            var smoke=new GameObject("Network validation client").AddComponent<MultiplayerSmokeClient>();
            smoke.startedAt=Time.realtimeSinceStartup;
            smoke.client=smoke.gameObject.AddComponent<NgoMatchClient>();
            smoke.client.Started+=smoke.OnStarted;
            smoke.client.StateReceived+=smoke.OnState;
            smoke.client.CommandAcknowledged+=smoke.OnAcknowledged;
            smoke.client.Finished+=smoke.OnFinished;
            smoke.client.StatusChanged+=message=>Debug.Log("FRONTIER_SMOKE_STATUS "+message);
            smoke.client.Disconnected+=message=>{if(!smoke.finished&&!smoke.verifyingRank)smoke.Fail("Disconnected before verified result: "+message);};
            return smoke;
        }

        private void OnStarted(MatchWelcome welcome)
        {
            if(expectedMatchId!=null&&welcome.matchId!=expectedMatchId){Fail("NGO welcome differs from the authenticated allocation.");return;}
            publicMatchmaker?.ConfirmJoined();
            player=welcome.player;
            catalog=new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
            map=JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/"+welcome.mapId).text);
            strategist=new Strategist(player,player==0?AiDifficulty.Hard:AiDifficulty.Normal,catalog,map);
            Debug.Log($"FRONTIER_SMOKE_STARTED player={player} map={welcome.mapId}");
        }

        private void OnState(PlayerView view)
        {
            if(finished)return;
            if(strategist==null||view.player!=player||view.tick<lastTick||view.visibility==null||view.visibility.Length!=map.width*map.height)
            {Fail("Invalid, misrouted or backwards player view.");return;}
            foreach(var entity in view.entities.Where(e=>e.owner!=player))
                if(entity.queue.Length!=0||entity.pendingOrders.Length!=0||entity.activeOrderSequence!=0||entity.energy!=0
                    ||entity.abilityCooldowns.Length!=0||entity.cargo!=0||entity.hasRallyPoint||entity.target!=0)
                {Fail("Private enemy state in PlayerView.");return;}
            latest=view;lastTick=view.tick;attacks+=view.attacks.Length;
            if(view.tick>=nextProgress)
            {
                nextProgress=view.tick+Simulation.TickRate*60;
                Debug.Log($"FRONTIER_SMOKE_PROGRESS player={player} tick={view.tick} accepted={accepted} builds={builds} trains={trains} attacks={attacks}");
            }
            if(view.winner!=-1){TryFinish();return;}
            if(client.CountdownRemaining>0)return;
            // Rendering/network samples need not land exactly on an AI decision tick.
            int decision=view.tick/Simulation.TickRate;
            if(decision!=lastDecision)
            {
                lastDecision=decision;
                int actualTick=view.tick;view.tick=decision*Simulation.TickRate;
                try
                {
                    foreach(var command in strategist.Decide(view))
                    {
                        Submit(command);
                        if(!duplicateSent)
                        {
                            duplicateSent=true;
                            if(!client.Send(command)){Fail("Could not send duplicate acknowledgement probe.");return;}
                        }
                    }
                }
                finally{view.tick=actualTick;}
            }
            // A concession is a complete normal game outcome. Bound this test even
            // when equally capable bots remain in a strategic stalemate.
            if(player==1&&!surrenderSent&&view.tick>=Simulation.TickRate*900&&builds>0&&trains>0&&attacks>0)
            {
                surrenderSent=true;
                Submit(new GameCommand{kind=CommandKind.Surrender});
            }
        }

        private void Submit(GameCommand command)
        {
            command.sequence=++sequence;
            // The server must derive ownership from the connection, not this field.
            command.player=1-player;
            commands.Add(command.sequence,command.kind);
            if(!client.Send(command))Fail("NGO rejected a local command send.");
        }

        private void OnAcknowledged(CommandAck acknowledgement)
        {
            if(finished)return;
            if(!commands.TryGetValue(acknowledgement.sequence,out var kind)){Fail("Acknowledgement has no sent command.");return;}
            if(outcomes.TryGetValue(acknowledgement.sequence,out bool original))
            {
                if(original!=acknowledgement.accepted){Fail("A duplicate command changed its outcome.");return;}
                duplicateAcks++;return;
            }
            outcomes.Add(acknowledgement.sequence,acknowledgement.accepted);
            if(!acknowledgement.accepted)return;
            accepted++;
            if(kind==CommandKind.Build)builds++;
            if(kind==CommandKind.Train)trains++;
        }

        private void OnFinished(MatchReport result){report=result;TryFinish();}

        private void TryFinish()
        {
            if(finished||verifyingRank||report==null||latest==null||latest.winner<0)return;
            if(report.Winner!=latest.winner||report.Tick!=latest.tick||accepted<10||builds<1||trains<1||duplicateAcks<1
                ||report.Players.Any(p=>p.WorkersProduced<1||p.StructuresCompleted<1||p.ArmyProduced<1)
                ||report.Players.Sum(p=>p.ArmyLost)<1)
            {Fail("Terminal view/report mismatch or missing economy, construction, combat or duplicate ACK evidence.");return;}
            string digest;
            using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(MatchReportData.From(report))))).Replace("-","").ToLowerInvariant();
            if(rankingService!=null){verifyingRank=true;VerifyPublicRating(digest);return;}
            Complete(digest);
        }

        private async void VerifyPublicRating(string digest)
        {
            using(var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(35)))
            try
            {
                while(!finished)
                {
                    var value=await rankingService.GetMatchResultAsync(rankingBackend,expectedMatchId,timeout.Token);
                    if(value.status=="pending"){await System.Threading.Tasks.Task.Delay(1000,timeout.Token);continue;}
                    int delta=report.Winner==2?0:report.Winner==player?16:-16;
                    if(value.status!="rated"||value.ratingBefore!=1000||value.ratingAfter!=1000+delta
                        ||value.ratingDelta!=delta||value.opponentRating!=1000)
                    {Fail("The authoritative rating does not match the completed game.");return;}
                    var snapshot=await rankingService.GetRankingAsync(rankingBackend,timeout.Token);
                    if(snapshot.profile.rank==0){await System.Threading.Tasks.Task.Delay(1000,timeout.Token);continue;}
                    if(snapshot.profile.rating!=value.ratingAfter||snapshot.profile.games!=1
                        ||snapshot.profile.wins!=(report.Winner==player?1:0)
                        ||snapshot.profile.draws!=(report.Winner==2?1:0)
                        ||snapshot.profile.losses!=(report.Winner!=2&&report.Winner!=player?1:0))
                    {Fail("The ranked profile does not agree with its match result.");return;}
                    Debug.Log($"FRONTIER_RANK_PASSED player={player} before={value.ratingBefore} after={value.ratingAfter} delta={value.ratingDelta} rank={snapshot.profile.rank}");
                    Complete(digest);return;
                }
            }
            catch(Exception){Fail("Could not verify the persisted match rating.");}
        }

        private void Complete(string digest)
        {
            if(finished)return;
            finished=true;
            Debug.Log($"FRONTIER_SMOKE_PASSED player={player} winner={report.Winner} tick={report.Tick} digest={digest} accepted={accepted} builds={builds} trains={trains} duplicateAcks={duplicateAcks}");
            // Give NGO a few frames to flush its final-report acknowledgement.
            Invoke(nameof(QuitSuccessfully),.5f);
        }

        private void Update()
        {
            if(!finished&&Time.realtimeSinceStartup-startedAt>480)Fail("Timed out waiting for a complete match.");
        }
        private void QuitSuccessfully()=>Application.Quit(0);
        private void OnDestroy()=>publicMatchmaker?.Dispose();
        private void Fail(string reason)
        {
            if(finished)return;
            finished=true;Debug.LogError("FRONTIER_SMOKE_FAILED "+reason);Application.Quit(2);
        }
    }
}
