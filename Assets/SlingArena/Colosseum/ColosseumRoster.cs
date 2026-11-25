using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The player's heroes (level, hero cards, upgrades) and the local league table shown on the league page.
    /// Everything is kept in PlayerPrefs next to the rest of the Colosseum save.
    /// </summary>
    public static class ColosseumRoster
    {
        public sealed class Hero
        {
            public string Key, Name, Skill, SkillDescription;
            public WeaponKind Weapon;
            public int Rarity;     // 0 common, 1 rare, 2 epic, 3 legendary: picks the card colour on the prefab
            public bool Starter;
            public int ArenaReward;
            public int WinsToRecruit;
            internal string RecruitmentKey => Prefix + Key + ".recruitment";
            public bool Locked => !Starter && PlayerPrefs.GetInt(RecruitmentKey) == 0;
        }

        /// <summary>Stable hero IDs shared by saves, combat and portraits.</summary>
        public static readonly Hero[] Heroes =
        {
            new Hero { Key = "axe", Weapon = WeaponKind.Axe, Name = "VIKING", Skill = "WHIRLWIND", SkillDescription = "Spins a whirlwind that pulls enemies in and hurls them sky-high.", Rarity = 0, WinsToRecruit = 8 },
            new Hero { Key = "sword", Weapon = WeaponKind.Sword, Name = "KNIGHT", Skill = "STAB", SkillDescription = "Charges through enemies and knocks them back.", Rarity = 0, Starter = true },
            new Hero { Key = "hammer", Weapon = WeaponKind.Slam, Name = "ORC", Skill = "SLAM", SkillDescription = "Leaps and slams the ground. Enemies in the inner ring take 30% more damage.", Rarity = 2, WinsToRecruit = 3 },
            new Hero { Key = "bow", Weapon = WeaponKind.Bow, Name = "ARCHER", Skill = "VOLLEY", SkillDescription = "Fires an arrow ahead as she dashes into battle.", Rarity = 1, WinsToRecruit = 1 },
            new Hero { Key = "goblin", Weapon = WeaponKind.Mine, Name = "GOBLIN", Skill = "MINE", SkillDescription = "Sticks a ticking bomb to each enemy he bumps.", Rarity = 1, WinsToRecruit = 5 },
            new Hero { Key = "spike", Weapon = WeaponKind.Bounce, Name = "SPIKE", Skill = "BOUNCE", SkillDescription = "Pricks enemies on each bounce, then lands with a heavy slam.", Rarity = 1, ArenaReward = 5 },
            new Hero { Key = "pirate", Weapon = WeaponKind.Hook, Name = "PIRATE", Skill = "HOOK", SkillDescription = "Scoops enemies onto his hook before firing his cannon.", Rarity = 2, ArenaReward = 4 },
            new Hero { Key = "brute", Weapon = WeaponKind.Boomerang, Name = "BRUTE", Skill = "BOOMERANG", SkillDescription = "Rides a looping blade that cuts enemies on the way out and back.", Rarity = 3, ArenaReward = 6 },
            new Hero { Key = "dawn", Weapon = WeaponKind.Star, Name = "DAWN", Skill = "STARFALL", SkillDescription = "Calls down falling stars around the target area.", Rarity = 3, ArenaReward = 7 },
        };

        public const int LineupSize = 4, MaxLevel = 20;
        const string LineupKey = "colosseum.lineup";
        const string Prefix = "colosseum.hero.";
        const string RecruitmentVersionKey = Prefix + "recruitmentVersion";
        const string LessonKey = "colosseum.onboarding.lesson";
        const string RecruitUpgradeKey = "colosseum.onboarding.arenaUpgrade";
        const string FirstVictoryKey = "colosseum.onboarding.firstVictory";
        const int AwaitingFirstWin = 1, AwaitingArcherBattle = 2, ArcherBattleStarted = 3;
        // Version 16 saved step 2 after a practice shot, even if no real match had been played.
        const int ShotLesson = 1, LegacyPracticeComplete = 2, AwaitingMatchClaim = 3, UpgradeAfterClaim = 4;
        internal const int Knight = 1, Orc = 2, Archer = 3;
        // Show and auto-equip heroes in the order they become available without changing their IDs.
        internal static readonly int[] CollectionOrder = { Knight, Archer, Orc, 4, 0, 6, 5, 7, 8 };
        const int PendingReveal = 1, Collected = 2;
        static readonly int[] CardsToLevel = new int[MaxLevel - 1]
        {
            5, 8, 12, 16, 20, 30, 45, 65, 90,
            120, 155, 195, 240, 290, 345, 405, 470, 540, 615
        };
        static readonly string[] Rivals =
        {
            "BoneKing", "Maximus", "SandViper", "IronTusk", "Lucius", "ArenaFox", "Spartak", "GoldLion", "Brutus", "NovaBlade",
            "RedComet", "Titus", "MadOrc", "Valeria", "StoneFist", "Cassia", "WildBoar", "Nero", "QuickBow", "Aurelia",
            "ThunderAxe", "Felix", "DustDevil", "Octavia", "BigHammer", "Remus", "SlyArrow", "Flavia", "KingSlam"
        };

        public static int Level(int hero) { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + Heroes[hero].Key + ".level", 1), 1, MaxLevel); }

        /// <summary>Upgrade copies in hand. Starters and heroes from pre-recruitment saves began with two.</summary>
        public static int Cards(int hero) => Mathf.Max(0, PlayerPrefs.GetInt(Prefix + Heroes[hero].Key + ".cards", Heroes[hero].Locked ? 0 : 2));

