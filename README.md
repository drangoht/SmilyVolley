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

Aucune dépendance externe : les sprites sont générés par code au premier build de scène,
à la seule exception de l'affiche du menu. La police est dans le dépôt, sa licence à côté.
Le projet se clone et se lance tel quel.

## Menu et options

Le jeu s'ouvre sur un menu ; « Jouer » y est déjà sélectionné, une frappe suffit à
lancer une partie. `Échap` met en pause à tout moment.

L'affiche du jeu (`Assets/Art/splash-screen.jpg`) sert de fond au menu principal : elle
porte déjà son logo, le titre écrit y est donc masqué. La pause et les options lui
préfèrent le terrain sous un voile clair — un réglage se juge sur ce qu'il change. Les
entrées reposent sur une carte de sable dont la hauteur suit le nombre de lignes, et dont
la largeur se resserre sur les écrans sans réglage. Une flèche apparaît dans sa marge quand
la liste déborde — l'écran d'options est plus long que l'écran, et rien ne le disait.

La ligne choisie n'est pas surlignée : **un blob la désigne**, dessiné par le même code que
ceux du terrain, et il saute de l'une à l'autre en s'écrasant comme la gelée du jeu. Le
libellé s'écarte pour lui faire place, la carte monte en fondu à l'ouverture, et une valeur
qu'on règle sursaute. Le texte est en **Fredoka** (`Assets/Fonts`, SIL Open Font License) :
des lettres rondes et pleines, de la même famille de formes que les blobs.

La navigation est au clavier — `↑ ↓` naviguer, `← →` régler, `Entrée` valider,
`Échap` revenir. `+` et `−` du pavé numérique doublent `← →`. La souris fait tout
aussi : la **surbrillance suit le curseur**, la **molette** déplace la sélection, et
chaque réglage porte un `−` et un `+` — sans eux le clic ne saurait que faire monter une
valeur. Tout **boucle** : la dernière ligne d'un
menu ramène à la première, et le dernier choix d'un réglage ramène au premier. L'appui
prolongé répète sur les deux volumes, qui sont des échelles ; sur un réglage à choix il
ne répète pas, sans quoi la liste tournerait sans qu'on puisse s'arrêter dessus.

**Au doigt, on fait défiler en glissant.** Le défilement de ce menu
n'existe pas séparément : la fenêtre visible se déduit de la ligne courante, que seuls le
clavier et la molette déplaçaient. Sur un téléphone il n'y a ni l'un ni l'autre — le joueur
touchait les lignes affichées et **rien au monde** ne lui donnait accès aux suivantes, si
bien que la moitié des réglages était hors d'atteinte.

**Et les deux flèches de débordement se touchent** : un appui fait défiler de trois lignes.
Elles ne faisaient qu'*annoncer* que la liste continue, ce qui suffisait tant qu'on avait une
molette ; au doigt, une indication qu'on ne peut pas toucher ne fait que désigner ce qu'on
n'atteint pas. Leur cible sensible est bien plus large que le dessin — le triangle fait
quatorze pixels de haut, soit le tiers d'un doigt.

> Le glissement lit la dalle directement (`TouchInput.ConsumeMenuDragY`) au lieu de passer par
> l'`IDragHandler` d'uGUI, qui fut la première version. Le glissement d'uGUI ne naît que si le
> pointeur **bouge après** avoir été enfoncé, sur des images distinctes : tout ce qui enfonce et
> déplace dans la même image ne franchit jamais son seuil et n'envoie rien. Le composant vivait,
> ses nombres étaient bons, et il ne recevait aucun événement — constaté en affichant ses
> compteurs à l'écran, faute de journal lisible dans un build de production (⚠ les `Debug.Log`
> du code managé n'y remontent pas, alors que ceux du moteur, si).
>
> ⚠ Un second défaut se cachait derrière : `SetGameControls(false)` est appelé **à chaque
> image** tant qu'un menu est ouvert — ce n'est pas une transition, c'est un état réaffirmé — et
> il remettait le glissement à zéro juste avant que le menu ne le lise. La liste ne bougeait pas
> d'un pixel sous un doigt qui la parcourait.
>
> ⚠ **Le glissement reste invérifiable depuis un navigateur piloté** : l'outil place le curseur
> *avant* d'enfoncer, si bien qu'aucun mécanisme fondé sur un déplacement ne voit jamais rien.
> C'est aussi pourquoi le déplacement des blobs vise une position **absolue** et non un delta —
> lui se vérifie. Les flèches, elles, sont des boutons : un simple appui les prouve.

