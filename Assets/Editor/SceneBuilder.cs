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
        const string MusicFolder = "Assets/Audio/Music";

        // L'affiche du jeu, seul asset graphique qui ne soit pas dessiné par code.
        const string SplashFile = "splash-screen.jpg";
        // Fredoka (SIL Open Font License) : des lettres rondes et pleines, de la même
        // famille de formes que les blobs et que le logo de l'affiche.
        const string FontPath = "Assets/Fonts/Fredoka.ttf";

        static readonly Color SandColor = new Color(0.93f, 0.82f, 0.58f);
        static readonly Color SandLineColor = new Color(0.78f, 0.66f, 0.44f);
        static readonly Color BorderColor = new Color(0.13f, 0.16f, 0.22f);

        [MenuItem("Smily Volley/Construire la scène de jeu")]
        public static void Build()
        {
            PlaceholderArt.GenerateAll();
            BlobArt.GenerateAll();
            ConfigureAudioImport();
            ConfigureSplashImport();

            var bouncyWall = CreateMaterial("Bouncy", 0.92f);
            var softGround = CreateMaterial("Sand", 0.45f);

            Sprite square = LoadSprite("square.png");
            Sprite sky = LoadSprite("sky.png");
            Sprite net = LoadSprite("net.png");
            Sprite shadow = LoadSprite("shadow.png");
            Sprite ballSprite = LoadSprite("ball.png");

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
            BlobController left = BuildBlob(Side.Left);
            BlobController right = BuildBlob(Side.Right);

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
            GameAudio audio = BuildAudio(ball, manager, left, right);
            MenuController menu = BuildMenu(manager, audio, left, right, hud);
            BuildTouchHud(manager, menu);
            BuildOrientationGate(menu);
            BuildStamp();

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

        static BlobController BuildBlob(Side side)
        {
            var go = new GameObject(side == Side.Left ? "BlobLeft" : "BlobRight");
            go.transform.position = new Vector3(side.Sign() * BlobStartX, GroundY, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = BlobRadius;

            var blob = go.AddComponent<BlobController>();
            blob.side = side;
            blob.groundY = GroundY;
            blob.radius = BlobRadius;

            AttachJelly(blob, side);

            // Chaque blob est confiné à son camp : mur latéral d'un côté, filet de l'autre.
            float inner = NetHalfWidth + BlobRadius;
            float outer = WallX - BlobRadius;
            blob.minX = side == Side.Left ? -outer : inner;
            blob.maxX = side == Side.Left ? -inner : outer;

            var human = go.AddComponent<HumanBlobInput>();
            // Le camp n'est pas déductible des touches : c'est lui qui dit de quel côté de l'écran
            // ce joueur trouve ses boutons tactiles.
            human.side = side;
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

        /// <summary>
        /// Pose le corps du blob : un maillage déformable, pas un sprite.
        ///
        /// Le maillage est construit et animé à l'exécution par <see cref="BlobJelly"/>. La
        /// scène n'a donc rien à enregistrer d'autre que le matériau — la peau du joueur, qui
        /// porte les trois styles côte à côte — et le lien vers le blob, d'où viennent les chocs.
        /// </summary>
        static void AttachJelly(BlobController blob, Side side)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(blob.transform, false);

            go.AddComponent<MeshFilter>();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadBlobMaterial(side);
            renderer.sortingOrder = OrderBlob;

            // Un maillage n'a pas les réglages d'un sprite : sans ces quatre lignes il
            // demanderait ombres et sondes de lumière, inutiles dans une scène en deux dimensions.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var jelly = go.AddComponent<BlobJelly>();
            jelly.blob = blob;
        }

        static Material LoadBlobMaterial(Side side)
        {
            string path = BlobArt.MaterialPath(side);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) Debug.LogError("Matériau de blob introuvable : " + path);
            return material;
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

        /// <summary>
        /// Tampon de build, en bas à droite.
        /// </summary>
        /// <remarks>
        /// Sur son propre canevas, et non dans le HUD : celui-ci s'éteint dès qu'un menu s'ouvre,
        /// or c'est du menu que viennent la plupart des captures d'écran. Ordre de tri élevé pour
        /// qu'il reste lisible par-dessus le voile du menu — un tampon caché ne tamponne rien.
        /// </remarks>
        static void BuildStamp()
        {
            var canvasGo = new GameObject("BuildStamp");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Encre du sable plutôt que blanc contourné : le coin bas-droit tombe sur la plage
            // aussi bien au menu qu'en match, et le blanc y était délavé au point de ne plus se lire.
            Text label = CreateText(canvasGo.transform, "Label", "", 22, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 12f), new Vector2(420f, 34f),
                Vector2.zero, new Color(0.28f, 0.21f, 0.10f, 0.6f), false);

            // Le pivot par défaut de CreateText centre horizontalement : ancré à droite, le texte
            // déborderait de la moitié de sa boîte hors de l'écran.
            var rect = (RectTransform)label.transform;
            rect.pivot = new Vector2(1f, 0f);

            label.gameObject.AddComponent<BuildStampLabel>();
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
            text.font = GameFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outlined)
            {
                // Le contour duplique le glyphe : au-delà de ~2 % du corps il empâte les
                // petits textes et creuse les lettres rondes de la police du jeu. Bleu de
                // nuit plutôt que noir : sur une plage en plein soleil, le noir pur tranche.
                float width = Mathf.Max(1f, fontSize * 0.020f);
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.13f, 0.24f, 0.72f);
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

        static GameAudio BuildAudio(BallController ball, GameManager manager, BlobController left, BlobController right)
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
            audio.musicClip = LoadMusic(menu: false);
            audio.menuMusicClip = LoadMusic(menu: true);
            return audio;
        }

        /// <summary>
        /// Morceau du dossier de musique. Le nom fait le tri : celui qui porte « menu »
        /// accompagne l'affiche, le premier des autres accompagne le match. Aucun réglage
        /// à toucher pour remplacer l'un ou l'autre — il suffit de nommer le fichier.
        /// Absent : le jeu se joue en silence, et le menu garde la musique du match.
        /// </summary>
        static AudioClip LoadMusic(bool menu)
        {
            if (!AssetDatabase.IsValidFolder(MusicFolder))
            {
                Debug.LogWarning("Dossier de musique absent : " + MusicFolder);
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isMenu = System.IO.Path.GetFileNameWithoutExtension(path)
                    .IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isMenu != menu) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) return clip;
            }

            Debug.LogWarning($"Aucune musique {(menu ? "de menu" : "de match")} trouvée dans {MusicFolder}");
            return null;
        }

        /// <summary>
        /// Règle l'import des sons. Un effet court laissé en « Compressed In Memory » se
        /// décode à chaque lecture : sur une frappe de balle, le hoquet s'entend. On les
        /// décompresse une fois pour toutes au chargement, et on coupe la spatialisation
        /// puisque le mixage est entièrement 2D.
        /// </summary>
        static void ConfigureAudioImport()
        {
            // Effets courts : décompressés une fois pour toutes, et ramenés en mono
            // puisque le mixage est entièrement 2D.
            ImportAudio(AudioFolder, AudioClipLoadType.DecompressOnLoad, mono: true);

            // Musique : surtout pas DecompressOnLoad. Les 52 s du morceau occuperaient
            // près de 9 Mo de RAM en PCM, contre 350 Ko en restant compressés.
            ImportAudio(MusicFolder, AudioClipLoadType.CompressedInMemory, mono: false);
        }

        static void ImportAudio(string folder, AudioClipLoadType loadType, bool mono)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                // Depuis Unity 6, le préchargement est un réglage par plateforme porté
                // par les sample settings, plus une propriété de l'importeur.
                settings.preloadAudioData = true;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = mono;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Fait de l'affiche un sprite utilisable par l'interface. Elle arrive comme une
        /// image ordinaire : sans cette passe, Unity l'importe en texture et
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> ne rend rien.
        ///
        /// Les 2048 pixels par défaut suffisent à la montrer plein cadre en 1080p.
        /// </summary>
        static void ConfigureSplashImport()
        {
            string path = PlaceholderArt.ArtFolder + "/" + SplashFile;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Affiche introuvable : " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;

            var platform = importer.GetPlatformTextureSettings("Standalone");
            if (platform.overridden)
            {
                platform.overridden = false;
                importer.SetPlatformTextureSettings(platform);
            }

            importer.SaveAndReimport();
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

        // ------------------------------------------------------------------ menu

        const int MenuRowCount = 14;
        const float MenuRowHeight = 52f;
        // Largeur des lignes. Les réglages ont besoin de toute la place — libellé à gauche,
        // valeur et boutons à droite. Un écran d'entrées seules n'en veut pas : une carte
        // de mille pixels sous quatre mots courts couvre l'affiche pour rien.
        const float MenuWidth = 1120f;
        const float MenuNarrowWidth = 720f;

        // Le panneau déborde des lignes : sans cette marge, les libellés collent au bord
        // arrondi et la carte ressemble à un rectangle mal coupé.
        const float MenuCardPadding = 26f;
        // Bas du panneau, mesuré depuis le bas de l'écran : juste au-dessus du bandeau
        // d'aide. Le panneau grandit vers le haut, donc un menu court reste posé en bas de
        // l'affiche, sous son logo, et l'écran d'options monte sans jamais le recouvrir.
        const float MenuCardBottom = 96f;

        // Le blob curseur. Le cadre suit le rapport du sprite recadré (260 sur 132) :
        // ajusté au jugé, « conserver les proportions » rétrécissait le blob pour tenir
        // dans une boîte trop étroite, et il paraissait deux fois trop petit.
        const float MenuCursorHeight = 44f;
        const float MenuCursorWidth = MenuCursorHeight * 260f / 132f;

        // Colonne de droite d'une ligne de menu, mesurée depuis son bord droit. Le libellé
        // s'arrête avant le − le plus à gauche : les valeurs s'alignent alors toutes sur la
        // même colonne, que la ligne porte des boutons ou non.
        const float MenuStepSize = 44f;
        const float MenuStepPlusX = -28f;
        const float MenuStepMinusX = -80f;
        const float MenuValueX = -132f;
        const float MenuValueWidth = 380f;
        const float MenuLabelWidth = 540f;
        // Les libellés démarrent après la place du blob : c'est là qu'il se pose, et un
        // libellé qui commencerait plus tôt le recouvrirait.
        const float MenuLabelX = 104f;

        // Palette du menu, reprise de l'affiche et du terrain : ciel bleu, sable crème,
        // bleu profond du logo. L'ancien menu était un voile bleu nuit posé sur un jeu en
        // plein soleil — les deux ne se ressemblaient pas.
        static readonly Color MenuCardColor = new Color(1f, 0.98f, 0.93f, 0.95f);
        static readonly Color MenuVeilColor = new Color(0.62f, 0.82f, 0.95f, 0.55f);
        static readonly Color MenuTitleColor = new Color(0.10f, 0.33f, 0.58f);
        static readonly Color MenuFooterColor = new Color(0.16f, 0.30f, 0.44f);
        static readonly Color MenuStepTint = new Color(0.16f, 0.52f, 0.80f);

        /// <summary>
        /// Menu principal, options et pause, sur un canvas propre posé au-dessus du HUD.
        /// Les lignes sont créées une bonne fois — quatorze suffisent à remplir l'écran —
        /// puis <see cref="MenuController"/> y fait défiler les entrées de l'écran courant.
        ///
        /// L'affiche du jeu sert de fond au menu principal ; les options et la pause lui
        /// préfèrent le terrain sous un voile clair, pour que le joueur voie ce qu'il règle.
        /// </summary>
        static MenuController BuildMenu(GameManager manager, GameAudio audio, BlobController left,
            BlobController right, HudController hud)
        {
            var canvasGo = new GameObject("Menu");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Au-dessus du HUD : le score ne doit pas transparaître à travers le panneau.
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = new GameObject("Panel", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)root.transform);

            Image splash = BuildSplash(root.transform);

            // Voile clair : le terrain reste lisible derrière, ce qui montre au joueur ce
            // que ses réglages changent, sans concurrencer le texte. Bleu ciel plutôt que
            // bleu nuit — la plage ne s'éteint pas quand on ouvre le menu.
            var veilGo = new GameObject("Veil", typeof(RectTransform));
            veilGo.transform.SetParent(root.transform, false);
            Stretch((RectTransform)veilGo.transform);
            var veil = veilGo.AddComponent<Image>();
            veil.color = MenuVeilColor;

            Text title = MenuText(root.transform, "Title", 72, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(MenuWidth, 100f),
                MenuTitleColor);
            title.fontStyle = FontStyle.Bold;
            // Une ombre portée plutôt qu'un liseré : le contour dessine la lettre quatre
            // fois autour d'elle-même et, sur un caractère rond et plein, le blanc lui
            // mange l'intérieur — le titre paraissait creux. L'ombre le pose sur le ciel
            // sans y toucher.
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.06f, 0.20f, 0.34f, 0.30f);
            titleShadow.effectDistance = new Vector2(0f, -4f);

            Text footer = MenuText(root.transform, "Footer", 26, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1700f, 44f),
                MenuFooterColor);
            Halo(footer, new Color(1f, 1f, 1f, 0.75f), 2);

            // Carte de sable sur laquelle les lignes reposent : sans elle, un libellé bleu
            // passant sur le ciel de l'affiche puis sur une palme change de lisibilité au
            // milieu du mot.
            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(root.transform, false);
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.anchoredPosition = new Vector2(0f, MenuCardBottom);
            cardRect.sizeDelta = new Vector2(MenuWidth + MenuCardPadding * 2f, 400f);
            var card = cardGo.AddComponent<Image>();
            card.sprite = LoadSprite("panel.png");
            card.type = Image.Type.Sliced;
            card.color = MenuCardColor;

            var cardGroup = cardGo.AddComponent<CanvasGroup>();

            // Ombre portée : posée sur du sable clair, une carte crème sans ombre flotte
            // sans qu'on sache si elle est devant ou dans l'illustration.
            var shadow = cardGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.06f, 0.20f, 0.32f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -7f);

            var listGo = new GameObject("Rows", typeof(RectTransform));
            listGo.transform.SetParent(cardGo.transform, false);
            var listRect = (RectTransform)listGo.transform;
            listRect.anchorMin = new Vector2(0.5f, 1f);
            listRect.anchorMax = new Vector2(0.5f, 1f);
            listRect.pivot = new Vector2(0.5f, 1f);
            listRect.sizeDelta = new Vector2(MenuWidth, MenuRowCount * MenuRowHeight);
            listRect.anchoredPosition = new Vector2(0f, -MenuCardPadding);

            // Flèches de défilement, logées dans la marge de la carte : la liste d'options
            // est plus longue que l'écran, et rien ne le disait — un joueur arrivé en bas
            // de ce qu'il voit n'a aucune raison de deviner qu'il reste des réglages.
            Image scrollUp = ScrollArrow(cardGo.transform, "ScrollUp", new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), false, out Button scrollUpButton);
            Image scrollDown = ScrollArrow(cardGo.transform, "ScrollDown", new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), true, out Button scrollDownButton);

            var menuRows = new MenuRow[MenuRowCount];
            for (int i = 0; i < MenuRowCount; i++) menuRows[i] = BuildMenuRow(listRect, i);

            // Le blob est créé après les lignes, donc au-dessus d'elles dans la pile de
            // rendu : il doit se poser sur la carte, pas passer dessous.
            MenuCursor cursor = BuildMenuCursor(cardGo.transform);

            var menu = canvasGo.AddComponent<MenuController>();
            menu.manager = manager;
            menu.gameAudio = audio;
            menu.leftBlob = left;
            menu.rightBlob = right;
            menu.hud = hud;
            menu.hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            menu.root = root;
            menu.titleText = title;
            menu.footerText = footer;
            menu.rows = menuRows;
            menu.splash = splash;
            menu.veil = veil;
            menu.card = cardRect;
            menu.rowHeight = MenuRowHeight;
            menu.cardPadding = MenuCardPadding;
            menu.wideWidth = MenuWidth;
            menu.narrowWidth = MenuNarrowWidth;
            menu.scrollUp = scrollUp;
            menu.scrollDown = scrollDown;
            menu.scrollUpButton = scrollUpButton;
            menu.scrollDownButton = scrollDownButton;
            menu.cursor = cursor;
            menu.cardGroup = cardGroup;

            return menu;
        }

        // ------------------------------------------------------------------ tactile

        /// <summary>
        /// Le canevas des commandes tactiles : pavé directionnel et bouton de saut par camp, plus
        /// le bouton de pause. Les images elles-mêmes sont créées à l'exécution par
        /// <see cref="TouchHud"/> — leur nombre dépend du mode de jeu et leur taille de la dalle.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b><c>ConstantPixelSize</c> à l'échelle 1, et c'est essentiel</b> : c'est le seul
        /// mode où une unité d'interface vaut un pixel d'écran. Les positions calculées par
        /// <c>TouchZones</c> — qui servent aussi à décider quel bouton un doigt touche — s'y posent
        /// alors sans conversion. Sous le <c>ScaleWithScreenSize</c> du reste de l'interface, le
        /// dessin et la lecture parleraient deux repères différents, et l'écart entre eux
        /// grandirait avec la taille de l'écran : des boutons visiblement décalés de ce qui répond,
        /// sans une erreur nulle part.</para>
        ///
        /// <para>Entre le HUD (0) et le menu (10) : les commandes passent par-dessus le score,
        /// qu'elles ne touchent pas, et sous le panneau d'un menu, qui les remplace.</para>
        /// </remarks>
        static void BuildTouchHud(GameManager manager, MenuController menu)
        {
            var canvasGo = new GameObject("TouchHud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // Pas de GraphicRaycaster : ces images ne sont pas des boutons uGUI. C'est TouchInput
            // qui lit la dalle, et lui seul — un raycaster ici volerait les appuis du menu.

            var touch = canvasGo.AddComponent<TouchHud>();
            touch.manager = manager;
            touch.menu = menu;
            touch.discSprite = LoadSprite("disc.png");
            touch.triangleSprite = LoadSprite("triangle.png");
            touch.squareSprite = LoadSprite("square.png");
        }

        /// <summary>
        /// Le panneau qui s'interpose quand un appareil tactile est tenu en portrait.
        /// </summary>
        /// <remarks>
        /// Au-dessus de tout, tampon de build compris : c'est le seul écran dont le message doive
        /// rester lisible quoi qu'il arrive derrière.
        /// </remarks>
        static void BuildOrientationGate(MenuController menu)
        {
            var canvasGo = new GameObject("OrientationGate");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            // ⚠ Référence PORTRAIT, contrairement à tout le reste de l'interface. Ce panneau est le
            // seul écran du jeu qui ne s'affiche qu'en portrait : le mesurer sur les 1920 de large
            // du paysage donnait un facteur d'échelle de 0,22 et un titre de dix-huit pixels, illisible
            // sur l'écran même dont il doit corriger la tenue.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            // Sur la largeur : c'est elle qui manque en portrait, et un titre calé sur la hauteur
            // sortirait par les côtés du téléphone qu'il demande justement de tourner.
            scaler.matchWidthOrHeight = 0f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)panel.transform);

            var background = panel.AddComponent<Image>();
            background.sprite = LoadSprite("square.png");
            background.color = new Color(0.56f, 0.82f, 0.95f, 1f);
            // Opaque, et il capte les appuis : le jeu est en attente derrière, rien de ce qu'il
            // montre ne doit être ni vu ni touché.
            background.raycastTarget = true;

            // Un rectangle en format paysage : le dessin dit en une image ce que le texte explique.
            var shape = new GameObject("Landscape", typeof(RectTransform));
            shape.transform.SetParent(panel.transform, false);
            var shapeRect = (RectTransform)shape.transform;
            shapeRect.anchorMin = shapeRect.anchorMax = new Vector2(0.5f, 0.5f);
            shapeRect.pivot = new Vector2(0.5f, 0.5f);
            shapeRect.sizeDelta = new Vector2(340f, 200f);
            shapeRect.anchoredPosition = new Vector2(0f, 170f);

            var shapeImage = shape.AddComponent<Image>();
            shapeImage.sprite = LoadSprite("rounded.png");
            shapeImage.type = Image.Type.Sliced;
            shapeImage.color = new Color(1f, 0.98f, 0.93f, 0.85f);
            shapeImage.raycastTarget = false;

            // Les boîtes ne se recouvrent pas : CreateText laisse le texte déborder de la sienne
            // (Overflow), si bien que deux boîtes voisines mais trop proches donnent deux lignes
            // superposées — ce qui ne se voit qu'à l'écran, jamais dans la hiérarchie.
            CreateText(panel.transform, "Title", "Tournez l'appareil", 58, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(660f, 80f), Vector2.zero, new Color(0.10f, 0.33f, 0.58f), false);

            CreateText(panel.transform, "Body", "Smily Volley se joue en largeur :\nle terrain n'entre pas dans la hauteur.",
                32, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f),
                new Vector2(660f, 130f), Vector2.zero, new Color(0.16f, 0.30f, 0.44f), false);

            var gate = canvasGo.AddComponent<OrientationGate>();
            gate.menu = menu;
            gate.panel = panel;
        }

        /// <summary>
        /// L'affiche du jeu, en fond du menu principal.
        ///
        /// <c>EnvelopeParent</c> la fait déborder du cadre plutôt que d'y laisser des
        /// bandes : l'illustration est cadrée large, elle supporte de perdre un peu de ciel
        /// ou de sable, mais pas de flotter sur du vide.
        /// </summary>
        static Image BuildSplash(Transform parent)
        {
            var go = new GameObject("Splash", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite(SplashFile);
            image.raycastTarget = false;

            if (image.sprite != null)
            {
                var fitter = go.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            }

            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Halo clair autour d'un texte sombre : sur l'affiche, un libellé peut tomber sur
        /// une palme aussi bien que sur le sable, et seul le détourage le tient lisible
        /// dans les deux cas.
        /// </summary>
        static void Halo(Text text, Color color, int distance)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, distance);
        }

        /// <summary>
        /// Le blob qui désigne la ligne choisie. Il vit dans la marge gauche de la carte,
        /// à l'aplomb du libellé qui s'écarte pour lui : les deux mouvements se répondent.
        /// </summary>
        static MenuCursor BuildMenuCursor(Transform parent)
        {
            var go = new GameObject("Cursor", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(MenuCursorWidth, MenuCursorHeight);
            rect.anchoredPosition = new Vector2(MenuCardPadding + MenuCursorWidth * 0.5f, 0f);

            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite(BlobArt.CursorFile);
            image.raycastTarget = false;
            image.preserveAspect = true;

            var cursor = go.AddComponent<MenuCursor>();
            cursor.rect = rect;
            cursor.image = image;
            return cursor;
        }

        static MenuRow BuildMenuRow(RectTransform parent, int index)
        {
            var go = new GameObject("Row" + index, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(MenuWidth, MenuRowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * MenuRowHeight);

            // Le bandeau reste présent même transparent : c'est lui qui reçoit les clics
            // souris, et le rendre visible ne demande qu'un changement de couleur.
            var highlight = go.AddComponent<Image>();
            highlight.sprite = LoadSprite("rounded.png");
            highlight.type = Image.Type.Sliced;
            highlight.color = Color.clear;

            var button = go.AddComponent<Button>();
            button.targetGraphic = highlight;
            button.transition = Selectable.Transition.None;

            Text label = MenuText(go.transform, "Label", 34, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.5f), new Vector2(MenuLabelX, 0f),
                new Vector2(MenuLabelWidth, MenuRowHeight), Color.white, new Vector2(0f, 0.5f));

            Text value = MenuText(go.transform, "Value", 34, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(MenuValueX, 0f), new Vector2(MenuValueWidth, MenuRowHeight),
                Color.white, new Vector2(1f, 0.5f));

            var row = go.AddComponent<MenuRow>();
            row.rect = rect;
            row.highlight = highlight;
            row.label = label;
            row.labelRect = (RectTransform)label.transform;
            row.value = value;
            row.valueRect = (RectTransform)value.transform;
            row.button = button;
            row.decrease = BuildMenuStep(go.transform, "Minus", "−", MenuStepMinusX);
            row.increase = BuildMenuStep(go.transform, "Plus", "+", MenuStepPlusX);
            return row;
        }

        /// <summary>
        /// Bouton − ou + d'une ligne réglable.
        ///
        /// Sans eux, la souris ne sait que faire monter une valeur : le clic sur la ligne
        /// équivaut à la flèche droite, et rien ne la fait redescendre. Ils sont enfants de
        /// la ligne, donc au-dessus de son bandeau dans la pile de rendu : le clic leur
        /// revient, pas à la ligne.
        /// </summary>
        static Button BuildMenuStep(Transform parent, string name, string glyph, float x)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(MenuStepSize, MenuStepSize);
            rect.anchoredPosition = new Vector2(x, 0f);

            var background = go.AddComponent<Image>();
            background.sprite = LoadSprite("rounded.png");
            background.type = Image.Type.Sliced;
            background.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            // Le fond est blanc : c'est la teinte qui porte la couleur et l'opacité, et
            // donc le survol.
            ColorBlock colors = button.colors;
            colors.normalColor = Alpha(MenuStepTint, 0.16f);
            colors.highlightedColor = Alpha(MenuStepTint, 0.34f);
            colors.pressedColor = Alpha(MenuStepTint, 0.55f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = Alpha(MenuStepTint, 0.06f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            MenuText(go.transform, "Glyph", 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MenuStepSize, MenuStepSize),
                MenuTitleColor).text = glyph;

            return button;
        }

        static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>
        /// Flèche de défilement, dans la marge de la carte du menu.
        /// </summary>
        /// <remarks>
        /// Une image et non un texte : la police du jeu ne contient aucun glyphe de flèche. Sur
        /// Windows le moteur allait chercher « ▲ » dans les polices du système ; un navigateur
        /// n'en propose aucune, et l'indicateur disparaissait donc dans la version web — c'est-à-
        /// dire précisément le signe qui dit qu'il reste des réglages plus bas.
        /// </remarks>
        /// <summary>
        /// Flèche de débordement, dans la marge de la carte — et bouton de défilement.
        /// </summary>
        /// <remarks>
        /// ⚠ Elle ne se contentait que de <b>dire</b> que la liste continue, et cela suffisait tant
        /// qu'on avait une molette ou des flèches de clavier. Au doigt, une indication qui ne se
        /// touche pas ne fait qu'annoncer ce qu'on ne peut pas atteindre. La cible sensible est
        /// bien plus large que le dessin — le triangle fait quatorze pixels de haut, soit le tiers
        /// d'un doigt.
        /// </remarks>
        static Image ScrollArrow(Transform parent, string name, Vector2 anchor,
            Vector2 anchoredPosition, bool pointingDown, out Button button)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(22f, 14f);
            rect.anchoredPosition = anchoredPosition;
            if (pointingDown) rect.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite("triangle.png");
            image.color = MenuStepTint;
            image.raycastTarget = false;

            // La zone touchable est une SŒUR de la flèche, non son parent : la flèche est tournée
            // d'un demi-tour quand elle pointe vers le bas, et une cible qui hériterait de cette
            // rotation garderait sa forme mais plus sa position au pixel près.
            var zone = new GameObject(name + "Touch", typeof(RectTransform));
            zone.transform.SetParent(parent, false);
            var zoneRect = (RectTransform)zone.transform;
            zoneRect.anchorMin = anchor;
            zoneRect.anchorMax = anchor;
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.sizeDelta = new Vector2(180f, 64f);
            zoneRect.anchoredPosition = anchoredPosition;

            var hit = zone.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // invisible, mais touchable
            hit.raycastTarget = true;

            button = zone.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            return image;
        }

        static Text MenuText(Transform parent, string name, int fontSize, TextAnchor alignment,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, anchor.y);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var text = go.AddComponent<Text>();
            text.font = GameFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Les textes ne doivent pas intercepter les clics : c'est le bandeau de la
            // ligne qui les reçoit, sinon survoler un libellé désactiverait le bouton.
            text.raycastTarget = false;
            return text;
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

        /// <summary>
        /// La police de tout le jeu. Arial, qui servait avant, ne dit rien : elle habille
        /// aussi bien un tableur. Le repli reste possible pour qu'un projet cloné sans le
        /// fichier de police s'ouvre quand même, avec un texte moins joli mais lisible.
        /// </summary>
        static Font GameFont()
        {
            if (gameFont != null) return gameFont;

            gameFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (gameFont == null)
            {
                Debug.LogWarning("Police introuvable, retour à celle d'Unity : " + FontPath);
                gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return gameFont;
        }

        static Font gameFont;

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
