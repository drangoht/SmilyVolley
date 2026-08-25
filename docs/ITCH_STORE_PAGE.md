# Smily Volley — page itch.io

> Document prêt à copier-coller, tenu à jour avec le jeu. **Strictement factuel** : tout ce
> qui est écrit ici existe dans le build (sources : `README.md`, `docs/GDD.md`). Ne rien
> ajouter qui n'ait été vérifié dans le jeu qui tourne.
>
> Page : <https://drangoht.itch.io/smily-volley> — **publique depuis le 24 août 2026**, en
> **version web uniquement**, poussée sur le canal `html5` par `tools/release_itch.ps1`.

---

## 1. Titre et accroche

**Titre :** Smily Volley

**Accroche courte (tagline) :**
> Le beach volley des Blobs souriants. Trois touches, un filet, et toute la profondeur dans
> l'endroit où la balle te touche.

---

## 2. Description courte (listes itch, 1–2 phrases)

> Volley de plage à deux blobs, dans la lignée de *Blobby Volley*. Trois commandes par joueur,
> à deux sur le même clavier — ou sur le même écran, au doigt — ou contre l'ordinateur.
> Jouable dans le navigateur, sur ordinateur comme sur téléphone.

---

## 3. Description longue (corps de page)

### Le pitch

Deux blobs, un filet, une balle. Chaque camp est un demi-cercle souriant qui ne sait faire que
trois choses : aller à gauche, aller à droite, sauter. Tout le jeu tient dans la quatrième, qui
n'a pas de touche : **où la balle te touche**.

La frappe n'est pas un rebond physique. La balle repart **radialement depuis le centre du
blob** : la toucher du côté sert à viser, lui tomber dessus sert à smasher. On apprend à placer
son blob, pas à appuyer plus vite.

### Ce qu'il y a dans la partie

- **À deux sur le même clavier** — ou sur le **même écran au doigt**, chacun son bord —, ou
  **contre l'ordinateur** : cinq niveaux, de Tranquille à Implacable.
- **Le placement donne la direction, l'élan donne la vitesse.** Un blob immobile renvoie au
  plancher, un blob qui retombe sur la balle lui ajoute sa chute : le smash sort à 20 u/s là où
  un échange calme tourne à 5. Une balle rapide perd la moitié de son excès à chaque frappe et
  revient d'elle-même au calme en trois échanges.
- **Des blobs en gelée** : ils s'écrasent à l'atterrissage, se tendent à l'appui, tremblent à
  l'impact — trois styles au choix, Ferme, Molle ou Moulée.
- **Match en 15 points**, deux points d'écart. Tout se règle : points pour gagner, écart,
  touches par camp, comptage *rally point* ou historique, camp qui engage.
- **Les six touches se réaffectent** une à une, et se retiennent d'une partie à l'autre.
- Une bande sonore qui suit l'écran : une boucle pour le menu, une autre pour le match.

### Ce que ce n'est pas

Ni progression, ni déblocage, ni classement en ligne. Un match dure trois à six minutes ; on
rejoue parce que le point précédent s'est joué de peu.

---

## 4. Commandes

| Action | Joueur 1 (AZERTY) | Joueur 1 (QWERTY) | Joueur 2 |
|---|---|---|---|
| Se déplacer | `Q` / `D` | `A` / `D` | `←` / `→` |
| Sauter | `Z` ou `Espace` | `W` ou `Espace` | `↑` |

`Tab` bascule entre « contre l'ordinateur » et « deux joueurs ». `R` relance le match.
`Échap` ouvre la pause. Toutes les touches se réaffectent dans les options.

### Au doigt, sur téléphone et tablette

Le jeu se joue aussi **au doigt, à un comme à deux joueurs**, en tenant l'appareil **en
largeur**. Les commandes apparaissent à l'écran dès le premier contact : un pavé
gauche/droite et un bouton de saut par joueur, plus un bouton de pause en haut à droite —
seul accès au menu, puisqu'il n'y a pas d'Échap.

- **À deux**, chacun tient son bord de l'écran avec ses trois boutons.
- **Contre l'ordinateur**, les commandes s'écartent aux deux bouts : déplacement sous le
  pouce gauche, saut sous le pouce droit.

En portrait, un panneau demande de tourner l'appareil : le terrain ne tient pas dans la
hauteur.

---

## 5. Fiche technique (à renseigner sur la page)

| Champ itch | Valeur |
|---|---|
| Kind of project | **HTML** — sans quoi le build se télécharge au lieu de se jouer |
| Release status | Released |
| Pricing | Free (No payments) |
| Uploads | dossier web poussé par butler sur le canal `html5`, coché « play in browser » |
| Viewport | **1280 × 720**, cadre 16/9 sur un fond de plage ; sur mobile, la page passe en plein écran |
| Fullscreen button | activé |
| Mobile friendly | **oui** — paysage uniquement, commandes tactiles à l'écran, 1 et 2 joueurs |
| Genre | Sports |
| Made with | Unity |
| Average session | A few minutes |
| Languages | French |
| Inputs | Keyboard, Touchscreen |
| Accessibility | Configurable controls |

---

## 6. Tags itch.io

Posés sur la page : `volleyball` · `sports` · `local-multiplayer` · `arcade` · `2d` ·
`physics` · `singleplayer` · `unity`.

---

## 7. Images

Toutes dans `docs/itch/`, régénérées depuis le jeu qui tourne :

| Fichier | Usage |
|---|---|
| `cover-630x500.jpg` | Couverture de la page (format imposé par itch) |
| `screen-1-menu.png` | Le menu sur l'affiche — première image de la galerie |
| `screen-2-match.png` | Un échange en cours, score affiché |
| `screen-3-smash.png` | Un blob en l'air au-dessus de la balle |
| `screen-4-options.png` | L'écran d'options, pour montrer ce qui se règle |

---

## 8. Déclaration d'IA

itch.io exige de classer le contenu produit par IA générative ; la classification est
obligatoire et alimente les filtres du site. Coché sur la page : **Graphics**,
**Text & Dialog**, **Code**. **Sounds** ne l'est pas : les deux musiques et les effets sont
des œuvres CC0 d'auteurs identifiés (Brandon Morris, Trex0n, Kenney), et la police est sous
licence OFL.

---

## 9. Crédits (bas de page)

- Musique : *Feel Good Island Loop* de **Brandon Morris** et *Beach Sports Theme* de
  **Trex0n** (Cal McEachern), toutes deux en **CC0** — [OpenGameArt](https://opengameart.org).
- Effets sonores : pack *Impact Sounds* de **[Kenney](https://kenney.nl)**, **CC0**.
- Police : **Fredoka**, SIL Open Font License.
- Inspiration : *Blobby Volley* (Daniel Skoraszewsky, 2000).
