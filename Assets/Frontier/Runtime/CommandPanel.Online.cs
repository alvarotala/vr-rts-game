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
        private bool endpointPort;
        private string endpointDraft="",endpointMessage;
        private string MatchDescription=>$"{ShortFaction(Session.Faction)} vs {ShortFaction(Session.EnemyFaction)} · "+(Session.IsOnline?"MULTIPLAYER LAN":DifficultyLabel+" AI");

        private void LoadOnlineEndpoint()
        {
            // Explicit launch arguments take priority over a previously saved LAN endpoint.
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-frontier-client")>=0)return;
            string address=PlayerPrefs.GetString(ServerAddressKey,Session.ServerAddress);
            if(ValidServerAddress(address))Session.ServerAddress=address;
            int port=PlayerPrefs.GetInt(ServerPortKey,Session.ServerPort);
            if(port>0&&port<=ushort.MaxValue)Session.ServerPort=(ushort)port;
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
                Label(actions,"Waiting for the server and your opponent",new Vector2(24,-32),new Vector2(992,64),27,ConsoleTheme.Ink,TextAnchor.MiddleCenter);
                Label(actions,Session.ServerAddress+":"+Session.ServerPort,new Vector2(24,-112),new Vector2(992,44),24,accent,TextAnchor.MiddleCenter);
                Label(actions,"Keep this app open. Both players need to join the same server.",new Vector2(40,-188),new Vector2(960,70),21,MenuMuted,TextAnchor.MiddleCenter);
                MenuFooter("Cancel connection",CancelOnlineSetup,352,336);return;
            }
            MenuCard("Server: "+Session.ServerAddress,"Enter the IPv4 address of the computer running the server.",Vector2.zero,new Vector2(512,116),()=>EditOnlineEndpoint(false));
            MenuCard("Port: "+Session.ServerPort,"Use the port configured on the server.",new Vector2(528,0),new Vector2(512,116),()=>EditOnlineEndpoint(true));
            Label(actions,"The server chooses the map and assigns each player's faction.",new Vector2(24,-144),new Vector2(992,52),22,MenuMuted,TextAnchor.MiddleCenter);
            Label(actions,"On Quest, use your server computer's LAN address. 127.0.0.1 refers to this device.",new Vector2(24,-212),new Vector2(992,72),19,MenuMuted,TextAnchor.MiddleCenter);
            MenuFooter("Back",CancelOnlineSetup,0,336);
            MenuFooter("Position board",()=>{Quest.CancelStartRequest();PlaceRequested?.Invoke();},352,336);
            MenuFooter("JOIN MATCH",()=>{endpointMessage=null;Quest.RequestOnlineMatch();Rebuild();},704,336,true);
        }
        private void EditOnlineEndpoint(bool port)
        {
            endpointPort=port;endpointDraft=port?Session.ServerPort.ToString(CultureInfo.InvariantCulture):Session.ServerAddress;
            endpointMessage=null;Show(MenuPage.OnlineEndpoint);
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
            MenuFooter("Cancel",()=>Show(MenuPage.OnlineSetup),0,336);
            MenuFooter("Clear",()=>{endpointDraft="";endpointMessage=null;UpdateMenuText();},352,336);
            MenuFooter("Save",SaveOnlineEndpoint,704,336,true);
        }
        private void SaveOnlineEndpoint()
        {
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
            PlayerPrefs.Save();endpointMessage=null;Show(MenuPage.OnlineSetup);
        }
        private void CancelOnlineSetup()
        {
            Quest.CancelStartRequest();Session.CancelOnline();Show(MenuPage.Main);
        }
        private void UpdateOnlineHeader()
        {
            headline.text=Page==MenuPage.OnlineEndpoint?(endpointPort?"SERVER PORT":"SERVER ADDRESS"):"MULTIPLAYER LAN";
            economy.text=Page==MenuPage.OnlineEndpoint?"EDIT CONNECTION":Session.IsConnecting?"CONNECTING / WAITING":"DIRECT CONNECTION · 1V1";
            selection.text=Page==MenuPage.OnlineEndpoint?endpointDraft+"_":Session.ServerAddress+":"+Session.ServerPort;
            harvesting.text=Page==MenuPage.OnlineEndpoint?"Point and select keys. Save applies the new value.":Board.Placed?"BOARD READY":"POSITION YOUR BOARD BEFORE JOINING";
            status.text=Page==MenuPage.OnlineEndpoint?(endpointMessage??"Use the keypad on Quest or desktop."):
                !string.IsNullOrEmpty(Session.NetworkStatus)?Session.NetworkStatus:"Join the same dedicated server as your opponent.";
        }
        private void ReturnToOnlineSetup()
        {
            Session.LeaveMatch();Show(MenuPage.OnlineSetup);
        }
    }
}
