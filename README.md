# Smily Volley

Clone de **Blobby Volley** sous Unity 6 (6000.5.6f1), en 2D, rendu **URP / Renderer 2D**,
entrées via le package **Input System** (le Built-in Render Pipeline et l'ancien Input
Manager sont tous deux dépréciés depuis Unity 6).

Deux blobs, un filet, trois touches par joueur. Toute la profondeur vient de l'endroit
où la balle touche le blob.

> Le détail du design — mécanique de frappe, réglages, IA, feuille de route — vit dans
> **[docs/GDD.md](docs/GDD.md)**. Ce fichier-ci explique comment ouvrir, jouer et compiler.

---

## Ouvrir le projet

1. Unity Hub → *Add* → `C:\CODE\JEUX\Smily-Volley`
2. Ouvrir la scène `Assets/Scenes/Game.unity`
3. Play

Aucune dépendance externe : tous les sprites sont générés par code au premier build de
scène. Le projet se clone et se lance tel quel.

## Menu et options

Le jeu s'ouvre sur un menu ; « Jouer » y est déjà sélectionné, une frappe suffit à
lancer une partie. `Échap` met en pause à tout moment.

La navigation est au clavier — `↑ ↓` naviguer, `← →` régler, `Entrée` valider,
`Échap` revenir — la souris fonctionne aussi. L'appui prolongé répète.

| Section | Réglages |
|---|---|
| **Commandes** | Les six touches, réaffectables une à une, plus un retour à l'origine |
| **Adversaire** | Ordinateur ou humain ; difficulté de Tranquille à Implacable |
| **Règles** | Points pour gagner, écart de deux, touches par camp, comptage, camp qui engage |
| **Son** | Musique et effets |
| **Affichage** | Plein écran |

Tout est conservé d'une partie à l'autre. Pour repartir de zéro, « Tout remettre par
défaut » dans les options — ou supprimer la clé de registre
`HKCU\Software\Smily\Smily Volley`.

## Commandes

| Action | Joueur 1 (AZERTY) | Joueur 1 (QWERTY) | Joueur 2 |
|---|---|---|---|
| Se déplacer | `Q` / `D` | `A` / `D` | `←` / `→` |
| Sauter | `Z` ou `Espace` | `W` ou `Espace` | `↑` |

Ce sont les touches d'origine : elles se réaffectent toutes dans les options.

- `Tab` : basculer entre « contre l'ordinateur » et « 2 joueurs sur le même clavier »
- `R` : relancer le match
- `Échap` : menu de pause

> **À savoir sur les touches.** L'énumération `Key` du package Input System identifie les
> touches par leur **position physique** sur un clavier QWERTY, jamais par le caractère
> imprimé dessus. `Key.A` désigne donc la touche marquée `Q` d'un clavier AZERTY. Le code
> utilise `A` / `D` / `W`, ce qui tombe sur `Q` / `D` / `Z` en AZERTY : les mêmes touches
> sous les doigts dans les deux dispositions. Corollaire : les lettres dont la position
> diffère entre les deux dispositions (`M`, `A`, `Q`, `Z`, `W`) sont à éviter pour les
> raccourcis globaux — d'où `Tab` pour le changement de mode.
>
> L'aide affichée en bas de l'écran n'est pas codée en dur : `HumanBlobInput.LabelOf` lit
> le caractère réellement imprimé sur la touche dans la disposition active du système. Le
> bandeau annonce donc `Q / D` sur un clavier français et `A / D` sur un clavier anglais.

## Règles implémentées

- La balle qui touche le sol donne le point au camp opposé.
- **Aucune limite de touches** par camp. La règle existe dans le code : passer
  `Max Touches Per Side` à 3 sur le `GameManager` la réactive.
- Match en **15 points** avec 2 points d'écart.
- **Le camp qui perd le point engage** (décochable via `Serve Goes To Loser`).
- **On ne joue que lorsque la balle est en jeu.** À l'instant du point, la balle sort de
  la simulation et se replace au-dessus du camp qui va engager, et les blobs cessent de
  répondre aux commandes. Rien ne bouge pendant le message de point ni pendant le
  service : les deux camps retrouvent la main à l'image exacte où la balle part.
- Service automatique après 1,1 s, au-dessus du joueur qui engage.
- Comptage *rally point* par défaut ; cocher `Side Out Scoring` sur le `GameManager`
  pour revenir au comptage historique (seul le serveur marque).

## Physique

Le contrôle « sec » de Blobby Volley vient de deux choix :

- Les blobs sont des `Rigidbody2D` **kinematic** dont la gravité et le saut sont calculés
  à la main (`BlobController`). La balle ne les pousse jamais.
- La frappe n'est pas un rebond physique : la balle repart **radialement depuis le centre
  du blob** à vitesse constante (`BallController.ApplyBlobHit`). Frapper avec le côté du
  blob permet de viser, le percuter par au-dessus permet de smasher.

