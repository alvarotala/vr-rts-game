using System;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    // Long background tracks stream from the player package instead of keeping
    // several minutes of decoded stereo PCM in the Quest's memory.
    public sealed class MapMusicImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/Frontier/Resources/Music/",StringComparison.Ordinal))return;
            var importer=(AudioImporter)assetImporter;
            importer.forceToMono=false;
            importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.8f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=false;
            importer.defaultSampleSettings=settings;
        }
    }
}
