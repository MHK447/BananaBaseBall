using UnityEngine;

namespace SlingArena.Colosseum
{
    internal static class ColosseumJourneyMaps
    {
        // Load each arena only as it is reached.
        static readonly (string name, string resource, Color32 accent)[] Maps =
        {
            ("ROYAL SANDPIT", "RoyalSandpitGate", new Color32(255, 204, 120, 255)),
            ("TIDAL BASTION", "TidalBastion", new Color32(83, 240, 211, 255)),
            ("EMBER CROWN", "EmberCrown", new Color32(255, 133, 80, 255)),
            ("FROSTSPIRE", "Frostspire", new Color32(122, 218, 255, 255)),
            ("PHARAOH SANDS", "PharaohSands", new Color32(255, 214, 92, 255)),
            ("JADE TEMPLE", "JadeTemple", new Color32(98, 226, 150, 255)),
            ("GRIDIRON BOWL", "GridironBowl", new Color32(255, 168, 72, 255)),
        };

        static int Index(int stageNumber) => (Mathf.Max(1, stageNumber) - 1) % Maps.Length;
        public static string Name(int stageNumber) => Maps[Index(stageNumber)].name;
        public static Color Accent(int stageNumber) => Maps[Index(stageNumber)].accent;

        public static Texture2D Texture(int stageNumber, Texture2D royalSandpit, out int seatingLayout)
        {
            int index = Index(stageNumber);
            Texture2D map = Resources.Load<Texture2D>("ColosseumJourneyMaps/" + Maps[index].resource);
            seatingLayout = map != null ? index : -1;
            return map ?? royalSandpit;
        }
    }
}
