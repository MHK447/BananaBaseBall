using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Menu icons are small toy models made with the same mesh kit and shader as the heroes. Each one is photographed
    /// once at start-up, cut out of its background and given an even dark sticker outline, so the lobby art matches
    /// the 3D characters instead of being flat symbols.
    /// </summary>
    public sealed class ColosseumIcons
    {
        const int Fine = 512, Final = 256;
        static readonly Color32 Edge = new Color32(30, 22, 62, 255);

        readonly Camera camera;
        readonly Transform rig;
        readonly RenderTexture target;
        readonly Texture2D reader;
        readonly Quaternion studio;
        readonly float[] reach = new float[Fine * Fine];
        readonly Color32[] fine = new Color32[Fine * Fine];

        public ColosseumIcons(Camera camera, Transform parent, Vector3 towardsLight)
        {
            this.camera = camera;
            // The little studio is turned so that the scene's sun arrives from the upper left of every icon.
            studio = Quaternion.FromToRotation(new Vector3(-.42f, .66f, -.62f).normalized, towardsLight.normalized);
            rig = new GameObject("Icon studio").transform;
            rig.SetParent(parent, false);
            rig.SetPositionAndRotation(parent.position + new Vector3(0f, 70f, 0f), studio);
            target = Kit.Own(new RenderTexture(Fine, Fine, 24, RenderTextureFormat.ARGB32) { name = "Colosseum icon studio", antiAliasing = 4 });
            reader = Kit.Own(new Texture2D(Fine, Fine, TextureFormat.RGBA32, false) { name = "Colosseum icon reader" });
        }

        /// <summary>
        /// Every stand-alone icon, keyed by the name the HUD asks for. One icon is made per step, so start-up can
        /// spread the work over a few frames. Places that are still locked also get a washed-out "-dim" copy.
        /// </summary>
        public IEnumerable<KeyValuePair<string, Sprite>> Models(Color32 lockedTone)
        {
            yield return Made("cup", Model("cup", Cup, new Vector3(-12f, 0f, 0f), 15f));
            yield return Made("coin", Model("coin", Coin, new Vector3(4f, -24f, 0f), 15f));
            yield return Made("swords", Model("swords", Swords, new Vector3(-6f, 0f, 0f), 11f));
            yield return Made("shield", Model("shield", kit => Badge(kit, 0xc9d3ea, 0x3f86f4, 0xffd24a, true), new Vector3(-4f, -14f, 0f), 11f));
            yield return Made("lock", Model("lock", Padlock, new Vector3(-6f, -18f, 0f), 15f));
            for (int league = 0; league < ColosseumSave.LeagueTone.Length; league++)
            {
                int tone = ColosseumSave.LeagueTone[league];
                yield return Made("medal" + league, Model("medal", kit => Badge(kit, tone, Blend(tone, 0x2a2466, .62f), 0xfff2c4, false), new Vector3(-4f, -12f, 0f), 12f));
            }
            Sprite chest = Model("chest", Chest, new Vector3(-17f, 27f, 0f), 11f);
            yield return Made("chest", chest);
            yield return Made("chest-dim", Muted(chest, lockedTone));
            Sprite banner = Model("banner", Banner, new Vector3(-4f, 18f, 0f), 11f);
            yield return Made("banner", banner);
            yield return Made("banner-dim", Muted(banner, lockedTone));
            Sprite crown = Model("crown", Crown, new Vector3(-20f, 0f, 0f), 11f);
            yield return Made("crown", crown);
            yield return Made("crown-dim", Muted(crown, lockedTone));
        }

        static KeyValuePair<string, Sprite> Made(string key, Sprite sprite) { return new KeyValuePair<string, Sprite>(key, sprite); }

        /// <summary>Builds a model inside a two-unit box (front towards -Z, up +Y), photographs it and returns the cut-out.</summary>
        public Sprite Model(string name, Action<MeshKit> build, Vector3 tilt, float outline, float view = 1.2f)
        {
            var kit = new MeshKit { Gradient = .2f };
            build(kit);
            Mesh mesh = kit.ToMesh("Colosseum icon " + name);
            GameObject item = Kit.Spawn("Icon " + name, rig, mesh, false, false);
            item.transform.localRotation = Quaternion.Euler(tilt);
            Sprite sprite = Shoot(name, rig.position, studio, view, outline);
            item.SetActive(false);
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(mesh);
            return sprite;
        }

        /// <summary>Head-and-shoulders cut-out of something already standing in the scene.</summary>
        public Sprite Bust(string name, Vector3 lookAt, float view, float outline)
        {
            return Shoot(name, lookAt, Quaternion.Euler(7f, 0f, 0f), view, outline);
        }

        /// <summary>Washed-out copy of an icon for things that are still locked.</summary>
        public static Sprite Muted(Sprite source, Color32 tone)
        {
            Color32[] pixels = source.texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                float light = (c.r * .30f + c.g * .59f + c.b * .11f) / 255f;
                float shade = .34f + light * .66f;
                pixels[i] = new Color32((byte)Mathf.Lerp(tone.r * shade, c.r, .16f), (byte)Mathf.Lerp(tone.g * shade, c.g, .16f),
                    (byte)Mathf.Lerp(tone.b * shade, c.b, .16f), c.a);
            }
            return Pack(source.name + " muted", pixels);
        }

        // ------------------------------------------------------------------ photographing

        Sprite Shoot(string name, Vector3 lookAt, Quaternion angle, float view, float outline)
        {
            RenderTexture before = camera.targetTexture;
            float size = camera.orthographicSize;
            Color back = camera.backgroundColor;
            camera.targetTexture = target;
            camera.orthographicSize = view;
            camera.transform.SetPositionAndRotation(lookAt - angle * Vector3.forward * 10f, angle);
            // The same picture on black and on white: where the two differ the background shows through.
            Color32[] onBlack = Snap(Color.black), onWhite = Snap(Color.white);
            camera.targetTexture = before;
            camera.orthographicSize = size;
            camera.backgroundColor = back;

            int count = Fine * Fine;
            for (int i = 0; i < count; i++)
            {
                Color32 k = onBlack[i], w = onWhite[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - k.r) + (w.g - k.g) + (w.b - k.b)) / 765f);
                if (alpha > .01f)
                {
                    float gain = 1f / alpha;
                    fine[i] = new Color32((byte)Mathf.Min(255f, k.r * gain), (byte)Mathf.Min(255f, k.g * gain),
                        (byte)Mathf.Min(255f, k.b * gain), (byte)(alpha * 255f + .5f));
                }
                else
                {
                    fine[i] = new Color32(Edge.r, Edge.g, Edge.b, 0);
                }
                reach[i] = alpha >= .5f ? 0f : 1e6f;
            }

            // Distance from every empty pixel to the model (two chamfer sweeps), which becomes the outline.
            const float straight = 1f, diagonal = 1.4142f;
            for (int y = 0; y < Fine; y++)
            {
                for (int x = 0; x < Fine; x++)
                {
                    int at = y * Fine + x;
                    float best = reach[at];
                    if (best <= 0f) continue;
                    if (x > 0) best = Mathf.Min(best, reach[at - 1] + straight);
                    if (y > 0)
                    {
                        best = Mathf.Min(best, reach[at - Fine] + straight);
                        if (x > 0) best = Mathf.Min(best, reach[at - Fine - 1] + diagonal);
                        if (x < Fine - 1) best = Mathf.Min(best, reach[at - Fine + 1] + diagonal);
                    }
                    reach[at] = best;
                }
            }
            for (int y = Fine - 1; y >= 0; y--)
            {
                for (int x = Fine - 1; x >= 0; x--)
                {
                    int at = y * Fine + x;
                    float best = reach[at];
                    if (best <= 0f) continue;
                    if (x < Fine - 1) best = Mathf.Min(best, reach[at + 1] + straight);
                    if (y < Fine - 1)
                    {
                        best = Mathf.Min(best, reach[at + Fine] + straight);
                        if (x < Fine - 1) best = Mathf.Min(best, reach[at + Fine + 1] + diagonal);
                        if (x > 0) best = Mathf.Min(best, reach[at + Fine - 1] + diagonal);
                    }
                    reach[at] = best;
                }
            }

            // Outline (a little heavier underneath, like a drop shadow) and a 2x2 shrink to the final size.
            int drop = Mathf.RoundToInt(outline * .6f);
            var pixels = new Color32[Final * Final];
            for (int y = 0; y < Final; y++)
            {
                for (int x = 0; x < Final; x++)
                {
                    float red = 0f, green = 0f, blue = 0f, cover = 0f;
                    for (int j = 0; j < 2; j++)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            int fx = x * 2 + i, fy = y * 2 + j, at = fy * Fine + fx;
                            Color32 c = fine[at];
                            float body = c.a / 255f, near = reach[at];
                            if (fy + drop < Fine) near = Mathf.Min(near, reach[at + drop * Fine]);
                            float seen = Mathf.Max(body, Mathf.Clamp01(outline + .5f - near));
                            red += (Edge.r + (c.r - Edge.r) * body) * seen;
                            green += (Edge.g + (c.g - Edge.g) * body) * seen;
                            blue += (Edge.b + (c.b - Edge.b) * body) * seen;
                            cover += seen;
                        }
                    }
                    pixels[y * Final + x] = cover > .001f
                        ? new Color32((byte)(red / cover), (byte)(green / cover), (byte)(blue / cover), (byte)(cover * 63.75f))
                        : new Color32(Edge.r, Edge.g, Edge.b, 0);
                }
            }
            return Pack("Colosseum icon " + name, pixels);
        }

        Color32[] Snap(Color background)
        {
            camera.backgroundColor = background;
            camera.Render();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            reader.ReadPixels(new Rect(0f, 0f, Fine, Fine), 0, 0, false);
            RenderTexture.active = active;
            return reader.GetPixels32();
        }

        static Sprite Pack(string name, Color32[] pixels)
        {
            var texture = Kit.Own(new Texture2D(Final, Final, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
            });
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            Sprite sprite = Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, Final, Final), new Vector2(.5f, .5f), 100f));
            sprite.name = name;
            return sprite;
        }

        // ------------------------------------------------------------------ outlines and colours

        static Color32 C(int hex, float gloss = .6f) { return MeshKit.Rgb(hex, gloss); }

        static int Blend(int from, int to, float amount)
        {
            int r = (int)Mathf.Lerp((from >> 16) & 255, (to >> 16) & 255, amount);
            int g = (int)Mathf.Lerp((from >> 8) & 255, (to >> 8) & 255, amount);
            int b = (int)Mathf.Lerp(from & 255, to & 255, amount);
            return (r << 16) | (g << 8) | b;
        }

        static Vector2[] StarRim(float outer, float inner, int points = 5)
        {
            var rim = new Vector2[points * 2];
            for (int i = 0; i < rim.Length; i++)
            {
                float angle = i * Mathf.PI / points, radius = (i & 1) == 0 ? outer : inner;
                rim[i] = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
            }
            return rim;
        }

        /// <summary>Heater shield: peaked top, straight shoulders, sides sweeping in to a point.</summary>
        static Vector2[] ShieldRim(float wide, float tall)
        {
            var half = new List<Vector2>
            {
                new Vector2(0f, tall), new Vector2(wide * .46f, tall * .84f), new Vector2(wide, tall * .9f), new Vector2(wide, tall * .1f)
            };
            for (int i = 1; i <= 6; i++)
            {
                float turn = i / 7f * Mathf.PI * .5f;
                half.Add(new Vector2(wide * Mathf.Cos(turn), tall * .1f - tall * 1.1f * Mathf.Sin(turn)));
            }
            var rim = new List<Vector2>(half);
            rim.Add(new Vector2(0f, -tall));
            for (int i = half.Count - 1; i >= 1; i--)
                rim.Add(new Vector2(-half[i].x, half[i].y));
            return rim.ToArray();
        }

        static void Arc(MeshKit kit, Vector3 centre, float radius, float fromDegrees, float toDegrees, float thick, Color32 color, int steps = 8)
        {
            Vector3 last = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float turn = Mathf.Lerp(fromDegrees, toDegrees, (float)i / steps) * Mathf.Deg2Rad;
                Vector3 point = centre + new Vector3(Mathf.Cos(turn), Mathf.Sin(turn), 0f) * radius;
                if (i > 0) kit.Capsule(last, point, thick, thick, color, 8);
                last = point;
            }
        }

        // ------------------------------------------------------------------ the models

        static void Cup(MeshKit kit)
        {
            Color32 gold = C(0xffc22e, .9f), light = C(0xffe27a, 1f), deep = C(0xe3941a, .8f), dark = C(0xa85e10, .3f);
            Color32 wood = C(0x6b4a8c, .3f), woodLight = C(0x8a66b4, .4f);
            kit.Lathe(new Vector3(0f, 0f, 0f), Quaternion.identity, new[]
            {
                new Vector2(0f, -.90f), new Vector2(.50f, -.90f), new Vector2(.50f, -.90f), new Vector2(.52f, -.68f), new Vector2(.52f, -.68f),
                new Vector2(.40f, -.64f), new Vector2(.40f, -.64f), new Vector2(.28f, -.57f), new Vector2(.12f, -.46f), new Vector2(.10f, -.30f),
                new Vector2(.16f, -.17f), new Vector2(.38f, -.05f), new Vector2(.53f, .20f), new Vector2(.60f, .52f), new Vector2(.62f, .78f),
                new Vector2(.67f, .86f), new Vector2(.67f, .86f), new Vector2(.57f, .86f), new Vector2(.57f, .86f), new Vector2(.48f, .64f),
                new Vector2(0f, .56f)
            }, new[]
            {
                wood, wood, wood, woodLight, woodLight,
                woodLight, deep, gold, gold, deep,
                gold, gold, gold, light, light,
                light, light, light, dark, dark,
                dark
            }, 32);
            for (int side = -1; side <= 1; side += 2)
                Arc(kit, new Vector3(side * .60f, .45f, 0f), .27f, side > 0 ? 78f : 102f, side > 0 ? -104f : 284f, .062f, gold);
            kit.Slab(new Vector3(0f, .40f, -.62f), Quaternion.identity, StarRim(.21f, .095f), .1f, .03f, C(0xfff6c8, 1f), light, deep);
        }

        static void Coin(MeshKit kit)
        {
            Color32 gold = C(0xffc832, .9f), light = C(0xffe888, 1f), deep = C(0xe8981a, .8f);
            kit.Lathe(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new[]
            {
                new Vector2(.84f, -.13f), new Vector2(.90f, -.08f), new Vector2(.90f, .08f), new Vector2(.86f, .13f), new Vector2(.86f, .13f),
                new Vector2(.69f, .13f), new Vector2(.69f, .13f), new Vector2(.63f, .05f), new Vector2(.63f, .05f), new Vector2(0f, .05f)
            }, new[] { deep, deep, gold, light, light, light, deep, deep, gold, gold }, 44);
            kit.Slab(new Vector3(0f, -.02f, -.10f), Quaternion.identity, StarRim(.43f, .2f), .1f, .035f, light, C(0xfff8cc, 1f), deep);
        }

        /// <summary>Shield-shaped badge: a metal rim, a coloured field and a star, optionally with a little crown on top.</summary>
        static void Badge(MeshKit kit, int rimHex, int fieldHex, int starHex, bool crowned)
        {
            Quaternion none = Quaternion.identity;
            Color32 rim = C(rimHex, .9f), rimLight = C(Blend(rimHex, 0xffffff, .5f), 1f), rimDeep = C(Blend(rimHex, 0x241c4a, .38f), .6f);
            Color32 field = C(fieldHex, .7f), fieldLight = C(Blend(fieldHex, 0xffffff, .3f), .8f), fieldDeep = C(Blend(fieldHex, 0x161038, .4f), .5f);
            Color32 star = C(starHex, 1f), starLight = C(Blend(starHex, 0xffffff, .6f), 1f), starDeep = C(Blend(starHex, 0x8a4a10, .45f), .7f);
            float lift = crowned ? -.1f : 0f;
            kit.Slab(new Vector3(0f, lift, 0f), none, ShieldRim(.78f, .86f), .30f, .09f, rim, rimLight, rimDeep);
            kit.Slab(new Vector3(0f, lift + .01f, -.17f), none, ShieldRim(.57f, .64f), .1f, .05f, field, fieldLight, fieldDeep);
            kit.Slab(new Vector3(0f, lift + .05f, -.25f), none, StarRim(.36f, .16f), .12f, .04f, star, starLight, starDeep);
            if (!crowned) return;
            Color32 gold = C(0xffc832, .9f), goldLight = C(0xffe888, 1f), goldDeep = C(0xe0901a, .7f);
            kit.Slab(new Vector3(0f, .80f, -.06f), none, new[]
            {
                new Vector2(-.40f, -.14f), new Vector2(.40f, -.14f), new Vector2(.46f, .16f), new Vector2(.22f, 0f), new Vector2(0f, .22f),
                new Vector2(-.22f, 0f), new Vector2(-.46f, .16f)
            }, .2f, .04f, gold, goldLight, goldDeep);
            for (int i = -1; i <= 1; i++)
                kit.Ball(new Vector3(i * .46f, i == 0 ? 1.04f : .98f, -.06f), .07f, goldLight, 8);
        }

        static void Swords(MeshKit kit)
        {
            Color32 steel = C(0xe8f0fb, 1f), steelLight = C(0xffffff, 1f), steelDeep = C(0x93a4c4, .8f), groove = C(0xb4c2dc, .8f);
            Color32 gold = C(0xffc22e, .9f), goldLight = C(0xffe27a, 1f), grip = C(0x8a4f2c, .25f), gripDark = C(0x5e3420, .2f);
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, side * 40f);
                Vector3 hub = new Vector3(0f, .12f, side * .1f);
                Vector3 At(float x, float y, float z) { return hub + turn * new Vector3(x, y, z); }
                kit.Slab(At(0f, 0f, 0f), turn, new[]
                {
                    new Vector2(-.135f, -.56f), new Vector2(.135f, -.56f), new Vector2(.135f, .58f), new Vector2(0f, .86f), new Vector2(-.135f, .58f)
                }, .1f, .055f, steel, steelLight, steelDeep);
                kit.Slab(At(0f, -.02f, -.045f), turn, new[]
                {
                    new Vector2(-.03f, -.5f), new Vector2(.03f, -.5f), new Vector2(.03f, .44f), new Vector2(0f, .52f), new Vector2(-.03f, .44f)
                }, .03f, .012f, groove, groove, groove);
                kit.Blob(At(0f, -.59f, 0f), new Vector3(.34f, .075f, .11f), turn, gold, .5f, .5f, 12);
                kit.Ball(At(-.34f, -.59f, 0f), .095f, goldLight, 10);
                kit.Ball(At(.34f, -.59f, 0f), .095f, goldLight, 10);
                kit.Capsule(At(0f, -.66f, 0f), At(0f, -.96f, 0f), .078f, .072f, grip, 10);
                kit.Blob(At(0f, -.76f, 0f), new Vector3(.086f, .022f, .086f), turn, gripDark, 1f, 1f, 8);
                kit.Blob(At(0f, -.87f, 0f), new Vector3(.084f, .022f, .084f), turn, gripDark, 1f, 1f, 8);
                kit.Ball(At(0f, -1.03f, 0f), .12f, gold, 12);
            }
        }

        static void Chest(MeshKit kit)
        {
            Quaternion none = Quaternion.identity;
            Color32 wood = C(0xb46a34, .3f), woodLight = C(0xcf8444, .35f), gold = C(0xffc22e, .9f), goldLight = C(0xffe27a, 1f), dark = C(0x4a2a1c, .1f);
            kit.Blob(new Vector3(0f, -.34f, 0f), new Vector3(.80f, .42f, .54f), none, wood, .26f, .26f, 22);
            kit.Blob(new Vector3(0f, .06f, 0f), new Vector3(.82f, .50f, .56f), none, woodLight, .9f, .3f, 22);
            kit.Blob(new Vector3(0f, .08f, 0f), new Vector3(.86f, .06f, .60f), none, gold, .5f, .26f, 22);
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Blob(new Vector3(side * .50f, -.34f, 0f), new Vector3(.085f, .44f, .565f), none, gold, .26f, .26f, 16);
                kit.Blob(new Vector3(side * .50f, .06f, 0f), new Vector3(.085f, .525f, .585f), none, gold, .9f, .3f, 16);
            }
            kit.Blob(new Vector3(0f, -.05f, -.58f), new Vector3(.17f, .2f, .07f), none, goldLight, .5f, .5f, 12);
            kit.Ball(new Vector3(0f, -.02f, -.65f), .055f, dark, 8);
            kit.Blob(new Vector3(0f, -.11f, -.65f), new Vector3(.026f, .07f, .02f), none, dark, 1f, 1f, 6);
        }

        static void Banner(MeshKit kit)
        {
            Quaternion none = Quaternion.identity;
            Color32 cloth = C(Kit.TeamMain[0], .4f), clothLight = C(Kit.TeamLight[0], .5f), clothDeep = C(Kit.TeamDark[0], .3f);
            Color32 gold = C(0xffc22e, .9f), goldLight = C(0xffe27a, 1f), goldDeep = C(0xe0901a, .7f), pole = C(0x8a5a34, .3f);
            kit.Capsule(new Vector3(-.60f, -.96f, .04f), new Vector3(-.60f, .82f, .04f), .06f, .06f, pole, 10);
            kit.Ball(new Vector3(-.60f, .92f, .04f), .12f, gold, 12);
            kit.Capsule(new Vector3(-.70f, .64f, 0f), new Vector3(.62f, .64f, 0f), .055f, .055f, gold, 10);
            kit.Ball(new Vector3(.66f, .64f, 0f), .085f, goldLight, 10);
            kit.Slab(new Vector3(.02f, 0f, 0f), none, new[]
            {
                new Vector2(-.50f, .62f), new Vector2(.50f, .62f), new Vector2(.50f, -.70f), new Vector2(0f, -.42f), new Vector2(-.50f, -.70f)
            }, .09f, .05f, cloth, clothLight, clothDeep);
            kit.Slab(new Vector3(.02f, .10f, -.08f), none, StarRim(.30f, .135f), .08f, .03f, gold, goldLight, goldDeep);
        }

        static void Crown(MeshKit kit)
        {
            Color32 gold = C(0xffc22e, .9f), light = C(0xffe27a, 1f), deep = C(0xe0901a, .8f), velvet = C(0xd8405a, .2f);
            kit.Blob(new Vector3(0f, -.02f, 0f), new Vector3(.56f, .46f, .56f), Quaternion.identity, velvet, 1f, 1f, 20);
            kit.Lathe(new Vector3(0f, 0f, 0f), Quaternion.identity, new[]
            {
                new Vector2(.60f, -.56f), new Vector2(.68f, -.50f), new Vector2(.66f, -.42f), new Vector2(.64f, -.18f), new Vector2(.68f, -.12f),
                new Vector2(.60f, -.10f)
            }, new[] { deep, gold, gold, light, light, deep }, 36);
            for (int i = 0; i < 6; i++)
            {
                float turn = (i * 60f + 30f) * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(turn) * .62f, -.16f, Mathf.Sin(turn) * .62f);
                var tip = new Vector3(Mathf.Cos(turn) * .74f, .50f, Mathf.Sin(turn) * .74f);
                kit.Capsule(foot, tip, .17f, .035f, gold, 10);
                kit.Ball(tip, .1f, light, 10);
            }
            int[] gems = { 0x44a2ff, 0xff5a6a, 0x44a2ff };
            for (int i = 0; i < 3; i++)
            {
                float turn = (-90f + (i - 1) * 52f) * Mathf.Deg2Rad;
                kit.Blob(new Vector3(Mathf.Cos(turn) * .67f, -.31f, Mathf.Sin(turn) * .67f), new Vector3(.085f, .1f, .085f), Quaternion.identity, C(gems[i], 1f), 1f, 1f, 10);
            }
        }

        static void Padlock(MeshKit kit)
        {
            Color32 gold = C(0xffc22e, .9f), light = C(0xffe27a, 1f), steel = C(0xd6e0f2, 1f), dark = C(0x3a2848, .1f);
            Arc(kit, new Vector3(0f, .16f, 0f), .36f, 0f, 180f, .1f, steel, 10);
            for (int side = -1; side <= 1; side += 2)
                kit.Capsule(new Vector3(side * .36f, .16f, 0f), new Vector3(side * .36f, -.1f, 0f), .1f, .1f, steel, 10);
            kit.Blob(new Vector3(0f, -.36f, 0f), new Vector3(.62f, .48f, .28f), Quaternion.identity, gold, .32f, .32f, 20);
            kit.Blob(new Vector3(0f, -.36f, -.24f), new Vector3(.46f, .33f, .06f), Quaternion.identity, light, .4f, .4f, 16);
            kit.Ball(new Vector3(0f, -.30f, -.30f), .1f, dark, 10);
            kit.Blob(new Vector3(0f, -.46f, -.30f), new Vector3(.045f, .12f, .03f), Quaternion.identity, dark, 1f, 1f, 6);
        }
    }
}