| Section | Réglages |
|---|---|
| **Commandes** | Les six touches, réaffectables une à une, plus un retour à l'origine |
| **Adversaire** | Ordinateur ou humain ; difficulté de Tranquille à Implacable |
| **Règles** | Points pour gagner, écart de deux, touches par camp, comptage, camp qui engage |
| **Son** | Musique et effets |
| **Apparence** | Style des blobs : Ferme, Molle ou Moulée |
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

## Se jouer au doigt

La version web se joue sur téléphone et sur tablette, **à un comme à deux joueurs**, en
**paysage** — le portrait affiche un panneau qui demande de tourner l'appareil et met le
jeu en attente (`OrientationGate`). Le terrain fait plus de seize unités de large : en
hauteur, les blobs deviendraient des pastilles et les commandes se poseraient par-dessus le
sable. Ce n'est pas une mise en page à réagencer, c'est le champ de vision.

**Le blob suit le doigt.** Chaque joueur glisse dans sa moitié d'écran : le point touché
désigne l'endroit du terrain où il veut être, et son blob y court. La position à l'écran est
reconvertie en point du monde par la caméra, si bien que le doigt pointe *littéralement*
l'endroit visé — les deux camps occupent déjà chacun leur moitié d'écran, la correspondance
est gratuite. Il ne reste qu'**un seul bouton** par joueur : le saut, au coin.

```
2 JOUEURS (chacun sa moitié)          1 JOUEUR (contre l'ordinateur)
┌──────────────────────────┐          ┌──────────────────────────┐
│  0 - 0            [ ⏸ ]  │          │  0 - 0            [ ⏸ ]  │
│   ~glisser~ | ~glisser~  │          │      ~glisser~           │
│      ( )    |    ( )     │          │      ( )    |    ( )     │
│ (^)                 (^)  │          │                    ( ^ ) │
└──────────────────────────┘          └──────────────────────────┘
   J1              J2                  pouce gauche    pouce droit
```

> Le schéma de droite se **retourne** si le camp du joueur passe à droite : on glisse alors
> dans la moitié droite et le bouton de saut passe au bord gauche.


- **La vitesse reste celle du clavier.** Ce que le doigt produit est un axe borné à ±1,
  exactement comme une touche : un geste qui traverse l'écran ne fait pas courir le blob
  plus vite qu'une touche maintenue. Sans cette borne, le tactile serait *plus fort* que le
  clavier et les deux modes ne se joueraient plus au même jeu.
- **Le blob ralentit en arrivant.** À pleine vitesse il dépasserait le point visé d'un
  demi-pas, reviendrait, le dépasserait encore — il vibrerait autour du doigt. L'axe est
  donc dosé sur le dernier segment, à l'échelle du pas de physique, qui est exactement la
  distance qu'il va parcourir.
- **Un doigt appartient au camp où il s'est posé**, même s'il franchit le milieu de l'écran.
  Sans cette mémoire, un joueur qui court vers le filet se mettrait à piloter *le blob de
  son adversaire* — et courir vers le filet est précisément ce qu'on fait dans ce jeu.
- **Un seul doigt par camp** : le second posé ne vole pas la main au premier, sans quoi une
  paume à plat ferait tressauter le blob entre deux points au gré de l'ordre de lecture.
- **Glissez où vous voulez, y compris tout en bas.** Seule l'**abscisse** du doigt est lue :
  l'ordonnée ne veut rien dire, et le joueur peut donc piloter au ras du sable, loin des blobs
  et de la balle qu'il regarde. Le bandeau d'aide le dit désormais en toutes lettres — la
  propriété existait depuis le premier jour et personne ne la découvrait, parce qu'une main se
  pose là où l'on regarde, c'est-à-dire en plein milieu du jeu.
