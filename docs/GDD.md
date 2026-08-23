# Smily Volley — Game Design Document

| | |
|---|---|
| **Titre** | Smily Volley |
| **Genre** | Sport / arcade, duel 1 contre 1 |
| **Plateforme** | Windows (PC standalone) |
| **Moteur** | Unity 6 — 6000.5.6f1, URP / Renderer 2D, Input System |
| **Joueurs** | 1 (contre l'IA) ou 2 (même clavier) |
| **Durée d'une partie** | 3 à 6 minutes |
| **Public** | Joueurs occasionnels, sessions courtes, jeu à deux sur un même poste |
| **Référence** | *Blobby Volley* (Daniel Skoraszewsky, 2000) |
| **Version du document** | 1.0 — 23 août 2026 |

---

## 1. Vision

Smily Volley est un volley-ball de plage à deux blobs. Chaque camp est un demi-cercle
souriant qui ne sait faire que trois choses : aller à gauche, aller à droite, sauter.
Toute la profondeur vient de **où** la balle touche le blob, pas d'un arsenal de coups.

> **Pitch en une phrase.** Deux touches de direction, une touche de saut, et pourtant
> on apprend à smasher.

### Ce que le jeu doit être

- **Lisible en trois secondes.** Aucun tutoriel : la forme des blobs et la trajectoire
  de la balle disent tout.
- **Immédiat.** Depuis le lancement, on joue en moins de deux secondes. Pas de menu
  entre le joueur et l'échange.
- **Nerveux.** Le contrôle est sec : pas d'inertie, pas de glissade, pas de latence
  entre l'appui et le déplacement.
- **Injuste seulement contre soi-même.** Une balle perdue est toujours un mauvais
  placement, jamais un aléa du moteur.

### Ce que le jeu ne doit pas être

- Une simulation de volley (pas de passe/attaque/contre codifiés).
- Un jeu à progression, déblocages ou monnaie.
- Un jeu en ligne. Le duel se joue côte à côte, sur le même clavier.

---

## 2. Piliers de design

### 2.1 La frappe radiale

C'est la mécanique centrale, et c'est le seul endroit où le jeu s'écarte de la physique.

Quand la balle touche un blob, elle **ne rebondit pas** : elle est relancée
**radialement depuis le centre du blob, à vitesse constante**. La direction de départ
est donc uniquement le vecteur centre-du-blob → balle.

```
        ↑                        ↖         ↑         ↗
        │                          ╲       │       ╱
       ( )   frappe au sommet       ( )  ( )  ( )     frappe sur le flanc
      /   \  → balle verticale     ╱       │       ╲  → balle rasante et dirigée
```

Conséquences de design, toutes voulues :

| Situation | Résultat |
|---|---|
| Balle au-dessus du centre | Renvoi quasi vertical, sans danger |
| Balle sur le flanc du blob | Renvoi rasant, dirigé vers ce flanc |
| Blob qui percute la balle par-dessus en retombant | **Smash** : la balle repart vers le bas |
| Blob qui monte sous la balle | Chandelle haute, gain de temps |

Viser, c'est donc **se placer**. Le joueur ne choisit jamais une direction de tir : il
choisit un point de contact. Un débutant renvoie ; un joueur habitué décale son blob
de quelques dizaines de centimètres et place la balle.

Une part de la vitesse du blob est ajoutée au renvoi (32 %), ce qui récompense le
mouvement au moment de l'impact sans jamais dominer la composante radiale.

#### L'angle minimal de renvoi

La vitesse de renvoi étant **imposée** et non conservée, rien ne s'amortit d'une frappe
à l'autre. Le renvoi radial possède donc un point fixe : une balle qui retombe
exactement sur le sommet d'un blob immobile repart exactement à la verticale, retombe
au même endroit, et repart à l'identique — indéfiniment.

Ce n'était pas un cas limite théorique : le service posait la balle pile au-dessus du
blob, l'égalité était donc exacte, et un joueur qui ne touchait à rien voyait la balle
osciller sans fin. Mesuré : **50 s sans qu'un seul point ne soit marqué.**

Deux garde-fous, complémentaires :

| Mesure | Rôle |
|---|---|
| **Service décalé de 0,4 unité vers le filet** | Le cas nominal n'atteint plus l'équilibre : la balle attaque le flanc du blob et repart d'elle-même à ~17° |
| **Angle minimal de 12° avec la verticale** | Filet de sécurité : aucun réglage ne peut recréer la boucle, où qu'elle survienne dans l'échange |

L'inclinaison conserve le côté déjà pris par la balle et ne tranche vers le camp adverse
que sur une frappe rigoureusement centrée. À 12°, la composante verticale vaut encore
98 % de la vitesse de renvoi : les chandelles restent hautes. Le smash n'est jamais
concerné, puisqu'il renvoie la balle vers le bas.

### 2.2 Le contrôle sec

Les blobs sont des `Rigidbody2D` **kinematic** dont la gravité, le saut et les butées
sont calculés à la main. Ils ne sont jamais poussés par la balle, ne glissent pas et
ne conservent aucune inertie horizontale : relâcher la touche arrête le blob **dans
l'image**.

C'est un choix de sensation, pas de facilité technique : un blob à physique dynamique
serait bousculé par chaque impact et le joueur perdrait la maîtrise de son placement.

### 2.3 Le terrain sans temps mort

Toute seconde où la balle est hors de vue est une seconde perdue.

- Un **plafond invisible** est collé au bord haut du champ visible et suit le cadrage.
  Sans lui, un smash mal dosé envoie la balle hors écran et le joueur attend, immobile,
  qu'elle redescende.
- Le sommet du filet est un **disque**, pas une arête plate : une balle ne peut pas s'y
  poser en équilibre et figer l'échange.
- Le service est **automatique** après 1,1 s : personne n'attend que l'autre appuie.

---

## 3. Boucle de jeu

```
   ┌──────────────────────────────────────────────────────────────┐
   │                                                              │
   ▼                                                              │
Service ──1,1 s──▶ Échange ──balle au sol──▶ Point ──1,8 s──▶ ────┘
(balle figée      (rally libre,            (la balle s'arrête net
 au-dessus du      aucune limite            et se replace côté
 serveur)          de touches)              perdant : score, message)
                                                  │
                                       15 pts et 2 d'écart
                                                  ▼
                                            Fin de match
                                          (R pour rejouer)
```

La machine à états est portée par `GameManager` : `Serving → Rally → PointScored → Serving`,
avec `MatchOver` comme sortie.

**L'échange se termine à l'instant du point.** Dès que le point est attribué, la balle
sort de la simulation et se replace au-dessus du camp qui va engager — celui qui vient
de perdre. Elle ne rebondit pas pendant la pause : le joueur voit immédiatement qui
récupère le service, et une balle encore en mouvement ne peut pas retoucher le sol et
brouiller la lecture du point qui vient d'être marqué.

### Durée d'une séquence

| Phase | Durée | Intention |
|---|---|---|
| Service | 1,1 s | Laisser le temps de se replacer, sans casser le rythme |
| Échange | variable | Le cœur du jeu |
| Point marqué | 1,8 s | Lire le score et le message, souffler — balle immobile |

---

## 4. Règles

### 4.1 Marquer

La balle qui **touche le sol** donne le point au camp opposé. Le côté du terrain est
déterminé par l'abscisse du point d'impact : `x < 0` = camp gauche.

Comptage **rally point** par défaut : chaque échange donne un point, quel que soit le
serveur. Le comptage historique de Blobby Volley (*side out* : seul le serveur marque)
reste disponible en cochant `Side Out Scoring`, mais il rallonge beaucoup les parties.

### 4.2 Gagner

Match en **15 points** avec **2 points d'écart** obligatoires. Sans l'écart de deux,
un match serré se finirait sur un coup de chance.

### 4.3 Le service

Le camp qui **vient de perdre** le point engage. C'est l'inverse du volley réel, et
c'est délibéré : en rally point, laisser le service au gagnant crée des séries
écrasantes contre un joueur en difficulté.

> Cette règle est neutralisée automatiquement si `Side Out Scoring` est actif : dans
> ce mode, seul le serveur marque, donc donner le service au perdant empêcherait le
> score de monter.

La balle rejoint sa position d'engagement — 3,6 unités au-dessus de la **position de
départ** du blob serveur, décalée de 0,4 unité vers le filet — dès l'attribution du
point, et non à la fin de la pause.
Elle y reste immobile pendant toute la pause (1,8 s), puis pendant le délai de service
(1,1 s), avant d'être lâchée sans vitesse initiale. Les deux blobs sont replacés à leur
position de départ au début du service.

> S'appuyer sur la position de départ du blob plutôt que sur sa position courante est
> ce qui permet de placer la balle **avant** que les blobs ne soient replacés : le point
> d'engagement est le même quel que soit l'endroit où le rally s'est terminé.

### 4.4 Le nombre de touches

**Aucune limite** par défaut. Un camp peut jongler autant qu'il veut avant de renvoyer.

C'est un choix d'accessibilité : en 1 contre 1, la règle des trois touches punit
surtout le joueur qui n'a pas encore le contrôle. Le code compte les touches
consécutives par camp et la règle s'active en passant `Max Touches Per Side` à 3.

### 4.5 Les limites du terrain

Chaque blob est confiné à son camp : le filet d'un côté, le mur latéral de l'autre.
Il ne peut ni passer sous le filet ni sortir du terrain. La balle, elle, rebondit
sur les murs latéraux, le filet et le plafond.

---

## 5. Commandes

| Action | Joueur 1 (AZERTY) | Joueur 1 (QWERTY) | Joueur 2 |
|---|---|---|---|
| Gauche / droite | `Q` / `D` | `A` / `D` | `←` / `→` |
| Sauter | `Z` ou `Espace` | `W` ou `Espace` | `↑` |

| Raccourci global | Effet |
|---|---|
| `Tab` | Bascule 1 joueur ↔ 2 joueurs, en cours de partie |
| `R` | Relancer le match |

### Le piège des dispositions clavier

L'énumération `Key` de l'Input System désigne une **position physique sur un clavier
QWERTY**, jamais le caractère imprimé sur la touche. `Key.A` est donc la touche marquée
`Q` sur un clavier français.

Le jeu déclare `A` / `D` / `W`, ce qui tombe sous les mêmes doigts dans les deux
dispositions : `Q` / `D` / `Z` en AZERTY, `A` / `D` / `W` en QWERTY.

Corollaire pour les raccourcis globaux : toute lettre dont la position change entre
les deux dispositions (`A`, `Q`, `Z`, `W`, `M`) est à proscrire. D'où `Tab`, dont la
position est universelle.

L'aide affichée en bas de l'écran **n'est pas écrite en dur** : `HumanBlobInput.LabelOf`
lit le caractère réellement imprimé sur la touche dans la disposition active du système.
Le bandeau annonce `Q / D` sur un clavier français et `A / D` sur un clavier anglais,
sans configuration.

---

## 6. Le terrain

Toutes les dimensions sont en unités monde, sol à `y = -4`. Elles vivent en constantes
en tête de `SceneBuilder` : décor, colliders et scripts en dérivent, ce qui interdit
toute dérive entre ce qu'on voit et ce qui bloque.

```
   x=-8,2                        x=0                        x=+8,2
     │                            ║                            │
     │                            ║  ← filet, hauteur 3,2      │
     │        ( )                 ║                 ( )        │
     │       blob G               ║                blob D      │
  ═══╧════════════════════════════╩════════════════════════════╧═══  y=-4
                              le sable
```

| Élément | Valeur |
|---|---|
| Sol | `y = -4` |
| Murs latéraux (face intérieure) | `x = ±8,2` |
| Filet | hauteur 3,2 ; demi-largeur 0,20 ; sommet arrondi |
| Rayon d'un blob | 1,0 |
| Rayon de la balle | 0,35 |
| Position de départ des blobs | `x = ±4,3` |
| Plafond | bord haut du champ visible, épaisseur 2 |

La caméra est orthographique et **s'adapte au format d'écran** : la largeur visible est
prioritaire (le terrain entier reste cadré quel que soit le ratio), et le bas de l'image
reste calé sur le sable. Sur un écran très large, des bordures sombres masquent
l'extérieur du terrain.

---

## 7. Réglages de jeu

Les valeurs ci-dessous sont le résultat du réglage actuel. Elles sont toutes exposées
dans l'Inspector.

### 7.1 Le blob

| Paramètre | Valeur | Effet ressenti |
|---|---|---|
| Vitesse de déplacement | 6,5 u/s | Traverse son demi-terrain en ~1,1 s |
| Vitesse de saut | 9,7 u/s | Apex à ~3,0 unités, soit le haut du filet |
| Gravité | 15,7 u/s² | Chute plus vive que la montée : saut « lourd », lisible |
| Récupération d'écrasement | 5 /s | Squash & stretch court, sans mollesse |

La gravité du blob (15,7) est volontairement plus forte que celle de la balle (14,7) :
le blob retombe avant elle, ce qui rend le smash atteignable.

### 7.2 La balle

| Paramètre | Valeur | Effet ressenti |
|---|---|---|
| Vitesse de renvoi | 12 u/s | Constante : la puissance ne dépend pas de l'impact |
| Influence de la vitesse du blob | 32 % | Récompense le mouvement sans écraser le placement |
| Vitesse maximale | 20 u/s | Plafond de sécurité, la balle reste suivable à l'œil |
| Échelle de gravité | 1,5 (≈ 14,7 u/s²) | Trajectoires tendues, peu de flottement |
| Rebond (murs, filet, sol) | 0,92 | Presque élastique : les échanges ne s'éteignent pas |
| Délai de re-frappe | 0,05 s | Évite que la balle se colle à un blob qui monte |
| Angle minimal / verticale | 12° | Interdit l'aller-retour vertical sans fin (§ 2.1) |

La rotation de la balle est purement décorative (−28°/unité parcourue) : elle donne un
repère de vitesse sans influencer la trajectoire.

### 7.3 Les repères visuels

Chaque blob et la balle projettent une **ombre au sol** qui rétrécit et pâlit avec la
hauteur (opacité 0,55 → 0,12 sur 7 unités). C'est le seul indicateur de profondeur du
jeu, et il vaut mieux qu'un marqueur explicite : on lit où la balle va retomber sans
quitter la balle des yeux.

---

## 8. L'intelligence artificielle

L'IA ne triche pas : elle lit la même position et la même vitesse de balle que le
joueur, et pilote son blob par les mêmes deux commandes (`Horizontal`, `JumpHeld`).
Elle passe par `BlobInput`, exactement comme le clavier — le `BlobController` ne sait
pas qui le pilote.

### 8.1 Décider où se placer

L'IA résout la trajectoire balistique de la balle :

```
y(t) = y₀ + v_y·t − ½·g·t²  =  hauteur de frappe
```

Elle garde la plus grande racine positive (le moment où la balle **redescend** à hauteur
de frappe), en déduit l'abscisse d'impact, puis **replie** ce résultat sur le terrain
pour tenir compte des rebonds sur les murs latéraux.

Elle ne se place ensuite pas sur le point d'impact, mais **0,45 unité en retrait côté
ligne de fond** : la frappe radiale renvoie alors la balle vers le filet, donc vers le
camp adverse. C'est l'IA appliquant sa propre mécanique de visée.

Si la balle n'est ni destinée à son camp ni déjà de son côté, elle rejoint sa position
d'attente en fond de court plutôt que de coller au filet.

### 8.2 Décider quand sauter

L'IA saute si les quatre conditions sont réunies :

1. la balle est de son côté ;
2. l'écart horizontal est inférieur à 1,7 unité ;
3. la balle est entre 0,25 et 2,6 unités au-dessus de la hauteur de frappe ;
4. la balle **redescend** (`v_y ≤ 1`) — sauter sous une balle qui monte la manquerait.

### 8.3 Le curseur de difficulté

Un seul réglage, `Ai Difficulty` (0 → 1), pilote deux grandeurs :

| Difficulté | Intervalle de décision | Erreur de visée |
|---|---|---|
| 0,0 | 300 ms | ± 1,8 unité |
| 0,65 *(défaut)* | ~125 ms | ± 0,66 unité |
| 1,0 | 30 ms | ± 0,05 unité |

Le **temps de réaction** est ce qui rend une IA facile réellement prenable : elle se
place au bon endroit, mais trop tard. L'erreur de visée, elle, produit des fautes
crédibles plutôt qu'un mur infranchissable.

À 1,0, l'IA est quasi imbattable : c'est assumé, c'est le mode démonstration.

---

## 9. Interface

Volontairement minimale : rien ne doit détourner le regard de la balle.

```
┌──────────────────────────────────────────────────────────┐
│                      0    -    0                         │  score
│               1 joueur — contre l'ordinateur             │  mode
│                                                          │
│                                                          │
│                   Service : Joueur 1                     │  message central
│                                                          │
│        ( )              ║              ( )               │
│  ════════════════════════╩════════════════════════════    │
│  Q / D : se déplacer — Z : sauter — Tab : 2 joueurs      │  aide
└──────────────────────────────────────────────────────────┘
```

| Zone | Contenu | Comportement |
|---|---|---|
| Haut centre | Score, gros chiffres contourés | Toujours visible |
| Sous le score | Mode de jeu courant | Change à l'appui sur `Tab` |
| Centre | Service, point marqué, fin de match | Effacé au lâcher de balle |
| Bas | Rappel des commandes, encre sombre sur le sable | Recomposé selon la disposition clavier et le mode |

Aucun menu, aucun bouton : le mode se change en jeu, le match se relance en jeu.

---

## 10. Direction artistique

**Style** : aplats vectoriels, contours francs, palette de plage en plein soleil.
Toute la lisibilité repose sur le contraste de teinte entre les deux camps.

| Élément | Couleur | Rôle |
|---|---|---|
| Blob gauche | vert | Joueur 1 |
| Blob droit | rouge-orangé | Joueur 2 / IA |
| Balle | jaune à quartiers blancs | Contraste maximal sur le ciel, rotation lisible |
| Ciel | dégradé bleu | Fond neutre, ne concurrence rien |
| Sable | beige `#EDD194` | Sépare la zone de jeu du bandeau d'aide |
| Bordures | bleu nuit `#212936` | Masquent l'extérieur du terrain sur écrans larges |

Les visages — deux yeux, un sourire — n'ont aucune fonction mécanique. Ils donnent au
jeu son nom et sa bonne humeur.

**État actuel** : tous les sprites sont **générés par code** (`PlaceholderArt`), sans
aucun asset externe. Le projet se clone et se lance sans dépendance. Ils sont pensés
pour être remplacés : il suffit d'écraser les PNG de `Assets/Art`.

**Éclairage** : le Renderer 2D d'URP applique aux sprites le matériau `Sprite-Lit-Default`.
Sans lumière dans la scène, tous les sprites seraient rendus **noirs**. Une `Light2D`
globale blanche d'intensité 1 reproduit l'aspect non éclairé, tout en laissant la porte
ouverte à l'éclairage 2D (halo sur la balle, ambiance de fin de journée).

**Particules** : trois bouffées, une par nature d'impact, émises au point de contact
exact. Elles sont le retour immédiat qui manquait : avant elles, une frappe et un
effleurement se ressemblaient.

| Effet | Déclencheur | Aspect |
|---|---|---|
| Éclat | Balle frappée par un blob | Bouffée jaune pâle, 10 grains, 0,32 s |
| Gerbe de sable | Balle qui retombe, blob qui atterrit | Grains couleur sable projetés vers le haut qui retombent, 0,60 s |
| Étincelle | Mur, filet, plafond | Six grains blancs, 0,26 s |

Les grains d'un blob qui atterrit sont proportionnels à sa vitesse de chute : un
petit saut soulève à peine de la poussière.

> **Piège de rendu.** Les particules doivent utiliser le shader
> `Universal Render Pipeline/Particles/Unlit`, jamais un shader de sprite. Un
> `ParticleSystemRenderer` portant `2D/Sprite-Unlit-Default` n'est tout simplement pas
> dessiné : les particules existent, vivent, se déclarent visibles — et rien n'apparaît,
> même grossies à cent pixels. Le shader particules démarre en revanche *opaque* : il
> faut lui régler à la main surface, mélange, ZWrite, mot-clé et file de rendu, ce que
> l'inspecteur ferait sinon. Tout est fait dans `SceneBuilder.CreateParticleMaterial`.

## Audio

**Sons** : extraits du pack *Impact Sounds* de [Kenney](https://kenney.nl/assets/impact-sounds),
en **CC0** (domaine public, aucune attribution exigée). Provenance et correspondance
fichier par fichier dans `Assets/Audio/Kenney/SOURCE.md`.

| Événement | Son | Volume |
|---|---|---|
| Frappe sur un blob | `impactSoft_medium` | 0,80 |
| Rebond mur / filet / plafond | `impactPlate_light` | 0,45 × force de l'impact |
| Balle sur le sable | `impactSoft_heavy` | 0,70 |
| Atterrissage d'un blob | `footstep_snow` | 0,22 × vitesse de chute |
| Point marqué | `impactBell_heavy` | 0,65 |
| Fin de match | `impactBell_heavy`, arpège 0-4-7-12 demi-tons | 0,75 |

Trois principes :

- **Cinq variantes par événement**, tirées au hasard, plus une variation de hauteur de
  ±12 %. C'est la répétition à l'identique que l'oreille repère, pas le son lui-même :
  sans cela un échange un peu long dégénère en cliquetis mécanique.
- **Le volume porte l'information.** Un frôlement contre le filet et un boulet contre le
  mur ne sonnent pas pareil ; un blob qui retombe d'un petit saut est presque muet.
- **Mixage entièrement 2D.** Le terrain tient dans l'écran : spatialiser ne ferait que
  déséquilibrer le casque au détriment du joueur de gauche.

La fanfare de fin de match n'est pas un fichier : le pack n'en fournit pas, alors
`GameAudio` rejoue la cloche du point sur une montée d'accord parfait, en repitchant
d'un rapport 2^(n/12) par demi-ton.

---

## 11. Architecture technique

### 11.1 Découpage

```
SmilyVolley (assembly runtime)
├── Core/
│   ├── GameManager      Machine à états du match, score, service, règles
│   ├── CameraFitter     Cadrage adaptatif au format d'écran
│   └── Side             Camp du terrain (la valeur enum est le signe sur X)
├── Gameplay/
│   ├── BlobController   Déplacement manuel, saut, butées, écrasement
│   ├── BlobInput        Abstraction des commandes (clavier ou IA)
│   ├── HumanBlobInput   Clavier via Input System
│   ├── AiBlobInput      Prédiction balistique et placement
│   ├── BallController   Frappe radiale, plafond de vitesse, événements
│   ├── GroundShadow     Ombre projetée, indicateur de hauteur
│   ├── GroundSurface    Marqueur du collider de sol
│   ├── ImpactEffects    Bouffées de particules aux points de contact
│   └── ScreenCeiling    Mur invisible calé sur le haut de l'écran
├── Audio/
│   └── GameAudio        Sons du match, pool de voix, variation de hauteur
└── UI/
    └── HudController    Score, messages, aide

SmilyVolley.Editor (assembly éditeur, exclu du build)
├── SceneBuilder         Assemble la scène complète
├── PlaceholderArt       Dessine les PNG
├── RenderPipelineSetup  Active URP sur tous les niveaux de qualité
└── BuildTools           Build Windows et réglages projet
```

### 11.2 Décisions structurantes

**Le `BlobController` ignore qui le pilote.** Il ne connaît que l'abstraction
`BlobInput`. Basculer humain ↔ IA revient à activer l'un ou l'autre composant sur le
même GameObject. Aucune branche `if (isAi)` n'existe dans le code de déplacement.

**La scène est générée par script.** `SceneBuilder` reconstruit `Game.unity` entièrement
depuis les constantes de terrain. C'est ce qui garantit que le collider du filet, le
sprite du filet et les butées de déplacement des blobs parlent des mêmes coordonnées.
La scène produite reste une scène Unity ordinaire, éditable ensuite à la main — mais
toute modification manuelle est perdue à la reconstruction.

**Le `GameManager` écoute, il ne sonde pas.** `BallController` expose deux événements
(`BlobHit`, `GroundHit`) ; l'arbitrage s'y abonne. La balle ignore l'existence du score.

**Deux assemblies.** Le code éditeur est isolé dans son propre assembly : il ne peut pas
fuir dans le build, et une modification d'outil ne force pas la recompilation du jeu.

### 11.3 Choix de performance

Le jeu est trivialement léger, mais quelques réflexes sont appliqués parce qu'ils
coûtent une ligne et évitent des habitudes coûteuses à plus grande échelle :

| Point | Traitement |
|---|---|
| `OnCollisionStay2D` | La nature de chaque collider (blob / sol) est résolue une fois puis mémorisée, au lieu d'un `GetComponentInParent` à chaque pas de physique |
| Lecture du clavier | Les `KeyControl` sont résolus au changement de périphérique, pas à chaque image |
| Écritures de `Transform` | Ombres, cadrage caméra, plafond et écrasement n'écrivent que sur changement réel |
| Allocations | Aucune allocation par image : tampon de liste réutilisé, table de chaînes pour le score |
| Boucles inutiles | Le HUD se désactive lui-même hors message temporisé |

---

## 12. Réglages exposés

| Objet | Champ | Effet |
|---|---|---|
| `GameManager` | `Right Player Is Ai` | Mode 1 joueur / 2 joueurs au démarrage |
| `GameManager` | `Ai Difficulty` | 0 = lent et imprécis, 1 = réflexes immédiats |
| `GameManager` | `Points To Win` | Longueur du match |
| `GameManager` | `Require Two Point Lead` | Écart de 2 obligatoire pour conclure |
| `GameManager` | `Max Touches Per Side` | 0 = illimité ; 3 = règle volley classique |
| `GameManager` | `Side Out Scoring` | Comptage historique : seul le serveur marque |
| `GameManager` | `Serve Goes To Loser` | Décoché : le gagnant du point engage |
| `GameManager` | `Serve Delay`, `Point Pause` | Rythme entre les échanges |
| `GameManager` | `Serve Offset X` | Décalage de la balle vers le filet au service ; 0 = pile au-dessus du blob |
| `Ball` | `Hit Speed`, `Blob Velocity Influence` | Nervosité des échanges |
| `Ball` | `Min Vertical Angle` | Écart minimal du renvoi avec la verticale ; 0 = échanges bloquables |
| `Ball` *(Rigidbody2D)* | `Gravity Scale` | Balle flottante ou lourde |
| `BlobLeft` / `BlobRight` | `Move Speed`, `Jump Speed`, `Gravity` | Sensation de déplacement |
| `BlobRight` *(AiBlobInput)* | `Aim Offset`, `Jump Reach` | Style de jeu de l'IA |
| `Audio` *(GameAudio)* | Volumes par événement | Équilibre du mixage |
| `Audio` *(GameAudio)* | `Pitch Jitter` | Variation de hauteur ; 0 = répétition mécanique |
| `Audio` *(GameAudio)* | `Voice Count` | Sons pouvant se superposer |
| `Audio` *(GameAudio)* | `Victory Semitones` | Notes de la fanfare de fin de match |
| `ImpactEffects` | `Hit / Ball Land / Blob Land / Bounce Particles` | Densité des bouffées |

---

## 13. Feuille de route

### Court terme — le jeu qu'il manque

- ~~**Sons.**~~ Fait : frappe, rebonds, point, fin de match (§ 10).
- ~~**Particules d'impact.**~~ Fait : éclat, gerbe de sable, étincelle (§ 10).
- **Musique de fond** discrète, et un son de saut — les deux manquent encore.
- **Menu principal** : choix du mode, difficulté nommée (Tranquille / Normal / Redoutable),
  score cible, options d'écran.

### Moyen terme — profondeur

- **Effet sur la balle** : la vitesse tangentielle du blob induirait une rotation qui
  courberait la trajectoire. C'est l'ajout le plus riche possible sans nouvelle touche.
- **Écran de fin de match** avec statistiques : plus long échange, smashs réussis.
- **Sprites définitifs** en remplacement des PNG générés, et animations d'idle.

### Long terme — s'il y a une suite

- Mode tournoi contre une échelle d'IA.
- Personnages aux réglages distincts (un blob plus rapide mais moins haut).
- Manette (l'abstraction `BlobInput` est déjà prête ; seule la source change).
- Portage mobile : deux zones tactiles par camp, la caméra adaptative est déjà en place.

### Hors périmètre

- Multijoueur en ligne.
- Progression, déblocages, monétisation.
- Modes à plus de deux blobs — le terrain et les règles de camp n'y survivraient pas.

---

## 14. Risques connus

| Risque | Impact | Traitement |
|---|---|---|
| Échange qui s'éternise sans limite de touches | Partie qui traîne | La règle des 3 touches est codée, activable en un champ |
| IA à difficulté 1 imbattable | Frustration | Défaut à 0,65 ; à exposer dans un menu de difficulté |
| ~~Balle qui rebondit indéfiniment sur un blob~~ | ~~Le match se bloque~~ | **Résolu** : service décalé de 0,4 unité et angle minimal de 12° (§ 2.1). Mesuré sur le jeu livré à lui-même : 0 point en 50 s avant, 7 après |
| Sprites générés par code | Aspect provisoire | Assumé ; remplaçables sans toucher au code |
| Dispositions clavier exotiques | Aide à l'écran trompeuse | `LabelOf` lit la disposition système, pas de valeur en dur |

---

## 15. Glossaire

| Terme | Définition |
|---|---|
| **Blob** | Le personnage : un demi-disque souriant, un par camp |
| **Frappe radiale** | Renvoi de la balle le long de l'axe centre-du-blob → balle, à vitesse constante |
| **Rally / échange** | Phase où la balle est en jeu entre le service et le point |
| **Rally point** | Comptage où chaque échange donne un point, quel que soit le serveur |
| **Side out** | Comptage historique où seul le camp au service peut marquer |
| **Squash & stretch** | Déformation du sprite à l'impulsion et à l'atterrissage |
| **Chandelle** | Renvoi très haut, joué pour gagner du temps de replacement |
| **CC0** | Renonciation au droit d'auteur : usage libre, y compris commercial, sans attribution |
| **Bouffée** | Émission ponctuelle de particules déclenchée par un impact |
