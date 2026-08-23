using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Génère les sprites provisoires du jeu (blobs, balle, ombre, filet, ciel).
    /// Tout est dessiné par code : aucun asset externe à installer, et il suffit de remplacer
    /// les PNG produits dans Assets/Art pour passer à de vrais graphismes.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string ArtFolder = "Assets/Art";
        public const float PixelsPerUnit = 200f;

        static readonly Color LeftBody = new Color(0.24f, 0.72f, 0.42f);
        static readonly Color RightBody = new Color(0.93f, 0.44f, 0.31f);
        static readonly Color BallA = new Color(0.98f, 0.85f, 0.30f);
        static readonly Color BallB = new Color(0.99f, 0.99f, 0.97f);

        [MenuItem("Smily Volley/Régénérer les sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(ArtFolder);

            Save(CreateBlob(LeftBody), "blob_left.png", new Vector2(0.5f, 0f), PixelsPerUnit);
            Save(CreateBlob(RightBody), "blob_right.png", new Vector2(0.5f, 0f), PixelsPerUnit);
            Save(CreateBall(), "ball.png", new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Save(CreateShadow(), "shadow.png", new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Save(CreateNet(), "net.png", new Vector2(0.5f, 0f), PixelsPerUnit);
            Save(CreateSky(), "sky.png", new Vector2(0.5f, 0.5f), 32f);
            Save(CreateSquare(), "square.png", new Vector2(0.5f, 0.5f), 8f);

            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ dessin

        /// <summary>
        /// Dôme smiley. L'espace normalisé va de -1 à 1 en X et de 0 (sol) à 1 (sommet) en Y ;
        /// la texture étant deux fois plus large que haute, un pixel a la même taille sur les deux axes.
        /// </summary>
        static Texture2D CreateBlob(Color body)
        {
            const int w = 400;
            const int h = 200;
            const float aa = 2f / w * 1.5f;

            Color outline = body * 0.55f;
            outline.a = 1f;
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h;
                    var point = new Vector2(nx, ny);
                    float d = point.magnitude;

                    float alpha = Mathf.Clamp01((1f - d) / aa);
                    if (alpha <= 0f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    Color c = Color.Lerp(body, Color.white, Mathf.Clamp01(ny - 0.45f) * 0.35f);
                    c = Color.Lerp(c, outline, Mathf.Clamp01((d - 0.90f) / 0.07f) * 0.9f);

                    // Yeux : blanc de l'oeil puis pupille légèrement décalée.
                    c = Disc(c, point, new Vector2(-0.30f, 0.64f), 0.130f, Color.white, aa);
                    c = Disc(c, point, new Vector2(0.30f, 0.64f), 0.130f, Color.white, aa);
                    c = Disc(c, point, new Vector2(-0.27f, 0.61f), 0.060f, Color.black, aa);
                    c = Disc(c, point, new Vector2(0.33f, 0.61f), 0.060f, Color.black, aa);

                    // Sourire : portion basse d'un anneau, arrêtée sous les yeux.
                    if (ny < 0.46f)
                    {
                        float ring = Mathf.Abs(Vector2.Distance(point, new Vector2(0f, 0.50f)) - 0.26f);
                        float mouth = 1f - Mathf.Clamp01((ring - 0.028f) / aa);
                        c = Color.Lerp(c, new Color(0.15f, 0.10f, 0.12f), mouth);
                    }

                    pixels[y * w + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }

            return Build(w, h, pixels);
        }

        static Texture2D CreateBall()
        {
            const int size = 140;
            const float aa = 2f / size * 1.5f;
            var pixels = new Color[size * size];
            Color outline = new Color(0.35f, 0.28f, 0.10f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    var point = new Vector2(nx, ny);
                    float d = point.magnitude;

                    float alpha = Mathf.Clamp01((1f - d) / aa);
                    if (alpha <= 0f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    // Six quartiers alternés : la rotation de la balle devient lisible.
                    float angle = Mathf.Atan2(ny, nx) / (2f * Mathf.PI) + 0.5f;
                    int sector = Mathf.FloorToInt(angle * 6f) % 2;
                    Color c = sector == 0 ? BallA : BallB;

                    float highlight = Mathf.Clamp01(0.55f - Vector2.Distance(point, new Vector2(-0.32f, 0.32f)));
                    c = Color.Lerp(c, Color.white, highlight * 0.7f);
                    c = Color.Lerp(c, outline, Mathf.Clamp01((d - 0.88f) / 0.09f));

                    pixels[y * size + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }

            return Build(size, size, pixels);
        }

        static Texture2D CreateShadow()
        {
            const int w = 200;
            const int h = 60;
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * 0.35f;
                    pixels[y * w + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            return Build(w, h, pixels);
        }

        static Texture2D CreateNet()
        {
            const int w = 80;
            const int h = 640;
            var pixels = new Color[w * h];

            Color post = new Color(0.95f, 0.95f, 0.93f);
            Color mesh = new Color(0.80f, 0.80f, 0.78f);
            Color edge = new Color(0.52f, 0.52f, 0.51f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool band = y > h - 46;                          // bandeau plein en haut du filet
                    bool meshLine = (y % 26 < 3) || (x % 26 < 3);    // mailles
                    Color c = band ? post : (meshLine ? mesh : mesh * 0.90f);
                    if (x < 3 || x > w - 4) c = edge;
                    if (band && (y == h - 46 || y == h - 1)) c = edge;
                    pixels[y * w + x] = c;
                }
            }

            return Build(w, h, pixels);
        }

        static Texture2D CreateSky()
        {
            const int w = 8;
            const int h = 256;
            var pixels = new Color[w * h];
            Color top = new Color(0.24f, 0.55f, 0.85f);
            Color bottom = new Color(0.70f, 0.87f, 0.96f);

            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, Mathf.Pow((float)y / (h - 1), 0.85f));
                for (int x = 0; x < w; x++) pixels[y * w + x] = c;
            }

            return Build(w, h, pixels);
        }

        static Texture2D CreateSquare()
        {
            const int size = 8;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            return Build(size, size, pixels);
        }

        // ------------------------------------------------------------------ outils

        /// <summary>Fond un disque plein sur la couleur courante, avec anticrénelage.</summary>
        static Color Disc(Color current, Vector2 point, Vector2 center, float radius, Color color, float aa)
        {
            float t = Mathf.Clamp01((radius - Vector2.Distance(point, center)) / aa);
            return Color.Lerp(current, color, t);
        }

        static Texture2D Build(int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static void Save(Texture2D texture, string fileName, Vector2 pivot, float pixelsPerUnit)
        {
            string path = ArtFolder + "/" + fileName;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
