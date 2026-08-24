using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Génère les sprites provisoires du jeu (balle, ombre, filet, ciel).
    /// Tout est dessiné par code : aucun asset externe à installer, et il suffit de remplacer
    /// les PNG produits dans Assets/Art pour passer à de vrais graphismes.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string ArtFolder = "Assets/Art";
        public const float PixelsPerUnit = 200f;

        // Publiques : la peau des blobs (BlobArt) reprend exactement ces teintes.
        public static readonly Color LeftBody = new Color(0.24f, 0.72f, 0.42f);
        public static readonly Color RightBody = new Color(0.93f, 0.44f, 0.31f);
        static readonly Color BallA = new Color(0.98f, 0.85f, 0.30f);
        static readonly Color BallB = new Color(0.99f, 0.99f, 0.97f);

        // Rayons des coins arrondis du menu, en pixels d'interface — les sprites sont
        // importés à 100 pixels par unité, la référence du canvas, donc un pixel de
        // texture vaut un pixel d'interface.
        const int PanelRadius = 36;
        const int RowRadius = 14;

        [MenuItem("Smily Volley/Régénérer les sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(ArtFolder);

            Save(CreateBall(), "ball.png", new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Save(CreateShadow(), "shadow.png", new Vector2(0.5f, 0.5f), PixelsPerUnit);
            Save(CreateNet(), "net.png", new Vector2(0.5f, 0f), PixelsPerUnit);
            Save(CreateSky(), "sky.png", new Vector2(0.5f, 0.5f), 32f);
            Save(CreateSquare(), "square.png", new Vector2(0.5f, 0.5f), 8f);
            Save(CreateSpark(), "spark.png", new Vector2(0.5f, 0.5f), PixelsPerUnit);

            // Flèche de défilement du menu. Dessinée plutôt qu'écrite : la police du jeu ne
            // contient aucun glyphe de flèche, et le navigateur n'a pas de police système pour
            // l'y suppléer — le triangle « ▲ » disparaissait donc, sans rien laisser, dans la
            // version web.
            Save(CreateTriangle(), "triangle.png", new Vector2(0.5f, 0.5f), 100f);

            // Panneaux du menu, découpés en neuf tranches : la bordure vaut le rayon, si
            // bien qu'un panneau s'étire à n'importe quelle taille sans déformer ses coins.
            Save(CreateRounded(PanelRadius), "panel.png", new Vector2(0.5f, 0.5f), 100f,
                PanelRadius);
            Save(CreateRounded(RowRadius), "rounded.png", new Vector2(0.5f, 0.5f), 100f,
                RowRadius);

            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ dessin

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

        /// <summary>
        /// Grain de particule : un disque blanc à bord dégradé, teinté ensuite par le
        /// système de particules. Le cœur reste plein sur la moitié du rayon, sinon une
        /// bouffée de dix grains n'est qu'un voile laiteux au lieu d'un éclat lisible.
        /// </summary>
        static Texture2D CreateSpark()
        {
            const int size = 64;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);

                    float alpha = Mathf.Clamp01((1f - d) / 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
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

        /// <summary>
        /// Rectangle à coins arrondis, blanc : l'<c>Image</c> qui le porte le teinte ensuite.
        /// Le carré fait quatre fois le rayon, ce qui laisse un pixel plein au centre de
        /// chaque bord — la tranche du milieu que le découpage en neuf va étirer.
        /// </summary>
        static Texture2D CreateRounded(int radius)
        {
            int size = radius * 4;
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float inner = half - radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance signée à un rectangle arrondi : positive dehors, négative
                    // dedans. Le demi-pixel de décalage centre l'anticrénelage sur le bord.
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            }

            return Build(size, size, pixels);
        }

        /// <summary>Triangle plein pointant vers le haut, anticrénelé sur ses trois bords.</summary>
        static Texture2D CreateTriangle()
        {
            const int size = 32;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Coordonnées centrées, y vers le haut, dans [-1, 1].
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;

                    // Distance signée aux trois côtés : le triangle est l'intersection du bas
                    // et des deux obliques. Le facteur 0,5 remet la pente à l'échelle du pixel.
                    float bottom = -1f - v;
                    float right = (u + v) * 0.70710678f;
                    float left = (-u + v) * 0.70710678f;
                    float d = Mathf.Max(bottom, Mathf.Max(right, left));

                    float alpha = Mathf.Clamp01(0.5f - d * size * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return Build(size, size, pixels);
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

        /// <param name="border">
        /// Largeur des tranches de bord, en pixels. Zéro pour un sprite ordinaire ; sinon
        /// le sprite se découpe en neuf et ses coins gardent leur taille à l'étirement.
        /// </param>
        static void Save(Texture2D texture, string fileName, Vector2 pivot, float pixelsPerUnit,
            int border = 0)
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
            settings.spriteBorder = new Vector4(border, border, border, border);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
