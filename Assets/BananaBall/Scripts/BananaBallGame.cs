using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BananaBall
{
    /// <summary>
    /// Entry point for Wacky Baseball. Builds the ballpark, both clubs, light, sky, post-processing, audio and UI at start-up,
    /// then runs everything from one ordered Update. Scene: Assets/BananaBall/Scenes/BananaBall.unity
    /// (re-create with the menu BananaBall > Build Scene).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BananaBallGame : MonoBehaviour
    {
        public static BananaBallGame I { get; private set; }

        [Header("Assets (filled by BananaBall > Build Scene)")]
        public Shader litShader;
        public Shader unlitShader;
        public Shader skyShader;
        [Tooltip("Latin display font (numbers, English call-outs).")]
        public Font fontFile;
        public TMP_FontAsset fontAsset;
        [Tooltip("Korean fallback font.")]
        public Font fontKrFile;
        public TMP_FontAsset fontKrAsset;
        [Tooltip("Index of the 3D (Universal) renderer in the URP asset. -1 = find it at runtime.")]
        public int rendererIndex = -1;
        [Tooltip("Cartoon FX Remaster prefabs for hits, dust, catches and fireworks (filled by Refresh Scene Assets).")]
        public FxPrefabs fxPrefabs = new FxPrefabs();

        [Header("Debug")]
        [Tooltip("Both clubs are played by the AI.")]
        public bool autoPlay;
        [Range(0.25f, 6f)] public float timeScale = 1f;
        [Tooltip("Test helper: plays the human side through the real input paths.")]
        public bool debugBot;
        [System.NonSerialized] public InputBot bot;

        [System.NonSerialized] public Mats mats;
        [System.NonSerialized] public Field field;
        [System.NonSerialized] public Match match;
        [System.NonSerialized] public Hud hud;
        [System.NonSerialized] public CamRig cam;
        [System.NonSerialized] public Fx fx;
        [System.NonSerialized] public BallView ballView;
        [System.NonSerialized] public TMP_FontAsset font;
        [System.NonSerialized] public Light sun;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            if (litShader == null) litShader = Shader.Find("BananaBall/Lit");
            if (unlitShader == null) unlitShader = Shader.Find("BananaBall/Unlit");
            if (skyShader == null) skyShader = Shader.Find("BananaBall/Sky");
            font = ResolveFont();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = camGo.AddComponent<Camera>();
            c.nearClipPlane = 0.15f;
            c.farClipPlane = 1800f;
            camGo.AddComponent<AudioListener>();
            var data = c.GetUniversalAdditionalCameraData();
            int ri = FindRenderer();
            if (ri >= 0) data.SetRenderer(ri);

            mats = new Mats(litShader, unlitShader, skyShader) { font = font };
            WorldText.Font = font;
            sun = Env.Setup(mats, c);

            var mine = Roster.Mine();
            var rival = Roster.Rival(Save.Matches % Roster.RivalCount, Save.Matches);
            field = new Field(mats, font, mine, rival);
            cam = new CamRig(c, this);
            fx = new Fx(mats, field.boardMesh, c) { lib = fxPrefabs };
            ballView = new BallView(mats, field.ballMesh, field.ballShadow, fx, c);
            ballView.Show(false);
            Sfx.Init(camGo);
            match = new Match(this) { autoPlay = autoPlay };
            hud = new Hud(this, font);
            match.Build(mats, field, ballView, mine, rival);
            cam.Snap();
        }

        TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset main = null, kr = null;
            if (fontFile != null) main = Runtime(fontFile, "Wacky Latin");
            if (main == null) main = fontAsset;
            if (fontKrFile != null) kr = Runtime(fontKrFile, "Wacky Korean");
            if (kr == null) kr = fontKrAsset;
            if (main == null) main = kr;
            if (main == null) main = TMP_Settings.defaultFontAsset;
            if (main != null && kr != null && kr != main)
            {
                if (main.fallbackFontAssetTable == null) main.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!main.fallbackFontAssetTable.Contains(kr)) main.fallbackFontAssetTable.Add(kr);
            }
            return main;
        }

        static TMP_FontAsset Runtime(Font f, string name)
        {
            try
            {
                var fa = TMP_FontAsset.CreateFontAsset(f, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fa != null) fa.name = name;
                return fa;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Wacky Baseball] runtime font failed (" + f.name + "): " + e.Message);
                return null;
            }
        }

        int FindRenderer()
        {
            if (rendererIndex >= 0) return rendererIndex;
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return -1;
            var list = asset.rendererDataList;
            for (int i = 0; i < list.Length; i++)
                if (list[i] is UniversalRendererData) return i;
            return -1;
        }

        // hit-stop: a freeze-frame on the crack of the bat (animation, physics and particles all stop), then optional slow motion
        float stopT, slowT, slowLen, slowScale = 1f;

        /// <summary>Freeze everything for 'freeze' real seconds, then run at 'slow' for 'slowFor' seconds easing back to full speed.</summary>
        public void HitStop(float freeze, float slow = 1f, float slowFor = 0f)
        {
            stopT = Mathf.Max(stopT, freeze);
            if (slowFor > 0f) { slowScale = slow; slowT = slowLen = slowFor; }
        }

        float Warp(float real)
        {
            if (stopT > 0f) { stopT -= real; return 0.02f; }
            if (slowT > 0f)
            {
                slowT -= real;
                float u = Mathf.Clamp01(slowT / slowLen);
                return Mathf.Lerp(1f, slowScale, Mathf.Clamp01(u / 0.45f));   // hold the slow motion, ease back over the last stretch
            }
            return 1f;
        }

        /// <summary>Real seconds this frame. While a fixed capture rate is set (Time.captureDeltaTime, as when recording a video) every
        /// frame is exactly that long, so the clip plays back at true speed however slowly its frames were rendered.</summary>
        public static float RealDelta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        void Update()
        {
            float real = Mathf.Min(RealDelta * timeScale, 0.05f);
            float warp = Warp(real);
            Time.timeScale = timeScale * warp;
            float dt = real * warp;
            if (match.phase == Phase.Lobby) match.autoPlay = autoPlay;
            if (debugBot) { if (bot == null) bot = new InputBot(this); bot.Tick(dt); }
            else if (bot != null) { bot.Stop(); bot = null; }
            else if (autoPlay && (match.phase == Phase.Lobby || match.phase == Phase.Result) && match.phaseT > 1.5f)
            {
                if (match.phase == Phase.Lobby) match.StartMatch(hud.nextRival); else match.StartMatch(match.T[1]);
            }
            Ptr.Tick();
            match.Tick(dt);
            field.Tick(dt);
            fx.Tick(dt);
            cam.Tick(dt, real);
            hud.Tick(dt);
            var M = match;
            int away = M.visitor, home = 1 - M.visitor;
            bool inGame = M.phase != Phase.Lobby;
            field.SetBoard(M.T[away], M.T[home], M.runs[away], M.runs[home], inGame ? M.HalfLabel : "", inGame ? M.balls + "-" + M.strikes + "  " + M.outs + " OUT" : "", M.batterP != null && inGame ? "NOW BATTING  #" + M.batterP.number + " " + M.batterP.name.ToUpperInvariant() : null);
            Sfx.Crowd(M.phase == Phase.Lobby ? 0.25f : M.phase == Phase.Live ? 0.55f : 0.38f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_BBCrowdExcite", 0f);
            Ptr.SimActive = false;
        }
    }

    /// <summary>Particle prefabs from the project's Cartoon FX Remaster pack (JMO). Any left empty falls back to the built-in sprites.</summary>
    [System.Serializable]
    public class FxPrefabs
    {
        public GameObject hit, hitSparks, bigImpact, woodDebris, groundHit, groundHitBig, smokePoof, catchPop, stun, firework, fireworkBig, fireTrail;
        public GameObject textBoom, textWham, textPow, textBoing, textWow, textSmash;
    }
}
