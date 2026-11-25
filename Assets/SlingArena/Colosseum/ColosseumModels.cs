using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Characters that are sculpted in Blender rather than assembled from primitives in code. The Blender script
    /// writes their meshes into one resource file (positions, normals, colour with gloss in alpha, and a role byte
    /// saying which vertices take a team colour); this class turns it into meshes for either team.
    /// </summary>
    public static class ColosseumModels
    {
        sealed class Raw
        {
            public Vector3[] Positions, Normals;
            public Color32[] Colors;
            public byte[] Roles;
            public int[] Indices;
        }

        static Dictionary<string, Raw> raws;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>Mesh of that name painted for the team, or null when the resource does not have it.</summary>
        public static Mesh Get(string name, int team)
        {
            string key = name + "#" + team;
            if (meshes.TryGetValue(key, out Mesh ready) && ready != null)
                return ready;
            if (raws == null) Read();
            if (!raws.TryGetValue(name, out Raw raw))
                return null;

            Color32[] tones = { default, MeshKit.Rgb(Kit.TeamMain[team], 1f), MeshKit.Rgb(Kit.TeamLight[team], 1f), MeshKit.Rgb(Kit.TeamDark[team], 1f) };
            var colors = new Color32[raw.Colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 c = raw.Colors[i];
                int role = raw.Roles[i];
                if (role > 0 && role < tones.Length)
                {
                    // Team-coloured cloth: the stored colour only carries the shading.
                    Color32 tone = tones[role];
                    c = new Color32((byte)(tone.r * c.r / 255), (byte)(tone.g * c.g / 255), (byte)(tone.b * c.b / 255), c.a);
                }
                colors[i] = c;
            }
            var mesh = new Mesh { name = "Colosseum " + name, indexFormat = raw.Positions.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(raw.Positions);
            mesh.SetNormals(raw.Normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(raw.Indices, 0);
            mesh.SetUVs(3, MeshKit.OutlineNormals(raw.Positions, raw.Normals));
            mesh.RecalculateBounds();
            meshes[key] = Kit.Own(mesh);
            return mesh;
        }

        static void Read()
        {
            raws = new Dictionary<string, Raw>();
            var asset = Resources.Load<TextAsset>("ColosseumOrc");
            if (asset == null) return;
            using (var reader = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                if (new string(reader.ReadChars(4)) != "CMDL") return;
                int parts = reader.ReadInt32();
                for (int p = 0; p < parts; p++)
                {
                    string name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                    int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                    var raw = new Raw { Positions = new Vector3[vertices], Normals = new Vector3[vertices], Colors = new Color32[vertices], Indices = new int[indices] };
                    for (int i = 0; i < vertices; i++) raw.Positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    for (int i = 0; i < vertices; i++) raw.Normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    for (int i = 0; i < vertices; i++) raw.Colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    raw.Roles = reader.ReadBytes(vertices);
                    for (int i = 0; i < indices; i++) raw.Indices[i] = reader.ReadInt32();
                    raws[name] = raw;
                }
            }
        }
    }
}
