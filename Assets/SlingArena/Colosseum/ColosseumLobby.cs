using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>Player progress kept between sessions: trophies, coins and which stage rewards were collected.</summary>
    public static class ColosseumSave
    {
        public const int WinsPerArena = 6, WinTrophies = 30, StageGoal = WinsPerArena * WinTrophies;
        public const int WinCoins = 40, LossCoins = 10, RewardCoins = 100;
        public static readonly int[] RewardAt = { 2 * WinTrophies, 4 * WinTrophies, StageGoal };
        static readonly string[] LeagueNames = { "BRONZE LEAGUE", "SILVER LEAGUE", "GOLD LEAGUE", "ROYAL LEAGUE" };
        // A new league with each new arena, so both come with the same win.
        static readonly int[] LeagueAt = { 0, StageGoal, 2 * StageGoal, 3 * StageGoal };
        public static readonly int[] LeagueTone = { 0xd08a52, 0xc3ccd8, 0xf7c23c, 0xb48cf2 };

        const string Prefix = "colosseum.";
        const string WinsKey = Prefix + "matchWins";
        const string CompletedMatchesKey = Prefix + "completedMatches";
        const string JourneyKey = Prefix + "journey.trophies";
        const string ClaimedKey = Prefix + "journey.claimed";
        const string LegacyRewardsKey = Prefix + "rewards";
        const string OpenChestKey = Prefix + "journey.openChest";
        const string ChestDrawsKey = Prefix + "journey.chestCards";
        const string ExtraChestKey = Prefix + "journey.chestTaken.";
        const string ExtraCountKey = Prefix + "journey.chestTakenCount";
        const string ViewedLeagueKey = Prefix + "rankings.seenLeague";
        const string ViewedPlaceKey = Prefix + "rankings.seenPlace";
        public const int PlayerNameLimit = 16;
        internal static readonly int[] AvatarIds = { 1, 5, 6, 3 }; // stable IDs: knight, axe warrior, orc and archer poses

#if UNITY_EDITOR
        public static void ResetEditorProgress()
        {
            // Out-of-order chest claims have one key per chest beyond the contiguous claimed prefix.
            int earned = EarnedChests(JourneyTrophies);
            int claimed = Mathf.Clamp(PlayerPrefs.GetInt(ClaimedKey), 0, earned);
            int outstanding = PlayerPrefs.GetInt(ExtraCountKey);
            for (int number = claimed + 1; number <= earned && outstanding > 0; number++)
            {
                string key = ExtraChestKey + number;
                if (!PlayerPrefs.HasKey(key)) continue;
                PlayerPrefs.DeleteKey(key);
                outstanding--;
            }

            foreach (string key in new[]
            {
                Prefix + "name", Prefix + "avatar", Prefix + "trophies", Prefix + "coins", WinsKey, CompletedMatchesKey,
                JourneyKey, ClaimedKey, LegacyRewardsKey, OpenChestKey, ChestDrawsKey, ExtraCountKey,
                ViewedLeagueKey, ViewedPlaceKey
            })
                PlayerPrefs.DeleteKey(key);

            ColosseumRoster.ResetEditorProgress();
            ColosseumMatchRewards.ResetEditorProgress();
            ColosseumEvents.ResetEditorProgress();
            PlayerPrefs.Save();
        }
#endif

        public static int Trophies
        {
            get { return PlayerPrefs.GetInt(Prefix + "trophies", 0); }
            private set { PlayerPrefs.SetInt(Prefix + "trophies", Mathf.Max(0, value)); }
        }

        public static int Coins
        {
            get { return PlayerPrefs.GetInt(Prefix + "coins", 0); }
            private set { PlayerPrefs.SetInt(Prefix + "coins", Mathf.Max(0, value)); }
        }

        /// <summary>Pays for something with coins; false (and nothing spent) when the purse is short.</summary>
        public static bool Spend(int coins)
        {
            if (!TryDebit(coins)) return false;
            PlayerPrefs.Save();
            return true;
        }

        // Callers updating multiple balances persist the debit together with the other changes.
        internal static bool TryDebit(int coins)
        {
            if (coins < 0 || Coins < coins) return false;
            Coins -= coins;
            return true;
        }

        internal static void EnsureTrainingCoins(int cost)
        {
            if (Coins < cost) Coins = cost;
        }

        public static string PlayerName
        {
            get
            {
                string name = PlayerPrefs.GetString(Prefix + "name", string.Empty);
                if (string.IsNullOrEmpty(name))
                {
                    name = "Player" + Random.Range(100, 1000);
                    PlayerPrefs.SetString(Prefix + "name", name);
                }
                return name;
            }
        }

        public static int PlayerAvatar
        {
            get
            {
                int avatar = PlayerPrefs.GetInt(Prefix + "avatar", AvatarIds[0]);
                return AvatarIndex(avatar) >= 0 ? avatar : AvatarIds[0];
            }
        }

        internal static int AvatarIndex(int avatar) { return System.Array.IndexOf(AvatarIds, avatar); }

        public static bool IsValidPlayerName(string name)
        {
            string trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > PlayerNameLimit) return false;
            foreach (char character in trimmed)
                if (char.IsControl(character)) return false;
            return true;
        }

        public static bool SaveProfile(string name, int avatar)
        {
            if (!IsValidPlayerName(name) || AvatarIndex(avatar) < 0) return false;
            PlayerPrefs.SetString(Prefix + "name", name.Trim());
            PlayerPrefs.SetInt(Prefix + "avatar", avatar);
            PlayerPrefs.Save();
            return true;
        }

        public static int League
        {
            get
            {
                int league = 0;
                for (int i = 1; i < LeagueAt.Length; i++)
                {
                    if (Trophies >= LeagueAt[i]) league = i;
                }
                return league;
            }
        }

        public static string LeagueName { get { return LeagueNames[League]; } }

        internal static (int League, int Place) LastViewedRank =>
            (PlayerPrefs.GetInt(ViewedLeagueKey, -1), PlayerPrefs.GetInt(ViewedPlaceKey, 0));

        internal static void RememberViewedRank(int league, int place)
        {
            if (place < 1 || LastViewedRank == (league, place)) return;
            PlayerPrefs.SetInt(ViewedLeagueKey, league);
            PlayerPrefs.SetInt(ViewedPlaceKey, place);
            PlayerPrefs.Save();
        }

        /// <summary>Trophies still missing for the next league, or 0 in the top league.</summary>
        public static int ToNextLeague
        {
            get { return League + 1 < LeagueAt.Length ? LeagueAt[League + 1] - Trophies : 0; }
        }

        /// <summary>Trophy mark where the current league began.</summary>
        public static int LeagueFloor { get { return LeagueAt[League]; } }

        /// <summary>Trophy mark of the next league, or -1 in the top league.</summary>
        public static int LeagueGoal { get { return League + 1 < LeagueAt.Length ? LeagueAt[League + 1] : -1; } }

        // Older saves may have different league and journey totals. Keep their progress independent.
        static int SavedJourneyTrophies
        {
            get
            {
                if (PlayerPrefs.HasKey(JourneyKey)) return Mathf.Max(0, PlayerPrefs.GetInt(JourneyKey));
                int trophies = Mathf.Max(0, Trophies);
                int legacyMask = PlayerPrefs.GetInt(LegacyRewardsKey, 0);
                for (int i = 0; i < RewardAt.Length; i++)
                    if ((legacyMask & (1 << i)) != 0) trophies = Mathf.Max(trophies, RewardAt[i]);
                return trophies;
            }
        }

        public static int JourneyTrophies => Mathf.Max(SavedJourneyTrophies,
            AddCapped(0, (long)Mathf.Max(0, PlayerPrefs.GetInt(WinsKey)) * WinTrophies));

        public static int StageFor(int journeyTrophies) => 1 + Mathf.Max(0, journeyTrophies) / StageGoal;
        public static int StageProgressFor(int journeyTrophies) => Mathf.Max(0, journeyTrophies) % StageGoal;

        // Older saves may record a win only in the trophy journey.
        internal static int MatchWins => Mathf.Max(PlayerPrefs.GetInt(WinsKey), SavedJourneyTrophies / WinTrophies);
        internal static int CompletedMatches => Mathf.Max(PlayerPrefs.GetInt(CompletedMatchesKey), MatchWins);

        public static bool RewardTaken(int stageNumber, int index)
        {
            if (stageNumber < 1 || stageNumber > StageFor(int.MaxValue) || index < 0 || index >= RewardAt.Length) return false;
            return ChestTaken(ChestNumber(stageNumber, index));
        }

        public static bool RewardTaken(int index) => RewardTaken(StageFor(JourneyTrophies), index);

        /// <summary>Books a finished match against the AI and returns the trophy change.</summary>
        public static int RecordMatch(bool won, bool save = true)
        {
            int before = Trophies;
            PlayerPrefs.SetInt(CompletedMatchesKey, AddCapped(CompletedMatches, 1));
            if (won)
            {
                int journey = JourneyTrophies;
                PlayerPrefs.SetInt(WinsKey, AddCapped(MatchWins, 1));
                Trophies = AddCapped(before, WinTrophies);
                PlayerPrefs.SetInt(JourneyKey, AddCapped(journey, WinTrophies));
            }
            Coins = AddCapped(Coins, won ? WinCoins : LossCoins);
            if (save) PlayerPrefs.Save();
            return Trophies - before;
        }

        static int AddCapped(int balance, long increase) => (int)System.Math.Min(int.MaxValue, balance + increase);

        internal static int ChestNumber(int stage, int index) => (stage - 1) * RewardAt.Length + index + 1;

        static bool LegacyChestTaken(int number) => number >= 1 && number <= RewardAt.Length
            && (PlayerPrefs.GetInt(LegacyRewardsKey) & (1 << (number - 1))) != 0;

        static bool ChestTaken(int number) => number <= PlayerPrefs.GetInt(ClaimedKey) || LegacyChestTaken(number)
            || PlayerPrefs.HasKey(ExtraChestKey + number);

        static int EarnedChests(int journey)
        {
            int earned = (StageFor(journey) - 1) * RewardAt.Length;
            int progress = StageProgressFor(journey);
            foreach (int mark in RewardAt)
                if (progress >= mark) earned++;

            return earned;
        }

        internal static int AvailableChests(int journey)
        {
            int earned = EarnedChests(journey);
            int through = Mathf.Clamp(PlayerPrefs.GetInt(ClaimedKey), 0, earned);
            int remaining = earned - through - PlayerPrefs.GetInt(ExtraCountKey);
            for (int i = through + 1; i <= Mathf.Min(earned, RewardAt.Length); i++)
                if (LegacyChestTaken(i)) remaining--;
            return Mathf.Max(0, remaining);
        }

        internal static int FirstAvailableChest()
        {
            int earned = EarnedChests(JourneyTrophies);
            int through = Mathf.Clamp(PlayerPrefs.GetInt(ClaimedKey), 0, earned);
            for (int i = through + 1; i <= earned; i++)
                if (!ChestTaken(i)) return i;
            return 0;
        }

        internal static int PendingChest => PlayerPrefs.GetInt(OpenChestKey);
        internal static bool AnyChestCollected => PlayerPrefs.GetInt(ClaimedKey) > 0 || PlayerPrefs.GetInt(ExtraCountKey) > 0
            || PlayerPrefs.GetInt(LegacyRewardsKey) != 0;
        internal static int ChestCoins => Mathf.Min(RewardCoins, int.MaxValue - Coins);

        internal static bool OpenJourneyChest(int number, out int[] cards)
        {
            cards = null;
            if (number < 1 || number > EarnedChests(JourneyTrophies) || ChestTaken(number)) return false;
            if (PendingChest == 0)
            {
                PlayerPrefs.SetString(ChestDrawsKey, string.Join(",", ColosseumRoster.DrawCards(3)));
                PlayerPrefs.SetInt(OpenChestKey, number);
                PlayerPrefs.Save();
            }
            if (PendingChest != number) return false;
            cards = ColosseumRoster.CountDraws(PlayerPrefs.GetString(ChestDrawsKey).Split(','));
            return true;
        }

        internal static bool CollectJourneyChest(int number, out int coins)
        {
            coins = 0;
            if (number <= 0 || PendingChest != number || ChestTaken(number)) return false;
            bool opensHeroes = ColosseumRoster.HeroesTabLocked;
            coins = ChestCoins;
            Coins += coins;
            var cards = ColosseumRoster.CountDraws(PlayerPrefs.GetString(ChestDrawsKey).Split(','));
            for (int i = 0; i < cards.Length; i++) ColosseumRoster.GrantCards(i, cards[i]);
            if (opensHeroes) ColosseumRoster.PrepareFirstUpgrade();

            // A player can tap a later marker first. Keep only those exceptions above the paid prefix.
            int through = Mathf.Max(0, PlayerPrefs.GetInt(ClaimedKey));
            int extras = PlayerPrefs.GetInt(ExtraCountKey) + 1;
            PlayerPrefs.SetInt(ExtraChestKey + number, 1);
            int earned = EarnedChests(JourneyTrophies);
            while (through < earned && ChestTaken(through + 1))
            {
                through++;
                string key = ExtraChestKey + through;
                if (!PlayerPrefs.HasKey(key)) continue;
                PlayerPrefs.DeleteKey(key);
                extras--;
            }
            PlayerPrefs.SetInt(ClaimedKey, through);
            PlayerPrefs.SetInt(ExtraCountKey, extras);
            PlayerPrefs.DeleteKey(OpenChestKey);
            PlayerPrefs.DeleteKey(ChestDrawsKey);
            PlayerPrefs.Save();
            return true;
        }
    }

    /// <summary>
    /// The painted backdrop fills the camera while the lobby is open. The model showcase adds a palace and podium.
    /// </summary>
    public sealed class ColosseumLobby
    {
        const float Pitch = 15.5f, UnitsPerPixel = .0099f, RaisePixels = -78f, Top = 2.9f;
        public static readonly Vector3 Home = new Vector3(300f, 0f, 0f);
        public static readonly Color32 SkyTop = new Color32(104, 76, 214, 255), SkyMiddle = new Color32(76, 112, 230, 255), SkyBottom = new Color32(40, 72, 172, 255);
        /// <summary>Where the four heroes stand, relative to <see cref="Home"/>.</summary>
        public static readonly Vector3[] HeroSpots =
        {
            new Vector3(-2.25f, 0f, -.28f), new Vector3(-.82f, 0f, -.94f), new Vector3(.82f, 0f, -.94f), new Vector3(2.25f, 0f, -.28f)
        };

        readonly GameObject root;
        readonly Transform[] flags;
        readonly Transform[] twinkles;
        readonly Quaternion view = Quaternion.Euler(Pitch, 0f, 0f);
        float clock;

        public Transform Root { get { return root.transform; } }

        /// <summary>Close-up framing for looking the heroes over: magnification, sideways shift and height of the view.</summary>
        public float Zoom = 1f, Pan, Lift;

        public ColosseumLobby(Transform parent, bool showModelShowcase = true)
        {
            root = new GameObject("Colosseum lobby");
            root.transform.SetParent(parent, false);
            root.transform.position = Home;

            Material far = Kit.StrokeMaterial(Color.white);
            far.renderQueue = 2800;
            Transform backdrop = Kit.SpawnStroke("Lobby backdrop", root.transform, BackdropMesh(showModelShowcase), far).transform;
            backdrop.SetPositionAndRotation(Home + view * Vector3.forward * 30f, view * Quaternion.Euler(-90f, 0f, 0f));

            Material stampInk = Kit.StrokeMaterial(Color.white);
            stampInk.renderQueue = 2801;
            Transform stamps = Kit.SpawnStroke("Scrolling lobby tiles", backdrop, BackdropStamps(showModelShowcase), stampInk).transform;
            stamps.gameObject.AddComponent<UITileComponent>().ScrollRepeatingGeometry(Vector3.right * (2.1f * 6f),
                Vector3.forward * (1.7f * 6f), ColosseumBackdropTiles.TravelPerSecond * UnitsPerPixel);
            if (showModelShowcase)
            {
                Material scenery = Kit.StrokeMaterial(Color.white);
                scenery.renderQueue = 2802;
                Kit.SpawnStroke("Lobby palace", backdrop, ShowcaseScenery(), scenery);
                Material under = Kit.StrokeMaterial(Color.white), stage = Kit.StrokeMaterial(Color.white);
                under.renderQueue = 2850;
                stage.renderQueue = 2900;
                var blur = new MeshKit { Gradient = 0f };
                blur.Disc(Vector3.zero, 4.2f, 4.2f, new Color32(18, 22, 92, 180), new Color32(18, 22, 92, 0), 40);
                Kit.SpawnStroke("Lobby podium shadow", root.transform, blur.ToMesh("Colosseum lobby shadow"), under).transform.localPosition = new Vector3(0f, -.9f, 0f);
                Kit.SpawnStroke("Lobby podium", root.transform, PodiumMesh(), stage);
                flags = new Transform[2];
                twinkles = new Transform[6];
                BuildMovingParts(backdrop);
            }
            root.SetActive(false);
        }

        public void Show(bool visible) { root.SetActive(visible); }

        /// <summary>Frames the backdrop and animates the showcase when it is present.</summary>
        public void Frame(Camera camera, float delta)
        {
            float tall = Mathf.Max(1280f, 720f / Mathf.Max(.2f, camera.aspect));
            camera.orthographicSize = tall * UnitsPerPixel * .5f / Mathf.Max(.1f, Zoom);
            Transform eye = camera.transform;
            eye.rotation = view;
            // On longer phones the extra room is shared above and below the backdrop's focus.
            float raise = Zoom > 1f ? 0f : RaisePixels - (tall - 1280f) * .22f;
            eye.position = Home + new Vector3(Pan, Lift, 0f) - view * Vector3.up * (raise * UnitsPerPixel) - view * Vector3.forward * 50f;

            if (flags == null) return;
            clock += delta;
            for (int i = 0; i < flags.Length; i++)
                flags[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(clock * 3.2f + i * 1.4f) * 9f, 0f);
            for (int i = 0; i < twinkles.Length; i++)
            {
                float pulse = Mathf.Max(0f, Mathf.Sin(clock * (1.1f + i * .17f) + i * 2.1f));
                twinkles[i].localScale = Vector3.one * (.25f + pulse * pulse * .55f);
                twinkles[i].localRotation = Quaternion.Euler(0f, clock * 20f + i * 31f, 0f);
            }
        }

        static Color32 Rgba(int r, int g, int b, int a = 255) { return new Color32((byte)r, (byte)g, (byte)b, (byte)a); }

        /// <summary>Point on the backdrop board: x to the right, y up the screen, both in world units from the podium centre.</summary>
        static Vector3 P(float x, float y) { return new Vector3(x, 0f, y); }

        static void Rect(MeshKit kit, float x0, float y0, float x1, float y1, Color32 low, Color32 high)
        {
            kit.Quad(P(x0, y0), P(x0, y1), P(x1, y1), P(x1, y0), low, high, high, low);
        }

        static void PaintJourneySky(MeshKit kit)
        {
            Color32 turquoise = Rgba(107, 183, 201), horizon = Rgba(203, 234, 225);
            Color32 skyBlue = Rgba(95, 177, 219), sunlight = Rgba(255, 229, 176);

            Color32 SkyAt(float x, float y)
            {
                Color32 sky = Color32.Lerp(turquoise, horizon, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 1.5f, y)));
                sky = Color32.Lerp(sky, skyBlue, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 9.5f, y)));
                // The arena sits around y=3.6 in a 16:9 portrait and y=5.6 on a tall phone.
                float dx = x / 5.4f, dy = (y - 4.6f) / 3.4f;
                return Color32.Lerp(sky, sunlight, .86f * Mathf.Exp(-dx * dx - dy * dy));
            }

            // Sample closely across the visible board, then extend the outer colours for wide camera framing.
            float[] xs = { -40f, -12f, -8f, -6f, -4f, -2f, 0f, 2f, 4f, 6f, 8f, 12f, 40f };
            float[] ys = { -30f, -7f, -5f, -3f, -1f, 1f, 2.5f, 4f, 5.5f, 7f, 8.5f, 10f, 13f, 30f };
            for (int row = 1; row < ys.Length; row++)
            {
                float y0 = ys[row - 1], y1 = ys[row];
                for (int col = 1; col < xs.Length; col++)
                {
                    float x0 = xs[col - 1], x1 = xs[col];
                    kit.Quad(P(x0, y0), P(x0, y1), P(x1, y1), P(x1, y0),
                        SkyAt(x0, y0), SkyAt(x0, y1), SkyAt(x1, y1), SkyAt(x1, y0));
                }
            }
            // Taper the sunbeams into the arena entrance and fade them out behind the road.
            var light = Rgba(255, 239, 194, 45);
            var clear = Rgba(255, 245, 211, 0);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 2.9f + .6f;
                kit.Quad(P(x * .06f - .08f, 5.4f), P(x * .06f + .08f, 5.4f),
                    P(x + .69f, -8f), P(x - .69f, -8f), light, light, clear, clear);
            }
        }

        /// <summary>Violet to deep blue with faint icons and optional showcase scenery.</summary>
        static Mesh BackdropMesh(bool showModelShowcase)
        {
            var kit = new MeshKit { Gradient = 0f };
            if (showModelShowcase)
            {
                const float wide = 40f, high = 30f;
                Rect(kit, -wide, -high, wide, -2.2f, SkyBottom, SkyBottom);
                Rect(kit, -wide, -2.2f, wide, 2.6f, SkyBottom, SkyMiddle);
                Rect(kit, -wide, 2.6f, wide, 8.6f, SkyMiddle, SkyTop);
                Rect(kit, -wide, 8.6f, wide, high, SkyTop, SkyTop);
            }
            else PaintJourneySky(kit);
            return kit.ToMesh("Colosseum lobby backdrop");
        }

        static Mesh BackdropStamps(bool showModelShowcase)
        {
            var kit = new MeshKit { Gradient = 0f };
            Color32 tint = showModelShowcase ? Rgba(255, 255, 255, 12) : Rgba(255, 250, 221, 26);
            for (int row = -13; row <= 13; row++)
            {
                for (int column = -12; column <= 12; column++)
                {
                    bool odd = (row & 1) != 0;
                    var centre = new Vector3(column * 2.1f + (odd ? 1.05f : 0f), 0f, row * 1.7f);
                    Quaternion turn = Quaternion.Euler(0f, ((row + column) & 1) == 0 ? 16f : -16f, 0f);
                    if (((row + column * 2) % 3 + 3) % 3 == 0) Shield(kit, centre, turn, .42f, tint);
                    else Star(kit, centre, turn, .4f, tint);
                }
            }

            return kit.ToMesh("Colosseum lobby tiles");
        }

        static Mesh ShowcaseScenery()
        {
            var kit = new MeshKit { Gradient = 0f };
            // Light falling from above the stage.
            for (int i = 0; i < 5; i++)
            {
                float lean = (i - 2) * 1.5f, spread = .5f + (i & 1) * .22f;
                Color32 bright = Rgba(214, 236, 255, 26), none = Rgba(214, 236, 255, 0);
                kit.Quad(P(lean * .25f - spread * .3f, 9.5f), P(lean * .25f + spread * .3f, 9.5f), P(lean + spread, .4f), P(lean - spread, .4f), none, none, bright, bright);
            }

            Palace(kit);

            // Pool of light behind the heroes; it also washes the palace out so it reads as far away.
            Color32 glow = Rgba(196, 228, 255, 226), glowEdge = Rgba(150, 196, 255, 0);
            kit.Disc(new Vector3(0f, 0f, 1.5f), 4.9f, 4.3f, glow, glowEdge, 48);
            kit.Disc(new Vector3(0f, 0f, 1.2f), 2.5f, 2.1f, Rgba(236, 248, 255, 150), Rgba(236, 248, 255, 0), 40);
            return kit.ToMesh("Colosseum lobby palace");
        }

        /// <summary>Flat far-away palace: curtain wall, gatehouse and two team towers, in colours close to the sky.</summary>
        static void Palace(MeshKit kit)
        {
            Color32 wallLow = Rgba(96, 124, 228), wallHigh = Rgba(126, 152, 242), edge = Rgba(164, 188, 252), dim = Rgba(84, 108, 214);
            // Slim spires at the very back.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.42f;
                Rect(kit, x - .2f, .2f, x + .2f, 2.35f, Rgba(104, 132, 232), Rgba(120, 148, 240));
                kit.Quad(P(x - .28f, 2.35f), P(x, 2.95f), P(x, 2.95f), P(x + .28f, 2.35f), Rgba(126, 154, 244), Rgba(150, 176, 250), Rgba(150, 176, 250), Rgba(126, 154, 244));
            }
            // Curtain wall with merlons.
            Rect(kit, -3.9f, -.6f, 3.9f, 1.98f, wallLow, wallHigh);
            Rect(kit, -3.9f, 1.92f, 3.9f, 1.98f, edge, edge);
            for (int i = -9; i <= 9; i++)
            {
                float x = i * .42f;
                Rect(kit, x - .13f, 1.98f, x + .13f, 2.15f, wallHigh, edge);
            }
            // Team towers with cone roofs.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.62f;
                Rect(kit, x - .52f, -.6f, x + .52f, 2.5f, wallLow, wallHigh);
                Rect(kit, x - .52f, -.6f, x - .30f, 2.5f, wallLow, edge);
                Rect(kit, x - .62f, 2.5f, x + .62f, 2.66f, wallHigh, edge);
                Rect(kit, x - .1f, 1.7f, x + .1f, 2.1f, dim, dim);
                Color32 roofLow = side < 0 ? Rgba(98, 140, 246) : Rgba(196, 136, 214), roofHigh = side < 0 ? Rgba(140, 180, 255) : Rgba(232, 172, 232);
                kit.Quad(P(x - .68f, 2.66f), P(x, 3.5f), P(x, 3.5f), P(x + .68f, 2.66f), roofLow, roofHigh, roofHigh, roofLow);
                Rect(kit, x - .018f, 3.5f, x + .018f, 3.86f, edge, edge);
            }
            // Gatehouse with its arch.
            Rect(kit, -.98f, -.6f, .98f, 2.42f, wallLow, wallHigh);
            Rect(kit, -.98f, 2.36f, .98f, 2.42f, edge, edge);
            for (int i = -1; i <= 1; i++)
                Rect(kit, i * .66f - .2f, 2.42f, i * .66f + .2f, 2.6f, wallHigh, edge);
            Rect(kit, -.44f, -.6f, .44f, 1.5f, dim, dim);
            for (int i = 0; i < 12; i++)
            {
                float a0 = i / 12f * Mathf.PI, a1 = (i + 1) / 12f * Mathf.PI;
                kit.Quad(P(0f, 1.5f), P(Mathf.Cos(a0) * .44f, 1.5f + Mathf.Sin(a0) * .44f), P(Mathf.Cos(a1) * .44f, 1.5f + Mathf.Sin(a1) * .44f), P(0f, 1.5f), dim, dim, dim, dim);
            }
        }

        static void Star(MeshKit kit, Vector3 centre, Quaternion turn, float size, Color32 color)
        {
            for (int i = 0; i < 10; i++)
            {
                float a0 = i * Mathf.PI / 5f, a1 = (i + 1) * Mathf.PI / 5f, r0 = (i & 1) == 0 ? size : size * .46f, r1 = (i & 1) == 0 ? size * .46f : size;
                kit.Tri(centre, centre + turn * new Vector3(Mathf.Sin(a0) * r0, 0f, Mathf.Cos(a0) * r0), centre + turn * new Vector3(Mathf.Sin(a1) * r1, 0f, Mathf.Cos(a1) * r1), color);
            }
        }

        static void Shield(MeshKit kit, Vector3 centre, Quaternion turn, float size, Color32 color)
        {
            Vector3[] rim =
            {
                new Vector3(-.8f, 0f, .9f), new Vector3(.8f, 0f, .9f), new Vector3(.8f, 0f, 0f), new Vector3(.56f, 0f, -.6f),
                new Vector3(0f, 0f, -1f), new Vector3(-.56f, 0f, -.6f), new Vector3(-.8f, 0f, 0f)
            };
            for (int i = 0; i < rim.Length; i++)
                kit.Tri(centre, centre + turn * (rim[i] * size), centre + turn * (rim[(i + 1) % rim.Length] * size), color);
        }

        /// <summary>Ring between two circles; each end takes its colour from the angle so highlights can be painted in.</summary>
        static void Band(MeshKit kit, float r0, float y0, float r1, float y1, System.Func<float, Color32> c0, System.Func<float, Color32> c1,
            float from, float to, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.Lerp(from, to, (float)i / seg), a1 = Mathf.Lerp(from, to, (float)(i + 1) / seg);
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                kit.Quad(d0 * r0 + Vector3.up * y0, d1 * r0 + Vector3.up * y0, d1 * r1 + Vector3.up * y1, d0 * r1 + Vector3.up * y1,
                    c0(a0), c0(a1), c1(a1), c1(a0));
            }
        }

        static Color32 Blend(Color32 a, Color32 b, float t) { return Color32.Lerp(a, b, Mathf.Clamp01(t)); }

        /// <summary>How squarely a spot on the podium wall faces the camera: 1 in the middle, 0 at the left and right ends.</summary>
        static float Facing(float angle) { return Mathf.Clamp01(-Mathf.Sin(angle)); }

        /// <summary>Painted reflections on the round wall: a strong streak left of centre, a thin one beside it, a soft one on the right.</summary>
        static float Gloss(float angle)
        {
            float across = Mathf.Cos(angle);
            float a = (across + .46f) / .085f, b = (across + .27f) / .04f, c = (across - .58f) / .1f;
            return Mathf.Clamp01(Mathf.Exp(-a * a) + .65f * Mathf.Exp(-b * b) + .4f * Mathf.Exp(-c * c));
        }

        /// <summary>
        /// Two-tier round podium painted with its own light: sand top, a pale polished rim and a deep violet base, each
        /// wall shaded darker towards the sides with bright reflection streaks. Only the half facing the camera is built.
        /// </summary>
        static Mesh PodiumMesh()
        {
            var kit = new MeshKit { Gradient = 0f };
            // Whole rings and half rings share the same step, so their edges meet exactly and nothing shows through.
            const int seg = 192, half = seg / 2;
            const float full = Mathf.PI * 2f, front0 = Mathf.PI, front1 = Mathf.PI * 2f;
            float rim = Top + .07f, foot = Top + .22f;
            Color32 white = Rgba(255, 255, 255);

            // Base wall, a light line along its top edge, then the ledge between the tiers.
            Band(kit, foot + .05f, -.9f, foot, -.40f,
                a => Blend(Blend(Rgba(30, 28, 98), Rgba(58, 54, 150), Facing(a)), Rgba(150, 150, 255), Gloss(a) * .22f),
                a => Blend(Blend(Rgba(52, 48, 138), Rgba(98, 92, 210), Facing(a)), Rgba(190, 190, 255), Gloss(a) * .5f), front0, front1, half);
            Band(kit, foot, -.40f, foot, -.345f,
                a => Blend(Rgba(98, 94, 206), Rgba(170, 168, 255), Facing(a)), a => Blend(Rgba(110, 106, 218), Rgba(190, 188, 255), Facing(a)), front0, front1, half);
            Band(kit, foot, -.345f, rim, -.345f, a => Rgba(120, 116, 222), a => Rgba(86, 82, 186), 0f, full, seg);

            // Polished rim wall and the bright bevel that catches the light around the sand.
            Band(kit, rim, -.345f, rim, -.05f,
                a => Blend(Blend(Rgba(112, 112, 200), Rgba(190, 192, 244), Facing(a)), white, Gloss(a) * .55f),
                a => Blend(Blend(Rgba(150, 152, 226), Rgba(238, 238, 255), Facing(a)), white, Gloss(a) * .95f), front0, front1, half);
            Band(kit, rim, -.05f, Top, 0f, a => Rgba(236, 236, 255), a => Rgba(255, 250, 232), 0f, full, seg);

            // Sand: bright in the middle, a touch deeper towards the rim.
            Color32 sandMiddle = Rgba(252, 233, 182), sandMid = Rgba(246, 220, 160), sandOuter = Rgba(238, 206, 142), sandEdge = Rgba(222, 186, 122);
            kit.Disc(Vector3.zero, Top * .5f, Top * .5f, sandMiddle, sandMid, seg);
            Band(kit, Top * .5f, 0f, Top * .88f, 0f, a => sandMid, a => sandOuter, 0f, full, seg);
            Band(kit, Top * .88f, 0f, Top, 0f, a => sandOuter, a => sandEdge, 0f, full, seg);
            return kit.ToMesh("Colosseum lobby podium");
        }

        /// <summary>Pennants on the tower tips and a few slow sparkles in the light.</summary>
        void BuildMovingParts(Transform backdrop)
        {
            Material soft = Kit.StrokeMaterial(Color.white);
            soft.renderQueue = 2820;
            for (int i = 0; i < flags.Length; i++)
            {
                bool left = i == 0;
                Color32 cloth = left ? Rgba(150, 188, 255, 235) : Rgba(226, 176, 238, 235);
                var kit = new MeshKit { Gradient = 0f };
                kit.Tri(P(0f, 0f), P(left ? -.44f : .44f, -.12f), P(0f, -.24f), cloth);
                flags[i] = Kit.SpawnStroke("Lobby flag", backdrop, kit.ToMesh("Colosseum lobby flag"), soft).transform;
                flags[i].localPosition = P(left ? -2.62f : 2.62f, 3.86f) + Vector3.up * .02f;
            }
            var spark = new MeshKit { Gradient = 0f };
            Color32 core = Rgba(255, 255, 255, 230), tip = Rgba(255, 255, 255, 0);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f, b = a + Mathf.PI * .25f, c = a - Mathf.PI * .25f;
                spark.Quad(Vector3.zero, P(Mathf.Cos(c) * .05f, Mathf.Sin(c) * .05f), P(Mathf.Cos(a) * .26f, Mathf.Sin(a) * .26f), P(Mathf.Cos(b) * .05f, Mathf.Sin(b) * .05f), core, core, tip, core);
            }
            Mesh twinkle = spark.ToMesh("Colosseum lobby sparkle");
            Vector2[] spots =
            {
                new Vector2(-3.2f, 2.9f), new Vector2(-1.5f, 2.5f), new Vector2(-.1f, 2.25f), new Vector2(1.55f, 2.4f),
                new Vector2(3.25f, 2.75f), new Vector2(-3.3f, 2.1f)
            };
            for (int i = 0; i < twinkles.Length; i++)
            {
                twinkles[i] = Kit.SpawnStroke("Lobby sparkle", backdrop, twinkle, soft).transform;
                twinkles[i].localPosition = P(spots[i].x, spots[i].y) + Vector3.up * .03f;
            }
        }
    }
}
