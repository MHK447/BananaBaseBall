using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Weapon = SlingArena.ArenaArt.WeaponStyle;

namespace SlingArena
{
    internal sealed class ArenaFeedback : IDisposable
    {
        private const int RingSegments = 64;
        private const float AimFloorHeight = 0.068f;
        private const float AimHalfWidth = 0.34f;
        private const float AimEndRadius = 0.36f;
        private const float AimWallMargin = 0.40f;
        private const float EffectLifetime = 0.72f;
        private const float LaunchBurstLifetime = 0.30f;
        private const float StabPulseLifetime = 0.24f;

        private static readonly Color PlayerColor = new Color32(46, 205, 255, 255);
        private static readonly Color RivalColor = new Color32(255, 114, 66, 255);
        private static readonly Color SparkColor = new Color32(255, 249, 173, 255);
        private static readonly Color Crimson = new Color32(160, 10, 36, 255);
        private static readonly Color Scarlet = new Color32(246, 51, 66, 255);
        private static readonly Color ChainColor = new Color32(255, 210, 82, 255);
        private static readonly Color CleaveColor = new Color32(137, 255, 210, 255);
        private static readonly Color HammerColor = new Color32(255, 167, 77, 255);
        private static readonly Color Ink = new Color32(38, 33, 42, 255);
        private static readonly Color32 AimFill = new Color32(103, 108, 112, 79);
        private static readonly Color32 AimEdge = new Color32(255, 251, 222, 177);
        private static readonly Color32 AimChevron = new Color32(255, 251, 231, 132);
        private static readonly Color32 AimEndShadow = new Color32(91, 86, 75, 155);
        private static readonly Color32 AimArrowInk = new Color32(20, 64, 115, 255);
        private static readonly Color32 AimArrowRim = new Color32(142, 226, 255, 255);
        private static readonly Color32 AimArrowBlue = new Color32(44, 176, 247, 255);

        private struct Spark
        {
            public Transform Transform;
            public Vector3 Origin;
            public Vector3 Velocity;
            public Vector3 Size;
            public float Lifetime;
        }

        private sealed class Hit
        {
            public Transform Root;
            public TMP_Text Label;
            public LineRenderer Ring;
            public LineRenderer Accent;
            public Spark[] Sparks;
            public Vector3 Point;
            public Vector3 Direction;
            public Color Color;
            public float Age;
            public bool Chain;
            public Weapon Weapon;
        }

        private sealed class UnitMarker
        {
            public Transform Root;
            public LineRenderer Border;
            public LineRenderer Ring;
            public LineRenderer HealthTrack;
            public LineRenderer HealthFill;
            public TMP_Text Number;
        }

        private readonly Transform root;
        private readonly Camera camera;
        private readonly TMP_FontAsset font;
        private readonly Material outline;
        private readonly Material strokeMaterial;
        private readonly Material goldMaterial;
        private readonly Material crimsonMaterial;
        private readonly Material scarletMaterial;
        private readonly UnitMarker[] unitMarkers = new UnitMarker[ArenaCombat.UnitsPerSide * 2];
        private readonly MeshRenderer aimRenderer;
        private readonly Mesh aimMesh;
        private readonly LineRenderer aimedRival;
        private readonly TrailRenderer weaponTrail;
        private readonly LineRenderer[] launchStreaks = new LineRenderer[5];
        private readonly Vector3[] ringDirections = new Vector3[RingSegments];
        private readonly Vector3[] ringPositions = new Vector3[RingSegments];
        private readonly List<Vector3> aimVertices = new List<Vector3>(512);
        private readonly List<Color32> aimColors = new List<Color32>(512);
        private readonly List<int> aimTriangles = new List<int>(768);
        private Vector3 aimOrigin;
        private Vector3 aimAxis;
        private Vector3 aimRight;
        private Matrix4x4 aimLocalSpace;
        private readonly List<Hit> hits = new List<Hit>();
        private readonly ArenaAtmosphere atmosphere;
        private readonly AudioSource audio;
        private readonly AudioClip select;
        private readonly AudioClip launch;
        private readonly AudioClip stab;
        private readonly AudioClip cleave;
        private readonly AudioClip hammer;
        private readonly AudioClip impact;
        private readonly AudioClip victory;
        private readonly AudioClip defeat;
        private float launchBurstAge = LaunchBurstLifetime;
        private float launchStrength;
        private Vector3 launchOrigin;
        private Vector3 launchDirection;
        private Color launchTint;