- **Le coin bas extérieur ignore les doigts qui s'y posent** (`TouchZones.IsPalmRest`). En
  paysage, ce qui touche la dalle à cet endroit n'est pas le pouce qui joue mais sa base : lue
  comme une désignation, elle envoie le blob au mur et l'y retient. Le joueur voit un blob qui
  « ne répond plus » alors que le jeu obéit parfaitement — à une main qu'il ne sait pas avoir
  posée. Le coin fait un rayon de bouton de large et 3 % de l'écran de haut — une quinzaine de
  millimètres sur deux —, et ne refuse que les doigts qui **arrivent** : un glissement déjà
  engagé le traverse sans rien perdre. ⚠ Ces deux mesures sont une **estimation** : ce qu'il
  faudrait connaître est où tombe le *centre* du contact que produit la base d'un pouce, et
  cela ne s'obtient que sur un vrai appareil. Un premier dimensionnement couvrait le tiers de
  la moitié du joueur — la zone reprenait alors d'une main ce que le conseil ci-dessus donne
  de l'autre.
- **Une colonne claire marque l'endroit désigné.** Elle ne double pas le doigt, elle le
  corrige : le doigt cache le point qu'il touche et le blob met un instant à l'atteindre —
  sans repère, le joueur ne sait ni où il a pointé, ni si le jeu l'a entendu.
- **Échap n'existe pas sur mobile.** Le bouton de pause, en haut à droite, est le seul
  accès au menu — donc à « rejouer », à « changer d'adversaire » et aux options.
- **Contre l'ordinateur, le camp se choisit** (options → *Camp du joueur*). Ce n'est pas une
  préférence esthétique : c'est ce qui met la tâche fine sous la main habile. Le côté de
  l'écran où l'on glisse et le camp où le blob court **ne peuvent pas être choisis
  séparément** — le doigt pointe le terrain, la moitié d'écran *est* la moitié de terrain. Le
  réglage déplace donc le joueur, l'ordinateur, le bouton de saut et le bandeau d'aide d'un
  seul geste. Au clavier, le joueur passe du même coup aux touches du joueur 2, et l'aide les
  annonce.

> **Ce schéma a remplacé un pavé directionnel**, et l'a fait pour une raison qu'on ne voit
> qu'en jouant : le pavé occupait le bas de l'écran, c'est-à-dire la bande où vivent les
> blobs. Le joueur perdait de vue le personnage qu'il déplaçait, au moment précis où il le
> déplaçait. Le glissement libre n'a rien à poser là : le bas de l'écran est rendu au jeu.
> Sur la version bureau, rien ne s'affiche : les commandes tactiles n'apparaissent qu'au
> premier contact d'un doigt.

### Ce que le tactile change dans le reste du jeu

| Ce qui s'affichait | Au doigt |
|---|---|
| `Q / D : se déplacer — Z : sauter — Tab : 2 joueurs` | `Glissez le doigt de votre côté de l'écran — le bouton pour sauter` : le déplacement est le seul geste du jeu qui ne se voie pas |
| `2 joueurs — même clavier` | `2 joueurs — même écran` |
| `Appuyez sur R pour rejouer` | `Touchez Pause, en haut à droite, pour rejouer` |
| `Haut/Bas ou molette : naviguer — Entrée : valider` | `Glissez pour faire défiler — touchez une ligne` |
| `Quitter` | absent : en WebGL, un onglet ne se ferme pas lui-même, et la ligne ne faisait rien |

> **Un texte peut être correct et faux.** Chacune de ces phrases était juste, et chacune
> désignait une touche que le joueur mobile n'a pas. La règle « une commande annonce sa
> touche » dit en réalité « annonce **comment** on la déclenche ».

### Vérifier sans téléphone

`--touch` en ligne de commande, `?touch` dans l'URL de la version web : le mode tactile est
forcé et la **souris est simulée en doigt** par l'Input System lui-même. Le chemin parcouru
est alors exactement celui d'un joueur, et non une image de démonstration posée à côté du
code qu'elle prétend montrer. Ce que cela ne couvre pas : le multi-touch — tenir le pavé
*et* presser le saut demande deux doigts, et à deux joueurs, quatre. Il faut alors un vrai
écran, ou l'émulation tactile du navigateur.

```powershell
.\Build\Windows\SmilyVolley.exe --touch
# ou, sur la version web :  http://127.0.0.1:8123/?touch
```

### Trois pièges qui ne se voient pas dans le code

