using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Networking;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class PublicMatchmakingChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

        [MenuItem("Frontier/Multiplayer/Check public search lifecycle")]
        public static void Run()
        {
            // Fakes deliberately ignore cancellation, so a late completion exercises
            // the session's own generation guard. No sockets or account service here.
            var context=SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            var fixture=new GameObject("Public matchmaking checks");
            int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();
                if(session.Catalog==null)Call("Awake");
                session.LoadMap("training");
                session.ServerAddress="192.168.50.2";session.ServerPort=7788;
                session.PublicBackendUrl="http://192.168.50.3:7350";
                session.Faction="biological";
                var savedMap=session.Map;
                var provider=new FakeMatchmaker();int created=0;
                session.MatchmakerFactory=()=>{created++;return provider;};
                Task first=session.FindPublicMatchAsync();
                Check(session.IsOnline&&session.IsPublicMatch&&session.IsConnecting&&session.IsMatchmaking&&!session.Running,
                    "Search reserves the online flow before any authentication or allocation completes.");
                Check(provider.Backend==session.PublicBackendUrl&&provider.Faction=="biological", "Search forwards service and selected faction.");
                Check(session.FindPublicMatchAsync().IsCompleted&&created==1,"Repeated Find cannot create a second search.");
                Check(!session.StartReplay("not-a-replay"),"Replay cannot start while searching.");
                provider.Report("Finding an opponent");
                Check(session.NetworkStatus=="Finding an opponent","Current provider status reaches the menu.");
                session.CancelOnline();
                Check(provider.Token.IsCancellationRequested&&provider.CancelCalls==1&&provider.DisposeCalls==1,
                    "Cancellation signals the request, cancels its remote search and disposes the provider.");
                Check(!session.IsOnline&&!session.IsPublicMatch&&!session.IsConnecting&&!session.IsMatchmaking&&!session.Running,
                    "Cancel returns to an idle session.");
                string status=session.NetworkStatus;
                provider.Report("Stale status");provider.Completion.SetResult(Connection());
                Check(first.IsCompleted&&!first.IsFaulted&&session.NetworkStatus==status&&!session.IsConnecting,
                    "A cancelled provider cannot reconnect or overwrite status when it completes late.");
                Check(session.ServerAddress=="192.168.50.2"&&session.ServerPort==7788&&session.Map==savedMap&&session.Faction=="biological",
                    "Cloud searching preserves the direct endpoint and offline setup.");

                provider=new FakeMatchmaker();Task failure=session.FindPublicMatchAsync();
                provider.Completion.SetException(new InvalidOperationException("sensitive diagnostic must not reach UI"));
                Check(failure.IsCompleted&&!failure.IsFaulted&&!session.IsConnecting&&!session.IsOnline&&session.MessageIsError,
                    "An authentication/search failure is handled and leaves retry available.");
                Check(!session.NetworkStatus.Contains("sensitive")&&provider.CancelCalls==1,"Failures expose only safe feedback and cancel the search.");

                provider=new FakeMatchmaker();Task invalid=session.FindPublicMatchAsync();
                var bad=Connection();bad.port=0;provider.Completion.SetResult(bad);
                Check(invalid.IsCompleted&&!session.IsConnecting&&provider.CancelCalls==1,
                    "Malformed allocations fail before creating a network client.");

                provider=new FakeMatchmaker();Task interrupted=session.FindPublicMatchAsync();
                session.StartMatch();var offline=session.Match;
                provider.Completion.SetResult(Connection());
                Check(interrupted.IsCompleted&&provider.CancelCalls==1&&!session.IsOnline&&session.Running&&session.Match==offline,
                    "Starting offline invalidates the pending search and ignores its late allocation.");
                session.LeaveMatch();

                provider=new FakeMatchmaker();Task older=session.FindPublicMatchAsync();var stale=provider;
                session.CancelOnline();provider=new FakeMatchmaker();Task newer=session.FindPublicMatchAsync();
                stale.Completion.SetException(new InvalidOperationException("stale error"));
                Check(older.IsCompleted&&session.IsConnecting&&session.IsMatchmaking&&provider.CancelCalls==0,
                    "An older failure cannot cancel a replacement search.");
                session.CancelOnline();provider.Completion.SetCanceled();
                Check(newer.IsCompleted&&!newer.IsFaulted,"A cancelled replacement observes cancellation without an unhandled task.");

                provider=new FakeMatchmaker();Task joined=session.FindPublicMatchAsync();
                typeof(GameSession).GetField("publicAllocatedMatchId",Private).SetValue(session,"test-match");
                Call("OnlineStarted",new MatchWelcome{matchId="test-match",mapId="rift",player=0,faction0="industrial",faction1="biological"});
                Check(provider.ConfirmCalls==1,"The server welcome confirms the successful handoff.");
                session.LeaveMatch();provider.Completion.SetCanceled();
                Check(joined.IsCompleted&&provider.CancelCalls==0&&provider.DisposeCalls==1,
                    "Leaving an acknowledged match disposes its provider without cancelling the started allocation.");

                provider=new FakeMatchmaker();Task mismatch=session.FindPublicMatchAsync();
                typeof(GameSession).GetField("publicAllocatedMatchId",Private).SetValue(session,"expected-match");
                Call("OnlineStarted",new MatchWelcome{matchId="other-match",mapId="rift",player=0,faction0="industrial",faction1="biological"});
                provider.Completion.SetCanceled();
                Check(mismatch.IsCompleted&&provider.ConfirmCalls==0&&provider.CancelCalls==1&&!session.IsOnline,
                    "A welcome for another allocation cannot commit the handoff.");

                var board=fixture.AddComponent<BoardView>();board.Session=session;
                var quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                provider=new FakeMatchmaker();int before=created;board.Placed=false;
                quest.RequestPublicMatch();
                Check(quest.Placing&&quest.StartPending&&created==before&&!session.IsConnecting,
                    "A missing board defers authentication and ticket creation.");
                quest.CancelPlacement();board.Placed=true;
                Check(!quest.StartPending&&!session.IsConnecting,"Cancelling placement also cancels the pending Find action.");
                quest.RequestPublicMatch();
                Check(session.IsMatchmaking&&created==before+1&&!quest.StartPending,"A placed board starts public search once.");
                session.CancelOnline();provider.Completion.SetCanceled();

                provider=new FakeMatchmaker();Task destroyed=session.FindPublicMatchAsync();
                Check(session.IsMatchmaking&&!destroyed.IsCompleted,"The destruction check starts with a live pending search.");
                // This edit-mode fixture invokes Awake explicitly; Unity has not
                // entered the runtime MonoBehaviour lifecycle. Exercise the real
                // OnDestroy callback explicitly before destroying its native object.
                Call("OnDestroy");
                Check(provider.Token.IsCancellationRequested&&provider.CancelCalls==1&&provider.DisposeCalls==1,
                    "The runtime OnDestroy callback cancels and disposes the active search.");
                Object.DestroyImmediate(fixture);fixture=null;
                provider.Completion.SetResult(Connection());
                Check(destroyed.IsCompleted&&!destroyed.IsFaulted&&provider.CancelCalls==1&&provider.DisposeCalls==1,
                    "A destroyed session rejects late allocation without repeating remote cleanup.");
                Debug.Log($"FRONTIER_PUBLIC_MATCHMAKING passed: {checks} checks; fake providers, no backend or transport implied");

                void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
                object Call(string name,params object[] args)=>typeof(GameSession).GetMethod(name,Private).Invoke(session,args);
            }
            finally
            {
                if(fixture!=null)Object.DestroyImmediate(fixture);
                SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        private static PublicMatchConnection Connection()=>new PublicMatchConnection
            {matchId="test-match",host="allocated.invalid",port=7777,joinTicket="test-only",faction="biological"};

        private sealed class FakeMatchmaker:IPublicMatchmaker
        {
            public readonly TaskCompletionSource<PublicMatchConnection> Completion=new TaskCompletionSource<PublicMatchConnection>();
            public int CancelCalls,DisposeCalls,ConfirmCalls;
            public string Backend,Faction;
            public CancellationToken Token;
            private Action<string> status;
            public Task<PublicMatchConnection> FindAsync(string backendUrl,string faction,CancellationToken cancellation,Action<string> report)
            {Backend=backendUrl;Faction=faction;Token=cancellation;status=report;return Completion.Task;}
            public Task CancelAsync(){CancelCalls++;return Task.CompletedTask;}
            public void ConfirmJoined()=>ConfirmCalls++;
            public void Dispose()=>DisposeCalls++;
            public void Report(string message)=>status?.Invoke(message);
        }
    }
}
