using System.Collections.Generic;
using GameAnalyticsSDK;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Voodoo.Tiny.Sauce.Internal.Analytics;
using Voodoo.Tiny.Sauce.Privacy;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Colosseum PvP: a 4 v 4 turn-based sling brawl. Hold a hero, pull back and let go; the whole body is flung,
    /// everything collides with everything, and heroes stay wherever they end up.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]   // LateUpdate after the dolls have posed, so the plates can follow them exactly
    public sealed partial class ColosseumGame : MonoBehaviour
    {
        public const float HalfWidth = 5.3f;
        public const float HalfDepth = 11f;
        const float BattleReferenceAspect = 402f / 826f;
        internal const int UnitsPerSide = ColosseumRoster.LineupSize;
        // Each side gets this many regular moves before remaining team HP decides the match.
        internal const int RoundLimit = 15;
        static readonly Color PowerGold = new Color(1f, .84f, .2f);
        const float MaximumPull = 2.6f;
        const float MinimumPull = .4f;
        const float MinimumSpeed = 8f;
        const float MaximumSpeed = 27f;
        internal static float SlingSpeed(float power) => Mathf.Lerp(MinimumSpeed, MaximumSpeed, power);
        const float ArrowLead = .8f, ArrowExtraSpeed = 11f;
        public const float CameraPitch = 52f;
        static readonly Color DamageRed = new Color(1f, .25f, .2f);

        /// <summary>The orc wrestler's leap: how far he can reach and how wide the landing hits.</summary>
        const float LeapNear = 2.2f, LeapFar = 14f, SlamRadius = 2.3f;
        const float SlamInnerRadius = SlamRadius * .5f, SlamInnerDamageMultiplier = 1.3f;
        static readonly string[] HitWord = { "CLEAVE", "STAB", "SMASH", "BONK", "SLAM", "BUMP", "POUNCE", "HOOK", "SLICE", "STAR" };
        // The dawn witch is not slung at all: she stands where she is and shoots StarCount stars straight up, one after
        // another, to come down out of the sky on the ground she was aimed at (the pull sets how far off that is, from
        // StarNear to StarFar).
        // She cannot be made to aim well: each star comes down anywhere within StarScatter of the mark, and hurts
        // whoever is within StarBurst of where it lands. Every star she looses kicks her about StarKick back the
        // way it went, give or take a good deal, so she ends her turn somewhere she did not begin it. StarWind is
        // the moment she takes to wind up, StarBeat the time from one star to the next, StarFlight how long each
        // is in the air.
        const int StarCount = 6;
        const float StarNear = 3f, StarFar = 15.5f, StarScatter = 2.7f, StarBurst = 1.4f, StarKick = 1.45f;
        const float StarWind = .5f, StarBeat = .32f, StarFlight = 1.5f;
        // The gladiator is not slung along the ground: he rides his great boomerang out and round on a long
        // narrow loop. The pull sets how far out the turn is, from LoopNear to LoopFar; LoopTurn is the radius of
        // the turn. Out and round the turn it goes where it was aimed. Coming home it gets away from him: it
        // weaves from side to side (up to WildSwing degrees a second, WildBeat radians a second through each sway),
        // lurches off line every so often, rings off the walls, glances off whoever it hits, and all the while
        // tries to come round to where he set off (about WildHome degrees a second); after about as long as the way home
        // should have taken, it throws him off wherever it has got to. Whoever comes within LoopReach of him at
        // any point (before their own girth) is cut for his damage and thrown aside, and cut again every
        // SliceAgain seconds for as long as the blade is still on them. Where he comes down, everyone within
        // LoopLand is knocked back for LoopLandHurt of his damage.
        const float LoopNear = 4.5f, LoopFar = 16.5f, LoopTurn = 1.25f, LoopReach = 1.05f, LoopLift = .44f;
        const float LoopLand = 2.1f, LoopLandHurt = .45f, SliceAgain = .3f;
        const float WildSwing = 240f, WildBeat = 8.5f, WildHome = 110f;
        // The pirate is slung like the rest, great hook first. Enemies it meets are caught on the hook, up to
        // HookRoom of them, for a small share of his damage, and carried along with him. When he comes to a stop
        // he brings up his gun and, BlastWait later, fires into them: each takes the rest of the damage and is
        // blown off the hook down a lane of its own. HookSeat is how far apart they hang along the shaft.
        // The hook catches ahead of him: anyone between HookNear and HookFar in front of his middle and within
        // HookWide of his line, before the enemy's own girth. All of these are in the pirate's own measure.
        const int HookRoom = 3;
        // Every one aboard is a weight on him: HookDrag is how much speed each costs him a second.
        const float HookHurt = .2f, BlastHurt = .8f, HookSeat = .66f, BlastWait = .6f;
        const float HookNear = 1.5f, HookFar = 2.95f, HookWide = .42f, HookDrag = 2.4f;
        // The porcupine does not fly straight: it takes this many small hops over the first part of the way,
        // pricking whoever each one comes down beside for a share of its damage, and then one great leap onto the
        // spot it was aimed at, which hurts everyone inside the larger ring in full.
        const int BounceHops = 3;
        const float BounceShare = .6f, HopRadius = 1.15f, HopHurt = .3f, PounceRadius = 2.05f;
        // The viking is slung like the rest, and where he first runs into an enemy he pulls up and turns on the spot
        // with his axe out, until the air is turning with him. The one he ran into takes WhirlBump of the blow for
        // that. Every enemy within WhirlReach of him (in his own measure) is taken up by the whirlwind, the nearest
        // first, and carried round and in and up inside it. WhirlSpan after it began it throws them out of its top:
        // each takes WhirlHurt of his damage and goes up out of sight (SkyOver beyond the top of the view), to come
        // down again SkyTime later, one every SkyBeat, somewhere between SkyNear and SkyFar from him. One it has
        // finished goes up and does not come down. The viking is WhirlFade running down afterwards.
        const float WhirlReach = 3f, WhirlSpan = 1.2f, WhirlFade = .55f, WhirlBump = .5f, WhirlHurt = .5f;
        const float SkyTime = 1.5f, SkyBeat = .14f, SkyOver = 9f, SkyNear = 2.6f, SkyFar = 5f;
        /// <summary>
        /// What one of the goblin sapper's bombs does when it goes off (see ColosseumMines): to the hero carrying it,
        /// and as a share of that to each of the carrier's team-mates inside the ring. BombBeat is how long after
        /// the last bomb was stuck on the first of them starts to shake.
        /// </summary>
        const float BombAttackRatio = 1.375f, ArrowAttackRatio = .6f;
        const float BombSplash = .4f, BombBeat = .75f;

        enum Phase { Menu, Aim, Sim, Settle, MatchEnd, Matching, Quake, Decree, Event }
        enum MatchOutcome { Win, Loss, Abandoned }

        /// <summary>Only game-owned match data is retained until TinySauce receives its outcome.</summary>
        struct AnalyticsMatch
        {
            internal readonly int Stage;
            internal readonly bool Tutorial;

            internal AnalyticsMatch(int stage, bool tutorial)
            {
                Stage = stage;
                Tutorial = tutorial;
            }
        }

        struct Truce
        {
            public Collider A, B;
            public float Left;
        }

        [SerializeField] TMP_FontAsset outfitFont;
        [SerializeField] Material blackOutline;
        [SerializeField] Shader strokeShader;
        [Tooltip("Level of each rival hero in collection order. Unspecified levels default to 1.")]
        [SerializeField] int[] redLevels = { 1, 1, 1, 1, 1, 1, 1, 1 };
        readonly int[] matchHeroes = new int[UnitsPerSide * 2];
        readonly int[] matchLevels = new int[UnitsPerSide * 2];
        int playerTeamSize, rivalTeamSize;
        bool trainingMatch;
        // Until a new player has won six battles, a rival that is ahead (the player's heroes have less health between
        // them than its own) throws its shot away into open sand; otherwise it plays as usual.
        bool guidedMatch, guidedMissTurn;
        const float GuidedBehind = 1f;
        static readonly Color CritYellow = new Color(1f, .86f, .12f);

        public Transform World { get; private set; }
        internal Material DecalMaterial { get; private set; }

        readonly List<ColosseumDoll> dolls = new List<ColosseumDoll>();
        readonly List<Truce> truces = new List<Truce>();
        readonly Material[,] ringMaterials = new Material[2, 3];

        Camera arenaCamera;
        ColosseumArena arena;
        ColosseumFx fx;
        ColosseumBoost boost;
        ColosseumQuake quake;
        ColosseumMines mines;
        ColosseumEvents events;
        ColosseumHud hud;
        Sfx sfx;
        PhysicsMaterial dollSurface, floorSurface, wallSurface;
        Phase phase = Phase.Menu;
        bool againstAi = true, pulling, standing;
        AnalyticsMatch? analyticsMatch;
        static bool analyticsAllowedAtStartup;
        static bool CanTrackMatch => analyticsAllowedAtStartup && PrivacyManager.AnalyticsConsent;
        int turn, winner = -1;
        int turnsTaken;               // regular moves completed towards the time limit
        int nextSide;
        readonly List<string> acts = new List<string>();   // those events, in the order they are still to come; an empty name is a pause
        readonly List<string> decreeCandidates = new List<string>();
        string decreeKey;
        float actSpan;                // how long the one that is coming now is given before the next
        int striker;                  // the side whose blows count right now: the side to move, or the side whose mine has gone off
        float bombClock;              // how long since the goblin last stuck a bomb on anyone
        readonly HashSet<ColosseumDoll> bombed = new HashSet<ColosseumDoll>();   // enemies it has stuck one on this turn: one each, however often it runs into them
        Vector3 bounceFrom, bounceTo; // the porcupine's run: where it set off and where its last leap comes down
        int bounceStep;               // how many of its small hops it has started
        float bouncePower;
        readonly Vector3[] bounceSpots = new Vector3[BounceHops + 1];
        readonly HashSet<ColosseumDoll> pricked = new HashSet<ColosseumDoll>();   // enemies a hop of this attack has caught already
        readonly List<ColosseumDoll> skewered = new List<ColosseumDoll>();        // enemies strung on the pirate's hook, in the order caught: the first ends up nearest his hand
        readonly List<Vector3> slack = new List<Vector3>();                       // how far each of them still is from its place on the hook; it closes up quickly
        Vector3 hookRoot;             // the pirate's wrist this frame, where the hook begins
        ColosseumDoll hooker;         // the pirate whose hook is out this turn
        ColosseumDoll rider;          // the gladiator who is up on his boomerang this turn
        readonly List<Vector3> loop = new List<Vector3>();      // the way it carries him, point by point
        readonly List<float> loopMarks = new List<float>();     // how far along the loop each of those points is
        static readonly List<Vector3> aimLoop = new List<Vector3>();   // the loop the sling guide is showing
        static readonly List<Vector3> aimHint = new List<Vector3>();   // and its guess at the way home
        readonly Dictionary<ColosseumDoll, float> cutAt = new Dictionary<ColosseumDoll, float>();   // when in this flight the blade last cut each enemy
        Vector3 loopHome;             // the way he was facing as he set off
        float loopLength, loopWhole, loopSpan, loopTime, loopDust;   // loopWhole: how long the loop would be if it came all the way home
        ColosseumStars stars;
        ColosseumTwister twister;
        /// <summary>One the whirlwind has marked: waiting to be taken up, or already going round in it.</summary>
        sealed class Taken
        {
            public ColosseumDoll Doll;
            public float Wait, Age, Reach, Turn, Height;   // time until it is taken; time since; and where it was when it was
            public int Seat;                               // how many were taken before it: each rides a little higher and wider than the last
            public bool Up;
        }
        readonly List<Taken> taken = new List<Taken>();
        readonly List<Vector3> landings = new List<Vector3>();   // where those it has thrown out are coming down
        ColosseumDoll whirler;        // the viking in the middle of his whirlwind
        Vector3 whirlEye, whirlFrom;  // where it stands, and where he was when it began
        float whirlClock, whirlReach; // how long it has been turning, and how far it reaches
        float skyFall;                // counting down to the moment those it threw out start to come down
        float whirlDust;              // counting down to the next wisp of sand it lifts
        int whirlSeats;               // how many it has taken up
        bool whirlBurst, whirled;     // it has thrown them out and is dying away; he has had his whirlwind this turn
        Vector3 aimEye;               // where the sling guide shows it standing,
        bool aimEyeSet;               // if he is aimed at anyone at all
        ColosseumDoll caster;         // the witch in the middle of her barrage
        Vector3 castMark;             // the middle of the ground her stars are falling on
        Vector3 castFrom, castTo;     // the kick she is in the middle of: thrown from here to there
        float castClock, castHop, castPeak;   // time to the next star; how far through the kick she is (1: landed); how high it throws her
        float castGlint;              // time to the next twinkle off the end of her wand
        int castLeft;                 // stars still to come
        bool wild;                    // the boomerang has got away from him and is on its way home, after a fashion
        Vector3 wildAt, wildWay;      // where it is now, and which way it is heading
        float wildSpeed, wildTop, wildClock, wildSpan, wildPhase, wildJolt;
        Vector3 hookWay = Vector3.forward;   // the level line his catch hangs along: it follows the hook, but never whips round with it
        float blastLeft;              // counting down to his shot once he has pulled up; 0 while he is still on the move
        float phaseTime, excitement, cheerLeft, stopLeft, slowLeft, shake, arrowSoundAt;
        float zoomKick, zoomNow;   // the view drawn in a touch by a heavy blow of the arena's own, and let out again
        float aiClock, aiPower, shotClock;
        bool selfTest;
        int pullNotch;
        RenderTexture captureTarget;
        // Self-test "video": every frame is kept at a fixed 30 fps game clock, with the game's own sound alongside.
        bool video;
        // A recording steps the interface clock by its own frame: a thirtieth of a second, or less if the recorder keeps more frames.
        static float VideoStep => 1f / Mathf.Max(30, Time.captureFramerate);
        System.IO.StreamWriter soundLog;
        int shots, shotLimit = 200;
        string shotFolder;
        Vector3 cameraRest, holdPoint, aiDirection;
        Vector2 aimCancelCenter;
        float aimCancelRadius;
        float aimCancelEnteredAt = -1f;
        const float AimCancelHoldSeconds = .24f;
        bool aimCancelVisible;
        Rect battleOutputPixels, battleTouchSafeArea;
        float battleViewSize;
        ColosseumLobby lobby;
        readonly List<ColosseumDoll> lobbyHeroes = new List<ColosseumDoll>();
        // Matchmaking: a hidden row of heroes is photographed once for the cards on the versus screen.
        internal static readonly string[] RivalNames =
        {
            "IRONJAW", "PEBBLE", "SIR BONK", "MAXIMUS", "LADY VEX", "GRUMBLE", "TORO", "NOVA",
            "BRUTUS", "FENN", "OTTO", "RAGNA", "DUSTY", "KIKO", "VALOR", "MOSSY"
        };
        readonly List<ColosseumDoll> studio = new List<ColosseumDoll>();
        readonly RenderTexture[] portraits = new RenderTexture[ColosseumRoster.Heroes.Length * 2];
        Transform studioRoot;
        Camera portraitCamera;
        ColosseumIcons icons;
        bool closeUp, testLeaper;
        IEnumerator<KeyValuePair<string, Sprite>> iconWork;
        Vector3 towardsSun = Vector3.up;
        static readonly Color32 LockedTone = new Color32(210, 216, 255, 255);
        bool portraitsReady, scriptedShots = true;
        int studioFrames;
        float autoMatch = -1f;
        static bool launchTitleEntered;
        bool waitingForRewardArrival, showingArenaReward;
        string rivalName;
        ColosseumDoll selected, launched, aiDoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLaunchTitle() => launchTitleEntered = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MigrateSavedPrivacyChoice()
        {
            // Earlier Smash Rumble builds stored explicit choices under the same two consent keys.
            if (PlayerPrefs.HasKey("FirstStart") || PlayerPrefs.GetInt("PrivacyChoiceVersion") != 1)
                return;
            int ads = PlayerPrefs.GetInt("AdConsent", -1);
            int analytics = PlayerPrefs.GetInt("AnalyticsConsent", -1);
            if ((ads != 0 && ads != 1) || (analytics != 0 && analytics != 1)) return;
            PlayerPrefs.SetInt("FirstStart", 0);
            PlayerPrefs.Save();
        }

        void Awake()
        {
            ColosseumRoster.InitializeCollection();
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform)
                Screen.orientation = ScreenOrientation.Portrait;

            Kit.Init(strokeShader);
            World = new GameObject("Colosseum world").transform;
            World.SetParent(transform, false);
            arenaCamera = CreateCamera();
            ConfigureLighting();

            dollSurface = Surface("Colosseum doll", .62f);
            floorSurface = Surface("Colosseum sand", .5f);
            wallSurface = Surface("Colosseum wall", .86f);
            DecalMaterial = Kit.StrokeMaterial(Color.white);
            for (int team = 0; team < 2; team++)
            {
                ringMaterials[team, 0] = Kit.StrokeMaterial(new Color(.15f, .08f, .05f, .22f));
                Color light = MeshKit.Rgb(Kit.TeamLight[team], 1f);
                ringMaterials[team, 1] = Kit.StrokeMaterial(new Color(light.r, light.g, light.b, .95f));
                ringMaterials[team, 2] = Kit.StrokeMaterial(new Color(1f, .82f, .23f, 1f));
            }

#if UNITY_EDITOR
            shotFolder = System.IO.Path.GetFullPath(Application.dataPath + "/../Library/ClaudeBridge");
            string selfTestPath = System.IO.Path.Combine(shotFolder, "selftest");
            string plan = System.IO.File.Exists(selfTestPath) ? System.IO.File.ReadAllText(selfTestPath) : null;
            selfTest = plan != null;
            // The close-up review still uses the original model showcase.
            closeUp = plan != null && plan.Contains("close");
#endif
            var assets = Resources.Load<ColosseumAssets>("ColosseumAssets");
            Texture2D lobbyIllustration = assets != null ? assets.lobbyIllustration : null;
            bool showModelShowcase = closeUp || lobbyIllustration == null;
            arena = new ColosseumArena(World, HalfWidth, HalfDepth, floorSurface, wallSurface,
                ColosseumSave.StageFor(ColosseumSave.JourneyTrophies));
            lobby = new ColosseumLobby(World, showModelShowcase);
            PrepareTeams();
            if (showModelShowcase) SpawnLobbyHeroes();
            BuildStudio();
            icons = new ColosseumIcons(portraitCamera, studioRoot, towardsSun);
            iconWork = icons.Models(LockedTone).GetEnumerator();
            fx = new ColosseumFx(this);
            boost = new ColosseumBoost(this);
            quake = new ColosseumQuake(this, arena, HalfWidth, HalfDepth);
            quake.Crumbled += OnWallCrumbles;
            mines = new ColosseumMines(this);
            mines.Lit += OnBombShakes;
            mines.Fizzes += at => fx.Sparks(at, 1);
            mines.WentOff += OnBombGoesOff;
            stars = new ColosseumStars(this);
            twister = new ColosseumTwister(this);
            stars.Landed += OnStarLands;
            stars.Falls += point => sfx.Play("starfall", .7f);
            sfx = new Sfx(gameObject);
            hud = new ColosseumHud(transform, assets != null ? assets.brawlFont : null, outfitFont, blackOutline, lobbyIllustration);
            hud.ModeChosen += _ => BeginMatching();   // the lobby only offers matches against the computer
            hud.MatchSound += sound => sfx.Play(sound,
                phase == Phase.Decree && (sound == "match-tick" || sound == "decree-unroll") ? .60f : 1f);
            hud.TrophyCharge += sfx.SetTrophyCharge;
            hud.RematchPressed += () =>
            {
                if (phase == Phase.MatchEnd) StartLobbyMatch(againstAi);
            };
            hud.TabPressed += () => sfx.Play("ui-tab");
            hud.MenuPressed += OpenMenu;
            hud.JourneyChestPressed += OpenJourneyChest;
            events = new ColosseumEvents(this, arena, fx, hud, sfx, dolls, wallSurface);
            SpawnTeams();
            OpenMenu();
#if UNITY_EDITOR
            // Editor self-test: when the bridge flag exists, both sides are AI-driven and frames are saved for review.
            if (plan != null)
            {
                Application.runInBackground = true;
                System.IO.Directory.CreateDirectory(shotFolder + "/shots");
                // Render into a phone-sized target for the whole run so the HUD is laid out and captured exactly as on a device.
                bool sharp = plan.Contains("hd");
                shotLimit = sharp ? 110 : 200;
                // "tall" checks the layout on a long 9:19.5 phone instead of 9:16.
                bool tall = plan.Contains("tall");
                video = plan.Contains("video");
                captureTarget = video
                    ? new RenderTexture(1080, tall ? 2340 : 1920, 24, RenderTextureFormat.ARGB32)
                    : new RenderTexture(sharp ? 1440 : 720, (sharp ? 2 : 1) * (tall ? 1560 : 1280), 24, RenderTextureFormat.ARGB32);
                captureTarget.antiAliasing = 4;
                if (video)
                {
                    shotLimit = 30 * 150;
                    Time.captureFramerate = 30;
                    System.IO.Directory.CreateDirectory(shotFolder + "/video");
                    foreach (string old in System.IO.Directory.GetFiles(shotFolder + "/video")) System.IO.File.Delete(old);
                    // Every sound is logged against the frame it plays on, so the soundtrack can be rebuilt exactly.
                    soundLog = new System.IO.StreamWriter(shotFolder + "/video/sounds.txt") { AutoFlush = true };
                    sfx.Heard = (name, volume) => { if (soundLog != null && shots < shotLimit) soundLog.WriteLine(shots + " " + name + " " + volume.ToString(System.Globalization.CultureInfo.InvariantCulture)); };
                }
                arenaCamera.targetTexture = captureTarget;
                hud.BeginCapture(arenaCamera);
                scriptedShots = !plan.Contains("lobby") && !plan.Contains("match");
                testLeaper = plan.Contains("orc");
                if (closeUp) hud.ShowMenu(false);
                if (plan.Contains("match")) autoMatch = video ? 3f : 1.2f;
                else if (!plan.Contains("lobby")) StartMatch(true);
            }
#endif
        }

        void OnDestroy()
        {
            FinishMatchAnalytics(MatchOutcome.Abandoned);
            Time.timeScale = 1f;
            if (video)
            {
                Time.captureFramerate = 0;
                if (soundLog != null) soundLog.Dispose();
                soundLog = null;
            }
            if (captureTarget != null)
            {
                if (arenaCamera != null) arenaCamera.targetTexture = null;
                captureTarget.Release();
                Destroy(captureTarget);
            }
            Kit.Dispose();
        }

        static PhysicsMaterial Surface(string name, float bounce)
        {
            return Kit.Own(new PhysicsMaterial(name)
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = bounce,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            });
        }

        internal Material RingMaterial(int team, int style)
        {
            return ringMaterials[team, Mathf.Clamp(style, 0, 2)];
        }

        // ------------------------------------------------------------------ match flow

        // Both the versus screen and every set of this match use this same snapshot.
        void PrepareTeams()
        {
            System.Array.Fill(matchHeroes, -1);
            playerTeamSize = rivalTeamSize = 0;
            foreach (int hero in ColosseumRoster.LineupHeroes())
            {
                if (hero < 0) continue;
                matchHeroes[playerTeamSize] = hero;
                matchLevels[playerTeamSize++] = ColosseumRoster.Level(hero);
            }

            int stage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            int completed = ColosseumSave.CompletedMatches;
            trainingMatch = !selfTest && stage == 1 && completed < 2 && ColosseumRoster.HasFirstVictoryPath;
            guidedMatch = !selfTest && ColosseumRoster.HasFirstVictoryPath && ColosseumSave.MatchWins < 6;
            archerLessonMatch = trainingMatch && completed == 1;
            if (archerLessonMatch && System.Array.IndexOf(matchHeroes, ColosseumRoster.Archer, 0, playerTeamSize) < 0)
            {
                // A first-match loss leaves the Archer locked. Lend her for this battle's lesson.
                int slot = Mathf.Min(playerTeamSize, UnitsPerSide - 1);
                matchHeroes[slot] = ColosseumRoster.Archer;
                matchLevels[slot] = ColosseumRoster.Level(ColosseumRoster.Archer);
                playerTeamSize = slot + 1;
            }
            // The rival puts out as many heroes as the player has: in the first arena the knight, the orc and the archer
            // first, then any others; further on, any heroes the player has met.
            int rivalCount = Mathf.Clamp(playerTeamSize, 1, UnitsPerSide);
            if (stage == 1)
                foreach (int hero in new[] { ColosseumRoster.Knight, ColosseumRoster.Orc, ColosseumRoster.Archer })
                    if (rivalTeamSize < rivalCount) AddRivalHero(hero);
            FillRivals(rivalCount);
        }

        void FillRivals(int count)
        {
            for (int round = 0; round < 2 && rivalTeamSize < count; round++)
            {
                var candidates = new List<int>(ColosseumRoster.Heroes.Length);
                for (int hero = 0; hero < ColosseumRoster.Heroes.Length; hero++)
                    if ((round == 1 || !ColosseumRoster.Heroes[hero].Locked) && System.Array.IndexOf(matchHeroes, hero, UnitsPerSide, rivalTeamSize) < 0)
                        candidates.Add(hero);
                while (rivalTeamSize < count && candidates.Count > 0)
                {
                    int pick = Random.Range(0, candidates.Count);
                    AddRivalHero(candidates[pick]);
                    candidates.RemoveAt(pick);
                }
            }
        }

        void AddRivalHero(int hero)
        {
            int index = UnitsPerSide + rivalTeamSize++;
            matchHeroes[index] = hero;
            matchLevels[index] = trainingMatch || redLevels == null || hero >= redLevels.Length
                ? 1 : Mathf.Clamp(redLevels[hero], 1, ColosseumRoster.MaxLevel);
        }

        void SpawnTeams()
        {
            if (events != null) events.Clear();
            if (boost != null) boost.Clear();
            if (quake != null) quake.Reset();
            if (mines != null) mines.Clear();
            if (stars != null) stars.Clear();
            if (twister != null) twister.Clear();
            caster = null;
            whirler = null;
            whirlBurst = whirled = false;
            skyFall = 0f;
            taken.Clear();
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll != null)
                    Destroy(doll.gameObject);
            }
            dolls.Clear();
            truces.Clear();
            fx.ClearArrows();
            skewered.Clear();
            slack.Clear();
            hooker = null;
            blastLeft = 0f;
            rider = null;
            float[] across = { -.78f, -.26f, .26f, .78f };
            // Distances from midfield keep the opening shots in range as the arena grows.
            float[] deep = { 7.14f, 8.47f, 8.47f, 7.14f };
            bool scatter = !selfTest && !trainingMatch && ColosseumSave.MatchWins >= 3;
            var placed = new List<Vector3>();
            for (int team = 0; team < 2; team++)
            {
                int teamSize = team == 0 ? playerTeamSize : rivalTeamSize;
                for (int slot = 0; slot < teamSize; slot++)
                {
                    float side = team == 0 ? -1f : 1f;
                    float x = teamSize == UnitsPerSide ? across[slot] : (slot - (teamSize - 1) * .5f) * .52f;
                    float z = trainingMatch ? 4.39f : deep[slot];
                    var home = new Vector3(x * HalfWidth * -side, 0f, z * side);
                    if (scatter) home = ScatterSpot(side, placed, home);
                    placed.Add(home);
                    int index = team * UnitsPerSide + slot;
                    int hero = matchHeroes[index];
                    int level = matchLevels[index];
                    var item = new GameObject((team == 0 ? "Blue " : "Red ") + ColosseumRoster.Heroes[hero].Weapon);
                    item.transform.SetParent(World, false);
                    item.transform.position = home;
                    var doll = item.AddComponent<ColosseumDoll>();
                    doll.Setup(this, index, team, slot, hero, level, dollSurface);
                    doll.LookTarget = arenaCamera.transform.position;
                    dolls.Add(doll);
                }
            }
            selected = launched = aiDoll = null;
            HideAimCancel();
            pulling = false;
        }

        /// <summary>Somewhere in one team's half, clear of the walls, the middle line and the heroes already put out.</summary>
        static Vector3 ScatterSpot(float side, List<Vector3> placed, Vector3 fallback)
        {
            for (int tries = 0; tries < 60; tries++)
            {
                var spot = new Vector3(Random.Range(-.8f, .8f) * HalfWidth, 0f, side * Random.Range(3.4f, HalfDepth - 1.6f));
                bool clear = true;
                foreach (Vector3 other in placed)
                    if (Kit.Flat(other - spot).magnitude < 2.1f) { clear = false; break; }
                if (clear) return spot;
            }
            return fallback;
        }

        /// <summary>The blue line-up stands on the lobby podium: the same models as in battle, frozen in place.</summary>
        void SpawnLobbyHeroes()
        {
            int[] lineup = ColosseumRoster.LineupHeroes();
            for (int slot = 0; slot < lineup.Length && slot < ColosseumLobby.HeroSpots.Length; slot++)
            {
                int hero = lineup[slot];
                if (hero < 0) continue;
                var item = new GameObject("Lobby " + ColosseumRoster.Heroes[hero].Weapon);
                item.transform.SetParent(lobby.Root, false);
                item.transform.position = ColosseumLobby.Home + ColosseumLobby.HeroSpots[slot];
                var doll = item.AddComponent<ColosseumDoll>();
                doll.Setup(this, 100 + slot, 0, slot, hero, ColosseumRoster.Level(hero), dollSurface);
                doll.EnterShowcase();
                doll.Body.isKinematic = true;
                doll.Shape.enabled = false;
                doll.Facing = Vector3.back;
                doll.RingStyle = -1;
                lobbyHeroes.Add(doll);
            }
        }

        /// <summary>Both line-ups stand in a row far off-screen so each hero can be photographed alone.</summary>
        void BuildStudio()
        {
            studioRoot = new GameObject("Portrait studio").transform;
            studioRoot.SetParent(World, false);
            studioRoot.position = ColosseumLobby.Home + new Vector3(80f, 0f, 0f);
            int heroCount = ColosseumRoster.Heroes.Length;
            for (int i = 0; i < heroCount * 2; i++)
            {
                int team = i / heroCount, hero = i % heroCount;
                var item = new GameObject("Studio " + ColosseumRoster.Heroes[hero].Weapon);
                item.transform.SetParent(studioRoot, false);
                item.transform.position = studioRoot.position + new Vector3(i * 5f, 0f, 0f);
                var doll = item.AddComponent<ColosseumDoll>();
                doll.Setup(this, 200 + i, team, hero, hero, 1, dollSurface);
                doll.EnterShowcase();
                doll.Body.isKinematic = true;
                doll.Shape.enabled = false;
                doll.Facing = Vector3.back;
                doll.RingStyle = -1;
                doll.LookTarget = item.transform.position + new Vector3(0f, 1.6f, -12f);
                studio.Add(doll);
            }
            portraitCamera = new GameObject("Portrait camera").AddComponent<Camera>();
            portraitCamera.transform.SetParent(studioRoot, false);
            portraitCamera.enabled = false;
            portraitCamera.orthographic = true;
            portraitCamera.orthographicSize = .8f;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.nearClipPlane = .1f;
            portraitCamera.farClipPlane = 30f;
        }

        void CapturePortraits()
        {
            Quaternion angle = Quaternion.Euler(8f, 0f, 0f);
            for (int i = 0; i < studio.Count; i++)
            {
                if (portraits[i] == null)
                    portraits[i] = Kit.Own(new RenderTexture(228, 268, 24, RenderTextureFormat.ARGB32) { name = "Colosseum hero portrait" });
                portraitCamera.backgroundColor = i < ColosseumRoster.Heroes.Length ? new Color32(108, 176, 250, 255) : new Color32(244, 124, 116, 255);
                portraitCamera.targetTexture = portraits[i];
                portraitCamera.transform.SetPositionAndRotation(studio[i].transform.position + Vector3.up * 1.02f - angle * Vector3.forward * 10f, angle);
                portraitCamera.Render();
            }
            portraitCamera.targetTexture = null;
            portraitsReady = true;

            // Cut-out busts while the heroes are still standing there: the knight for the player's tile, the
            // axe hero for the heroes tab.
            StepIcons(-1);
            studio[0].ShowWeapon(false);
            studio[1].ShowWeapon(false);
            studio[ColosseumRoster.Heroes.Length + 1].ShowWeapon(false);
            Sprite avatar = icons.Bust("avatar", studio[1].HeadPosition + new Vector3(0f, -.05f, 0f), .6f, 10f);
            Sprite rival = icons.Bust("rival", studio[ColosseumRoster.Heroes.Length + 1].HeadPosition + new Vector3(0f, -.05f, 0f), .6f, 10f);
            Sprite hero = icons.Bust("hero", studio[0].transform.position + new Vector3(0f, .94f, 0f), 1.24f, 11f);
            studio[0].ShowWeapon(true);
            studio[1].ShowWeapon(true);
            studio[ColosseumRoster.Heroes.Length + 1].ShowWeapon(true);
            hud.SetArt("avatar", avatar);
            hud.SetArt("rival", rival);
            hud.SetArt("hero", hero);
            SaveArt("avatar", avatar);
            SaveArt("hero", hero);
            studioRoot.gameObject.SetActive(false);
        }

        /// <summary>Photographs the next few toy icons and hands them to the HUD; a negative count finishes them all.</summary>
        void StepIcons(int steps)
        {
            while (iconWork != null && steps-- != 0)
            {
                if (!iconWork.MoveNext())
                {
                    iconWork.Dispose();
                    iconWork = null;
                    break;
                }
                hud.SetArt(iconWork.Current.Key, iconWork.Current.Value);
                SaveArt(iconWork.Current.Key, iconWork.Current.Value);
            }
        }

        void SaveArt(string key, Sprite sprite)
        {
#if UNITY_EDITOR
            if (!selfTest) return;
            System.IO.Directory.CreateDirectory(shotFolder + "/icons");
            System.IO.File.WriteAllBytes(shotFolder + "/icons/" + key + ".png", sprite.texture.EncodeToPNG());
#endif
        }

        /// <summary>Pick the rival once, then let the versus HUD run the intro and reveal.</summary>
        void BeginMatching()
        {
            if (phase != Phase.Menu || hud.IsLaunchTitleOpen || waitingForRewardArrival || showingArenaReward || hud.JourneyChestOpen ||
                (!selfTest && ColosseumMatchRewards.HasPendingMatch)) return;

            phase = Phase.Matching;
            sfx.Play("battle-start");
            if (!portraitsReady) CapturePortraits();
            hud.HideBattleLesson();

            PrepareTeams();
            int trophies = ColosseumSave.Trophies;
            rivalName = RivalNames[Random.Range(0, RivalNames.Length)];
            int rivalTrophies = Mathf.Max(0, trophies + Random.Range(-4, 7) * 10);
            int[] avatars = ColosseumSave.AvatarIds;
            int rivalAvatar = avatars[Random.Range(0, avatars.Length)];
            hud.ShowMatch(ColosseumSave.PlayerName, trophies, portraits, matchHeroes, matchLevels, rivalName, rivalTrophies, rivalAvatar, 6f);
        }

        void OpenMenu() => EnterLobby(false);

        void EnterLobby(bool afterClaim)
        {
            if (slingLessonActive) CloseSlingLesson();
            FinishMatchAnalytics(MatchOutcome.Abandoned);
            sfx.Music("bgm-lobby");
            CancelPull();
            phase = Phase.Menu;
            decreeKey = null;
            hud.HideRoyalDecree();
            hud.HideBanner();
            hud.HideTurn();
            hud.ShowEnd(false);
            hud.ShowBattle(false);
            hud.HideMatch();
            hud.ShowMenu(true);
            if (events != null) events.Clear();
            if (boost != null) boost.Clear();
            if (quake != null) quake.Reset();
            if (mines != null) mines.Clear();
            if (stars != null) stars.Clear();
            DropWhirl();
            lobby.Show(true);
            arenaCamera.backgroundColor = ColosseumLobby.SkyMiddle;
            hud.RefreshLobby(0);
            if (selfTest || afterClaim) return;
            if (!launchTitleEntered)
            {
                if (!hud.IsLaunchTitleOpen) hud.ShowLaunchTitle(EnterFromLaunchTitle);
                return;
            }
            RestoreLobbyRewards();
        }

        void EnterFromLaunchTitle()
        {
            launchTitleEntered = true;
            if (ColosseumRoster.FirstBattleLocked && !ColosseumMatchRewards.HasPendingMatch)
                StartLobbyMatch(true);
            else
                RestoreLobbyRewards();
        }

        void RestoreLobbyRewards()
        {
            if (ColosseumMatchRewards.HasPendingMatch) ShowUnclaimedMatchReward();
            else ShowNextLobbyReward();
        }

        void ShowUnclaimedMatchReward()
        {
            bool won = ColosseumMatchRewards.PendingWon;
            int coins = Mathf.Min(won ? ColosseumSave.WinCoins : ColosseumSave.LossCoins, int.MaxValue - ColosseumSave.Coins);
            var cards = ColosseumMatchRewards.PendingCards();
            if (won)
            {
                int trophies = Mathf.Min(ColosseumSave.WinTrophies, int.MaxValue - ColosseumSave.Trophies);
                hud.ShowVictoryRewards(trophies, coins, cards, ClaimMatchReward);
            }
            else hud.ShowDefeatRewards(coins, cards, ClaimMatchReward);
        }

        void ClaimMatchReward()
        {
            bool won = ColosseumMatchRewards.PendingWon;
            int previous = ColosseumSave.JourneyTrophies;
            int previousScore = ColosseumSave.Trophies;
            int previousCoins = ColosseumSave.Coins;
            if (!ColosseumMatchRewards.ClaimMatch(out int[] cards)) return;
            int total = ColosseumSave.JourneyTrophies;
            int flyingCoins = won ? 0 : ColosseumSave.Coins - previousCoins;
            waitingForRewardArrival = true;
            hud.HideVictoryRewards();
            hud.PrepareVictoryArrival(previous, previousScore);
            EnterLobby(true);
            hud.StartVictoryArrival(previous, total, total - previous, cards, flyingCoins);
        }

        void OpenJourneyChest(int number)
        {
            if (selfTest || ColosseumRoster.FirstBattleLocked || phase != Phase.Menu || hud.IsLaunchTitleOpen || hud.JourneyChestOpen || waitingForRewardArrival ||
                showingArenaReward || ColosseumMatchRewards.HasPendingMatch) return;
            if (!ColosseumSave.OpenJourneyChest(number, out int[] cards)) return;
            hud.ShowJourneyChest(ColosseumSave.ChestCoins, cards, () =>
            {
                if (!ColosseumSave.CollectJourneyChest(number, out int coins)) return;
                hud.HideJourneyChest();
                hud.RefreshLobby(0);
                int total = ColosseumSave.JourneyTrophies;
                waitingForRewardArrival = true;
                hud.StartVictoryArrival(total, total, 0, cards, coins);
            });
        }

        void ShowNextLobbyReward()
        {
            ColosseumMatchRewards.RecoverReachedArena();
            int stage = ColosseumMatchRewards.PendingArena;
            if (stage == 0)
            {
                showingArenaReward = false;
                if (ColosseumSave.PendingChest > 0) OpenJourneyChest(ColosseumSave.PendingChest);
                else hud.ShowPendingRecruit();
                return;
            }
            showingArenaReward = true;
            var cards = ColosseumMatchRewards.ArenaCards(stage);
            int cursor = ColosseumMatchRewards.ArenaCardCursor;
            if (cursor < 0)
            {
                hud.ShowArenaUnlock(stage, cards, () =>
                {
                    ColosseumMatchRewards.ConfirmArena(stage);
                    hud.HideArenaUnlock();
                    ShowNextLobbyReward();
                });
                return;
            }
            if (cursor >= cards.Count)
            {
                ColosseumMatchRewards.GrantArenaCard(stage, cursor);
                ShowNextLobbyReward();
                return;
            }
            hud.ShowArenaCardGrant(cards[cursor], () => ColosseumMatchRewards.GrantArenaCard(stage, cursor), ShowNextLobbyReward);
        }

        void StartLobbyMatch(bool vsAi)
        {
            if (hud.IsLaunchTitleOpen || (!selfTest && (ColosseumMatchRewards.HasPendingMatch || showingArenaReward || waitingForRewardArrival))) return;
            if (phase != Phase.Matching) PrepareTeams();
            // Editor event previews can share a play session with the lobby and its rematch button.
            ColosseumEvents.TestPlan = null;
            StartMatch(vsAi);
        }

        void StartMatch(bool vsAi)
        {
            FinishMatchAnalytics(MatchOutcome.Abandoned);
            sfx.Music("bgm-battle");
            againstAi = vsAi;
            winner = -1;
            decreeKey = null;
            hud.HideRoyalDecree();
            lobby.Show(false);
            // Every arena of the journey has its own map: the arena is dressed for the one this battle is fought in.
            int arenaStage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            if (arena.Dress(ColosseumEvents.TestStage > 0 ? ColosseumEvents.TestStage : arenaStage)) fx.Dress();
            arenaCamera.backgroundColor = arena.Sky;
            hud.ShowMenu(false);
            hud.ShowEnd(false);
            hud.HideMatch();
            hud.ShowBattle(true);
            hud.SetLabels(vsAi, rivalName);
            hud.ClearPopups();
            // The balance sheet is read before the teams are put out: the earthquake picks its stretches of wall as they are.
            int stage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            events.Prepare(stage, ColosseumJourneyMaps.Name(stage), vsAi && !selfTest);
            SpawnTeams();
            // Counted by wins: the battle after the second win opens with the power gate, after the third with the
            // earthquake, and from the fourth on with a random event.
            events.Match = vsAi && !selfTest ? ColosseumSave.MatchWins + 1 : 0;
            events.BeginMatch();
            BeginSlingLesson(vsAi);
            if (vsAi && !selfTest && ColosseumRoster.NeedsArcherBattle) ColosseumRoster.StartArcherBattle();
            turnsTaken = 0;
            BeginScheduledTurn(0, true);
            PositionCamera(0f);
            hud.Banner("BATTLE!", "BLUE STARTS", 1.3f);
            sfx.Play("set-start");
            StartMatchAnalytics();
        }

        /// <summary>
        /// Announce the move, play any scheduled map event, then hand control to the side taking the shot.
        /// </summary>
        void BeginScheduledTurn(int team, bool afterBanner = false)
        {
            nextSide = team;
            acts.Clear();
            bool newRound = events.StartingTurn(turnsTaken, acts);
            if (newRound) hud.SetTurnCounter(events.Round);
            if (acts.Count > 0)
            {
                if (afterBanner) acts.Insert(0, string.Empty);
                hud.HideTurn();
            }
            if (newRound && events.Round == events.TerrainTurn)
            {
                hud.HideTurn();
                if (events.TerrainDecreed)
                {
                    // Read out on the scroll first, once the opening banner has had its moment.
                    bool paused = acts.Count > 0 && acts[0].Length == 0;
                    acts.Insert(paused ? 1 : 0, ColosseumEventTable.Quake);
                    if (afterBanner && !paused) acts.Insert(0, string.Empty);
                    NextAct();
                    return;
                }
                BeginQuake();
                return;
            }
            NextAct();
        }

        void NextAct()
        {
            if (acts.Count == 0)
            {
                BeginTurn(nextSide);
                return;
            }
            string act = acts[0];
            acts.RemoveAt(0);
            selected = launched = aiDoll = null;
            HideAimCancel();
            pulling = false;
            fx.ClearArrows();
            fx.HideAim();
            phaseTime = 0f;
            if (act.Length == 0)
            {
                phase = Phase.Event;
                actSpan = 1.25f;
                return;
            }
            if (act == ColosseumEventTable.Quake)
            {
                decreeCandidates.Clear();
                decreeCandidates.Add(ColosseumEventTable.Quake);
                decreeCandidates.Add(ColosseumEventTable.Gate);
                decreeKey = ColosseumEventTable.Quake;
                hud.ShowRoyalDecree(decreeCandidates, decreeKey, "Parts of the arena wall come down.", FirstDecree(decreeKey));
                phase = Phase.Decree;
                return;
            }
            decreeKey = events.PrepareDecree(act, decreeCandidates, out string description);
            if (decreeKey == null)
            {
                NextAct();
                return;
            }
            hud.ShowRoyalDecree(decreeCandidates, decreeKey, description, FirstDecree(decreeKey));
            phase = Phase.Decree;
        }

        /// <summary>
        /// True the first time the player is shown this event on the royal scroll: the scroll then stays open a
        /// little longer, long enough to read. It is remembered with the save, so it happens once for each event.
        /// </summary>
        bool FirstDecree(string key)
        {
            if (selfTest || !againstAi || string.IsNullOrEmpty(key)) return false;
            string seen = ColosseumEvents.DecreeSeenKey + key;
            if (PlayerPrefs.GetInt(seen, 0) != 0) return false;
            PlayerPrefs.SetInt(seen, 1);
            PlayerPrefs.Save();
            return true;
        }

        void ResolveDecree()
        {
            hud.HideRoyalDecree();
            if (decreeKey == ColosseumEventTable.Quake)
            {
                decreeKey = null;
                BeginQuake();
                return;
            }
            bool gate = decreeKey == ColosseumEventTable.Gate;
            decreeKey = null;
            if (gate) OpenPowerStrips();
            else events.RevealDecree();
            phase = Phase.Event;
            phaseTime = 0f;
            actSpan = gate ? .9f : 1.85f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ObserveTinySauceStartup()
        {
            // This mirrors ZombieDefense's game-side bridge.  TinySauce itself stays untouched.
            if (Application.isEditor) return;

            analyticsAllowedAtStartup = false;
            TinySauce.SubscribeOnInitFinishedEvent(OnTinySauceStartup);
        }

        static void OnTinySauceStartup(bool attribution, bool analytics)
        {
            TinySauce.UnsubscribeOnInitFinishedEvent(OnTinySauceStartup);
            // GameAnalytics measures gameplay without collecting the Android advertising ID.
            if (analytics)
            {
                try
                {
                    GameAnalytics.EnableAdvertisingIdTracking(false);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"Could not configure GameAnalytics advertising ID tracking ({exception.GetType().Name}).");
                }
            }
            // Providers are configured from this startup choice, so retain that decision for the session.
            analyticsAllowedAtStartup = analytics;
        }

        internal static void StopMatchAnalyticsForSession()
        {
            analyticsAllowedAtStartup = false;
        }

        void StartMatchAnalytics()
        {
            if (!againstAi || selfTest || Application.isEditor || !CanTrackMatch || analyticsMatch.HasValue) return;

            int stage = ColosseumSave.StageFor(ColosseumSave.JourneyTrophies);
            var match = new AnalyticsMatch(stage, trainingMatch);
            analyticsMatch = match;
            string level = stage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                TinySauce.OnGameStarted(new GameStartedParameters
                {
                    levelDimension1 = level,
                    levelDefinitionID = level,
                    ordinal = stage,
                    eventContextProperties = new Dictionary<string, object> { { "tutorial", match.Tutorial } }
                });
            }
            catch (System.Exception exception)
            {
                analyticsMatch = null;
                Debug.LogWarning($"Could not record battle start ({exception.GetType().Name}).", this);
            }
        }

        void FinishMatchAnalytics(MatchOutcome outcome)
        {
            if (!analyticsMatch.HasValue) return;

            // Claim it before SDK work: a reward/scene retry cannot duplicate this result.
            AnalyticsMatch match = analyticsMatch.Value;
            analyticsMatch = null;
            if (Application.isEditor || !CanTrackMatch) return;

            bool won = outcome == MatchOutcome.Win;
            bool finished = outcome != MatchOutcome.Abandoned;
            MatchOutcome reason = finished ? MatchOutcome.Loss : MatchOutcome.Abandoned;
            var context = new Dictionary<string, object>
            {
                { "tutorial", match.Tutorial },
                { "outcome", won ? "win" : finished ? "loss" : "abandoned" }
            };
            string level = match.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                TinySauce.OnGameFinished(new GameFinishedParameters
                {
                    levelDimension1 = level,
                    levelDefinitionID = level,
                    status = won,
                    score = won ? 1 : 0,
                    gameEndReason = won ? null : (System.Enum)reason,
                    eventContextProperties = context
                });
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Could not record battle result ({exception.GetType().Name}).", this);
            }
        }

        void BeginTurn(int team)
        {
            TickTruces(0f, endTurn: true);
            turn = striker = team;
            DropCatch();
            DropWhirl();
            rider = null;
            wild = false;
            if (caster != null) caster.EndCast();
            caster = null;
            phase = Phase.Aim;
            phaseTime = 0f;
            selected = launched = aiDoll = null;
            HideAimCancel();
            pulling = false;
            aiClock = video ? -1.6f : -0.7f;   // when recording, let the turn banner clear first, as a player would
            fx.ClearArrows();
            fx.HideAim();
            events.BeginTurn(team);
            hud.AnnounceTurn(againstAi ? (team == 0 ? "YOUR TURN!" : "RIVAL TURN!") : (team == 0 ? "BLUE TURN!" : "RED TURN!"), team);
            sfx.Play("turn-start", team == 0 ? 1f : .55f);
        }

        bool RivalThinks { get { return selfTest || (againstAi && turn == 1); } }

        int AliveCount(int team)
        {
            int count = 0;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Team == team && doll.Alive)
                    count++;
            }
            return count;
        }

        void EndTurn()
        {
            // The goblin's bombs go off before the turn passes: one after another, first stuck first. They are set
            // going as soon as the sand is still (see Update); this catches any that are somehow left.
            if (mines.Any)
            {
                mines.Light();
                phase = Phase.Sim;
                phaseTime = 0f;
                return;
            }
            // The map events have their say once the turn is played out (lava vents go off now). If one of them
            // throws anybody about, that is played out too before the turn passes.
            if (events.EndTurn(turn))
            {
                phase = Phase.Sim;
                phaseTime = 0f;
                return;
            }
            boost.EndTurn();
            int blue = AliveCount(0), red = AliveCount(1);
            if (blue == 0 || red == 0)
            {
                FinishMatch(blue > 0 ? 0 : 1, false);
                return;
            }
            // Another turn won out of the supply crate is taken at once, and is not counted as one of the round's two.
            if (events.TakeExtraTurn(turn))
            {
                hud.Banner("EXTRA TURN!", (turn == 0 ? "BLUE" : "RED") + " GOES AGAIN", 1.2f, turn);
                sfx.Play("powerup");
                BeginScheduledTurn(turn, true);
                return;
            }

            turnsTaken++;
            if (turnsTaken >= RoundLimit * 2)
            {
                FinishMatch(Leader(), true);
                return;
            }
            bool finalPair = turnsTaken == (RoundLimit - 1) * 2;
            if (finalPair) hud.Banner("FINAL TURNS", "MOST HP WINS", 1.3f);
            BeginScheduledTurn(1 - turn, finalPair);
        }

        /// <summary>Let the walls settle before aiming. This terrain change plays entirely on the court.</summary>
        void BeginQuake()
        {
            phase = Phase.Quake;
            phaseTime = 0f;
            hud.HideBanner();
            selected = launched = aiDoll = null;
            HideAimCancel();
            pulling = false;
            fx.ClearArrows();
            fx.HideAim();
            quake.Begin();
            sfx.Play("quake", .65f);
            arena.ReactToQuake(2.6f);
        }

        /// <summary>
        /// The goblin sapper has run into an enemy and hurt it: a bomb is stuck on that enemy's head. It goes off
        /// before the turn is over. Every enemy the goblin hits gets one, but only one to a head.
        /// </summary>
        void StickBomb(ColosseumDoll sapper, ColosseumDoll victim)
        {
            if (!victim.Alive || victim.Lost || bombed.Contains(victim)) return;
            if (!mines.Plant(sapper.Team, victim, AttackDamage(sapper, BombAttackRatio))) return;
            bombed.Add(victim);
            bombClock = 0f;
            hud.Pop(victim.transform.position + Vector3.up * 2.3f, "BOMB!", PowerGold, 1.15f);
            sfx.Play("plant");
        }

        /// <summary>A bomb's turn has come: it starts to shake on its carrier's head, and the fuse fizzes up.</summary>
        void OnBombShakes(ColosseumDoll carrier)
        {
            sfx.Play("fuse");
            Shake(.06f);
        }

        /// <summary>
        /// A bomb goes off: the hero carrying it is hurt and thrown into the air, and every team-mate of theirs
        /// inside the ring is hurt less and thrown clear.
        /// </summary>
        void OnBombGoesOff(ColosseumDoll carrier, int team, int full)
        {
            Vector3 spot = Kit.Flat(carrier.transform.position);
            fx.Crater(spot, ColosseumMines.BlastRadius * .7f);
            fx.Shockwave(spot, ColosseumMines.BlastRadius * 1.25f);
            fx.Puff(spot, 22, 6.5f);
            fx.Impact(spot + Vector3.up * .6f, 1.6f);
            fx.Sparks(spot + Vector3.up * .5f, 18);
            fx.Burst(spot + Vector3.up, 1.6f);
            events.Blast(spot, ColosseumMines.BlastRadius, team);
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Alive || doll.Team == team || doll.Body.isKinematic) continue;
                if (doll == carrier)
                {
                    Vector2 fling = Random.insideUnitCircle.normalized * 4f;
                    doll.Knock(new Vector3(fling.x, 9f, fling.y));
                    Deal(doll, full, null, Color.white, 1.3f);
                    continue;
                }
                Vector3 away = Kit.Flat(doll.transform.position - spot);
                float gap = away.magnitude;
                if (gap > ColosseumMines.BlastRadius + ColosseumDoll.Radius * doll.Bulk) continue;
                away = gap > .01f ? away / gap : Vector3.forward;
                doll.Knock(away * Mathf.Lerp(10.5f, 6.5f, Mathf.Clamp01(gap / ColosseumMines.BlastRadius)) + Vector3.up * 5.8f);
                Deal(doll, Mathf.RoundToInt(full * BombSplash), null, new Color(1f, .91f, .69f), 1f, new Color(1f, .62f, .3f));
            }
            hud.Pop(spot + Vector3.forward * 1.5f, "BOOM!", new Color(1f, .82f, .23f), 1.5f);
            sfx.Play("boom");
            sfx.Play("cheer", .5f);
            arena.ReactToHit();
            Shake(.45f);
            stopLeft = .06f;
            slowLeft = Mathf.Max(slowLeft, .2f);
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .5f);
            // The clock on a turn that will not settle starts again with every blast.
            phaseTime = 0f;
        }

        /// <summary>A stretch of wall has come down, leaving a gap.</summary>
        void OnWallCrumbles(Vector3 at, int side)
        {
            sfx.Play("crumble");
            Shake(.42f);
            bool end = Mathf.Abs(at.z) - HalfDepth > Mathf.Abs(at.x) - HalfWidth;
            fx.Shockwave(at - (end ? Vector3.forward * (Mathf.Sign(at.z) * .4f) : Vector3.right * (side * .4f)), 1.7f);
            excitement = 1.6f;
        }

        /// <summary>A hero has gone out through a gap in the wall: it is out of the fight at once, whatever health it had left.</summary>
        void RingOut(ColosseumDoll doll)
        {
            bool wasAlive = doll.Alive;
            Vector3 at = doll.transform.position;
            bool end = Mathf.Abs(at.z) - HalfDepth > Mathf.Abs(at.x) - HalfWidth;
            float side = (end ? at.z : at.x) < 0f ? -1f : 1f;
            doll.FallOut(end ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f));
            fx.Puff(end ? new Vector3(at.x, .2f, side * (HalfDepth + .15f)) : new Vector3(side * (HalfWidth + .15f), .2f, at.z), 8, 3f);
            sfx.Play("fall");
            if (!wasAlive) return;
            // The words are called out inside the court, where they cannot run off the edge of the screen.
            var shown = end ? new Vector3(Mathf.Clamp(at.x, 2f - HalfWidth, HalfWidth - 2f), 0f, side * (HalfDepth - 1.9f)) : new Vector3(side * (HalfWidth - 1.9f), 0f, at.z);
            hud.Pop(shown, "RING OUT!", DamageRed, 1.45f);
            ColosseumHaptics.Play(doll.Team == 0 ? ColosseumHaptics.Feel.Fallen : ColosseumHaptics.Feel.Kill);
            hud.Pop(shown + Vector3.forward * 1.1f, "K.O!", new Color(1f, .82f, .23f), 1.3f);
            sfx.Play("ko");
            sfx.Play("crowd-gasp", .9f);
            sfx.Play("cheer", .8f);
            arena.ReactToKnockout();
            Shake(.26f);
            slowLeft = .5f;
            cheerLeft = 1.1f;
            excitement = 1.6f;
        }

        /// <summary>The side ahead when the rounds run out: more health left, then more heroes standing.</summary>
        int Leader()
        {
            int[] health = new int[2], standingCount = new int[2];
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Alive) continue;
                health[doll.Team] += doll.Health;
                standingCount[doll.Team]++;
            }
            if (health[0] != health[1]) return health[0] > health[1] ? 0 : 1;
            if (standingCount[0] != standingCount[1]) return standingCount[0] > standingCount[1] ? 0 : 1;
            return 1;   // Blue opens the match; red wins an exact tie as the second mover.
        }

        void OpenPowerStrips()
        {
            boost.Open(HalfWidth);
            sfx.Play("strip");
            Shake(.24f);
            EventPunch(.035f);
            events.Fanfare(PowerGold, .1f, false);
            excitement = Mathf.Min(1.6f, excitement + .4f);
        }

        /// <summary>The power gate's rounds are up (the balance sheet can give it only so many): it is gone.</summary>
        internal void ClosePowerGate() { boost.Clear(); }

        /// <summary>The slung hero has gone through a power gate: it is bigger and hits harder until the turn ends.</summary>
        void PowerUp(ColosseumDoll doll)
        {
            Vector3 at = doll.transform.position + Vector3.up * .8f;
            // Lifted well clear of the hero, so the words do not hide it flickering and springing out to size.
            hud.Pop(doll.transform.position + Vector3.up * 2.4f, "ATTACK UP!", PowerGold, 1.2f);
            fx.Burst(at, 1.3f);
            fx.Sparks(at, 10);
            sfx.Play("powerup");
            Shake(.08f);
            cheerLeft = Mathf.Max(cheerLeft, .8f);
            excitement = Mathf.Min(1.6f, excitement + .3f);
        }

        /// <summary>Damage multiplier for a hero's blows: more while it is powered up.</summary>
        float Might(ColosseumDoll attacker)
        {
            return boost.IsBoosted(attacker) ? ColosseumBoost.DamageScale : 1f;
        }

        bool Guided => guidedMatch && againstAi;

        /// <summary>True when the player's heroes have less health between them than the rival's.</summary>
        bool PlayerFarBehind()
        {
            int blue = 0, red = 0;
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Alive) continue;
                if (doll.Team == 0) blue += doll.Health; else red += doll.Health;
            }
            return blue < red * GuidedBehind;
        }

        /// <summary>True while a hero of this side has crossed the power gate this turn.</summary>
        bool SideBoosted(int team)
        {
            foreach (ColosseumDoll doll in dolls)
                if (doll.Team == team && boost.IsBoosted(doll)) return true;
            return false;
        }
        int AttackDamage(ColosseumDoll attacker, float ratio = 1f) => Mathf.RoundToInt(attacker.Attack * ratio * Might(attacker));

        void FinishMatch(int team, bool timeUp)
        {
            if (phase == Phase.MatchEnd) return;
            if (slingLessonActive) CloseSlingLesson();
            if (phase == Phase.Decree)
            {
                events.CancelDecree();
                hud.HideRoyalDecree();
                decreeKey = null;
            }
            phase = Phase.MatchEnd;
            phaseTime = 0f;
            winner = team;
            bool won = winner == 0;
            bool cheerful = won || !againstAi;
            excitement = 1.6f;
            arena.Cheer(6f, 1f);
            fx.Celebrate(HalfWidth, HalfDepth);
            sfx.Play(cheerful ? "match-win" : "match-lose");
            sfx.Duck(4.5f);
            sfx.Play("cheer", cheerful ? 1f : .5f);
            FinishMatchAnalytics(won ? MatchOutcome.Win : MatchOutcome.Loss);
            if (againstAi && !selfTest)
            {
                ColosseumMatchRewards.PrepareMatch(won);
                hud.HideTurn();
                hud.HideBanner();
                hud.ShowEnd(false);
                ShowUnclaimedMatchReward();
                return;
            }
            hud.Banner(won ? "BLUE WINS!" : "RED WINS!", timeUp ? "TIME UP!" : "KNOCKOUT!", 600f, winner);
            hud.ShowEnd(true);
        }

        // ------------------------------------------------------------------ frame loop

        void Update()
        {
            // Capture the UI clock at the same fixed cadence as the editor video and sound log.
            float real = video ? VideoStep : Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (stopLeft > 0f) { stopLeft -= real; Time.timeScale = .03f; }
            else if (slowLeft > 0f) { slowLeft -= real; Time.timeScale = .32f; }
            else Time.timeScale = 1f;

            float delta = Time.deltaTime;
            phaseTime += delta;
            excitement = Mathf.Max(0f, excitement - real * .45f);
            // The crowd murmurs under a fight, louder the more there is to see, and is not heard in the lobby.
            sfx.Crowd(phase == Phase.Menu || phase == Phase.Matching ? 0f : .2f + .3f * Mathf.Clamp01(excitement / 1.6f));
            sfx.Tick(real);
            cheerLeft -= delta;
            TickTruces(delta);
            CarryCatch(delta);
            TickLoop(delta);
            TickCast(delta);
            TickWhirl(delta);
            PositionCamera(real);

            studioFrames++;
            // One icon a frame keeps start-up smooth; the hero portraits follow once the studio is free.
            if (studioFrames > 1)
                StepIcons(1);
            if (!portraitsReady && studioFrames > 12 && iconWork == null)
                CapturePortraits();
            if (autoMatch > 0f && (autoMatch -= real) <= 0f)
                BeginMatching();

            if (slingLessonActive) TickSlingLesson(real);
            switch (phase)
            {
                case Phase.Matching:
                    if (hud.MatchReadyToStart) StartLobbyMatch(true);
                    break;
                case Phase.Aim:
                    if (RivalThinks) ThinkForRival(delta);
                    else ReadPointer();
                    break;
                case Phase.Sim:
                    if (launched != null && boost.Passes(launched)) PowerUp(launched);
                    // The goblin's bombs start to go off a beat after the last of them was stuck on, one after another,
                    // while it is still skidding away; the turn does not move on until the last of them has burst.
                    bombClock += delta;
                    if (mines.Any && !mines.Burning && (bombClock > BombBeat || EveryoneResting())) mines.Light();
                    if (phaseTime > .4f && fx.FlyingArrows == 0 && stars.Flying == 0 && caster == null && whirler == null && EveryoneResting() && !mines.Any && !events.Busy)
                    {
                        phase = Phase.Settle;
                        phaseTime = 0f;
                        standing = false;
                    }
                    else if (phaseTime > 9f)
                    {
                        foreach (ColosseumDoll doll in dolls)
                        {
                            if (!doll.Resting && !doll.Body.isKinematic)
                                doll.Body.linearVelocity = Vector3.zero;
                        }
                        fx.ClearArrows();
                    }
                    break;
                case Phase.Settle:
                    Settle();
                    break;
                case Phase.Quake:
                    if (!quake.Shaking) NextAct();
                    break;
                case Phase.Decree:
                    sfx.Duck(.30f);
                    // Keep the reel at real speed on slower phones. Limit the first step after app resume.
                    hud.TickRoyalDecree(video ? VideoStep : Mathf.Min(Time.unscaledDeltaTime, .25f));
                    if (hud.RoyalDecreeComplete) ResolveDecree();
                    break;
                case Phase.Event:
                    if (phaseTime > actSpan && !events.Busy) NextAct();
                    break;
            }

            if (quake.Open)
            {
                foreach (ColosseumDoll doll in dolls)
                {
                    // Whoever hangs on the pirate's hook goes where he goes, but is never carried out of the arena.
                    if (!doll.Lost && !doll.Caught && quake.IsOut(doll.transform.position)) RingOut(doll);
                }
            }
            float rumble = quake.Rumble;
            if (rumble > 0f) Shake(rumble);

            DirectDolls();
            if (video)
            {
                if (shots < shotLimit)
                {
                    SaveFrame(shotFolder + "/video/f_" + shots.ToString("00000") + ".jpg");
                    shots++;
                }
            }
            else if (selfTest && shots < shotLimit && (shotClock -= real) <= 0f)
            {
                shotClock = phase == Phase.Sim ? .16f : phase == Phase.Aim && phaseTime < 1.5f ? .07f : phase == Phase.Matching ? .24f : .6f;
                SaveFrame(shotFolder + "/shots/shot_" + shots.ToString("000") + ".png");
                shots++;
                // Self-test only: walk the emperor through every mood and show a win banner in the review frames.
                if (scriptedShots) {
                if (shots == 12) arena.ShowMood(2, 1.6f);
                if (shots == 20 && dolls.Count >= 4) { dolls[1].TakeDamage(110); dolls[2].TakeDamage(110); dolls[3].TakeDamage(60); }   // lopsided totals for the banner check
                if (shots == 30) arena.ShowMood(3, 1.5f);
                if (shots == 46) arena.ShowMood(4, 1.5f);
                if (shots == 60) arena.ShowMood(5, 1.4f);
                if (shots == 76)
                {
                    arena.Cheer(2.2f, 1f);
                    hud.Banner("BLUE WINS!", "KNOCKOUT!", 2.2f, 0);
                }
                }
            }
            fx.Tick(delta, dolls, HalfWidth, HalfDepth);
            boost.Tick(delta, arenaCamera);
            quake.Tick(delta, fx);
            mines.Tick(delta);
            if (phase != Phase.Decree) events.Tick(delta);
            stars.Tick(delta, -arenaCamera.transform.forward);
            twister.Tick(delta, -arenaCamera.transform.forward);
            ColosseumDoll star = launched != null ? launched : selected != null ? selected : aiDoll;
            arena.Watch(star != null ? star.transform.position : Vector3.zero);
            arena.Tick(delta, excitement);
            for (int i = 0; i < dolls.Count; i++)
                hud.SetBoost(dolls[i].Id, boost.IsBoosted(dolls[i]));
            int pickTeam = phase == Phase.Aim && !pulling && (!RivalThinks || (selfTest && aiDoll == null)) ? turn : -1;
            hud.Tick(real, arenaCamera, dolls, selected, pickTeam, RequiredLessonHero);
            if (waitingForRewardArrival && !hud.VictoryArrivalActive)
            {
                waitingForRewardArrival = false;
                ShowNextLobbyReward();
            }
        }

        void LateUpdate()
        {
            if (hud != null) hud.PlacePlates(arenaCamera, dolls);
        }

        void FixedUpdate()
        {
            // The map events that act on heroes as they move (portals, ice) keep step with the physics.
            if (events != null && phase != Phase.Menu && phase != Phase.Matching && phase != Phase.Decree)
                events.Step(Time.fixedDeltaTime);
        }

        bool EveryoneResting()
        {
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Resting)
                    return false;
            }
            return true;
        }

        void Settle()
        {
            if (!standing)
            {
                foreach (ColosseumDoll doll in dolls)
                {
                    if (!doll.Resting) { phase = Phase.Sim; return; }
                    if (doll.State == DollState.Down && doll.StateTime < .95f) return;
                    if (doll.Falling) return;   // a hero going out through a gap is watched all the way down
                }
                foreach (ColosseumDoll doll in dolls)
                {
                    if (doll.State == DollState.Ko) doll.Retire();
                    else doll.StandUp();
                }
                standing = true;
                return;
            }
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Alive && doll.State != DollState.Idle)
                    return;
            }
            EndTurn();
        }

        /// <summary>Head tracking, cheering and ring colours for everyone who is standing around.</summary>
        void DirectDolls()
        {
            Vector3 lens = arenaCamera.transform.position;
            ColosseumDoll aiming = selected != null ? selected : aiDoll;
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Alive) continue;
                bool mine = doll.Team == turn;
                switch (phase)
                {
                    case Phase.Sim:
                    case Phase.Settle:
                        doll.LookTarget = Glance(doll, launched, lens);
                        doll.Cheering = mine && cheerLeft > 0f;
                        doll.Startled = !mine && phase == Phase.Sim;
                        doll.Targeted = false;
                        doll.RingStyle = -1;
                        break;
                    case Phase.Aim:
                        doll.Targeted = !mine && InAimPath(aiming, doll);
                        doll.LookTarget = mine || doll.Targeted ? lens : Glance(doll, aiming, lens);
                        doll.Cheering = false;
                        doll.Startled = !mine && aiming != null;
                        bool human = !RivalThinks;
                        bool eligible = mine && human && (RequiredLessonHero == null || doll == RequiredLessonHero);
                        doll.RingStyle = doll == aiming ? 2 : eligible ? 1 : 0;
                        break;
                    case Phase.Quake:
                    case Phase.Event:
                        // Something is coming on to the court: every head turns to where, and nobody likes the look of it.
                        doll.LookTarget = phase == Phase.Event && events.Showing ? events.ShowSpot : lens;
                        doll.Cheering = false;
                        doll.Startled = phase == Phase.Quake || events.Showing;
                        doll.Targeted = false;
                        doll.RingStyle = -1;
                        break;
                    default:
                        doll.LookTarget = lens;
                        doll.Cheering = winner >= 0 && doll.Team == winner && phase != Phase.Menu;
                        doll.Startled = false;
                        doll.Targeted = false;
                        doll.RingStyle = phase == Phase.Menu ? 0 : -1;
                        break;
                }

                if (doll.State != DollState.Idle || doll.Gunning) continue;   // the pirate keeps his gun on his catch
                ColosseumDoll foe = Nearest(doll, 1 - doll.Team);
                if (foe == null) continue;
                Vector3 toward = Kit.Flat(foe.transform.position - doll.transform.position);
                if (toward.sqrMagnitude > .01f)
                    doll.Facing = Vector3.RotateTowards(doll.Facing, toward.normalized, Time.deltaTime * 6f, 0f);
            }
        }

        /// <summary>True when the hero being pulled back is pointed at this one, close enough to the line to get hit.</summary>
        bool InAimPath(ColosseumDoll aiming, ColosseumDoll target)
        {
            if (aiming == null || aiming.State != DollState.Aim)
                return false;
            Vector3 pull = Kit.Flat(aiming.AimPull);
            if (pull.magnitude < MinimumPull)
                return false;
            if (aiming.Kind == WeaponKind.Slam)
                return Kit.Flat(target.transform.position - aiming.LandingAim).magnitude < SlamRadius + ColosseumDoll.Radius;
            // Everyone the viking's whirlwind would reach, as well as whoever is in his way.
            if (aiming.Kind == WeaponKind.Axe && aimEyeSet
                && Kit.Flat(target.transform.position - aimEye).magnitude < WhirlReach * aiming.Bulk + ColosseumDoll.Radius * target.Bulk)
                return true;
            if (aiming.Kind == WeaponKind.Boomerang)
                return OnLoop(aimLoop, target);
            if (aiming.Kind == WeaponKind.Star)
                return Kit.Flat(target.transform.position - aiming.LandingAim).magnitude < StarScatter + StarBurst * .5f;
            if (aiming.Kind == WeaponKind.Bounce)
            {
                // Under the last leap, or beside one of the hops on the way.
                Vector3 at = Kit.Flat(target.transform.position), from = Kit.Flat(aiming.transform.position), to = Kit.Flat(aiming.LandingAim);
                if ((at - to).magnitude < PounceRadius + ColosseumDoll.Radius) return true;
                for (int k = 1; k <= BounceHops; k++)
                {
                    if ((at - Vector3.Lerp(from, to, BounceShare * k / BounceHops)).magnitude < HopRadius + ColosseumDoll.Radius) return true;
                }
                return false;
            }
            Vector3 direction = -pull.normalized;
            Vector3 gap = Kit.Flat(target.transform.position - aiming.transform.position);
            float along = Vector3.Dot(gap, direction);
            if (along > 0f && (gap - direction * along).magnitude < ColosseumDoll.Radius * 2f + .4f)
                return true;
            if (aiming.Kind != WeaponKind.Bow || target.transform.position.y >= ColosseumFx.ArrowHitHeight)
                return false;

            Vector3 targetPoint = Kit.Flat(target.transform.position);
            float speed = SlingSpeed(Mathf.InverseLerp(MinimumPull, MaximumPull, pull.magnitude));
            for (int side = -1; side <= 1; side += 2)
            {
                if (!ColosseumFx.SideArrowGuide(aiming.transform.position, direction, speed, side, out var muzzle, out var tip))
                    continue;
                Vector3 branch = Kit.Flat(tip - muzzle);
                Vector3 offset = targetPoint - Kit.Flat(muzzle);
                float fraction = Mathf.Clamp01(Vector3.Dot(offset, branch) / branch.sqrMagnitude);
                if ((offset - branch * fraction).sqrMagnitude < ColosseumFx.ArrowHitRadius * ColosseumFx.ArrowHitRadius)
                    return true;
            }
            return false;
        }

        /// <summary>Keep faces turned to the camera so expressions read, with a sideways glance at the action.</summary>
        static Vector3 Glance(ColosseumDoll doll, ColosseumDoll action, Vector3 lens)
        {
            Vector3 head = doll.HeadPosition;
            Vector3 toLens = (lens - head).normalized;
            if (action == null || action == doll)
                return lens;
            Vector3 toAction = action.HeadPosition - head;
            if (toAction.sqrMagnitude < .01f)
                return lens;
            return head + Vector3.Slerp(toLens, toAction.normalized, .3f) * 10f;
        }

        ColosseumDoll Nearest(ColosseumDoll from, int team)
        {
            ColosseumDoll best = null;
            float bestGap = float.PositiveInfinity;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Team != team || !doll.Alive) continue;
                float gap = Kit.Flat(doll.transform.position - from.transform.position).sqrMagnitude;
                if (gap < bestGap) { bestGap = gap; best = doll; }
            }
            return best;
        }

        // ------------------------------------------------------------------ input and AI

        void ReadPointer()
        {
            if (pulling && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Canceled)
            {
                CancelPull();
                return;
            }
            if (Input.GetMouseButtonDown(0))
                BeginPull(Input.mousePosition);
            else if (pulling && Input.GetMouseButton(0))
                UpdatePull(Input.mousePosition);
            else if (pulling)
                ReleasePull(Input.mousePosition);
        }

        static bool OverButton()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            if (Input.touchCount > 0)
                return events.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return events.IsPointerOverGameObject();
        }

        bool PointerOnGround(Vector2 screen, out Vector3 point)
        {
            Ray ray = arenaCamera.ScreenPointToRay(screen);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        void BeginPull(Vector2 screen)
        {
            if (pulling || !battleOutputPixels.Contains(screen) || OverButton() || !PointerOnGround(screen, out holdPoint))
                return;

            float pixelsPerUnit = arenaCamera.pixelHeight / (2f * arenaCamera.orthographicSize);
            float best = 1.15f * pixelsPerUnit;
            best *= best;
            ColosseumDoll pick = null;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Team != turn || !doll.Alive || doll.State != DollState.Idle) continue;
                Vector2 chest = arenaCamera.WorldToScreenPoint(doll.transform.position + Vector3.up * (ColosseumDoll.Radius * 1.8f));
                float gap = (chest - screen).sqrMagnitude;
                if (gap < best) { best = gap; pick = doll; }
            }
            if (pick == null || (RequiredLessonHero != null && pick != RequiredLessonHero)) return;
            selected = pick;
            pulling = true;
            selected.BeginAim();
            // No cancel target (the X beside the hero) is put up while aiming: a shot is called off by letting go
            // with the sling barely drawn.
            pullNotch = 0;
            sfx.Play("pull");
        }

        void UpdatePull(Vector2 screen)
        {
            if (selected == null || UpdateAimCancel(screen) || !PointerOnGround(screen, out Vector3 point))
                return;
            Vector3 drag = Kit.Flat(point - holdPoint);
            Vector3 pull = Vector3.ClampMagnitude(drag, MaximumPull);
            float length = pull.magnitude;
            if (length < MinimumPull)
            {
                selected.SetAim(pull, 0f);
                fx.HideAim();
                return;
            }
            float power = Mathf.InverseLerp(MinimumPull, MaximumPull, length);
            // The sling clicks through eight notches as it is drawn, and knocks once at full stretch.
            int notch = Mathf.FloorToInt(power * 8f);
            if (notch != pullNotch)
            {
                if (notch > pullNotch) ColosseumHaptics.Play(notch >= 8 ? ColosseumHaptics.Feel.Medium : ColosseumHaptics.Feel.Tick, power);
                pullNotch = notch;
            }
            selected.SetAim(pull, power);
            ShowAim(selected, pull, -pull / length, power);
            KeepAimCancelClear();
        }

        void ShowAimCancel(Vector2 press, float pixelsPerUnit)
        {
            aimCancelRadius = Mathf.Min(battleOutputPixels.width, battleOutputPixels.height) * .0288f;
            aimCancelPixelsPerUnit = pixelsPerUnit;
            Vector2 courtCenter = arenaCamera.WorldToScreenPoint(Vector3.zero);
            Vector2 feet = arenaCamera.WorldToScreenPoint(selected != null ? selected.transform.position : Vector3.zero);
            // Close beside the hero at its feet, on the side towards the middle of the court.
            aimCancelSide = feet.x > courtCenter.x ? -1f : 1f;
            PlaceAimCancel(aimCancelSide);
            aimCancelVisible = true;
        }

        float aimCancelSide = 1f, aimCancelPixelsPerUnit;

        /// <summary>
        /// The X sits beside the hero's feet, just clear of the hero, on the given side. The hero's body leans back
        /// as the sling is drawn, so while it is drawn the X keeps to whichever side the body is not.
        /// </summary>
        void PlaceAimCancel(float side)
        {
            Rect safe = battleTouchSafeArea;
            float inset = aimCancelRadius * 1.25f;
            float bottom = safe.yMin + inset, top = safe.yMax - safe.height * .12f - inset;
            Vector2 feet = arenaCamera.WorldToScreenPoint(selected != null ? selected.transform.position : Vector3.zero);
            float ring = ColosseumDoll.Radius * (selected != null ? selected.Bulk : 1f) * aimCancelPixelsPerUnit;
            float reach = ring * 1.35f + aimCancelRadius + 6f;
            float x = feet.x + side * reach;
            // Where that side is off the screen, the other side.
            if (x < safe.xMin + inset || x > safe.xMax - inset) { side = -side; x = feet.x + side * reach; }
            aimCancelSide = side;
            aimCancelCenter = new Vector2(Mathf.Clamp(x, safe.xMin + inset, safe.xMax - inset), Mathf.Clamp(feet.y, bottom, top));
            hud.ShowAimCancel(aimCancelCenter, aimCancelRadius * 2f);
        }

        /// <summary>True while the drawn-back body of the aiming hero lies over the X.</summary>
        bool AimCancelCovered()
        {
            if (selected == null) return false;
            float ring = ColosseumDoll.Radius * selected.Bulk * aimCancelPixelsPerUnit;
            Vector3 body = selected.transform.position + selected.AimPull * .42f;
            Vector2 low = arenaCamera.WorldToScreenPoint(body), high = arenaCamera.WorldToScreenPoint(body + Vector3.up * (ColosseumDoll.Radius * 2.6f * selected.Bulk));
            // The body is a stand of circles from the feet up to the head.
            for (int i = 0; i <= 3; i++)
            {
                Vector2 at = Vector2.Lerp(low, high, i / 3f);
                if ((at - aimCancelCenter).magnitude < ring * 1.25f + aimCancelRadius) return true;
            }
            return false;
        }

        void KeepAimCancelClear()
        {
            if (!aimCancelVisible || !AimCancelCovered()) return;
            PlaceAimCancel(-aimCancelSide);
            if (AimCancelCovered()) PlaceAimCancel(-aimCancelSide);
            aimCancelEnteredAt = -1f;
            hud.SetAimCancelTargeted(false);
        }

        bool InsideAimCancel(Vector2 screen) =>
            aimCancelVisible && (screen - aimCancelCenter).sqrMagnitude <= aimCancelRadius * aimCancelRadius;

        bool UpdateAimCancel(Vector2 screen)
        {
            if (!InsideAimCancel(screen))
            {
                if (aimCancelEnteredAt >= 0f)
                {
                    aimCancelEnteredAt = -1f;
                    hud.SetAimCancelTargeted(false);
                }
                return false;
            }
            if (aimCancelEnteredAt < 0f)
            {
                aimCancelEnteredAt = Time.unscaledTime;
                hud.SetAimCancelTargeted(true);
            }
            float held = Time.unscaledTime - aimCancelEnteredAt;
            hud.SetAimCancelProgress(held / AimCancelHoldSeconds);
            if (held < AimCancelHoldSeconds) return false;
            CancelPull();
            return true;
        }

        void HideAimCancel()
        {
            if (!aimCancelVisible) return;
            aimCancelVisible = false;
            aimCancelEnteredAt = -1f;
            hud.HideAimCancel();
        }

        /// <summary>Where a leap of this strength comes down, kept inside the arena walls.</summary>
        static Vector3 LandingSpot(ColosseumDoll doll, Vector3 direction, float power)
        {
            Vector3 spot = doll.transform.position + direction * Mathf.Lerp(LeapNear, LeapFar, power);
            spot.x = Mathf.Clamp(spot.x, -HalfWidth + .8f, HalfWidth - .8f);
            spot.z = Mathf.Clamp(spot.z, -HalfDepth + .8f, HalfDepth - .8f);
            return spot;
        }

        /// <summary>The middle of the ground a barrage of this strength falls on, kept inside the arena walls.</summary>
        static Vector3 StarMark(Vector3 from, Vector3 direction, float power)
        {
            Vector3 mark = Kit.Flat(from) + Kit.Flat(direction).normalized * Mathf.Lerp(StarNear, StarFar, power);
            mark.x = Mathf.Clamp(mark.x, -HalfWidth + .6f, HalfWidth - .6f);
            mark.z = Mathf.Clamp(mark.z, -HalfDepth + .6f, HalfDepth - .6f);
            return mark;
        }

        /// <summary>
        /// Both the player and the opponent preview the selected hero's attack before release.
        /// </summary>
        void ShowAim(ColosseumDoll doll, Vector3 pull, Vector3 direction, float power)
        {
            Vector3 home = doll.transform.position;
            if (doll.Kind == WeaponKind.Star)
            {
                doll.LandingAim = StarMark(home, direction, power);
                fx.ShowStarfall(home, home + pull * .42f, doll.LandingAim, StarScatter, StarBurst, StarCount, StarKick, doll.Team);
                return;
            }
            // Whatever holds the heroes back (the tide) shows in the guide too: it marks where they will really get to.
            power = events.Sling(power);
            if (doll.Kind == WeaponKind.Boomerang)
            {
                LoopPath(home, direction, power, aimLoop);
                WildHint(aimLoop, home, aimHint);
                doll.LandingAim = Kit.Flat(home);
                fx.ShowLoop(home, home + pull * .42f, aimLoop, aimHint, LoopReach, doll.Team);
                return;
            }
            if (doll.Kind == WeaponKind.Bounce)
            {
                doll.LandingAim = LandingSpot(doll, direction, power);
                for (int k = 0; k < BounceHops; k++)
                    bounceSpots[k] = Vector3.Lerp(home, doll.LandingAim, BounceShare * (k + 1) / BounceHops);
                bounceSpots[BounceHops] = doll.LandingAim;
                fx.ShowBounce(home, home + pull * .42f, bounceSpots, HopRadius, PounceRadius, Mathf.Lerp(.55f, 1.15f, power), Mathf.Lerp(3.2f, 5.4f, power), doll.Team);
                return;
            }
            if (doll.Kind == WeaponKind.Slam)
            {
                doll.LandingAim = LandingSpot(doll, direction, power);
                fx.ShowLanding(home, home + pull * .42f, doll.LandingAim, SlamRadius, SlamInnerRadius, Mathf.Lerp(2.6f, 4.8f, power), doll.Team);
            }
            else if (doll.Kind == WeaponKind.Axe)
            {
                // The viking's arrow, and round the place where he would pull up the reach of his whirlwind.
                aimEyeSet = WhirlMark(doll, direction, power, out aimEye);
                fx.ShowAim(home, home + pull * .42f, direction, power, doll.Team, aimEyeSet, aimEye, WhirlReach * doll.Bulk);
            }
            else
            {
                float bowSpeed = doll.Kind == WeaponKind.Bow ? SlingSpeed(power) : 0f;
                fx.ShowAim(home, home + pull * .42f, direction, power, doll.Team, bowSpeed: bowSpeed);
            }
        }

        /// <summary>
        /// Where a viking slung this way and this hard would pull up and his whirlwind stand: against the first
        /// enemy in his way, if he would get that far. Walls and his own side are not reckoned with.
        /// </summary>
        bool WhirlMark(ColosseumDoll viking, Vector3 direction, float power, out Vector3 eye)
        {
            Vector3 from = Kit.Flat(viking.transform.position);
            eye = from;
            // About how far the sand lets him slide before he drops.
            float speed = SlingSpeed(power);
            float reach = 2f * (speed - 1.3f) - 6.4f * Mathf.Log((speed + 3.2f) / 4.5f) + 1f;
            float nearest = reach;
            bool met = false;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Team == viking.Team || !doll.Alive || doll.Lost) continue;
                Vector3 gap = Kit.Flat(doll.transform.position) - from;
                float along = Vector3.Dot(gap, direction);
                if (along <= 0f) continue;
                float room = ColosseumDoll.Radius * (viking.Bulk + doll.Bulk), off = (gap - direction * along).magnitude;
                if (off >= room) continue;
                float touch = Mathf.Max(0f, along - Mathf.Sqrt(room * room - off * off));
                if (touch >= nearest) continue;
                nearest = touch;
                met = true;
            }
            if (!met) return false;
            eye = WhirlStand(from + direction * nearest);
            return true;
        }

        /// <summary>The whirlwind stands a little clear of the walls, so that nobody in it is carried through one.</summary>
        static Vector3 WhirlStand(Vector3 spot)
        {
            return new Vector3(Mathf.Clamp(spot.x, -HalfWidth + 1.2f, HalfWidth - 1.2f), 0f, Mathf.Clamp(spot.z, -HalfDepth + 1.2f, HalfDepth - 1.2f));
        }

        void ReleasePull(Vector2 screen)
        {
            if (!pulling) return;
            if (InsideAimCancel(screen))
            {
                CancelPull();
                return;
            }
            HideAimCancel();
            ColosseumDoll doll = selected;
            pulling = false;
            fx.HideAim();
            if (doll == null) return;
            Vector3 pull = Vector3.zero;
            if (PointerOnGround(screen, out Vector3 point))
                pull = Vector3.ClampMagnitude(Kit.Flat(point - holdPoint), MaximumPull);
            float length = pull.magnitude;
            if (length < MinimumPull)
            {
                doll.CancelAim();
                selected = null;
                return;
            }
            Launch(doll, -pull / length, Mathf.InverseLerp(MinimumPull, MaximumPull, length));
            if (slingLessonActive && !lessonShotTaken)
            {
                lessonShotTaken = true;
                ColosseumRoster.CompleteShotLesson();
            }
        }

        void CancelPull()
        {
            HideAimCancel();
            if (selected != null) selected.CancelAim();
            if (aiDoll != null) aiDoll.CancelAim();
            selected = aiDoll = null;
            pulling = false;
            if (fx != null) fx.HideAim();
        }

        void ThinkForRival(float delta)
        {
            aiClock += delta;
            if (aiClock < 0f) return;
            if (aiDoll == null)
            {
                var mine = new List<ColosseumDoll>();
                var foes = new List<ColosseumDoll>();
                foreach (ColosseumDoll doll in dolls)
                {
                    if (!doll.Alive || doll.State != DollState.Idle) continue;
                    (doll.Team == turn ? mine : foes).Add(doll);
                }
                if (mine.Count == 0 || foes.Count == 0) { EndTurn(); return; }
                guidedMissTurn = Guided && !trainingMatch && PlayerFarBehind();
                if (TrainingRivalTurn)
                {
                    // The rival's heroes take the shot in turn.
                    aiDoll = mine[trainingRivalShots % mine.Count];
                    trainingPlayer = foes[0];
                    AimTrainingShot();
                }
                else PickRivalShot(mine, foes);
                aiDoll.BeginAim();
                aiClock = 0f;
            }

            // Recheck health and positions at release, after the wind-up.
            if (TrainingRivalTurn && aiClock > 1.05f) AimTrainingShot();
            float k = Mathf.Clamp01(aiClock / .75f);
            float power = k * k * (3f - 2f * k) * aiPower;
            Vector3 pull = -aiDirection * Mathf.Lerp(MinimumPull, MaximumPull, power);
            aiDoll.SetAim(pull, power);
            ShowAim(aiDoll, pull, aiDirection, power);
            if (aiClock > 1.05f)
            {
                ColosseumDoll doll = aiDoll;
                fx.HideAim();
                if (TrainingRivalTurn)
                {
                    trainingRivalShots++;
                    trainingHitLanded = false;
                    if (!trainingMiss) trainingLastTarget = trainingPlayer;
                    foreach (var defender in dolls)
                        if (defender.Team == 0 && defender.Alive && (trainingMiss || defender != trainingPlayer))
                            CallTruce(doll, defender, float.PositiveInfinity);
                }
                Launch(doll, aiDirection, aiPower);
                if (TrainingRivalTurn && !trainingMiss && doll.Kind == WeaponKind.Sword)
                {
                    // Bodies already touching at release may not generate a new collision-enter callback.
                    float contact = doll.Shape.radius + trainingPlayer.Shape.radius + 2f * Physics.defaultContactOffset;
                    Vector3 gap = trainingPlayer.Body.position - doll.Body.position;
                    if (gap.sqrMagnitude <= contact * contact)
                        WeaponHit(doll, trainingPlayer, doll.Body.position + gap * .5f, doll.Body.linearVelocity);
                }
            }
        }

        void PickRivalShot(List<ColosseumDoll> mine, List<ColosseumDoll> foes)
        {
            aiDoll = mine[Random.Range(0, mine.Count)];
            if (testLeaper)
            {
                foreach (ColosseumDoll doll in mine)
                    if (doll.Kind == WeaponKind.Slam) aiDoll = doll;
            }
            ColosseumDoll target = foes[Random.Range(0, foes.Count)];
            if (Random.value < .55f)
            {
                foreach (ColosseumDoll foe in foes)
                {
                    if (foe.Health < target.Health) target = foe;
                }
            }
            if (aiDoll.Kind == WeaponKind.Bounce)
            {
                // The porcupine cannot cross the whole arena: it goes for someone its last leap can come down on.
                Vector3 from = aiDoll.transform.position;
                List<ColosseumDoll> inReach = foes.FindAll(foe => Kit.Flat(foe.transform.position - from).magnitude < LeapFar + PounceRadius * .6f);
                if (inReach.Count > 0 && !inReach.Contains(target)) target = inReach[Random.Range(0, inReach.Count)];
            }
            if (aiDoll.Kind == WeaponKind.Hook || aiDoll.Kind == WeaponKind.Mine)
            {
                // The pirate looks for the line that strings the most enemies on his hook; the goblin, for the
                // one that takes it through the most heads to stick a bomb on.
                Vector3 from = aiDoll.transform.position;
                int most = 0;
                foreach (ColosseumDoll foe in foes)
                {
                    Vector3 line = Kit.Flat(foe.transform.position - from).normalized;
                    int strung = 0;
                    foreach (ColosseumDoll other in foes)
                    {
                        Vector3 gap = Kit.Flat(other.transform.position - from);
                        float along = Vector3.Dot(gap, line);
                        if (along > 0f && (gap - line * along).magnitude < .75f) strung++;
                    }
                    if (strung > most || (strung == most && Random.value < .3f))
                    {
                        most = strung;
                        target = foe;
                    }
                }
            }
            if (aiDoll.Kind == WeaponKind.Axe)
            {
                // The viking goes for whoever has the most company within reach of where he would pull up.
                Vector3 from = Kit.Flat(aiDoll.transform.position);
                float most = -1f;
                foreach (ColosseumDoll foe in foes)
                {
                    Vector3 line = Kit.Flat(foe.transform.position) - from;
                    float far = line.magnitude;
                    if (far < .1f) continue;
                    Vector3 eye = WhirlStand(Kit.Flat(foe.transform.position) - line / far * (ColosseumDoll.Radius * (aiDoll.Bulk + foe.Bulk)));
                    float worth = Random.Range(0f, .6f);
                    foreach (ColosseumDoll other in foes)
                    {
                        if (Kit.Flat(other.transform.position - eye).magnitude < WhirlReach * aiDoll.Bulk + ColosseumDoll.Radius * other.Bulk) worth += 1f;
                    }
                    if (worth <= most) continue;
                    most = worth;
                    target = foe;
                }
            }
            Vector3 toward = Kit.Flat(target.transform.position - aiDoll.transform.position).normalized;
            aiDirection = Quaternion.Euler(0f, Random.Range(-5f, 5f), 0f) * toward;
            // With the walls broken, a shot that would carry the hero straight out through a gap is aimed again:
            // at someone else, and in the end dead straight at them.
            for (int attempt = 0; attempt < 8 && SlingsOut(aiDoll, aiDirection, foes); attempt++)
            {
                target = foes[Random.Range(0, foes.Count)];
                toward = Kit.Flat(target.transform.position - aiDoll.transform.position).normalized;
                aiDirection = attempt < 5 ? Quaternion.Euler(0f, Random.Range(-5f, 5f), 0f) * toward : toward;
            }
            aiPower = Random.Range(.7f, 1f);
            // With a supply crate standing, a hero that is simply slung goes for it now and then instead.
            bool slung = aiDoll.Kind == WeaponKind.Axe || aiDoll.Kind == WeaponKind.Sword || aiDoll.Kind == WeaponKind.Hammer || aiDoll.Kind == WeaponKind.Mine;
            if (slung && Random.value < .4f && events.Prize(out Vector3 prize))
            {
                Vector3 toPrize = Kit.Flat(prize - aiDoll.transform.position);
                if (toPrize.magnitude > 1.2f && !SlingsOut(aiDoll, toPrize.normalized, foes)) aiDirection = toPrize.normalized;
            }
            if (aiDoll.Kind == WeaponKind.Slam || aiDoll.Kind == WeaponKind.Bounce)
            {
                // The leapers come down where they are aimed: the pull is measured to land on the target.
                float reach = Kit.Flat(target.transform.position - aiDoll.transform.position).magnitude + Random.Range(-.6f, .6f);
                aiPower = Mathf.Clamp(Mathf.InverseLerp(LeapNear, LeapFar, reach), .05f, 1f);
            }
            if (aiDoll.Kind == WeaponKind.Star)
            {
                // The witch looks for the patch of sand with the most enemies standing on it: on one of them,
                // or half way between two.
                Vector3 from = aiDoll.transform.position, best = target.transform.position;
                float most = -1f;
                foreach (ColosseumDoll one in foes)
                {
                    foreach (ColosseumDoll other in foes)
                    {
                        Vector3 mark = Kit.Flat(one.transform.position + other.transform.position) * .5f;
                        float worth = Random.Range(0f, .3f);
                        foreach (ColosseumDoll foe in foes)
                        {
                            float gap = Kit.Flat(foe.transform.position - mark).magnitude;
                            if (gap < StarScatter) worth += 1f - .5f * gap / StarScatter;
                        }
                        if (worth <= most) continue;
                        most = worth;
                        best = mark;
                    }
                }
                Vector3 reach = Kit.Flat(best - from);
                if (reach.sqrMagnitude > .01f) aiDirection = reach.normalized;
                aiPower = Mathf.Clamp(Mathf.InverseLerp(StarNear, StarFar, reach.magnitude), .02f, 1f);
            }
            if (aiDoll.Kind == WeaponKind.Boomerang)
            {
                // The gladiator tries a loop out to each enemy, a little short and a little long, and takes
                // the one that cuts the most of them.
                Vector3 from = aiDoll.transform.position;
                int most = -1;
                foreach (ColosseumDoll foe in foes)
                {
                    Vector3 gap = Kit.Flat(foe.transform.position - from);
                    float range = gap.magnitude;
                    if (range < .5f) continue;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        Vector3 way = Quaternion.Euler(0f, (attempt - 1) * 6f, 0f) * (gap / range);
                        float drawn = Mathf.Clamp(Mathf.InverseLerp(LoopNear, LoopFar, range + LoopTurn * (attempt == 1 ? .3f : 1.2f)), .02f, 1f);
                        LoopPath(from, way, drawn, aimLoop);
                        int cut = 0;
                        foreach (ColosseumDoll other in foes)
                        {
                            if (OnLoop(aimLoop, other)) cut++;
                        }
                        if (cut > most || (cut == most && Random.value < .25f))
                        {
                            most = cut;
                            aiDirection = way;
                            aiPower = drawn;
                        }
                    }
                }
            }
        }

        /// <summary>True if a hero slung this way would fly out through a gap in the wall with no enemy in the way to stop it.</summary>
        bool SlingsOut(ColosseumDoll doll, Vector3 direction, List<ColosseumDoll> foes)
        {
            if (doll.Kind == WeaponKind.Slam || doll.Kind == WeaponKind.Bounce) return false;   // the leapers always come down inside the walls
            if (doll.Kind == WeaponKind.Boomerang) return false;                                 // and the boomerang never leaves them
            if (doll.Kind == WeaponKind.Star) return false;                                      // nor is the witch slung at all
            Vector3 from = doll.transform.position;
            if (!quake.LeadsOut(from, direction, out float reach)) return false;
            // A sword carries on through whoever it hits, so an enemy in the way does not save it. An archer bounces
            // back off the first enemy it meets, and a viking pulls up against it.
            if (doll.Kind != WeaponKind.Bow && doll.Kind != WeaponKind.Axe) return true;
            foreach (ColosseumDoll foe in foes)
            {
                Vector3 gap = Kit.Flat(foe.transform.position - from);
                float along = Vector3.Dot(gap, direction);
                if (along > 0f && along < reach && (gap - direction * along).magnitude < ColosseumDoll.Radius * 2f + .1f)
                    return false;
            }
            return true;
        }

        void Launch(ColosseumDoll doll, Vector3 direction, float power)
        {
            // Nobody goes as far as they pulled for while something holds them back (the tide); a star is thrown, and flies as ever.
            if (doll.Kind != WeaponKind.Star) power = events.Sling(power);
            float speed = SlingSpeed(power);
            if (doll.Kind == WeaponKind.Slam)
                doll.Leap(LandingSpot(doll, direction, power), Mathf.Lerp(.7f, 1.15f, power), Mathf.Lerp(2.6f, 4.8f, power));
            else if (doll.Kind == WeaponKind.Bounce)
                BeginBounce(doll, direction, power);
            else if (doll.Kind == WeaponKind.Boomerang)
                BeginLoop(doll, direction, power);
            else if (doll.Kind == WeaponKind.Star)
                BeginCast(doll, direction, power);
            else
                doll.Launch(direction, speed);
            if (doll.Kind == WeaponKind.Hook)
            {
                hooker = doll;
                hookWay = Kit.Flat(direction).normalized;
                hookRoot = doll.HookRoot;
                skewered.Clear();
                slack.Clear();
                blastLeft = 0f;
            }
            launched = doll;
            whirled = false;
            boost.Watch(doll);
            bombed.Clear();
            bombClock = 0f;
            selected = aiDoll = null;
            phase = Phase.Sim;
            phaseTime = 0f;
            sfx.Play("launch");
            if (doll.Team == 0) ColosseumHaptics.Play(ColosseumHaptics.Feel.Launch, power);
            fx.Puff(doll.transform.position, 8, 3.2f);
            Shake(Mathf.Lerp(.05f, .16f, power));
            excitement = Mathf.Min(1.6f, excitement + .3f);
            if (doll.Kind == WeaponKind.Bow)
                fx.FireArrow(doll, doll.transform.position + Vector3.up * .95f + direction * ArrowLead, direction * (speed + ArrowExtraSpeed));
        }

        // ------------------------------------------------------------------ collisions and damage

        internal void OnDollsCollide(ColosseumDoll a, ColosseumDoll b, Vector3 relative, Vector3 point)
        {
            if (phase != Phase.Sim && phase != Phase.Settle)
                return;
            if (TrainingRivalTurn && (trainingMiss || trainingHitLanded)) return;
            if (launched != null && launched.State == DollState.Fly)
            {
                ColosseumDoll victim = launched == a ? b : launched == b ? a : null;
                if (victim != null && victim.Team != launched.Team && victim.Alive && !launched.OnCooldown(victim)
                    && Kit.Flat(launched.PreVelocity).magnitude > 2f)
                {
                    WeaponHit(launched, victim, point);
                    return;
                }
            }
            float impact = relative.magnitude;
            if (impact > 1.6f && !TrainingRivalTurn)
                Bump(a, b, impact, point);
        }

        void WeaponHit(ColosseumDoll attacker, ColosseumDoll victim, Vector3 point, Vector3? launchVelocity = null)
        {
            Vector3 velocity = Kit.Flat(launchVelocity ?? attacker.PreVelocity);
            float speed = velocity.magnitude;
            Vector3 direction = velocity / speed;
            Vector3 apart = Kit.Flat(victim.transform.position - attacker.transform.position);
            apart = apart.sqrMagnitude > .0001f ? apart.normalized : direction;
            Vector3 push = (apart * .6f + direction * .4f).normalized;
            if (TrainingRivalTurn && victim.Team == 0 && !SafeTrainingHit(attacker, victim, push)) return;
            int kind = (int)attacker.Kind;
            int damage = AttackDamage(attacker, Mathf.Clamp(speed / 15f, .55f, 1f));
            float lift = attacker.Body.linearVelocity.y;
            Vector3 centre = (attacker.transform.position + victim.transform.position) * .5f;
            attacker.SetCooldown(victim, .6f);
            point.y = Mathf.Max(point.y, .8f);

            switch (attacker.Kind)
            {
                case WeaponKind.Sword:
                    victim.Knock(push * Mathf.Max(8f, speed * .95f) + Vector3.up * (5f + speed * .12f));
                    attacker.Body.linearVelocity = direction * (speed * .82f) + Vector3.up * lift;
                    if (TrainingRivalTurn) trainingHitLanded = true;
                    CallTruce(attacker, victim, .3f);
                    sfx.Play("hit");
                    break;
                case WeaponKind.Axe:
                    if (!whirled && attacker == launched)
                    {
                        // He pulls up against the first one he meets, and the whirlwind begins. The blow itself is
                        // the lesser part of it, and does not send its man anywhere: the wind will.
                        damage = Mathf.Max(1, Mathf.RoundToInt(damage * WhirlBump));
                        victim.Squash(.6f);
                        BeginWhirl(attacker);
                    }
                    else
                    {
                        victim.Knock(push * Mathf.Max(5.5f, speed * .62f) + Vector3.up * 4.2f);
                        attacker.Body.linearVelocity = Vector3.Reflect(velocity, -apart) * .78f + Vector3.up * lift;
                    }
                    sfx.Play("hit");
                    break;
                case WeaponKind.Mine:
                    // The sapper's bump is a light one: the enemy only flinches where it stands, with a bomb stuck
                    // on its head, and the goblin scampers on through to the next. It keeps low, so that it does
                    // not hop clean over whoever is behind.
                    if (victim.State == DollState.Idle)
                    {
                        victim.Squash(.6f);
                        victim.Body.linearVelocity = push * 1.4f + Vector3.up * 3.4f;
                    }
                    else
                    {
                        victim.Knock(push * 2.6f + Vector3.up * 4.2f);
                    }
                    attacker.Body.linearVelocity = direction * (speed * .8f) + Vector3.up * Mathf.Min(lift, 0f);
                    CallTruce(attacker, victim, .4f);
                    StickBomb(attacker, victim);
                    sfx.Play("hit");
                    break;
                case WeaponKind.Hook:
                {
                    // The hook itself only pricks: the pirate's damage comes later, out of his gun.
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * HookHurt));
                    // He keeps his feet on the sand: a hop here would carry him clean over the next one in his way.
                    float settle = Mathf.Min(lift, 0f);
                    if (attacker == hooker && skewered.Count < HookRoom && victim.Health > damage)
                    {
                        // Caught: off its feet and onto the hook, which carries it along with barely a check. It
                        // goes on at the curve, and those aboard already are pushed down the shaft to make room.
                        for (int k = 0; k < slack.Count; k++)
                            slack[k] += hookWay * (HookSeat * attacker.Span);
                        Vector3 from = victim.transform.position;
                        from.y = hookRoot.y;
                        victim.Catch(skewered.Count);
                        skewered.Add(victim);
                        slack.Add(from - HookPlace(skewered.Count - 1));
                        attacker.Body.linearVelocity = direction * (speed * .9f) + Vector3.up * settle;
                        sfx.Play("hook");
                    }
                    else
                    {
                        // No room left on the hook, or a blow that finishes its man: he is swept aside, out of the way.
                        Vector3 across = Vector3.Cross(Vector3.up, direction);
                        if (Vector3.Dot(across, apart) < 0f) across = -across;
                        victim.Knock((across * .8f + direction * .35f).normalized * Mathf.Max(6.5f, speed * .6f) + Vector3.up * 4.6f);
                        attacker.Body.linearVelocity = direction * (speed * .85f) + Vector3.up * settle;
                        CallTruce(attacker, victim, .35f);
                        sfx.Play("hit");
                    }
                    break;
                }
                case WeaponKind.Hammer:
                    victim.Knock(push * Mathf.Max(10f, speed * 1.25f) + Vector3.up * (7f + speed * .16f));
                    attacker.Body.linearVelocity = -direction * (speed * .22f) + Vector3.up * 5f;
                    fx.Shockwave(centre, 2.9f);
                    fx.Puff(centre, 14, 5f);
                    Splash(attacker, victim, centre, 2.9f, .55f, 11.5f, "SMASH");
                    Shake(.34f);
                    sfx.Play("smash");
                    break;
                default:
                    victim.Knock(push * Mathf.Max(5.5f, speed * .62f) + Vector3.up * 4.2f);
                    attacker.Body.linearVelocity = Vector3.Reflect(velocity, -apart) * .78f + Vector3.up * lift;
                    sfx.Play("hit");
                    break;
            }

            Deal(victim, damage, HitWord[kind], Color.white, 1.12f);
            arena.ReactToHit();
            fx.Burst(point, 1.5f);
            fx.Impact(point, .78f);
            fx.Sparks(point, 12);
            Shake(.2f);
            stopLeft = .07f;
            slowLeft = Mathf.Max(slowLeft, .24f);
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .55f);
        }

        void Splash(ColosseumDoll attacker, ColosseumDoll primary, Vector3 centre, float radius, float share, float force, string word, float lift = 5.5f,
            HashSet<ColosseumDoll> once = null, bool orcSlam = false)
        {
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll == primary || doll == attacker || doll.Team == attacker.Team || !doll.Alive || attacker.OnCooldown(doll))
                    continue;
                Vector3 away = Kit.Flat(doll.transform.position - centre);
                float gap = away.magnitude;
                if (gap > radius) continue;
                if (once != null && !once.Add(doll)) continue;
                away = gap > .01f ? away / gap : Vector3.forward;
                if (TrainingRivalTurn && attacker.Team == 1 && (trainingMiss || doll != trainingPlayer || !SafeTrainingHit(attacker, doll, away))) continue;
                attacker.SetCooldown(doll, .6f);
                doll.Knock(away * (force * (1f - gap / radius * .5f)) + Vector3.up * lift);
                float hitShare = orcSlam && gap <= SlamInnerRadius ? share * SlamInnerDamageMultiplier : share;
                Deal(doll, AttackDamage(attacker, hitShare), word, new Color(1f, .91f, .69f), .95f);
            }
        }

        void Bump(ColosseumDoll a, ColosseumDoll b, float impact, Vector3 point)
        {
            bool aCarries = Kit.Flat(a.PreVelocity).sqrMagnitude >= Kit.Flat(b.PreVelocity).sqrMagnitude;
            ColosseumDoll carrier = aCarries ? a : b, target = aCarries ? b : a;
            for (int i = 0; i < 2; i++)
            {
                ColosseumDoll doll = i == 0 ? a : b;
                if (doll.Body.isKinematic) continue;
                if (doll.State == DollState.Idle || doll.State == DollState.Down || doll.State == DollState.GetUp)
                {
                    Vector3 after = doll.Body.linearVelocity;
                    doll.Body.linearVelocity = new Vector3(after.x, Mathf.Max(after.y, 2.2f + impact * .16f), after.z);
                    doll.StartTumble(after);
                }
            }

            fx.Puff(point, 3);
            if (impact > 1.2f)
                fx.Impact(point, Mathf.Lerp(.3f, .58f, Mathf.Clamp01(impact / 12f)));
            sfx.Play("bounce", Mathf.Clamp01(impact / 10f));
            // Heroes thrown about by the map itself once a turn is over (a lava vent) do not hurt whoever they land on.
            bool canChain = target.Alive && target.Team != striker && impact > 3.2f && !events.Neutral
                && !carrier.OnCooldown(target) && !target.OnCooldown(carrier);
            if (!canChain || launched == null) return;
            carrier.SetCooldown(target, .7f);
            target.SetCooldown(carrier, .7f);
            point.y = Mathf.Max(point.y, .8f);
            // An enemy knocked into a teammate carries the launched hero's attack into that collision.
            ColosseumDoll source = carrier.Team == striker ? carrier : launched;
            float chainShare = 29f / ColosseumUnitInfo.For(source.Kind, 1).Attack;
            Deal(target, AttackDamage(source, chainShare * Mathf.Clamp(impact / 11f, .5f, 1f)), "CHAIN", new Color(1f, .81f, .42f), 1.02f);
            // The goblin sticks a bomb on whoever it hurts, even tumbling into them after its run is over.
            if (carrier == launched && carrier.Kind == WeaponKind.Mine && carrier.Team == turn) StickBomb(carrier, target);
            fx.Sparks(point, 8);
            Shake(.1f);
            sfx.Play("chain");
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .4f);
        }

        void Deal(ColosseumDoll victim, int amount, string word, Color color, float scale, Color? popColor = null, float popLift = 0f, Vector3? popAt = null)
        {
            bool wasAlive = victim.Alive;
            // A blow struck by a side that has been through the power gate this turn is called out as a critical.
            bool crit = popColor == null && victim.Team != turn && SideBoosted(turn);
            int dealt = victim.TakeDamage(amount);
            if (dealt <= 0) return;
            // The hand feels it too: a crack for a blow landed, a double thump for one taken, more for a knockout.
            if (victim.Alive)
                ColosseumHaptics.Play(victim.Team == 0 ? ColosseumHaptics.Feel.Hurt : ColosseumHaptics.Feel.Hit, dealt / (victim.MaxHealth * .45f));
            else if (wasAlive)
                ColosseumHaptics.Play(victim.Team == 0 ? ColosseumHaptics.Feel.Fallen : ColosseumHaptics.Feel.Kill);
            if (slingLessonActive && turn == 0 && launched != null && victim.Team != launched.Team && !events.Neutral)
                lessonHit = true;
            string text = string.IsNullOrEmpty(word) ? "-" + dealt : word + " -" + dealt;
            if (crit) text += "!";
            hud.Pop((popAt ?? victim.transform.position) + Vector3.up * popLift, text, popColor ?? (crit ? CritYellow : DamageRed), crit ? scale * 1.4f : scale);
            if (crit) Shake(.12f);
            if (!wasAlive || victim.Alive) return;
            Vector3 flight = victim.Body.linearVelocity;
            victim.Knock(new Vector3(flight.x * 1.35f, Mathf.Max(flight.y, 0f) + 6f, flight.z * 1.35f));
            hud.Pop((popAt ?? victim.transform.position) + Vector3.forward * .9f, "K.O!", new Color(1f, .82f, .23f), 1.5f);
            sfx.Play("ko");
            sfx.Play("cheer", .8f);
            arena.ReactToKnockout();
            slowLeft = .5f;
            excitement = 1.6f;
        }

        internal void OnDollHitsArena(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative)
        {
            if (doll.Resting) return;
            // Not the arena itself, but something a map event has stood in it: a bumper, spikes, the supply crate.
            if (events.Struck(doll, point, normal, relative)) return;
            if (normal.y > .6f)
            {
                if (Mathf.Abs(relative.y) < 4.5f) return;
                doll.Squash(.5f);
                fx.Puff(point, 4);
                sfx.Play("bounce", .5f);
                return;
            }
            if (Kit.Flat(relative).magnitude < 2f) return;
            doll.Squash(.6f);
            doll.FlipSpin();
            fx.Puff(point, 6, 3f);
            fx.Impact(point, .38f);
            sfx.Play("bounce");
            Shake(.07f);
            excitement = Mathf.Min(1.6f, excitement + .08f);
        }

        /// <summary>A hero sliding or tumbling along the sand kicks up a line of dust behind it.</summary>
        internal void OnDollSkids(ColosseumDoll doll, Vector3 direction, float speed)
        {
            fx.Trail(doll.transform.position, direction, speed);
        }

        /// <summary>
        /// The way the gladiator's boomerang carries him while he still has it in hand: out along one side of a long
        /// narrow loop and round the turn at the far end of it, until it is pointing back at where he set off. The
        /// pull sets how far out the turn is; the turn is kept inside the walls, so a loop aimed at a wall bends
        /// away along it. What it does on the way home is nobody's to plan (see TickLoop).
        /// </summary>
        static void LoopPath(Vector3 from, Vector3 direction, float power, List<Vector3> points)
        {
            points.Clear();
            from.y = 0f;
            Vector3 ahead = Kit.Flat(direction);
            ahead = ahead.sqrMagnitude > .0001f ? ahead.normalized : Vector3.forward;
            Vector3 centre = from + ahead * (Mathf.Lerp(LoopNear, LoopFar, power) - LoopTurn);
            float keepX = HalfWidth - LoopTurn - .7f, keepZ = HalfDepth - LoopTurn - .7f;
            centre.x = Mathf.Clamp(centre.x, -keepX, keepX);
            centre.z = Mathf.Clamp(centre.z, -keepZ, keepZ);
            Vector3 gap = centre - from;
            float far = gap.magnitude;
            if (far < LoopTurn + .5f)
            {
                // Too close to turn round on: the turn is pushed out to arm's length.
                Vector3 outward = far > .05f ? gap / far : ahead;
                far = LoopTurn + .5f;
                centre = from + outward * far;
            }
            Vector3 axis = (centre - from) / far, side = Vector3.Cross(Vector3.up, axis);
            // He goes out along the side nearer a wall and comes home up the middle.
            if (Vector3.Dot(side, Vector3.right) * (from.x + centre.x) < 0f) side = -side;
            float lean = Mathf.Asin(Mathf.Clamp01(LoopTurn / far)), straight = far * Mathf.Cos(lean);
            Vector3 first = from + (axis * Mathf.Cos(lean) + side * Mathf.Sin(lean)) * straight;
            int steps = Mathf.Max(2, Mathf.CeilToInt(straight / .35f));
            for (int i = 0; i < steps; i++)
                points.Add(Vector3.Lerp(from, first, (float)i / steps));
            float sweep = Mathf.PI + lean * 2f;
            int turns = Mathf.Max(6, Mathf.CeilToInt(LoopTurn * sweep / .3f));
            for (int i = 0; i <= turns; i++)
            {
                float angle = -lean + sweep * i / turns;
                points.Add(centre + (side * Mathf.Cos(angle) + axis * Mathf.Sin(angle)) * LoopTurn);
            }
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                point.x = Mathf.Clamp(point.x, -HalfWidth + .55f, HalfWidth - .55f);
                point.z = Mathf.Clamp(point.z, -HalfDepth + .55f, HalfDepth - .55f);
                points[i] = point;
            }
        }

        /// <summary>
        /// For the sling guide: a guess at the first of the way home from the end of the loop, swaying wider as it
        /// goes and soon giving out.
        /// </summary>
        static void WildHint(List<Vector3> path, Vector3 home, List<Vector3> hint)
        {
            hint.Clear();
            Vector3 from = path[path.Count - 1], gap = Kit.Flat(home - from);
            float far = gap.magnitude;
            if (far < .8f) return;
            Vector3 way = gap / far, across = Vector3.Cross(Vector3.up, way);
            float length = Mathf.Min(far * .6f, 4.4f);
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / .25f));
            for (int i = 0; i <= steps; i++)
            {
                float along = length * i / steps;
                Vector3 point = from + way * along + across * (Mathf.Sin(along * 2.1f) * Mathf.Min(.8f, along * .22f));
                point.x = Mathf.Clamp(point.x, -HalfWidth + .3f, HalfWidth - .3f);
                point.z = Mathf.Clamp(point.z, -HalfDepth + .3f, HalfDepth - .3f);
                hint.Add(point);
            }
        }

        /// <summary>True if the blade would reach this hero somewhere along the loop.</summary>
        static bool OnLoop(List<Vector3> path, ColosseumDoll target)
        {
            Vector3 at = Kit.Flat(target.transform.position);
            float reach = LoopReach + ColosseumDoll.Radius * target.Bulk;
            for (int i = 0; i < path.Count; i++)
            {
                if ((at - path[i]).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        /// <summary>The gladiator steps up onto his boomerang and it carries him off round its loop.</summary>
        void BeginLoop(ColosseumDoll doll, Vector3 direction, float power)
        {
            LoopPath(doll.transform.position, direction, power, loop);
            loopMarks.Clear();
            float total = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                if (i > 0) total += (loop[i] - loop[i - 1]).magnitude;
                loopMarks.Add(total);
            }
            loopLength = total;
            // The flight is timed as if the loop came all the way home again. A long loop is flown faster than a
            // short one, but not so much faster that it takes no longer.
            loopWhole = total + Kit.Flat(loop[0] - loop[loop.Count - 1]).magnitude;
            loopSpan = Mathf.Lerp(1f, 2.05f, Mathf.InverseLerp(10f, 36f, loopWhole));
            loopTime = loopDust = 0f;
            cutAt.Clear();
            wild = false;
            loopHome = Kit.Flat(direction).normalized;
            rider = doll;
            doll.BeginRide(loopHome);
            sfx.Play("whirl");
        }

        /// <summary>
        /// Every frame of the gladiator's ride. Out and round the turn the boomerang carries him along the loop,
        /// quick away and slowing into the turn. Out of the turn it gets away from him and makes for home any way it
        /// can. All the way, whoever its blade reaches is cut and thrown aside; and in the end it throws him off.
        /// </summary>
        void TickLoop(float delta)
        {
            if (rider == null) return;
            if (!rider.Riding || rider.Lost || (phase != Phase.Sim && phase != Phase.Settle))
            {
                rider = null;
                wild = false;
                return;
            }
            loopTime += delta;
            Vector3 point, heading;
            float lift;
            if (!wild)
            {
                float gone = Mathf.Clamp01(loopTime / loopSpan);
                float along = Mathf.Min((gone + .08f * Mathf.Sin(gone * Mathf.PI * 2f)) * loopWhole, loopLength);
                int next = 1;
                while (next < loop.Count - 1 && loopMarks[next] < along) next++;
                float stretch = loopMarks[next] - loopMarks[next - 1];
                point = Vector3.Lerp(loop[next - 1], loop[next], stretch > .0001f ? (along - loopMarks[next - 1]) / stretch : 0f);
                heading = loop[next] - loop[next - 1];
                heading = heading.sqrMagnitude > .0001f ? heading.normalized : loopHome;
                // Up off the sand as he sets out.
                lift = LoopLift * Mathf.Clamp01(gone / .07f);
                if (along >= loopLength - .001f)
                    GoWild(point, heading, loopWhole / loopSpan * (1f + .16f * Mathf.PI * Mathf.Cos(gone * Mathf.PI * 2f)));
            }
            else
            {
                wildClock += delta;
                float spent = Mathf.Clamp01(wildClock / wildSpan);
                rider.RideWobble = Mathf.Clamp01(wildClock / .16f);
                // It weaves from side to side, wider all the time...
                wildWay = Quaternion.Euler(0f, WildSwing * (.55f + .75f * spent) * Mathf.Sin(wildClock * WildBeat + wildPhase) * delta, 0f) * wildWay;
                // ...every so often it lurches off line altogether...
                wildJolt -= delta;
                if (wildJolt <= 0f)
                {
                    wildJolt = Random.Range(.22f, .42f);
                    wildWay = Quaternion.Euler(0f, Random.Range(22f, 48f) * (Random.value < .5f ? -1f : 1f), 0f) * wildWay;
                    rider.Squash(.3f);
                    fx.Puff(wildAt, 5, 3.6f);
                    sfx.Play("lurch", .7f);
                    Shake(.07f);
                }
                // ...and all the while it is trying to come round to where he set off, harder as its time runs out.
                Vector3 home = Kit.Flat(loop[0] - wildAt);
                if (home.sqrMagnitude > 1f)
                    wildWay = Vector3.RotateTowards(wildWay, home.normalized, WildHome * (.8f + 1.1f * spent) * Mathf.Deg2Rad * delta, 0f);
                wildSpeed = Mathf.MoveTowards(wildSpeed, wildTop, 28f * delta);
                Vector3 step = wildAt + wildWay * (wildSpeed * delta);
                // It cannot leave the arena: it rings off the walls, gap or no gap, and goes on some other way.
                float keepX = HalfWidth - .75f, keepZ = HalfDepth - .75f;
                bool rang = false;
                if (Mathf.Abs(step.x) > keepX) { wildWay.x = -wildWay.x; step.x = Mathf.Clamp(step.x, -keepX, keepX); rang = true; }
                if (Mathf.Abs(step.z) > keepZ) { wildWay.z = -wildWay.z; step.z = Mathf.Clamp(step.z, -keepZ, keepZ); rang = true; }
                if (rang)
                {
                    wildWay = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f) * wildWay;
                    if (Mathf.Abs(step.x) > keepX - .01f && wildWay.x * step.x > 0f) wildWay.x = -wildWay.x;
                    if (Mathf.Abs(step.z) > keepZ - .01f && wildWay.z * step.z > 0f) wildWay.z = -wildWay.z;
                    wildSpeed *= .9f;
                    Vector3 struck = step + wildWay * -.4f + Vector3.up * .5f;
                    fx.Sparks(struck, 10);
                    fx.Impact(struck, .5f);
                    fx.Puff(step, 6, 3.4f);
                    rider.Squash(.45f);
                    sfx.Play("clang");
                    Shake(.15f);
                }
                wildWay = Kit.Flat(wildWay).normalized;
                wildAt = step;
                point = wildAt;
                heading = wildWay;
                // It bobs like a cork, and dips towards the sand as it gets ready to be rid of him.
                lift = LoopLift * (1f + .5f * Mathf.Sin(wildClock * 21f)) * Mathf.Clamp01((1f - spent) / .12f + .3f);
            }
            rider.Ride(point, heading, lift);

            float blade = LoopReach * rider.Growth;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Team == rider.Team || !doll.Alive || doll.Lost || doll.Caught) continue;
                if (doll.transform.position.y > 1.5f) continue;
                // One turn of the blade, one cut: it has to come round again before it bites the same hero twice.
                if (cutAt.TryGetValue(doll, out float cut) && loopTime - cut < SliceAgain) continue;
                Vector3 gap = Kit.Flat(doll.transform.position) - point;
                float reach = blade + ColosseumDoll.Radius * doll.Bulk;
                if (gap.sqrMagnitude > reach * reach) continue;
                cutAt[doll] = loopTime;
                Slice(rider, doll, point, heading);
                if (!wild) continue;
                // Out of his hands, the blade glances off whoever it hits and goes off some other way.
                Vector3 off = gap.sqrMagnitude > .01f ? -gap.normalized : -wildWay;
                if (Vector3.Dot(wildWay, off) < 0f) wildWay = Vector3.Reflect(wildWay, off);
                wildWay = Kit.Flat(Quaternion.Euler(0f, Random.Range(-24f, 24f), 0f) * wildWay).normalized;
                wildSpeed *= .9f;
                wildJolt = Mathf.Max(wildJolt, .16f);
            }

            // The wind of it lifts a line of dust all the way, so the shape of the flight hangs in the air behind him.
            loopDust -= delta;
            if (loopDust <= 0f)
            {
                loopDust = .045f;
                fx.Trail(point - heading * .6f, heading, 12f);
                fx.Puff(point - heading * .5f, 1, 1.6f);
            }
            if (wild && wildClock >= wildSpan) ThrowOff();
        }

        /// <summary>
        /// Round the turn and pointing home, the boomerang gets away from the gladiator. It picks up speed again as
        /// it would have done, and has about as long as the straight way home would have taken before it is rid of
        /// him.
        /// </summary>
        void GoWild(Vector3 point, Vector3 heading, float speed)
        {
            wild = true;
            wildAt = Kit.Flat(point);
            wildWay = Kit.Flat(heading).normalized;
            wildSpeed = Mathf.Clamp(speed, 9f, 17f);
            wildTop = Mathf.Clamp(loopWhole / loopSpan, 10f, 15f);
            wildClock = 0f;
            wildSpan = Mathf.Clamp(Kit.Flat(loop[0] - wildAt).magnitude / wildTop * Random.Range(.95f, 1.25f) + .15f, .5f, 1.5f);
            wildPhase = Random.value * Mathf.PI * 2f;
            wildJolt = Random.Range(.1f, .28f);
            hud.Pop(wildAt, "WHOA!", PowerGold, 1.1f);
            sfx.Play("lurch");
        }

        /// <summary>The blade of the boomerang catches an enemy in passing: cut, and thrown out of its way.</summary>
        void Slice(ColosseumDoll attacker, ColosseumDoll victim, Vector3 point, Vector3 heading)
        {
            Vector3 away = Kit.Flat(victim.transform.position) - point;
            away = away.sqrMagnitude > .01f ? away.normalized : Vector3.Cross(Vector3.up, heading);
            victim.Knock((away * .8f + heading * .5f).normalized * 7.6f + Vector3.up * 5f);
            Vector3 hit = victim.transform.position + Vector3.up * .8f - away * .3f;
            Deal(victim, AttackDamage(attacker), HitWord[(int)attacker.Kind], Color.white, 1.1f);
            arena.ReactToHit();
            fx.Burst(hit, 1.25f);
            fx.Impact(hit, .62f);
            fx.Sparks(hit, 10);
            sfx.Play("slice");
            Shake(.15f);
            // A flight can cut a dozen times now, so each cut holds the picture only for an instant.
            stopLeft = Mathf.Max(stopLeft, .035f);
            slowLeft = Mathf.Max(slowLeft, .1f);
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .45f);
        }

        /// <summary>
        /// The boomerang is rid of the gladiator: it pitches him off wherever it has got to, and he goes rolling,
        /// the blade in his fist again. The sand jumps where he comes down, and whoever is standing near is knocked
        /// back.
        /// </summary>
        void ThrowOff()
        {
            ColosseumDoll doll = rider;
            rider = null;
            wild = false;
            Vector3 spot = wildAt, way = wildWay;
            // Never out through a gap in the wall: pitched that way, he is turned back in.
            if (quake.LeadsOut(spot, way, out float reach) && reach < 6f) way = -way;
            doll.EndRide(spot, way);
            doll.Knock(way * Mathf.Clamp(wildSpeed * .38f, 3.5f, 5.5f) + Vector3.up * 5.2f);
            fx.Shockwave(spot, LoopLand * 1.1f);
            fx.Puff(spot, 16, 5.5f);
            fx.Impact(spot + Vector3.up * .4f, .9f);
            Splash(doll, null, spot, LoopLand * doll.Growth + ColosseumDoll.Radius, LoopLandHurt, 9.5f, "CRASH");
            sfx.Play("thud");
            Shake(.26f);
            excitement = Mathf.Min(1.6f, excitement + .3f);
        }

        /// <summary>
        /// The witch is let go: she does not move from where she stands, but throws up her wand and starts to bring
        /// the stars down on the ground she was aimed at.
        /// </summary>
        void BeginCast(ColosseumDoll doll, Vector3 direction, float power)
        {
            caster = doll;
            castMark = StarMark(doll.transform.position, direction, power);
            castFrom = castTo = Kit.Flat(doll.transform.position);
            castLeft = StarCount;
            castClock = StarWind;
            castHop = 1f;
            doll.BeginCast(castMark - castTo);
            hud.Pop(castTo, "STARFALL!", PowerGold, 1.15f);
            sfx.Play("cackle");
        }

        /// <summary>
        /// Every frame of the witch's barrage: a star each beat, and after each the kick of it carrying her off
        /// backwards through the air. When the last has gone and she has come down, she is back on her feet; the
        /// turn waits for the stars still falling.
        /// </summary>
        void TickCast(float delta)
        {
            if (caster == null) return;
            if (!caster.Casting || caster.Lost || (phase != Phase.Sim && phase != Phase.Settle))
            {
                if (caster.Casting) caster.EndCast();
                caster = null;
                return;
            }
            castClock -= delta;
            // Her wand is alight all through.
            if ((castGlint -= delta) <= 0f)
            {
                castGlint = .055f;
                stars.Shed(caster.WandTip + Random.insideUnitSphere * .16f, Random.insideUnitSphere * 1.1f + Vector3.up * 1.4f, Random.Range(.1f, .2f), Random.Range(.25f, .45f));
            }
            Vector3 facing = Kit.Flat(castMark - castTo);
            if (castHop < 1f)
            {
                // Thrown hard, and slowing as she comes down.
                castHop = Mathf.Min(1f, castHop + delta / .29f);
                float arc = 4f * castHop * (1f - castHop), gone = 1f - (1f - castHop) * (1f - castHop);
                caster.Cast(Vector3.Lerp(castFrom, castTo, gone), facing, castPeak * arc, 24f * arc);
                if (castHop < 1f) return;
                // Down again, in a puff of sand.
                caster.Squash(.4f);
                fx.Puff(castTo, 4, 2.4f);
            }
            if (castClock > 0f) return;
            if (castLeft > 0)
            {
                LooseStar();
                return;
            }
            caster.EndCast();
            caster = null;
        }

        /// <summary>
        /// One star: off the end of her wand, far up over the arena and down again somewhere near the mark, by a
        /// way of its own; and her thrown back from it, off whatever wall is behind her.
        /// </summary>
        void LooseStar()
        {
            int number = StarCount - castLeft;
            castLeft--;
            Vector3 here = castTo;
            // Anywhere in the ring round the mark is as likely as anywhere else: she is not aiming, she is pointing.
            Vector2 stray = Random.insideUnitCircle * StarScatter;
            Vector3 fall = castMark + new Vector3(stray.x, 0f, stray.y);
            fall.x = Mathf.Clamp(fall.x, -HalfWidth + .5f, HalfWidth - .5f);
            fall.z = Mathf.Clamp(fall.z, -HalfDepth + .5f, HalfDepth - .5f);
            Vector3 toward = Kit.Flat(fall - here);
            toward = toward.sqrMagnitude > .01f ? toward.normalized : caster.Facing;
            // No two go the same way: higher or lower, and bowed out to one side or the other. All of them go up
            // out of sight.
            Vector3 bow = Vector3.Cross(Vector3.up, toward) * Random.Range(-3f, 3f);
            caster.Flick();
            Vector3 tip = caster.WandTip;
            stars.Fire(caster, tip, fall, StarFlight * Random.Range(.92f, 1.12f), Random.Range(19f, 25f), bow, StarBurst, number);
            fx.Sparks(tip, 8);
            fx.Impact(tip, .55f);
            // The flash of it going: a spray of glitter thrown up after it.
            for (int i = 0; i < 9; i++)
                stars.Shed(tip + Random.insideUnitSphere * .2f, new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(5f, 11f), Random.Range(-1.6f, 1.6f)), Random.Range(.14f, .28f), Random.Range(.3f, .55f));
            sfx.Play("cast");
            Shake(.07f);

            // The kick: back the way the star went, give or take a good deal.
            Vector3 thrown = here + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * -toward * (StarKick * Random.Range(.75f, 1.3f));
            // She comes off a wall the way a ball does, gap or no gap: the kick never carries her out of the arena.
            float keepX = HalfWidth - .7f, keepZ = HalfDepth - .7f;
            if (Mathf.Abs(thrown.x) > keepX) thrown.x = Mathf.Sign(thrown.x) * (2f * keepX - Mathf.Abs(thrown.x));
            if (Mathf.Abs(thrown.z) > keepZ) thrown.z = Mathf.Sign(thrown.z) * (2f * keepZ - Mathf.Abs(thrown.z));
            // She may go over people's heads on the way, but she does not come down on top of anybody.
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll == caster || doll.Lost) continue;
                Vector3 gap = thrown - Kit.Flat(doll.transform.position);
                float room = ColosseumDoll.Radius * (doll.Bulk + caster.Bulk) + .08f;
                if (gap.sqrMagnitude >= room * room) continue;
                thrown = Kit.Flat(doll.transform.position) + (gap.sqrMagnitude > .001f ? gap.normalized : -toward) * room;
            }
            thrown.x = Mathf.Clamp(thrown.x, -keepX, keepX);
            thrown.z = Mathf.Clamp(thrown.z, -keepZ, keepZ);
            castFrom = here;
            castTo = thrown;
            castHop = 0f;
            castPeak = Random.Range(.6f, 1f);
            castClock = StarBeat;
        }

        /// <summary>A star comes down: a burst of light and sand, and every enemy near it hurt and thrown back.</summary>
        void OnStarLands(ColosseumDoll owner, Vector3 point, int number)
        {
            fx.Shockwave(point, StarBurst * 1.2f);
            fx.Impact(point + Vector3.up * .35f, 1.05f);
            fx.Sparks(point + Vector3.up * .3f, 12);
            fx.Puff(point, 9, 4.4f);
            sfx.Play("starhit");
            Shake(.2f);
            if (owner == null || (phase != Phase.Sim && phase != Phase.Settle)) return;
            events.Blast(point, StarBurst, owner.Team);
            int damage = AttackDamage(owner);
            bool struck = false;
            foreach (ColosseumDoll doll in dolls)
            {
                if (!doll.Alive || doll.Lost || doll.Team == owner.Team || doll.Body.isKinematic) continue;
                Vector3 gap = Kit.Flat(doll.transform.position - point);
                float far = gap.magnitude;
                if (far > StarBurst + ColosseumDoll.Radius * doll.Bulk) continue;
                Vector3 away = far > .05f ? gap / far : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                doll.Knock(away * Mathf.Lerp(6.8f, 3.6f, Mathf.Clamp01(far / StarBurst)) + Vector3.up * 4.8f);
                Deal(doll, damage, HitWord[(int)WeaponKind.Star], Color.white, 1.05f);
                struck = true;
            }
            if (!struck) return;
            arena.ReactToHit();
            stopLeft = Mathf.Max(stopLeft, .03f);
            slowLeft = Mathf.Max(slowLeft, .08f);
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .35f);
        }

        // ------------------------------------------------------------------ the viking's whirlwind

        /// <summary>The viking has run into his first enemy: he pulls up there, and the whirlwind begins.</summary>
        void BeginWhirl(ColosseumDoll viking)
        {
            whirler = viking;
            whirled = true;
            whirlBurst = false;
            whirlClock = 0f;
            whirlSeats = 0;
            whirlReach = WhirlReach * viking.Bulk;
            whirlFrom = Kit.Flat(viking.transform.position);
            whirlEye = WhirlStand(whirlFrom);
            taken.Clear();
            viking.BeginWhirl();
            // However long he took to get here, the turn is not hurried on while it lasts.
            phaseTime = Mathf.Min(phaseTime, 1.5f);
            twister.Begin(whirlEye, whirlReach, viking.Team);
            fx.Puff(whirlEye, 10, 4.5f);
            sfx.Play("twister");
            Shake(.16f);
            excitement = Mathf.Min(1.6f, excitement + .4f);
        }

        /// <summary>
        /// Every frame of the viking's whirlwind. He turns faster and faster on the spot. Whoever of the other side
        /// is within reach is taken up, the nearest first, and goes round and in and up; at the end of it they are
        /// all thrown out of the top, and he runs down.
        /// </summary>
        void TickWhirl(float delta)
        {
            // Those it threw out are over the top and on their way down: it can be heard before it is seen.
            if (skyFall > 0f && (skyFall -= delta) <= 0f && (phase == Phase.Sim || phase == Phase.Settle)) sfx.Play("fall", .85f);
            if (whirler == null) return;
            if (!whirler.Alive || whirler.Lost || !whirler.Whirling || (phase != Phase.Sim && phase != Phase.Settle))
            {
                DropWhirl();
                return;
            }
            whirlClock += delta;
            float rate = whirlBurst
                ? Mathf.Lerp(1500f, 240f, Mathf.Clamp01((whirlClock - WhirlSpan) / WhirlFade))
                : Mathf.Lerp(760f, 1500f, Mathf.Clamp01(whirlClock / (WhirlSpan * .7f)));
            float lift = whirlBurst ? 0f : .3f * whirler.Span * Mathf.Clamp01(whirlClock / .3f);
            whirler.Whirl(Vector3.Lerp(whirlFrom, whirlEye, Mathf.Clamp01(whirlClock / .18f)), rate, lift);
            if (whirlBurst)
            {
                if (whirlClock >= WhirlSpan + WhirlFade) EndWhirl();
                return;
            }
            // The ground hums under it, harder as it winds up, and sand is lifted all round its foot.
            Shake(.03f + .05f * Mathf.Clamp01(whirlClock / WhirlSpan));
            whirlDust -= delta;
            if (whirlDust <= 0f)
            {
                whirlDust = .05f;
                Vector2 off = Random.insideUnitCircle.normalized * (Random.Range(.5f, 1.5f) * whirler.Bulk);
                fx.Puff(whirlEye + new Vector3(off.x, 0f, off.y), 1, 2.4f);
            }

            // Whoever is within reach and not marked yet is marked: the nearer, the sooner it goes. Nobody is
            // marked in the second half of it, to be thrown out before it has gone round once.
            if (whirlClock < WhirlSpan * .5f)
            {
                foreach (ColosseumDoll doll in dolls)
                {
                    if (doll.Team == whirler.Team || !doll.Alive || doll.Lost || doll.Caught || doll.Body.isKinematic) continue;
                    float far = Kit.Flat(doll.transform.position - whirlEye).magnitude;
                    if (far > whirlReach + ColosseumDoll.Radius * doll.Bulk || taken.Exists(one => one.Doll == doll)) continue;
                    taken.Add(new Taken { Doll = doll, Wait = .04f + .2f * Mathf.Clamp01(far / whirlReach) });
                    doll.Rattle(.6f);
                }
            }

            float brink = Mathf.Clamp01((whirlClock - (WhirlSpan - .28f)) / .28f);   // the last moment: drawn right in, and up
            for (int i = 0; i < taken.Count; i++)
            {
                Taken one = taken[i];
                ColosseumDoll doll = one.Doll;
                if (!one.Up)
                {
                    one.Wait -= delta;
                    if (one.Wait > 0f) continue;
                    if (doll == null || !doll.Alive || doll.Lost || doll.Caught || doll.Body.isKinematic)
                    {
                        taken.RemoveAt(i--);
                        continue;
                    }
                    // Off its feet.
                    Vector3 gap = doll.transform.position - whirlEye;
                    one.Up = true;
                    one.Age = 0f;
                    one.Reach = Kit.Flat(gap).magnitude;
                    one.Turn = Mathf.Atan2(gap.x, gap.z) * Mathf.Rad2Deg;
                    one.Height = Mathf.Max(0f, gap.y);
                    one.Seat = whirlSeats++;
                    fx.Puff(doll.transform.position, 6, 3f);
                    doll.Sweep();
                }
                one.Age += delta;
                // Round and in and up, quickly; after that it only goes round, until the last moment draws it in.
                float k = Mathf.Clamp01(one.Age / .5f), eased = k * k * (3f - 2f * k);
                float ring = (1.3f + .18f * one.Seat) * whirler.Bulk + ColosseumDoll.Radius * 2f * (doll.Bulk - 1f);
                float shelf = .7f + .5f * one.Seat;
                one.Turn += Mathf.Lerp(320f, 640f, eased) * (1f + .55f * brink) * delta;
                float reach = Mathf.Lerp(one.Reach, ring, eased) * (1f - .35f * brink);
                float height = Mathf.Lerp(one.Height, shelf, eased) + brink * brink * 1.5f;
                Vector3 place = whirlEye + Quaternion.Euler(0f, one.Turn, 0f) * Vector3.forward * reach + Vector3.up * height;
                place.x = Mathf.Clamp(place.x, -HalfWidth + .45f, HalfWidth - .45f);
                place.z = Mathf.Clamp(place.z, -HalfDepth + .45f, HalfDepth - .45f);
                doll.Carry(place);
            }
            // With nobody in it and nobody coming, it blows itself out early.
            if (whirlClock >= WhirlSpan || (taken.Count == 0 && whirlClock > .5f)) BurstWhirl();
        }

        /// <summary>The whirlwind throws out everyone it has: hurt, and up into the sky.</summary>
        void BurstWhirl()
        {
            ColosseumDoll viking = whirler;
            Vector3 eye = whirlEye;
            whirlBurst = true;
            whirlClock = WhirlSpan;   // he is as long running down whenever it burst
            int thrown = 0;
            foreach (Taken one in taken)
            {
                if (one.Up) thrown++;
            }
            if (thrown == 0)
            {
                // There was nobody in it: it only dies away.
                twister.Stop();
                taken.Clear();
                fx.Puff(eye, 12, 4.5f);
                return;
            }
            twister.Burst();
            fx.Shockwave(eye, whirlReach * 1.15f);
            fx.Burst(eye + Vector3.up * 1.4f, 2.4f);
            fx.Puff(eye, 24, 7f);
            fx.Sparks(eye + Vector3.up * 1.2f, 18);
            events.Blast(eye, whirlReach * .7f, viking.Team);
            hud.Pop(eye + Vector3.forward * .7f, "WHIRLWIND!", new Color(1f, .82f, .23f), 1.5f);
            sfx.Play("skyward");
            sfx.Play("smash", .7f);
            sfx.Play("cheer", .6f);
            arena.ReactToHit();
            Shake(.42f);
            EventPunch(.03f);

            int damage = AttackDamage(viking, WhirlHurt), k = 0, back = 0;
            float bearing = Random.Range(0f, 360f);
            // They are all in the one place, so what each took is called out in a column over the word,
            // or under him where the wall behind leaves no room for one.
            const float PopStep = 1.35f;
            bool under = eye.z + 2.1f + (thrown - 1) * PopStep > HalfDepth + .6f;
            landings.Clear();
            foreach (Taken one in taken)
            {
                if (!one.Up) continue;
                ColosseumDoll doll = one.Doll;
                Vector3 from = doll.transform.position;
                Vector3 called = eye + Vector3.forward * (under ? -(2f + k * PopStep) : 2.1f + k * PopStep);
                Deal(doll, damage, "WHIRL", Color.white, 1.15f, null, 0f, called);
                fx.Sparks(from + Vector3.up * .5f, 6);
                float seconds = SkyTime + back * SkyBeat;
                // One it has finished only goes up, leaning a little the way it was thrown.
                Vector3 landing = doll.Alive ? SkyLanding(doll, bearing + k * 360f / thrown)
                    : Kit.Flat(from) + Quaternion.Euler(0f, bearing + k * 360f / thrown, 0f) * Vector3.forward * 1.5f;
                doll.Hurl(landing, seconds, Mathf.Max(SkyLine(from), SkyLine(landing)) + SkyOver);
                if (doll.Alive)
                {
                    twister.Mark(landing, seconds, ColosseumDoll.Radius * doll.Bulk * 2.2f, doll.Team);
                    back++;
                }
                k++;
            }
            taken.Clear();
            if (back > 0) skyFall = SkyTime * ColosseumDoll.HurlClimb;
            stopLeft = .08f;
            slowLeft = Mathf.Max(slowLeft, .32f);
            cheerLeft = 1.1f;
            excitement = 1.6f;
        }

        /// <summary>How high over a spot something has to be to be at the top edge of the view.</summary>
        float SkyLine(Vector3 spot)
        {
            Vector3 up = arenaCamera.transform.up;
            return Mathf.Max(4f, (battleViewSize - Vector3.Dot(Kit.Flat(spot) - cameraRest, up)) / Mathf.Max(.2f, up.y));
        }

        /// <summary>
        /// Where one thrown out of the whirlwind comes down: out from it the way given, give or take, inside the
        /// walls and well clear of any gap in them, and not on top of anyone or of where another is coming down.
        /// </summary>
        Vector3 SkyLanding(ColosseumDoll doll, float bearing)
        {
            float keepX = HalfWidth - 1.6f, keepZ = HalfDepth - 1.1f, roomiest = float.NegativeInfinity;
            Vector3 best = whirlEye;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 spot = whirlEye + Quaternion.Euler(0f, bearing + Random.Range(-28f, 28f) + attempt * 47f, 0f) * Vector3.forward * Random.Range(SkyNear, SkyFar);
                spot.x = Mathf.Clamp(spot.x, -keepX, keepX);
                spot.z = Mathf.Clamp(spot.z, -keepZ, keepZ);
                // How much room there is round it: from the whirlwind itself, from everyone on the sand, from the others coming down.
                float room = Kit.Flat(spot - whirlEye).magnitude - 1.2f;
                foreach (ColosseumDoll other in dolls)
                {
                    if (other == doll || other.Lost || other.Swept) continue;
                    room = Mathf.Min(room, Kit.Flat(other.transform.position - spot).magnitude);
                }
                foreach (Vector3 spoken in landings) room = Mathf.Min(room, (spoken - spot).magnitude);
                if (room > roomiest)
                {
                    roomiest = room;
                    best = spot;
                }
                if (room >= 1.3f) break;
            }
            landings.Add(best);
            return best;
        }

        /// <summary>The whirlwind has blown itself out: the viking is let down where he stands, with his head still going round.</summary>
        void EndWhirl()
        {
            ColosseumDoll viking = whirler;
            whirler = null;
            whirlBurst = false;
            twister.Stop();
            if (viking != null) viking.EndWhirl();
        }

        /// <summary>
        /// Cut short: the viking is gone, the set is over, or a new turn has begun. Whoever it had hold of is set
        /// down on the sand, and so is anyone it had already thrown at the sky.
        /// </summary>
        void DropWhirl()
        {
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll != null && doll.Swept) doll.SetDown();
            }
            taken.Clear();
            skyFall = 0f;
            if (twister != null) twister.Clear();
            ColosseumDoll viking = whirler;
            whirler = null;
            whirlBurst = false;
            if (viking != null) viking.EndWhirl();
        }

        /// <summary>One thrown up by the whirlwind is back: it hits the sand hard enough to mark it.</summary>
        internal void OnDollComesDown(ColosseumDoll doll, Vector3 point)
        {
            if (phase != Phase.Sim && phase != Phase.Settle) return;
            point.y = 0f;
            float girth = ColosseumDoll.Radius * doll.Bulk;
            fx.Crater(point, girth * 2.3f);
            fx.Shockwave(point, girth * 4.2f);
            fx.Puff(point, 14, 5f);
            fx.Impact(point + Vector3.up * .4f, .9f);
            fx.Sparks(point + Vector3.up * .3f, 8);
            doll.Squash(.7f);
            sfx.Play("thud");
            Shake(.24f);
            cheerLeft = Mathf.Max(cheerLeft, .8f);
            excitement = Mathf.Min(1.6f, excitement + .25f);
        }

        /// <summary>One the whirlwind finished has gone up for good: a twinkle at the top of the view, over where it went.</summary>
        internal void OnDollGoneSkyward(ColosseumDoll doll, Vector3 at)
        {
            if (phase != Phase.Sim && phase != Phase.Settle) return;
            Vector3 seen = arenaCamera.WorldToViewportPoint(at);
            twister.Twinkle(arenaCamera.ViewportToWorldPoint(new Vector3(Mathf.Clamp(seen.x, .12f, .88f), .9f, 12f)));
            sfx.Play("twinkle", .9f);
        }

        /// <summary>True while this pirate has anyone hanging on his hook.</summary>
        internal bool HookLoaded(ColosseumDoll doll) { return doll == hooker && skewered.Count > 0; }

        /// <summary>The pirate has come to a stop with his catch still hooked: the gun comes up, and the shot follows.</summary>
        internal void OnHookStops(ColosseumDoll doll)
        {
            if (doll != hooker) return;
            blastLeft = BlastWait;
            sfx.Play("cock");
        }

        /// <summary>Where on the hook the one caught k-th belongs: the latest at the curve, the rest in a row back towards his hand.</summary>
        Vector3 HookPlace(int k)
        {
            return hookRoot + hookWay * ((ColosseumDoll.HookShaft - .14f - (skewered.Count - 1 - k) * HookSeat) * hooker.Span);
        }

        /// <summary>
        /// Every frame of the pirate's turn. While he is still flying the hook catches whoever its far end sweeps
        /// over; whoever is on it hangs in a row along its shaft; and once he has pulled up, the wait for his shot
        /// runs down.
        /// </summary>
        void CarryCatch(float delta)
        {
            if (hooker == null) return;
            if (!hooker.Alive || hooker.Lost || (phase != Phase.Sim && phase != Phase.Settle))
            {
                DropCatch();
                return;
            }
            // The row hangs level along the hook. Should the pirate be sent tumbling, hook and all, it comes round
            // after him at its own pace and at a steady height instead of being flung about with his arm.
            float span = hooker.Span;
            Vector3 flat = Kit.Flat(hooker.HookAlong);
            if (flat.sqrMagnitude > .16f)
                hookWay = Vector3.RotateTowards(hookWay, flat.normalized, delta * (hooker.State == DollState.Tumble ? 5f : 14f), 0f);
            // The hook was last placed a frame ago; lead it by the pirate's speed so the catch does not trail behind.
            hookRoot = hooker.HookRoot + (hooker.Body.isKinematic ? Vector3.zero : Kit.Flat(hooker.Body.linearVelocity) * delta);
            hookRoot.y = Mathf.Clamp(hookRoot.y, .66f * span, 1.2f * span);

            if (hooker.State == DollState.Fly && Kit.Flat(hooker.PreVelocity).magnitude > 2f)
            {
                Vector3 middle = Kit.Flat(hooker.transform.position);
                foreach (ColosseumDoll doll in dolls)
                {
                    if (doll.Team == hooker.Team || !doll.Alive || doll.Lost || doll.Caught || doll.Body.isKinematic || hooker.OnCooldown(doll))
                        continue;
                    if (doll.transform.position.y > 1.1f) continue;
                    Vector3 gap = Kit.Flat(doll.transform.position) - middle;
                    float ahead = Vector3.Dot(gap, hookWay);
                    if (ahead < HookNear * span || ahead > HookFar * span) continue;
                    if ((gap - hookWay * ahead).magnitude > HookWide * span + ColosseumDoll.Radius * doll.Bulk) continue;
                    WeaponHit(hooker, doll, doll.transform.position + Vector3.up * .8f);
                }
            }

            if (skewered.Count == 0) return;
            if (hooker.State == DollState.Fly && !hooker.Body.isKinematic)
            {
                Vector3 going = hooker.Body.linearVelocity, level = Kit.Flat(going);
                float pace = level.magnitude;
                if (pace > .01f)
                    hooker.Body.linearVelocity = level / pace * Mathf.Max(0f, pace - HookDrag * skewered.Count * delta) + Vector3.up * going.y;
            }
            float close = 1f - Mathf.Exp(-13f * delta);
            for (int k = 0; k < skewered.Count; k++)
            {
                slack[k] = Vector3.Lerp(slack[k], Vector3.zero, close);
                // The hook can reach past a wall he has pulled up against; whoever is on it stays this side of it.
                Vector3 place = HookPlace(k) + slack[k];
                place.x = Mathf.Clamp(place.x, -HalfWidth + .45f, HalfWidth - .45f);
                place.z = Mathf.Clamp(place.z, -HalfDepth + .45f, HalfDepth - .45f);
                skewered[k].Hang(place, hookWay);
            }
            if (blastLeft > 0f)
            {
                blastLeft -= delta;
                if (blastLeft <= 0f) Blast();
            }
            else if (phaseTime > 7f)
            {
                // Should he somehow never pull up, the shot is not lost.
                blastLeft = .05f;
            }
        }

        /// <summary>
        /// The pirate fires into his catch. Each of them takes the larger share of his damage and is blown off
        /// the hook down a lane of its own, the lanes spread across a wide fan in front of the gun and dealt out at
        /// random; the kick of the gun sits the pirate down.
        /// </summary>
        void Blast()
        {
            ColosseumDoll pirate = hooker;
            Vector3 along = hookWay;
            Vector3 muzzle = pirate.Muzzle;
            Vector3 heap = HookPlace(skewered.Count / 2);
            fx.Burst(muzzle + along * .35f, 1.9f);
            fx.Burst(heap, 2.5f);
            fx.Impact(heap, 1.2f);
            fx.Shockwave(Kit.Flat(heap), 2.7f);
            fx.Sparks(muzzle + along * .3f, 30);
            fx.Sparks(heap, 16);
            fx.Puff(muzzle, 18, 5.5f);
            fx.Puff(heap, 12, 6.5f);
            sfx.Play("blast");
            Shake(.45f);
            int count = skewered.Count, first = Random.Range(0, count);
            int damage = AttackDamage(pirate, BlastHurt);
            float fan = count > 2 ? 72f : 44f;
            for (int k = 0; k < count; k++)
            {
                ColosseumDoll victim = skewered[k];
                victim.LetGo();
                float lane = count == 1 ? Random.Range(-.6f, .6f) : (k + first) % count / (count - 1f) * 2f - 1f;
                Vector3 way = Quaternion.Euler(0f, lane * fan + Random.Range(-14f, 14f), 0f) * along;
                victim.Knock(way * Random.Range(12.5f, 17.5f) + Vector3.up * Random.Range(5.5f, 9f));
                // The figures are called out one above the other, not all on the one spot.
                Deal(victim, damage, "BLAST", Color.white, 1.15f, null, k * 1.6f);
                fx.Sparks(victim.transform.position + Vector3.up * .8f, 6);
            }
            // They leave the hook from all but the same spot: they do not knock lumps off each other, or off the
            // pirate, on the way out. Whoever they land on afterwards is another matter.
            for (int a = 0; a < count; a++)
            {
                CallTruce(pirate, skewered[a], .45f);
                for (int b = a + 1; b < count; b++)
                    CallTruce(skewered[a], skewered[b], .45f);
            }
            skewered.Clear();
            slack.Clear();
            hooker = null;
            blastLeft = 0f;
            pirate.LowerGun();
            pirate.Knock(-along * 3.4f + Vector3.up * 3.8f);
            arena.ReactToHit();
            stopLeft = .09f;
            slowLeft = Mathf.Max(slowLeft, .4f);
            cheerLeft = 1.1f;
            excitement = 1.6f;
        }

        /// <summary>
        /// Everyone off the hook without a shot fired: the pirate has gone down, or out through a gap in the wall.
        /// Nobody is left outside the walls by it.
        /// </summary>
        void DropCatch()
        {
            for (int k = 0; k < skewered.Count; k++)
            {
                ColosseumDoll victim = skewered[k];
                if (victim == null || !victim.Caught) continue;
                Vector3 at = victim.transform.position;
                at.x = Mathf.Clamp(at.x, -HalfWidth + .7f, HalfWidth - .7f);
                at.z = Mathf.Clamp(at.z, -HalfDepth + .7f, HalfDepth - .7f);
                victim.Hang(new Vector3(at.x, .75f, at.z), hookWay);
                victim.LetGo();
                // A nudge back towards the middle of the court, away from whatever gap the pirate went out by.
                Vector3 inward = Kit.Flat(-at);
                inward = inward.sqrMagnitude > .01f ? inward.normalized : Vector3.forward;
                victim.Knock(inward * Random.Range(1f, 2.2f) + new Vector3(Random.Range(-.5f, .5f), 2.6f, Random.Range(-.5f, .5f)));
                for (int other = 0; other < k; other++)
                {
                    if (skewered[other] != null) CallTruce(skewered[other], victim, .45f);
                }
            }
            skewered.Clear();
            slack.Clear();
            if (hooker != null) hooker.LowerGun();
            hooker = null;
            blastLeft = 0f;
        }

        /// <summary>
        /// The porcupine's run: small hops along the way, pricking whoever each one comes down beside, and then one
        /// great leap onto the spot it was aimed at.
        /// </summary>
        void BeginBounce(ColosseumDoll doll, Vector3 direction, float power)
        {
            bounceFrom = doll.transform.position;
            bounceTo = LandingSpot(doll, direction, power);
            bouncePower = power;
            bounceStep = 0;
            pricked.Clear();
            NextBounce(doll);
        }

        void NextBounce(ColosseumDoll doll)
        {
            if (bounceStep < BounceHops)
            {
                bounceStep++;
                doll.Leap(Vector3.Lerp(bounceFrom, bounceTo, BounceShare * bounceStep / BounceHops), Mathf.Lerp(.22f, .32f, bouncePower),
                    Mathf.Lerp(.55f, 1.15f, bouncePower), true);
            }
            else
            {
                doll.Leap(bounceTo, Mathf.Lerp(.5f, .78f, bouncePower), Mathf.Lerp(3.2f, 5.4f, bouncePower));
            }
        }

        /// <summary>The porcupine has touched down between hops: whoever is beside it is pricked, and up it goes again.</summary>
        internal void OnDollHops(ColosseumDoll doll, Vector3 point)
        {
            point.y = 0f;
            fx.Shockwave(point, HopRadius * 1.1f);
            fx.Puff(point, 7, 3f);
            // However the hops fall, each enemy is pricked once in an attack; the last leap is what hurts.
            Splash(doll, null, point, HopRadius + ColosseumDoll.Radius, HopHurt, 3f, "PRICK", 3.2f, pricked);
            events.Blast(point, HopRadius, doll.Team);
            sfx.Play("bounce", .8f);
            Shake(.12f);
            NextBounce(doll);
        }

        /// <summary>
        /// A leaper has come down: a crater, a ring of dust, and every enemy in the circle is hurt and thrown clear.
        /// The orc deals bonus damage to enemies centred within the inner aiming ring.
        /// </summary>
        internal void OnDollSlams(ColosseumDoll doll, Vector3 point)
        {
            point.y = 0f;
            bool pounce = doll.Kind == WeaponKind.Bounce;
            float reach = pounce ? PounceRadius : SlamRadius;
            fx.Crater(point, reach);
            fx.Shockwave(point, reach * 1.25f);
            fx.Puff(point, 26, 7f);
            fx.Impact(point + Vector3.up * .5f, 1.6f);
            fx.Sparks(point + Vector3.up * .3f, pounce ? 22 : 16);
            Splash(doll, null, point, reach + ColosseumDoll.Radius, 1f, pounce ? 13f : 15f, HitWord[(int)doll.Kind], orcSlam: doll.Kind == WeaponKind.Slam);
            events.Blast(point, reach, doll.Team);
            hud.Pop(point + Vector3.forward * .6f, pounce ? "POUNCE!" : "SLAM!", new Color(1f, .82f, .23f), 1.5f);
            sfx.Play("smash");
            sfx.Play("cheer", .6f);
            arena.ReactToHit();
            Shake(.5f);
            stopLeft = .08f;
            slowLeft = Mathf.Max(slowLeft, .3f);
            cheerLeft = 1.1f;
            excitement = Mathf.Min(1.6f, excitement + .6f);
        }

        /// <summary>True where a stretch of the side wall has come down (see ColosseumQuake).</summary>
        internal bool GapAt(Vector3 point) { return quake != null && quake.GapAt(point); }

        // What the map events ask of the game (see ColosseumEvents).

        /// <summary>The side whose turn it is.</summary>
        internal int TurnSide { get { return turn; } }

        /// <summary>A map event hurts a hero: the damage, the word that goes up with it, and its colour.</summary>
        internal void EventHurt(ColosseumDoll victim, int amount, string word, Color color)
        {
            if (amount <= 0) return;
            Deal(victim, amount, word, Color.white, 1.05f, color);
            arena.ReactToHit();
        }

        internal void EventShake(float amount, float slow)
        {
            Shake(amount);
            if (slow > 0f) slowLeft = Mathf.Max(slowLeft, slow);
        }

        /// <summary>The camera the arena is seen through, for what an event draws across the whole view.</summary>
        internal Camera Lens { get { return arenaCamera; } }

        /// <summary>Something heavy has landed: the view is drawn in by this much (.05 is a twentieth), and eases back.</summary>
        internal void EventPunch(float amount) { zoomKick = Mathf.Max(zoomKick, Mathf.Min(.08f, amount)); }

        internal void EventThrill(float amount)
        {
            excitement = Mathf.Min(1.6f, excitement + amount);
            cheerLeft = Mathf.Max(cheerLeft, .8f);
        }

        /// <summary>A hero has come out of a portal: the power gate must not take the jump for a run across the court.</summary>
        internal void EventWarped(ColosseumDoll doll) { boost.Watch(doll); }

        /// <summary>A hero's shield has just taken this much of a blow for it.</summary>
        internal void OnShieldHolds(ColosseumDoll doll, int held)
        {
            if (events != null) events.ShieldHeld(doll, held);
        }

        internal void OnDollCollapsed(ColosseumDoll doll)
        {
            fx.Puff(doll.transform.position, 5);
        }

        internal void OnDollStood(ColosseumDoll doll)
        {
            fx.Puff(doll.transform.position, 3, 1.6f);
        }

        internal void FireArrow(ColosseumDoll owner, Vector3 from, Vector3 velocity)
        {
            fx.FireArrow(owner, from + Vector3.up * .95f, velocity);
            if (Time.unscaledTime > arrowSoundAt)
            {
                arrowSoundAt = Time.unscaledTime + .05f;
                sfx.Play("arrow");
            }
        }

        internal void OnArrowHit(ColosseumDoll owner, ColosseumDoll victim, Vector3 velocity, Vector3 point)
        {
            if (!victim.Alive || victim.Body.isKinematic) return;
            Vector3 push = Kit.Flat(velocity).normalized;
            Vector3 current = victim.State == DollState.Tumble ? victim.Body.linearVelocity : Vector3.zero;
            victim.Knock(current + push * 4.5f + Vector3.up * 3.6f);
            Deal(victim, AttackDamage(owner, ArrowAttackRatio), string.Empty, new Color(.85f, .94f, 1f), .9f);
            fx.Sparks(point, 5);
            fx.Impact(point, .26f);
            sfx.Play("chain", .6f);
            cheerLeft = .8f;
        }

        void CallTruce(ColosseumDoll a, ColosseumDoll b, float seconds)
        {
            Physics.IgnoreCollision(a.Shape, b.Shape, true);
            truces.Add(new Truce { A = a.Shape, B = b.Shape, Left = seconds });
        }

        void TickTruces(float delta, bool endTurn = false)
        {
            for (int i = truces.Count - 1; i >= 0; i--)
            {
                Truce truce = truces[i];
                truce.Left -= delta;
                if (truce.Left > 0f && !(endTurn && float.IsPositiveInfinity(truce.Left))) { truces[i] = truce; continue; }
                if (truce.A != null && truce.B != null)
                    Physics.IgnoreCollision(truce.A, truce.B, false);
                truces.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ camera and light

        Camera CreateCamera()
        {
            var item = new GameObject("Colosseum Camera", typeof(Camera), typeof(AudioListener));
            item.transform.SetParent(transform, false);
            item.tag = "MainCamera";
            var view = item.GetComponent<Camera>();
            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color32(46, 34, 48, 255);
            view.nearClipPlane = .1f;
            view.farClipPlane = 140f;
            view.allowMSAA = true;
            view.allowHDR = false;
            FrameBattleCamera(view);
            return view;
        }

        void FrameBattleCamera(Camera view)
        {
            // Render textures have their own pixel coordinates and no device cutout.
            Rect output = view.pixelRect;
            if (output.width < 1f || output.height < 1f) return;
            Rect safe = output;
            Rect device = Screen.safeArea;
            if (!view.targetTexture && device.width > 0f && device.height > 0f && device.Overlaps(output))
            {
                safe = Rect.MinMaxRect(Mathf.Max(output.xMin, device.xMin), Mathf.Max(output.yMin, device.yMin),
                    Mathf.Min(output.xMax, device.xMax), Mathf.Min(output.yMax, device.yMax));
            }
            if (pulling && (battleOutputPixels != output || battleTouchSafeArea != safe)) CancelPull();
            battleOutputPixels = output;
            battleTouchSafeArea = safe;
            hud?.SetBattleOutput(output);

            var angle = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 up = angle * Vector3.up;
            // Reproduce the reference phone, then match the fraction of screen covered by the sand.
            // For an orthographic camera that fraction varies as 1 / (aspect * size²).
            float top = (HalfDepth + 3.55f) * up.z + 4f * up.y;
            float bottom = -(HalfDepth + 1.45f) * up.z;
            const float hudSpace = .105f, gateSpace = .035f;
            float referenceSize = Mathf.Max((top - bottom) / (2f * (1f - hudSpace - gateSpace)),
                (HalfWidth + 1.45f) / BattleReferenceAspect);
            float aspect = output.width / output.height;
            float sizeForArea = referenceSize * Mathf.Sqrt(BattleReferenceAspect / aspect);

            // Wider tablets also need the royal box above the far gate to read beneath the HUD.
            float throne = (HalfDepth + 1.25f) * up.z + 2.8f * up.y;
            float topMargin = .09f + (output.yMax - safe.yMax) / output.height;
            float bottomMargin = Mathf.Max(.025f, (safe.yMin - output.yMin) / output.height);
            float sizeForEnds = (throne - bottom) / (2f * Mathf.Max(.25f, 1f - topMargin - bottomMargin));
            battleViewSize = Mathf.Max(sizeForArea, Mathf.Max(sizeForEnds, (HalfWidth + .65f) / aspect));
            float referenceCenter = Mathf.Max(2.6f * up.z, top - (1f - 2f * hudSpace) * referenceSize);
            float centre = Mathf.Clamp(referenceCenter, throne - (1f - 2f * topMargin) * battleViewSize,
                bottom + (1f - 2f * bottomMargin) * battleViewSize);
            cameraRest = Vector3.forward * (centre / up.z) - angle * Vector3.forward * 50f;
            view.orthographicSize = battleViewSize * (1f - zoomNow);
            view.transform.SetPositionAndRotation(cameraRest, angle);
        }

        /// <summary>Renders the arena camera to a portrait PNG without needing the Game view to be visible.</summary>
        void SaveFrame(string path)
        {
            if (captureTarget == null)
                return;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = captureTarget;
            var image = new Texture2D(captureTarget.width, captureTarget.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, captureTarget.width, captureTarget.height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            System.IO.File.WriteAllBytes(path, path.EndsWith(".jpg") ? image.EncodeToJPG(92) : image.EncodeToPNG());
            Destroy(image);
        }

        void PositionCamera(float real)
        {
            zoomKick *= Mathf.Exp(-real * 6.5f);
            zoomNow = Mathf.Lerp(zoomNow, zoomKick, 1f - Mathf.Exp(-real * 28f));
            if (phase == Phase.Menu || phase == Phase.Matching)
            {
                if (closeUp)
                {
                    lobby.Zoom = 2.1f;
                    lobby.Lift = .8f;
                    lobby.Pan = Time.unscaledTime % 6f < 3f ? -1.54f : 1.54f;
                }
                lobby.Frame(arenaCamera, real);
                // Heroes watch the player and take turns cheering.
                float beat = Time.unscaledTime / 2.6f;
                for (int i = 0; i < lobbyHeroes.Count; i++)
                {
                    lobbyHeroes[i].LookTarget = arenaCamera.transform.position;
                    lobbyHeroes[i].Cheering = !closeUp && (int)beat % (lobbyHeroes.Count + 2) == i && beat % 1f < .42f;
                }
                return;
            }
            FrameBattleCamera(arenaCamera);
            shake = Mathf.MoveTowards(shake, 0f, real * 1.4f);
            Transform view = arenaCamera.transform;
            if (shake > .001f)
                view.position += (view.right * Random.Range(-1f, 1f) + view.up * Random.Range(-1f, 1f)) * shake;
        }

        void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(200, 226, 248, 255);
            RenderSettings.ambientEquatorColor = new Color32(181, 171, 154, 255);
            RenderSettings.ambientGroundColor = new Color32(163, 120, 79, 255);
            RenderSettings.fog = false;
            var sun = new GameObject("Colosseum sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
            towardsSun = -sun.transform.forward;
            sun.type = LightType.Directional;
            sun.color = new Color32(255, 240, 205, 255);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .7f;
            sun.shadowBias = .04f;
            sun.shadowNormalBias = .3f;
            RenderSettings.sun = sun;
        }
    }
}
