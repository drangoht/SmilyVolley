# Musique — provenance et licence

Deux morceaux : l'un pour le match, l'autre pour l'affiche du menu principal.

## Match — Feel Good Island Loop

**Feel Good Island Loop**, par **Brandon Morris**, publié sur
[OpenGameArt](https://opengameart.org/content/feel-good-island-loop).

Double licence : **CC0 1.0** (domaine public) et OGA-BY 3.0. C'est au titre du **CC0**
que le morceau est utilisé ici — aucune attribution n'est donc exigée. Elle est faite
de bon gré, ici et dans le README.

- <https://creativecommons.org/publicdomain/zero/1.0/>
- Fichier repris tel quel, sans modification : `feel_good_island_loop.ogg`

### Pourquoi ce morceau

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

## Menu — Beach Sports Theme (Loop)

**Beach Sports Theme (Loop)**, par **Trex0n** (Cal McEachern), publié sur
[OpenGameArt](https://opengameart.org/content/beach-sports-theme-loop), en **CC0** —
aucune attribution exigée, elle est faite de bon gré.

- <https://creativecommons.org/publicdomain/zero/1.0/>
- Fichier repris tel quel, sans modification : `beach_sports_menu_loop.ogg`

### Pourquoi ce morceau

Le menu ne doit pas sonner comme un autre jeu. Mesuré face au morceau du match :

| Grandeur | Menu | Match | Ce qu'elle dit |
|---|---|---|---|
| Durée | 32,0 s | 51,7 s | Deux boucles, aucune n'a de fin audible |
| Discontinuité au raccord | 0,0011 | 0,0004 | Le bouclage ne produit aucun clic |
| Tempo détecté | 117,5 BPM | 64,6 BPM | Rapport proche de 2 : les deux battues tombent ensemble |
| Répartition grave / médium | 85 % / 14 % | 99 % / 1 % | Deux mixages portés par le bas, le menu un peu plus ouvert |
| RMS | 0,122 | 0,243 | **Le menu est mixé 6 dB plus bas** |

Ce dernier écart est le seul point à traiter : deux morceaux au même réglage de volume
s'enchaîneraient comme un coup de volume. Le rattrapage est fait dans le mixage
(`Menu Music Gain` = 2 sur l'objet `Audio`), pas dans le fichier, qui reste l'original.

Le morceau du menu est plus clair de timbre (centroïde 2 710 Hz contre 306 Hz). Sans
conséquence : le menu n'a aucun effet sonore à laisser passer, là où le morceau du
match doit rester sous les frappes.

## Où chaque morceau se fait entendre

| Écran | Morceau |
|---|---|
| Menu principal, et options ouvertes depuis lui | Menu |
| Match | Match |
| Pause, et options ouvertes depuis elle | Match |

La pause garde la bande sonore du match : on y est encore, et la couper le temps de
régler un volume ferait deux fondus enchaînés pour rien. Le passage d'un morceau à
l'autre est un fondu enchaîné de 0,8 s (`Music Crossfade Seconds`), sur le temps non
mis à l'échelle — le menu arrête l'horloge du jeu.

## Remplacer la musique

Déposer un autre fichier ici : `SceneBuilder` le range au nom. Un nom contenant
**`menu`** devient le morceau du menu, sinon c'est celui du match. Rien d'autre à
régler — mais penser à revoir `Menu Music Gain` si le nouveau morceau n'a pas le même
niveau que celui qu'il remplace.
