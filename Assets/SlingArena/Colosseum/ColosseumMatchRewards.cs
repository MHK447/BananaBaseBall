using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>Match rewards and arena gifts can be claimed after returning to the game.</summary>
    internal static class ColosseumMatchRewards
    {
        const string WinKey = "colosseum.reward.pendingWin";
        const string LossKey = "colosseum.reward.pendingLoss";
        const string ArenaKey = "colosseum.reward.pendingArena";
        const string CursorKey = "colosseum.reward.arenaCard";
        const string LegacyPirateGift = "colosseum.reward.legacyArenaTwoPirate";
        const string PreviousArenaGiftKey = "colosseum.reward.previousArenaGift";

        internal static bool PendingWon => PlayerPrefs.HasKey(WinKey);
        internal static bool HasPendingMatch => PendingWon || PlayerPrefs.HasKey(LossKey);
        internal static int PendingArena => PlayerPrefs.GetInt(ArenaKey);
        internal static int ArenaCardCursor => PlayerPrefs.GetInt(CursorKey, -1);

#if UNITY_EDITOR
        internal static void ResetEditorProgress()
        {
            PlayerPrefs.DeleteKey(WinKey);
            PlayerPrefs.DeleteKey(LossKey);
            PlayerPrefs.DeleteKey(ArenaKey);
            PlayerPrefs.DeleteKey(CursorKey);
            PlayerPrefs.DeleteKey(LegacyPirateGift);
            PlayerPrefs.DeleteKey(PreviousArenaGiftKey);
        }
#endif

        internal static void PrepareMatch(bool won)
        {
            if (HasPendingMatch) return;
            string[] draws = ColosseumRoster.DrawCards(won ? 3 : 2);
            PlayerPrefs.SetString(won ? WinKey : LossKey, string.Join(",", draws));
            PlayerPrefs.Save();
        }

        internal static int[] PendingCards()
        {
            var cards = ColosseumRoster.CountDraws(PlayerPrefs.GetString(PendingWon ? WinKey : LossKey).Split(','));
            if (ColosseumRoster.FirstBattleLocked)
            {
                // An unclaimed pack from an older version may already contain the first-win recruit.
                cards[ColosseumRoster.Knight] += cards[ColosseumRoster.Archer];
                cards[ColosseumRoster.Archer] = 0;
            }
            return cards;
        }

        internal static bool ClaimMatch(out int[] cards)
        {
            cards = null;
            if (!HasPendingMatch) return false;
            bool won = PendingWon;
            cards = PendingCards();
            int oldStage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            ColosseumSave.RecordMatch(won, false);
            for (int hero = 0; hero < cards.Length; hero++)
                ColosseumRoster.GrantCards(hero, cards[hero]);
            int stage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            if (stage > oldStage) PlayerPrefs.SetInt(ArenaKey, stage);
            ColosseumRoster.RecordTutorialMatchClaim(won);
            PlayerPrefs.DeleteKey(won ? WinKey : LossKey);
            PlayerPrefs.Save();
            return true;
        }

        internal static List<int> ArenaCards(int stage)
        {
            var heroes = new List<int>();
            if (stage == 2 && PlayerPrefs.HasKey(LegacyPirateGift))
                heroes.Add(System.Array.FindIndex(ColosseumRoster.Heroes, h => h.Weapon == WeaponKind.Hook));
            // Older accounts already received the Orc and Goblin as starters. A persisted
            // arena-two cursor on those accounts refers only to their original Pirate gift.
            if (stage == 2 && !ColosseumRoster.HasFirstVictoryPath) return heroes;
            if (stage == PendingArena && PlayerPrefs.HasKey(PreviousArenaGiftKey))
            {
                string previous = PlayerPrefs.GetString(PreviousArenaGiftKey);
                int gift = System.Array.FindIndex(ColosseumRoster.Heroes, h => h.Key == previous);
                if (gift >= 0) heroes.Add(gift);
            }
            foreach (int i in ColosseumRoster.CollectionOrder)
                if (stage > 0 && ColosseumRoster.Heroes[i].ArenaReward == stage) heroes.Add(i);
            return heroes;
        }

        // Keep the previously promised card if the game was closed mid-promotion.
        internal static void MigratePendingArenaGift()
        {
            int stage = PendingArena;
            if (stage == 2 && ColosseumRoster.HasFirstVictoryPath && ArenaCardCursor == 1)
                PlayerPrefs.SetInt(CursorKey, 0); // The Orc was granted. The Goblin is now the only arena-two card.
            if (stage == 4 || stage == 5)
                PlayerPrefs.SetString(PreviousArenaGiftKey, stage == 4 ? "spike" : "pirate");
        }

        // The old arena-two chest promised Pirate. Preserve an earned or in-flight gift
        // across the table change, including a launch after Confirm and before the impact.
        internal static void PreserveLegacyArenaGift()
        {
            int reached = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            bool pirateLocked = System.Array.Find(ColosseumRoster.Heroes, h => h.Weapon == WeaponKind.Hook).Locked;
            if (PendingArena == 2 || (reached >= 2 && pirateLocked))
                PlayerPrefs.SetInt(LegacyPirateGift, 1);
        }

        // An existing save may have already passed a newly introduced guaranteed recruit.
        internal static void RecoverReachedArena()
        {
            if (PendingArena != 0) return;
            if (PlayerPrefs.HasKey(LegacyPirateGift))
            {
                PlayerPrefs.SetInt(ArenaKey, 2);
                PlayerPrefs.Save();
                return;
            }
            int reached = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            foreach (int id in ColosseumRoster.CollectionOrder)
            {
                var hero = ColosseumRoster.Heroes[id];
                if (!hero.Locked || hero.ArenaReward < 2 || hero.ArenaReward > reached) continue;
                PlayerPrefs.SetInt(ArenaKey, hero.ArenaReward);
                PlayerPrefs.Save();
                return;
            }
        }

        internal static void ConfirmArena(int stage)
        {
            if (stage == 0 || stage != PendingArena || ArenaCardCursor >= 0) return;
            if (ArenaCards(stage).Count == 0) PlayerPrefs.DeleteKey(ArenaKey);
            else PlayerPrefs.SetInt(CursorKey, 0);
            PlayerPrefs.Save();
        }

        internal static void GrantArenaCard(int stage, int cursor)
        {
            if (stage == 0 || stage != PendingArena || cursor < 0 || cursor != ArenaCardCursor) return;
            var heroes = ArenaCards(stage);
            if (cursor < heroes.Count) ColosseumRoster.GrantArenaHero(heroes[cursor]);
            if (cursor + 1 < heroes.Count) PlayerPrefs.SetInt(CursorKey, cursor + 1);
            else
            {
                PlayerPrefs.DeleteKey(CursorKey);
                PlayerPrefs.DeleteKey(ArenaKey);
                PlayerPrefs.DeleteKey(PreviousArenaGiftKey);
                if (stage == 2) PlayerPrefs.DeleteKey(LegacyPirateGift);
            }
            PlayerPrefs.Save();
        }
    }
}
