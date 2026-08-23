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
| `footstep_snow`     | 000 → 004 | Atterrissage d'un blob (le crissement de la neige passe pour du sable) |
| `impactBell_heavy`  | 000 → 004 | Point marqué, et jingle de fin de match |

Cinq variantes par famille : `GameAudio` en tire une au hasard et lui applique une
légère variation de hauteur, ce qui évite l'effet de répétition mécanique sur les
échanges longs.

## Remplacer ces sons

Déposer d'autres fichiers dans ce dossier et réaffecter les banques du composant
`GameAudio` sur l'objet `Audio` de la scène. Aucun code à modifier : les banques sont
de simples tableaux d'`AudioClip` remplis par `SceneBuilder`, qui charge tout ce que
contient ce dossier selon les préfixes ci-dessus.
