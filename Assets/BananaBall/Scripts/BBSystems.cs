using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BananaBall
{
    /// <summary>One-pointer input (first touch or mouse). A press that starts over a UI control is flagged so field input ignores it.</summary>
    public static class Ptr
    {
        public static bool Down, Held, Up, OverUI;
        public static Vector2 Pos, DownPos;
        public static float DownTime;
        // Simulated pointer (used by automated checks)
        public static bool SimActive, SimHeld;
        public static Vector2 SimPos;
        static bool prevHeld;

        public static void Tick()
        {
            bool held;
            Vector2 pos;
            int id = -1;
            if (SimActive) { held = SimHeld; pos = SimPos; }
            else if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                pos = t.position;
                id = t.fingerId;
            }
            else
            {
                held = Input.GetMouseButton(0);
                pos = Input.mousePosition;
            }
            Down = held && !prevHeld;
            Up = !held && prevHeld;
            Held = held;
            if (held || Up) Pos = pos;
            if (Down)
            {
                DownPos = pos;
                DownTime = Time.unscaledTime;
                OverUI = !SimActive && IsOverUI(id);
            }
            prevHeld = held;
        }

        static bool IsOverUI(int id)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return id >= 0 ? es.IsPointerOverGameObject(id) : es.IsPointerOverGameObject();
        }

        public static bool FieldDown => Down && !OverUI;
        public static bool FieldHeld => Held && !OverUI;
        public static bool FieldUp => Up && !OverUI;
        public static Vector2 Drag => Pos - DownPos;
    }

    /// <summary>uGUI control that reports press-and-hold (pitch buttons).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool held;
        public System.Action onDown, onUp;
        public void OnPointerDown(PointerEventData e) { held = true; onDown?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { if (held) { held = false; onUp?.Invoke(); } }
        void OnDisable() { if (held) { held = false; onUp?.Invoke(); } }
    }

    /// <summary>On-screen joystick: drag inside the base, value is -1..1 on each axis.</summary>
    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public float radius = 120f;
        public Vector2 value;
        public bool held;
        RectTransform rt;

        void Awake() { rt = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) { held = true; OnDrag(e); }

        public void OnDrag(PointerEventData e)
        {
            if (rt == null) rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 lp)) return;
            Vector2 v = Vector2.ClampMagnitude(lp, radius);
            value = v / radius;
            if (knob != null) knob.anchoredPosition = v;
        }

        public void OnPointerUp(PointerEventData e) { held = false; value = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
        void OnDisable() { held = false; value = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
    }

    /// <summary>
    /// Where progress lives. The data is the showballdata table of the project's FlatBuffers save
    /// (Assets/FlatBuffers/UserData.fbs → UserDataSystem.Showballdata → Master.dat). This assembly cannot see
    /// UserDataSystem (it is in Assembly-CSharp), so Assets/BananaBall/SaveBridge/ShowBallSaveBridge.cs plugs it in here.
    /// </summary>
    public interface ISaveBackend
    {
        List<int> Lineup { get; }            // batting order: indices into the club's default line-up
        int Fans { get; set; }
        int Matches { get; set; }
        int LastVisitor { get; set; }        // 0 = no match yet, 1 = the player's club batted first, 2 = the rival batted first
        List<string> Hints { get; }          // first-time hints already shown
        List<string> MatchLog { get; }       // oldest first, at most Save.LogMax
        /// <summary>Write the save file now.</summary>
        void Commit();
        /// <summary>Reset this game's progress only; every other save field is kept.</summary>
        void Clear();
    }

    /// <summary>Progress: batting order, fans, home/away alternation, one-time hints, a short match log.</summary>
    public static class Save
    {
        public const int LogMax = 20;

        /// <summary>Set by ShowBallSaveBridge before the first scene loads (and in the editor after each script reload).</summary>
        public static ISaveBackend Backend;

        static ISaveBackend B
        {
            get
            {
                if (Backend == null)
                {
                    Debug.LogError("[Wacky Baseball] no save backend (ShowBallSaveBridge missing): progress is kept in memory only.");
                    Backend = new MemoryBackend();
                }
                return Backend;
            }
        }

        /// <summary>A valid permutation of 0..n-1 from the saved order (bad or old entries are repaired).</summary>
        public static int[] BattingOrder(int n)
        {
            var s = B.Lineup;
            var r = new int[n];
            var used = new bool[n];
            int k = 0;
            for (int i = 0; i < s.Count && k < n; i++)
            {
                int v = s[i];
                if (v < 0 || v >= n || used[v]) continue;
                used[v] = true;
                r[k++] = v;
            }
            for (int v = 0; v < n && k < n; v++) if (!used[v]) { used[v] = true; r[k++] = v; }
            return r;
        }

        public static void SetBattingOrder(int[] order)
        {
            var l = B.Lineup;
            l.Clear();
            foreach (int v in order) l.Add(v);
            B.Commit();
        }

        public static int Fans => B.Fans;
        public static int Matches => B.Matches;
        public static int FanLevel(int fans) => 1 + fans / 100;
        public static IReadOnlyList<string> MatchLog => B.MatchLog;

        public static void Record(out int wins, out int losses, out int draws)
        {
            wins = losses = draws = 0;
            foreach (var l in B.MatchLog)
            {
                if (l.StartsWith("W")) wins++;
                else if (l.StartsWith("L")) losses++;
                else if (l.StartsWith("D")) draws++;
            }
        }

        /// <summary>A finished match: fans earned, match count and the log line go to the file in one write.</summary>
        public static void RecordMatch(int fansEarned, string logLine)
        {
            B.Fans += fansEarned;
            B.Matches += 1;
            var log = B.MatchLog;
            log.Add(logLine);
            while (log.Count > LogMax) log.RemoveAt(0);
            B.Commit();
        }

        /// <summary>First match: random. Rematch: swap who bats first. Returns the team index that bats first (0 = the player).</summary>
        public static int NextVisitor()
        {
            int last = B.LastVisitor;
            int v = last == 1 ? 1 : last == 2 ? 0 : Random.Range(0, 2);
            B.LastVisitor = v + 1;
            B.Commit();
            return v;
        }

        public static bool FirstTime(string hint)
        {
            var h = B.Hints;
            if (h.Contains(hint)) return false;
            h.Add(hint);
            B.Commit();
            return true;
        }

        /// <summary>Clears this game's progress (order, fans, matches, match log, hints) and nothing else.</summary>
        public static void ResetAll() => B.Clear();

        sealed class MemoryBackend : ISaveBackend
        {
            public List<int> Lineup { get; } = new List<int>();
            public int Fans { get; set; }
            public int Matches { get; set; }
            public int LastVisitor { get; set; }
            public List<string> Hints { get; } = new List<string>();
            public List<string> MatchLog { get; } = new List<string>();
            public void Commit() { }
            public void Clear() { Lineup.Clear(); Fans = Matches = LastVisitor = 0; Hints.Clear(); MatchLog.Clear(); }
        }
    }

    /// <summary>Procedurally synthesised sound effects (no audio assets).</summary>
    public static class Sfx
    {
        public enum Id { Crack, Pop, Whoosh, Cheer, Aww, Ding, Tick, Thud, Whistle, Tap, Boing, Bonk, SlideWhistle, Rocket, Fireworks, Organ, Slide, Smack, Count }
        static AudioClip[] clips;
        static AudioSource[] pool;
        static AudioSource crowdSrc;
        static int next;
        const int Rate = 44100;
        public static bool Muted;

        public static void Init(GameObject host)
        {
            var rnd = new System.Random(3);
            clips = new AudioClip[(int)Id.Count];
            const float P2 = Mathf.PI * 2f;
            clips[(int)Id.Crack] = Make("crack", 0.22f, (t, n) => n * Mathf.Exp(-t / 0.010f) * 0.9f + Mathf.Sin(t * P2 * 1750f) * Mathf.Exp(-t / 0.025f) * 0.45f + Mathf.Sin(t * P2 * 460f) * Mathf.Exp(-t / 0.06f) * 0.4f, rnd);
            // the body of a well-struck ball under the crack: a hard transient, a deep falling thump and a short wooden ring
            clips[(int)Id.Smack] = Make("smack", 0.34f, (t, n) => n * Mathf.Exp(-t / 0.0035f) * 0.85f
                + Mathf.Sin(P2 * (125f - 55f * Mathf.Min(1f, t / 0.12f)) * t) * Mathf.Exp(-t / 0.085f) * 0.9f
                + Mathf.Sin(P2 * 1180f * t) * Mathf.Exp(-t / 0.035f) * 0.28f + n * Mathf.Exp(-t / 0.028f) * 0.2f, rnd);
            clips[(int)Id.Pop] = Make("pop", 0.12f, (t, n) => Mathf.Sin(P2 * (170f - 500f * t) * t) * Mathf.Exp(-t / 0.03f) * 0.9f + n * Mathf.Exp(-t / 0.006f) * 0.6f, rnd);
            clips[(int)Id.Thud] = Make("thud", 0.2f, (t, n) => Mathf.Sin(P2 * 80f * t) * Mathf.Exp(-t / 0.05f) * 0.9f + n * Mathf.Exp(-t / 0.02f) * 0.3f, rnd);
            clips[(int)Id.Ding] = Make("ding", 0.6f, (t, n) => (Mathf.Sin(P2 * 1320f * t) * 0.5f + Mathf.Sin(P2 * 1980f * t) * 0.3f) * Mathf.Exp(-t / 0.18f), rnd);
            clips[(int)Id.Tick] = Make("tick", 0.04f, (t, n) => Mathf.Sin(P2 * 2100f * t) * Mathf.Exp(-t / 0.01f) * 0.6f, rnd);
            clips[(int)Id.Tap] = Make("tap", 0.05f, (t, n) => Mathf.Sin(P2 * 900f * t) * Mathf.Exp(-t / 0.012f) * 0.5f, rnd);
            clips[(int)Id.Whistle] = Make("whistle", 0.45f, (t, n) => Mathf.Sin(P2 * (2300f + 60f * Mathf.Sin(t * 70f)) * t) * Mathf.Clamp01(t * 30f) * Mathf.Clamp01((0.45f - t) * 12f) * 0.35f, rnd);
            clips[(int)Id.Boing] = Make("boing", 0.55f, (t, n) =>
            {
                float f = 180f + 260f * Mathf.Exp(-t * 6f) * (1f + 0.4f * Mathf.Sin(t * 60f));
                return Mathf.Sin(P2 * f * t) * Mathf.Exp(-t / 0.22f) * 0.7f;
            }, rnd);
            clips[(int)Id.Bonk] = Make("bonk", 0.25f, (t, n) => (Mathf.Sin(P2 * 620f * t) * 0.6f + Mathf.Sin(P2 * 1040f * t) * 0.3f) * Mathf.Exp(-t / 0.05f) + n * Mathf.Exp(-t / 0.004f) * 0.4f, rnd);
            clips[(int)Id.SlideWhistle] = MakePhase("slidewhistle", 0.7f, t => 1800f - 1500f * (t / 0.7f), t => Mathf.Clamp01(t * 20f) * Mathf.Clamp01((0.7f - t) * 8f) * 0.32f);
            clips[(int)Id.Rocket] = MakeFiltered("rocket", 0.6f, t => Mathf.Clamp01(t * 8f) * Mathf.Exp(-t * 2.5f) * 0.9f, 0.18f, rnd);
            clips[(int)Id.Whoosh] = MakeFiltered("whoosh", 0.26f, t => Mathf.Sin(Mathf.PI * t / 0.26f) * 0.6f, 0.08f, rnd);
            clips[(int)Id.Slide] = MakeFiltered("slide", 0.45f, t => Mathf.Clamp01(t * 12f) * Mathf.Exp(-t * 4f) * 0.7f, 0.3f, rnd);
            clips[(int)Id.Fireworks] = MakeFireworks("fireworks", 2.2f, rnd);
            clips[(int)Id.Organ] = MakeOrgan("organ");
            clips[(int)Id.Cheer] = MakeCrowd("cheer", 2.2f, t => Mathf.Clamp01(t * 6f) * Mathf.Exp(-Mathf.Max(0f, t - 0.3f) / 0.9f), 0.9f, rnd);
            clips[(int)Id.Aww] = MakeCrowd("aww", 1.2f, t => Mathf.Clamp01(t * 5f) * Mathf.Exp(-t / 0.6f) * 0.8f, 0.45f, rnd);

            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = host.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            crowdSrc = host.AddComponent<AudioSource>();
            crowdSrc.clip = MakeCrowd("crowdloop", 3f, t => 1f, 0.6f, rnd);
            crowdSrc.loop = true;
            crowdSrc.volume = 0.08f;
            crowdSrc.playOnAwake = false;
            crowdSrc.Play();
        }

        delegate float Gen(float t, float noise);

        static AudioClip Make(string name, float len, Gen g, System.Random r)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Clamp(g(t, (float)(r.NextDouble() * 2.0 - 1.0)), -1f, 1f);
            }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakePhase(string name, float len, System.Func<float, float> freq, System.Func<float, float> env)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += freq(t) / Rate;
                d[i] = Mathf.Sin(ph * Mathf.PI * 2f) * env(t);
            }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeFiltered(string name, float len, System.Func<float, float> env, float k, System.Random r)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float a = k * (0.4f + 0.6f * Mathf.Sin(Mathf.PI * t / len));
                lp += ((float)(r.NextDouble() * 2.0 - 1.0) - lp) * a;
                d[i] = Mathf.Clamp(lp * env(t) * 2.5f, -1f, 1f);
            }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeFireworks(string name, float len, System.Random r)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            float[] pops = { 0.05f, 0.42f, 0.7f, 1.05f, 1.3f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                foreach (float p in pops)
                {
                    float u = t - p;
                    if (u < 0f) continue;
                    s += ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-u / 0.05f) * 0.7f + Mathf.Sin(u * 2f * Mathf.PI * 70f) * Mathf.Exp(-u / 0.12f) * 0.5f;
                    if (u > 0.08f && u < 0.9f && r.NextDouble() < 0.004) s += ((float)r.NextDouble() * 2f - 1f) * 0.6f;
                }
                d[i] = Mathf.Clamp(s, -1f, 1f);
            }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        /// <summary>The ballpark organ "charge" riff.</summary>
        static AudioClip MakeOrgan(string name)
        {
            float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 659.25f, 783.99f };
            float[] lens = { 0.16f, 0.16f, 0.16f, 0.3f, 0.14f, 0.5f };
            float total = 0f;
            foreach (float l in lens) total += l;
            int n = Mathf.CeilToInt(total * Rate);
            var d = new float[n];
            int idx = 0;
            float start = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                while (idx < notes.Length - 1 && t > start + lens[idx]) { start += lens[idx]; idx++; }
                float u = t - start;
                float f = notes[idx];
                float env = Mathf.Clamp01(u * 60f) * Mathf.Clamp01((lens[idx] - u) * 30f + 0.2f);
                float v = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.12f;
                v *= 1f + 0.05f * Mathf.Sin(t * 2f * Mathf.PI * 6f);
                d[i] = v * env * 0.5f;
            }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeCrowd(string name, float len, System.Func<float, float> env, float bright, System.Random r)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            float lp1 = 0f, lp2 = 0f, wob = 0f, wobT = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                if (t >= wobT) { wob = (float)r.NextDouble(); wobT = t + 0.05f + (float)r.NextDouble() * 0.1f; }
                float noise = (float)(r.NextDouble() * 2.0 - 1.0);
                lp1 += (noise - lp1) * (0.05f + 0.1f * bright);
                lp2 += (lp1 - lp2) * 0.2f;
                float voice = Mathf.Sin(2f * Mathf.PI * (180f + 120f * wob) * t) * 0.15f * bright;
                d[i] = (lp2 * 3f + voice * lp1 * 4f) * env(t) * (0.75f + 0.25f * wob);
            }
            int fade = Mathf.Min(2000, n / 4);
            for (int i = 0; i < fade; i++) { float w = i / (float)fade; d[i] *= w; d[n - 1 - i] *= w; }
            var c = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        /// <summary>Raised for every sound the game asks for (id, volume, pitch), muted or not, so a video capture can rebuild its
        /// soundtrack in step with the frames.</summary>
        public static System.Action<Id, float, float> Played;

        public static void Play(Id id, float vol = 1f, float pitch = 1f)
        {
            Played?.Invoke(id, vol, pitch);
            if (Muted || clips == null || pool == null) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = pitch;
            src.PlayOneShot(clips[(int)id], vol);
        }

        public static void Crowd(float level)
        {
            if (crowdSrc == null) return;
            crowdSrc.volume = Muted ? 0f : Mathf.Lerp(0.05f, 0.34f, Mathf.Clamp01(level));
        }
    }

    /// <summary>Pooled quads for flashes, dust, confetti, dizzy stars, bat splinters, smoke and fireworks.</summary>
    public class Fx
    {
        class P
        {
            public Transform t;
            public MeshRenderer r;
            public Vector3 v, spin, center;
            public float life, max, s0, s1, ang, rad, angV;
            public bool bill, grav, orbit;
            public Color c;
        }

        struct Pending { public float at; public Vector3 pos; public Color c; }

        readonly List<P> pool = new List<P>();
        readonly List<Pending> pending = new List<Pending>();
        readonly Mats M;
        readonly Mesh board;
        readonly Camera cam;
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly Transform root;
        float clock;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        /// <summary>Cartoon FX Remaster prefabs; the built-in sprite effects stand in for any that are missing.</summary>
        public FxPrefabs lib;

        public Fx(Mats m, Mesh boardMesh, Camera c)
        {
            M = m;
            board = boardMesh;
            cam = c;
            root = new GameObject("Fx").transform;
        }

        P Get(Material mat)
        {
            foreach (var p in pool)
                if (p.life >= p.max) { p.r.sharedMaterial = mat; p.t.gameObject.SetActive(true); return p; }
            var go = new GameObject("fx");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = board;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var np = new P { t = go.transform, r = r };
            pool.Add(np);
            return np;
        }

        P Spawn(Material mat, Vector3 pos, Color c, float life, float s0, float s1, Vector3 v, bool bill, bool grav, Vector3 spin)
        {
            var p = Get(mat);
            p.t.position = pos;
            p.c = c;
            p.life = 0f;
            p.max = life;
            p.s0 = s0;
            p.s1 = s1;
            p.v = v;
            p.bill = bill;
            p.grav = grav;
            p.spin = spin;
            p.orbit = false;
            p.t.rotation = bill && cam ? cam.transform.rotation : Random.rotation;
            p.t.localScale = Vector3.one * s0;
            return p;
        }

        /// <summary>Spawn a particle prefab (scaled as a whole), optionally riding on a transform; it cleans itself up.</summary>
        public GameObject Play(GameObject prefab, Vector3 pos, float scale = 1f, Transform follow = null, float life = 5f, Quaternion? rot = null)
        {
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pos, rot ?? prefab.transform.rotation, follow != null ? follow : root);
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (life > 0f && main.loop && follow == null) main.loop = false;
            }
            if (life > 0f) Object.Destroy(go, life);
            return go;
        }

        /// <summary>A comic word (BOOM, WHAM...) facing the camera.</summary>
        public void Word(GameObject prefab, Vector3 pos, float scale = 1f)
        {
            if (prefab == null) return;
            Quaternion face = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : Quaternion.identity;
            Play(prefab, pos, scale, null, 3f, face);
        }

        /// <summary>Bat on ball: a white hit flash, sparks for solid contact.</summary>
        public void Contact(Vector3 pos, float quality)
        {
            if (lib != null && lib.hit != null)
            {
                Play(lib.hit, pos, 0.45f + 0.35f * quality);
                if (quality > 0.55f) Play(lib.hitSparks, pos, 0.5f + 0.4f * quality);
            }
            else Flash(pos, Color.white, 1f);
        }

        /// <summary>Bat on ball, scaled by how well it was struck (k 0..1): a white star flash that pops at full size (it holds through
        /// the freeze-frame), a shock ring, the Cartoon FX hit and sparks, and the big impact for a crushed ball.</summary>
        public void Impact(Vector3 pos, float k, bool big)
        {
            // sized to the view, so it reads the same from the close batting camera and from behind the pitcher
            float vis = View(pos);
            float size = vis * (0.06f + 0.08f * k);
            Spawn(M.Unlit(Color.white, M.star, true, 3100), pos, Color.white, 0.14f + 0.1f * k, size * 0.85f, size, Vector3.zero, true, false, Vector3.zero);
            RingPop(pos, new Color(1f, 0.95f, 0.72f, 0.95f), vis * (0.1f + 0.16f * k));
            if (lib != null && lib.hit != null)
            {
                Play(lib.hit, pos, vis * (0.03f + 0.03f * k));
                if (k > 0.35f && lib.hitSparks != null) Play(lib.hitSparks, pos, vis * (0.035f + 0.035f * k));
                // a crushed ball: a second, wider spray and ring (the Cartoon FX big impact leaves a smoke cloud that would sit
                // over the hitter while the camera holds on him)
                if (big && lib.hitSparks != null) { Play(lib.hitSparks, pos, vis * 0.09f); RingPop(pos, new Color(1f, 0.85f, 0.4f, 0.8f), vis * 0.42f); }
            }
        }

        /// <summary>Height of the camera's view (metres) at a point: effects sized by it take the same share of the screen at any distance.</summary>
        public float View(Vector3 pos)
        {
            if (cam == null) return 8f;
            float d = Mathf.Max(1f, Vector3.Distance(cam.transform.position, pos));
            return 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>The KA-BOOM perfect hit.</summary>
        public void BigHit(Vector3 pos)
        {
            if (lib != null && lib.hitSparks != null)
            {
                float vis = View(pos);
                Play(lib.hitSparks, pos, vis * 0.09f);
                Word(lib.textBoom, pos + Vector3.up * vis * 0.12f + (cam != null ? -cam.transform.forward * 0.6f : Vector3.zero), vis * 0.055f);
            }
            else Flash(pos, Pal.Gold, 2.4f);
        }

        /// <summary>Where a batted ball first hits the grass or dirt.</summary>
        public void Landing(Vector3 pos, float speed)
        {
            if (lib != null && lib.groundHit != null) Play(speed > 18f ? lib.groundHitBig : lib.groundHit, new Vector3(pos.x, 0.02f, pos.z), Mathf.Clamp(speed / 22f, 0.45f, 1.2f));
            else Puff(pos, 4, 0.7f);
        }

        /// <summary>Ball into the glove.</summary>
        public void CatchPop(Vector3 pos)
        {
            if (lib != null && lib.catchPop != null) Play(lib.catchPop, pos, 0.35f);
        }

        /// <summary>Dazed stars over a head (follows it).</summary>
        public void Dizzy(Transform head, Vector3 at, float secs = 2.2f)
        {
            if (lib != null && lib.stun != null) Play(lib.stun, at, 0.55f, head, secs);
            else Stars(at);
        }

        public void Flash(Vector3 pos, Color c, float size = 1.2f) =>
            Spawn(M.Unlit(Color.white, M.star, true, 3100), pos, c, 0.22f, size * 0.3f, size, Vector3.zero, true, false, Vector3.zero);

        public void RingPop(Vector3 pos, Color c, float size = 1f) =>
            Spawn(M.Unlit(Color.white, M.ring, true, 3101), pos, c, 0.25f, size * 0.2f, size, Vector3.zero, true, false, Vector3.zero);

        public void Puff(Vector3 pos, int n = 5, float size = 0.6f)
        {
            if (lib != null && lib.smokePoof != null) { Play(lib.smokePoof, pos + Vector3.up * 0.1f, size * 0.75f); return; }
            Color dust = new Color(0.78f, 0.62f, 0.45f, 0.75f);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.3f, 1f), Random.Range(-1f, 1f)) * 1.4f;
                Spawn(M.Unlit(Color.white, M.soft, false, 3040), pos + Vector3.up * 0.15f, dust, Random.Range(0.5f, 0.8f), size * 0.4f, size * Random.Range(1f, 1.6f), v, true, false, Vector3.zero);
            }
        }

        public void Smoke(Vector3 pos, Color c, float size)
        {
            Spawn(M.Unlit(Color.white, M.soft, false, 3042), pos, c, 0.45f, size * 0.6f, size * 1.8f, new Vector3(0f, 0.6f, 0f), true, false, Vector3.zero);
        }

        public void Confetti(Vector3 pos, int n = 40)
        {
            Color[] cols = { Pal.Gold, Pal.Red, Pal.Blue, Color.white, Pal.Green, Pal.Hex(0xFF7EB6) };
            var mat = M.Unlit(Color.white, null, false, 3045);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = new Vector3(Random.Range(-4f, 4f), Random.Range(4f, 9f), Random.Range(-4f, 4f));
                Spawn(mat, pos, cols[i % cols.Length], Random.Range(1.6f, 2.4f), 0.16f, 0.16f, v, false, true, new Vector3(Random.Range(-600f, 600f), Random.Range(-600f, 600f), 0f));
            }
        }

        public void Splinters(Vector3 pos)
        {
            if (lib != null && lib.woodDebris != null) { Play(lib.woodDebris, pos, 0.7f); Word(lib.textSmash, pos + Vector3.up * 0.8f, 0.4f); return; }
            var mat = M.Unlit(Color.white, null, false, 3046);
            for (int i = 0; i < 14; i++)
            {
                Vector3 v = new Vector3(Random.Range(-5f, 5f), Random.Range(2f, 7f), Random.Range(-2f, 6f));
                Spawn(mat, pos, Color.Lerp(Pal.Wood, Pal.Hex(0x8A5A2E), Random.value), Random.Range(0.7f, 1.2f), 0.06f, 0.04f, v, false, true, new Vector3(Random.Range(-900f, 900f), Random.Range(-900f, 900f), 0f));
            }
        }

        /// <summary>Cartoon stars circling a dizzy head.</summary>
        public void Stars(Vector3 head)
        {
            var mat = M.Unlit(Color.white, M.star, false, 3102);
            for (int i = 0; i < 5; i++)
            {
                var p = Spawn(mat, head, Pal.Gold, 1.8f, 0.14f, 0.14f, Vector3.zero, true, false, Vector3.zero);
                p.orbit = true;
                p.center = head + Vector3.up * 0.12f;
                p.ang = i / 5f * Mathf.PI * 2f;
                p.rad = 0.26f;
                p.angV = 7f;
            }
        }

        public void Fireworks(Vector3 at)
        {
            if (lib != null && lib.firework != null)
            {
                // a volley of rockets over the outfield, then a looping barrage for a few seconds
                for (int i = 0; i < 7; i++)
                    Play(lib.firework, at + new Vector3(Random.Range(-45f, 45f), Random.Range(-28f, -12f), Random.Range(-12f, 18f)), Random.Range(4.5f, 6.5f), null, 6f);
                if (lib.fireworkBig != null)
                {
                    Play(lib.fireworkBig, at + new Vector3(-30f, -30f, 0f), 5.5f, root, 5.5f);
                    Play(lib.fireworkBig, at + new Vector3(30f, -30f, 0f), 5.5f, root, 5.5f);
                }
                return;
            }
            Color[] cols = { Pal.Gold, Pal.Red, Pal.Blue, Pal.Green, Pal.Hex(0xFF7EB6), Color.white };
            for (int i = 0; i < 6; i++)
                pending.Add(new Pending { at = clock + i * 0.32f, pos = at + new Vector3(Random.Range(-35f, 35f), Random.Range(-6f, 14f), Random.Range(-15f, 15f)), c = cols[i % cols.Length] });
        }

        void Burst(Vector3 pos, Color c)
        {
            var mat = M.Unlit(Color.white, M.soft, true, 3105);
            for (int i = 0; i < 28; i++)
            {
                Vector3 v = Random.onUnitSphere * Random.Range(9f, 14f);
                Spawn(mat, pos, c, Random.Range(1.0f, 1.5f), 1.4f, 0.4f, v, true, true, Vector3.zero);
            }
            Spawn(M.Unlit(Color.white, M.star, true, 3106), pos, Color.Lerp(c, Color.white, 0.5f), 0.3f, 4f, 14f, Vector3.zero, true, false, Vector3.zero);
        }

        public void Tick(float dt)
        {
            clock += dt;
            for (int i = pending.Count - 1; i >= 0; i--)
                if (clock >= pending[i].at) { Burst(pending[i].pos, pending[i].c); pending.RemoveAt(i); }
            foreach (var p in pool)
            {
                if (p.life >= p.max) continue;
                p.life += dt;
                if (p.life >= p.max) { p.t.gameObject.SetActive(false); continue; }
                float u = p.life / p.max;
                if (p.orbit)
                {
                    p.ang += p.angV * dt;
                    p.t.position = p.center + new Vector3(Mathf.Cos(p.ang) * p.rad, Mathf.Sin(p.ang * 2f) * 0.04f, Mathf.Sin(p.ang) * p.rad);
                }
                else
                {
                    if (p.grav) { p.v.y -= 9f * dt; p.v *= 1f - 1.2f * dt; }
                    else p.v *= 1f - 2.5f * dt;
                    p.t.position += p.v * dt;
                }
                if (p.bill && cam) p.t.rotation = cam.transform.rotation;
                else p.t.Rotate(p.spin * dt, Space.Self);
                p.t.localScale = Vector3.one * Mathf.Lerp(p.s0, p.s1, 1f - (1f - u) * (1f - u));
                Color c = p.c;
                c.a *= p.grav ? Mathf.Clamp01((1f - u) * 4f) : p.orbit ? Mathf.Clamp01((1f - u) * 5f) : 1f - u;
                mpb.SetColor(ColorId, c);
                p.r.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>The ball: a seamed baseball kept readable at any distance, a trail styled per pitch, a flame for KA-BOOM hits, a ground shadow.</summary>
    public class BallView
    {
        public readonly Transform t;
        readonly TrailRenderer trail;
        readonly Transform shadow;
        readonly Fx fx;
        readonly Camera cam;
        PitchType style = PitchType.Four;
        bool fire;
        float smokeT;
        Vector3 last;
        GameObject fireFx;

        public BallView(Mats m, Mesh ballMesh, Transform shadowT, Fx fxs, Camera c)
        {
            fx = fxs;
            cam = c;
            var go = new GameObject("Ball");
            go.AddComponent<MeshFilter>().sharedMesh = ballMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m.ball;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            t = go.transform;
            trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 0.07f;
            trail.sharedMaterial = m.trail;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
            shadow = shadowT;
            Style(PitchType.Four);
        }

        public void Show(bool on)
        {
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (shadow.gameObject.activeSelf != on) shadow.gameObject.SetActive(on);
            if (!on) { trail.Clear(); fire = false; KillFire(); }
        }

        void KillFire()
        {
            if (fireFx != null) Object.Destroy(fireFx);
            fireFx = null;
        }

        public void Trail(bool on) { trail.emitting = on; if (!on) trail.Clear(); }

        public void Style(PitchType p)
        {
            style = p;
            fire = false;
            KillFire();
            Gradient g = new Gradient();
            Color a, b;
            switch (p)
            {
                case PitchType.Zigzag: a = Pal.Gold; b = Pal.Hex(0xFF5AA0); break;
                case PitchType.Balloon: a = Pal.Hex(0xFF7EB6); b = Pal.Hex(0xFFD1E6); break;
                case PitchType.Phantom: a = Pal.Hex(0x9AE6FF); b = Pal.Hex(0x4AA8FF); break;
                case PitchType.Missile: a = Pal.Hex(0xFFE08A); b = Pal.Hex(0xFF3A1F); break;
                case PitchType.Stop: a = Pal.Hex(0xB6FF5C); b = Pal.Hex(0x3AD86A); break;
                default: a = Color.white; b = Color.white; break;
            }
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.time = PitchInfo.Of(p).magic ? 0.32f : 0.14f;
        }

        public void Fire(bool on)
        {
            fire = on;
            if (!on) KillFire();
            else if (fireFx == null && fx.lib != null && fx.lib.fireTrail != null)
            {
                fireFx = fx.Play(fx.lib.fireTrail, t.position, 0.5f, t, 0f);
                // flame and embers only: the trail's smoke clouds would hang over the hitter while the camera holds on him
                if (fireFx != null)
                    foreach (var ps in fireFx.GetComponentsInChildren<ParticleSystem>(true))
                        if (ps.name.Contains("Smoke")) { var em = ps.emission; em.enabled = false; }
            }
            if (on)
            {
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Pal.Hex(0xFFF2A0), 0f), new GradientColorKey(Pal.Hex(0xFF3A1F), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.time = 0.4f;
            }
        }

        /// <summary>A long bright gold streak behind a crushed ball (no fire, no smoke: the view stays clear).</summary>
        public void Streak()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Pal.Gold, 0.35f), new GradientColorKey(Pal.Hex(0xFF9A2B), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.time = 0.5f;
        }

        public void Set(Vector3 p)
        {
            Vector3 d = p - last;
            last = p;
            t.position = p;
            if (d.sqrMagnitude > 1e-6f) t.Rotate(Vector3.Cross(Vector3.up, d.normalized), d.magnitude * 900f, Space.World);
            float gy = Geo.GroundY(p);
            float h = Mathf.Clamp01((p.y - gy) / 25f);
            shadow.position = new Vector3(p.x, gy + 0.03f, p.z);
            shadow.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.4f, h) * Scale();
            // keep it readable from far away
            float s = Scale();
            t.localScale = Vector3.one * s;
            trail.widthMultiplier = 0.07f * s;
            smokeT -= Time.deltaTime;
            // (a crushed ball keeps only its flame: smoke puffs behind it would fill the view while the camera holds on the hitter)
            if (style == PitchType.Missile && !fire && smokeT <= 0f && t.gameObject.activeSelf)
            {
                smokeT = 0.025f;
                fx.Smoke(p, fire ? new Color(1f, 0.55f, 0.2f, 0.7f) : new Color(0.85f, 0.85f, 0.85f, 0.55f), 0.12f * s);
            }
        }

        float Scale()
        {
            if (cam == null) return 1f;
            float dist = Vector3.Distance(cam.transform.position, t.position);
            float world = 13f / Mathf.Max(400f, Screen.height) * 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float s = world / (2f * Cfg.BallRadius);
            if (style == PitchType.Balloon && !fire) s *= 1.6f;
            return Mathf.Clamp(s, 1f, 9f);
        }
    }
}