        public ArenaFeedback(Transform parent, Camera camera, TMP_FontAsset font, Material outline, Shader shader)
        {
            this.camera = camera;
            this.font = font;
            this.outline = outline;
            root = new GameObject("Aim and hit feedback").transform;
            root.SetParent(parent, false);
            atmosphere = new ArenaAtmosphere(root, camera);
            strokeMaterial = new Material(shader) { name = "Arena strokes" };
            goldMaterial = new Material(shader) { name = "Toy impact sparks", color = SparkColor };
            crimsonMaterial = new Material(shader) { name = "Sword hit crimson drops", color = Crimson };
            scarletMaterial = new Material(shader) { name = "Sword hit scarlet drops", color = Scarlet };

            var aimSurface = new GameObject("Pull and release guide", typeof(MeshFilter), typeof(MeshRenderer));
            aimSurface.transform.SetParent(root, false);
            aimMesh = new Mesh { name = "Aim corridor" };
            aimMesh.MarkDynamic();
            aimSurface.GetComponent<MeshFilter>().sharedMesh = aimMesh;
            aimRenderer = aimSurface.GetComponent<MeshRenderer>();
            aimRenderer.sharedMaterial = strokeMaterial;
            aimRenderer.shadowCastingMode = ShadowCastingMode.Off;
            aimRenderer.receiveShadows = false;
            aimRenderer.enabled = false;
            aimedRival = CreateLine(root, "Aimed rival", 0.062f, new Color32(225, 45, 44, 234), RingSegments, true);
            for (int i = 0; i < launchStreaks.Length; i++)
                launchStreaks[i] = CreateLine(root, "Rear push streak " + i, 0.048f, Color.white, 3);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / RingSegments);
                ringDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            for (int i = 0; i < ArenaCombat.UnitsPerSide; i++)
            {
                unitMarkers[i] = CreateUnitMarker(true, i);
                unitMarkers[i + ArenaCombat.UnitsPerSide] = CreateUnitMarker(false, i);
            }

            weaponTrail = new GameObject("Weapon motion trail", typeof(TrailRenderer)).GetComponent<TrailRenderer>();
            weaponTrail.transform.SetParent(root, false);
            weaponTrail.sharedMaterial = strokeMaterial;
            weaponTrail.time = 0.19f;
            weaponTrail.minVertexDistance = 0.04f;
            weaponTrail.startWidth = 0.32f;
            weaponTrail.endWidth = 0.025f;
            weaponTrail.numCapVertices = 3;
            weaponTrail.shadowCastingMode = ShadowCastingMode.Off;
            weaponTrail.receiveShadows = false;
            weaponTrail.emitting = false;

            audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = 0.45f;
            select = CreateClip("Wooden toggle", 0.105f,
                (time, span) => Tone((560f + 250f * span) * time) * Mathf.Exp(-span * 5f));
            launch = CreateClip("Rear shove", 0.24f, (time, span) =>
                Tone(time * 120f) * Mathf.Exp(-span * 14f) * 0.67f +
                (Noise(time) * 0.34f + Tone((330f - 170f * span) * time) * 0.29f) *
                Mathf.Sin(Mathf.PI * span) * (1f - span));
            stab = CreateClip("Blade contact", 0.15f, (time, span) =>
                (Tone(time * 1030f) * 0.34f + Tone((415f - 180f * span) * time) * 0.43f +
                 Noise(time) * 0.33f) * Mathf.Exp(-span * 8f));
            cleave = CreateClip("Axe cleave", 0.22f, (time, span) =>
                (Tone((850f - 530f * span) * time) * .30f + Noise(time) * .52f) * Mathf.Exp(-span * 5f) +
                Tone(time * 176f) * Mathf.Exp(-span * 12f) * .35f);
            hammer = CreateClip("Hammer impact", 0.30f, (time, span) =>
                Tone((139f - 62f * span) * time) * Mathf.Exp(-span * 5f) * .77f +
                (Tone(time * 463f) * .27f + Noise(time) * .31f) * Mathf.Exp(-span * 15f));
            impact = CreateClip("Toy knock", 0.17f, (time, span) =>
                (Tone((245f - 110f * span) * time) * 0.58f + Noise(time) * 0.42f) * Mathf.Exp(-span * 6f));
            victory = CreateClip("Round won", 0.42f, (time, span) => Stinger(time, 440f, 587.33f, 740f));
            defeat = CreateClip("Round lost", 0.42f, (time, span) => Stinger(time, 440f, 349.23f, 293.66f));
        }

        private UnitMarker CreateUnitMarker(bool player, int index)
        {
            var marker = new GameObject($"{(player ? "Ally" : "Rival")} {index + 1} indicator").transform;
            marker.SetParent(root, false);
            Color color = player ? PlayerColor : RivalColor;
            var unit = new UnitMarker
            {
                Root = marker,
                Border = CreateLine(marker, "Unit ring border", 0.09f, Ink, RingSegments, true),
                Ring = CreateLine(marker, "Unit ring", 0.055f, color, RingSegments, true),
                HealthTrack = CreateLine(marker, "Health track", 0.13f, Ink, 2),
                HealthFill = CreateLine(marker, "Health fill", 0.072f, color, 2),
                Number = CreateText(marker, "Unit number", (index + 1).ToString(), 3.2f, new Vector2(0.35f, 0.35f))
            };
            unit.Border.enabled = unit.Ring.enabled = unit.HealthTrack.enabled = unit.HealthFill.enabled = true;
            marker.gameObject.SetActive(false);
            return unit;
        }

        public void SetUnitMarker(bool player, int index, Vector3 position, int health, int maxHealth, bool active)
        {
            UnitMarker marker = unitMarkers[index + (player ? 0 : ArenaCombat.UnitsPerSide)];
            marker.Root.gameObject.SetActive(health > 0);
            if (health <= 0)
                return;

            Color color = player ? PlayerColor : RivalColor;
            Color ringColor = active ? ChainColor : color;
            marker.Ring.startColor = marker.Ring.endColor = ringColor;
            marker.Border.widthMultiplier = active ? 0.12f : 0.085f;
            marker.Ring.widthMultiplier = active ? 0.076f : 0.051f;
            float radius = active ? 0.66f : 0.60f;
            SetRing(marker.Border, new Vector3(position.x, 0.025f, position.z), radius);
            SetRing(marker.Ring, new Vector3(position.x, 0.033f, position.z), radius);

            Transform view = camera.transform;
            Vector3 left = position + view.up * 1.12f - view.forward * 2.0f - view.right * 0.30f;
            Vector3 right = left + view.right * 0.74f;
            marker.HealthTrack.SetPosition(0, left);
            marker.HealthTrack.SetPosition(1, right);
            Vector3 above = -view.forward * 0.018f;
            marker.HealthFill.SetPosition(0, left + above);
            marker.HealthFill.SetPosition(1, Vector3.Lerp(left, right, (float)health / maxHealth) + above);
            marker.Number.transform.SetPositionAndRotation(left - view.right * 0.17f + above, view.rotation);
        }

        public static float GetAimDistance(Vector3 position, Vector2 direction, float power)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return 0f;

