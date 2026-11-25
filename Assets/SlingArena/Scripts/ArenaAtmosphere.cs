using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Weapon = SlingArena.ArenaArt.WeaponStyle;

namespace SlingArena
{
    internal sealed class ArenaAtmosphere : IDisposable
    {
        private enum Shape { Wisp, Glow, Star }

        private struct Particle
        {
            public Vector3 Origin;
            public Vector3 Velocity;
            public Vector2 Size;
            public Color Color;
            public float Lifetime;
            public float Age;
            public float Roll;
            public Shape Shape;
        }

        private static readonly Vector2[] Corners = { new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f) };
        private static readonly Color DaylightHaze = new Color32(244, 233, 182, 255);
        private static readonly Color Ember = new Color32(255, 222, 134, 255);
        private static readonly Color Dust = new Color32(255, 235, 185, 255);

        private readonly Camera camera;
        private readonly Transform space;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Particle> bursts = new(48);
        private readonly List<Vector3> vertices = new(384);
        private readonly List<Vector2> uvs = new(384);
        private readonly List<Vector2> styles = new(384);
        private readonly List<Color32> colors = new(384);
        private readonly List<int> triangles = new(576);
        private float clock;

        public ArenaAtmosphere(Transform parent, Camera camera)
        {
            this.camera = camera;
            var surface = new GameObject("Marsh mist and glints", typeof(MeshFilter), typeof(MeshRenderer));
            space = surface.transform;
            space.SetParent(parent, false);
            mesh = new Mesh { name = "Marsh billboards" };
            mesh.MarkDynamic();
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("SlingArenaAtmosphere");
            material = new Material(shader) { name = "Marsh mist and glints" };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        public void ShowLaunch(Vector3 origin, Vector3 direction, float power, Color team)
        {
            Vector3 sideways = Vector3.Cross(Vector3.up, direction);
            Vector3 floor = new Vector3(origin.x, .23f, origin.z);
            for (int i = 0; i < 8; i++)
            {
                float spread = (i - 3.5f) / 3.5f;
                Vector3 start = floor - direction * (.34f + .10f * (i % 3)) + sideways * spread * .45f;
                Vector3 velocity = -direction * (1.0f + power * 1.6f) + sideways * spread * 1.25f + Vector3.up * .73f;
                AddBurst(start, velocity, new Vector2(.30f, .175f), Color.Lerp(Dust, team, .10f),
                    .45f + .06f * (i % 3), spread, Shape.Wisp);
            }
            Vector3 wake = floor - direction * .36f - camera.transform.forward * .12f;
            AddBurst(wake, -direction * .65f, new Vector2(.44f, .30f), Color.Lerp(team, Color.white, .68f),
                .21f, .6f, Shape.Star);
        }

        public void ShowWeaponDraw(Vector3 position, Color tint)
        {
            Transform view = camera.transform;
            Vector3 center = position + view.up * .55f + view.right * .35f - view.forward * 1.05f;
            AddBurst(center, view.up * .45f, new Vector2(.28f, .28f), Color.Lerp(tint, Color.white, .55f),
                .24f, .42f, Shape.Star);
        }

        public void ShowImpact(Vector3 point, bool chain, Weapon weapon, Vector3 impulse)
        {
            Transform view = camera.transform;
            Vector3 center = point - view.forward * .42f;
            bool heavy = !chain && weapon == Weapon.Hammer;
            bool cut = !chain && weapon == Weapon.Axe;
            Color spark = chain || heavy ? Ember : cut ? new Color32(193, 255, 218, 255) : new Color32(255, 235, 206, 255);
            AddBurst(center, Vector3.up * .17f, new Vector2(.53f, .53f), spark,
                chain ? .23f : .17f, .70f, Shape.Star);
            for (int i = 0; i < (chain ? 7 : 4); i++)
            {
                float angle = .62f + i * 2.4f;
                Vector3 direction = view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle);
                Vector3 drift = cut || heavy ? impulse * 1.65f : Vector3.zero;
                AddBurst(center, direction * (2.1f + .44f * (i % 3)) + drift, new Vector2(.13f, .13f), spark,
                    .29f + .03f * (i % 4), angle, Shape.Star);
            }

            if (!heavy)
                return;

            Vector3 floor = new Vector3(point.x, .25f, point.z);
            Vector3 side = Vector3.Cross(Vector3.up, impulse);
            for (int i = 0; i < 9; i++)
            {
                float offset = (i - 4f) * .20f;
                Vector3 velocity = impulse * (1.7f + .32f * (i % 3)) + side * (offset * 2.5f) + Vector3.up * .76f;
                AddBurst(floor + side * offset, velocity, new Vector2(.36f, .20f), Dust,
                    .50f + .05f * (i % 3), offset, Shape.Wisp);
            }
        }

