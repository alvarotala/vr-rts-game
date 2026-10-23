using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public bool IsOnline { get; private set; }
        public bool IsConnecting { get; private set; }
        public bool OnlineReportUnavailable { get; private set; }
        public int LocalPlayer => View?.player ?? 0;
        public int Winner => View?.winner ?? -1;
        public int Tick => View?.tick ?? 0;
        public MatchReport ResultReport => Match?.Report ?? remoteReport;
        public object SessionIdentity => (object)Match ?? remoteIdentity;
        public bool CanIssueOrders => Running && !IsReplay && !IsPaused && View != null && Winner == -1
            && (!IsOnline || remoteClient != null && remoteClient.IsConnected);
        public string ServerAddress = "127.0.0.1";
        public ushort ServerPort = 7777;
        public string NetworkStatus { get; private set; } = "Connect two clients to a dedicated server.";
        private NgoMatchClient remoteClient;
        private object remoteIdentity = new object();
        private MatchReport remoteReport;
        private readonly Dictionary<int, GameCommand> pendingCommands = new Dictionary<int, GameCommand>();
        private GridMap previewMap;
        private MapDefinition onlineSetupMap;
        private string onlineSetupFaction,onlineSetupEnemy;
        private readonly Dictionary<int,VisibleEntity> previousRemoteEntities=new Dictionary<int,VisibleEntity>();
        private float remoteFrameTime,remoteFrameDuration;

        public void ConnectOnline()
        {
            if(Running || IsConnecting) return;
            DisconnectOnline();
            CloseArchive();
            onlineSetupMap=Map;onlineSetupFaction=Faction;onlineSetupEnemy=EnemyFaction;
            Match=null;View=null;bot=null;remoteReport=null;
            IsOnline=true;IsConnecting=true;LastClosedWasReplay=false;
            OnlineReportUnavailable=false;
            remoteIdentity=new object();
            Selection.Clear();SelectedResource=0;foreach(var group in groups)group.Clear();
            BuildDefinition=null;AttackMoveMode=false;AbilityDefinitionId=null;LastCommandSequence=0;sequence=0;
            ArchiveStatus="Online reports are shown after the match. Local replay recording is available for offline matches.";
            ArchiveFailed=false;
            remoteClient=new GameObject("NGO match client").AddComponent<NgoMatchClient>();
            remoteClient.Started+=OnlineStarted;
            remoteClient.StateReceived+=OnlineState;
            remoteClient.CommandAcknowledged+=OnlineAcknowledged;
            remoteClient.Finished+=OnlineFinished;
            remoteClient.StatusChanged+=OnlineStatus;
            remoteClient.Disconnected+=OnlineDisconnected;
            OnlineStatus("Connecting to "+ServerAddress+":"+ServerPort+"…");
            if(!remoteClient.Connect(ServerAddress,ServerPort,Faction))
                OnlineDisconnected("Could not connect. Check the server address and port.");
        }

        public void CancelOnline()
        {
            if(IsOnline || IsConnecting)LeaveMatch();
        }

        private void OnlineStarted(MatchWelcome welcome)
        {
            LoadMap(welcome.mapId);
            Faction=welcome.player==0?welcome.faction0:welcome.faction1;
            EnemyFaction=welcome.player==0?welcome.faction1:welcome.faction0;
            pauseReasons.Remove("menu");
            OnlineStatus("Match starting · Player "+(welcome.player+1));
        }

        private void OnlineState(PlayerView view)
        {
            if(!IsOnline)return;
            bool first=!Running;
            previousRemoteEntities.Clear();
            if(View!=null&&view.tick>View.tick&&view.tick-View.tick<=6)
                foreach(var entity in View.entities)previousRemoteEntities[entity.id]=entity;
            remoteFrameDuration=View==null?0:(view.tick-View.tick)/(float)Simulation.TickRate;
            remoteFrameTime=Time.unscaledTime;
            ApplyView(view);
            Selection.RemoveWhere(id=>!view.entities.Any(e=>e.id==id&&e.owner==view.player));
            IsConnecting=false;Running=true;
            if(first)
            {
                PrepareCountdown((float)(remoteClient?.CountdownRemaining ?? 0));
                Announce("Connected · Your opponent is another player.");
                NewMatch?.Invoke();
            }
            if(view.winner!=-1)
            {
                PrepareCountdown(0);
                Announce(view.winner==view.player?"VICTORY · The territory is yours.":view.winner==2?"DRAW":"DEFEAT · Your colony has fallen.");
            }
            Changed?.Invoke();
        }

        private void OnlineFinished(MatchReport report)
        {
            remoteReport=report;OnlineReportUnavailable=report==null;
            NetworkStatus=report==null?"Match finished. The detailed report is unavailable.":"Match finished.";
            Changed?.Invoke();
        }

        private void OnlineStatus(string status)
        {
            NetworkStatus=status;Announce(status);Changed?.Invoke();
        }

        private void OnlineDisconnected(string reason)
        {
            // A completed report remains readable after the server shuts down.
            if(remoteReport!=null||Winner!=-1)
            {
                DisconnectOnline(keepResult:true);OnlineReportUnavailable=remoteReport==null;
                OnlineStatus(OnlineReportUnavailable?"Match finished. Connection lost before the final report arrived.":reason);return;
            }
            LeaveMatch();
            OnlineStatus(reason);
        }

        private void DisconnectOnline(bool keepResult=false)
        {
            if(remoteClient!=null)
            {
                var client=remoteClient;remoteClient=null;
                client.Started-=OnlineStarted;client.StateReceived-=OnlineState;
                client.CommandAcknowledged-=OnlineAcknowledged;client.Finished-=OnlineFinished;
                client.StatusChanged-=OnlineStatus;client.Disconnected-=OnlineDisconnected;
                client.Disconnect();Destroy(client.gameObject);
            }
            pendingCommands.Clear();IsConnecting=false;
            previousRemoteEntities.Clear();
            if(!keepResult)
            {
                IsOnline=false;remoteReport=null;remoteIdentity=new object();OnlineReportUnavailable=false;
                if(onlineSetupMap!=null)
                {
                    Map=onlineSetupMap;MapId=Map.id;Faction=onlineSetupFaction;EnemyFaction=onlineSetupEnemy;
                    onlineSetupMap=null;
                }
            }
        }

        private bool SendOnline(GameCommand command)
        {
            if(remoteClient==null || pendingCommands.Count>=128 || !remoteClient.Send(command))
            {Announce("Order could not be sent. Check the connection.",true);Feedback?.Invoke(false);return false;}
            pendingCommands.Add(command.sequence,command);
            // Selection/aim respond immediately; economy and combat wait for the server.
            MarkOnlineCommand(command);
            Announce("Order sent · awaiting server confirmation.");Changed?.Invoke();return true;
        }

        private void OnlineAcknowledged(CommandAck ack)
        {
            if(!pendingCommands.TryGetValue(ack.sequence,out var command))return;
            pendingCommands.Remove(ack.sequence);
            if(!ack.accepted)
            {
                if(LastCommandSequence==ack.sequence)LastCommandSequence=0;
                Announce(ack.message,true);
            }
            else if(command.kind!=CommandKind.Move&&command.kind!=CommandKind.Attack&&command.kind!=CommandKind.AttackMove)
                Announce("Order accepted.");
            Feedback?.Invoke(ack.accepted);Changed?.Invoke();
        }

        private void MarkOnlineCommand(GameCommand command)
        {
            if(command.kind!=CommandKind.Move&&command.kind!=CommandKind.Attack&&command.kind!=CommandKind.AttackMove)
            {LastCommandSequence=0;return;}
            var focus=command.kind==CommandKind.Attack?View.entities.FirstOrDefault(e=>e.id==command.target&&!e.remembered):null;
            float half=focus!=null&&Catalog[focus.definition].building?Catalog[focus.definition].footprint*.5f:0;
            LastCommandPoint=focus==null?command.position:new Point(focus.position.x+half,focus.position.y+half);
            LastCommandKind=command.kind;LastCommandSequence=command.sequence;LastCommandTime=Time.unscaledTime;
        }

        // One frame of visual buffering absorbs regular network cadence. Never
        // extrapolate into fog or interpolate an entity that has just reappeared.
        public Point PresentationPoint(VisibleEntity entity)
        {
            if(!IsOnline||remoteFrameDuration<=0||entity.remembered||Catalog[entity.definition].building
                ||!previousRemoteEntities.TryGetValue(entity.id,out var previous)||previous.remembered)
                return entity.position;
            float t=Mathf.Clamp01((Time.unscaledTime-remoteFrameTime)/remoteFrameDuration);
            return new Point(Mathf.Lerp(previous.position.x,entity.position.x,t),Mathf.Lerp(previous.position.y,entity.position.y,t));
        }

        // Resolve the same origin for the ghost and the submitted command. Only
        // observed deposits participate, including direct hits on their raised model.
        public Point BuildPlacement(Point point,int resourceId=0)
        {
            var origin=new Point(Mathf.Floor(point.x),Mathf.Floor(point.y));
            if(View==null||BuildDefinition==null||Catalog[BuildDefinition].role!="extractor")return origin;
            ResourceState nearest=null;float distance=2.5f;
            foreach(var resource in View.resources)
            {
                if(resource.kind!="catalyst"||resource.remaining==0)continue;
                if(resource.id==resourceId){nearest=resource;break;}
                float candidate=resource.position.Distance(point);
                if(candidate<distance||candidate==distance&&(nearest==null||resource.id<nearest.id))
                {nearest=resource;distance=candidate;}
            }
            if(nearest==null)return origin;
            float half=Catalog[BuildDefinition].footprint*.5f;
            return new Point(Mathf.Floor(nearest.position.x-half),Mathf.Floor(nearest.position.y-half));
        }

        // Placement preview uses public terrain and this player's observed buildings.
        // The authoritative server still checks occupancy, vision and resources.
        public bool CanPreviewBuild(Point point,int size)
        {
            if(View==null)return false;
            if(previewMap==null||previewMap.Definition!=Map)previewMap=new GridMap(Map);
            if(!previewMap.CanPlace(point,size))return false;
            int x=(int)Math.Floor(point.x),y=(int)Math.Floor(point.y);
            for(int dy=0;dy<size;dy++)for(int dx=0;dx<size;dx++)
                if(View.visibility[(y+dy)*Map.width+x+dx]!=2)return false;
            if(BuildDefinition!=null&&Catalog[BuildDefinition].role=="extractor")
            {
                var covered=View.resources.Where(r=>r.remaining!=0&&r.position.x>=point.x&&r.position.x<point.x+size
                    &&r.position.y>=point.y&&r.position.y<point.y+size).ToArray();
                if(covered.Length!=1||covered[0].kind!="catalyst")return false;
            }
            foreach(var e in View.entities)
            {
                var type=Catalog[e.definition];
                if(type.building&&point.x<e.position.x+type.footprint&&point.x+size>e.position.x
                    &&point.y<e.position.y+type.footprint&&point.y+size>e.position.y)return false;
            }
            return true;
        }
    }
}
