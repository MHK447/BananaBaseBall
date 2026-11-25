using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlingArena.Colosseum.EditorTools
{
    public static class ColosseumFanAtlas
    {
        const string AssetPath = "Assets/SlingArena/Colosseum/Resources/ColosseumUI/lobby-match-fans.png";
        const int Cell = 128, Samples = 3;

        // Read across the top row, then the bottom. Cells 2 and 6 are the two helmeted duelists.
        static readonly int[] Variants = { 0, 1, 3, 4, 10, 7, 9, 11 };

        [MenuItem("SlingArena/Art/Bake lobby spectator atlas")]
        public static void Bake()
        {
            var preview = new PreviewRenderUtility();
            int size = Cell * Samples;
            var target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var atlas = new Texture2D(Cell * 4, Cell * 2, TextureFormat.RGBA32, false);
            var material = new Material(Resources.Load<Shader>("ColosseumToon"));
            var savedTarget = RenderTexture.active;
            try
            {
                var model = new GameObject("Spectator", typeof(MeshFilter), typeof(MeshRenderer));
                preview.AddSingleGO(model);
                model.transform.rotation = Quaternion.Euler(0f, -15f, 0f);
                model.GetComponent<MeshRenderer>().sharedMaterial = material;
                var filter = model.GetComponent<MeshFilter>();
                Camera camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = .8f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 12f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                camera.aspect = 1f;
                camera.transform.rotation = Quaternion.Euler(9f, 180f, 0f);
                camera.transform.position = Vector3.up * .64f - camera.transform.forward * 4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, -147f, 0f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[1].intensity = 0f;

                for (int i = 0; i < Variants.Length; i++)
                {
                    Mesh fan = ColosseumArena.FanMesh(Variants[i], i / 4);
                    try
                    {
                        filter.sharedMesh = fan;
                        preview.Render(allowScriptableRenderPipeline: true, updatefov: false);
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                        atlas.SetPixels32(i % 4 * Cell, (1 - i / 4) * Cell, Cell, Cell,
                            ResolveCoverage(readback.GetPixels32()));
                    }
                    finally
                    {
                        filter.sharedMesh = null;
                        Object.DestroyImmediate(fan);
                    }
                }
                // Fill one entire mobile ASTC block so the UI's solid texel stays opaque on phones.
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        atlas.SetPixel(x, y, Color.white);
                atlas.Apply();
                File.WriteAllBytes(AssetPath, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = savedTarget;
                preview.camera.targetTexture = null;
                preview.Cleanup();
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(atlas);
            }
        }

        // The pipeline can force the render target alpha to one. Resolve the opaque toon silhouettes
        // against a chroma matte, supersampled before writing to keep the exported RGB straight.
        static Color32[] ResolveCoverage(Color32[] source)
        {
            var pixels = new Color32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    int covered = 0, r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < Samples; dy++)
                        for (int dx = 0; dx < Samples; dx++)
                        {
                            var sample = source[(y * Samples + dy) * Cell * Samples + x * Samples + dx];
                            if (sample.r > 210 && sample.b > 210 && sample.g < 35) continue;
                            covered++;
                            r += sample.r;
                            g += sample.g;
                            b += sample.b;
                        }
                    if (covered > 0)
                        pixels[y * Cell + x] = new Color32((byte)(r / covered), (byte)(g / covered),
                            (byte)(b / covered), (byte)(255 * covered / (Samples * Samples)));
                }
            return pixels;
        }
    }
}