- **`Touchscreen.current != null` ne dit pas que le joueur se sert de ses doigts.** Un
  portable Windows à dalle tactile en déclare une alors que son propriétaire joue au
  clavier. `TouchInput` distingue donc deux questions : *le joueur touche-t-il en ce
  moment ?* (réversible, décide de l'affichage) et *cet appareil est-il tactile ?* (jamais
  relâché, décide de ce qui est possible). Confondre les deux fait disparaître la garde
  d'orientation au premier contact — **tourner son téléphone ne cesse pas d'en faire un
  téléphone**.
- **`EventSystem.pixelDragThreshold` vaut 10 px**, calibré pour une souris, qui ne bouge
  pas quand on clique. Un pouce roule de deux millimètres pendant l'appui : uGUI conclut au
  glissement et **le bouton ne reçoit jamais son clic**. Aucune erreur, aucun journal — le
  menu paraît simplement mort. Élargi à 24 px au premier contact.
- **Un appui du doigt produit aussi un clic de souris**, l'événement de compatibilité hérité
  du web d'avant le tactile. Relâcher le mode tactile sur un clic ferait donc disparaître
  les contrôles au moment même où le joueur les touche.

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
  du blob** (`BallController.ApplyBlobHit`). Frapper avec le côté du blob permet de viser,
  le percuter par au-dessus permet de smasher.

Les rebonds sur les murs, le filet et le sol restent gérés par la physique 2D
(`Assets/Art/Bouncy.physicsMaterial2D` et `Sand.physicsMaterial2D`).

### Le placement donne la direction, l'élan donne la vitesse

La balle ne repart plus toujours à la même vitesse. Le renvoi vaut :

```
plancher 12 u/s   +   élan du blob sur l'axe de renvoi   +   50 % de l'excès reçu
```

- Un blob **immobile** renvoie au plancher : un échange calme le reste.
- Un blob qui **retombe sur la balle** lui ajoute ses 9 u/s de chute — c'est le smash.
- Une **balle rapide** garde la moitié de son excès à chaque frappe, et revient donc au
  plancher en trois échanges si personne ne la relance. C'est ce report inférieur à 1 qui
  garantit que la partie ne diverge pas.

L'élan compté est celui du blob **le long de l'axe de renvoi** : courir perpendiculairement
à la balle n'accélère rien, il faut aller dedans.

Mesuré sur le jeu compilé, vitesse relevée image par image :

| | Avant | Après, échange passif | Après, frappes en vol |
|---|---|---|---|
| Médiane en vol | 5,3 u/s | 5,4 u/s | 13,0 u/s |
| Maximum | 13,1 u/s | 12,6 u/s | **20,2 u/s** |
| Temps au-dessus de 15 u/s | 0 % | 0 % | 23 % |

La colonne du milieu compte autant que la dernière : **un échange que personne ne relance
n'a pas bougé**. Seul le joueur qui se jette dans la balle débloque les 20 u/s — et avant
le chantier, ce même joueur n'obtenait rien de plus que 13,1 u/s.

> **Le plafond de montée.** Sans garde-fou, un blob qui saute sous la balle lui met ses
> 9,7 u/s d'impulsion dans la verticale : mesurée, la balle sortait par le haut du cadre.
> La montée est écrêtée à 13,5 u/s (`Max Climb Speed`), ce qui n'aplatit que les
> chandelles — le smash va vers le bas et le renvoi rasant sur le côté, ni l'un ni l'autre
> n'est touché.

> **Pourquoi un angle minimal de renvoi.** La vitesse de renvoi converge vers le plancher
> au lieu de se conserver : deux frappes sans élan ramènent n'importe quelle balle à
> 12 u/s. Une balle qui retombe pile sur le
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

