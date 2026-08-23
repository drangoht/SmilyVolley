using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Construit la scène de jeu complète (terrain, blobs, balle, interface) et l'enregistre
    /// dans Assets/Scenes/Game.unity. Tout est assemblé ici plutôt qu'à la main pour que les
    /// dimensions du terrain restent cohérentes entre le décor, les colliders et les scripts.
    /// La scène produite reste une scène Unity ordinaire, éditable normalement ensuite.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        const string MaterialsFolder = "Assets/Art";

        // ----- géométrie du terrain (unités monde) -----
        const float GroundY = -4f;          // surface du sol
        const float WallX = 8.2f;           // face intérieure des murs latéraux
        const float BlobRadius = 1f;
        const float BallRadius = 0.35f;
        const float NetHalfWidth = 0.20f;
        const float NetHeight = 3.2f;
        const float BlobStartX = 4.3f;
        const float CameraBottomY = -5.1f;
        const float CameraMinSize = 5.1f;
        const float CeilingThickness = 2f;

        // ----- ordres de rendu -----
        const int OrderSky = -100;
        const int OrderGround = -50;
        const int OrderShadow = -10;
        const int OrderBlob = 0;
        const int OrderBall = 5;
        const int OrderNet = 20;
        const int OrderParticles = 30;
        const int OrderBorder = 50;

        const string AudioFolder = "Assets/Audio/Kenney";

        static readonly Color SandColor = new Color(0.93f, 0.82f, 0.58f);
        static readonly Color SandLineColor = new Color(0.78f, 0.66f, 0.44f);
        static readonly Color BorderColor = new Color(0.13f, 0.16f, 0.22f);

        [MenuItem("Smily Volley/Construire la scène de jeu")]
        public static void Build()
        {
            PlaceholderArt.GenerateAll();
            ConfigureAudioImport();

            var bouncyWall = CreateMaterial("Bouncy", 0.92f);
            var softGround = CreateMaterial("Sand", 0.45f);

            Sprite square = LoadSprite("square.png");
            Sprite sky = LoadSprite("sky.png");
            Sprite net = LoadSprite("net.png");
            Sprite shadow = LoadSprite("shadow.png");
            Sprite ballSprite = LoadSprite("ball.png");
            Sprite leftSprite = LoadSprite("blob_left.png");
            Sprite rightSprite = LoadSprite("blob_right.png");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera cam = BuildCamera();
            var environment = new GameObject("Environment").transform;
            BuildGlobalLight(environment);
            BuildBackdrop(environment, sky, square);
            BuildGround(environment, square, softGround);
            BuildWalls(environment, square, bouncyWall);
            BuildCeiling(environment, cam, bouncyWall);
            BuildNet(environment, net, bouncyWall);

            BallController ball = BuildBall(ballSprite, bouncyWall);
            BlobController left = BuildBlob(Side.Left, leftSprite);
            BlobController right = BuildBlob(Side.Right, rightSprite);

            var shadows = new GameObject("Shadows").transform;
            shadows.SetParent(environment, false);
            BuildShadow(shadows, shadow, ball.transform, 0.9f);
            BuildShadow(shadows, shadow, left.transform, 2.1f);
            BuildShadow(shadows, shadow, right.transform, 2.1f);

            AiBlobInput ai = ConfigureAi(right, ball);
            // Mode par défaut : le blob de droite est piloté par l'IA.
            right.GetComponent<HumanBlobInput>().enabled = false;

            HudController hud = BuildHud();
            GameManager manager = BuildGameManager(ball, left, right, hud, ai);

            BuildEffects(ball, left, right);
            BuildAudio(ball, manager, left, right);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("Smily Volley : scène construite dans " + ScenePath);
        }

        // ------------------------------------------------------------------ décor

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, CameraBottomY + CameraMinSize, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraMinSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.62f, 0.88f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();

            var fitter = go.AddComponent<CameraFitter>();
            fitter.minVisibleHalfWidth = WallX + 0.7f;
            fitter.minSize = CameraMinSize;
            fitter.bottomY = CameraBottomY;

            return cam;
        }

        /// <summary>Mur invisible sur le bord haut du champ visible : la balle ne sort jamais du cadre.</summary>
        static void BuildCeiling(Transform parent, Camera cam, PhysicsMaterial2D material)
        {
            var go = new GameObject("Ceiling");
            go.transform.SetParent(parent, false);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(44f, CeilingThickness);
            box.sharedMaterial = material;

            var ceiling = go.AddComponent<ScreenCeiling>();
            ceiling.targetCamera = cam;
            ceiling.thickness = CeilingThickness;
        }

        /// <summary>
        /// Lumière globale 2D. Sous le Renderer 2D d'URP, les sprites utilisent le matériau
        /// Sprite-Lit-Default : sans aucune lumière dans la scène, ils seraient rendus noirs.
        /// </summary>
        static void BuildGlobalLight(Transform parent)
        {
            var go = new GameObject("Global Light 2D");
            go.transform.SetParent(parent, false);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
        }

        static void BuildBackdrop(Transform parent, Sprite sky, Sprite square)
        {
            var skyGo = NewSprite("Sky", parent, sky, Color.white, OrderSky);
            skyGo.transform.position = new Vector3(0f, 3f, 0f);
            skyGo.transform.localScale = new Vector3(160f, 2.2f, 1f);

            // Bordures latérales : elles masquent l'extérieur du terrain sur les écrans larges.
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var border = NewSprite(i == 0 ? "BorderLeft" : "BorderRight", parent, square, BorderColor, OrderBorder);
                border.transform.position = new Vector3(sign * (WallX + 2f), 2f, 0f);
                border.transform.localScale = new Vector3(4f, 24f, 1f);
            }
        }

        static void BuildGround(Transform parent, Sprite square, PhysicsMaterial2D material)
        {
            var visual = NewSprite("GroundVisual", parent, square, SandColor, OrderGround);
            visual.transform.position = new Vector3(0f, GroundY - 3f, 0f);
            visual.transform.localScale = new Vector3(44f, 6f, 1f);

            var line = NewSprite("GroundLine", parent, square, SandLineColor, OrderGround + 1);
            line.transform.position = new Vector3(0f, GroundY - 0.05f, 0f);
            line.transform.localScale = new Vector3(44f, 0.1f, 1f);

            var collider = new GameObject("Ground");
            collider.transform.SetParent(parent, false);
            collider.transform.position = new Vector3(0f, GroundY - 3f, 0f);
            var box = collider.AddComponent<BoxCollider2D>();
            box.size = new Vector2(44f, 6f);
            box.sharedMaterial = material;
            collider.AddComponent<GroundSurface>();
        }

        static void BuildWalls(Transform parent, Sprite square, PhysicsMaterial2D material)
        {
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var wall = new GameObject(i == 0 ? "WallLeft" : "WallRight");
                wall.transform.SetParent(parent, false);
                wall.transform.position = new Vector3(sign * (WallX + 5f), 4f, 0f);
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = new Vector2(10f, 40f);
                box.sharedMaterial = material;
            }
        }

        static void BuildNet(Transform parent, Sprite netSprite, PhysicsMaterial2D material)
        {
            var net = NewSprite("Net", parent, netSprite, Color.white, OrderNet);
            net.transform.position = new Vector3(0f, GroundY, 0f);

            // Le mât est un rectangle coiffé d'un disque : un sommet plat laisserait la balle
            // s'y poser en équilibre et bloquerait l'échange.
            float shaftHeight = NetHeight - NetHalfWidth;

            var box = net.AddComponent<BoxCollider2D>();
            box.size = new Vector2(NetHalfWidth * 2f, shaftHeight);
            box.offset = new Vector2(0f, shaftHeight * 0.5f);
            box.sharedMaterial = material;

            var cap = net.AddComponent<CircleCollider2D>();
            cap.radius = NetHalfWidth;
            cap.offset = new Vector2(0f, shaftHeight);
            cap.sharedMaterial = material;
        }

        static void BuildShadow(Transform parent, Sprite sprite, Transform target, float scale)
        {
            var go = NewSprite(target.name + "Shadow", parent, sprite, Color.white, OrderShadow);
            var projector = go.AddComponent<GroundShadow>();
            projector.target = target;
            projector.groundY = GroundY + 0.03f;
            projector.baseScale = scale;
        }

        // ------------------------------------------------------------------ acteurs

        static BallController BuildBall(Sprite sprite, PhysicsMaterial2D material)
        {
            var go = new GameObject("Ball");
            go.transform.position = new Vector3(-BlobStartX, GroundY + 3.6f, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 1.5f;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = BallRadius;
            collider.sharedMaterial = material;

            var visual = NewSprite("Visual", go.transform, sprite, Color.white, OrderBall);

            var ball = go.AddComponent<BallController>();
            ball.visual = visual.transform;
            return ball;
        }

        static BlobController BuildBlob(Side side, Sprite sprite)
        {
            var go = new GameObject(side == Side.Left ? "BlobLeft" : "BlobRight");
            go.transform.position = new Vector3(side.Sign() * BlobStartX, GroundY, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = BlobRadius;

            var visual = NewSprite("Visual", go.transform, sprite, Color.white, OrderBlob);

            var blob = go.AddComponent<BlobController>();
            blob.side = side;
            blob.groundY = GroundY;
            blob.radius = BlobRadius;
            blob.visual = visual.transform;

            // Chaque blob est confiné à son camp : mur latéral d'un côté, filet de l'autre.
            float inner = NetHalfWidth + BlobRadius;
            float outer = WallX - BlobRadius;
            blob.minX = side == Side.Left ? -outer : inner;
            blob.maxX = side == Side.Left ? -inner : outer;

            var human = go.AddComponent<HumanBlobInput>();
            if (side == Side.Left)
            {
                // Key designe une position physique QWERTY : A / D / W se jouent
                // Q / D / Z sur un clavier AZERTY. Voir HumanBlobInput.
                human.leftKey = Key.A;
                human.rightKey = Key.D;
                human.jumpKey = Key.W;
                human.altLeftKey = Key.None;
                human.altRightKey = Key.None;
                human.altJumpKey = Key.Space;
            }
            else
            {
                human.leftKey = Key.LeftArrow;
                human.rightKey = Key.RightArrow;
                human.jumpKey = Key.UpArrow;
                human.altLeftKey = Key.None;
                human.altRightKey = Key.None;
                human.altJumpKey = Key.None;
            }

            return blob;
        }

        static AiBlobInput ConfigureAi(BlobController blob, BallController ball)
        {
            var ai = blob.gameObject.AddComponent<AiBlobInput>();
            ai.ball = ball;
            ai.blob = blob;
            ai.wallMinX = -WallX;
            ai.wallMaxX = WallX;
            ai.idleX = blob.side.Sign() * BlobStartX;
            ai.jumpReach = 2.6f;
            return ai;
        }

        // ------------------------------------------------------------------ interface

        static HudController BuildHud()
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var hud = canvasGo.AddComponent<HudController>();

            hud.leftScoreText = CreateText(canvasGo.transform, "LeftScore", "0", 110, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(220f, 150f), new Vector2(-170f, 0f));

            hud.rightScoreText = CreateText(canvasGo.transform, "RightScore", "0", 110, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(220f, 150f), new Vector2(170f, 0f));

            CreateText(canvasGo.transform, "ScoreSeparator", "-", 90, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(120f, 150f), Vector2.zero);

            hud.modeText = CreateText(canvasGo.transform, "Mode", "", 34, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(900f, 50f), Vector2.zero);

            hud.messageText = CreateText(canvasGo.transform, "Message", "", 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 220f), Vector2.zero);

            // Le bandeau d'aide se lit sur le sable : encre sombre plutôt que blanc contourné.
            hud.hintText = CreateText(canvasGo.transform, "Hint", "", 30, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1600f, 50f), Vector2.zero,
                new Color(0.28f, 0.21f, 0.10f), false);

            return hud;
        }

        static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 extraOffset,
            Color? color = null, bool outlined = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y > 0.9f ? 1f : (anchorMin.y < 0.1f ? 0f : 0.5f));
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition + extraOffset;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outlined)
            {
                // Le contour duplique le glyphe : au-delà de ~3 % du corps il empâte les petits textes.
                float width = Mathf.Max(1f, fontSize * 0.028f);
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
                outline.effectDistance = new Vector2(width, -width);
            }

            return text;
        }

        static GameManager BuildGameManager(BallController ball, BlobController left, BlobController right,
            HudController hud, AiBlobInput ai)
        {
            var go = new GameObject("GameManager");
            var manager = go.AddComponent<GameManager>();
            manager.ball = ball;
            manager.leftBlob = left;
            manager.rightBlob = right;
            manager.hud = hud;
            manager.groundY = GroundY;
            manager.rightPlayerIsAi = true;
            manager.aiDifficulty = ai != null ? ai.difficulty : 0.65f;
            return manager;
        }

        // ------------------------------------------------------------------ effets

        /// <summary>
        /// Trois systèmes de particules, un par nature d'impact, qu'<see cref="ImpactEffects"/>
        /// alimente à la demande. Aucun n'émet tout seul : l'émission est désactivée et les
        /// bouffées sont déclenchées par événement.
        /// </summary>
        static void BuildEffects(BallController ball, BlobController left, BlobController right)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlaceholderArt.ArtFolder + "/spark.png");
            if (texture == null) Debug.LogError("Texture de particule introuvable : spark.png");

            Material material = CreateParticleMaterial(texture);

            var root = new GameObject("Effects").transform;

            // Frappe : éclat clair et bref, à peine soumis à la gravité.
            ParticleSystem hit = BuildBurst(root, "HitBurst", material,
                new Color(1f, 0.97f, 0.80f), new Vector2(0.13f, 0.28f), new Vector2(2.2f, 5.2f),
                0.32f, 0.5f, 360f);

            // Sable : grains projetés vers le haut qui retombent aussitôt.
            ParticleSystem sand = BuildBurst(root, "SandBurst", material,
                SandColor, new Vector2(0.12f, 0.26f), new Vector2(1.6f, 4.2f),
                0.60f, 2.4f, 110f);

            // Mur, filet, plafond : étincelle discrète.
            ParticleSystem bounce = BuildBurst(root, "BounceBurst", material,
                new Color(1f, 1f, 1f), new Vector2(0.09f, 0.19f), new Vector2(1.5f, 3.6f),
                0.26f, 0.3f, 360f);

            var go = new GameObject("ImpactEffects");
            go.transform.SetParent(root, false);

            var effects = go.AddComponent<ImpactEffects>();
            effects.ball = ball;
            effects.leftBlob = left;
            effects.rightBlob = right;
            effects.hitBurst = hit;
            effects.sandBurst = sand;
            effects.bounceBurst = bounce;
        }

        /// <param name="arc">360 = bouffée dans toutes les directions ; moins = cône vers le haut.</param>
        static ParticleSystem BuildBurst(Transform parent, string name, Material material, Color color,
            Vector2 sizeRange, Vector2 speedRange, float lifetime, float gravity, float arc)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            // Le système doit rester « en lecture » pour que les particules ajoutées par
            // Emit() soient simulées : d'où playOnAwake et loop, malgré l'émission coupée.
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            // En espace monde, une bouffée reste où elle est née même si le système bouge.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.06f;
            shape.radiusThickness = 1f;
            shape.arc = arc;
            // Le cône par défaut pointe vers +Z : on le bascule vers le haut de l'écran.
            shape.rotation = new Vector3(0f, 0f, arc >= 360f ? 0f : 90f - arc * 0.5f);

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeOut(Color.white);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = OrderParticles;

            return system;
        }

        /// <summary>Dégradé qui garde la teinte et efface l'alpha sur la fin de vie.</summary>
        static Gradient FadeOut(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        /// <summary>
        /// Matériau des particules.
        ///
        /// Il faut le shader Particles d'URP, et pas un shader de sprite : les shaders
        /// « 2D/Sprite-* » ne sont pas dessinés pour un ParticleSystemRenderer. Testé —
        /// les particules existaient, vivaient et se déclaraient visibles, mais rien
        /// n'apparaissait à l'écran, même agrandies à cent pixels.
        ///
        /// Ce shader démarre opaque : le passer en transparence demande de régler à la
        /// main la surface, le mélange, le ZWrite, le mot-clé et la file de rendu, ce que
        /// l'inspecteur ferait sinon pour nous.
        /// </summary>
        static Material CreateParticleMaterial(Texture2D texture)
        {
            string path = MaterialsFolder + "/Particle.mat";

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("Shader de particules URP introuvable.");
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

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);

            material.SetFloat("_Surface", 1f);   // 1 = Transparent
            material.SetFloat("_Blend", 0f);     // 0 = Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ son

        static void BuildAudio(BallController ball, GameManager manager, BlobController left, BlobController right)
        {
            var go = new GameObject("Audio");
            var audio = go.AddComponent<GameAudio>();

            audio.ball = ball;
            audio.manager = manager;
            audio.leftBlob = left;
            audio.rightBlob = right;

            audio.blobHitClips = LoadClips("impactSoft_medium");
            audio.bounceClips = LoadClips("impactPlate_light");
            audio.ballLandClips = LoadClips("impactSoft_heavy");
            audio.blobLandClips = LoadClips("footstep_snow");
            audio.pointClips = LoadClips("impactBell_heavy");
        }

        /// <summary>
        /// Règle l'import des sons. Un effet court laissé en « Compressed In Memory » se
        /// décode à chaque lecture : sur une frappe de balle, le hoquet s'entend. On les
        /// décompresse une fois pour toutes au chargement, et on coupe la spatialisation
        /// puisque le mixage est entièrement 2D.
        /// </summary>
        static void ConfigureAudioImport()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                // Depuis Unity 6, le préchargement est un réglage par plateforme porté
                // par les sample settings, plus une propriété de l'importeur.
                settings.preloadAudioData = true;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Charge les variantes 000 à 004 d'une famille de sons du pack Kenney.</summary>
        static AudioClip[] LoadClips(string prefix, int count = 5)
        {
            var clips = new System.Collections.Generic.List<AudioClip>(count);

            for (int i = 0; i < count; i++)
            {
                string path = $"{AudioFolder}/{prefix}_{i:000}.ogg";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                if (clip != null) clips.Add(clip);
                else Debug.LogWarning("Son introuvable : " + path);
            }

            return clips.ToArray();
        }

        // ------------------------------------------------------------------ outils

        static GameObject NewSprite(string name, Transform parent, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        static Sprite LoadSprite(string fileName)
        {
            string path = PlaceholderArt.ArtFolder + "/" + fileName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError("Sprite introuvable : " + path);
            return sprite;
        }

        static PhysicsMaterial2D CreateMaterial(string name, float bounciness)
        {
            string path = MaterialsFolder + "/" + name + ".physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                material = new PhysicsMaterial2D(name);
                AssetDatabase.CreateAsset(material, path);
            }

            material.bounciness = bounciness;
            material.friction = 0f;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
