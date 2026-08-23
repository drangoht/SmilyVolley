using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Génère les planches de sprites des blobs : trois styles de gelée, chacun décliné
    /// sur neuf états de déformation et sur les deux joueurs.
    ///
    /// Chaque planche fait 9 colonnes × 2 lignes. La colonne est l'écrasement, du plus
    /// aplati (gauche) au plus étiré (droite) ; la ligne est le joueur, vert en haut,
    /// orange en bas. Cette disposition tient en un seul fichier par style — c'est ce qui
    /// permet de comparer les trois d'un coup d'œil, dans l'éditeur comme en jeu.
    ///
    /// La silhouette n'est pas un simple redimensionnement : chaque image est redessinée
    /// à partir d'un profil radial propre au style. C'est là que se joue la différence
    /// entre une balle qu'on aplatit et une gelée qui se déforme.
    /// </summary>
    public static class BlobSheetArt
    {
        public const int FrameCount = 9;
        // Le cadre doit contenir l'état le plus débordant, atteint par le style « mou »
        // à l'écrasement maximal : demi-largeur 1,60 unité, hauteur 1,48 à l'étirement.
        // Dimensionné trop juste, il rognait les images des deux extrémités.
        public const int FrameWidth = 320;
        public const int FrameHeight = 160;

        /// <summary>Le blob mesure un rayon d'une unité monde : 88 px la représentent ici.</summary>
        public const float SheetPixelsPerUnit = 88f;

        /// <summary>Marge sous la base, pour que l'anticrénelage du bas ne soit pas coupé.</summary>
        const float BaseMargin = 6f;

        public const float MinSquash = 0.72f;
        public const float MaxSquash = 1.28f;

        static readonly Color MouthColor = new Color(0.15f, 0.10f, 0.12f);

        [MenuItem("Smily Volley/Régénérer les planches de blobs")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(PlaceholderArt.ArtFolder);

            foreach (BlobStyle style in System.Enum.GetValues(typeof(BlobStyle)))
            {
                Generate(style);
            }

            AssetDatabase.Refresh();
            Debug.Log("Smily Volley : trois planches de blobs régénérées dans " + PlaceholderArt.ArtFolder);
        }

        public static string FileName(BlobStyle style) => "blob_sheet_" + style.ToString().ToLowerInvariant() + ".png";
        public static string AssetPath(BlobStyle style) => PlaceholderArt.ArtFolder + "/" + FileName(style);

        /// <summary>Nom du sprite d'une case, tel qu'il apparaît après découpage.</summary>
        public static string SpriteName(BlobStyle style, Side side, int frame)
            => $"{style.ToString().ToLowerInvariant()}_{(side == Side.Left ? "left" : "right")}_{frame}";

        /// <summary>Écrasement représenté par une colonne. La colonne du milieu vaut exactement 1.</summary>
        public static float SquashOf(int frame)
            => Mathf.Lerp(MinSquash, MaxSquash, frame / (float)(FrameCount - 1));

        static void Generate(BlobStyle style)
        {
            int width = FrameWidth * FrameCount;
            int height = FrameHeight * 2;
            var pixels = new Color[width * height];

            for (int row = 0; row < 2; row++)
            {
                Color body = row == 0 ? PlaceholderArt.LeftBody : PlaceholderArt.RightBody;
                // Ligne 0 en haut : les textures se remplissent depuis le bas, on inverse.
                int originY = (1 - row) * FrameHeight;

                for (int frame = 0; frame < FrameCount; frame++)
                {
                    DrawFrame(pixels, width, frame * FrameWidth, originY, SquashOf(frame), style, body);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            string path = AssetPath(style);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSheet(path, style);
        }

        // ------------------------------------------------------------------ dessin

        static void DrawFrame(Color[] pixels, int sheetWidth, int originX, int originY,
            float squash, BlobStyle style, Color body)
        {
            Color outline = body * 0.55f;
            outline.a = 1f;

            // Anticrénelage exprimé en unités : une largeur de pixel et demie.
            float aa = 1.5f / SheetPixelsPerUnit;

            for (int y = 0; y < FrameHeight; y++)
            {
                for (int x = 0; x < FrameWidth; x++)
                {
                    int target = (originY + y) * sheetWidth + originX + x;

                    // Position dans le cadre, en unités, base du blob à l'origine.
                    float fx = (x + 0.5f - FrameWidth * 0.5f) / SheetPixelsPerUnit;
                    float fy = (y + 0.5f - BaseMargin) / SheetPixelsPerUnit;

                    if (fy < 0f) { pixels[target] = Color.clear; continue; }

                    // Retour à l'espace du dôme unité : c'est là que vivent le profil du
                    // style et le visage, qui se déforment donc avec le corps.
                    float ux = fx * squash;
                    float uy = fy / squash;

                    var point = new Vector2(ux, uy);
                    float radius = point.magnitude;
                    float angle = Mathf.Atan2(Mathf.Max(uy, 0f), ux);

                    float boundary = BoundaryRadius(angle, squash, style);
                    float alpha = Mathf.Clamp01((boundary - radius) / aa);

                    if (alpha <= 0f) { pixels[target] = Color.clear; continue; }

                    Color c = Shade(point, radius / Mathf.Max(boundary, 0.001f), angle, style, body, outline);
                    c = DrawFace(c, point, aa);

                    pixels[target] = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        /// <summary>
        /// Rayon du contour dans l'espace du dôme unité, pour un angle donné.
        /// C'est cette fonction, et elle seule, qui distingue les trois styles.
        /// </summary>
        static float BoundaryRadius(float angle, float squash, BlobStyle style)
        {
            switch (style)
            {
                case BlobStyle.Soft:
                {
                    // Le volume fuit sur les côtés quand on écrase, se creuse quand on étire.
                    // cos(2θ) vaut +1 aux flancs et −1 au sommet : un seul terme suffit à
                    // décrire les deux effets, avec le signe de (1 − écrasement).
                    float bulge = (1f - squash) * 0.55f;
                    // Un léger repli sous les flancs donne la goutte qui retombe.
                    float sag = (1f - squash) * 0.12f * Mathf.Sin(4f * angle);
                    return 1f + bulge * Mathf.Cos(2f * angle) + sag;
                }

                case BlobStyle.Angular:
                {
                    // Profil d'un polygone régulier : le rayon suit la face la plus proche.
                    const int facets = 10;
                    float sector = 2f * Mathf.PI / facets;
                    float local = Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f;
                    float polygon = Mathf.Cos(sector * 0.5f) / Mathf.Cos(local);
                    // Les faces s'arrondissent un peu sous la contrainte : une gelée moulée
                    // reste ferme, mais pas au point d'être un cristal.
                    float softening = Mathf.Abs(1f - squash) * 0.35f;
                    return Mathf.Lerp(polygon, 1f, softening);
                }

                default:
                    return 1f;
            }
        }

        static Color Shade(Vector2 point, float normalizedRadius, float angle,
            BlobStyle style, Color body, Color outline)
        {
            // Éclairage général : plus clair vers le haut, comme sous un soleil de plage.
            Color c = Color.Lerp(body, Color.white, Mathf.Clamp01(point.y - 0.45f) * 0.35f);

            switch (style)
            {
                case BlobStyle.Soft:
                {
                    // Aspect mouillé : un reflet net en haut à gauche et un fond translucide.
                    float gloss = Mathf.Clamp01(0.42f - Vector2.Distance(point, new Vector2(-0.34f, 0.62f)));
                    c = Color.Lerp(c, Color.white, gloss * 1.9f);
                    c = Color.Lerp(c, Color.Lerp(body, Color.white, 0.25f), Mathf.Clamp01(0.35f - point.y) * 0.5f);
                    break;
                }

                case BlobStyle.Angular:
                {
                    // Une valeur par facette : c'est ce qui fait lire les faces planes.
                    const int facets = 10;
                    int index = Mathf.FloorToInt(angle / (2f * Mathf.PI / facets));
                    float step = (index % 3) * 0.06f - 0.04f;
                    c = Color.Lerp(c, step > 0f ? Color.white : Color.black, Mathf.Abs(step) * 1.6f);
                    break;
                }
            }

            // Assombrissement du bord, commun aux trois styles.
            float edge = Mathf.Clamp01((normalizedRadius - 0.90f) / 0.07f);
            return Color.Lerp(c, outline, edge * 0.9f);
        }

        /// <summary>Yeux et sourire, dessinés dans l'espace unité pour suivre la déformation.</summary>
        static Color DrawFace(Color c, Vector2 point, float aa)
        {
            c = Disc(c, point, new Vector2(-0.30f, 0.64f), 0.130f, Color.white, aa);
            c = Disc(c, point, new Vector2(0.30f, 0.64f), 0.130f, Color.white, aa);
            c = Disc(c, point, new Vector2(-0.27f, 0.61f), 0.060f, Color.black, aa);
            c = Disc(c, point, new Vector2(0.33f, 0.61f), 0.060f, Color.black, aa);

            if (point.y < 0.46f)
            {
                float ring = Mathf.Abs(Vector2.Distance(point, new Vector2(0f, 0.50f)) - 0.26f);
                float mouth = 1f - Mathf.Clamp01((ring - 0.028f) / aa);
                c = Color.Lerp(c, MouthColor, mouth);
            }

            return c;
        }

        static Color Disc(Color current, Vector2 point, Vector2 center, float radius, Color color, float aa)
        {
            float t = Mathf.Clamp01((radius - Vector2.Distance(point, center)) / aa);
            return Color.Lerp(current, color, t);
        }

        // ------------------------------------------------------------------ découpage

        /// <summary>
        /// Configure l'import et découpe la planche en dix-huit sprites.
        ///
        /// Le découpage passe par <see cref="ISpriteEditorDataProvider"/> et non par
        /// <c>TextureImporter.spritesheet</c>, qui est l'ancienne voie.
        /// </summary>
        static void ConfigureSheet(string path, BlobStyle style)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError("Import de planche introuvable : " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = SheetPixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();

            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>(FrameCount * 2);
            for (int row = 0; row < 2; row++)
            {
                Side side = row == 0 ? Side.Left : Side.Right;
                int originY = (1 - row) * FrameHeight;

                for (int frame = 0; frame < FrameCount; frame++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = SpriteName(style, side, frame),
                        rect = new Rect(frame * FrameWidth, originY, FrameWidth, FrameHeight),
                        alignment = SpriteAlignment.Custom,
                        // Le pivot est la base du blob : elle reste posée sur le sable
                        // quelle que soit la déformation, sans quoi le blob sauterait
                        // d'une image à l'autre.
                        pivot = new Vector2(0.5f, BaseMargin / FrameHeight),
                        spriteID = GUID.Generate(),
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