        private void AddBurst(Vector3 origin, Vector3 velocity, Vector2 size, Color color,
            float lifetime, float roll, Shape shape)
        {
            bursts.Add(new Particle
            {
                Origin = origin, Velocity = velocity, Size = size, Color = color,
                Lifetime = lifetime, Roll = roll, Shape = shape
            });
        }

        public void Tick(float deltaTime)
        {
            clock += deltaTime;
            vertices.Clear();
            uvs.Clear();
            styles.Clear();
            colors.Clear();
            triangles.Clear();
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;

            for (int i = 0; i < 10; i++)
            {
                float phase = i * 1.83f;
                float side = i % 2 == 0 ? 1f : -1f;
                float x = side * (3.82f + Mathf.Sin(phase + clock * .17f) * .34f);
                float z = -5.7f + (i / 2) * 2.66f + Mathf.Sin(phase + clock * .24f) * .64f;
                float lift = .19f + .11f * Mathf.Sin(phase * 2f + clock * .31f);
                Color tint = DaylightHaze;
                tint.a = .12f + .06f * Mathf.Sin(phase + clock * .47f);
                AddQuad(new Vector3(x, lift, z), right, up, new Vector2(.86f, .32f), tint, Shape.Wisp, phase, -.23f * side);
            }

            for (int i = 0; i < 24; i++)
            {
                float phase = i * 1.77f;
                float side = i % 2 == 0 ? 1f : -1f;
                float x = side * (3.61f + .42f * Mathf.Sin(phase + clock * .37f));
                float z = -5.85f + (i / 2) * 1.06f + .46f * Mathf.Sin(phase + clock * .32f);
                float y = .53f + (i % 5) * .18f + .19f * Mathf.Sin(phase * 3f + clock * .63f);
                Color tint = i % 4 == 0 ? DaylightHaze : Ember;
                tint.a = .19f + .48f * Mathf.Pow(Mathf.Sin(phase + clock * .80f), 2f);
                float size = .036f + .018f * (i % 4);
                AddQuad(new Vector3(x, y, z), right, up, Vector2.one * size, tint, Shape.Glow, phase, 0f);
            }

            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Particle particle = bursts[i];
                particle.Age += deltaTime;
                float t = particle.Age / particle.Lifetime;
                if (t >= 1f)
                {
                    bursts.RemoveAt(i);
                    continue;
                }
                bursts[i] = particle;
                bool dust = particle.Shape == Shape.Wisp;
                Color tint = particle.Color;
                tint.a = dust ? (1f - t) * .81f : 1f - t;
                Vector2 size = particle.Size * (dust ? Mathf.Lerp(.8f, 2.8f, t) : Mathf.Lerp(1.15f, .58f, t));
                Vector3 center = particle.Origin + particle.Velocity * particle.Age * (1f - t * .34f);
                AddQuad(center, right, up, size, tint, particle.Shape, i * .63f, particle.Roll);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, styles);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
        }

        private void AddQuad(Vector3 center, Vector3 right, Vector3 up, Vector2 size, Color color,
            Shape shape, float seed, float roll)
        {
            Vector3 horizontal = right * Mathf.Cos(roll) + up * Mathf.Sin(roll);
            Vector3 vertical = up * Mathf.Cos(roll) - right * Mathf.Sin(roll);
            int start = vertices.Count;
            foreach (Vector2 corner in Corners)
            {
                Vector3 point = center + horizontal * (corner.x * size.x) + vertical * (corner.y * size.y);
                vertices.Add(space.InverseTransformPoint(point));
                uvs.Add(corner);
                styles.Add(new Vector2((float)shape, seed));
                colors.Add(color);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
        }

        public void Clear() => bursts.Clear();

        public void Dispose()
        {
            Object.Destroy(mesh);
            Object.Destroy(material);
        }
    }
}
