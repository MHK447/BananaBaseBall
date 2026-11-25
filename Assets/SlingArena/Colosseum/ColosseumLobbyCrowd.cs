using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The match spectators in the stands and a friendly exhibition bout on the lobby arena floor.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumLobbyCrowd : RawImage
    {
        const float RoundSeconds = 6f;
        const float FrameInterval = 1f / 30f;
        static readonly Vector2 Feet = new Vector2(.50f, .105f);
        static readonly Vector2 SwordHand = new Vector2(.7254f, .6032f);
        // The atlas also contains a white swatch for the contact shadows, sword blades and impact flash.
        static readonly Vector2 White = new Vector2(2f / 512f, 2f / 256f);

        // Foot positions from the upper left of each stage illustration, mirrored for the opposite stand.
        // The second arena is a harbor. Its spectators stand on the two wooden piers.
        // The fifth to seventh arenas share one round stage: fans on its benches and in its arches.
        static readonly Vector2[][] RearSeats = {
            new[] { new Vector2(.115f,.420f), new Vector2(.181f,.378f), new Vector2(.204f,.369f), new Vector2(.265f,.344f), new Vector2(.295f,.336f) },
            new Vector2[0],
            new[] { new Vector2(.120f,.381f), new Vector2(.194f,.332f), new Vector2(.325f,.308f) },
            new[] { new Vector2(.165f,.373f), new Vector2(.245f,.357f) },
            new[] { new Vector2(.165f,.465f), new Vector2(.275f,.413f) },
            new[] { new Vector2(.165f,.464f), new Vector2(.289f,.407f) },
            new[] { new Vector2(.165f,.465f), new Vector2(.289f,.408f) },
        };
        static readonly Vector2[][] FrontSeats = {
            new[] { new Vector2(.119f,.478f), new Vector2(.152f,.455f), new Vector2(.185f,.436f), new Vector2(.216f,.419f), new Vector2(.249f,.408f), new Vector2(.282f,.396f), new Vector2(.314f,.387f) },
            new[] { new Vector2(.300f,.458f), new Vector2(.337f,.449f), new Vector2(.393f,.462f) },
            new[] { new Vector2(.112f,.440f), new Vector2(.154f,.418f), new Vector2(.197f,.397f), new Vector2(.237f,.390f), new Vector2(.277f,.383f), new Vector2(.320f,.377f) },
            new[] { new Vector2(.118f,.463f), new Vector2(.157f,.440f), new Vector2(.200f,.427f), new Vector2(.242f,.415f), new Vector2(.271f,.410f) },
            new[] { new Vector2(.175f,.552f), new Vector2(.199f,.527f), new Vector2(.230f,.504f), new Vector2(.266f,.484f) },
            new[] { new Vector2(.175f,.552f), new Vector2(.199f,.526f), new Vector2(.230f,.503f), new Vector2(.266f,.483f), new Vector2(.307f,.466f) },
            new[] { new Vector2(.175f,.552f), new Vector2(.199f,.527f), new Vector2(.230f,.504f), new Vector2(.266f,.484f) },
        };
        static readonly float[] CourtDepth = { .665f, .684f, .666f, .681f, .657f, .656f, .656f };

        int mapIndex;
        float clock, redraw;

        protected override void Awake()
        {
            base.Awake();
            texture = Resources.Load<Texture2D>("ColosseumUI/lobby-match-fans");
            raycastTarget = false;
        }

        public void ShowMap(int index)
        {
            mapIndex = index;
            enabled = index >= 0;
            if (enabled) SetVerticesDirty();
        }

        public void Tick(float delta)
        {
            if (!IsActive() || texture == null) return;
            clock = Mathf.Repeat(clock + delta, RoundSeconds * 2f);
            redraw += delta;
            if (redraw < FrameInterval) return;
            redraw %= FrameInterval;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (texture == null || mapIndex < 0) return;
            Rect rect = rectTransform.rect;
            float time = Mathf.Repeat(clock, RoundSeconds);
            bool blueWins = clock < RoundSeconds;
            float victory = Between(time, 3.35f, 3.68f, 5.00f, 5.60f);
            DrawSeats(mesh, rect, RearSeats[mapIndex], true, victory, blueWins);
            DrawSeats(mesh, rect, FrontSeats[mapIndex], false, victory, blueWins);

            float first = Strike(time, 1.35f), reply = Strike(time, 2.16f), finish = Strike(time, 3.10f);
            float size = rect.width * .096f;
            float distance = size * Mathf.Lerp(.98f, .41f, Between(time, .12f, 1.05f, 5.05f, 5.93f));
            float floor = rect.yMax - rect.height * CourtDepth[mapIndex];
            Vector2 leftTip = Duelist(mesh, rect.center.x, floor, size, distance, true, blueWins, time, victory, first, reply, finish, out Vector2 leftGrip);
            Vector2 rightTip = Duelist(mesh, rect.center.x, floor, size, distance, false, !blueWins, time, victory, first, reply, finish, out Vector2 rightGrip);
            float flash = Mathf.Max(Contact(time, 1.35f), Mathf.Max(Contact(time, 2.16f), Contact(time, 3.10f)));
            if (flash > .001f)
            {
                Vector2 leftBlade = leftTip - leftGrip, rightBlade = rightTip - rightGrip;
                float crossed = Cross(leftBlade, rightBlade);
                if (Mathf.Abs(crossed) > .001f)
                {
                    float along = Cross(rightGrip - leftGrip, rightBlade) / crossed;
                    float across = Cross(rightGrip - leftGrip, leftBlade) / crossed;
                    if (along >= 0f && along <= 1f && across >= 0f && across <= 1f)
                        Flash(mesh, leftGrip + leftBlade * along, size * (.05f + .075f * flash), flash);
                }
            }
        }

        void DrawSeats(VertexHelper mesh, Rect rect, Vector2[] seats, bool rear, float victory, bool blueWins)
        {
            for (int bank = 0; bank < 2; bank++)
            {
                bool right = bank == 1;
                for (int i = 0; i < seats.Length; i++)
                {
                    Vector2 spot = seats[i];
                    float phase = i * 2.36f + bank * 1.70f + (rear ? .91f : 0f);
                    float beat = clock * (5.5f + i % 4) + phase;
                    float excitement = .22f + victory * (blueWins != right ? .78f : .38f);
                    float rise = Mathf.Max(0f, Mathf.Sin(beat)) * (.048f + excitement * .18f)
                        + Mathf.Max(0f, Mathf.Sin(clock * 2.6f - spot.x * 8f - bank)) * excitement * .10f;
                    float size = rect.width * (rear ? .053f : .067f);
                    Vector2 stand = new Vector2(rect.xMin + rect.width * (right ? 1f - spot.x : spot.x),
                        rect.yMax - rect.height * spot.y + rise * size);
                    int cell = bank * 4 + (i + (rear ? 1 : 0)) % 4;
                    float angle = Mathf.Sin(beat * .5f + phase) * (3f + excitement * 7f);
                    Portrait(mesh, cell, stand, Vector2.one * size, angle, right, .20f,
                        rear ? new Color32(228, 220, 226, 255) : (Color32)Color.white);
                }
            }
        }

        Vector2 Duelist(VertexHelper mesh, float center, float floor, float size, float distance, bool left, bool winner,
            float time, float victory, float first, float reply, float finish, out Vector2 grip)
        {
            float direction = left ? 1f : -1f;
            float swing = (left ? first : reply) + (winner ? finish : 0f);
            float parry = (left ? reply : first) + (winner ? 0f : finish);
            float windup = Ready(time, left ? 1.35f : 2.16f) + (winner ? Ready(time, 3.10f) : 0f);
            float stride = Between(time, .12f, .35f, .9f, 1.12f) + Between(time, 5.04f, 5.25f, 5.72f, 5.93f);
            float step = Mathf.Sin(15f * time + (left ? 0f : .85f));
            float hop = stride * Mathf.Abs(step) * .065f;
            float tuck = stride * (1f - Mathf.Abs(step)) * .035f;
            if (winner)
            {
                hop += .16f * Bounce(time, 3.70f, .40f) + .12f * Bounce(time, 4.19f, .35f);
                tuck += .10f * Between(time, 3.48f, 3.65f, 3.70f, 3.77f)
                      + .08f * Between(time, 4.06f, 4.12f, 4.18f, 4.26f);
            }
            else tuck += .12f * victory;
            float x = center - direction * distance
                + direction * size * (.12f * swing - .08f * parry - (winner ? 0f : .16f * victory));
            Vector2 rest = new Vector2(x, floor);
            Shadow(mesh, rest, size * .24f, 1f - hop * 2.5f);
            Vector2 foot = rest + Vector2.up * (hop * size);
            float tilt = direction * (2f * Mathf.Sin(time * 5f) + 9f * stride * step + 6f * windup - 8f * swing
                + 11f * parry + (winner ? 0f : (17f + 5f * Mathf.Sin(time * 14f)) * victory));
            Vector2 body = size * new Vector2(1.04f + tuck, .94f - tuck + hop * .18f);
            Portrait(mesh, left ? 2 : 6, foot, body, tilt, !left, Feet.y, Color.white);
            grip = Point(foot, body, tilt, !left, SwordHand - Feet);
            float guard = 54f + 3f * Mathf.Sin(time * 5f + (left ? 0f : .8f));
            float angle = guard + 47f * windup - 17f * swing + 10f * parry;
            angle = Mathf.Lerp(angle, winner ? 99f : -45f, victory);
            Vector2 blade = new Vector2(direction * Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return Sword(mesh, grip, blade, size);
        }

        static float Ease(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        static float Between(float t, float a, float b, float c, float d) => Ease(t, a, b) * (1f - Ease(t, c, d));
        static float Ready(float t, float hit) => Between(t, hit - .40f, hit - .22f, hit - .18f, hit - .06f);
        static float Strike(float t, float hit) => Between(t, hit - .16f, hit, hit + .05f, hit + .34f);
        static float Contact(float t, float hit) => Between(t, hit - .025f, hit, hit + .035f, hit + .19f);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float Bounce(float time, float start, float duration)
        {
            float t = Mathf.Clamp01((time - start) / duration);
            return 4f * t * (1f - t);
        }

        // The projected foot and sword palm use the same transform as the baked model.
        static Vector2 Point(Vector2 origin, Vector2 size, float angle, bool mirror, Vector2 local)
        {
            float a = angle * Mathf.Deg2Rad;
            local = Vector2.Scale(local, size);
            if (mirror) local.x = -local.x;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return origin + new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }

        static void Portrait(VertexHelper mesh, int cell, Vector2 anchor, Vector2 size, float angle, bool mirror,
            float bottom, Color32 tint)
        {
            int first = mesh.currentVertCount;
            // Crop the transparent foot padding before placing the character against the rail or floor.
            for (int i = 0; i < 4; i++)
            {
                float u = i == 1 || i == 2 ? .993f : .007f;
                float v = i >= 2 ? .993f : bottom;
                Vector2 p = Point(anchor, size, angle, mirror, new Vector2(u - .5f, v - bottom));
                mesh.AddVert(p, tint, new Vector2((cell % 4 + u) * .25f, (1 - cell / 4 + v) * .5f));
            }
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }

        Vector2 Sword(VertexHelper mesh, Vector2 grip, Vector2 blade, float size)
        {
            Vector2 edge = new Vector2(-blade.y, blade.x);
            Vector2 guard = grip + blade * (.079f * size);
            Vector2 neck = guard + blade * (.025f * size), shoulder = guard + blade * (.285f * size);
            Vector2 tip = guard + blade * (.355f * size);
            Color32 steel = new Color32(106, 162, 205, 255), light = new Color32(236, 250, 255, 255);
            Color32 ink = new Color32(61, 54, 75, 255), gold = new Color32(255, 197, 68, 255);
            float halfPixel = .65f / Mathf.Max(.01f, canvas.scaleFactor);
            Line(mesh, grip - blade * (.036f * size), guard, size * .030f, ink, halfPixel);
            Line(mesh, grip - blade * (.027f * size), guard, size * .016f, new Color32(179, 112, 57, 255), halfPixel);
            Vector2 wide = edge * (.044f * size);
            SoftPolygon(mesh, stackalloc Vector2[] { neck - wide, neck + wide, shoulder + wide * .82f, tip, shoulder - wide * .82f }, ink, halfPixel);
            Vector2 point = tip - blade * (.027f * size);
            SoftPolygon(mesh, stackalloc Vector2[] { neck - wide * .70f, neck + wide * .70f,
                shoulder + wide * .57f, point, shoulder - wide * .57f }, steel, halfPixel);
            SoftPolygon(mesh, stackalloc Vector2[] { neck, neck + wide * .70f, shoulder + wide * .57f, point }, light, halfPixel);
            Line(mesh, guard - edge * (.083f * size), guard + edge * (.083f * size), .031f * size, ink, halfPixel);
            Line(mesh, guard - edge * (.075f * size), guard + edge * (.075f * size), .017f * size, gold, halfPixel);
            return tip;
        }

        static void Shadow(VertexHelper mesh, Vector2 floor, float width, float strength)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(floor, new Color32(83, 47, 51, (byte)(95f * strength)), White);
            for (int i = 0; i <= 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector2 edge = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .24f);
                mesh.AddVert(floor + width * edge, new Color32(83, 47, 51, 0), White);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        static void Flash(VertexHelper mesh, Vector2 center, float size, float strength)
        {
            Color32 glint = new Color32(255, 246, 170, (byte)(255f * strength));
            Quad(mesh, center - Vector2.right * size, center + Vector2.up * size * .24f,
                center + Vector2.right * size, center - Vector2.up * size * .24f, glint);
            Quad(mesh, center - Vector2.up * size, center - Vector2.right * size * .27f,
                center + Vector2.up * size, center + Vector2.right * size * .27f, glint);
        }

        static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float halfWidth, Color32 color, float halfPixel)
        {
            Vector2 axis = (b - a).normalized, edge = new Vector2(-axis.y, axis.x) * halfWidth;
            SoftPolygon(mesh, stackalloc Vector2[] { a - edge, a + edge, b + edge, b - edge }, color, halfPixel);
        }

        // A single coverage fringe around each clockwise convex shape keeps the blade tip
        // and guard continuous through the swing, even when a bevel is narrower than a pixel.
        static void SoftPolygon(VertexHelper mesh, System.ReadOnlySpan<Vector2> points, Color32 tint, float halfPixel)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 p in points) center += p;
            center /= points.Length;
            System.Span<Vector2> normals = stackalloc Vector2[points.Length];
            float inset = halfPixel;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 side = (points[(i + 1) % points.Length] - points[i]).normalized;
                normals[i] = new Vector2(-side.y, side.x);
                inset = Mathf.Min(inset, .9f * Vector2.Dot(points[i] - center, normals[i]));
            }
            tint.a = (byte)Mathf.RoundToInt(tint.a * (2f * inset / (inset + halfPixel)));
            Color32 clear = tint;
            clear.a = 0;
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, White);
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 prev = normals[(i + points.Length - 1) % points.Length];
                Vector2 join = (prev + normals[i]) / Mathf.Max(.30f, 1f + Vector2.Dot(prev, normals[i]));
                mesh.AddVert(points[i] - join * inset, tint, White);
                mesh.AddVert(points[i] + join * halfPixel, clear, White);
                int a = first + 1 + 2 * i, b = first + 1 + 2 * ((i + 1) % points.Length);
                mesh.AddTriangle(first, a, b);
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, White); mesh.AddVert(b, tint, White);
            mesh.AddVert(c, tint, White); mesh.AddVert(d, tint, White);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
