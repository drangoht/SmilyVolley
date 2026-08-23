# Musique — provenance et licence

**Feel Good Island Loop**, par **Brandon Morris**, publié sur
[OpenGameArt](https://opengameart.org/content/feel-good-island-loop).

Double licence : **CC0 1.0** (domaine public) et OGA-BY 3.0. C'est au titre du **CC0**
que le morceau est utilisé ici — aucune attribution n'est donc exigée. Elle est faite
de bon gré, ici et dans le README.

- <https://creativecommons.org/publicdomain/zero/1.0/>
- Fichier repris tel quel, sans modification : `feel_good_island_loop.ogg`

## Pourquoi ce morceau

Mesuré avant intégration, faute de pouvoir l'écouter :

| Grandeur | Valeur | Ce qu'elle dit |
|---|---|---|
| Durée | 51,7 s | Boucle assez longue pour ne pas lasser sur un match de 3 à 6 min |
| Discontinuité au raccord | 0,0004 | Le bouclage ne produit aucun clic |
| RMS par tranche de 5 s | 0,219 → 0,256 | Niveau régulier : aucun passage ne saute aux oreilles |
| Centroïde spectral | 306 Hz | Timbre chaud, qui ne vient pas masquer les impacts |

Le niveau du morceau (RMS 0,243) est proche de celui des effets. Il est donc joué à
**0,25** de volume, soit une douzaine de décibels sous les frappes : présent, jamais
concurrent de l'action.

## Remplacer la musique

Déposer un autre fichier ici et réaffecter `Music Clip` sur l'objet `Audio` de la
scène. `SceneBuilder` charge le premier `AudioClip` trouvé dans ce dossier.
