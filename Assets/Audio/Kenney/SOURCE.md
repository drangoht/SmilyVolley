# Sons — provenance et licence

Extraits du pack **Impact Sounds** de [Kenney](https://kenney.nl/assets/impact-sounds),
publié sous **Creative Commons Zero (CC0 1.0)** — domaine public.

- Utilisation libre en projet personnel, éducatif ou commercial.
- **Aucune attribution n'est exigée.** Kenney la demande à titre facultatif ;
  ce fichier et la mention dans le README y répondent.
- Texte de licence intégral : `License.txt`, et
  <https://creativecommons.org/publicdomain/zero/1.0/>

## Fichiers retenus

Sur les 130 sons du pack, 25 sont repris ici, sous leur nom d'origine pour que la
correspondance avec le pack amont reste vérifiable.

| Famille | Variantes | Usage dans le jeu |
|---|---|---|
| `impactSoft_medium` | 000 → 004 | Frappe de la balle sur un blob |
| `impactPlate_light` | 000 → 004 | Rebond sur un mur, le filet ou le plafond |
| `impactSoft_heavy`  | 000 → 004 | Balle qui retombe sur le sable |
| `footstep_snow`     | 000 → 004 | Atterrissage d'un blob **et appui du saut** (le crissement de la neige passe pour du sable) |
| `impactBell_heavy`  | 000 → 004 | Point marqué, et jingle de fin de match |

Cinq variantes par famille : `GameAudio` en tire une au hasard et lui applique une
légère variation de hauteur, ce qui évite l'effet de répétition mécanique sur les
échanges longs.

## Pourquoi le saut n'a pas son propre fichier

Les banques de sons de saut libres consultées ne convenaient pas, et c'est mesurable
plutôt qu'affaire de goût : les cinq échantillons du pack *Jump Sounds* de rudy85
(CC0, OpenGameArt) durent **1,85 à 2,50 s**, quand toute la palette du jeu tient sous
0,54 s. Dans un jeu où les blobs sautent sans arrêt, un son de deux secondes se
superpose à lui-même et prend toute la place. Les autres pistes menaient soit à des
ressorts de dessin animé, soit à des boucles de whoosh d'arme blanche.

L'appui réutilise donc le pas dans le sable, à **0,14** de volume et **×1,25** de
hauteur, contre 0,22 et ×1,0 à la réception. C'est aussi le geste réel : quitter le
sable et y retomber font le même bruit, plus vif et plus léger à l'appui. Les deux
restent nettement distincts à l'oreille comme à la mesure.

## Remplacer ces sons

Déposer d'autres fichiers dans ce dossier et réaffecter les banques du composant
`GameAudio` sur l'objet `Audio` de la scène. Aucun code à modifier : les banques sont
de simples tableaux d'`AudioClip` remplis par `SceneBuilder`, qui charge tout ce que
contient ce dossier selon les préfixes ci-dessus.
