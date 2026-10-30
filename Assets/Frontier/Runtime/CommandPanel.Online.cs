using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const string ServerAddressKey="frontier.multiplayer.address.v1",ServerPortKey="frontier.multiplayer.port.v1";
        // Start production builds on the public service even after a LAN playtest.
        // Keep the old preference and all per-endpoint account identities intact.
        private const string PublicBackendKey="frontier.multiplayer.service.v2";
        private bool endpointPort,endpointPublic;
        private string endpointDraft="",endpointMessage;
        private string MatchDescription=>Session.IsTutorial?"GUIDED TUTORIAL · PRACTICE AT YOUR OWN PACE":
            $"{ShortFaction(Session.Faction)} vs {ShortFaction(Session.EnemyFaction)} · "+(Session.IsOnline?(Session.IsPublicMatch?"PUBLIC 1V1":"MULTIPLAYER LAN"):DifficultyLabel+" AI");

        private void LoadOnlineEndpoint(string[] args=null)
        {
            // Explicit launch arguments take priority over a previously saved LAN endpoint.
            args=args??Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-frontier-client")<0)
            {
                string address=PlayerPrefs.GetString(ServerAddressKey,Session.ServerAddress);
                if(ValidServerAddress(address))Session.ServerAddress=address;
                int port=PlayerPrefs.GetInt(ServerPortKey,Session.ServerPort);
                if(port>0&&port<=ushort.MaxValue)Session.ServerPort=(ushort)port;
            }
            if(Array.IndexOf(args,"-frontier-backend")<0)
            {
                string saved=PlayerPrefs.GetString(PublicBackendKey,Session.PublicBackendUrl);
                if(ValidPublicBackend(saved))Session.PublicBackendUrl=saved;
            }
        }
        private static bool ValidServerAddress(string address)
        {
            if(string.IsNullOrEmpty(address))return false;
            var parts=address.Split('.');
            if(parts.Length!=4)return false;
            foreach(string part in parts)
                if(part.Length==0||part.Length>3||!byte.TryParse(part,NumberStyles.None,CultureInfo.InvariantCulture,out _))return false;
            return IPAddress.TryParse(address,out var ip)&&ip.AddressFamily==AddressFamily.InterNetwork;
        }
        private void BuildOnlineSetup()
        {
            if(Session.IsConnecting)
            {
                Label(actions,Session.IsMatchmaking?"Finding an opponent":"Opponent found",new Vector2(24,-32),new Vector2(992,64),27,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                Label(actions,"Keep this app open while your match is prepared.",new Vector2(40,-144),new Vector2(960,90),23,MenuMuted,TextAnchor.MiddleCenter);
                MenuFooter("Cancel search",CancelOnlineSetup,352,336);return;
            }
            FactionCard(false,Vector2.zero);
            MenuCard("Ranking","Your rating and the top 10 players.",new Vector2(528,0),new Vector2(512,116),()=>Show(MenuPage.Ranking));
            MenuCard("Connection","Online service and direct LAN connection.",new Vector2(0,-132),new Vector2(512,116),()=>Show(MenuPage.OnlineConnection));
            Label(actions,"Find an opponent near your rating. The range widens while you wait.",new Vector2(552,-150),new Vector2(464,84),22,MenuMuted,TextAnchor.UpperLeft);
            MenuFooter("Back",CancelOnlineSetup,0,336);
#if FRONTIER_BROWSER
            MenuFooter("Full map",()=>Quest.Navigation.Fit(),352,336);
#else
            MenuFooter("Position board",()=>{Quest.CancelStartRequest();PlaceRequested?.Invoke();},352,336);
#endif
            MenuFooter("FIND MATCH",()=>{endpointMessage=null;Quest.RequestPublicMatch();Rebuild();},704,336,true);
        }
        private void BuildOnlineConnection()
        {
            var service=PublicBackend();
            MenuCard("Online service",service.Host,Vector2.zero,new Vector2(512,116),()=>EditOnlineEndpoint(false,true));
            MenuCard("Service port: "+service.Port,"Use the port of your online service.",new Vector2(528,0),new Vector2(512,116),()=>EditOnlineEndpoint(true,true));
            MenuCard("Direct connection","Join a server by address on your local network.",new Vector2(0,-132),new Vector2(512,116),()=>Show(MenuPage.DirectSetup));
            Label(actions,"Direct LAN matches do not affect your rating.",new Vector2(552,-150),new Vector2(464,84),22,MenuMuted,TextAnchor.UpperLeft);
            MenuFooter("Back",()=>Show(MenuPage.OnlineSetup),352,336);
        }
        private void BuildDirectSetup()
        {
            if(Session.IsConnecting)
            {
                Label(actions,"Waiting for the server and your opponent",new Vector2(24,-32),new Vector2(992,64),27,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                Label(actions,Session.ServerAddress+":"+Session.ServerPort,new Vector2(24,-112),new Vector2(992,44),24,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                Label(actions,"Keep this app open. Both players need to join the same server.",new Vector2(40,-188),new Vector2(960,70),21,MenuMuted,TextAnchor.MiddleCenter);
                MenuFooter("Cancel connection",CancelOnlineSetup,352,336);return;
            }
            MenuCard("Server: "+Session.ServerAddress,"Enter the IPv4 address of the computer running the server.",Vector2.zero,new Vector2(512,116),()=>EditOnlineEndpoint(false));
            MenuCard("Port: "+Session.ServerPort,"Use the port configured on the server.",new Vector2(528,0),new Vector2(512,116),()=>EditOnlineEndpoint(true));
            Label(actions,"The server chooses the map and assigns each player's faction.",new Vector2(24,-144),new Vector2(992,52),22,MenuMuted,TextAnchor.MiddleCenter);
#if FRONTIER_BROWSER
            Label(actions,"Use your server computer's LAN address. 127.0.0.1 refers to this device.",new Vector2(24,-212),new Vector2(992,72),19,MenuMuted,TextAnchor.MiddleCenter);
#else
            Label(actions,"On Quest, use your server computer's LAN address. 127.0.0.1 refers to this device.",new Vector2(24,-212),new Vector2(992,72),19,MenuMuted,TextAnchor.MiddleCenter);
#endif
            MenuFooter("Back",()=>{Quest.CancelStartRequest();Show(MenuPage.OnlineSetup);},0,336);
#if FRONTIER_BROWSER
            MenuFooter("Full map",()=>Quest.Navigation.Fit(),352,336);
#else
            MenuFooter("Position board",()=>{Quest.CancelStartRequest();PlaceRequested?.Invoke();},352,336);
#endif
            MenuFooter("JOIN MATCH",()=>{endpointMessage=null;Quest.RequestOnlineMatch();Rebuild();},704,336,true);
        }
        private void EditOnlineEndpoint(bool port,bool publicService=false)
        {
            endpointPort=port;endpointPublic=publicService;
            endpointDraft=publicService?(port?PublicBackend().Port.ToString(CultureInfo.InvariantCulture):PublicBackend().Host)
                :port?Session.ServerPort.ToString(CultureInfo.InvariantCulture):Session.ServerAddress;
            endpointMessage=null;Show(publicService?MenuPage.PublicEndpoint:MenuPage.OnlineEndpoint);
        }
        private void BuildOnlineEndpoint()
        {
            string[] keys={"1","2","3","4","5","6","7","8","9",endpointPort?"Clear":".","0","Delete"};
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i];int col=i%3,row=i/3;
                var rect=Section(actions,"Endpoint key "+key,new Vector2(col*352,-row*70),new Vector2(336,60),button);
                Label(rect,key,Vector2.zero,new Vector2(336,60),27,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                targets.Add(new Target{rect=rect,image=rect.GetComponent<Image>(),normal=button,click=()=>
                {
                    endpointMessage=null;
                    if(key=="Delete"){if(endpointDraft.Length>0)endpointDraft=endpointDraft.Substring(0,endpointDraft.Length-1);}
                    else if(key=="Clear")endpointDraft="";
                    else if(endpointDraft.Length<(endpointPort?5:15))endpointDraft+=key;
                    UpdateMenuText();
                }});
            }
            MenuFooter("Cancel",()=>Show(endpointPublic?MenuPage.OnlineConnection:MenuPage.DirectSetup),0,336);
            MenuFooter("Clear",()=>{endpointDraft="";endpointMessage=null;UpdateMenuText();},352,336);
            MenuFooter("Save",SaveOnlineEndpoint,704,336,true);
        }
        private void SaveOnlineEndpoint()
        {
            if(endpointPublic)
            {
                var service=new UriBuilder(PublicBackend());
                if(endpointPort)
                {
                    if(!ushort.TryParse(endpointDraft,NumberStyles.None,CultureInfo.InvariantCulture,out ushort port)||port==0)
                    {endpointMessage="Enter a port from 1 to 65535.";UpdateMenuText();return;}
                    service.Port=port;
                }
                else
                {
                    if(!ValidServerAddress(endpointDraft))
                    {endpointMessage="Enter four numbers from 0 to 255, separated by dots.";UpdateMenuText();return;}
                    service.Host=IPAddress.Parse(endpointDraft).ToString();
                }
                Session.PublicBackendUrl=service.Uri.GetLeftPart(UriPartial.Authority);
                PlayerPrefs.SetString(PublicBackendKey,Session.PublicBackendUrl);PlayerPrefs.Save();
                endpointMessage=null;Show(MenuPage.OnlineConnection);return;
            }
            if(endpointPort)
            {
                if(!ushort.TryParse(endpointDraft,NumberStyles.None,CultureInfo.InvariantCulture,out ushort port)||port==0)
                {endpointMessage="Enter a port from 1 to 65535.";UpdateMenuText();return;}
                Session.ServerPort=port;PlayerPrefs.SetInt(ServerPortKey,port);
            }
            else
            {
                if(!ValidServerAddress(endpointDraft))
                {endpointMessage="Enter four numbers from 0 to 255, separated by dots.";UpdateMenuText();return;}
                Session.ServerAddress=IPAddress.Parse(endpointDraft).ToString();PlayerPrefs.SetString(ServerAddressKey,Session.ServerAddress);
            }
            PlayerPrefs.Save();endpointMessage=null;Show(MenuPage.DirectSetup);
        }
        private void CancelOnlineSetup()
        {
            Quest.CancelStartRequest();Session.CancelOnline();Show(MenuPage.Main);
        }
        private void UpdateOnlineHeader()
        {
            bool editing=Page==MenuPage.OnlineEndpoint||Page==MenuPage.PublicEndpoint;
            bool publicPage=Page==MenuPage.OnlineSetup||Page==MenuPage.PublicEndpoint||Page==MenuPage.OnlineConnection;
            headline.text=editing?(endpointPort?"SERVER PORT":"SERVER ADDRESS"):Page==MenuPage.OnlineConnection?"CONNECTION":publicPage?"MULTIPLAYER":"MULTIPLAYER LAN";
            economy.text=editing?"EDIT CONNECTION":Session.IsConnecting?(Session.IsMatchmaking?"SEARCHING FOR AN OPPONENT":"CONNECTING / WAITING"):publicPage?"PUBLIC 1V1":"DIRECT CONNECTION · 1V1";
            selection.text=editing?endpointDraft+"_":Page==MenuPage.OnlineSetup?RankingSummary:publicPage?"Choose your online service or a direct LAN server.":Session.ServerAddress+":"+Session.ServerPort;
            harvesting.text=editing?"Point and select keys. Save applies the new value.":Board.Placed?"BOARD READY":"POSITION YOUR BOARD BEFORE JOINING";
            status.text=editing?(endpointMessage??"Use the keypad on Quest or desktop."):
                !string.IsNullOrEmpty(Session.NetworkStatus)?Session.NetworkStatus:"Join the same dedicated server as your opponent.";
        }
        private void ReturnToOnlineSetup()
        {
            bool publicMatch=Session.IsPublicMatch;Session.LeaveMatch();Show(publicMatch?MenuPage.OnlineSetup:MenuPage.DirectSetup);
        }
        private Uri PublicBackend()=>ValidPublicBackend(Session.PublicBackendUrl)?new Uri(Session.PublicBackendUrl):new Uri(GameSession.DefaultPublicBackendUrl);
        private static bool ValidPublicBackend(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)
            &&(uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps)&&!string.IsNullOrEmpty(uri.Host)
            &&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment)&&uri.AbsolutePath=="/";
    }
}
