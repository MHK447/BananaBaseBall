using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena
{
    public static class ArenaDollArt
    {
        private static readonly Color32 Thread = Color(0xF9DDA3, 0.09f);
        private static readonly Color32 Coffee = Color(0x362330, 0.11f);
        private static readonly Color32 Leather = Color(0x573746, 0.19f);
        private static readonly Color32 BootSole = Color(0xA87551, 0.13f);
        private static readonly Color32 EyeWhite = Color(0xFFF7E4, 0.35f);
        private static readonly Color32 Pupil = Color(0x24223C, 0.29f);
        private static readonly Color32 Tongue = Color(0xE77D7A, 0.18f);
        private static readonly Color32 Wood = Color(0x774536, 0.13f);
        private static readonly Color32 Gold = Color(0xF9AA54, 0.38f);
        private static readonly Color32 Iron = Color(0x7484A7, 0.57f);
        private static readonly Color32 Silver = Color(0xE1EEF0, 0.68f);

        private static readonly Dictionary<(ArenaArt.Part, bool, int, Vector3), Model> Parts = new();
        private static readonly Dictionary<(bool, int, ArenaArt.WeaponStyle), Model> Weapons = new();
        private static readonly Primitive Cushion = MakeCushion(2f);
        private static readonly Primitive SoftCorners = MakeCushion(2.75f);
        private static Material fabric;
        private static Material ink;

        public static void AttachPart(ArenaArt.Part part, Transform anchor, bool player, int slot, Vector3 colliderSize)
        {
            slot = Mathf.Clamp(slot, 0, 2);
            var key = (part, player, slot, colliderSize);
            if (!Parts.TryGetValue(key, out Model model) || model.Paint == null)
            {
                model = MakePart(part, new BrawlerColors(player, slot), slot, colliderSize);
                Parts[key] = model;
            }

            Mount(anchor, "Brawler " + part, model);
        }

        public static void AttachSword(Transform anchor, bool player, int slot)
        {
            AttachWeapon(anchor, player, slot, ArenaArt.WeaponStyle.Sword);
        }

        public static void AttachWeapon(Transform anchor, bool player, int slot, ArenaArt.WeaponStyle style)
        {
            slot = Mathf.Clamp(slot, 0, 2);
            var key = (player, slot, style);
            if (!Weapons.TryGetValue(key, out Model model) || model.Paint == null)
            {
                model = MakeWeapon(new BrawlerColors(player, slot), slot, style);
                Weapons[key] = model;
            }

            Mount(anchor, "Wrapped toy " + style.ToString().ToLowerInvariant(), model);
        }

        private static void Mount(Transform parent, string name, Model model)
        {
            if (fabric == null)
            {
                Shader shader = Resources.Load<Shader>("SlingArenaToySurface");
                if (shader == null)
                    shader = Shader.Find("SlingArena/Toy Surface");
                fabric = new Material(shader) { name = "Cartoon skin and tunics", hideFlags = HideFlags.DontSave };
                ink = new Material(shader) { name = "Brawler silhouette", hideFlags = HideFlags.DontSave };
                ink.SetFloat("_InkMode", 1f);
                ink.SetFloat("_InkWidth", 0.015f);
                ink.SetColor("_InkColor", new Color32(56, 37, 43, 255));
                ink.SetFloat("_Cull", (float)CullMode.Front);
            }

            AddRenderer(parent, name, model.Paint, fabric, true);
            if (model.Ink != null)
                AddRenderer(parent, name + " silhouette", model.Ink, ink, false);
        }

        private static void AddRenderer(Transform parent, string name, Mesh mesh, Material material, bool castsShadow)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castsShadow;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        private static Model MakePart(ArenaArt.Part part, BrawlerColors colors, int slot, Vector3 size)
        {
            var b = new Painter();
            switch (part)
            {
                case ArenaArt.Part.Head:
                    PaintHead(b, colors, slot, size);
                    break;
                case ArenaArt.Part.Torso:
                    PaintTorso(b, colors, slot, size);
                    break;
                case ArenaArt.Part.Hips:
                    b.Molded(Vector3.zero, size * 0.52f, colors.Dark, true);
                    b.Molded(new Vector3(0f, size.y * 0.16f, 0f),
                        new Vector3(size.x * 0.53f, 0.053f, size.z * 0.54f), Leather, false);
                    b.Molded(new Vector3(0.025f, size.y * 0.17f, size.z * 0.51f),
                        new Vector3(0.061f, 0.047f, 0.022f), Gold, false);
                    b.Molded(new Vector3(0.027f, size.y * 0.17f, size.z * 0.56f),
                        new Vector3(0.035f, 0.024f, 0.016f), Leather, false);
                    break;
                case ArenaArt.Part.LeftUpperArm:
                case ArenaArt.Part.RightUpperArm:
                    PaintLimb(b, size, colors, slot, false, false, part == ArenaArt.Part.LeftUpperArm ? -1f : 1f);
                    break;
                case ArenaArt.Part.LeftForearm:
                case ArenaArt.Part.RightForearm:
                    PaintLimb(b, size, colors, slot, false, true, part == ArenaArt.Part.LeftForearm ? -1f : 1f);
                    break;
                case ArenaArt.Part.LeftThigh:
                case ArenaArt.Part.RightThigh:
                    PaintLimb(b, size, colors, slot, true, false, 0f);
                    break;
                case ArenaArt.Part.LeftShin:
                case ArenaArt.Part.RightShin:
                    PaintLimb(b, size, colors, slot, true, true, 0f);
                    break;
            }
            return b.Bake(part + " brawler");
        }

        private static void PaintHead(Painter b, BrawlerColors colors, int slot, Vector3 size)
        {
            Vector3 radii = Vector3.Scale(size, slot switch
            {
                0 => new Vector3(0.48f, 0.52f, 0.50f),
                1 => new Vector3(0.57f, 0.47f, 0.53f),
                _ => new Vector3(0.49f, 0.52f, 0.49f)
            });
            Vector3 center = new Vector3(0f, 0.022f, 0f);
            if (slot == 1)
                b.Molded(center, radii, colors.Face, true);
            else
                b.Cushion(center, radii, colors.Face, true, false, Quaternion.Euler(0f, 0f, slot == 0 ? -5f : 5f));

            // The features sit on the upward-facing part of the head to read on the portrait court.
            Quaternion face = Quaternion.Euler(-70f, 0f, slot == 1 ? 8f : -5f);
            Vector3 faceCenter = center + face * new Vector3(0f, -0.030f, radii.z * 1.04f + 0.028f);
            b.Cushion(faceCenter - face * Vector3.forward * 0.053f,
                new Vector3(radii.x * 0.79f, radii.y * 0.70f, 0.108f), colors.Face, false, false, face);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 ear = new Vector3(side * radii.x * 0.90f, -0.039f, 0.055f);
                b.Cushion(ear, new Vector3(0.077f, 0.107f, 0.073f), colors.Face, true, false,
                    Quaternion.Euler(-12f, 0f, side * -12f));
                b.Cushion(ear + new Vector3(side * 0.024f, 0.020f, 0.056f),
                    new Vector3(0.031f, 0.061f, 0.027f), colors.Cheek, false);
                if (slot == 0 && side == -1)
                    b.Cushion(ear + new Vector3(-0.033f, -0.066f, 0.053f),
                        new Vector3(0.028f, 0.035f, 0.023f), Gold, false);
            }

            PaintHair(b, colors, slot, radii);
            float eyeHeight = slot == 1 ? 0.086f : 0.097f;
            Vector3 leftEye = faceCenter + face * new Vector3(-radii.x * 0.38f, 0.064f, 0.043f);
            Vector3 rightEye = faceCenter + face * new Vector3(radii.x * 0.37f, 0.091f, 0.038f);
            PaintEye(b, leftEye, face, new Vector2(0.080f, eyeHeight), new Vector2(0.015f, -0.005f), colors.Iris);
            PaintEye(b, rightEye, face, new Vector2(0.087f, eyeHeight * (slot == 1 ? 0.84f : 1.10f)),
                new Vector2(slot == 1 ? -0.018f : 0.009f, slot == 2 ? 0.012f : -0.011f), colors.Iris);

            float browSlope = slot == 1 ? 0.034f : -0.018f;
            Vector3 leftBrow = leftEye + face * new Vector3(-0.007f, eyeHeight + 0.026f, 0.009f);
            Vector3 rightBrow = rightEye + face * new Vector3(0.006f, eyeHeight + (slot == 2 ? 0.052f : 0.018f), 0.006f);
            b.Tube(leftBrow + face * new Vector3(-0.063f, browSlope, 0f),
                leftBrow + face * new Vector3(0.060f, -browSlope, 0.022f), 0.023f, colors.HairDark);
            b.Tube(rightBrow + face * new Vector3(-0.063f, -browSlope, 0.018f),
                rightBrow + face * new Vector3(0.060f, browSlope, 0f), slot == 1 ? 0.025f : 0.019f, colors.HairDark);

            Vector3 nose = faceCenter + face * new Vector3(0.012f, -0.038f, 0.082f);
            b.Cushion(nose, slot == 1 ? new Vector3(0.087f, 0.054f, 0.068f) : new Vector3(0.059f, 0.072f, 0.076f),
                colors.Cheek, true, false, face);
            b.Cushion(nose + face * new Vector3(-0.017f, 0.020f, 0.048f),
                new Vector3(0.025f, 0.025f, 0.021f), colors.Face, false, false, face);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 cheek = faceCenter + face * new Vector3(side * radii.x * 0.60f, -0.067f, 0.028f);
                b.Cushion(cheek, new Vector3(0.053f, 0.036f, 0.021f), colors.Cheek, false, false, face);
            }

            PaintMouth(b, faceCenter, face, colors, slot);
        }

        private static void PaintEye(Painter b, Vector3 center, Quaternion face, Vector2 radii, Vector2 gaze, Color32 iris)
        {
            b.Cushion(center - face * Vector3.forward * 0.010f,
                new Vector3(radii.x + 0.012f, radii.y + 0.011f, 0.038f), Leather, false, false, face);
            b.Cushion(center, new Vector3(radii.x, radii.y, 0.046f), EyeWhite, false, false, face);
            Vector3 pupil = center + face * new Vector3(gaze.x, gaze.y, 0.041f);
            b.Cushion(pupil, new Vector3(radii.x * 0.66f, radii.y * 0.62f, 0.019f), iris, false, false, face);
            b.Cushion(pupil + face * Vector3.forward * 0.013f,
                new Vector3(radii.x * 0.50f, radii.y * 0.48f, 0.012f), Pupil, false, false, face);
            b.Cushion(pupil + face * new Vector3(-0.018f, 0.026f, 0.024f),
                new Vector3(0.017f, 0.018f, 0.009f), EyeWhite, false, false, face);
            b.Cushion(pupil + face * new Vector3(0.018f, -0.018f, 0.023f),
                new Vector3(0.009f, 0.009f, 0.007f), EyeWhite, false, false, face);
        }

        private static void PaintMouth(Painter b, Vector3 faceCenter, Quaternion face, BrawlerColors colors, int slot)
        {
            Vector3 mouth = faceCenter + face * new Vector3(slot == 0 ? 0.036f : -0.018f, -0.146f, 0.050f);
            Quaternion smile = face * Quaternion.Euler(0f, 0f, slot switch { 0 => -13f, 1 => 9f, _ => 19f });
            Vector3 size = slot switch
            {
                0 => new Vector3(0.120f, 0.065f, 0.040f),
                1 => new Vector3(0.111f, 0.066f, 0.037f),
                _ => new Vector3(0.083f, 0.083f, 0.043f)
            };
            b.Cushion(mouth, size * 1.12f, colors.Cheek, false, false, smile);
            b.Cushion(mouth + smile * Vector3.forward * 0.021f, size, Coffee, false, false, smile);
            b.Cushion(mouth + smile * new Vector3(0.024f, -size.y * 0.48f, 0.054f),
                new Vector3(size.x * 0.56f, size.y * 0.33f, 0.012f), Tongue, false, false, smile);
            if (slot == 2)
            {
                PaintTooth(b, mouth, smile, -0.017f, 0.049f, 0.036f);
                return;
            }

            PaintTooth(b, mouth, smile, -0.061f, 0.034f, 0.040f);
            PaintTooth(b, mouth, smile, slot == 0 ? 0.038f : 0.007f, 0.036f, 0.052f);
            if (slot == 1)
                PaintTooth(b, mouth, smile, 0.066f, 0.026f, 0.038f);
            if (slot == 0)
            {
                Vector3 mustache = mouth + smile * new Vector3(-0.042f, 0.068f, 0.033f);
                b.Cushion(mustache, new Vector3(0.077f, 0.029f, 0.025f), colors.HairDark, false, false,
                    smile * Quaternion.Euler(0f, 0f, 12f));
                b.Cushion(mustache + smile * Vector3.right * 0.107f, new Vector3(0.063f, 0.025f, 0.022f),
                    colors.Hair, false, false, smile * Quaternion.Euler(0f, 0f, -16f));
            }
        }

        private static void PaintTooth(Painter b, Vector3 mouth, Quaternion smile, float center, float width, float height)
        {
            b.Molded(mouth + smile * new Vector3(center, 0.029f, 0.060f),
                new Vector3(width * 0.54f, height * 0.52f, 0.013f), EyeWhite, false, smile);
        }

        private static void PaintHair(Painter b, BrawlerColors colors, int slot, Vector3 radii)
        {
            b.Cushion(new Vector3(0f, 0.118f, -0.113f),
                new Vector3(radii.x * 0.96f, radii.y * 0.85f, radii.z * 0.72f), colors.HairDark, true, false,
                Quaternion.Euler(-8f, 0f, 0f));
            if (slot == 0)
            {
                b.Cushion(new Vector3(-0.105f, 0.275f, -0.047f), new Vector3(0.227f, 0.124f, 0.165f),
                    colors.Hair, true, false, Quaternion.Euler(0f, 0f, 14f));
                b.Cushion(new Vector3(0.113f, 0.294f, -0.091f), new Vector3(0.205f, 0.104f, 0.168f),
                    colors.Hair, true, false, Quaternion.Euler(-12f, 0f, -22f));
                b.Cushion(new Vector3(-0.178f, 0.287f, -0.172f), new Vector3(0.137f, 0.118f, 0.130f),
                    colors.HairLight, true, false, Quaternion.Euler(0f, 0f, -22f));
                b.Cushion(new Vector3(-0.072f, 0.355f, -0.044f), new Vector3(0.168f, 0.046f, 0.098f),
                    colors.HairLight, false, false, Quaternion.Euler(0f, 0f, 10f));
                b.Cushion(new Vector3(-0.193f, 0.125f, -0.213f), new Vector3(0.104f, 0.092f, 0.111f),
                    colors.Hair, true);
                return;
            }

            if (slot == 1)
            {
                b.Molded(new Vector3(0f, 0.235f, -0.068f),
                    new Vector3(0.332f, 0.115f, 0.18f), colors.Hair, true, Quaternion.Euler(0f, 0f, 11f));
                for (int i = 0; i < 4; i++)
                {
                    Vector3 quiff = new Vector3(-0.169f + i * 0.110f, 0.321f + (i % 2) * 0.036f, -0.111f - i * 0.012f);
                    b.Cushion(quiff, new Vector3(0.086f, 0.135f - i * 0.014f, 0.125f), i == 0 ? colors.HairLight : colors.Hair,
                        true, false, Quaternion.Euler(-35f, 0f, -28f + i * 6f));
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Molded(new Vector3(side * (radii.x - 0.038f), 0.072f, -0.047f),
                        new Vector3(0.057f, 0.123f, 0.082f), colors.HairDark, false,
                        Quaternion.Euler(0f, 0f, side * 15f));
                }
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                float along = i / 4f;
                Vector3 curl = new Vector3(Mathf.Lerp(-0.221f, 0.20f, along),
                    0.267f + Mathf.Sin(along * Mathf.PI) * 0.094f, -0.087f - (i % 2) * 0.060f);
                b.Cushion(curl, new Vector3(0.108f, 0.101f + (i % 2) * 0.016f, 0.131f),
                    i % 2 == 0 ? colors.Hair : colors.HairLight, true, false, Quaternion.Euler(0f, 0f, 22f - 15f * i));
            }
            b.Cushion(new Vector3(0.064f, 0.403f, -0.091f), new Vector3(0.089f, 0.087f, 0.131f),
                colors.HairLight, true, false, Quaternion.Euler(-15f, 0f, -22f));
        }

        private static void PaintTorso(Painter b, BrawlerColors colors, int slot, Vector3 size)
        {
            Vector3 radii = Vector3.Scale(size, slot switch
            {
                0 => new Vector3(0.46f, 0.53f, 0.52f),
                1 => new Vector3(0.61f, 0.54f, 0.56f),
                _ => new Vector3(0.51f, 0.52f, 0.56f)
            });
            b.Molded(Vector3.zero, radii, colors.Team, true, Quaternion.Euler(0f, 0f, -4f + 4f * slot));
            b.Cushion(new Vector3(0f, radii.y * 0.83f, 0f), new Vector3(0.129f, 0.091f, 0.109f),
                colors.Face, false);
            b.Molded(new Vector3(0f, radii.y * 0.62f, 0f),
                new Vector3(radii.x * 0.78f, 0.056f, radii.z * 0.88f), colors.Trim, false);
            b.Molded(new Vector3(0f, -radii.y * 0.61f, 0f),
                new Vector3(radii.x * 0.96f, 0.063f, radii.z * 0.93f), Leather, true);
            b.Molded(new Vector3(0f, -radii.y * 0.61f, radii.z * 0.89f),
                new Vector3(0.072f, 0.051f, 0.037f), Gold, false);
            b.Molded(new Vector3(0.009f, -radii.y * 0.61f, radii.z * 1.01f),
                new Vector3(0.041f, 0.026f, 0.021f), colors.Dark, false);

            if (slot == 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Molded(new Vector3(side * radii.x * 0.62f, 0.014f, radii.z * 0.84f),
                        new Vector3(0.043f, radii.y * 0.68f, 0.049f), colors.Dark, false,
                        Quaternion.Euler(6f, 0f, side * 10f));
                    b.Cushion(new Vector3(side * radii.x * 0.61f, -0.032f, radii.z * 1.04f),
                        new Vector3(0.029f, 0.030f, 0.018f), Gold, false);
                }
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion angle = Quaternion.Euler(0f, 0f, side * (slot == 0 ? 25f : 11f));
                b.Molded(new Vector3(side * radii.x * 0.09f, 0.012f, side * radii.z * 0.87f),
                    new Vector3(0.068f, radii.y * 0.78f, 0.045f), colors.Patch, false, angle);
            }
            if (slot == 2)
            {
                b.Cushion(new Vector3(-radii.x * 0.64f, radii.y * 0.62f, -radii.z * 0.56f),
                    new Vector3(0.112f, 0.067f, 0.077f), colors.Trim, false);
                b.Molded(new Vector3(-radii.x * 0.78f, radii.y * 0.34f, -radii.z * 0.77f),
                    new Vector3(0.061f, 0.182f, 0.054f), colors.Trim, true, Quaternion.Euler(15f, 0f, -30f));
            }
        }

        private static void PaintLimb(Painter b, Vector3 size, BrawlerColors colors, int slot, bool leg, bool lower,
            float side)
        {
            Vector3 radii = Vector3.Scale(size, new Vector3(slot == 1 ? 0.56f : 0.47f, 0.54f, 0.53f));
            b.Cushion(Vector3.zero, radii, leg ? colors.Dark : colors.Face, true, false,
                Quaternion.Euler(leg ? 3f : -6f, 0f, slot == 0 ? 5f : -3f));
            if (!lower)
            {
                if (leg)
                {
                    b.Molded(new Vector3(0f, radii.y * 0.25f, 0.015f),
                        new Vector3(radii.x * 1.08f, radii.y * 0.72f, radii.z * 1.02f), colors.Team, true);
                    return;
                }

                Vector3 sleeve = new Vector3(-side * 0.025f, radii.y * 0.37f, 0f);
                b.Molded(sleeve, new Vector3(radii.x * 1.17f, radii.y * 0.72f, radii.z * 1.15f),
                    colors.Team, true, Quaternion.Euler(0f, 0f, -side * 25f));
                b.Cushion(sleeve + new Vector3(-side * 0.128f, 0.085f, 0.008f),
                    new Vector3(0.082f, 0.060f, 0.065f), colors.Team, false);
                b.Molded(new Vector3(0f, -radii.y * 0.15f, 0.002f),
                    new Vector3(radii.x * 1.13f, 0.048f, radii.z * 1.12f), colors.Trim, false);
                return;
            }

            float extremityY = -radii.y * (leg ? 0.79f : 0.76f);
            Vector3 terminal = new Vector3(0f, extremityY, leg ? 0.065f : 0.034f);
            if (leg)
            {
                Vector3 boot = new Vector3(radii.x * 1.17f, 0.069f, radii.z * 1.44f);
                b.Molded(terminal, boot, Leather, true);
                b.Molded(terminal + Vector3.down * 0.045f, new Vector3(boot.x * 1.10f, 0.026f, boot.z * 1.03f),
                    BootSole, false);
                b.Molded(terminal + new Vector3(0f, 0.102f, -0.055f),
                    new Vector3(radii.x * 1.07f, 0.100f, radii.z * 1.02f), Leather, true);
                b.Molded(terminal + new Vector3(0f, 0.157f, -0.058f),
                    new Vector3(radii.x * 1.16f, 0.038f, radii.z * 1.08f), colors.Trim, false);
                return;
            }

            b.Molded(new Vector3(0f, extremityY + 0.105f, 0.003f),
                new Vector3(radii.x * 1.01f, 0.049f, radii.z * 1.01f), slot == 1 ? Leather : colors.Trim, false);
            b.Cushion(terminal, new Vector3(radii.x * 1.08f, 0.092f, radii.z * 0.90f),
                colors.Face, true, false, Quaternion.Euler(-24f, 0f, 0f));
            b.Cushion(terminal + new Vector3(-side * 0.064f, 0.016f, 0.034f),
                new Vector3(0.042f, 0.053f, 0.045f), colors.Cheek, false);
            for (int knuckle = -1; knuckle <= 1; knuckle++)
            {
                b.Cushion(terminal + new Vector3(knuckle * 0.038f, -0.010f, 0.065f),
                    new Vector3(0.029f, 0.034f, 0.026f), colors.Face, false);
            }
        }

        private static Model MakeWeapon(BrawlerColors colors, int slot, ArenaArt.WeaponStyle style)
        {
            var b = new Painter();
            PaintWeaponGrip(b, colors);
            switch (style)
            {
                case ArenaArt.WeaponStyle.Axe:
                    PaintAxe(b, colors);
                    break;
                case ArenaArt.WeaponStyle.Hammer:
                    PaintHammer(b, colors);
                    break;
                default:
                    PaintSword(b, colors, slot);
                    break;
            }
            return b.Bake("Sewn grip " + style);
        }

        private static void PaintWeaponGrip(Painter b, BrawlerColors colors)
        {
            b.Tube(new Vector3(0f, 0f, -0.057f), new Vector3(0f, 0f, 0.249f), 0.058f, Wood, true);
            b.Cushion(new Vector3(0f, 0f, -0.055f), new Vector3(0.087f, 0.064f, 0.064f),
                colors.Dark, true, true);
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = new Vector3(0f, 0f, -0.020f + i * 0.050f);
                b.Cushion(position, new Vector3(0.064f, 0.067f, 0.017f), i % 2 == 0 ? colors.Rope : colors.Team,
                    false, true, Quaternion.Euler(0f, 17f, 0f));
            }
        }

        private static void PaintSword(Painter b, BrawlerColors colors, int slot)
        {
            b.Tube(new Vector3(-0.171f, 0f, 0.229f), new Vector3(0.171f, 0f, 0.229f), 0.049f, Wood, true);
            for (int i = -1; i <= 1; i += 2)
                b.Cushion(new Vector3(i * 0.14f, 0.012f, 0.225f), new Vector3(0.048f, 0.049f, 0.057f),
                    i == -1 ? Gold : colors.Trim, true);
            b.Cushion(new Vector3(0f, 0.038f, 0.247f), new Vector3(0.087f, 0.048f, 0.079f),
                colors.Team, false, true);
            if (slot == 2)
                b.Cushion(new Vector3(0f, 0.083f, 0.245f), new Vector3(0.035f, 0.018f, 0.041f), Gold, false);
            b.SteelBlade();
        }

        private static void PaintAxe(Painter b, BrawlerColors colors)
        {
            b.Tube(new Vector3(0f, 0f, 0.19f), new Vector3(0f, 0f, 1.00f), 0.058f, Wood, true);
            b.Cushion(new Vector3(0f, 0f, 0.38f), new Vector3(0.071f, 0.067f, 0.080f), colors.Team, false, true);
            b.Cushion(new Vector3(-0.071f, 0f, 0.748f), new Vector3(0.065f, 0.059f, 0.082f),
                Iron, true, true);
            b.AxeBlade();
            b.Cushion(new Vector3(0.011f, 0.020f, 0.748f), new Vector3(0.087f, 0.052f, 0.147f),
                colors.Dark, false, true);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.665f + i * 0.081f;
                b.Cushion(new Vector3(0.004f, 0.043f, z), new Vector3(0.082f, 0.028f, 0.021f),
                    i == 1 ? Gold : colors.Rope, false, true, Quaternion.Euler(0f, i == 1 ? 0f : 24f, 0f));
            }
            b.Cushion(new Vector3(0.119f, 0.061f, 0.748f), new Vector3(0.032f, 0.018f, 0.032f),
                Gold, false);
        }

        private static void PaintHammer(Painter b, BrawlerColors colors)
        {
            b.Tube(new Vector3(0f, 0f, 0.19f), new Vector3(0f, 0f, 0.92f), 0.067f, Wood, true);
            b.Cushion(new Vector3(0f, 0f, 0.60f), new Vector3(0.082f, 0.076f, 0.061f), colors.Dark, true, true);
            Vector3 head = new Vector3(0f, 0f, 0.858f);
            b.Cushion(head, new Vector3(0.220f, 0.115f, 0.157f), Wood, true, true);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Cushion(head + Vector3.right * (side * 0.174f),
                    new Vector3(0.061f, 0.118f, 0.151f), colors.Dark, true, true);
                b.Cushion(head + Vector3.right * (side * 0.161f),
                    new Vector3(0.026f, 0.118f, 0.145f), colors.Rope, false, true);
                b.Cushion(head + Vector3.right * (side * 0.208f),
                    new Vector3(0.023f, 0.090f, 0.117f), colors.Patch, false, true);
            }

            b.Cushion(head + Vector3.up * 0.092f, new Vector3(0.126f, 0.033f, 0.089f),
                colors.Team, false, true, Quaternion.Euler(0f, 13f, 0f));
            b.Tube(head + new Vector3(-0.065f, 0.122f, -0.052f),
                head + new Vector3(0.072f, 0.122f, 0.052f), 0.010f, Thread);
            b.Tube(head + new Vector3(-0.071f, 0.121f, 0.049f),
                head + new Vector3(0.064f, 0.121f, -0.049f), 0.010f, Thread);
            b.Cushion(head + new Vector3(-0.11f, 0.105f, -0.067f), new Vector3(0.022f, 0.016f, 0.022f), Gold, false);
            b.Cushion(head + new Vector3(0.11f, 0.105f, 0.066f), new Vector3(0.022f, 0.016f, 0.022f), Gold, false);
        }

        private readonly struct Model
        {
            public readonly Mesh Paint;
            public readonly Mesh Ink;

            public Model(Mesh paint, Mesh ink)
            {
                Paint = paint;
                Ink = ink;
            }
        }

        private readonly struct BrawlerColors
        {
            public readonly Color32 Team;
            public readonly Color32 Dark;
            public readonly Color32 Face;
            public readonly Color32 Cheek;
            public readonly Color32 Rope;
            public readonly Color32 Patch;
            public readonly Color32 Trim;
            public readonly Color32 Hair;
            public readonly Color32 HairDark;
            public readonly Color32 HairLight;
            public readonly Color32 Iris;

            public BrawlerColors(bool player, int slot)
            {
                Team = Color(player
                    ? slot switch { 0 => 0x2D8FDF, 1 => 0x3C65C7, _ => 0x26B9A8 }
                    : slot switch { 0 => 0xEA5F41, 1 => 0xD1404C, _ => 0xD74875 }, 0.075f);
                Dark = Color(player
                    ? slot switch { 0 => 0x31567D, 1 => 0x403A65, _ => 0x225C64 }
                    : slot switch { 0 => 0x974641, 1 => 0x71334D, _ => 0x8D3B60 }, 0.080f);
                Face = Color(slot switch { 0 => 0xFFC49A, 1 => 0xE69663, _ => 0xC77D60 }, 0.16f);
                Cheek = Color(slot switch { 0 => 0xF79B76, 1 => 0xCC6D51, _ => 0xA95046 }, 0.22f);
                Rope = Color(slot == 2 ? 0xDDB888 : 0xC59766, 0.056f);
                Patch = Color(player
                    ? slot switch { 0 => 0xF8BA48, 1 => 0x69BCD2, _ => 0xFFE27A }
                    : slot switch { 0 => 0xFFD06F, 1 => 0xEB7E62, _ => 0xFDA754 }, 0.062f);
                Trim = Color(player
                    ? slot switch { 0 => 0x71D5FA, 1 => 0x8EC8F3, _ => 0xFFF086 }
                    : slot switch { 0 => 0xFFD18D, 1 => 0xFCAB73, _ => 0xFFCC92 }, 0.12f);
                Hair = Color(slot switch { 0 => 0xB84329, 1 => 0x44415B, _ => 0xE3A442 }, 0.31f);
                HairDark = Color(slot switch { 0 => 0x8A3829, 1 => 0x35263F, _ => 0x9C5C2A }, 0.25f);
                HairLight = Color(slot switch { 0 => 0xDF6336, 1 => 0x5E5C7E, _ => 0xFFC35A }, 0.36f);
                Iris = Color(slot switch { 0 => 0x4A96C0, 1 => 0x84553B, _ => 0x477C5E }, 0.25f);
            }
        }

        private static Color32 Color(int hex, float shine)
        {
            return new Color32((byte)(hex >> 16 & 0xff), (byte)(hex >> 8 & 0xff), (byte)(hex & 0xff),
                (byte)Mathf.RoundToInt(shine * 255f));
        }

        private static readonly Primitive TubeShape = MakeCushion(2f, 8, 5);

        private sealed class Painter
        {
            private readonly MeshData color = new MeshData();
            private readonly MeshData silhouette = new MeshData();

            public void Cushion(Vector3 position, Vector3 radii, Color32 pigment, bool outline, bool textile = false,
                Quaternion? orientation = null)
            {
                Add(textile ? SoftCorners : ArenaDollArt.Cushion, position, radii, pigment,
                    outline, textile, orientation ?? Quaternion.identity);
            }

            public void Molded(Vector3 position, Vector3 radii, Color32 pigment, bool outline,
                Quaternion? orientation = null)
            {
                Add(SoftCorners, position, radii, pigment, outline, false, orientation ?? Quaternion.identity);
            }

            public void Tube(Vector3 from, Vector3 to, float radius, Color32 pigment, bool outline = false)
            {
                Vector3 delta = to - from;
                if (delta.sqrMagnitude < 0.000001f)
                    return;
                Add(TubeShape, (from + to) * 0.5f, new Vector3(radius, delta.magnitude * 0.5f + radius * 0.42f, radius),
                    pigment, outline, false, Quaternion.FromToRotation(Vector3.up, delta));
            }

            private void Add(Primitive shape, Vector3 position, Vector3 radii, Color32 pigment, bool outline,
                bool textile, Quaternion rotation)
            {
                color.Append(shape, position, radii, pigment, textile, rotation);
                if (outline)
                    silhouette.Append(shape, position, radii, pigment, false, rotation);
            }

            public void AxeBlade()
            {
                Vector2[] edge =
                {
                    new(-0.064f, 0.612f), new(0.102f, 0.598f), new(0.203f, 0.548f),
                    new(0.267f, 0.527f), new(0.304f, 0.591f), new(0.317f, 0.676f),
                    new(0.314f, 0.790f), new(0.281f, 0.889f), new(0.223f, 0.962f),
                    new(0.180f, 0.983f), new(0.109f, 0.912f), new(0.032f, 0.878f),
                    new(-0.064f, 0.874f)
                };
                Vector2 middle = new Vector2(0.142f, 0.748f);
                Vector3 crown = PlatePoint(middle, 0.068f);
                Vector3 back = PlatePoint(middle, -0.052f);
                Color32 temper = Color(0xAFC4D4, 0.62f);
                Color32 polishedEdge = Color(0xFAF3DF, 0.71f);
                for (int i = 0; i < edge.Length; i++)
                {
                    int next = (i + 1) % edge.Length;
                    Vector2 innerA = Vector2.Lerp(edge[i], middle, 0.27f);
                    Vector2 innerB = Vector2.Lerp(edge[next], middle, 0.27f);
                    Vector3 a = PlatePoint(edge[i], 0.015f);
                    Vector3 b = PlatePoint(edge[next], 0.015f);
                    Vector3 c = PlatePoint(edge[next], -0.015f);
                    Vector3 d = PlatePoint(edge[i], -0.015f);
                    Vector3 ridgeA = PlatePoint(innerA, 0.057f);
                    Vector3 ridgeB = PlatePoint(innerB, 0.057f);
                    Vector3 undersideA = PlatePoint(innerA, -0.045f);
                    Vector3 undersideB = PlatePoint(innerB, -0.045f);
                    Color32 facet = i >= 3 && i <= 9 ? polishedEdge : Silver;
                    color.Quad(a, ridgeA, ridgeB, b, facet);
                    color.Quad(d, a, b, c, Iron);
                    color.Quad(d, c, undersideB, undersideA, Iron);
                    color.Triangle(crown, ridgeB, ridgeA, temper);
                    color.Triangle(back, undersideA, undersideB, Iron);

                    Vector3 normalA = PlateNormal(edge[i] - middle);
                    Vector3 normalB = PlateNormal(edge[next] - middle);
                    Vector3 topA = (normalA + Vector3.up).normalized;
                    Vector3 topB = (normalB + Vector3.up).normalized;
                    Vector3 bottomA = (normalA + Vector3.down).normalized;
                    Vector3 bottomB = (normalB + Vector3.down).normalized;
                    silhouette.Quad(a, ridgeA, ridgeB, b, Coffee, topA, Vector3.up, Vector3.up, topB);
                    silhouette.Quad(d, a, b, c, Coffee, bottomA, topA, topB, bottomB);
                    silhouette.Quad(d, c, undersideB, undersideA, Coffee, bottomA, bottomB, Vector3.down, Vector3.down);
                    silhouette.Triangle(crown, ridgeB, ridgeA, Coffee, Vector3.up);
                    silhouette.Triangle(back, undersideA, undersideB, Coffee, Vector3.down);
                }
            }

            private static Vector3 PlatePoint(Vector2 point, float height)
            {
                return new Vector3(point.x, height, point.y);
            }

            private static Vector3 PlateNormal(Vector2 point)
            {
                return new Vector3(point.x, 0f, point.y).normalized;
            }

            public void SteelBlade()
            {
                Vector3[] sections =
                {
                    new Vector3(0.125f, 0.047f, 0.27f), new Vector3(0.150f, 0.058f, 0.36f),
                    new Vector3(0.153f, 0.057f, 0.73f), new Vector3(0.111f, 0.048f, 0.85f),
                    new Vector3(0.054f, 0.036f, 0.95f), new Vector3(0.007f, 0.012f, 1.02f)
                };
                Vector2[] sectionShape =
                {
                    new Vector2(-1f, 0), new Vector2(-0.58f, 0.63f), new Vector2(0f, 1f),
                    new Vector2(0.58f, 0.63f), new Vector2(1f, 0), new Vector2(0.58f, -0.63f),
                    new Vector2(0f, -1f), new Vector2(-0.58f, -0.63f)
                };
                Color32 highlight = Color(0xFAF6D9, 0.68f);
                Color32[] strips = { highlight, Silver, Iron, Silver, Iron, Silver, Iron, Silver };
                for (int ring = 0; ring < sections.Length - 1; ring++)
                {
                    for (int edge = 0; edge < sectionShape.Length; edge++)
                    {
                        int next = (edge + 1) % sectionShape.Length;
                        Vector3 a = BladePoint(sections[ring], sectionShape[edge]);
                        Vector3 b = BladePoint(sections[ring + 1], sectionShape[edge]);
                        Vector3 c = BladePoint(sections[ring + 1], sectionShape[next]);
                        Vector3 d = BladePoint(sections[ring], sectionShape[next]);
                        color.Quad(a, b, c, d, strips[edge]);
                        Vector3 n1 = new Vector3(sectionShape[edge].x, sectionShape[edge].y, 0f).normalized;
                        Vector3 n2 = new Vector3(sectionShape[next].x, sectionShape[next].y, 0f).normalized;
                        silhouette.Quad(a, b, c, d, Iron, n1, n1, n2, n2);
                    }
                }
            }

            private static Vector3 BladePoint(Vector3 section, Vector2 position)
            {
                return new Vector3(section.x * position.x, section.y * position.y, section.z);
            }

            public Model Bake(string name) => new Model(color.Build(name), silhouette.Build(name + " contour"));
        }

        private sealed class MeshData
        {
            private readonly List<Vector3> positions = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int> triangles = new List<int>();

            public void Append(Primitive shape, Vector3 position, Vector3 radii, Color32 pigment,
                bool textile, Quaternion orientation)
            {
                int offset = positions.Count;
                var inverseRadii = new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z);
                foreach (Vector3 point in shape.Vertices)
                {
                    Vector3 molded = Vector3.Scale(point, radii);
                    Color32 shade = pigment;
                    if (textile)
                    {
                        float weave = Mathf.Sin(point.x * 38f + point.z * 22f) * Mathf.Sin(point.y * 52f);
                        float falloff = 0.92f + (point.y * 0.045f) + weave * 0.085f;
                        shade.r = (byte)Mathf.Clamp(Mathf.RoundToInt(shade.r * falloff), 0, 255);
                        shade.g = (byte)Mathf.Clamp(Mathf.RoundToInt(shade.g * falloff), 0, 255);
                        shade.b = (byte)Mathf.Clamp(Mathf.RoundToInt(shade.b * falloff), 0, 255);
                    }
                    Vector3 normal = Vector3.Scale(point, inverseRadii).normalized;
                    Vertex(position + orientation * molded, orientation * normal, shade);
                }

                foreach (int triangle in shape.Triangles)
                    triangles.Add(offset + triangle);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 pigment)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Quad(a, b, c, d, pigment, normal, normal, normal, normal);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color32 pigment, Vector3? normal = null)
            {
                Vector3 faceNormal = normal ?? Vector3.Cross(b - a, c - a).normalized;
                int first = positions.Count;
                Vertex(a, faceNormal, pigment);
                Vertex(b, faceNormal, pigment);
                Vertex(c, faceNormal, pigment);
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 pigment,
                Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
            {
                int first = positions.Count;
                Vertex(a, na, pigment);
                Vertex(b, nb, pigment);
                Vertex(c, nc, pigment);
                Vertex(d, nd, pigment);
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }

            private void Vertex(Vector3 point, Vector3 normal, Color32 pigment)
            {
                positions.Add(point);
                normals.Add(normal);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    UnityEngine.Color linear = ((UnityEngine.Color)pigment).linear;
                    linear.a = pigment.a / 255f;
                    pigment = linear;
                }
                colors.Add(pigment);
            }

            public Mesh Build(string name)
            {
                if (positions.Count == 0)
                    return null;
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (positions.Count > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        private readonly struct Primitive
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;

            public Primitive(Vector3[] vertices, int[] triangles)
            {
                Vertices = vertices;
                Triangles = triangles;
            }
        }

        private static Primitive MakeCushion(float power, int columns = 16, int rows = 9)
        {
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var triangles = new List<int>();
            for (int i = 0; i <= rows; i++)
            {
                float phi = Mathf.PI * i / rows;
                for (int j = 0; j <= columns; j++)
                {
                    float theta = 2f * Mathf.PI * j / columns;
                    vertices[i * (columns + 1) + j] = new Vector3(
                        ShapePower(Mathf.Sin(phi) * Mathf.Cos(theta), power),
                        ShapePower(Mathf.Cos(phi), power),
                        ShapePower(Mathf.Sin(phi) * Mathf.Sin(theta), power));
                }
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int top = row * (columns + 1) + col;
                    int bottom = top + columns + 1;
                    if (row != 0) Face(triangles, vertices, top, top + 1, bottom + 1);
                    if (row != rows - 1) Face(triangles, vertices, top, bottom + 1, bottom);
                }
            }
            return new Primitive(vertices, triangles.ToArray());
        }

        private static void Face(List<int> triangles, Vector3[] vertices, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 outward = vertices[a] + vertices[b] + vertices[c];
            bool ordered = Vector3.Dot(cross, outward) > 0f;
            triangles.Add(a);
            triangles.Add(ordered ? b : c);
            triangles.Add(ordered ? c : b);
        }

        private static float ShapePower(float value, float power)
        {
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), 2f / power);
        }
    }
}
