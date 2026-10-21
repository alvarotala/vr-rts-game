using System;
using System.Collections.Generic;
using System.IO;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        private MatchArchiveStore history;
        private MatchArchiveWriter archive;
        private bool archiveInEditor,archiveAbandoned;
        private float nextArchiveRetry;
        private readonly List<(MatchArchiveWriter writer,bool abandoned)> pendingArchives=new List<(MatchArchiveWriter,bool)>();
        public MatchArchiveStore History=>history??(history=new MatchArchiveStore(Path.Combine(Application.persistentDataPath,"Matches")));
        public string ArchiveStatus {get;private set;}
        public bool ArchiveFailed {get;private set;}
        private void StartArchive()
        {
            ArchiveStatus=null;ArchiveFailed=false;archiveAbandoned=false;
            // Editor fixtures opt into an isolated temporary store. They never write fake matches to player history.
            if(!Application.isPlaying&&!archiveInEditor)return;
            try {archive=History.Begin(Match,Difficulty);UpdateArchive(true);}
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){ArchiveFailure(error);}
        }
        private void UpdateArchive(bool force=false)
        {
            if(archive==null||!force&&ArchiveFailed&&Time.unscaledTime<nextArchiveRetry)return;
            try
            {
                if(Match.Report!=null)archive.Finish(archiveAbandoned);
                else if(force||archive.Due)archive.Flush();
                ArchiveFailed=false;ArchiveStatus=archive.Finished?"Saved on this device. Find this report in Match history.":"Recording this match on this device.";
            }
            catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){ArchiveFailure(error);}
        }
        private void ArchiveFailure(Exception error)
        {
            if(!ArchiveFailed)Debug.LogWarning("Match history could not be saved: "+error.Message);
            nextArchiveRetry=Time.unscaledTime+5;
            ArchiveFailed=true;ArchiveStatus="Could not save this match. Check device storage, then Retry save.";
        }
        public void RetryArchiveSave()=>UpdateArchive(true);
        private void CloseArchive()
        {
            if(archive!=null)
            {
                if(Match.Winner==-1)
                {
                    // Leaving is an explicit concession. Include it as the terminal replay command.
                    archiveAbandoned=true;bool paused=Match.Paused;Match.Paused=false;
                    Match.Submit(new GameCommand{player=0,kind=CommandKind.Surrender});Match.Paused=paused;
                }
                UpdateArchive(true);
                if(!archive.Finished)pendingArchives.Add((archive,archiveAbandoned));
                archive=null;
            }
            RetryPendingArchives();
        }
        private void RetryPendingArchives()
        {
            for(int i=pendingArchives.Count-1;i>=0;i--)
                try{pendingArchives[i].writer.Finish(pendingArchives[i].abandoned);pendingArchives.RemoveAt(i);}
                catch(Exception error) when(MatchArchiveStore.IsStorageError(error)){ArchiveFailure(error);}
        }
        private void OnApplicationQuit(){UpdateArchive(true);RetryPendingArchives();}
        private void OnDestroy(){UpdateArchive(true);RetryPendingArchives();DisconnectOnline();}
    }
}
