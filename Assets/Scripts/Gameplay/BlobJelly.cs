using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Le corps du blob, simulé comme une gelée.
    ///
    /// Le contour n'est pas une image : c'est un anneau de points reliés par des ressorts,
    /// intégré à chaque image et rendu dans un maillage reconstruit. Trois forces le font vivre :
    ///
    ///   mémoire de forme  chaque point est rappelé vers sa place au repos ;
    ///   couplage          chaque point est tiré vers le milieu de ses deux voisins, ce qui
    ///                     propage une bosse le long du contour — l'onde qui fait la gelée ;
    ///   pression          l'aire est conservée, donc le flanc gonfle de ce que le sommet perd.
    ///
    /// Ces trois forces suffisent à produire une déformation locale : la balle creuse un vrai
    /// creux là où elle frappe, l'atterrissage écrase par le haut et fait déborder les côtés,
    /// le démarrage laisse le sommet en arrière. Aucune de ces formes n'existe dans un jeu
    /// d'images : elles dépendent de l'endroit et de la force du choc.
    ///
    /// Le maillage est un éventail parti du centre de gravité vers chaque point du contour.
    /// Les UV sont figées sur la forme au repos, si bien que la texture — ombrage et visage —
    /// se déforme avec le corps sans une ligne de code de plus.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class BlobJelly : MonoBehaviour
    {
        /// <summary>Réglages mécaniques d'une gelée. C'est là que se joue le caractère d'un style.</summary>
        [System.Serializable]
        public class StyleProfile
        {
            [Tooltip("Rappel vers la forme au repos. Haut = gelée ferme, qui refuse de se déformer.")]
            public float shapeStiffness = 260f;

            [Tooltip("Couplage entre voisins. C'est lui qui fait voyager l'onde le long du contour.")]
            public float smoothStiffness = 110f;

            [Tooltip("Frottement entre voisins. Éteint le froissement du contour sans " +
                     "toucher au ballottement d'ensemble.")]
            public float smoothDamping = 8f;

            [Tooltip("Conservation de l'aire : le flanc gonfle de ce que le sommet perd.")]
            public float pressure = 300f;

            [Tooltip("Extinction des oscillations. Bas = la gelée ballotte longtemps.")]
            public float damping = 9f;

            [Tooltip("Multiplie tous les chocs reçus : une gelée molle encaisse plus.")]
            public float impact = 1f;
        }

        // ----- géométrie du contour -----

        /// <summary>Points de l'arc, de la droite (angle 0) vers la gauche (angle pi).</summary>
        const int DomePoints = 31;

        /// <summary>Points de la base, entre les deux extrémités de l'arc.</summary>
        const int BasePoints = 10;

        const int PointCount = DomePoints + BasePoints;

        /// <summary>Anneaux du centre au contour. Un de plus porte la jupe transparente.</summary>
        const int Rings = 5;
        const int RingCount = Rings + 2;

        /// <summary>
        /// Débord du maillage au-delà du contour. C'est là que l'alpha de la texture s'éteint :
        /// sans cette marge, le bord serait tranché net par la dernière rangée de triangles.
        /// </summary>
        const float SkirtWidth = 0.08f;

        /// <summary>Pas d'intégration fixe : le ressort le plus raide reste stable à ce pas.</summary>
        const float SimStep = 1f / 360f;

        const int MaxStepsPerFrame = 12;

        /// <summary>
        /// Écart d'aire au-delà duquel la pression cesse de croître, en part de l'aire au repos.
        /// La pression pousse le long de normales calculées sur le contour déformé ; sans
        /// plafond, un contour très écrasé produit une poussée qui creuse le pli qui l'a créée.
        /// </summary>
        const float MaxAreaDeficit = 0.35f;

        /// <summary>
        /// Garde-fou : aucun point ne s'éloigne de sa place au repos de plus que cela.
        ///
        /// Borner l'écart au repos plutôt que la distance au centre borne la déformation
        /// elle-même. Sans cette limite, un choc reçu en l'air — où la base n'est plus tenue
        /// par le sol — peut plier le corps en crochet, et le contour se croise.
        /// </summary>
        const float MaxOffset = 0.7f;

        /// <summary>Vitesse maximale d'un point du contour, en unités par seconde.</summary>
        const float MaxPointSpeed = 14f;

        // ----- disposition de la texture -----
        // Une tuile par style, côte à côte dans un même fichier par joueur. Ces constantes sont
        // partagées avec le générateur d'images : la texture et le maillage décrivent le même
        // espace, sinon le visage glisserait sur le corps.

        public const int TileWidth = 336;
        public const int TileHeight = 208;
        public const float TilePixelsPerUnit = 128f;

        /// <summary>Ligne de la texture où passe la base du blob (y = 0).</summary>
        public const int TileBaseRow = 32;

        [Header("Références")]
        public BlobController blob;

        [Header("Gelées")]
        [Tooltip("Ferme : revient vite, ne déborde qu'un peu.")]
        public StyleProfile round = new StyleProfile
        {
            shapeStiffness = 210f, smoothStiffness = 120f, smoothDamping = 7f,
            pressure = 340f, damping = 7f, impact = 1f,
        };

        [Tooltip("Molle : grande amplitude, ballotte plusieurs fois avant de se poser.")]
        public StyleProfile soft = new StyleProfile
        {
            shapeStiffness = 115f, smoothStiffness = 150f, smoothDamping = 10f,
            pressure = 340f, damping = 2.6f, impact = 1.05f,
        };

        [Tooltip("Moulée : très ferme, les faces reprennent leur planéité presque aussitôt.")]
        public StyleProfile angular = new StyleProfile
        {
            shapeStiffness = 640f, smoothStiffness = 55f, smoothDamping = 10f,
            pressure = 340f, damping = 15f, impact = 0.7f,
        };

        [Header("Chocs")]
        [Tooltip("Part de la vitesse de chute renvoyée dans la gelée à l'atterrissage.")]
        public float landGain = 0.95f;

        [Tooltip("Étirement à l'appui du saut, en part de la vitesse d'impulsion.")]
        public float jumpGain = 0.50f;

        [Tooltip("Creux laissé par la balle, en part de sa vitesse d'arrivée.")]
        public float ballGain = 0.55f;

        [Tooltip("Inertie latérale : le sommet reste en arrière quand le pied démarre ou s'arrête.")]
        public float inertia = 0.50f;

        readonly Vector2[] rest = new Vector2[PointCount];
        readonly Vector2[] restNormal = new Vector2[PointCount];
        readonly float[] restEdge = new float[PointCount];
        readonly Vector2[] position = new Vector2[PointCount];
        readonly Vector2[] velocity = new Vector2[PointCount];

        readonly Vector3[] vertices = new Vector3[PointCount * RingCount];
        readonly Vector2[] uv = new Vector2[PointCount * RingCount];

        Mesh mesh;
        BlobStyle current = BlobStyle.Round;
        StyleProfile profile;
        Vector2 restCentroid;
        float restArea;
        float lastBlobVelocityX;
        float leftover;

        void Awake()
        {
            SetStyle(current);
        }

        /// <summary>
        /// Construit le maillage au premier besoin.
        ///
        /// Le style est appliqué depuis le <c>Awake</c> du menu, qui peut passer avant celui
        /// de la gelée : l'ordre d'éveil ne se décrète pas. Construire à la demande rend cet
        /// ordre indifférent.
        /// </summary>
        void EnsureMesh()
        {
            if (mesh != null) return;

            mesh = new Mesh { name = "BlobJelly" };
            mesh.MarkDynamic();

            // Les sommets sont posés avant les triangles : Unity refuse un indice qui
            // désigne un sommet encore inexistant.
            mesh.SetVertices(vertices);
            mesh.SetTriangles(BuildTriangles(), 0, false);

            // Le maillage bouge à chaque image mais ne quitte jamais cette boîte. La fixer une
            // fois évite un RecalculateBounds par image, et le clignotement qu'un recalcul
            // tardif provoquerait en bord d'écran.
            mesh.bounds = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(5f, 4f, 1f));

            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void OnEnable()
        {
            if (blob == null) return;

            blob.Landed += OnLanded;
            blob.Jumped += OnJumped;
            blob.BallStruck += OnBallStruck;
            blob.Respawned += Settle;
            lastBlobVelocityX = blob.Velocity.x;
        }

        void OnDisable()
        {
            if (blob == null) return;

            blob.Landed -= OnLanded;
            blob.Jumped -= OnJumped;
            blob.BallStruck -= OnBallStruck;
            blob.Respawned -= Settle;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        // ------------------------------------------------------------------ style

        /// <summary>
        /// Change de gelée : profil mécanique, contour au repos et tuile de texture. Le contour
        /// au repos changeant, la simulation repart de la forme neuve.
        /// </summary>
        public void SetStyle(BlobStyle style)
        {
            EnsureMesh();

            current = style;
            profile = ProfileOf(style);

            BuildRestShape(FacetsOf(style));
            BuildUv((int)style);

            Settle();
        }

        public static int StyleCount => System.Enum.GetValues(typeof(BlobStyle)).Length;

        /// <summary>
        /// Nombre de faces du contour au repos, zéro pour un arc rond. Le générateur d'images
        /// lit la même valeur : le dessin et le maillage partagent une seule silhouette.
        /// </summary>
        public static int FacetsOf(BlobStyle style) => style == BlobStyle.Angular ? 10 : 0;

        /// <summary>
        /// Rayon du contour au repos pour un angle donné. Avec des faces, le rayon suit la face
        /// la plus proche : sommet plat, arêtes nettes aux multiples de la face.
        /// </summary>
        public static float RestRadius(float angle, int facets)
        {
            if (facets < 3) return 1f;

            float sector = 2f * Mathf.PI / facets;
            float local = Mathf.Repeat(angle, sector) - sector * 0.5f;
            return Mathf.Cos(sector * 0.5f) / Mathf.Cos(local);
        }

        StyleProfile ProfileOf(BlobStyle style) => style switch
        {
            BlobStyle.Soft => soft,
            BlobStyle.Angular => angular,
            _ => round,
        };

        // ------------------------------------------------------------------ forme au repos

        void BuildRestShape(int facets)
        {
            for (int i = 0; i < DomePoints; i++)
            {
                float angle = Mathf.PI * i / (DomePoints - 1);
                float radius = RestRadius(angle, facets);
                rest[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            // La base ferme le contour, de l'extrémité gauche vers l'extrémité droite. Le tour
            // est ainsi parcouru dans le sens direct : les normales sortent bien du corps.
            float left = rest[DomePoints - 1].x;
            float right = rest[0].x;

            for (int j = 0; j < BasePoints; j++)
            {
                float t = (j + 1f) / (BasePoints + 1f);
                rest[DomePoints + j] = new Vector2(Mathf.Lerp(left, right, t), 0f);
            }

            restCentroid = Centroid(rest);
            restArea = SignedArea(rest);

            for (int i = 0; i < PointCount; i++)
            {
                restNormal[i] = OutwardNormal(rest, i);
                restEdge[i] = Vector2.Distance(rest[i], rest[i == PointCount - 1 ? 0 : i + 1]);
            }
        }

        void BuildUv(int tile)
        {
            float sheetWidth = TileWidth * StyleCount;

            for (int ring = 0; ring < RingCount; ring++)
            {
                for (int i = 0; i < PointCount; i++)
                {
                    Vector2 point = RestVertex(ring, i);

                    float px = tile * TileWidth + point.x * TilePixelsPerUnit + TileWidth * 0.5f;
                    float py = point.y * TilePixelsPerUnit + TileBaseRow;

                    uv[ring * PointCount + i] = new Vector2(px / sheetWidth, py / TileHeight);
                }
            }

            mesh.SetUVs(0, uv);
        }

        /// <summary>Place d'un sommet sur la forme au repos : c'est elle qui fige les UV.</summary>
        Vector2 RestVertex(int ring, int i)
        {
            if (ring == RingCount - 1) return rest[i] + restNormal[i] * SkirtWidth;

            float t = ring / (float)Rings;
            return restCentroid + (rest[i] - restCentroid) * t;
        }

        /// <summary>Remet la gelée au repos, sans oscillation résiduelle.</summary>
        public void Settle()
        {
            for (int i = 0; i < PointCount; i++)
            {
                position[i] = rest[i];
                velocity[i] = Vector2.zero;
            }

            leftover = 0f;
            if (blob != null) lastBlobVelocityX = blob.Velocity.x;

            WriteMesh();
        }

        // ------------------------------------------------------------------ chocs

        /// <summary>Le sable arrête le pied, pas le sommet : le haut continue vers le bas.</summary>
        void OnLanded(Vector2 point, float fallSpeed)
        {
            Push(Vector2.down, fallSpeed * landGain);
        }

        void OnJumped(Vector2 point)
        {
            float speed = blob != null ? blob.jumpSpeed : 9f;
            Push(Vector2.up, speed * jumpGain);
        }

        /// <summary>
        /// Creux laissé par la balle. La direction va du centre du blob vers la balle : les
        /// points qui regardent de ce côté reculent, les autres ne bougent pas. Le cube du
        /// produit scalaire resserre le creux autour du point de contact.
        /// </summary>
        void OnBallStruck(Vector2 direction, float speed)
        {
            // Un smash arrive bien plus vite qu'une passe : sans plafond, le creux traverse
            // le corps et le contour se plie en deux.
            float strength = Mathf.Min(speed, 13f) * ballGain * profile.impact;

            for (int i = 0; i < PointCount; i++)
            {
                float facing = Vector2.Dot(rest[i].normalized, direction);
                if (facing <= 0f) continue;

                velocity[i] -= direction * (strength * facing * facing * facing);
            }
        }

        /// <summary>
        /// Poussée pondérée par la hauteur au repos : nulle au sol, entière au sommet. C'est ce
        /// dégradé qui écrase le blob au lieu de le déplacer en bloc.
        /// </summary>
        void Push(Vector2 axis, float amount)
        {
            float strength = amount * profile.impact;

            for (int i = 0; i < PointCount; i++)
            {
                velocity[i] += axis * (strength * Mathf.Clamp01(rest[i].y));
            }
        }

        // ------------------------------------------------------------------ simulation

        void LateUpdate()
        {
            if (profile == null) return;

            ApplyLateralInertia();

            // Pas fixe : la plus raide des trois gelées deviendrait instable si le pas suivait
            // la fréquence d'affichage. Le reliquat est reporté sur l'image suivante.
            leftover += Mathf.Min(Time.deltaTime, 0.1f);

            int steps = 0;
            while (leftover >= SimStep && steps < MaxStepsPerFrame)
            {
                Step(SimStep);
                leftover -= SimStep;
                steps++;
            }

            if (steps == MaxStepsPerFrame) leftover = 0f;

            WriteMesh();
        }

        /// <summary>
        /// Le blob change de vitesse d'un coup — il n'a pas d'inertie, c'est ce qui donne son
        /// contrôle sec. La gelée, elle, en a une : le sommet garde l'ancienne vitesse un
        /// instant. Une différence de vitesse est déjà une impulsion, il n'y a pas à la
        /// diviser par le pas de temps.
        /// </summary>
        void ApplyLateralInertia()
        {
            if (blob == null) return;

            float delta = blob.Velocity.x - lastBlobVelocityX;
            lastBlobVelocityX = blob.Velocity.x;

            if (Mathf.Abs(delta) < 0.0001f) return;

            float gain = delta * inertia * profile.impact;
            for (int i = 0; i < PointCount; i++)
            {
                velocity[i].x -= gain * Mathf.Clamp01(rest[i].y);
            }
        }

        void Step(float dt)
        {
            float deficit = Mathf.Clamp(restArea - SignedArea(position),
                -MaxAreaDeficit * restArea, MaxAreaDeficit * restArea);
            float squeeze = deficit * profile.pressure;

            for (int i = 0; i < PointCount; i++)
            {
                int previous = i == 0 ? PointCount - 1 : i - 1;
                int next = i == PointCount - 1 ? 0 : i + 1;

                Vector2 middle = (position[previous] + position[next]) * 0.5f;
                Vector2 drift = (velocity[previous] + velocity[next]) * 0.5f - velocity[i];

                Vector2 force = (rest[i] - position[i]) * profile.shapeStiffness;
                force += (middle - position[i]) * profile.smoothStiffness;
                force += drift * profile.smoothDamping;
                force += OutwardNormal(position, i) * squeeze;
                force -= velocity[i] * profile.damping;

                velocity[i] += force * dt;

                float speed = velocity[i].magnitude;
                if (speed > MaxPointSpeed) velocity[i] *= MaxPointSpeed / speed;
            }

            bool grounded = blob == null || blob.Grounded;

            for (int i = 0; i < PointCount; i++)
            {
                position[i] += velocity[i] * dt;

                Vector2 offset = position[i] - rest[i];
                if (offset.sqrMagnitude > MaxOffset * MaxOffset)
                {
                    position[i] = rest[i] + offset.normalized * MaxOffset;
                    velocity[i] *= 0.5f;
                }

                // Posé sur le sable, rien ne passe sous la ligne de base. En l'air le ventre
                // est libre de s'arrondir vers le bas, ce qui se voit au sommet du saut.
                if (grounded && position[i].y < 0f)
                {
                    position[i].y = 0f;
                    if (velocity[i].y < 0f) velocity[i].y = 0f;
                }
            }

            LimitStretch();
            UnfoldBase();
        }

        /// <summary>
        /// Empêche une arête du contour de s'étirer au-delà d'un peu plus du double de sa
        /// longueur au repos.
        ///
        /// Sans ce plafond, un point emporté par un choc part seul et laisse derrière lui une
        /// aiguille d'un pixel de large. La contrainte se règle en tirant les deux extrémités
        /// l'une vers l'autre : c'est plus court à écrire qu'un ressort, et surtout ça ne
        /// rajoute pas d'énergie au système.
        /// </summary>
        void LimitStretch()
        {
            const float slack = 2.2f;

            for (int i = 0; i < PointCount; i++)
            {
                int next = i == PointCount - 1 ? 0 : i + 1;

                Vector2 edge = position[next] - position[i];
                float length = edge.magnitude;
                float limit = restEdge[i] * slack;

                if (length <= limit || length < 1e-5f) continue;

                Vector2 pull = edge * ((length - limit) / length * 0.5f);
                position[i] += pull;
                position[next] -= pull;
            }
        }

        /// <summary>
        /// Garde la base ordonnée de gauche à droite.
        ///
        /// Sous un grand écrasement, deux points voisins de la base finissent par se croiser :
        /// le contour se noue et un coin du corps disparaît. Les écarter du strict nécessaire
        /// coûte moins qu'un modèle de contact, et suffit — le nœud n'a jamais le temps de
        /// se former.
        /// </summary>
        void UnfoldBase()
        {
            const float gap = 0.03f;

            for (int k = 0; k <= BasePoints; k++)
            {
                int left = k == 0 ? DomePoints - 1 : DomePoints + k - 1;
                int right = k == BasePoints ? 0 : DomePoints + k;

                float overlap = position[left].x + gap - position[right].x;
                if (overlap <= 0f) continue;

                position[left].x -= overlap * 0.5f;
                position[right].x += overlap * 0.5f;
            }
        }

        // ------------------------------------------------------------------ maillage

        void WriteMesh()
        {
            Vector2 centroid = Centroid(position);

            for (int ring = 0; ring < RingCount; ring++)
            {
                bool skirt = ring == RingCount - 1;
                float t = ring / (float)Rings;

                for (int i = 0; i < PointCount; i++)
                {
                    Vector2 point = skirt
                        ? position[i] + SkirtDirection(i, centroid) * SkirtWidth
                        : centroid + (position[i] - centroid) * t;

                    vertices[ring * PointCount + i] = new Vector3(point.x, point.y, 0f);
                }
            }

            mesh.SetVertices(vertices);
        }

        /// <summary>
        /// Direction du débord en un point du contour.
        ///
        /// La normale suffit tant que le contour reste lisse. Dans un pli serré elle bascule
        /// vers l'intérieur, et la jupe — qui porte la fin transparente de la texture — vient
        /// alors mordre le corps. Le rayon depuis le centre de gravité, lui, sort toujours :
        /// on s'y rabat dès que la normale n'est plus fiable.
        /// </summary>
        Vector2 SkirtDirection(int i, Vector2 centroid)
        {
            Vector2 normal = OutwardNormal(position, i);
            Vector2 radial = position[i] - centroid;

            float length = radial.magnitude;
            if (length < 1e-4f) return normal;

            radial /= length;
            return Vector2.Dot(normal, radial) > 0.35f ? normal : radial;
        }

        /// <summary>
        /// Bandes de quadrilatères entre anneaux successifs. L'anneau du centre est réduit à un
        /// point : sa bande est plate, ce qui ne coûte rien et évite un cas particulier.
        /// </summary>
        static int[] BuildTriangles()
        {
            var triangles = new int[(RingCount - 1) * PointCount * 6];
            int cursor = 0;

            for (int ring = 0; ring < RingCount - 1; ring++)
            {
                int inner = ring * PointCount;
                int outer = (ring + 1) * PointCount;

                for (int i = 0; i < PointCount; i++)
                {
                    int j = i == PointCount - 1 ? 0 : i + 1;

                    triangles[cursor++] = inner + i;
                    triangles[cursor++] = outer + i;
                    triangles[cursor++] = inner + j;

                    triangles[cursor++] = inner + j;
                    triangles[cursor++] = outer + i;
                    triangles[cursor++] = outer + j;
                }
            }

            return triangles;
        }

        // ------------------------------------------------------------------ géométrie

        static Vector2 Centroid(Vector2[] points)
        {
            var sum = Vector2.zero;
            for (int i = 0; i < points.Length; i++) sum += points[i];
            return sum / points.Length;
        }

        /// <summary>Aire du polygone fermé. Positive tant que le tour reste dans le sens direct.</summary>
        static float SignedArea(Vector2[] points)
        {
            float area = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i == points.Length - 1 ? 0 : i + 1];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        /// <summary>Normale sortante en un point, prise perpendiculairement à la corde de ses voisins.</summary>
        static Vector2 OutwardNormal(Vector2[] points, int i)
        {
            Vector2 previous = points[i == 0 ? points.Length - 1 : i - 1];
            Vector2 next = points[i == points.Length - 1 ? 0 : i + 1];

            Vector2 tangent = next - previous;
            var normal = new Vector2(tangent.y, -tangent.x);

            float length = normal.magnitude;
            return length > 1e-5f ? normal / length : Vector2.zero;
        }
    }
}