Les rebonds sur les murs, le filet et le sol restent gérés par la physique 2D
(`Assets/Art/Bouncy.physicsMaterial2D` et `Sand.physicsMaterial2D`).

> **Pourquoi un angle minimal de renvoi.** La vitesse de renvoi est imposée, pas
> conservée : rien ne s'amortit d'une frappe à l'autre. Une balle qui retombe pile sur le
> sommet d'un blob immobile repart donc exactement à la verticale, retombe au même
> endroit et repart à l'identique — sans fin. Le service posait précisément la balle
> au-dessus du blob, ce qui rendait l'égalité exacte : un joueur qui ne touchait à rien
> bloquait le match. Deux garde-fous : le service est décalé de 0,4 unité vers le filet
> (`Serve Offset X`), et tout renvoi vers le haut est écarté d'au moins 12° de la
> verticale (`Min Vertical Angle`). Les chandelles restent hautes — à 12°, la composante
> verticale vaut encore 98 % — et le smash, qui renvoie vers le bas, n'est pas concerné.

Deux murs invisibles évitent les temps morts :

- `ScreenCeiling` colle un collider sur le bord **haut** du champ visible et le repositionne
  quand le cadrage change. Sans lui, une balle bien frappée sort de l'écran et le joueur
  attend plusieurs secondes sans rien voir avant qu'elle ne retombe.
- Le sommet du filet est coiffé d'un `CircleCollider2D` : un sommet plat laisserait la balle
  s'y poser en équilibre et figerait l'échange.

## Sons et particules

Chaque impact a un retour sonore et visuel :

| Événement | Son | Particules |
|---|---|---|
| Balle frappée par un blob | Impact mat | Éclat jaune pâle |
| Rebond mur / filet / plafond | Tap léger, volume selon la force | Étincelle blanche |
| Balle sur le sable | Impact sourd | Gerbe de sable |
| Blob qui saute | Pas dans le sable, discret et plus aigu | — |
| Blob qui atterrit | Pas dans le sable, selon la vitesse de chute | Poussière proportionnelle |
| Point marqué | Cloche | — |
| Fin de match | Arpège de cloches | — |