#if UNITY_EDITOR
        internal static void ResetEditorProgress()
        {
            foreach (Hero hero in Heroes)
            {
                PlayerPrefs.DeleteKey(Prefix + hero.Key + ".level");
                PlayerPrefs.DeleteKey(Prefix + hero.Key + ".cards");
                PlayerPrefs.DeleteKey(hero.RecruitmentKey);
            }
            PlayerPrefs.DeleteKey(LineupKey);
            PlayerPrefs.DeleteKey(RecruitmentVersionKey);
            PlayerPrefs.DeleteKey(LessonKey);
            PlayerPrefs.DeleteKey(RecruitUpgradeKey);
            PlayerPrefs.DeleteKey(FirstVictoryKey);
            PlayerPrefs.DeleteKey(WindowRecruitKey);
        }
#endif

        /// <summary>Runs before the first lobby generates a name or creates journey progress.</summary>
        public static void InitializeCollection()
        {
            int previousVersion = PlayerPrefs.GetInt(RecruitmentVersionKey);
            if (previousVersion >= 4) return;
            if (previousVersion == 3)
            {
                ColosseumMatchRewards.MigratePendingArenaGift();
                GrantWinRecruits();
                PlayerPrefs.SetInt(RecruitmentVersionKey, 4);
                PlayerPrefs.Save();
                return;
            }
            bool returning = previousVersion > 0 || PlayerPrefs.HasKey("colosseum.name") || PlayerPrefs.HasKey("colosseum.avatar")
                || PlayerPrefs.HasKey("colosseum.trophies") || PlayerPrefs.HasKey("colosseum.coins")
                || PlayerPrefs.HasKey("colosseum.journey.trophies") || PlayerPrefs.HasKey("colosseum.journey.claimed")
                || PlayerPrefs.HasKey("colosseum.rewards") || PlayerPrefs.HasKey(LineupKey)
                || ColosseumMatchRewards.HasPendingMatch || ColosseumMatchRewards.PendingArena != 0;
            foreach (Hero hero in Heroes)
                returning |= PlayerPrefs.HasKey(Prefix + hero.Key + ".level") || PlayerPrefs.HasKey(Prefix + hero.Key + ".cards")
                    || PlayerPrefs.HasKey(hero.RecruitmentKey);
            if (returning)
            {
                // In v2 these three were always owned, even without a recruitment record.
                foreach (var hero in Heroes)
                    if ((hero.Weapon == WeaponKind.Slam || hero.Weapon == WeaponKind.Mine || hero.Weapon == WeaponKind.Bow)
                        && PlayerPrefs.GetInt(hero.RecruitmentKey) == 0)
                        PlayerPrefs.SetInt(hero.RecruitmentKey, Collected);
                if (previousVersion < 2 && Heroes[0].Locked)
                    PlayerPrefs.SetInt(Heroes[0].RecruitmentKey, Collected);
                if (previousVersion == 0)
                {
                    // All nine were available before the recruitment system existed.
                    foreach (Hero hero in Heroes)
                        PlayerPrefs.SetInt(hero.RecruitmentKey, Collected);
                }
                if (previousVersion < 2) ColosseumMatchRewards.PreserveLegacyArenaGift();
                ColosseumMatchRewards.MigratePendingArenaGift();
            }
            else
            {
                PlayerPrefs.SetString(LineupKey, Heroes[Knight].Key);
                PlayerPrefs.SetInt(FirstVictoryKey, AwaitingFirstWin);
                PlayerPrefs.SetInt(LessonKey, ShotLesson);
            }
            PlayerPrefs.SetInt(RecruitmentVersionKey, 4);
            PlayerPrefs.Save();
        }

        internal static bool FirstBattleLocked => PlayerPrefs.GetInt(FirstVictoryKey) == AwaitingFirstWin;
        internal static bool NeedsArcherBattle => PlayerPrefs.GetInt(FirstVictoryKey) == AwaitingArcherBattle;
        internal static bool HasFirstVictoryPath => PlayerPrefs.HasKey(FirstVictoryKey);
        // Accounts from before the first-victory tutorial already had unrestricted access.
        internal static int WinsToUnlockHeroes => HasFirstVictoryPath ? Mathf.Max(0, 3 - ColosseumSave.MatchWins) : 0;
        // HEROES opens when the first trophy-road chest has been collected. A save that has already levelled a hero up stays open.
        internal static bool HeroesTabLocked => HasFirstVictoryPath && !ColosseumSave.AnyChestCollected && !AnyHeroLevelled;
        static bool AnyHeroLevelled
        {
            get
            {
                for (int i = 0; i < Heroes.Length; i++)
                    if (Level(i) > 1) return true;
                return false;
            }
        }

        internal static void StartArcherBattle()
        {
            if (!NeedsArcherBattle) return;
            PlayerPrefs.SetInt(FirstVictoryKey, ArcherBattleStarted);
            PlayerPrefs.Save();
        }

        internal static bool NeedsShotLesson
        {
            get
            {
                int step = PlayerPrefs.GetInt(LessonKey);
                return step == ShotLesson || step == LegacyPracticeComplete;
            }
        }

        internal static int PendingUpgradeLesson
        {
            get
            {
                if (HeroesTabLocked || NeedsArcherBattle || PlayerPrefs.GetInt(LessonKey) != UpgradeAfterClaim) return -1;
                if (CanUpgrade(Knight)) return Knight;
                for (int i = 0; i < Heroes.Length; i++)
                    if (CanUpgrade(i)) return i;
                return -1;
            }
        }
        internal static int PendingRecruitUpgradeLesson
        {
            get
            {
                string key = PlayerPrefs.GetString(RecruitUpgradeKey);
                return System.Array.FindIndex(Heroes, hero => hero.Key == key);
            }
        }
        internal static string UnlockHint(int hero)
        {
            int wins = Heroes[hero].WinsToRecruit;
            if (wins == 1) return "WIN 1 BATTLE";
            return wins > 1 ? "WIN " + wins + " BATTLES" : "ARENA " + Heroes[hero].ArenaReward;
        }

        internal static bool FirstArenaRecruit(int hero) => Heroes[hero].Locked && Level(hero) < MaxLevel &&
            !PlayerPrefs.HasKey(RecruitUpgradeKey);

        internal static int RecruitUpgradeCopies(int hero) => Mathf.Max(0, CardsNeeded(hero) - Cards(hero));
        internal static int RecruitUpgradeCoins(int hero) => Mathf.Max(0, UpgradeCost(hero) - ColosseumSave.Coins);

        // The arena cursor and these rewards are committed in the same PlayerPrefs.Save call.
        internal static void GrantArenaHero(int hero)
        {
            bool first = FirstArenaRecruit(hero);
            int extraCopies = first ? RecruitUpgradeCopies(hero) : 0;
            GrantCards(hero, 1 + extraCopies, revealInCollection: true);
            if (!first) return;
            ColosseumSave.EnsureTrainingCoins(UpgradeCost(hero));
            PlayerPrefs.SetString(RecruitUpgradeKey, Heroes[hero].Key);
        }

        // The first chest opens HEROES for the upgrade lesson. It leaves the Knight with exactly the cards and coins
        // for one level, and nobody else with enough for any, so the lesson is one upgrade and no more.
        internal static void PrepareFirstUpgrade()
        {
            for (int i = 0; i < Heroes.Length; i++)
            {
                if (Heroes[i].Locked) continue;
                int needed = CardsNeeded(i);
                if (needed <= 0) continue;
                PlayerPrefs.SetInt(Prefix + Heroes[i].Key + ".cards", i == Knight ? needed : Mathf.Min(Cards(i), needed - 1));
            }
            ColosseumSave.EnsureTrainingCoins(UpgradeCost(Knight));
        }

        internal static void CompleteShotLesson()
        {
            if (!NeedsShotLesson) return;
            PlayerPrefs.SetInt(LessonKey, AwaitingMatchClaim);
            PlayerPrefs.Save();
        }

        static void GrantWinRecruits()
        {
            int wins = ColosseumSave.MatchWins;
            bool recruited = false;
            foreach (int hero in CollectionOrder)
            {
                var recruit = Heroes[hero];
                if (recruit.WinsToRecruit == 0 || wins < recruit.WinsToRecruit || !recruit.Locked) continue;
                if (recruit.WinsToRecruit >= WindowRecruitWins) { AddWindowRecruit(hero); continue; }
                GrantCards(hero, 1);
                recruited = true;
            }
            if (recruited) SaveLineup(LineupHeroes());
        }

        // From the goblin on, a hero won by wins is not revealed on the big portal screen: it stays locked until the
        // lobby opens HEROES and unlocks it on its own card (ColosseumHud.ShowPendingRecruit), with no upgrade lesson.
        internal const int WindowRecruitWins = 5;
        const string WindowRecruitKey = "colosseum.recruit.window";

        static void AddWindowRecruit(int hero)
        {
            var keys = new List<string>(PlayerPrefs.GetString(WindowRecruitKey, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (keys.Contains(Heroes[hero].Key)) return;
            keys.Add(Heroes[hero].Key);
            PlayerPrefs.SetString(WindowRecruitKey, string.Join(",", keys));
        }

        internal static int NextWindowRecruit()
        {
            foreach (string key in PlayerPrefs.GetString(WindowRecruitKey, "").Split(','))
            {
                int hero = System.Array.FindIndex(Heroes, h => h.Key == key);
                if (hero >= 0 && Heroes[hero].Locked) return hero;
            }
            return -1;
        }

        internal static void CompleteWindowRecruit(int hero)
        {
            if (Heroes[hero].Locked) GrantCards(hero, 1, revealInCollection: true);
            var keys = new List<string>(PlayerPrefs.GetString(WindowRecruitKey, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            keys.Remove(Heroes[hero].Key);
            PlayerPrefs.SetString(WindowRecruitKey, string.Join(",", keys));
            SaveLineup(LineupHeroes());
            PlayerPrefs.Save();
        }

        // The caller persists this together with the claimed cards, coins and consumed reward.
        internal static void RecordTutorialMatchClaim(bool won)
        {
            if (won) GrantWinRecruits();
            if (FirstBattleLocked)
            {
                if (!won) return;
                PlayerPrefs.SetInt(FirstVictoryKey, AwaitingArcherBattle);
            }
            int step = PlayerPrefs.GetInt(LessonKey);
            if (step == ShotLesson || step == LegacyPracticeComplete || step == AwaitingMatchClaim)
                PlayerPrefs.SetInt(LessonKey, UpgradeAfterClaim);
        }

        public static int NextRecruitReveal()
        {
            foreach (int hero in CollectionOrder)
                if (PlayerPrefs.GetInt(Heroes[hero].RecruitmentKey) == PendingReveal) return hero;
            return -1;
        }

        public static void AcknowledgeRecruit(int hero)
        {
            if (hero < 0 || hero >= Heroes.Length || PlayerPrefs.GetInt(Heroes[hero].RecruitmentKey) != PendingReveal) return;
            PlayerPrefs.SetInt(Heroes[hero].RecruitmentKey, Collected);
            PlayerPrefs.Save();
        }

        /// <summary>Cards the next level costs; 0 at the top level.</summary>
        public static int CardsNeeded(int hero)
        {
            int level = Level(hero);
            return level >= MaxLevel ? 0 : CardsToLevel[level - 1];
        }

        /// <summary>Coins the next level costs; 0 at the top level.</summary>
        public static int UpgradeCost(int hero)
        {
            int level = Level(hero);
            return level >= MaxLevel ? 0 : 50 * level;
        }

        public static bool CanUpgrade(int hero)
        {
            return !HeroesTabLocked && !Heroes[hero].Locked && HasUpgradeResources(hero, Cards(hero), ColosseumSave.Coins);
        }

        internal static bool HasUpgradeResources(int hero, int copies, long coins)
        {
            int needed = CardsNeeded(hero);
            return needed > 0 && copies >= needed && coins >= UpgradeCost(hero);
        }

        internal static bool HasAvailableUpgrade()
        {
            for (int i = 0; i < Heroes.Length; i++)
                if (CanUpgrade(i)) return true;
            return false;
        }

        /// <summary>Spends the cards and coins and raises the hero one level. False when it cannot be paid for.</summary>
        public static bool Upgrade(int hero)
        {
            if (!CanUpgrade(hero)) return false;
            int cards = Cards(hero) - CardsNeeded(hero);
            if (!ColosseumSave.TryDebit(UpgradeCost(hero))) return false;
            PlayerPrefs.SetInt(Prefix + Heroes[hero].Key + ".cards", cards);
            PlayerPrefs.SetInt(Prefix + Heroes[hero].Key + ".level", Level(hero) + 1);
            if (PlayerPrefs.GetInt(LessonKey) == UpgradeAfterClaim) PlayerPrefs.DeleteKey(LessonKey);
            if (hero == PendingRecruitUpgradeLesson) PlayerPrefs.SetString(RecruitUpgradeKey, "done");
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Fill vacant slots with owned heroes. -1 is an empty socket when fewer than four are owned.</summary>
        public static int[] LineupHeroes()
        {
            string[] keys = PlayerPrefs.GetString(LineupKey, "").Split(',');
            var lineup = new int[LineupSize];
            var used = new bool[Heroes.Length];
            for (int slot = 0; slot < lineup.Length; slot++)
            {
                int hero = slot < keys.Length ? System.Array.FindIndex(Heroes, entry => entry.Key == keys[slot]) : -1;
                lineup[slot] = -1;
                if (hero < 0 || Heroes[hero].Locked || used[hero]) continue;
                lineup[slot] = hero;
                used[hero] = true;
            }

            int emptySlot = 0;
            foreach (int candidate in CollectionOrder)
            {
                if (used[candidate] || Heroes[candidate].Locked) continue;
                while (emptySlot < lineup.Length && lineup[emptySlot] >= 0) emptySlot++;
                if (emptySlot == lineup.Length) break;
                lineup[emptySlot++] = candidate;
            }
            return lineup;
        }

        public static int LineupHero(int slot) => LineupHeroes()[slot];

        public static bool IsEquipped(int hero) => hero >= 0 && System.Array.IndexOf(LineupHeroes(), hero) >= 0;

        static void SaveLineup(int[] lineup) =>
            PlayerPrefs.SetString(LineupKey, string.Join(",", System.Array.ConvertAll(lineup, index => index < 0 ? "" : Heroes[index].Key)));

        /// <summary>Places a reserve in the deck, or trades positions with its destination if already equipped.</summary>
        public static bool Equip(int hero, int slot)
        {
            if (HeroesTabLocked || hero < 0 || hero >= Heroes.Length || Heroes[hero].Locked || slot < 0 || slot >= LineupSize)
                return false;
            int[] lineup = LineupHeroes();
            int previous = System.Array.IndexOf(lineup, hero);
            if (previous == slot) return false;
            if (previous >= 0) lineup[previous] = lineup[slot];
            lineup[slot] = hero;
            SaveLineup(lineup);
            PlayerPrefs.Save();
            return true;
        }

        internal static string[] DrawCards(int count)
        {
            var pool = System.Array.FindAll(Heroes, h => !h.Locked);
            var draws = new string[count];
            for (int i = 0; i < draws.Length; i++) draws[i] = pool[Random.Range(0, pool.Length)].Key;
            return draws;
        }

        internal static int[] CountDraws(string[] keys)
        {
            var counts = new int[Heroes.Length];
            foreach (string key in keys)
            {
                int index = System.Array.FindIndex(Heroes, h => h.Key == key);
                if (index >= 0) counts[index]++;
            }
            return counts;
        }

        // A new hero uses the first card for recruitment. Only duplicates become upgrade material.
        internal static int CardsAfterGrant(int hero, int count)
        {
            int copies = Cards(hero);
            if (count <= 0) return copies;
            int duplicates = Heroes[hero].Locked ? count - 1 : count;
            return (int)System.Math.Min(int.MaxValue, (long)copies + duplicates);
        }

        internal static void GrantCards(int hero, int count, bool revealInCollection = false)
        {
            if (count <= 0) return;
            Hero drawn = Heroes[hero];
            int copies = CardsAfterGrant(hero, count);
            if (drawn.Locked)
                PlayerPrefs.SetInt(drawn.RecruitmentKey, revealInCollection ? Collected : PendingReveal);
            PlayerPrefs.SetInt(Prefix + drawn.Key + ".cards", copies);
        }

        // ------------------------------------------------------------------ league table

        public struct Standing
        {
            public string Name;
            public int Trophies, Avatar;
            public bool You;
        }

        internal static bool HasUnseenRankGain()
        {
            var seen = ColosseumSave.LastViewedRank;
            if (seen.Place < 1 || seen.League < 0) return false;
            int league = ColosseumSave.League;
            if (league != seen.League) return league > seen.League;
            int place = Standings().FindIndex(entry => entry.You) + 1;
            return place > 0 && place < seen.Place;
        }

        /// <summary>
        /// The local league: the player among the same rivals every time, spread across the trophy range of the
        /// player's current league, best first.
        /// </summary>
        public static List<Standing> Standings()
        {
            int league = ColosseumSave.League, floor = ColosseumSave.LeagueFloor, goal = ColosseumSave.LeagueGoal;
            int top = goal > 0 ? goal : floor + 400;
            int[] avatars = ColosseumSave.AvatarIds;
            var table = new List<Standing>();
            var dice = new System.Random(7919 * (league + 1));
            for (int i = 0; i < Rivals.Length; i++)
            {
                // Most rivals sit in the lower half of the league, a few are about to be promoted.
                double share = dice.NextDouble();
                int trophies = floor + (int)((top - floor) * share * share) / 5 * 5;
                table.Add(new Standing { Name = Rivals[i], Trophies = trophies, Avatar = avatars[dice.Next(avatars.Length)] });
            }
            table.Add(new Standing { Name = ColosseumSave.PlayerName, Trophies = ColosseumSave.Trophies, Avatar = ColosseumSave.PlayerAvatar, You = true });
            table.Sort((a, b) => a.Trophies != b.Trophies ? b.Trophies.CompareTo(a.Trophies) : a.You ? -1 : b.You ? 1 : string.CompareOrdinal(a.Name, b.Name));
            return table;
        }
    }
}
