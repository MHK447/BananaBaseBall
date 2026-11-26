using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BananaBall
{
    /// <summary>Every material the game uses, built once from three shaders: Lit (PBR or toon, vertex colour, ink outline), Unlit (effects) and Sky.
    /// Players, bats, balls and mascots are toon-shaded with a thick ink line; the stadium stays PBR without one.</summary>
    public class Mats
    {
        public readonly Shader lit, unlit, sky;
        public readonly Material body, ground, crowd, glow, ball, prop, metal, glass, skyMat, trail, smoke, net, shadow, chalk, gear, mascot;
        public readonly Texture2D detail, soft, circle, ring, star, netTex, burst, white;
        readonly Dictionary<long, Material> unlitCache = new Dictionary<long, Material>();
        readonly Dictionary<int, Material> teamCache = new Dictionary<int, Material>();
        public TMP_FontAsset font;

        public Mats(Shader litShader, Shader unlitShader, Shader skyShader)
        {
            lit = litShader;
            unlit = unlitShader;
            sky = skyShader;
            white = Texture2D.whiteTexture;
            detail = Tex.Detail(256, 11);
            soft = Tex.SoftCircle();
            circle = Tex.Circle();
            ring = Tex.RingTex();
            star = Tex.Star();
            burst = Tex.Burst(256, 13);
            netTex = Tex.Net(64, 8);

            body = Toon("BB_Body", 0.9f, Look.PlayerInk);
            gear = Toon("BB_Gear", 0.9f, Look.GearInk);
            ball = Toon("BB_Ball", 0.55f, Look.BallInk, Look.BallInkMaxPx, 1f);
            mascot = Toon("BB_Mascot", 0.75f, Look.MascotInk);
            prop = Lit("BB_Prop", Color.white, 0.9f);
            metal = Lit("BB_Metal", Color.white, 0.92f, 0.85f);
            glass = Lit("BB_Glass", Color.white, 1f, 0.4f);
            glow = Lit("BB_Glow", Color.white, 0.5f);
            glow.SetFloat("_Emission", 1f);

            ground = Lit("BB_Ground", Color.white, 0.3f);
            ground.EnableKeyword("_GROUND");
            ground.SetTexture("_DetailTex", detail);
            ground.SetFloat("_StripeContrast", 0.075f);

            chalk = Lit("BB_Chalk", Color.white, 0.2f);

            crowd = Lit("BB_Crowd", Color.white, 0.5f, 0f, 0.4f);
            crowd.EnableKeyword("_CROWD");
            crowd.EnableKeyword("_TOON");

            skyMat = new Material(sky) { name = "BB_Sky" };

            trail = Unlit(new Color(1f, 1f, 1f, 0.85f), null, false, 3050);
            smoke = Unlit(new Color(1f, 1f, 1f, 1f), soft, false, 3060);
            net = Unlit(new Color(0.12f, 0.13f, 0.15f, 0.55f), netTex, false, 3010);
            net.SetFloat("_Cull", 0f);
            shadow = Unlit(new Color(0.02f, 0.04f, 0.02f, 0.5f), soft, false, 2950);
        }

        /// <summary>PBR material (no ink line).</summary>
        public Material Lit(string name, Color c, float gloss = 0.9f, float metallic = 0f, float wrap = 0f)
        {
            var m = new Material(lit) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Wrap", wrap);
            m.enableInstancing = false;
            Ink(m, 0f);
            return m;
        }

        /// <summary>Cel-shaded material with an ink outline. width: line width in metres where it is seen (clamped in pixels).
        /// Meshes drawn with it need ToMesh(outline: true).</summary>
        public Material Toon(string name, float gloss, float width, float maxPx = Look.InkMaxPx, float minPx = Look.InkMinPx)
        {
            var m = Lit(name, Color.white, gloss);
            m.EnableKeyword("_TOON");
            Ink(m, width, maxPx, minPx);
            return m;
        }

        /// <summary>Set or remove (width 0) the ink outline of a Lit material.</summary>
        public static void Ink(Material m, float width, float maxPx = Look.InkMaxPx, float minPx = Look.InkMinPx)
        {
            m.SetFloat("_OutlineWidth", width);
            m.SetFloat("_OutlineMaxPx", maxPx);
            m.SetFloat("_OutlineMinPx", minPx);
            m.SetColor("_OutlineColor", Look.Ink);
            m.SetFloat("_OutlineTint", Look.InkTint);
            bool on = width > 0f;
            m.SetShaderPassEnabled("SRPDefaultUnlit", on);   // the pass's LightMode
            m.SetShaderPassEnabled("Outline", on);           // and its name, whichever Unity matches
        }

        /// <summary>Character material for a team: the jersey texture carries the pinstripes when the team wears them.</summary>
        public Material Team(TeamData t)
        {
            int key = t.index * 1000 + (t.pinstripes ? 1 : 0) + ColorKey(t.stripe) % 997;
            if (teamCache.TryGetValue(key, out Material m) && m != null) return m;
            m = Toon("BB_Team_" + t.abbr, 0.9f, Look.PlayerInk);
            if (t.pinstripes) m.SetTexture("_MainTex", Tex.Pinstripe(t.stripe));
            teamCache[key] = m;
            return m;
        }

        public Material Unlit(Color c, Texture tex = null, bool overlay = false, int queue = 3000)
        {
            long key = ((long)ColorKey(c) << 20) ^ ((tex != null ? tex.GetInstanceID() : 0) * 31L) ^ (overlay ? 1L << 62 : 0L) ^ ((long)queue << 40);
            if (unlitCache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(unlit) { name = "BB_Unlit", renderQueue = queue };
            m.SetColor("_BaseColor", c);
            if (tex != null) m.SetTexture("_MainTex", tex);
            if (overlay) m.SetFloat("_ZTest", (float)CompareFunction.Always);
            unlitCache[key] = m;
            return m;
        }

        static int ColorKey(Color c) =>
            (Mathf.RoundToInt(c.r * 255) << 24) ^ (Mathf.RoundToInt(c.g * 255) << 16) ^ (Mathf.RoundToInt(c.b * 255) << 8) ^ Mathf.RoundToInt(c.a * 255);
    }

    /// <summary>
    /// The cartoon look shared by every toon material: ink line widths and colour, and the cel-shading globals
    /// (_BBToon, _BBShade, _BBRim, _BBOutlineScale) read by BananaBall/Lit. Change the statics and call Apply to retune live.
    /// </summary>
    public static class Look
    {
        // ink widths in metres at the object's distance; the shader clamps them to [InkMinPx, InkMaxPx] pixels at 1080p.
        // Bold comic ink: a near player gets a line about 1% of the screen wide, a fielder across the park still 2+ px.
        public const float PlayerInk = 0.034f, GearInk = 0.028f, BallInk = 0.012f, MascotInk = 0.07f;
        public const float InkMaxPx = 8.5f, InkMinPx = 2.3f, BallInkMaxPx = 2.6f;
        /// <summary>Pure black ink (no colour taken from the surface), so every player reads as a clean cut-out.</summary>
        public static readonly Color Ink = new Color(0.012f, 0.01f, 0.016f, 1f);
        public const float InkTint = 0f;

        public static float outlineScale = 1f;
        /// <summary>Ink hull pushed back behind the surface: x metres + y per metre of camera distance.</summary>
        public static Vector4 inkPush = new Vector4(0.018f, 0.0045f, 0f, 0f);
        /// <summary>x terminator in N.L, y softness of the tone edge, z gradient kept inside the lit tone, w sun spill into the shade.</summary>
        public static Vector4 toon = new Vector4(0.0f, 0.15f, 0.45f, 0.5f);
        /// <summary>rgb: cool tint of the shade tone, w: sun scale on the lit tone.</summary>
        public static Vector4 shade = new Vector4(0.84f, 0.83f, 0.96f, 0.92f);
        /// <summary>rgb: rim colour, w: where the rim starts (1 - N.V).</summary>
        public static Vector4 rim = new Vector4(0.24f, 0.19f, 0.16f, 0.66f);

        public static void Apply()
        {
            Shader.SetGlobalFloat("_BBOutlineScale", outlineScale);
            Shader.SetGlobalVector("_BBInkPush", inkPush);
            Shader.SetGlobalVector("_BBToon", toon);
            Shader.SetGlobalVector("_BBShade", shade);
            Shader.SetGlobalVector("_BBRim", rim);
        }
    }

    /// <summary>Late-afternoon look after Baseball Clash: a low warm sun, purple-to-peach sky with pink clouds, warm haze, and a light
    /// grade (tonemap, bloom, vignette). Values are for the project's Gamma colour space.</summary>
    public static class Env
    {
        public static readonly Color Horizon = new Color(0.97f, 0.71f, 0.56f);
        public static Volume volume;
        public static Vignette vignette;
        public static ColorAdjustments grade;

        public static Light Setup(Mats m, Camera cam)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.89f, 0.74f);
            sun.intensity = 0.98f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(40f, 62f, 0f);
            RenderSettings.sun = sun;
            Look.Apply();

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.33f, 0.32f, 0.37f));
            sh.AddDirectionalLight(Vector3.up, new Color(0.46f, 0.47f, 0.66f), 0.5f);
            sh.AddDirectionalLight(Vector3.down, new Color(0.34f, 0.27f, 0.2f), 0.36f);
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = sh;
            RenderSettings.ambientIntensity = 1f;

            m.skyMat.SetColor("_Zenith", new Color(0.33f, 0.38f, 0.74f));
            m.skyMat.SetColor("_Horizon", Horizon);
            m.skyMat.SetColor("_Ground", new Color(0.46f, 0.37f, 0.40f));
            m.skyMat.SetColor("_SunColor", new Color(1f, 0.84f, 0.62f));
            m.skyMat.SetColor("_CloudColor", new Color(1f, 0.64f, 0.52f));
            m.skyMat.SetColor("_CloudShade", new Color(0.74f, 0.36f, 0.40f));
            m.skyMat.SetFloat("_CloudCover", 0.4f);
            RenderSettings.skybox = m.skyMat;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = Tex.SkyCube(32, new Color(0.36f, 0.42f, 0.76f), Horizon, new Color(0.30f, 0.30f, 0.26f));
            RenderSettings.reflectionIntensity = 0.8f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 900f;

            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.None;

            var vgo = new GameObject("PostFX");
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            prof.name = "BB_Post";
            var tm = prof.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);
            grade = prof.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0f);
            grade.contrast.Override(10f);
            grade.saturation.Override(8f);
            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.62f);
            vignette = prof.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.42f);
            volume.sharedProfile = prof;
            return sun;
        }
    }

    /// <summary>Runtime-made textures (no imported art).</summary>
    public static partial class Tex
    {
        /// <summary>Tileable value-noise fbm, grey, for grass and dirt variation.</summary>
        public static Texture2D Detail(int size, int seed)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "BB_Detail", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[size * size];
            var r = new System.Random(seed);
            int[] periods = { 4, 8, 16, 32, 64 };
            float[] amps = { 0.34f, 0.26f, 0.18f, 0.13f, 0.09f };
            var lattices = new float[periods.Length][];
            for (int o = 0; o < periods.Length; o++)
            {
                int p = periods[o];
                lattices[o] = new float[p * p];
                for (int i = 0; i < p * p; i++) lattices[o][i] = (float)r.NextDouble();
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < periods.Length; o++)
                    {
                        int p = periods[o];
                        float fx = x / (float)size * p, fy = y / (float)size * p;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float ux = fx - x0, uy = fy - y0;
                        ux = ux * ux * (3f - 2f * ux); uy = uy * uy * (3f - 2f * uy);
                        int x1 = (x0 + 1) % p, y1 = (y0 + 1) % p;
                        x0 %= p; y0 %= p;
                        var L = lattices[o];
                        float a = Mathf.Lerp(L[y0 * p + x0], L[y0 * p + x1], ux);
                        float b = Mathf.Lerp(L[y1 * p + x0], L[y1 * p + x1], ux);
                        v += Mathf.Lerp(a, b, uy) * amps[o];
                    }
                    v = Mathf.Clamp01((v - 0.5f) * 1.6f + 0.5f + ((float)r.NextDouble() - 0.5f) * 0.12f);
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Pinstripe(Color stripe)
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, true) { name = "BB_Pinstripe", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[64 * 4];
            Color s = Color.Lerp(Color.white, stripe, 0.85f);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 64; x++)
                {
                    float w = x < 3 ? (x == 1 ? 1f : 0.45f) : 0f;
                    px[y * 64 + x] = Color.Lerp(Color.white, s, w);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Net(int size, int cell)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "BB_Net", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool line = (x % cell) == 0 || (y % cell) == 0;
                    px[y * size + x] = new Color(1f, 1f, 1f, line ? 1f : 0f);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Comic starburst for pop-up callouts.</summary>
        public static Texture2D Burst(int size, int spikes)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "BB_Burst", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            var r = new System.Random(5);
            var jag = new float[spikes * 2];
            for (int i = 0; i < jag.Length; i++) jag[i] = i % 2 == 0 ? 0.92f + (float)r.NextDouble() * 0.08f : 0.62f + (float)r.NextDouble() * 0.1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 0.5f;
                    float rr = Mathf.Sqrt(u * u + v * v);
                    float f = a * jag.Length;
                    int i0 = Mathf.FloorToInt(f) % jag.Length, i1 = (i0 + 1) % jag.Length;
                    float edge = Mathf.Lerp(jag[i0], jag[i1], f - Mathf.Floor(f));
                    float alpha = Mathf.Clamp01((edge - rr) * size * 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Cubemap SkyCube(int size, Color top, Color horizon, Color bottom)
        {
            var c = new Cubemap(size, TextureFormat.RGBA32, true) { name = "BB_SkyCube" };
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            var px = new Color[size * size];
            foreach (var f in faces)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                        Vector3 d;
                        switch (f)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                            default: d = new Vector3(-u, -v, -1f); break;
                        }
                        d.Normalize();
                        Color col = d.y >= 0f ? Color.Lerp(horizon, top, Mathf.Pow(d.y, 0.6f)) : Color.Lerp(horizon, bottom, Mathf.Clamp01(-d.y * 5f));
                        px[y * size + x] = col;
                    }
                c.SetPixels(px, f);
            }
            c.Apply(true, true);
            return c;
        }
    }
}
