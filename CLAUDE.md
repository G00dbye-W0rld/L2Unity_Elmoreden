# L2Unity : consignes du projet

## Textes affiches au joueur (HTML des PNJ, messages, interface)

- En francais, accents ecrits en clair (UTF-8). Jamais d'entites `&#233;` dans les .htm : le client lit `&#<n>;` comme un nom d'objet.
- **Pas de tiret cadratin (—) ni demi-cadratin (–)** dans les textes affiches, HTML compris. Utiliser deux-points, virgule ou parentheses.
- Espace avant `:` `;` `?` `!` et dans les montants (`25 000 adena`).

## Code

- Commentaires courts (2-3 lignes max), en francais, sans pave titre.
- Tout outil d'editeur sous `Assets/Scripts/Tools` est garde par `#if UNITY_EDITOR`.
- Le client lit les tables `*_Classic` de `StreamingAssets/Data/Meta`, pas les `*_Interlude`.
