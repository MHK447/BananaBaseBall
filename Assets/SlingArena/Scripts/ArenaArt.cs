using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena
{
    public static class ArenaArt
    {
        public enum Part
        {
            Hips, Torso, Head,
            LeftUpperArm, RightUpperArm, LeftForearm, RightForearm,
            LeftThigh, RightThigh, LeftShin, RightShin
        }

        public enum WeaponStyle { Sword, Axe, Hammer }

        static readonly Color Ink = Rgb(0x242237, .12f);
        static readonly Color Leather = Rgb(0x725044, .18f);
        static readonly Color LeatherDark = Rgb(0x483345, .22f);
        static readonly Color Silver = Rgb(0xCACFE7, .69f);
        static readonly Color Steel = Rgb(0x64779F, .56f);
        static readonly Color Gold = Rgb(0xFABC53, .56f);
        static readonly Color Skin = Rgb(0xFAC19A, .24f);
        static readonly Color Cream = Rgb(0xFFF2D4, .30f);
        static readonly Color SkyStone = Rgb(0x8496A1, .06f);

        static readonly Palette Blue = new Palette(0x3C8CE0, 0x5DB6F5, 0x25528C, 0xF9CE66);
        static readonly Palette Coral = new Palette(0xD5523B, 0xF08A54, 0x963D43, 0xFFBB58);

        static readonly Dictionary<(Part, bool), Model> Fighters = new Dictionary<(Part, bool), Model>();
        static readonly Dictionary<(WeaponStyle, bool), Model> Weapons =
            new Dictionary<(WeaponStyle, bool), Model>();

        static Material surfaceMaterial;
        static Material outlineMaterial;
        static Material sandstoneMaterial;
        static Mesh board;
        static Mesh backdrop;
        static Mesh foreground;
        static Mesh teamPennants;
        static Primitive roundedBox;
        static Primitive sphere;
        static Primitive cylinder;

        public static void BuildEnvironment(Transform parent)
        {
            GameObject court = Resources.Load<GameObject>("SlingArenaVoodooCourt");
            Shader sandstone = Resources.Load<Shader>("SlingArenaSandstone");
            if (court && sandstone)
            {
                if (!sandstoneMaterial)
                    sandstoneMaterial = new Material(sandstone) { name = "Sunlit court wood and sand" };
                GameObject visual = Object.Instantiate(court, parent, false);
                visual.name = "Blender tournament court";
                foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterial = sandstoneMaterial;
            }
            else
            {
                if (!board)
                {
                    board = MakeBoard();
                    backdrop = MakeStands();
                    foreground = MakeOuterGround();
                }
                var fallback = new GameObject("Portrait court fallback").transform;
                fallback.SetParent(parent, false);
                fallback.localScale = new Vector3(.54f, 1f, 1.30f);
                Display(fallback, "Painted sandstone board", board, false);
                Display(fallback, "Timber stands and portcullis", backdrop, false);
                Display(fallback, "Sand below the tabletop", foreground, false);
            }

            if (!teamPennants)
                teamPennants = MakeTeamPennants();
            Display(parent, "Team pennants", teamPennants, false);

            var ground = new GameObject("Arena floor");
            ground.transform.SetParent(parent, false);
            var collider = ground.AddComponent<BoxCollider>();
            collider.size = new Vector3(8.15f, .9f, 11.95f);
            collider.center = new Vector3(0f, -.45f, 0f);
        }

        static Mesh MakeTeamPennants()
        {
            var painter = new Painter();
            Color timber = Rgb(0x754730);
            Color trim = Rgb(0xF6BD57, .42f);
            for (int side = -1; side <= 1; side += 2)
            {
                Color cloth = side < 0 ? Rgb(0x2D86D5) : Rgb(0xD9533E);
                Color fold = side < 0 ? Rgb(0x2268AE) : Rgb(0xB73536);
                float z = 5.84f * side;
                float y = side < 0 ? .81f : 1.25f;
                for (int post = -1; post <= 1; post += 2)
                {
                    float x = post * 2.71f;
                    painter.Box(new Vector3(x, y * .53f, z), new Vector3(.10f, y * 1.06f, .10f), timber);
                    painter.Cylinder(new Vector3(x, y + .028f, z), new Vector3(.16f, .085f, .16f), trim);
                    painter.Box(new Vector3(x, y - .125f, z - .08f), new Vector3(.76f, .082f, .107f), timber);
                    Vector3 upperLeft = new Vector3(x - .29f, y - .12f, z - .15f);
                    Vector3 upperRight = upperLeft + Vector3.right * .58f;
                    Vector3 lowerLeft = upperLeft + new Vector3(.030f, -.43f, -.047f);
                    Vector3 lowerRight = upperRight + new Vector3(-.034f, -.41f, -.026f);
                    Vector3 point = new Vector3(x, y - .72f, z - .23f);
                    Vector3 topFold = (upperLeft + upperRight) * .5f + Vector3.back * .019f;
                    Vector3 lowerFold = (lowerLeft + lowerRight) * .5f + Vector3.back * .029f;
                    painter.Quad(upperLeft, topFold, lowerFold, lowerLeft, Vector3.back, cloth);
                    painter.Quad(topFold, upperRight, lowerRight, lowerFold, Vector3.back, fold);
                    painter.Triangle(lowerLeft, lowerFold, point, Vector3.back, cloth);
                    painter.Triangle(lowerFold, lowerRight, point, Vector3.back, fold);
                    Vector3 crest = new Vector3(x, y - .322f, z - .218f);
                    painter.Quad(crest + Vector3.up * .108f, crest + Vector3.right * .091f,
                        crest + Vector3.down * .108f, crest + Vector3.left * .091f, Vector3.back, trim);
                }
            }
            return painter.Bake("Tournament pennants").Color;
        }

        public static void AttachFighter(Part part, Transform bodyAnchor, bool player)
        {
            if (part == Part.Head && AttachKnightModel(bodyAnchor, player, "Head"))
                return;

            if (!Fighters.TryGetValue((part, player), out var model) || !model.Color)
            {
                model = MakeFighterPart(part, player);
                Fighters[(part, player)] = model;
            }

            Display(bodyAnchor, part + " color", model.Color, false);
            Display(bodyAnchor, part + " ink", model.Ink, true);
        }

        public static void AttachWeapon(Transform weaponAnchor, WeaponStyle style, bool player)
        {
            if (style == WeaponStyle.Sword && AttachKnightModel(weaponAnchor, player, "Blade"))
                return;

            if (!Weapons.TryGetValue((style, player), out var model) || !model.Color)
            {
                model = MakeWeapon(style, player);
                Weapons[(style, player)] = model;
            }

            Display(weaponAnchor, style + " color", model.Color, false);
            Display(weaponAnchor, style + " ink", model.Ink, true);
        }

        static bool AttachKnightModel(Transform parent, bool player, string part)
        {
            string name = (player ? "KnightBlue" : "KnightCoral") + part;
            GameObject mesh = Resources.Load<GameObject>("SlingArenaKnights/" + name);
            if (!mesh)
                return false;

            GameObject visual = Object.Instantiate(mesh, parent, false);
            visual.name = name;
            if (part == "Head")
                visual.transform.localRotation *= Quaternion.Euler(0f, player ? 20f : -20f, 0f);
            foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                bool ink = renderer.name.IndexOf("Silhouette", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Material material = MaterialFor(ink);
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = material;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ink ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = !ink;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return true;
        }

        static void Display(Transform parent, string name, Mesh mesh, bool outline)
        {
            if (!mesh) return;
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(outline);
            renderer.shadowCastingMode = outline ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = !outline;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Material MaterialFor(bool outline)
        {
            if (!surfaceMaterial)
            {
                var shader = Resources.Load<Shader>("SlingArenaToySurface");
                if (!shader) shader = Shader.Find("SlingArena/Toy Surface");
                surfaceMaterial = new Material(shader) { name = "Painted toy surface", hideFlags = HideFlags.DontSave };
                outlineMaterial = new Material(shader) { name = "Toy silhouette ink", hideFlags = HideFlags.DontSave };
                outlineMaterial.SetColor("_InkColor", Rgb(0x272337));
                outlineMaterial.SetFloat("_InkWidth", .019f);
                outlineMaterial.SetFloat("_InkMode", 1f);
                outlineMaterial.SetFloat("_Cull", (float)CullMode.Front);
            }

            return outline ? outlineMaterial : surfaceMaterial;
        }

        static Model MakeFighterPart(Part part, bool player)
        {
            var painter = new Painter();
            Palette palette = player ? Blue : Coral;
            switch (part)
            {
                case Part.Hips:
                    painter.Box(new Vector3(0f,-.015f,0f), new Vector3(.64f,.35f,.47f), palette.Dark, true);
                    painter.Box(new Vector3(0f,.10f,.015f), new Vector3(.67f,.14f,.50f), LeatherDark);
                    painter.Box(new Vector3(0f,.10f,.266f), new Vector3(.18f,.14f,.052f), Gold);
                    painter.Box(new Vector3(0f,.10f,.293f), new Vector3(.088f,.06f,.021f), Leather);
                    for (int side = -1; side <= 1; side += 2)
                        painter.Box(new Vector3(side * .26f,-.11f,.15f), new Vector3(.27f,.24f,.19f),
                            palette.Base, false, new Vector3(0f,0f,side * 11f));
                    break;
                case Part.Torso:
                    PaintChest(painter, palette, player);
                    break;
                case Part.Head:
                    PaintHelmet(painter, palette, player);
                    break;
                case Part.LeftUpperArm:
                case Part.RightUpperArm:
                    PaintUpperArm(painter, palette, part == Part.LeftUpperArm ? -1 : 1);
                    break;
                case Part.LeftForearm:
                case Part.RightForearm:
                    PaintForearm(painter, palette);
                    break;
                case Part.LeftThigh:
                case Part.RightThigh:
                    painter.Box(new Vector3(0f,.015f,0f), new Vector3(.285f,.44f,.31f), palette.Dark, true);
                    painter.Box(new Vector3(0f,.045f,.124f), new Vector3(.21f,.28f,.12f), palette.Base);
                    painter.Ellipsoid(new Vector3(0f,-.151f,.118f), new Vector3(.23f,.18f,.13f), Steel);
                    break;
                case Part.LeftShin:
                case Part.RightShin:
                    painter.Box(new Vector3(0f,.042f,.005f), new Vector3(.248f,.258f,.29f), palette.Base, true);
                    painter.Box(new Vector3(0f,-.104f,.083f), new Vector3(.32f,.19f,.47f), LeatherDark, true);
                    painter.Box(new Vector3(0f,-.141f,.093f), new Vector3(.33f,.083f,.475f), Leather);
                    painter.Box(new Vector3(0f,-.083f,.256f), new Vector3(.24f,.107f,.13f), palette.Trim);
                    painter.Ellipsoid(new Vector3(0f,.097f,.155f), new Vector3(.14f,.14f,.052f), palette.Light);
                    break;
            }

            return painter.Bake((player ? "Blue " : "Coral ") + part);
        }

        static void PaintChest(Painter painter, Palette palette, bool player)
        {
            painter.Box(new Vector3(0f,-.040f,0f), new Vector3(.72f,.61f,.49f), palette.Base, true);
            painter.Box(new Vector3(0f,.24f,-.01f), new Vector3(.54f,.17f,.42f), palette.Trim, true);
            painter.Box(new Vector3(0f,.155f,.205f), new Vector3(.64f,.18f,.15f), palette.Light);
            painter.Box(new Vector3(0f,-.18f,.195f), new Vector3(.55f,.12f,.17f), palette.Dark);
            painter.Box(new Vector3(0f,.255f,-.126f), new Vector3(.45f,.11f,.15f), LeatherDark);
            painter.Box(new Vector3(0f,.068f,-.21f), new Vector3(.60f,.48f,.11f), palette.Dark);
            painter.Box(new Vector3(0f,.071f,.281f), new Vector3(.36f,.36f,.06f), Ink,
                false, new Vector3(0f,0f,45f));
            painter.Box(new Vector3(0f,.071f,.31f), new Vector3(.25f,.25f,.042f), palette.Trim,
                false, new Vector3(0f,0f,45f));
            if (player)
            {
                painter.Box(new Vector3(0f,.074f,.344f), new Vector3(.055f,.25f,.031f), Cream);
                painter.Box(new Vector3(0f,.074f,.344f), new Vector3(.25f,.055f,.031f), Cream);
            }
            else
            {
                painter.Box(new Vector3(-.053f,.071f,.343f), new Vector3(.044f,.19f,.031f), Cream,
                    false,new Vector3(0f,0f,-24f));
                painter.Box(new Vector3(.053f,.071f,.343f), new Vector3(.044f,.19f,.031f), Cream,
                    false,new Vector3(0f,0f,24f));
            }

            for (int side = -1; side <= 1; side += 2)
            {
                painter.Ellipsoid(new Vector3(side * .239f,.163f,.287f), new Vector3(.071f,.071f,.052f), Gold);
                painter.Box(new Vector3(side * .27f,-.094f,.236f), new Vector3(.10f,.21f,.08f),
                    palette.Light,false,new Vector3(0f,0f,side * 9f));
            }
        }

        static void PaintHelmet(Painter painter, Palette palette, bool player)
        {
            painter.Ellipsoid(new Vector3(0f,-.082f,.037f), new Vector3(.55f,.52f,.58f), Skin, true);
            painter.Box(new Vector3(0f,-.185f,-.018f), new Vector3(.52f,.17f,.49f), palette.Dark);
            if (player)
            {
                painter.Ellipsoid(new Vector3(0f,.086f,-.042f), new Vector3(.77f,.69f,.72f), palette.Base, true);
                painter.Box(new Vector3(0f,.073f,.255f), new Vector3(.73f,.16f,.15f), palette.Light, true);
                painter.Box(new Vector3(0f,.241f,.182f), new Vector3(.15f,.29f,.18f), Silver);
                painter.Box(new Vector3(0f,.274f,.206f), new Vector3(.06f,.24f,.14f), Cream);
                painter.Box(new Vector3(0f, .407f, -.026f), new Vector3(.43f, .087f, .39f),
                    palette.Dark, true);
                for (int side = -1; side <= 1; side += 2)
                    painter.Box(new Vector3(side * .083f, .458f, -.019f), new Vector3(.106f, .072f, .269f),
                        Gold, false, new Vector3(0f, -side * 38f, 0f));
                painter.Ellipsoid(new Vector3(0f, .499f, .084f), new Vector3(.110f, .028f, .105f), Cream);
            }
            else
            {
                painter.Box(new Vector3(0f,.089f,-.042f), new Vector3(.70f,.63f,.68f), palette.Base, true,
                    new Vector3(-4f,0f,0f));
                painter.Box(new Vector3(0f,.061f,.269f), new Vector3(.76f,.13f,.14f), palette.Trim, true);
                painter.Box(new Vector3(0f,.209f,.297f), new Vector3(.46f,.15f,.093f), palette.Light);
                painter.Box(new Vector3(0f, .393f, -.039f), new Vector3(.52f, .116f, .47f), palette.Dark);
                painter.Box(new Vector3(0f, .454f, -.047f), new Vector3(.47f, .085f, .151f), palette.Trim, true);
                painter.Box(new Vector3(0f, .456f, .107f), new Vector3(.36f, .057f, .078f), palette.Light);
                for (int side = -1; side <= 1; side += 2)
                    painter.Ellipsoid(new Vector3(side * .164f, .501f, -.044f),
                        new Vector3(.072f, .029f, .072f), Cream);
                for (int i = -1; i <= 1; i++)
                    painter.Box(new Vector3(i * .111f,.205f,.346f), new Vector3(.038f,.091f,.032f),
                        palette.Dark, false, new Vector3(0f,0f,-13f));
            }

            painter.Ellipsoid(new Vector3(0f,-.081f,.342f), new Vector3(.17f,.14f,.15f), Skin);
            for (int side = -1; side <= 1; side += 2)
            {
                painter.Box(new Vector3(side * .319f,-.056f,.092f), new Vector3(.177f,.31f,.27f),
                    palette.Dark, true, new Vector3(0f,0f,side * 7f));
                painter.Ellipsoid(new Vector3(side * .399f,-.025f,.088f), new Vector3(.074f,.125f,.125f), Gold);
                painter.Ellipsoid(new Vector3(side * .139f,-.049f,.302f), new Vector3(.172f,.174f,.102f), Cream,
                    false, new Vector3(0f,side * 12f,0f));
                painter.Ellipsoid(new Vector3(side * .134f,-.050f,.350f), new Vector3(.074f,.088f,.040f), Ink);
                painter.Ellipsoid(new Vector3(side * .130f-.012f,-.026f,.369f), new Vector3(.023f,.023f,.014f), Cream);
                painter.Box(new Vector3(side * .145f,.029f,.326f), new Vector3(.14f,.036f,.031f),
                    Ink,false,new Vector3(0f,0f,side * (player ? -10f : 21f)));
            }

            painter.Box(new Vector3(0f,-.158f,.321f), new Vector3(.162f,.038f,.037f), LeatherDark,
                false, new Vector3(0f,0f,player ? 8f : -8f));
        }

        static void PaintUpperArm(Painter painter, Palette palette, int side)
        {
            painter.Box(new Vector3(0f,-.041f,0f), new Vector3(.25f,.39f,.24f), palette.Base, true);
            painter.Ellipsoid(new Vector3(side * .025f,.116f,-.014f), new Vector3(.35f,.25f,.37f),
                palette.Light, true, new Vector3(0f,0f,side * 14f));
            painter.Box(new Vector3(side * .044f,.051f,.137f), new Vector3(.235f,.085f,.102f), palette.Trim);
            painter.Ellipsoid(new Vector3(0f,-.174f,.016f), new Vector3(.205f,.207f,.225f), palette.Dark);
            painter.Ellipsoid(new Vector3(side * .041f, .228f, -.008f),
                new Vector3(.172f, .054f, .172f), palette.Dark);
            painter.Ellipsoid(new Vector3(side * .041f, .255f, -.008f),
                new Vector3(.109f, .030f, .109f), palette.Trim);
        }

        static void PaintForearm(Painter painter, Palette palette)
        {
            painter.Box(new Vector3(0f,.023f,0f), new Vector3(.243f,.306f,.235f), palette.Base, true);
            painter.Box(new Vector3(0f,-.043f,.092f), new Vector3(.231f,.225f,.118f), palette.Light);
            painter.Box(new Vector3(0f,-.10f,0f), new Vector3(.266f,.087f,.262f), palette.Dark);
            painter.Ellipsoid(new Vector3(0f,-.166f,.02f), new Vector3(.25f,.195f,.245f), Skin, true);
            painter.Ellipsoid(new Vector3(.083f,-.158f,.103f), new Vector3(.11f,.12f,.12f), Skin);
        }

        static Model MakeWeapon(WeaponStyle style, bool player)
        {
            var painter = new Painter();
            Palette palette = player ? Blue : Coral;
            switch (style)
            {
                case WeaponStyle.Sword:
                    painter.Cylinder(new Vector3(0f,0f,.107f), new Vector3(.114f,.265f,.114f), LeatherDark,
                        true,new Vector3(90f,0f,0f));
                    for (int i = 0; i < 4; i++)
                        painter.Cylinder(new Vector3(0f,0f,.025f + .05f * i), new Vector3(.125f,.021f,.125f),
                            palette.Base,false,new Vector3(90f,0f,0f));
                    painter.Ellipsoid(new Vector3(0f,0f,-.023f), new Vector3(.16f,.115f,.117f), Gold, true);
                    painter.Box(new Vector3(0f, 0f, .242f), new Vector3(.35f, .105f, .122f), Gold, true);
                    painter.Box(new Vector3(0f, .054f, .241f), new Vector3(.167f, .032f, .094f), palette.Base);
                    painter.Plate(new[] {
                        new Vector2(-.133f, .290f), new Vector2(.133f, .290f), new Vector2(.149f, .723f),
                        new Vector2(.091f, .880f), new Vector2(0f, 1.020f), new Vector2(-.091f, .880f),
                        new Vector2(-.149f, .723f)
                    }, new Vector2(0f, .620f), .064f, Silver, Steel, true);
                    painter.Plate(new[] {
                        new Vector2(-.042f, .345f), new Vector2(.042f, .345f),
                        new Vector2(.040f, .740f), new Vector2(0f, .919f), new Vector2(-.040f, .740f)
                    }, new Vector2(0f, .620f), .045f, Rgb(0xFAFCF8, .70f), Silver, false, .025f);
                    break;
                case WeaponStyle.Axe:
                    painter.Cylinder(new Vector3(0f,0f,.378f), new Vector3(.112f,.82f,.112f), Leather,
                        true,new Vector3(90f,0f,0f));
                    painter.Box(new Vector3(0f,0f,.15f), new Vector3(.117f,.104f,.30f), palette.Dark, true);
                    painter.Box(new Vector3(0f,0f,.70f), new Vector3(.151f,.118f,.23f), Gold, true);
                    painter.Plate(new[] {
                        new Vector2(-.046f,.615f), new Vector2(.075f,.619f), new Vector2(.21f,.531f),
                        new Vector2(.255f,.625f), new Vector2(.26f,.758f), new Vector2(.204f,.913f),
                        new Vector2(.095f,.831f), new Vector2(-.046f,.794f)
                    }, new Vector2(.13f,.733f), .067f, Silver, Steel, true);
                    painter.Box(new Vector3(.014f,.072f,.703f), new Vector3(.103f,.026f,.103f),
                        palette.Light,false,new Vector3(0f,45f,0f));
                    break;
                case WeaponStyle.Hammer:
                    painter.Cylinder(new Vector3(0f,0f,.388f), new Vector3(.117f,.84f,.117f), Leather,
                        true,new Vector3(90f,0f,0f));
                    painter.Box(new Vector3(0f,0f,.147f), new Vector3(.116f,.106f,.32f), palette.Dark, true);
                    painter.Box(new Vector3(0f,0f,.794f), new Vector3(.36f,.245f,.31f), palette.Base, true);
                    painter.Box(new Vector3(0f,.125f,.794f), new Vector3(.206f,.062f,.225f), palette.Trim);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        painter.Box(new Vector3(side * .158f,0f,.795f), new Vector3(.067f,.225f,.276f), Silver);
                        painter.Box(new Vector3(side * .197f,0f,.795f), new Vector3(.067f,.216f,.25f), palette.Dark, true);
                    }
                    painter.Box(new Vector3(0f,0f,.941f), new Vector3(.14f,.14f,.06f), Gold);
                    break;
            }

            return painter.Bake((player ? "Blue " : "Coral ") + style);
        }

        static Mesh MakeBoard()
        {
            var painter = new Painter();
            painter.Platform(new[] {
                new Vector3(7.35f,-.98f,4.35f),
                new Vector3(7.56f,-.82f,4.55f),
                new Vector3(7.56f,-.59f,4.55f),
                new Vector3(7.43f,-.54f,4.44f),
                new Vector3(7.48f,-.41f,4.48f),
                new Vector3(7.48f,-.14f,4.48f),
                new Vector3(7.36f,0f,4.37f)
            }, .78f, new[] {
                Rgb(0x774846), Rgb(0xAE7353), Rgb(0x74494A), Rgb(0xAC765E),
                Rgb(0xC79262), Rgb(0xF3CF87), Rgb(0xF4D896)
            });

            const float borderY = .004f;
            for (int side = -1; side <= 1; side += 2)
            {
                painter.Box(new Vector3(0f,borderY,side * 4.18f), new Vector3(13.03f,.011f,.078f),
                    Rgb(0xCB9C60));
                painter.Box(new Vector3(side * 7.13f,borderY,0f), new Vector3(.078f,.011f,6.89f),
                    Rgb(0xCB9C60));
                for (int i = 0; i < 10; i++)
                {
                    float x = (i - 4.5f) * 1.36f;
                    Color stone = i % 3 == 0 ? Rgb(0xEBC282) : Rgb(0xF0CD8C);
                    painter.Box(new Vector3(x,borderY - .014f,side * 4.18f),
                        new Vector3(1.31f,.03f,.34f), stone);
                    painter.Box(new Vector3(x,-.325f,side * 4.481f),
                        new Vector3(1.305f,.25f,.045f), i % 2 == 0 ? Rgb(0xCD9667) : Rgb(0xC28B62));
                }

                for (int i = 0; i < 6; i++)
                {
                    float z = (i - 2.5f) * 1.20f;
                    painter.Box(new Vector3(side * 7.14f,borderY - .014f,z), new Vector3(.35f,.03f,1.16f),
                        i % 2 == 0 ? Rgb(0xE9C17F) : Rgb(0xF0CD8C));
                }

                for (int zSide = -1; zSide <= 1; zSide += 2)
                {
                    painter.Box(new Vector3(side * 6.96f,borderY - .012f,zSide * 3.95f),
                        new Vector3(.75f,.038f,.35f), Rgb(0xBD8E60), false,new Vector3(0f,side * zSide * 46f,0f));
                    painter.Box(new Vector3(side * 6.96f,.011f,zSide * 3.95f),
                        new Vector3(.16f,.027f,.16f), Rgb(0xFBE0A0), false,new Vector3(0f,45f,0f));
                }

                Color home = side < 0 ? Rgb(0x8DADB5) : Rgb(0xD49B78);
                Color chalk = side < 0 ? Rgb(0xBCC8B6) : Rgb(0xE6B58B);
                PaintFormationMark(painter, new Vector3(side * 2.70f, .008f, 0f), side, home, chalk);
                PaintFormationMark(painter, new Vector3(side * 3.72f, .008f, -1.75f), side, home, chalk);
                PaintFormationMark(painter, new Vector3(side * 4.70f, .008f, .34f), side, home, chalk);
            }

            Color medallion = Rgb(0xD2A472);
            painter.Ring(new Vector3(0f,.008f,0f),1.30f,.045f,0f,360f,medallion);
            painter.Ring(new Vector3(0f,.009f,0f),1.19f,.027f,5f,80f,Rgb(0xE8BA7C));
            painter.Ring(new Vector3(0f,.009f,0f),1.19f,.027f,185f,80f,Rgb(0xE8BA7C));
            painter.Plate(new[] {
                new Vector2(0f,.79f), new Vector2(.157f,.164f), new Vector2(.71f,0f),
                new Vector2(.157f,-.164f), new Vector2(0f,-.79f), new Vector2(-.157f,-.164f),
                new Vector2(-.71f,0f), new Vector2(-.157f,.164f)
            }, Vector2.zero, .008f, Rgb(0xDBAE77), Rgb(0xC99A66), false, .008f);
            painter.Box(new Vector3(0f,.015f,0f),new Vector3(.255f,.012f,.255f),
                Rgb(0xF4D69E),false,new Vector3(0f,45f,0f));

            var sand = new System.Random(139);
            for (int i = 0; i < 96; i++)
            {
                float x = Mathf.Lerp(-6.95f,6.95f,(float)sand.NextDouble());
                float z = Mathf.Lerp(-3.85f,3.85f,(float)sand.NextDouble());
                if (Mathf.Abs(z) < 2.32f && (Mathf.Abs(x) < 1.47f || Mathf.Abs(Mathf.Abs(x) - 3.64f) < 1.08f))
                    continue;
                float width = .025f + .12f * (float)sand.NextDouble();
                float length = .016f + .04f * (float)sand.NextDouble();
                painter.Box(new Vector3(x,.0025f,z),new Vector3(width,.004f,length),
                    i % 3 == 0 ? Rgb(0xE7BE83) : Rgb(0xF7DB9D), false,new Vector3(0f,i * 19f,0f));
            }

            return painter.Bake("Tabletop").Color;
        }

        static void PaintFormationMark(Painter painter, Vector3 center, int side, Color stone, Color chalk)
        {
            painter.Ring(center, .73f, .04f, 0f, 360f, chalk);
            painter.Ring(center + Vector3.up * .001f, .73f, .065f, side < 0 ? 124f : -56f, 112f, stone);
            const float markerDepth = .089f;
            const float markerWidth = .095f;
            Vector3 mark = center + new Vector3(side * .63f, .001f, 0f);
            painter.Triangle(mark + new Vector3(-side * markerDepth, 0f, 0f),
                mark + new Vector3(side * markerDepth, 0f, markerWidth),
                mark + new Vector3(side * markerDepth, 0f, -markerWidth), Vector3.up, stone);
        }

        static Mesh MakeStands()
        {
            var painter = new Painter();
            Color beam = Rgb(0xA66C56);
            Color plaster = Rgb(0xD7A777);
            Color sun = Rgb(0xEDC58D);
            Color plinth = Rgb(0xB48264);
            Color seat = Rgb(0xD5A170);
            for (int i = -5; i <= 5; i++)
            {
                if (i == 0) continue;
                float angle = i * .225f;
                float x = Mathf.Sin(angle) * 10.4f;
                float z = Mathf.Cos(angle) * 6.75f;
                float yaw = angle * Mathf.Rad2Deg * .75f;
                var rotation = new Vector3(0f,yaw,0f);

                painter.Box(new Vector3(x,-.53f,z),new Vector3(2.40f,1.13f,1.10f),plinth,false,rotation);
                painter.Box(new Vector3(x,-.058f,z-.22f),new Vector3(2.36f,.26f,1.50f),sun,false,rotation);
                painter.Box(new Vector3(x,.27f,z+.06f),new Vector3(2.35f,.47f,1.00f),seat,false,rotation);
                painter.Box(new Vector3(x,.49f,z+.22f),new Vector3(2.31f,.145f,1.04f),sun,false,rotation);
                painter.Box(new Vector3(x,.79f,z+.363f),new Vector3(2.38f,.46f,.61f),plaster,false,rotation);
                painter.Box(new Vector3(x,.95f,z+.37f),new Vector3(2.42f,.145f,.69f),sun,false,rotation);
                painter.Box(new Vector3(x,.66f,z-.049f),new Vector3(2.23f,.13f,.077f),beam,false,rotation);

                float poleX = x + Mathf.Cos(angle) * .67f;
                float poleZ = z - Mathf.Sin(angle) * .62f;
                painter.Box(new Vector3(poleX,.274f,poleZ),new Vector3(.24f,1.66f,.46f),beam,false,rotation);
                painter.Box(new Vector3(poleX,1.116f,poleZ),new Vector3(.39f,.21f,.53f),SunInset(i),false,rotation);
                painter.Box(new Vector3(poleX,-.568f,poleZ-.09f),new Vector3(.41f,.235f,.67f),sun,false,rotation);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = 1.7f - 1.48f * i;
                    float x = side * (9.47f + i * .07f);
                    float y = -.39f - i * .045f;
                    painter.Box(new Vector3(x,y,z),new Vector3(1.05f,.84f,1.57f),plaster);
                    painter.Box(new Vector3(x,y+.435f,z),new Vector3(1.21f,.20f,1.55f),sun);
                    painter.Box(new Vector3(x-side * .54f,y-.23f,z),new Vector3(.25f,.19f,1.56f),beam);
                    painter.Box(new Vector3(x-side * .21f,y+.571f,z+.52f),new Vector3(.57f,.31f,.49f),sun);
                }

                float xPole = side * 3.67f;
                float zPole = 6.03f;
                PaintStandard(painter, new Vector3(xPole,0f,zPole), side < 0 ? Blue : Coral);
            }

            PaintGate(painter);
            return painter.Bake("Stands").Color;
        }

        static Color SunInset(int index) => index % 3 == 0 ? Rgb(0xEABF86) : Rgb(0xECC891);

        static void PaintStandard(Painter painter, Vector3 position, Palette palette)
        {
            painter.Box(position + new Vector3(0f,.54f,0f),new Vector3(.34f,1.8f,.39f),Rgb(0xAD795C));
            painter.Box(position + new Vector3(0f,.91f,0f),new Vector3(.49f,.18f,.56f),Rgb(0xE6BB7F));
            painter.Cylinder(position + new Vector3(0f,1.92f,0f),new Vector3(.105f,2.03f,.105f),LeatherDark);
            painter.Ellipsoid(position + new Vector3(0f,2.98f,0f),new Vector3(.21f,.23f,.21f),Gold);
            painter.Cylinder(position + new Vector3(0f,2.67f,0f),new Vector3(.09f,1.03f,.09f),
                Gold,false,new Vector3(0f,0f,90f));

            Vector3 topLeft = position + new Vector3(-.42f,2.61f,-.068f);
            Vector3 topRight = position + new Vector3(.42f,2.61f,-.068f);
            Vector3 lowLeft = position + new Vector3(-.41f,1.74f,-.08f);
            Vector3 lowRight = position + new Vector3(.41f,1.74f,-.08f);
            Vector3 lowCenter = position + new Vector3(0f,1.51f,-.12f);
            painter.Quad(topLeft,topRight,lowRight,lowLeft,Vector3.back,palette.Base);
            painter.Triangle(lowLeft,lowRight,lowCenter,Vector3.back,palette.Base);
            painter.Box(position + new Vector3(-.38f,2.17f,-.094f),new Vector3(.045f,.88f,.038f),palette.Trim);
            painter.Box(position + new Vector3(.38f,2.17f,-.094f),new Vector3(.045f,.88f,.038f),palette.Trim);
            painter.Box(position + new Vector3(0f,2.153f,-.106f),new Vector3(.21f,.21f,.034f),palette.Trim,
                false,new Vector3(0f,0f,45f));
        }

        static void PaintGate(Painter painter)
        {
            const float z = 6.73f;
            Color roof = Rgb(0xC28A63);
            Color block = Rgb(0xD4A577);
            Color light = Rgb(0xEAC58E);
            Color tunnel = Rgb(0x514A62);
            Color dark = Rgb(0x4C3F4D);
            painter.Box(new Vector3(0f,-.025f,z+.18f),new Vector3(3.07f,1.45f,1.75f),roof);
            painter.Box(new Vector3(0f,.67f,z+.27f),new Vector3(2.85f,.66f,1.55f),block);
            painter.Box(new Vector3(0f,1.09f,z+.28f),new Vector3(3.10f,.21f,1.61f),light);

            for (int side = -1; side <= 1; side += 2)
            {
                painter.Box(new Vector3(side * 1.16f,.22f,z-.765f),new Vector3(.39f,1.64f,.64f),light);
                painter.Box(new Vector3(side * 1.16f,-.55f,z-.81f),new Vector3(.52f,.25f,.69f),block);
                painter.Box(new Vector3(side * 1.33f,1.30f,z+.26f),new Vector3(.47f,.29f,1.33f),light);
                for (int i = 0; i < 3; i++)
                    painter.Box(new Vector3(side * 1.16f,-.28f + i * .36f,z-1.087f),
                        new Vector3(.39f,.04f,.032f),roof);
            }

            painter.Box(new Vector3(0f,-.36f,z-.709f),new Vector3(1.84f,.99f,.23f),tunnel);
            painter.Quad(new Vector3(-.92f,-.89f,z-.89f),new Vector3(.92f,-.89f,z-.89f),
                new Vector3(.92f,.25f,z-.89f),new Vector3(-.92f,.25f,z-.89f),Vector3.back,tunnel);
            for (int i = 0; i < 11; i++)
            {
                float start = Mathf.PI * i / 11;
                float end = Mathf.PI * (i + 1) / 11;
                var a = new Vector3(Mathf.Cos(start),Mathf.Sin(start),0f);
                var b = new Vector3(Mathf.Cos(end),Mathf.Sin(end),0f);
                var center = new Vector3(0f,.25f,z-.90f);
                painter.Triangle(center,center + a * .925f,center + b * .925f,Vector3.back,tunnel);
                var borderCenter = new Vector3(0f,.25f,z-1.04f);
                painter.Quad(borderCenter+a*.915f,borderCenter+a*1.235f,borderCenter+b*1.235f,
                    borderCenter+b*.915f,Vector3.back,i % 2 == 0 ? light : block);
            }

            for (int i = -3; i <= 3; i++)
            {
                float x = i * .223f;
                float height = .89f + Mathf.Sqrt(.925f * .925f - x * x);
                painter.Box(new Vector3(x,-.64f + height / 2f,z-1.01f),new Vector3(.055f,height,.068f),dark);
            }

            painter.Box(new Vector3(0f,-.078f,z-1.04f),new Vector3(1.79f,.079f,.075f),dark);
        }

        static Mesh MakeOuterGround()
        {
            var painter = new Painter();
            painter.Box(new Vector3(0f,-1.18f,.5f),new Vector3(180f,.31f,180f),Rgb(0xEBC48A));
            for (int i = 0; i < 7; i++)
            {
                float stripeZ = -4.9f - i * .95f;
                var stripe = new Vector3(0f,-1.022f,stripeZ);
                painter.Box(stripe,new Vector3(29f,.022f,i % 2 == 0 ? .3f : .58f),
                    i % 2 == 0 ? Rgb(0xE7BE84) : Rgb(0xEFC68A),false,new Vector3(0f,6f,0f));
            }

            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                float x = i * 4.2f;
                painter.Box(new Vector3(x,-.974f,-4.87f),new Vector3(.95f,.16f,.63f),
                    SkyStone,false,new Vector3(15f,x*6f,4f));
                painter.Box(new Vector3(x+.51f,-.987f,-5.24f),new Vector3(.43f,.11f,.34f),
                    Rgb(0xBE926F),false,new Vector3(-13f,x*7f,6f));
            }

            return painter.Bake("Sand").Color;
        }

        static Color Rgb(int hex, float shine = .08f)
        {
            return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, shine);
        }

        readonly struct Palette
        {
            public readonly Color Base;
            public readonly Color Light;
            public readonly Color Dark;
            public readonly Color Trim;

            public Palette(int basis, int light, int dark, int trim)
            {
                Base = Rgb(basis, .36f);
                Light = Rgb(light, .52f);
                Dark = Rgb(dark, .27f);
                Trim = Rgb(trim, .51f);
            }
        }

        readonly struct Model
        {
            public readonly Mesh Color;
            public readonly Mesh Ink;

            public Model(Mesh color, Mesh ink)
            {
                Color = color;
                Ink = ink;
            }
        }

        sealed class Painter
        {
            readonly Surface paint = new Surface();
            readonly Surface outline = new Surface();

            public void Box(Vector3 at, Vector3 size, Color color, bool ink = false, Vector3 rotation = default)
            {
                if (roundedBox == null) roundedBox = MakeRoundedBox();
                Add(roundedBox,at,size,color,ink,rotation);
            }

            public void Ellipsoid(Vector3 at, Vector3 size, Color color, bool ink = false, Vector3 rotation = default)
            {
                if (sphere == null) sphere = MakeSphere();
                Add(sphere,at,size,color,ink,rotation);
            }

            public void Cylinder(Vector3 at, Vector3 size, Color color, bool ink = false, Vector3 rotation = default)
            {
                if (cylinder == null) cylinder = MakeCylinder();
                Add(cylinder,at,size,color,ink,rotation);
            }

            void Add(Primitive shape, Vector3 at, Vector3 size, Color color, bool ink, Vector3 rotation)
            {
                Quaternion orientation = Quaternion.Euler(rotation);
                paint.Add(shape,at,size,color,orientation);
                if (ink) outline.Add(shape,at,size,color,orientation);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
            {
                paint.Triangle(a,b,c,normal,color);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
            {
                paint.Quad(a,b,c,d,normal,color);
            }

            public void Plate(Vector2[] shape, Vector2 center, float height, Color face, Color edge,
                bool ink, float offsetY = 0f)
            {
                Vector3 top = new Vector3(center.x,offsetY + height,center.y);
                Vector3 bottom = new Vector3(center.x,offsetY - height,center.y);
                for (int i = 0; i < shape.Length; i++)
                {
                    var a = shape[i];
                    var b = shape[(i+1) % shape.Length];
                    Vector3 upperA = new Vector3(a.x,offsetY + height * .19f,a.y);
                    Vector3 upperB = new Vector3(b.x,offsetY + height * .19f,b.y);
                    Vector3 lowerA = new Vector3(a.x,offsetY - height * .19f,a.y);
                    Vector3 lowerB = new Vector3(b.x,offsetY - height * .19f,b.y);
                    Vector3 outer = new Vector3((a.x+b.x)*.5f-center.x,0f,(a.y+b.y)*.5f-center.y).normalized;
                    Color facet = i % 3 == 0 ? Color.Lerp(face,edge,.43f) : face;
                    AddPlateWedge(paint,upperA,upperB,lowerA,lowerB,top,bottom,outer,facet,edge);
                    if (ink) AddPlateWedge(outline,upperA,upperB,lowerA,lowerB,top,bottom,outer,facet,edge);
                }
            }

            static void AddPlateWedge(Surface surface, Vector3 upperA, Vector3 upperB, Vector3 lowerA,
                Vector3 lowerB, Vector3 top, Vector3 bottom, Vector3 outer, Color face, Color edge)
            {
                surface.Facet(upperA,upperB,top,Vector3.up,face);
                surface.Facet(lowerA,lowerB,bottom,Vector3.down,edge);
                surface.Quad(upperA,lowerA,lowerB,upperB,outer,edge);
            }

            public void Ring(Vector3 center, float radius, float width, float start, float sweep, Color color)
            {
                int divisions = Mathf.Max(6, Mathf.CeilToInt(sweep / 11f));
                for (int i = 0; i < divisions; i++)
                {
                    float a = (start + sweep * i / divisions) * Mathf.Deg2Rad;
                    float b = (start + sweep * (i + 1) / divisions) * Mathf.Deg2Rad;
                    var first = new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
                    var second = new Vector3(Mathf.Cos(b),0f,Mathf.Sin(b));
                    Quad(center+first*radius,center+second*radius,center+second*(radius-width),
                        center+first*(radius-width),Vector3.up,color);
                }
            }

            public void Platform(Vector3[] rings, float corner, Color[] colors)
            {
                var prior = ChamferedRing(rings[0],corner);
                for (int row = 1; row < rings.Length; row++)
                {
                    var next = ChamferedRing(rings[row],corner);
                    for (int i = 0; i < next.Length; i++)
                    {
                        int j = (i + 1) % next.Length;
                        Vector3 midpoint = (next[i] + next[j]) * .5f;
                        Vector3 outward = new Vector3(midpoint.x / rings[row].x,0f,midpoint.z / rings[row].z);
                        Color shaded = i % 2 == 0 ? colors[row-1] : Color.Lerp(colors[row-1], colors[row], .16f);
                        paint.FacetedQuad(prior[i],prior[j],next[j],next[i],outward,shaded);
                    }
                    prior = next;
                }

                for (int i = 1; i < prior.Length - 1; i++)
                    Triangle(prior[0],prior[i],prior[i+1],Vector3.up,colors[colors.Length-1]);
            }

            static Vector3[] ChamferedRing(Vector3 measures, float corner)
            {
                float x = measures.x;
                float y = measures.y;
                float z = measures.z;
                return new[] {
                    new Vector3(-x+corner,y,-z), new Vector3(x-corner,y,-z),
                    new Vector3(x,y,-z+corner), new Vector3(x,y,z-corner),
                    new Vector3(x-corner,y,z), new Vector3(-x+corner,y,z),
                    new Vector3(-x,y,z-corner), new Vector3(-x,y,-z+corner)
                };
            }

            public Model Bake(string name) => new Model(paint.Bake(name),outline.Bake(name+" silhouette"));
        }

        sealed class Primitive
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Faces = new List<int>();

            public int Vertex(Vector3 position, Vector3 normal)
            {
                Positions.Add(position);
                Normals.Add(normal);
                return Positions.Count - 1;
            }

            public void Triangle(int a, int b, int c, Vector3 expected)
            {
                AddFace(Faces,Positions,a,b,c,expected);
            }

            public void Quad(int a, int b, int c, int d, Vector3 expected)
            {
                Triangle(a,b,c,expected);
                Triangle(a,c,d,expected);
            }
        }

        sealed class Surface
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color32> colors = new List<Color32>();
            readonly List<int> faces = new List<int>();

            void Vertex(Vector3 p, Vector3 n, Color32 color)
            {
                vertices.Add(p);
                normals.Add(n);
                colors.Add(color);
            }

            public void Add(Primitive source, Vector3 position, Vector3 size, Color shade, Quaternion rotation)
            {
                int offset = vertices.Count;
                var inverseScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
                Color32 color = MeshColor(shade);
                for (int i = 0; i < source.Positions.Count; i++)
                {
                    Vector3 p = position + rotation * Vector3.Scale(source.Positions[i],size);
                    Vector3 normal = rotation * Vector3.Scale(source.Normals[i],inverseScale).normalized;
                    Vertex(p,normal,color);
                }

                for (int i = 0; i < source.Faces.Count; i++)
                    faces.Add(source.Faces[i] + offset);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color shade)
            {
                int offset = vertices.Count;
                Color32 color = MeshColor(shade);
                Vertex(a,normal,color);
                Vertex(b,normal,color);
                Vertex(c,normal,color);
                AddFace(faces,vertices,offset,offset+1,offset+2,normal);
            }

            public void Facet(Vector3 a, Vector3 b, Vector3 c, Vector3 expected, Color shade)
            {
                Triangle(a,b,c,OrientedNormal(a,b,c,expected),shade);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color shade)
            {
                int offset = vertices.Count;
                Color32 color = MeshColor(shade);
                Vertex(a,normal,color);
                Vertex(b,normal,color);
                Vertex(c,normal,color);
                Vertex(d,normal,color);
                AddFace(faces,vertices,offset,offset+1,offset+2,normal);
                AddFace(faces,vertices,offset,offset+2,offset+3,normal);
            }

            public void FacetedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expected, Color shade)
            {
                Quad(a,b,c,d,OrientedNormal(a,b,c,expected),shade);
            }

            public Mesh Bake(string name)
            {
                if (vertices.Count == 0) return null;
                var mesh = new Mesh {name = name, hideFlags = HideFlags.DontSave};
                if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(faces,0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }

            static Color32 MeshColor(Color color)
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            }
        }

        static Vector3 OrientedNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 expected)
        {
            Vector3 normal = Vector3.Cross(b-a,c-a).normalized;
            return Vector3.Dot(normal,expected) >= 0f ? normal : -normal;
        }

        static void AddFace(List<int> indices, List<Vector3> points, int a, int b, int c, Vector3 expected)
        {
            bool forward = Vector3.Dot(Vector3.Cross(points[b]-points[a],points[c]-points[a]),expected) >= 0f;
            indices.Add(a);
            indices.Add(forward ? b : c);
            indices.Add(forward ? c : b);
        }

        static Primitive MakeRoundedBox()
        {
            var shape = new Primitive();
            var coordinates = new[] {-.5f,-.44f,-.36f,.36f,.44f,.5f};
            var directions = new[] {Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            int side = coordinates.Length;
            foreach (Vector3 normal in directions)
            {
                Vector3 axis = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 other = Vector3.Cross(normal,axis);
                int start = shape.Positions.Count;
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        Vector3 cube = normal * .5f + axis * coordinates[x] + other * coordinates[y];
                        Vector3 core = new Vector3(Mathf.Clamp(cube.x,-.36f,.36f),
                            Mathf.Clamp(cube.y,-.36f,.36f), Mathf.Clamp(cube.z,-.36f,.36f));
                        Vector3 roundedNormal = (cube-core).normalized;
                        shape.Vertex(core + roundedNormal * .14f,roundedNormal);
                    }
                }

                for (int y = 0; y < side-1; y++)
                    for (int x = 0; x < side-1; x++)
                    {
                        int a = start + y * side + x;
                        shape.Quad(a,a+1,a+side+1,a+side,normal);
                    }
            }

            return shape;
        }

        static Primitive MakeSphere()
        {
            const int columns = 16;
            const int rows = 10;
            var shape = new Primitive();
            for (int y = 0; y <= rows; y++)
            {
                float inclination = Mathf.PI * y / rows;
                for (int x = 0; x <= columns; x++)
                {
                    float angle = Mathf.PI * 2f * x / columns;
                    var normal = new Vector3(Mathf.Sin(inclination)*Mathf.Cos(angle),Mathf.Cos(inclination),
                        Mathf.Sin(inclination)*Mathf.Sin(angle));
                    shape.Vertex(normal*.5f,normal);
                }
            }

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int first = y * (columns+1) + x;
                    int nextRow = first + columns + 1;
                    if (y > 0) shape.Triangle(first,first+1,nextRow+1,shape.Normals[first]);
                    if (y < rows-1) shape.Triangle(first,nextRow+1,nextRow,shape.Normals[nextRow]);
                }
            return shape;
        }

        static Primitive MakeCylinder()
        {
            var shape = new Primitive();
            const int divisions = 16;
            int lower = shape.Vertex(Vector3.down*.5f,Vector3.down);
            int upper = shape.Vertex(Vector3.up*.5f,Vector3.up);
            for (int i = 0; i < divisions; i++)
            {
                float a = Mathf.PI * 2f * i / divisions;
                float b = Mathf.PI * 2f * (i + 1) / divisions;
                var first = new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
                var second = new Vector3(Mathf.Cos(b),0f,Mathf.Sin(b));
                var aLow = first*.5f + Vector3.down*.5f;
                var bLow = second*.5f + Vector3.down*.5f;
                var aHigh = first*.5f + Vector3.up*.5f;
                var bHigh = second*.5f + Vector3.up*.5f;
                shape.Quad(shape.Vertex(aLow,first),shape.Vertex(bLow,second),shape.Vertex(bHigh,second),
                    shape.Vertex(aHigh,first),first+second);
                shape.Triangle(lower,shape.Vertex(aLow,Vector3.down),shape.Vertex(bLow,Vector3.down),Vector3.down);
                shape.Triangle(upper,shape.Vertex(aHigh,Vector3.up),shape.Vertex(bHigh,Vector3.up),Vector3.up);
            }
            return shape;
        }
    }
}
