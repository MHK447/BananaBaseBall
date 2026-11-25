using UnityEditor;
using UnityEngine;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>
    /// Import settings for the game's recorded sounds (Resources/ColosseumAudio): short ones are unpacked into
    /// memory so that they start at once; the music and the crowd's murmur stay packed and are streamed.
    /// </summary>
    sealed class ColosseumAudioImport : AssetPostprocessor
    {
        const string Folder = "Assets/SlingArena/Colosseum/Resources/ColosseumAudio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool longPiece = name.StartsWith("bgm-") || name.StartsWith("crowd-loop");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = longPiece ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = longPiece ? .55f : .75f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = longPiece;
        }
    }
}