            direction.Normalize();
            float range = Mathf.SmoothStep(2.2f, 5.80f, power);
            float edgeX = DistanceToAimEdge(position.x, direction.x, ArenaCombat.HalfWidth - AimWallMargin);
            float edgeZ = DistanceToAimEdge(position.z, direction.y, ArenaCombat.HalfDepth - AimWallMargin);
            return Mathf.Max(0f, Mathf.Min(range, Mathf.Min(edgeX, edgeZ)));
        }

        private static float DistanceToAimEdge(float start, float heading, float limit)
        {
            if (Mathf.Abs(heading) < 0.0001f)
                return float.PositiveInfinity;
            return (Mathf.Sign(heading) * limit - start) / heading;
        }

        public void SetAim(Vector3 position, Vector2 direction, float power, bool visible, Vector3? target = null)
        {
            float distance = visible ? GetAimDistance(position, direction, power) : 0f;
            if (distance < 0.13f)
            {
                HideAim();
                return;
            }

            aimOrigin = new Vector3(position.x, AimFloorHeight, position.z);
            aimAxis = new Vector3(direction.x, 0f, direction.y).normalized;
            aimRight = Vector3.Cross(Vector3.up, aimAxis);
            aimLocalSpace = aimRenderer.transform.worldToLocalMatrix;
            aimVertices.Clear();
            aimColors.Clear();
            aimTriangles.Clear();

            float start = Mathf.Min(0.26f, distance * 0.3f);
            float radius = Mathf.Min(AimEndRadius, distance * 0.6f);
            float width = radius * AimHalfWidth / AimEndRadius;
            AddAimRectangle(-width, width, start, distance, AimFill);
            AddAimRectangle(-width, -width + 0.028f, start, distance, AimEdge);
            AddAimRectangle(width - 0.028f, width, start, distance, AimEdge);

            for (float marker = 1.57f; marker < distance - radius - 0.08f; marker += 0.56f)
            {
                AddAimTriangle(new Vector2(-0.19f, marker),
                    new Vector2(0.19f, marker), new Vector2(0f, marker + 0.19f), AimChevron);
            }

            AddAimCircle(distance, radius, 0.06f, AimEndShadow);
            AddAimCircle(distance, radius - 0.006f, 0.027f, AimEdge);

            float arrowStart = Mathf.Min(1.22f, distance * 0.46f);
            float arrowLength = Mathf.Min(Mathf.Lerp(0.98f, 1.62f, power), distance - arrowStart);
            float arrowHalfWidth = Mathf.Min(0.43f, arrowLength * 0.46f);
            AddAimArrow(arrowStart, arrowLength, arrowHalfWidth, AimArrowInk);
            AddAimArrow(arrowStart + arrowLength * 0.045f, arrowLength * 0.88f, arrowHalfWidth * 0.86f, AimArrowRim);
            AddAimArrow(arrowStart + arrowLength * 0.069f, arrowLength * 0.82f, arrowHalfWidth * 0.80f, AimArrowBlue);

            aimMesh.Clear();
            aimMesh.SetVertices(aimVertices);
            aimMesh.SetColors(aimColors);
            aimMesh.SetTriangles(aimTriangles, 0);
            aimRenderer.enabled = true;
            aimedRival.enabled = target.HasValue;
            if (target is Vector3 rival)
                SetRing(aimedRival, new Vector3(rival.x, AimFloorHeight + 0.015f, rival.z), 0.74f);
        }

        public void HideAim()
        {
            aimRenderer.enabled = false;
            aimedRival.enabled = false;
        }

        private void AddAimArrow(float start, float length, float headHalfWidth, Color32 color)
        {
            float neck = start + length * 0.59f;
            float stem = headHalfWidth * 0.47f;
            AddAimRectangle(-stem, stem, start, neck, color);
            AddAimTriangle(new Vector2(-headHalfWidth, neck), new Vector2(headHalfWidth, neck),
                new Vector2(0f, start + length), color);
        }

        private void AddAimCircle(float center, float radius, float stroke, Color32 color)
        {
            float inner = Mathf.Max(0f, radius - stroke);
            for (int i = 0; i < RingSegments; i += 2)
            {
                Vector3 from = ringDirections[i];
                Vector3 to = ringDirections[(i + 2) % RingSegments];
                AddAimQuad(new Vector2(from.x * radius, center + from.z * radius),
                    new Vector2(to.x * radius, center + to.z * radius),
                    new Vector2(to.x * inner, center + to.z * inner),
                    new Vector2(from.x * inner, center + from.z * inner), color);
            }
        }

        private void AddAimRectangle(float left, float right, float start, float end, Color32 color)
        {
            AddAimQuad(new Vector2(left, start), new Vector2(right, start),
                new Vector2(right, end), new Vector2(left, end), color);
        }

        private void AddAimQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int start = aimVertices.Count;
            AddAimTriangle(a, b, c, color);
            AddAimVertex(d, color);
            aimTriangles.Add(start);
            aimTriangles.Add(start + 2);
            aimTriangles.Add(start + 3);
        }

        private void AddAimTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = aimVertices.Count;
            AddAimVertex(a, color);
            AddAimVertex(b, color);
            AddAimVertex(c, color);
            aimTriangles.Add(start);
            aimTriangles.Add(start + 1);
            aimTriangles.Add(start + 2);
        }

        private void AddAimVertex(Vector2 point, Color32 color)
        {
            Vector3 worldPoint = aimOrigin + aimRight * point.x + aimAxis * point.y;
            worldPoint.x = Mathf.Clamp(worldPoint.x, -ArenaCombat.HalfWidth, ArenaCombat.HalfWidth);
            worldPoint.z = Mathf.Clamp(worldPoint.z, -ArenaCombat.HalfDepth, ArenaCombat.HalfDepth);
            aimVertices.Add(aimLocalSpace.MultiplyPoint3x4(worldPoint));
            aimColors.Add(color);
        }

        public void SetWeaponTrail(bool visible, Vector3 tip, bool player, Weapon weapon)
        {
            if (!visible)
            {
                weaponTrail.emitting = false;
                return;
            }

            weaponTrail.transform.position = tip + Vector3.up * 0.025f;
            if (weaponTrail.emitting)
                return;

            weaponTrail.Clear();
            Color tint = weapon switch
            {
                Weapon.Axe => CleaveColor,
                Weapon.Hammer => HammerColor,
                _ => player ? PlayerColor : RivalColor
            };
            weaponTrail.startWidth = weapon == Weapon.Hammer ? .47f : .32f;
            weaponTrail.startColor = Color.Lerp(Color.white, tint, .26f);
            Color end = tint;
            end.a = 0f;
            weaponTrail.endColor = end;
            weaponTrail.emitting = true;
        }

        public void ShowLaunch(Vector3 origin, Vector3 direction, float strength, bool player)
        {
            launchOrigin = new Vector3(origin.x, 0.10f, origin.z);
            launchDirection = direction;
            launchStrength = strength;
            launchTint = Color.Lerp(player ? PlayerColor : RivalColor, SparkColor, 0.65f);
            launchBurstAge = 0f;
            UpdateLaunchBurst(0f);
            atmosphere.ShowLaunch(origin, direction, strength, player ? PlayerColor : RivalColor);
            audio.PlayOneShot(launch, Mathf.Lerp(0.55f, 1f, strength));
        }

        private void UpdateLaunchBurst(float deltaTime)
        {
            if (launchBurstAge >= LaunchBurstLifetime)
                return;

            launchBurstAge += deltaTime;
            float progress = Mathf.Clamp01(launchBurstAge / LaunchBurstLifetime);
            Vector3 sideways = Vector3.Cross(Vector3.up, launchDirection);
            for (int i = 0; i < launchStreaks.Length; i++)
            {
                var streak = launchStreaks[i];
                streak.enabled = progress < 1f;
                if (!streak.enabled)
                    continue;

                float branch = (i - 2) * 0.19f;
                Vector3 lead = launchOrigin - launchDirection * (0.49f + progress * 0.17f) + sideways * branch;
                Vector3 wake = launchOrigin - launchDirection * (0.78f + launchStrength * 0.56f + progress * 0.48f)
                    + sideways * (branch * (1.45f + progress * 0.6f));
                streak.SetPosition(0, lead);
                streak.SetPosition(1, Vector3.Lerp(lead, wake, 0.53f) + launchDirection * 0.06f);
                streak.SetPosition(2, wake);
                Color front = Color.Lerp(launchTint, Color.white, 0.35f);
                front.a = (1f - progress) * (0.86f + launchStrength * 0.12f);
                Color tail = launchTint;
                tail.a = front.a * 0.15f;
                streak.startColor = front;
                streak.endColor = tail;
                streak.widthMultiplier = Mathf.Lerp(0.065f, 0.02f, progress);
            }
        }

        public void ShowImpact(Vector3 point, int damage, bool chain, ArenaArt.Part part, Weapon weapon, Vector3 direction)
        {
            string hitName = weapon == Weapon.Sword ? "Blade" : weapon.ToString();
            var holder = new GameObject(chain ? "Chain impact" : $"{hitName} impact ({part})").transform;
            holder.SetParent(root, false);
            (string action, Color color, AudioClip clip) = chain ? ("CHAIN", ChainColor, impact) : weapon switch
            {
                Weapon.Axe => ("CLEAVE", CleaveColor, cleave),
                Weapon.Hammer => ("SMASH", HammerColor, hammer),
                _ => ("STAB", Crimson, stab)
            };
            string damageText = $"<size=72%>{action}</size> -{damage}";
            TMP_Text label = CreateText(holder, "Damage", damageText, 4.6f, new Vector2(3.0f, 0.55f));
            if (chain || weapon != Weapon.Sword)
                label.color = Color.Lerp(color, Color.white, .48f);
            label.transform.position = DamageLabelPosition(point, 0f);
            label.transform.rotation = camera.transform.rotation;

            var hit = new Hit
            {
                Root = holder,
                Label = label,
                Ring = CreateLine(holder, "Impact pulse", 0.055f, color, RingSegments, chain || weapon != Weapon.Axe),
                Sparks = SpawnImpactParticles(holder, point, part, chain, weapon, direction),
                Point = point,
                Direction = direction,
                Color = color,
                Chain = chain,
                Weapon = weapon
            };
            if (!chain && weapon != Weapon.Sword)
                hit.Accent = CreateLine(holder, "Impact highlight", .035f, Color.white, RingSegments, weapon == Weapon.Hammer);
            UpdateImpactRing(hit);
            hits.Add(hit);
            atmosphere.ShowImpact(point, chain, weapon, direction);
            audio.PlayOneShot(clip, chain ? 0.73f : 0.87f);
        }

        private Spark[] SpawnImpactParticles(Transform holder, Vector3 point, ArenaArt.Part part, bool chain,
            Weapon weapon, Vector3 direction)
        {
            bool drops = !chain && weapon != Weapon.Hammer;
            bool broadHit = part == ArenaArt.Part.Torso || part == ArenaArt.Part.Hips;
            bool headHit = part == ArenaArt.Part.Head;
            int count = drops ? broadHit ? 11 : 8 : chain ? 7 : 12;
            var sparks = new Spark[count];
            Transform view = camera.transform;
            Vector3 origin = chain ? point : point - view.forward * 0.39f;
            for (int i = 0; i < count; i++)
            {
                var bit = GameObject.CreatePrimitive(drops ? PrimitiveType.Sphere : PrimitiveType.Cube);
                var collider = bit.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
                bit.name = drops ? "Crimson droplet" : "Spark";
                bit.transform.SetParent(holder, false);
                float angle = 2f * Mathf.PI * i / count;
                Vector3 radial = drops ? view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle)
                    : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float scale = drops ? .072f + (i % 4) * .017f : .070f;
                Vector3 size = new Vector3(scale, scale, scale * (drops ? 1.4f : 2.19f));
                float speed = drops ? (broadHit ? 2.75f : 2.1f) + (i % 3) * .34f : 2.9f;
                float lift = headHit && drops ? 1.45f : .95f;
                Vector3 velocity = radial * speed + Vector3.up * (lift + 0.22f * (i % 3));
                if (!chain && weapon != Weapon.Sword)
                    velocity += direction * (weapon == Weapon.Axe ? 1.7f : 2.2f);
                bit.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(velocity));
                bit.transform.localScale = size;
                var renderer = bit.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = !drops ? goldMaterial : i % 3 == 0 ? scarletMaterial : crimsonMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                sparks[i] = new Spark
                {
                    Transform = bit.transform,
                    Origin = origin,
                    Velocity = velocity,
                    Size = size,
                    Lifetime = drops ? .31f + .035f * (i % 3) : EffectLifetime
                };
            }
            return sparks;
        }

        private void UpdateImpactRing(Hit hit)
        {
            float lifetime = hit.Chain ? EffectLifetime : hit.Weapon == Weapon.Hammer ? .51f :
                hit.Weapon == Weapon.Axe ? .35f : StabPulseLifetime;
            float t = Mathf.Clamp01(hit.Age / lifetime);
            hit.Ring.enabled = t < 1f;
            Color color = hit.Color;
            color.a = 1f - t;
            hit.Ring.startColor = hit.Ring.endColor = color;
            hit.Ring.widthMultiplier = Mathf.Lerp(0.055f, 0.01f, t);

            if (hit.Chain)
            {
                SetRing(hit.Ring, new Vector3(hit.Point.x, 0.062f, hit.Point.z), Mathf.Lerp(0.2f, 1.32f, t));
                return;
            }

            if (hit.Weapon != Weapon.Sword)
            {
                hit.Accent.enabled = t < 1f;
                Color highlight = Color.Lerp(hit.Color, Color.white, .8f);
                highlight.a = (1f - t) * .88f;
                hit.Accent.startColor = hit.Accent.endColor = highlight;
                hit.Accent.widthMultiplier = Mathf.Lerp(.03f, .008f, t);
                if (hit.Weapon == Weapon.Hammer)
                {
                    Vector3 floor = new Vector3(hit.Point.x, .082f, hit.Point.z) + hit.Direction * (.15f + t * .45f);
                    float radius = Mathf.Lerp(.38f, 1.62f, Mathf.Sqrt(t));
                    hit.Ring.widthMultiplier *= 2.35f;
                    SetRing(hit.Ring, floor, radius);
                    SetRing(hit.Accent, floor + Vector3.up * .01f, radius * .72f);
                    return;
                }

                Vector3 arcCenter = hit.Point - camera.transform.forward * .36f + hit.Direction * (t * .58f);
                float spread = Mathf.Lerp(.42f, 1.04f, t);
                hit.Ring.widthMultiplier *= 2.15f;
                SetCleaveArc(hit.Ring, arcCenter, hit.Direction, spread);
                SetCleaveArc(hit.Accent, arcCenter + Vector3.up * .025f, hit.Direction, spread * .87f);
                return;
            }

            Transform view = camera.transform;
            Vector3 center = hit.Point - view.forward * 0.24f;
            SetRing(hit.Ring, center, Mathf.Lerp(0.11f, 0.43f, t), view.right, view.up);
        }

        private void SetCleaveArc(LineRenderer line, Vector3 center, Vector3 direction, float radius)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = Mathf.Lerp(-1.22f, 1.22f, (float)i / (RingSegments - 1));
                ringPositions[i] = center + (direction * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius;
            }
            line.SetPositions(ringPositions);
        }

        private TMP_Text CreateText(Transform parent, string name, string value, float size, Vector2 bounds)
        {
            var label = new GameObject(name, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSharedMaterial = outline;
            label.text = value;
            label.color = Color.white;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = bounds;
            return label;
        }

        public void Tick(float deltaTime)
        {
            atmosphere.Tick(deltaTime);
            UpdateLaunchBurst(deltaTime);
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                Hit hit = hits[i];
                hit.Age += deltaTime;
                if (hit.Age >= EffectLifetime)
                {
                    Object.Destroy(hit.Root.gameObject);
                    hits.RemoveAt(i);
                    continue;
                }

                float t = hit.Age / EffectLifetime;
                UpdateImpactRing(hit);
                hit.Label.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, t));
                hit.Label.transform.position = DamageLabelPosition(hit.Point, t);
                hit.Label.transform.rotation = camera.transform.rotation;

                foreach (Spark spark in hit.Sparks)
                {
                    spark.Transform.position = spark.Origin + spark.Velocity * hit.Age +
                        Vector3.down * (5f * hit.Age * hit.Age);
                    spark.Transform.localScale = spark.Size * (1f - Mathf.Clamp01(hit.Age / spark.Lifetime));
                }
            }
        }

        public void ShowWeaponDraw(Vector3 position, Weapon weapon)
        {
            Color tint = weapon == Weapon.Axe ? CleaveColor : weapon == Weapon.Hammer ? HammerColor : PlayerColor;
            atmosphere.ShowWeaponDraw(position, tint);
            PlaySelect();
        }

        public void PlaySelect() => audio.PlayOneShot(select, 0.52f);
        public void PlayResult(bool won) => audio.PlayOneShot(won ? victory : defeat, 0.6f);

        public void Clear()
        {
            HideAim();
            atmosphere.Clear();
            weaponTrail.emitting = false;
            weaponTrail.Clear();
            launchBurstAge = LaunchBurstLifetime;
            foreach (LineRenderer streak in launchStreaks)
                streak.enabled = false;
            foreach (Hit hit in hits)
                Object.Destroy(hit.Root.gameObject);
            hits.Clear();
        }

        public void Dispose()
        {
            atmosphere.Dispose();
            Object.Destroy(aimMesh);
            Object.Destroy(strokeMaterial);
            Object.Destroy(goldMaterial);
            Object.Destroy(crimsonMaterial);
            Object.Destroy(scarletMaterial);
            Object.Destroy(select);
            Object.Destroy(launch);
            Object.Destroy(stab);
            Object.Destroy(cleave);
            Object.Destroy(hammer);
            Object.Destroy(impact);
            Object.Destroy(victory);
            Object.Destroy(defeat);
        }

        private LineRenderer CreateLine(Transform parent, string name, float width, Color color, int points,
            bool loop = false)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.useWorldSpace = true;
            line.sharedMaterial = strokeMaterial;
            line.positionCount = points;
            line.loop = loop;
            line.numCapVertices = loop ? 0 : 3;
            line.numCornerVertices = 2;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void SetRing(LineRenderer line, Vector3 position, float radius)
        {
            SetRing(line, position, radius, Vector3.right, Vector3.forward);
        }

        private void SetRing(LineRenderer line, Vector3 position, float radius, Vector3 right, Vector3 up)
        {
            for (int i = 0; i < RingSegments; i++)
                ringPositions[i] = position + (right * ringDirections[i].x + up * ringDirections[i].z) * radius;
            line.SetPositions(ringPositions);
        }

        private Vector3 DamageLabelPosition(Vector3 point, float progress)
        {
            return point - camera.transform.forward * 2.2f +
                camera.transform.up * (0.64f + progress * 0.6f);
        }

        private static AudioClip CreateClip(string name, float length, Func<float, float, float> wave)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(length * rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                samples[i] = wave(time, time / length) * Mathf.Clamp01(i / 30f) * 0.62f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Tone(float cycles) => Mathf.Sin(cycles * 2f * Mathf.PI);
        private static float Noise(float time) => Mathf.Repeat(Mathf.Sin(time * 145782.45f) * 734.16f, 1f) * 2f - 1f;

        private static float Stinger(float time, float first, float second, float third)
        {
            const float beat = 0.14f;
            int note = Mathf.Min((int)(time / beat), 2);
            float phase = (time - note * beat) / beat;
            float frequency = note == 0 ? first : note == 1 ? second : third;
            return Tone(time * frequency) * Mathf.Sin(phase * Mathf.PI) * (1f - phase);
        }
    }
}
