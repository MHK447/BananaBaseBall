using TMPro;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>Project assets the Colosseum scene loads by name (kept in a Resources folder so builds include them).</summary>
    public sealed class ColosseumAssets : ScriptableObject
    {
        public TMP_FontAsset brawlFont;
        public Texture2D lobbyIllustration;
    }
}
