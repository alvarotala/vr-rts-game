using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public string PublicBackendUrl="http://127.0.0.1:7350";
        public bool IsMatchmaking {get;private set;}
        public bool IsPublicMatch {get;private set;}
        public Func<IPublicMatchmaker> MatchmakerFactory=()=>new NakamaMatchmaker();
        private IPublicMatchmaker publicMatchmaker;
        private CancellationTokenSource publicSearchCancellation;
        private int publicSearchGeneration;
        private bool publicMatchJoined;
        private string publicAllocatedMatchId;

        public async Task FindPublicMatchAsync()
        {
            if(Running||IsConnecting)return;
            PrepareOnlineSession(true);
            IsMatchmaking=true;
            var cancellation=new CancellationTokenSource();
            publicSearchCancellation=cancellation;
            int generation=publicSearchGeneration;
            try
            {
                var provider=MatchmakerFactory();
                if(provider==null)throw new InvalidOperationException("Missing matchmaker.");
                publicMatchmaker=provider;
                OnlineStatus("Signing in for online play…");
                var connection=await provider.FindAsync(PublicBackendUrl,Faction,cancellation.Token,status=>
                {
                    if(IsMatchmaking&&CurrentPublicSearch(generation,cancellation))OnlineStatus(status);
                });
                if(!CurrentPublicSearch(generation,cancellation))return;
                if(connection==null||string.IsNullOrWhiteSpace(connection.host)||connection.port<1||connection.port>65535
                    ||string.IsNullOrEmpty(connection.matchId)||string.IsNullOrEmpty(connection.joinTicket)
                    ||connection.faction!="industrial"&&connection.faction!="biological")
                    throw new InvalidOperationException("Invalid allocation.");
                IsMatchmaking=false;
                publicAllocatedMatchId=connection.matchId;
                ConnectRemote(connection.host,(ushort)connection.port,connection.faction,connection.matchId,connection.joinTicket);
            }
            catch(OperationCanceledException)
            {
                if(CurrentPublicSearch(generation,cancellation))FailPublicSearch("The search timed out. Try again.");
            }
            catch(Exception)
            {
                // Never display credentials, raw transport exceptions or server response bodies.
                if(CurrentPublicSearch(generation,cancellation))FailPublicSearch("Could not find a match. Check the connection and try again.");
            }
        }

        private bool CurrentPublicSearch(int generation,CancellationTokenSource cancellation)
            =>this!=null&&IsOnline&&IsPublicMatch&&generation==publicSearchGeneration&&!cancellation.IsCancellationRequested;

        private void FailPublicSearch(string message)
        {
            DisconnectOnline();
            NetworkStatus=message;Announce(message,true);Changed?.Invoke();
        }

        private void ConfirmPublicMatchJoined()
        {
            if(!IsPublicMatch||publicMatchmaker==null)return;
            publicMatchJoined=true;
            publicMatchmaker.ConfirmJoined();
        }

        private void CancelPublicSearch()
        {
            publicSearchGeneration++;IsMatchmaking=false;publicAllocatedMatchId=null;
            var provider=publicMatchmaker;publicMatchmaker=null;
            var cancellation=publicSearchCancellation;publicSearchCancellation=null;
            bool cancelRemote=!publicMatchJoined;publicMatchJoined=false;
            cancellation?.Cancel();
            if(provider!=null)_=ReleasePublicMatchmaker(provider,cancellation,cancelRemote);
            else cancellation?.Dispose();
        }

        private static async Task ReleasePublicMatchmaker(IPublicMatchmaker provider,CancellationTokenSource cancellation,bool cancelRemote)
        {
            try {if(cancelRemote)await provider.CancelAsync();}
            catch(Exception) {Debug.LogWarning("Online search cancellation could not be confirmed.");}
            finally {provider.Dispose();cancellation?.Dispose();}
        }
    }
}
