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

## Commandes

| Action | Joueur 1 (AZERTY) | Joueur 1 (QWERTY) | Joueur 2 |
|---|---|---|---|
| Se déplacer | `Q` / `D` | `A` / `D` | `←` / `→` |
| Sauter | `Z` ou `Espace` | `W` ou `Espace` | `↑` |

- `Tab` : basculer entre « contre l'ordinateur » et « 2 joueurs sur le même clavier »
- `R` : relancer le match

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

Deux murs invisibles évitent les temps morts :

- `ScreenCeiling` colle un collider sur le bord **haut** du champ visible et le repositionne
  quand le cadrage change. Sans lui, une balle bien frappée sort de l'écran et le joueur
  attend plusieurs secondes sans rien voir avant qu'elle ne retombe.
- Le sommet du filet est coiffé d'un `CircleCollider2D` : un sommet plat laisserait la balle
  s'y poser en équilibre et figerait l'échange.

## Structure

```
Assets/
├── Art/                 Sprites générés par code + matériaux physiques
├── Settings/            Pipeline URP, Renderer 2D, volume profile par défaut
├── Editor/              → assembly SmilyVolley.Editor (exclue du build)
│   ├── PlaceholderArt.cs      Dessine les PNG (blobs, balle, filet, ciel, ombre)
│   ├── SceneBuilder.cs        Assemble toute la scène de jeu
│   ├── RenderPipelineSetup.cs Active URP sur tous les niveaux de qualité
│   └── BuildTools.cs          Build Windows + réglages projet
├── Scenes/Game.unity
└── Scripts/             → assembly SmilyVolley
    ├── Core/            GameManager, CameraFitter, Side
    ├── Gameplay/        BlobController, BallController, IA, entrées, ombre
    └── UI/              HudController
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
| `Ball` | `Hit Speed`, `Blob Velocity Influence` | Nervosité des échanges |
| `Ball` (Rigidbody2D) | `Gravity Scale` | Balle flottante ou lourde |
| `BlobLeft` / `BlobRight` | `Move Speed`, `Jump Speed`, `Gravity` | Sensation de déplacement |

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

- Sons (frappe, rebond, point) et particules d'impact — le manque le plus criant
- Menu principal, sélection de mode et écran d'options
- Effet de rotation sur la balle influençant la trajectoire
- Sprites définitifs en remplacement des PNG générés
- Difficultés d'IA nommées, mode tournoi

La feuille de route complète et les risques identifiés sont dans [docs/GDD.md](docs/GDD.md).
