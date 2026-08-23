using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Peau des blobs : une texture par joueur, une tuile par style de gelée.
    ///
    /// La silhouette n'est plus dessinée image par image — c'est <see cref="BlobJelly"/> qui la
    /// simule et la rend en maillage. La texture ne porte plus que ce qui ne bouge pas avec la
    /// forme : la couleur, l'ombrage, le visage. Elle est dessinée dans l'espace du corps au
    /// repos ; le maillage figeant ses UV sur cette même forme, tout se déforme ensuite avec le
    /// corps, y compris le sourire.
    ///
    /// Le bord n'a pas d'anticrénelage à lui : l'alpha s'éteint en douceur autour du contour au
    /// repos, et la jupe du maillage — un anneau de triangles débordant du contour — laisse la
    /// place à cette extinction. Le bord reste donc lisse quelle que soit la déformation.
    /// </summary>
    public static class BlobArt
    {
        public const string LeftFile = "blob_jelly_left.png";
        public const string RightFile = "blob_jelly_right.png";
        public const string LeftMaterial = "BlobLeft.mat";
        public const string RightMaterial = "BlobRight.mat";

        /// <summary>Lumière du dessin, venue du haut à gauche comme sur toute la scène.</summary>
        static readonly Vector2 LightDirection = new Vector2(-0.42f, 0.91f).normalized;

        static readonly Color MouthColor = new Color(0.15f, 0.10f, 0.12f);

        [MenuItem("Smily Volley/Régénérer la peau des blobs")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(PlaceholderArt.ArtFolder);

            Generate(Side.Left);
            Generate(Side.Right);

            AssetDatabase.Refresh();
            Debug.Log("Smily Volley : peau des blobs régénérée dans " + PlaceholderArt.ArtFolder);
        }

        public static string FileName(Side side) => side == Side.Left ? LeftFile : RightFile;
        public static string TexturePath(Side side) => PlaceholderArt.ArtFolder + "/" + FileName(side);
        public static string MaterialPath(Side side)
            => PlaceholderArt.ArtFolder + "/" + (side == Side.Left ? LeftMaterial : RightMaterial);

        // ------------------------------------------------------------------ dessin

        static void Generate(Side side)
        {
            int tiles = BlobJelly.StyleCount;
            int width = BlobJelly.TileWidth * tiles;
            int height = BlobJelly.TileHeight;

            Color body = side == Side.Left ? PlaceholderArt.LeftBody : PlaceholderArt.RightBody;
            var pixels = new Color[width * height];

            foreach (BlobStyle style in System.Enum.GetValues(typeof(BlobStyle)))
            {
                DrawTile(pixels, width, (int)style * BlobJelly.TileWidth, style, body);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            string path = TexturePath(side);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImport(path);
            BuildMaterial(side);
        }

        static void DrawTile(Color[] pixels, int sheetWidth, int originX, BlobStyle style, Color body)
        {
            int facets = BlobJelly.FacetsOf(style);
            float ppu = BlobJelly.TilePixelsPerUnit;

            // Trois pixels et demi de fondu : la jupe du maillage déborde de 0,08 unité,
            // soit dix pixels, largement de quoi contenir cette extinction.
            float aa = 3.5f / ppu;

            Color outline = body * 0.5f;
            outline.a = 1f;

            for (int y = 0; y < BlobJelly.TileHeight; y++)
            {
                for (int x = 0; x < BlobJelly.TileWidth; x++)
                {
                    int target = y * sheetWidth + originX + x;

                    float ux = (x + 0.5f - BlobJelly.TileWidth * 0.5f) / ppu;
                    float uy = (y + 0.5f - BlobJelly.TileBaseRow) / ppu;

                    var point = new Vector2(ux, uy);
                    float radius = point.magnitude;
                    float angle = Mathf.Atan2(uy, ux);
                    float boundary = BlobJelly.RestRadius(angle, facets);

                    // Profondeur sous la peau : le contour d'un côté, la ligne de base de
                    // l'autre. Une seule grandeur décrit ainsi tout le pourtour.
                    float depth = Mathf.Min(boundary - radius, uy);

                    float alpha = Mathf.Clamp01(0.5f + depth / aa);
                    if (alpha <= 0f) { pixels[target] = Color.clear; continue; }

                    Color c = Shade(point, angle, facets, style, body);

                    // Assombrissement du pourtour, à distance constante du bord : la même
                    // épaisseur de trait sur le dôme et sur la base.
                    float edge = 1f - Mathf.Clamp01(depth / 0.085f);
                    c = Color.Lerp(c, outline, edge * edge * 0.85f);

                    c = DrawFace(c, point, aa);

                    pixels[target] = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        /// <summary>
        /// Couleur du corps sous la lumière. La normale est celle du contour au repos : lisse
        /// sur un arc rond, constante par face sur un contour à facettes — c'est cette rupture
        /// de valeur d'une face à l'autre qui fait lire la gelée moulée.
        /// </summary>
        static Color Shade(Vector2 point, float angle, int facets, BlobStyle style, Color body)
        {
            Vector2 normal = SurfaceNormal(angle, facets);
            float lit = Vector2.Dot(normal, LightDirection);

            Color c = Color.Lerp(body, Color.white, Mathf.Clamp01(lit) * 0.42f);
            c = Color.Lerp(c, body * 0.62f, Mathf.Clamp01(-lit) * 0.55f);

            if (style != BlobStyle.Soft) return c;

            // Aspect mouillé : un reflet net en haut à gauche, et un fond qui s'éclaircit
            // comme une gelée traversée par la lumière.
            float gloss = Mathf.Clamp01(0.34f - Vector2.Distance(point, new Vector2(-0.36f, 0.60f)));
            c = Color.Lerp(c, Color.white, Mathf.Clamp01(gloss * 5f));
            c = Color.Lerp(c, Color.Lerp(body, Color.white, 0.30f), Mathf.Clamp01(0.32f - point.y) * 0.55f);

            return c;
        }

        /// <summary>
        /// Normale du contour au repos. Sur un polygone, tous les points d'une face partagent
        /// la normale de cette face : la valeur est plate, l'arête tranche.
        /// </summary>
        static Vector2 SurfaceNormal(float angle, int facets)
        {
            if (facets < 3) return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float sector = 2f * Mathf.PI / facets;
            float center = (Mathf.Floor(Mathf.Repeat(angle, 2f * Mathf.PI) / sector) + 0.5f) * sector;
            return new Vector2(Mathf.Cos(center), Mathf.Sin(center));
        }

        /// <summary>Yeux et sourire, posés dans l'espace du corps au repos pour suivre la gelée.</summary>
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

        // ------------------------------------------------------------------ import et matériau

        /// <summary>
        /// La peau alimente un <c>MeshRenderer</c>, pas un <c>SpriteRenderer</c> : elle est
        /// importée en texture ordinaire. Découpée en sprites, elle n'aurait servi à rien —
        /// c'est le maillage qui choisit la tuile, par ses UV.
        /// </summary>
        static void ConfigureImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError("Import de peau introuvable : " + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Matériau non éclairé et transparent du maillage.
        ///
        /// Le shader démarre opaque : le passer en transparence demande de régler à la main la
        /// surface, le mélange, le ZWrite, le mot-clé et la file de rendu, ce que l'inspecteur
        /// ferait sinon pour nous. Les faces ne sont pas triées non plus : le maillage se
        /// retourne localement quand la gelée se creuse, et une face arrière disparaîtrait.
        /// </summary>
        static Material BuildMaterial(Side side)
        {
            string path = MaterialPath(side);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("Shader URP non éclairé introuvable.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(side));
            if (texture == null) Debug.LogError("Peau de blob introuvable : " + TexturePath(side));

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);

            material.SetFloat("_Surface", 1f);   // 1 = Transparent
            material.SetFloat("_Blend", 0f);     // 0 = Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_AlphaClip", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return material;
        }
    }
}
