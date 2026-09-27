# L2Unity : consignes du projet

## Textes affiches au joueur (HTML des PNJ, messages, interface)

- En francais, accents ecrits en clair (UTF-8). Jamais d'entites `&#233;` dans les .htm : le client lit `&#<n>;` comme un nom d'objet.
- **Pas de tiret cadratin (—) ni demi-cadratin (–)** dans les textes affiches, HTML compris. Utiliser deux-points, virgule ou parentheses.
- Espace avant `:` `;` `?` `!` et dans les montants (`25 000 adena`).

## Code

- Commentaires courts (2-3 lignes max), en francais, sans pave titre.
- Tout outil d'editeur sous `Assets/Scripts/Tools` est garde par `#if UNITY_EDITOR`.
- Le client lit les tables `*_Classic` de `StreamingAssets/Data/Meta`, pas les `*_Interlude`.

## Versions melangees

- **Regles et donnees de jeu : Interlude.** Le gameserver derive d'Acis Interlude, il fait foi sur les valeurs, les formules et la progression.
- **Assets : client Orfen** (Salvation). Modeles, animations, textures, sons et effets en viennent, Interlude n'en a pas d'exploitables.
- Ne jamais importer de *donnees de jeu* Orfen : il a des classes, objets et regles qui n'existent pas en Interlude.
- Quand une chose manque cote serveur en Interlude mais existe en Orfen, la reference a consulter est le serveur L2J Mobius Salvation : `F:\Lineage\L2J_Mobius-master-L2J_Mobius_05.0_Salvation\L2J_Mobius-master-L2J_Mobius_05.0_Salvation\L2J_Mobius_05.0_Salvation` (skills dans `dist/game/data/stats/skills`, code dans `java`). S'en servir pour comprendre un mecanisme, pas pour recopier des valeurs : en cas de conflit, Acis Interlude tranche.