Deux **musiques de fond** tournent en boucle, en **CC0** : *Feel Good Island Loop* de
**Brandon Morris** ([OpenGameArt](https://opengameart.org/content/feel-good-island-loop))
pendant le match, et *Beach Sports Theme* de **Trex0n**
([OpenGameArt](https://opengameart.org/content/beach-sports-theme-loop)) sur l'affiche du
menu principal. Elles sont jouées à 0,25 de volume, soit une douzaine de décibels sous
les frappes — les morceaux ont un niveau proche de celui des effets, le baisser est ce
qui les place derrière l'action. Démarrage en fondu de 1,5 s.

Le menu principal a sa musique, la pause garde celle du match : on y est encore, et la
couper le temps de régler un volume ferait deux fondus enchaînés pour rien. Le passage
d'un morceau à l'autre est un fondu enchaîné de 0,8 s, compté sur le temps non mis à
l'échelle — le menu arrête l'horloge du jeu, et un fondu réglé sur l'horloge du jeu
resterait figé tant qu'on n'en sort pas.

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

## Les blobs : une gelée simulée

Le blob n'est pas un sprite. Son contour est un anneau de **41 points reliés par des
ressorts**, intégré à pas fixe et rendu dans un maillage reconstruit à chaque image
(`BlobJelly`). Trois forces le font vivre :

| Force | Rôle |
|---|---|
| **Mémoire de forme** | Chaque point est rappelé vers sa place au repos |
| **Couplage** | Chaque point est tiré vers le milieu de ses voisins : la bosse voyage le long du contour |
| **Pression** | L'aire est conservée : le flanc gonfle de ce que le sommet perd |

C'est ce qui distingue une gelée d'un sprite mis à l'échelle : la déformation est
**locale**. La balle creuse un vrai creux là où elle touche, l'atterrissage écrase par
le haut et fait déborder les côtés, un départ latéral laisse le sommet en arrière. Ces
formes dépendent de l'endroit et de la force du choc — aucun jeu d'images ne les contient.

La peau — couleur, ombrage, visage — est une texture dessinée dans l'espace du corps au
repos. Les UV du maillage étant figées sur cette forme, **le sourire se déforme avec le
corps** sans une ligne de code de plus.

### Les trois gelées

Le style ne change ni la taille du corps ni la physique du jeu : il change le contour au
repos et les réglages du ressort. La différence se voit donc surtout en mouvement.

| Style | Contour | Comportement | Largeur × hauteur mesurées en jeu |
|---|---|---|---|
| **Ferme** | Arc rond | Revient vite, déborde un peu | 124…178 × 49…77 |
| **Molle** | Arc rond, reflet mouillé | S'étale, ballotte longtemps | 113…206 × 44…85 |
| **Moulée** | Dix faces planes | Très raide, les arêtes se redressent aussitôt | 128…144 × 57…71 |

Le blob mesure 137 × 69 pixels au repos : la molle s'écrase à la moitié de sa hauteur et
s'étale d'une fois et demie, la moulée bouge à peine.

Le style se change dans les options, en cours de partie, et se garde d'une fois sur
l'autre.

## Structure

```
Assets/
├── Art/                 Sprites et peaux générés par code + matériaux
├── Audio/Kenney/        Effets CC0 + licence et provenance
├── Audio/Music/         Musiques CC0 (match + menu) + licence et provenance
├── Settings/            Pipeline URP, Renderer 2D, volume profile par défaut
├── Editor/              → assembly SmilyVolley.Editor (exclue du build)
│   ├── PlaceholderArt.cs      Dessine les PNG (balle, filet, ciel, ombre, particule)
│   ├── BlobArt.cs             Dessine la peau des blobs et ses matériaux
│   ├── SceneBuilder.cs        Assemble toute la scène de jeu
│   ├── RenderPipelineSetup.cs Active URP sur tous les niveaux de qualité
│   └── BuildTools.cs          Builds Windows et web, réglages projet, tampon de build
├── Scenes/Game.unity
└── Scripts/             → assembly SmilyVolley
    ├── Core/            GameManager, GameSettings, BlobStyle, CameraFitter, Side, TouchZones
    ├── Gameplay/        BlobController, BlobJelly, BallController, IA, entrées, TouchInput, particules
    ├── Audio/           GameAudio
    └── UI/              HudController, MenuController, MenuRow, TouchHud, OrientationGate
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
- **Régénérer la peau des blobs** — redessine les deux textures (une par joueur, une
  tuile par style) et reconfigure leurs matériaux.
- **Compiler le build Windows** — produit `Build/Windows/SmilyVolley.exe`.
- **Compiler la version web** — produit `Build/Web`, le dossier jouable dans un navigateur.
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

## La version web

`RebuildWeb` remplace `RebuildEverything` pour produire `Build/Web`, à servir tel quel :

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "C:\CODE\JEUX\Smily-Volley" `
  -executeMethod SmilyVolley.EditorTools.BuildTools.RebuildWeb `
  -logFile "$env:TEMP\smilyvolley-web.log"

python -m http.server 8123 --directory Build\Web   # http://127.0.0.1:8123
```

> Le jeu ne se lance pas depuis un `file://` : le chargeur d'Unity va chercher ses fichiers
> en HTTP. Un serveur local, même minimal, est indispensable pour l'essayer.

La page hôte est le gabarit `Assets/WebGLTemplates/SmilyVolley`. Elle fait des choses
qu'aucun réglage d'Unity ne fait :

- **elle garde le cadre 16/9** sur un écran de bureau et centre le jeu sur un fond de plage,
  plutôt que d'étirer la plage à la fenêtre ;
- **elle confisque les touches que le navigateur détourne** — l'espace fait défiler la page
  d'un écran, les flèches suivent le curseur ;
- **elle réveille le contexte audio** au premier clic : un navigateur n'ouvre le son qu'après
  un geste du joueur, et sans cela la musique du menu ne démarre qu'au hasard d'une frappe.

> ⚠ **La moitié du portage mobile n'est pas dans Unity.** Le moteur ne peut rien contre ce
> qui se passe avant lui, et rien de tout cela ne lève d'erreur : le **double-appui zoome**
> la page (donc sauter deux fois de suite), le **glissement fait défiler**, le **geste depuis
> le bord revient en arrière** — et un pavé directionnel est justement collé au bord —,
> l'**appui long ouvre un menu système**, et la **barre d'URL recouvre le bas de l'écran**,
> c'est-à-dire les commandes. Le gabarit les désarme tous, en CSS (`touch-action: none`,
> `user-scalable=no`, `100dvh`) et en quatre écouteurs, puis **arme le plein écran** au
> premier contact — celui-là exige un geste de l'utilisateur, l'appeler au chargement échoue
> en silence. Sur mobile, le cadre 16/9 laisse place au plein écran : `CameraFitter` garde le
> terrain entier visible de toute façon, et un cadre centré ne ferait que rapetisser les
> boutons.
>
> ⚠ **Et `devicePixelRatio` est forcé à 1** sur mobile. Un téléphone récent en annonce 3 :
> Unity rendrait **neuf fois** plus de pixels que la dalle logique n'en montre, sur un GPU
> qui vaut le dixième d'une carte de bureau. C'est le réglage le plus rentable du portage, et
> son absence ne se signale que par une cadence effondrée.

Les réglages du lecteur (Brotli avec repli JS, cache des données, stripping bas, taille de
toile) sont posés par `BuildTools.ApplyWebSettings`, jamais à la main dans l'éditeur.

> **La police du jeu n'a aucune flèche.** Fredoka ne contient ni « ← ↑ → ↓ » ni « ▲ ▼ ».
> Sur Windows, le moteur allait les chercher dans les polices du système ; un navigateur n'en
> propose aucune, et le build web perdait donc en silence les flèches des bandeaux d'aide et
> les indicateurs de défilement du menu. Les textes disent maintenant « Haut/Bas », et les
> indicateurs sont un sprite dessiné par `PlaceholderArt`.

### Le tampon de build

Le coin bas-droit affiche `v<version>-<sha>` : la version du projet et le commit dont le build
est issu, posé par `BuildTools.StampGitSha` **à chaque compilation**. Un `+` signale un arbre
de travail modifié — le build ne correspond alors à aucun commit — et `dev` avoue que git n'a
rien pu dire. Ce n'est pas pour le joueur : c'est ce qui permet à une capture d'écran de dire
quelle version elle montre, ce qui compte double sur une page web, où un navigateur ressert
volontiers un ancien fichier depuis son cache.

### Publier sur itch.io

```powershell
& "tools/release_itch.ps1" -Version 1.0.0 -DryRun   # va jusqu'au staging, ne publie rien
& "tools/release_itch.ps1" -Version 1.0.0
```

Le script pose le numéro de version, rebâtit, **vérifie que le tampon du build porte bien la
version demandée**, prépare un dossier de distribution propre et le pousse avec `butler` sur
le canal `html5` de [`drangoht/smily-volley`](https://drangoht.itch.io/smily-volley). Le nom
du canal n'est pas décoratif : `html5` est ce qui rend le fichier **jouable dans le
navigateur** ; sur n'importe quel autre nom, le même build se téléchargerait, sans que rien
ne le signale.

Le contenu de la fiche (accroche, description, tags, images) vit dans
[`docs/ITCH_STORE_PAGE.md`](docs/ITCH_STORE_PAGE.md), les images dans `docs/itch/`.

## Rendu

Le Renderer 2D d'URP applique aux sprites le matériau `Sprite-Lit-Default` : **sans lumière
dans la scène, tous les sprites seraient noirs**. `SceneBuilder` place donc une `Light2D`
globale blanche d'intensité 1, qui reproduit exactement l'aspect non éclairé tout en laissant
la porte ouverte aux effets d'éclairage 2D (halo sur la balle, ombres portées, ambiance).

Le corps des blobs échappe à cette règle : c'est un `MeshRenderer` portant un matériau
`Universal Render Pipeline/Unlit` transparent, **sans tri de faces** — le maillage se
retourne localement quand la gelée se creuse, et une face arrière disparaîtrait.

## Réglages utiles (Inspector)

| Objet | Champ | Effet |
|---|---|---|
| `GameManager` | `Right Player Is Ai` | Mode 1 joueur / 2 joueurs au démarrage |
| `GameManager` | `Ai Difficulty` | 0 = lent et imprécis, 1 = réflexes immédiats |
| `GameManager` | `Points To Win` | Longueur du match |
| `GameManager` | `Max Touches Per Side` | 0 = illimité ; 3 = règle volley classique |
| `GameManager` | `Serve Goes To Loser` | Décoché : le gagnant du point engage |
| `GameManager` | `Serve Offset X` | Décalage de la balle vers le filet au service |
| `Ball` | `Hit Speed` | Vitesse plancher d'un échange calme |
| `Ball` | `Blob Drive`, `Speed Carry` | Puissance du smash, durée de vie d'une balle rapide |
| `Ball` | `Blob Velocity Influence` | Part du déplacement du blob dans la direction du renvoi |
| `Ball` | `Max Climb Speed` | Hauteur des chandelles ; trop haut, la balle sort du cadre |
| `Ball` | `Min Vertical Angle` | Écart minimal du renvoi avec la verticale (0 = échanges bloquables) |
| `Ball` (Rigidbody2D) | `Gravity Scale` | Balle flottante ou lourde |
| `BlobLeft` / `BlobRight` | `Move Speed`, `Jump Speed`, `Gravity` | Sensation de déplacement |
| `Visual` *(BlobJelly)* | `Shape Stiffness`, `Damping` par style | Fermeté et durée du ballottement |
| `Visual` *(BlobJelly)* | `Land Gain`, `Jump Gain`, `Ball Gain`, `Inertia` | Ampleur de chaque source de déformation |
| `Audio` | Volumes par événement, `Pitch Jitter` | Équilibre et variété du mixage |
| `Audio` | `Music Volume`, `Music Fade In Seconds` | Présence de la musique |
| `Audio` | `Menu Music Gain`, `Music Crossfade Seconds` | Niveau du morceau du menu, durée du fondu enchaîné |
| `Audio` | `Jump Volume`, `Jump Pitch` | Discrétion de l'appui du saut |
| `ImpactEffects` | Nombre de particules par effet | Densité des bouffées |

## Notes de performance

Le jeu est léger, mais le code évite les schémas qui coûtent cher dès qu'un projet grossit :

- La nature de chaque collider rencontré par la balle est résolue **une fois** puis
  mémorisée — `OnCollisionStay2D` se déclenche à chaque pas de physique, y refaire un
  `GetComponentInParent` remonterait la hiérarchie 50 fois par seconde et par contact.
- Les `KeyControl` de l'Input System sont résolus au changement de périphérique, pas à
  chaque image.
- Ombres, cadrage caméra et plafond n'écrivent dans leur `Transform`
  que lorsque la valeur change réellement.
- Aucune allocation par image : tampon de liste réutilisé pour la sélection des entrées,
  table de chaînes pré-calculée pour le score. La gelée n'échappe pas à la règle — 41
  points et 287 sommets par blob, tous les tableaux alloués une fois pour toutes.
- Le HUD désactive sa propre boucle `Update` hors message temporisé.

## Pistes pour la suite

- Menu principal, sélection de mode et écran d'options
- Effet de rotation sur la balle influençant la trajectoire
- Sprites définitifs en remplacement des textures générées, et animation de repos
- Difficultés d'IA nommées, mode tournoi

La feuille de route complète et les risques identifiés sont dans [docs/GDD.md](docs/GDD.md).