Une **musique de fond** tourne en boucle : *Feel Good Island Loop* de **Brandon Morris**
([OpenGameArt](https://opengameart.org/content/feel-good-island-loop)), en **CC0**. Elle
est jouée à 0,25 de volume, soit une douzaine de décibels sous les frappes — le morceau
a un niveau proche de celui des effets, le baisser est ce qui le place derrière l'action.
Démarrage en fondu de 1,5 s.

Les effets viennent du pack **Impact Sounds** de [Kenney](https://kenney.nl/assets/impact-sounds),
également en **CC0** — domaine public, aucune attribution exigée dans les deux cas (elle
est faite ici de bon gré). Détail fichier par fichier dans
[`Assets/Audio/Kenney/SOURCE.md`](Assets/Audio/Kenney/SOURCE.md) et
[`Assets/Audio/Music/SOURCE.md`](Assets/Audio/Music/SOURCE.md).

Cinq variantes par événement, tirées au hasard et repitchées de ±12 % : c'est la
répétition à l'identique que l'oreille repère, pas le son. Sans cette variation, un
échange un peu long devient un cliquetis mécanique.

> **Si vous ajoutez des particules.** Elles doivent utiliser le shader
> `Universal Render Pipeline/Particles/Unlit`, jamais un shader de sprite. Un
> `ParticleSystemRenderer` portant `2D/Sprite-Unlit-Default` n'est pas dessiné du tout —
> les particules existent, vivent et se déclarent visibles, mais rien n'apparaît à
> l'écran. En contrepartie, le shader particules démarre opaque : surface, mélange,
> ZWrite, mot-clé et file de rendu se règlent à la main dans
> `SceneBuilder.CreateParticleMaterial`.

## Structure

```
Assets/
├── Art/                 Sprites générés par code + matériaux physiques
├── Audio/Kenney/        Effets CC0 + licence et provenance
├── Audio/Music/         Musique CC0 + licence et provenance
├── Settings/            Pipeline URP, Renderer 2D, volume profile par défaut
├── Editor/              → assembly SmilyVolley.Editor (exclue du build)
│   ├── PlaceholderArt.cs      Dessine les PNG (blobs, balle, filet, ciel, ombre)
│   ├── SceneBuilder.cs        Assemble toute la scène de jeu
│   ├── RenderPipelineSetup.cs Active URP sur tous les niveaux de qualité
│   └── BuildTools.cs          Build Windows + réglages projet
├── Scenes/Game.unity
└── Scripts/             → assembly SmilyVolley
    ├── Core/            GameManager, GameSettings, CameraFitter, Side
    ├── Gameplay/        BlobController, BallController, IA, entrées, ombre, particules
    ├── Audio/           GameAudio
    └── UI/              HudController, MenuController, MenuRow
docs/
└── GDD.md               Game Design Document
```

Le code est réparti en **deux assemblies** (`SmilyVolley.asmdef`, `SmilyVolley.Editor.asmdef`) :
les outils d'édition ne peuvent pas fuir dans le build, et modifier un outil ne force pas
la recompilation du jeu.

## Menu Unity « Smily Volley »

- **Construire la scène de jeu** — régénère `Game.unity` de zéro (toute modification manuelle
  de la scène est perdue). Les dimensions du terrain sont les constantes en tête de `SceneBuilder`.
- **Régénérer les sprites** — redessine les PNG de `Assets/Art`.
- **Compiler le build Windows** — produit `Build/Windows/SmilyVolley.exe`.
- **Activer le pipeline URP** — réassigne `Assets/Settings/UniversalRP.asset` sur tous les
  niveaux de qualité. Unity range le pipeline actif dans `QualitySettings` niveau par niveau :
  ne renseigner que le pipeline par défaut de `GraphicsSettings` laisserait les autres en Built-in.

## Compiler sans ouvrir l'éditeur

Tout le pipeline (URP + scène + build) se pilote en ligne de commande :

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "C:\CODE\JEUX\Smily-Volley" `
  -executeMethod SmilyVolley.EditorTools.BuildTools.RebuildEverything `
  -logFile "$env:TEMP\smilyvolley-build.log"
```

> La commande **échoue si l'éditeur Unity est ouvert** sur le projet (« another Unity
> instance is running »). Vérifier `Temp\UnityLockfile` avant de lancer.

Le binaire produit se lance en fenêtré :

```powershell
.\Build\Windows\SmilyVolley.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0
```

## Rendu

Le Renderer 2D d'URP applique aux sprites le matériau `Sprite-Lit-Default` : **sans lumière
dans la scène, tous les sprites seraient noirs**. `SceneBuilder` place donc une `Light2D`
globale blanche d'intensité 1, qui reproduit exactement l'aspect non éclairé tout en laissant
la porte ouverte aux effets d'éclairage 2D (halo sur la balle, ombres portées, ambiance).

## Réglages utiles (Inspector)

| Objet | Champ | Effet |
|---|---|---|
| `GameManager` | `Right Player Is Ai` | Mode 1 joueur / 2 joueurs au démarrage |
| `GameManager` | `Ai Difficulty` | 0 = lent et imprécis, 1 = réflexes immédiats |
| `GameManager` | `Points To Win` | Longueur du match |
| `GameManager` | `Max Touches Per Side` | 0 = illimité ; 3 = règle volley classique |
| `GameManager` | `Serve Goes To Loser` | Décoché : le gagnant du point engage |
| `GameManager` | `Serve Offset X` | Décalage de la balle vers le filet au service |
| `Ball` | `Hit Speed`, `Blob Velocity Influence` | Nervosité des échanges |
| `Ball` | `Min Vertical Angle` | Écart minimal du renvoi avec la verticale (0 = échanges bloquables) |
| `Ball` (Rigidbody2D) | `Gravity Scale` | Balle flottante ou lourde |
| `BlobLeft` / `BlobRight` | `Move Speed`, `Jump Speed`, `Gravity` | Sensation de déplacement |
| `Audio` | Volumes par événement, `Pitch Jitter` | Équilibre et variété du mixage |
| `Audio` | `Music Volume`, `Music Fade In Seconds` | Présence de la musique |
| `Audio` | `Jump Volume`, `Jump Pitch` | Discrétion de l'appui du saut |
| `ImpactEffects` | Nombre de particules par effet | Densité des bouffées |

## Notes de performance

Le jeu est léger, mais le code évite les schémas qui coûtent cher dès qu'un projet grossit :

- La nature de chaque collider rencontré par la balle est résolue **une fois** puis
  mémorisée — `OnCollisionStay2D` se déclenche à chaque pas de physique, y refaire un
  `GetComponentInParent` remonterait la hiérarchie 50 fois par seconde et par contact.
- Les `KeyControl` de l'Input System sont résolus au changement de périphérique, pas à
  chaque image.
- Ombres, cadrage caméra, plafond et écrasement des blobs n'écrivent dans leur `Transform`
  que lorsque la valeur change réellement.
- Aucune allocation par image : tampon de liste réutilisé pour la sélection des entrées,
  table de chaînes pré-calculée pour le score.
- Le HUD désactive sa propre boucle `Update` hors message temporisé.

## Pistes pour la suite

- Menu principal, sélection de mode et écran d'options
- Effet de rotation sur la balle influençant la trajectoire
- Sprites définitifs en remplacement des PNG générés
- Difficultés d'IA nommées, mode tournoi

La feuille de route complète et les risques identifiés sont dans [docs/GDD.md](docs/GDD.md).
