using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The phone's vibration, as a handful of named feels. The battle asks for the ones that depend on who was hit;
    /// the rest follow the sounds, so anything that has a sound can have a feel by adding one line to <see cref="BySound"/>.
    /// A stronger feel cuts a weaker one short and a weaker one waits its turn, so a flurry of hits stays readable.
    /// </summary>
    public static class ColosseumHaptics
    {
        public enum Feel { Tick, Light, Medium, Heavy, Launch, Hit, Hurt, Rumble, Open, Kill, Fallen, Win, Lose }

        const string Key = "colosseum.haptics";
        static int on = -1;

        /// <summary>The player's own switch, kept with the rest of the Colosseum save. On until they turn it off.</summary>
        public static bool On
        {
            get { if (on < 0) on = PlayerPrefs.GetInt(Key, 1) != 0 ? 1 : 0; return on == 1; }
            set { on = value ? 1 : 0; PlayerPrefs.SetInt(Key, on); PlayerPrefs.Save(); }
        }

        /// <summary>Told about every feel that is played, on any platform (the editor has no motor, so tests listen here).</summary>
        public static System.Action<Feel, float> Felt;

        // Each feel: how much it matters, then pairs of milliseconds and strength (0 is a pause, 255 is full).
        static readonly int[][] Shapes =
        {
            /* Tick   */ new[] { 1, 10, 70 },
            /* Light  */ new[] { 2, 16, 120 },
            /* Medium */ new[] { 3, 26, 180 },
            /* Heavy  */ new[] { 4, 42, 255 },
            /* Launch */ new[] { 3, 24, 230 },
            /* Hit    */ new[] { 4, 38, 255 },
            /* Hurt   */ new[] { 5, 34, 255, 46, 0, 40, 190 },
            /* Rumble */ new[] { 3, 70, 90, 30, 0, 70, 130, 30, 0, 90, 90 },
            /* Open   */ new[] { 5, 40, 255, 40, 0, 24, 150, 40, 0, 24, 110 },
            /* Kill   */ new[] { 6, 55, 255, 50, 0, 95, 230 },
            /* Fallen */ new[] { 6, 110, 255, 70, 0, 170, 140 },
            /* Win    */ new[] { 6, 28, 170, 70, 0, 28, 200, 70, 0, 110, 255 },
            /* Lose   */ new[] { 6, 150, 170, 90, 0, 240, 90 },
        };

        static readonly Dictionary<string, Feel> BySound = new Dictionary<string, Feel>
        {
            { "ui-tap", Feel.Tick }, { "ui-tab", Feel.Tick }, { "ui-open", Feel.Tick }, { "ui-close", Feel.Tick }, { "click", Feel.Tick },
            { "card-pick", Feel.Tick }, { "card-equip", Feel.Tick }, { "match-tick", Feel.Tick }, { "trophy-tick", Feel.Tick },
            { "coin", Feel.Tick }, { "pull", Feel.Tick }, { "cock", Feel.Tick }, { "plant", Feel.Tick },

            { "powerup", Feel.Light }, { "heal", Feel.Light }, { "shield", Feel.Light }, { "block", Feel.Light }, { "boing", Feel.Light },
            { "warp", Feel.Light }, { "crate", Feel.Light }, { "reward-claim", Feel.Light }, { "trophy-gain", Feel.Light },
            { "trophy-loss", Feel.Light }, { "match-ding", Feel.Light }, { "chest-appear", Feel.Light }, { "decree-unroll", Feel.Light },
            { "strip", Feel.Light }, { "hook", Feel.Light }, { "arrow", Feel.Light }, { "cast", Feel.Light }, { "lurch", Feel.Light },
            { "set-lose", Feel.Light }, { "reward-show-lose", Feel.Light }, { "match-reveal", Feel.Light },

            { "smash", Feel.Medium }, { "starhit", Feel.Medium }, { "skyward", Feel.Medium }, { "crumble", Feel.Medium },
            { "clang", Feel.Medium }, { "slice", Feel.Medium }, { "thud", Feel.Medium }, { "spike", Feel.Medium },
            { "spike-out", Feel.Medium }, { "fall", Feel.Medium }, { "battle-start", Feel.Medium }, { "set-start", Feel.Medium },
            { "event", Feel.Medium }, { "match-found", Feel.Medium }, { "set-win", Feel.Medium },

            { "boom", Feel.Heavy }, { "blast", Feel.Heavy },

            { "quake", Feel.Rumble }, { "lava-rumble", Feel.Rumble }, { "lava", Feel.Rumble }, { "twister", Feel.Rumble },
            { "tide", Feel.Rumble }, { "fire-pillar", Feel.Rumble }, { "fire-warn", Feel.Rumble }, { "scald", Feel.Medium },
            { "whirl", Feel.Rumble }, { "chest-shake", Feel.Rumble }, { "upgrade-charge", Feel.Rumble }, { "recruit-portal", Feel.Rumble },

            { "chest-open", Feel.Open }, { "upgrade-burst", Feel.Open }, { "recruit-reveal", Feel.Open }, { "arena-unlock", Feel.Open },
            { "rank-up", Feel.Open }, { "stage-up", Feel.Open },

            { "match-win", Feel.Win }, { "reward-show", Feel.Win }, { "match-lose", Feel.Lose },
        };

        static int busyWeight;
        static float busyUntil, lastAt = -1f;

        /// <summary>Every sound passes through here; the ones with a feel of their own get it.</summary>
        public static void OnSound(string name, float volume)
        {
            // Bodies rattle off the walls many times a turn: the faintest tick, and only for the hard knocks.
            if (name == "bounce") { if (volume >= .75f) Play(Feel.Tick, volume); return; }
            // The opponent's turn is announced more quietly, and does not need a tap on the hand.
            if (name == "turn-start") { if (volume >= 1f) Play(Feel.Light); return; }
            if (BySound.TryGetValue(name, out Feel feel)) Play(feel);
        }

        /// <param name="power">0 to 1: how hard, for the feels that come in sizes (a hit, a launch, a tick of the sling).</param>
        public static void Play(Feel feel, float power = 1f)
        {
            if (!On) return;
            int[] shape = Shapes[(int)feel];
            int weight = shape[0];
            float now = Time.unscaledTime;
            if (now < busyUntil && weight < busyWeight) return;
            if (now - lastAt < .045f && weight <= busyWeight) return;
            int total = 0;
            for (int i = 1; i < shape.Length; i += 2) total += shape[i];
            busyWeight = weight;
            busyUntil = now + total * .001f;
            lastAt = now;
            power = Mathf.Clamp01(power);
            Felt?.Invoke(feel, power);
            if (Application.platform == RuntimePlatform.Android) Android(feel, shape, power);
#if UNITY_IOS && !UNITY_EDITOR
            else if (Application.platform == RuntimePlatform.IPhonePlayer) Apple(feel, power);
#endif
        }

        // ---- Android: the system vibrator, with a strength for each pulse where the motor allows it ----

        static AndroidJavaObject vibrator;
        static AndroidJavaClass effects;
        static bool looked, graded;
        static readonly Dictionary<int, AndroidJavaObject> made = new Dictionary<int, AndroidJavaObject>();

        static bool Motor()
        {
            if (looked) return vibrator != null;
            looked = true;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null && !vibrator.Call<bool>("hasVibrator")) vibrator = null;
                if (vibrator != null)
                {
                    graded = vibrator.Call<bool>("hasAmplitudeControl");
                    effects = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception problem)
            {
                Debug.LogWarning("Colosseum haptics are off: " + problem.Message);
                vibrator = null;
            }
            return vibrator != null;
        }

        static void Android(Feel feel, int[] shape, float power)
        {
            if (!Motor()) return;
            try
            {
                // Sized feels come in five strengths, so each effect is built once and kept.
                int step = Mathf.RoundToInt(Mathf.Lerp(.45f, 1f, power) * 4f);
                int key = (int)feel * 8 + step;
                if (!made.TryGetValue(key, out AndroidJavaObject effect))
                {
                    float size = step / 4f;
                    int count = (shape.Length - 1) / 2;
                    var times = new long[count];
                    var strengths = new int[count];
                    for (int i = 0; i < count; i++)
                    {
                        int time = shape[1 + i * 2], strength = shape[2 + i * 2];
                        if (strength > 0)
                        {
                            strength = Mathf.Clamp(Mathf.RoundToInt(strength * size), 1, 255);
                            // A motor with one strength only says "softer" by being shorter.
                            if (!graded) time = Mathf.Max(8, Mathf.RoundToInt(time * Mathf.Lerp(.35f, 1f, strength / 255f)));
                        }
                        times[i] = time;
                        strengths[i] = strength;
                    }
                    effect = count == 1
                        ? effects.CallStatic<AndroidJavaObject>("createOneShot", times[0], graded ? strengths[0] : -1)
                        : effects.CallStatic<AndroidJavaObject>("createWaveform", times, strengths, -1);
                    made[key] = effect;
                }
                vibrator.Call("vibrate", effect);
            }
            catch (System.Exception problem)
            {
                Debug.LogWarning("Colosseum haptics stopped: " + problem.Message);
                vibrator = null;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        // ---- iPhone: the Taptic Engine's own taps (see Assets/Plugins/iOS/ColosseumHaptics.mm) ----

        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void ColosseumHapticImpact(int style, float strength);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void ColosseumHapticNotify(int kind);

        static void Apple(Feel feel, float power)
        {
            float size = Mathf.Lerp(.45f, 1f, power);
            switch (feel)
            {
                case Feel.Tick: ColosseumHapticImpact(0, .55f * size); break;
                case Feel.Light: ColosseumHapticImpact(0, size); break;
                case Feel.Medium: ColosseumHapticImpact(1, 1f); break;
                case Feel.Launch: ColosseumHapticImpact(1, size); break;
                case Feel.Rumble: ColosseumHapticImpact(3, 1f); break;
                case Feel.Heavy: ColosseumHapticImpact(2, 1f); break;
                case Feel.Hit: ColosseumHapticImpact(4, size); break;
                case Feel.Kill: ColosseumHapticImpact(2, 1f); break;
                case Feel.Hurt: ColosseumHapticNotify(1); break;
                case Feel.Fallen: ColosseumHapticNotify(2); break;
                case Feel.Lose: ColosseumHapticNotify(2); break;
                default: ColosseumHapticNotify(0); break;
            }
        }
#endif
    }
}
