using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EXFILEditor
{
    /// <summary>
    /// Configures imported sprite sheets (the generated 2D art) : grid slicing by cell size,
    /// bottom pivot, point filtering and an optional chroma-key pass that removes the
    /// background colour sampled from the top-left pixel.
    /// </summary>
    public static class SpriteSheetSlicer
    {
        [MenuItem("EXFIL/Art/Slice selected sprite sheets by grid", false, 60)]
        public static void SliceSelected()
        {
            Object[] selection = Selection.objects;
            int count = 0;
            foreach (Object o in selection)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png")) continue;
                if (SliceOne(path, GuessColumns(path), 1, 256f)) count++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EXFIL] Sliced " + count + " sprite sheets.");
        }

        [MenuItem("EXFIL/Art/Remove background (corner colour) on selected", false, 61)]
        public static void RemoveBackgroundSelected()
        {
            Object[] selection = Selection.objects;
            foreach (Object o in selection)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png")) continue;
                RemoveBackground(path, 0.12f);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>Filename conventions: *_Idle8.png = 8 columns, *4x3.png = 4x3 grid.</summary>
        private static int GuessColumns(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("8")) return 8;
            if (name.EndsWith("16")) return 16;
            if (name.Contains("4x3")) return 4;
            return 8;
        }

        private static int GuessRows(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains("4x3")) return 3;
            return 1;
        }

        public static bool SliceOne(string path, int columns, int rows, float pixelsPerUnit)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0f);

            List<SpriteMetaData> slices = new List<SpriteMetaData>();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int width = texture != null ? texture.width : 2048;
            int height = texture != null ? texture.height : 512;

            if (rows <= 1) rows = GuessRows(path);
            int cellWidth = width / Mathf.Max(1, columns);
            int cellHeight = height / Mathf.Max(1, rows);
            string baseName = Path.GetFileNameWithoutExtension(path);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    SpriteMetaData meta = new SpriteMetaData
                    {
                        name = baseName + "_" + y + "_" + x,
                        rect = new Rect(x * cellWidth, (rows - 1 - y) * cellHeight, cellWidth, cellHeight),
                        pivot = new Vector2(0.5f, 0f),
                        alignment = (int)SpriteAlignment.BottomCenter
                    };
                    slices.Add(meta);
                }
            }

            importer.spritesheet = slices.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        public static void RemoveBackground(string path, float tolerance)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null) return;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels();
            Color key = pixels[0];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float diff = Mathf.Abs(c.r - key.r) + Mathf.Abs(c.g - key.g) + Mathf.Abs(c.b - key.b);
                if (diff < tolerance * 3f) pixels[i] = new Color(c.r, c.g, c.b, 0f);
            }

            copy.SetPixels(pixels);
            copy.Apply();
            byte[] bytes = copy.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Debug.Log("[EXFIL] Background removed: " + path);
        }
    }
}
