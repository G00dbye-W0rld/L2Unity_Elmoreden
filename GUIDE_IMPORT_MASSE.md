# Guide — Importer et raccorder toutes les régions

Procédure opérationnelle pour passer des 6 régions actuelles aux **153 régions**
du client Interlude, puis les raccorder entre elles.

Complète [TUTO_IMPORT_MAP.md](TUTO_IMPORT_MAP.md), qui reste la référence sur le
*fonctionnement* du pipeline. Ce document-ci ne traite que de la **conduite du
chantier de masse**.

Établi le 2026-08-01.

---

## 0. À trancher avant de lancer quoi que ce soit

### Décision git — RÉGLÉE le 2026-08-01

Le dossier `Maps/` n'était couvert par aucune règle et le dépôt n'utilise pas
Git LFS. Décision prise : **ignorer les données de terrain générées**.

Règles ajoutées au `.gitignore` :

```
/l2-unity-main/l2-unity/Assets/Resources/Data/Maps/[0-9][0-9]_[0-9][0-9]/TerrainData/
/l2-unity-main/l2-unity/Assets/Resources/Data/Maps/[0-9][0-9]_[0-9][0-9]/TerrainData.meta
/l2-unity-main/l2-unity/Assets/Resources/Data/Maps/[0-9][0-9]_[0-9][0-9]/Meta/
/l2-unity-main/l2-unity/Assets/Resources/Data/Maps/[0-9][0-9]_[0-9][0-9]/Meta.meta
```

Ce qui est ignoré (~105 Mo/région, **entièrement régénérable** depuis le client
L2) : `TerrainData/` (heightmap, splatmaps, et surtout 81 Mo de texture arrays
MicroSplat) et `Meta/` (le `.t3d` et `Brushes.json` réextraits du `.unr`).

Ce qui reste versionné, **volontairement** : les `.prefab` des régions (~9 Mo
chacun). Ils portent le travail **manuel** des régions de référence —
`MusicArea`, `BoxVolumes`, `Marker`, réglages d'eau sur 16_24/16_25/17_24/17_25
— qui n'est pas régénérable par le pipeline.

Le motif ne cible que les dossiers nommés `nn_nn` : `l2_lobby/` vit au même
endroit sans être une région et reste versionné (vérifié).

> **Les fichiers déjà suivis ne sont pas désindexés par une règle
> `.gitignore`.** Les 354 fichiers `TerrainData` et 52 fichiers `Meta` des 6
> régions existantes continueront d'être suivis tant qu'un
> `git rm --cached -r` n'aura pas été fait sur eux (ils resteraient sur le
> disque, seul l'index change). À faire une fois l'arbre de travail propre.

Note : la **mutualisation des arrays MicroSplat** (todo projet) supprimerait
81 Mo par région à la source — elle rendrait ce gitignore beaucoup moins
critique.

### Prérequis

- **~20 Go libres** sur le disque du projet (452 Go disponibles au moment
  d'écrire — OK). Mesure : une région fraîchement importée pèse **~114 Mo**
  (99 Mo TerrainData + 6 Mo Meta + 9 Mo prefabs) → ~17 Go pour les 147
  restantes. Les 214 Mo/région observés sur les anciennes régions sont gonflés
  par un dossier `Old/` de 109 Mo, résidu que les nouveaux imports ne créent
  pas.
- **Unity fermé** pendant les lots : le mode batch ne peut pas verrouiller la
  base d'assets si l'éditeur est ouvert (les scripts le vérifient et refusent
  de démarrer sinon).
- **Commit propre** de l'état actuel avant de commencer, pour pouvoir revenir
  en arrière proprement.
- Compter **~4,5 min par région** (mesuré sur 17_23) → **~11 h** pour les 147
  régions restantes. À étaler sur plusieurs sessions, typiquement de nuit.

---

## 1. La liste de référence — 153 régions

Liste faisant autorité, obtenue depuis les `.unr` réellement présents dans
`Lineage II - The Chaotic Throne - Interlude/maps/`, **pas** depuis la carte du
monde en jeu.

> **La carte du monde ne montre pas tout.** Le client contient aussi la
> **colonne 15** (15_20 → 15_26) et la **rangée 26** (16_26, 17_26, 18_26,
> 22_26, 23_26, 24_26), absentes de l'image de la carte — ce sont les tuiles de
> bordure/océan. Elles comptent pour l'horizon et le bord du monde ; les
> importer est recommandé.

| Colonne | Régions | Nb |
|---|---|---|
| 15 | 15_20 → 15_26 | 7 |
| 16 | 16_10, 16_11, 16_12, 16_19 → 16_26 | 11 |
| 17 | 17_10, 17_11, 17_12, 17_18 → 17_26 | 12 |
| 18 | 18_10 → 18_15, 18_17 → 18_26 | 16 |
| 19 | 19_10, 19_11, 19_13 → 19_25 | 15 |
| 20 | 20_10, 20_11, 20_13 → 20_25 | 15 |
| 21 | 21_13 → 21_25 | 13 |
| 22 | 22_13 → 22_26 | 14 |
| 23 | 23_10 → 23_26 | 17 |
| 24 | 24_10 → 24_26 | 17 |
| 25 | 25_10, 25_11, 25_12, 25_14 → 25_21 | 11 |
| 26 | 26_11, 26_12, 26_14, 26_15, 26_16 | 5 |

**Déjà importées** (à ne pas refaire) : `16_24`, `16_25`, `17_22`, `17_23`,
`17_24`, `17_25`. **Restantes : 147.**

Pas besoin de tenir ce compte à jour à la main : `importer.bat` le recalcule à
chaque lancement, en listant les scènes réellement présentes.

Les trous dans les séquences (18_16, 19_12, 25_13, 25_22→25_25…) correspondent à
des `.unr` qui **n'existent pas** dans le client. Le stitch les ignore
silencieusement, ce n'est pas une erreur.

---

## 2. Phase A — Import

### 2.0 Le lanceur

**Un seul point d'entrée : `importer.bat`** (les anciens `importer-map.bat` et
`importer-maps.bat` ont été supprimés le 01/08 — `import-maps.ps1` gère aussi
bien une région que plusieurs, la distinction n'avait plus lieu d'être).

Double-cliquer `importer.bat` affiche l'état du chantier et demande quoi
importer. La saisie accepte :

| Saisie | Effet |
|---|---|
| `17_23` | une région |
| `17_22,17_23` | plusieurs régions |
| `col 18` | toute la colonne 18 |
| `reste` | tout ce qui n'est pas encore importé |

Il **valide les noms** contre les `.unr` réellement présents (une faute de
frappe est rejetée immédiatement au lieu de gâcher une extraction complète),
**détecte ce qui est déjà importé** et propose de le sauter, **refuse de
démarrer si Unity est ouvert** (avant l'extraction, pas après), et **estime la
durée**.

Les mêmes raccourcis marchent en ligne de commande (avec des guillemets quand
il y a un espace) :

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 "col 18" -RunUnity
```

### 2.1 Lot pilote (à faire en premier, obligatoire)

Trois bugs du pipeline ont été trouvés le 30/07 sur 17_23 (eau surdimensionnée,
caméra/lumière parasites, matériaux réparés globalement). Lancer 147 régions
sans validation préalable répliquerait un éventuel bug résiduel partout.

Commence par la **colonne 26**, la plus petite (5 régions) : double-clique
`importer.bat` et saisis `col 26`.

Équivalent en ligne de commande :

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 26_11,26_12,26_14,26_15,26_16 -RunUnity
```

Puis **ouvre une des 5 scènes dans Unity et vérifie** :

- [ ] pas de `Main Camera` ni de `Directional Light` dans la hiérarchie
- [ ] l'objet `Water` a bien l'échelle `104.2 / 0.1 / 104.2`
- [ ] l'objet `Safenet` est présent
- [ ] les textures du terrain ne sont ni roses ni délavées
- [ ] pas de flou/halo qui change selon l'angle de vue
- [ ] Console sans erreur

**Ne passe à la suite que si ces 6 points sont bons.**

### 2.2 Les autres colonnes

Le plus simple : `importer.bat`, puis `col 15`, `col 16`, … `col 25` — une
colonne par lancement. Chaque lot = **un seul lancement d'Unity** (le coût fixe
de démarrage/recompilation est amorti sur toute la colonne), et les régions
déjà importées sont automatiquement proposées à l'exclusion.

Les commandes équivalentes en ligne de commande, si tu préfères :

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 15_20,15_21,15_22,15_23,15_24,15_25,15_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 16_10,16_11,16_12,16_19,16_20,16_21,16_22,16_23,16_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 17_10,17_11,17_12,17_18,17_19,17_20,17_21,17_22,17_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 18_10,18_11,18_12,18_13,18_14,18_15,18_17,18_18,18_19,18_20,18_21,18_22,18_23,18_24,18_25,18_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 19_10,19_11,19_13,19_14,19_15,19_16,19_17,19_18,19_19,19_20,19_21,19_22,19_23,19_24,19_25 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 20_10,20_11,20_13,20_14,20_15,20_16,20_17,20_18,20_19,20_20,20_21,20_22,20_23,20_24,20_25 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 21_13,21_14,21_15,21_16,21_17,21_18,21_19,21_20,21_21,21_22,21_23,21_24,21_25 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 22_13,22_14,22_15,22_16,22_17,22_18,22_19,22_20,22_21,22_22,22_23,22_24,22_25,22_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 23_10,23_11,23_12,23_13,23_14,23_15,23_16,23_17,23_18,23_19,23_20,23_21,23_22,23_23,23_24,23_25,23_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 24_10,24_11,24_12,24_13,24_14,24_15,24_16,24_17,24_18,24_19,24_20,24_21,24_22,24_23,24_24,24_25,24_26 -RunUnity
```

```bash
cd "D:\Jeux\MAP_L2Unity"; .\import-maps.ps1 -MapIds 25_10,25_11,25_12,25_14,25_15,25_16,25_17,25_18,25_19,25_20,25_21 -RunUnity
```

Une région dont l'extraction échoue **n'interrompt pas** le lot : elle est
signalée et simplement absente de la liste transmise à Unity. Le script affiche
un récapitulatif par région à la fin, et vérifie que chaque scène existe
réellement (il ne se fie pas au code de sortie d'Unity, qui n'est pas fiable).

Les colonnes 23 et 24 (17 régions) prennent **~1 h 20** chacune. Si tu préfères
des lots plus courts, coupe-les en deux — ça ne change rien au résultat.

### 2.3 Après chaque lot

- Lire le récapitulatif : toute ligne `ECHEC` ou `import Unity echoue` est à
  reprendre individuellement avec `importer.bat <region>`.
- Vérifier une ou deux scènes au hasard (mêmes 6 points que le pilote).
- Committer (ou pas, selon la décision git du §0) avant le lot suivant.

> **Ne coche PAS les régions dans `_mapList`** au fur et à mesure. L'import les
> ajoute automatiquement avec `enabled = false`, et c'est volontaire : cocher
> les 153 ferait **crasher le jeu** (il n'y a pas de streaming, tout se charge
> d'un coup — voir la todo projet). Garde une poignée de régions cochées pour
> tes tests.

---

## 3. Phase B — Raccord (stitch)

### Comprendre l'algorithme — le point qui change tout

`L2/Import/11` ne traite **pas une région et ses voisins**. Il boucle sur
**TOUTES les régions présentes dans la scène** et traite les coutures de
chacune, en un seul appel (`L2TerrainGenerator.StitchTerrainSeams`, la boucle
`for (int i = 0; i < keys.Length; ++i)` ligne 385).

Pour **chaque** région chargée `Z_X`, il cherche ses deux voisins d'indice
**inférieur** — `(Z-1)_X` et `Z_(X-1)` — et, s'ils sont présents dans la scène,
modifie `Z_X` pour épouser leurs bords. Un voisin absent est ignoré sans erreur.

C'est ce qui rend le travail par gros lots possible : charger 30 régions et
cliquer une fois traite les ~30 coutures d'un coup. Travailler région par
région fonctionne aussi, mais demande 153 passes au lieu de 12.

Deux autres conséquences :

- Il ne faut **jamais** charger les 4 voisines d'une région : seules les deux
  d'indice inférieur comptent.
- L'opération est **idempotente** : l'algorithme lit toujours le bord *éloigné*
  du voisin (haut/droite) et écrit le bord *proche* de la cible (bas/gauche) —
  ce sont des bords différents, donc repasser sur une région déjà traitée
  réécrit les mêmes valeurs, et une passe ultérieure n'invalide pas une couture
  déjà faite. Seul le sommet unique où 4 régions se rejoignent peut varier d'un
  vertex, invisible en pratique.

### Méthode : fenêtre glissante de 2 colonnes — 12 passes

| Passe | Charger | Couvre |
|---|---|---|
| 1 | col 15 | coutures internes 15 |
| 2 | col 15 + 16 | internes 16 + toutes les 15↔16 |
| 3 | col 16 + 17 | internes 17 + 16↔17 |
| 4 | col 17 + 18 | internes 18 + 17↔18 |
| 5 | col 18 + 19 | internes 19 + 18↔19 |
| 6 | col 19 + 20 | internes 20 + 19↔20 |
| 7 | col 20 + 21 | internes 21 + 20↔21 |
| 8 | col 21 + 22 | internes 22 + 21↔22 |
| 9 | col 22 + 23 | internes 23 + 22↔23 |
| 10 | col 23 + 24 | internes 24 + 23↔24 |
| 11 | col 24 + 25 | internes 25 + 24↔25 |
| 12 | col 25 + 26 | internes 26 + 25↔26 |

Chaque arête est couverte **exactement une fois**.

### Déroulé d'une passe

1. **Nouvelle scène vide** (`File > New Scene`), jetable — à ne **jamais**
   sauvegarder.
2. Glisser depuis le Project les **`{région}.prefab`** des deux colonnes.
   Uniquement ceux-là : *pas* `StaticMeshes.prefab`, `Brushes.prefab` ni
   `AmbientSounds.prefab` — le stitch ne touche qu'au composant `Terrain`, le
   reste ne ferait qu'alourdir la scène. Les prefabs portent déjà leur position
   monde, aucun placement manuel.
3. Lancer **`L2/Import/11 Terrain - Stitch terrain seams`**.
4. Vérifier la Console : `[Stitch] Raccord de N region(s) : …`. Les lignes
   `Region 'X' absente de la scene, ignoree` sont normales (toutes les autres
   colonnes).
5. Inspecter visuellement les jointures dans la Scene view.
6. **`File > Save Project`** — le correctif s'écrit dans les assets
   `TerrainData`, **pas** dans la scène. Sans ça, tout est perdu.
7. Fermer la scène **sans sauvegarder**.

`DiscoverStitchableRegions()` scanne automatiquement `Data/Maps`, aucune liste à
maintenir à la main.

### D'où vient le lag

Ce n'est pas le `TerrainData`, et ce ne sont pas les décors : le
`{région}.prefab` ne référence **ni** `StaticMeshes.prefab`, **ni**
`Brushes.prefab`, **ni** `Lights.prefab` (vérifié par GUID — aucune référence).

Mesure sur les colonnes 15+16, soit 18 régions :

| | Poids |
|---|---|
| `TerrainData` | 132 Mo |
| **Texture arrays MicroSplat** | **262 Mo** |

Chaque région possède son **propre** shader, matériau et jeu de texture arrays —
`16_25` à elle seule fait 30 Mo de diffuse + 30 Mo de normales. Charger 18
régions, c'est donc compiler et garder résidents **18 shaders MicroSplat
distincts**. C'est là que passe le temps, et ça croît linéairement avec le
nombre de régions chargées.

**Conclusion : réduire le nombre de régions par passe est la bonne réponse.**

### Pourquoi on peut découper librement

L'algorithme (`StitchTerrainSeams`) est plus permissif qu'il n'y paraît. Pour
chaque région `colonne_rangée`, il ne regarde que **deux** voisins :

| Voisin | Ce qui est copié |
|---|---|
| `(colonne-1)_rangée` | sa **dernière colonne** → dans la **colonne 0** de la cible |
| `colonne_(rangée-1)` | sa **dernière rangée** → dans la **rangée 0** de la cible |

Le point décisif : **les lectures portent sur les arêtes *hautes* du voisin, les
écritures sur les arêtes *basses* de la cible**. Une écriture ne peut donc jamais
corrompre une lecture ultérieure.

> **Trois conséquences pratiques** :
> - l'**ordre des passes n'a aucune importance** ;
> - une passe **rejouée** donne exactement le même résultat (idempotente) ;
> - les **recouvrements sont gratuits** — dans le doute, recouvre plus.
>
> Tu peux donc découper aussi finement que tu veux sans rien risquer.

Le minimum absolu pour raccorder une région est de **3 régions** chargées :
elle-même, celle de gauche, celle du dessous.

### Plan détaillé — 41 passes, 12 régions maximum

Chaque ligne donne les **identifiants exacts à glisser** dans la scène jetable.
Ce sont les régions qui **existent réellement** — les trous de la grille sont
déjà retirés, il n'y a rien à interpréter.

En **gras** : les régions déjà chargées à la passe précédente. C'est le
recouvrement, qui garantit la couture entre deux bandes.

> **Couverture vérifiée par programme.** Le monde compte **266 coutures** entre
> régions voisines. Ce plan les couvre **toutes les 266** — contrôlé
> automatiquement, pas à l'œil.

| # | Régions à glisser | Nb |
|---|---|---|
| 1 | 15_20 15_21 15_22 15_23 15_24 15_25 | 6 |
| 2 | **15_25** 15_26 | 2 |
| 3 | 16_10 16_11 16_12 | 3 |
| 4 | 16_19 15_20 16_20 15_21 16_21 15_22 16_22 15_23 16_23 15_24 16_24 | 11 |
| 5 | **15_24** **16_24** 15_25 16_25 15_26 16_26 | 6 |
| 6 | 16_10 17_10 16_11 17_11 16_12 17_12 | 6 |
| 7 | 17_18 16_19 17_19 16_20 17_20 16_21 17_21 16_22 17_22 16_23 17_23 | 11 |
| 8 | **16_23** **17_23** 16_24 17_24 16_25 17_25 16_26 17_26 | 8 |
| 9 | 17_10 18_10 17_11 18_11 17_12 18_12 18_13 18_14 18_15 | 9 |
| 10 | 18_17 17_18 18_18 17_19 18_19 17_20 18_20 17_21 18_21 17_22 18_22 | 11 |
| 11 | **17_22** **18_22** 17_23 18_23 17_24 18_24 17_25 18_25 17_26 18_26 | 10 |
| 12 | 18_10 19_10 18_11 19_11 18_12 18_13 19_13 18_14 19_14 18_15 19_15 | 11 |
| 13 | **18_15** **19_15** 19_16 18_17 19_17 18_18 19_18 18_19 19_19 18_20 19_20 | 11 |
| 14 | **18_20** **19_20** 18_21 19_21 18_22 19_22 18_23 19_23 18_24 19_24 18_25 19_25 | 12 |
| 15 | **18_25** **19_25** 18_26 | 3 |
| 16 | 19_10 20_10 19_11 20_11 | 4 |
| 17 | 19_13 20_13 19_14 20_14 19_15 20_15 19_16 20_16 19_17 20_17 19_18 20_18 | 12 |
| 18 | **19_18** **20_18** 19_19 20_19 19_20 20_20 19_21 20_21 19_22 20_22 19_23 20_23 | 12 |
| 19 | **19_23** **20_23** 19_24 20_24 19_25 20_25 | 6 |
| 20 | 20_10 20_11 | 2 |
| 21 | 20_13 21_13 20_14 21_14 20_15 21_15 20_16 21_16 20_17 21_17 20_18 21_18 | 12 |
| 22 | **20_18** **21_18** 20_19 21_19 20_20 21_20 20_21 21_21 20_22 21_22 20_23 21_23 | 12 |
| 23 | **20_23** **21_23** 20_24 21_24 20_25 21_25 | 6 |
| 24 | 21_13 22_13 21_14 22_14 21_15 22_15 21_16 22_16 21_17 22_17 21_18 22_18 | 12 |
| 25 | **21_18** **22_18** 21_19 22_19 21_20 22_20 21_21 22_21 21_22 22_22 21_23 22_23 | 12 |
| 26 | **21_23** **22_23** 21_24 22_24 21_25 22_25 22_26 | 7 |
| 27 | 23_10 23_11 23_12 22_13 23_13 22_14 23_14 22_15 23_15 | 9 |
| 28 | **22_15** **23_15** 22_16 23_16 22_17 23_17 22_18 23_18 22_19 23_19 22_20 23_20 | 12 |
| 29 | **22_20** **23_20** 22_21 23_21 22_22 23_22 22_23 23_23 22_24 23_24 22_25 23_25 | 12 |
| 30 | **22_25** **23_25** 22_26 23_26 | 4 |
| 31 | 23_10 24_10 23_11 24_11 23_12 24_12 23_13 24_13 23_14 24_14 23_15 24_15 | 12 |
| 32 | **23_15** **24_15** 23_16 24_16 23_17 24_17 23_18 24_18 23_19 24_19 23_20 24_20 | 12 |
| 33 | **23_20** **24_20** 23_21 24_21 23_22 24_22 23_23 24_23 23_24 24_24 23_25 24_25 | 12 |
| 34 | **23_25** **24_25** 23_26 24_26 | 4 |
| 35 | 24_10 25_10 24_11 25_11 24_12 25_12 24_13 24_14 25_14 24_15 25_15 | 11 |
| 36 | **24_15** **25_15** 24_16 25_16 24_17 25_17 24_18 25_18 24_19 25_19 24_20 25_20 | 12 |
| 37 | **24_20** **25_20** 24_21 25_21 24_22 24_23 24_24 24_25 | 8 |
| 38 | **24_25** 24_26 | 2 |
| 39 | 25_10 25_11 26_11 25_12 26_12 | 5 |
| 40 | 25_14 26_14 25_15 26_15 25_16 26_16 25_17 25_18 25_19 | 9 |
| 41 | **25_19** 25_20 25_21 | 3 |

**Encore trop lourd ?** Coupe n'importe quelle passe en deux, en gardant dans
les deux moitiés les régions de la **rangée charnière**. Le découpage reste
valide : la seule règle est qu'une région et ses deux voisins d'indice inférieur
se retrouvent ensemble au moins une fois.

### Textures aux jonctions

`L2/Import/11` ne touche **ni aux textures ni au LOD** : il n'ajuste que les
hauteurs. La transition de peinture entre deux terrains reste manuelle.

**Ne la fais pas systématiquement.** Beaucoup de jonctions (océan, forêt
uniforme, terrains similaires) ne demanderont rien. Traite d'abord toutes les
hauteurs via les 12 passes ci-dessus — c'est mécanique et rapide — puis
parcours la carte et ne peins que les coutures qui choquent réellement à l'œil.

---

## 4. Données absentes du client officiel

Trois régions ont échoué avant que le pipeline ne devienne tolérant à ce que le
client Interlude **ne contient pas**. Le motif est toujours le même : une
donnée morte ou manquante faisait échouer **toute une région**.

| Symptôme | Cause | Régions | Traitement |
|---|---|---|---|
| `FileNotFoundException` sur `Height.<région>.bmp` | pas de `t_<région>.utx` dans le client | `19_11`, `20_11` — **les 2 seules sur 153** | terrain **plat** généré |
| `NullReferenceException` dans `GenerateUVLayers` | couche de terrain pointant sur `T_themepark`, package absent du client | `18_17`, `19_16`, `19_17` | couche **ignorée** |
| `NullReferenceException` dans `GetFolderAndFileFromInfo` | `StaticMeshActor` sans mesh (vestige `bDeleteMe`) | `16_21` | acteur **ignoré** |

**Sur la couche `T_themepark`** : c'est la couche *mer* de ces régions (son
masque s'appelle `..._See`). Vérification faite, ces 3 régions sont les
**seules** de tout le lot à avoir une couche mer, et la texture n'existe dans
aucun `.utx`. La retirer les met donc dans le même état que toutes les autres
régions côtières, dont l'eau vient du plan `Water` posé à l'étape 11 — pas
d'appauvrissement réel. Pour `19_17`, l'île de l'Olympiad Stadium est
intacte : elle vient de static meshes (`Aden_Colosseum`, `Broken_coloseum_S`),
pas de la couche de terrain.

**Pourquoi filtrer au parsing et pas dans le générateur** :
`GenerateUVLayers` dimensionne à la fois le tableau de `TerrainLayer` **et** le
splatmap 3D sur `uvLayers.Count`, puis les parcourt en parallèle. Neutraliser
une couche côté générateur décalerait les poids de splatmap. Le filtre en amont
protège en prime les 3 autres endroits qui lisent `uvLayers[i].texture` sans
garde (`UpdateMicrosplatParams`, `L2TerrainConverter`).

Ces cas sont désormais **signalés en tête d'import** par
`WarnAboutTextureCoverage`, avant les ~4 min de traitement. Un avertissement
« aucune heightmap » sur une région qui n'est *pas* une tuile d'océan trahirait
une extraction incomplète — à investiguer plutôt qu'à ignorer.

> Impossible de savoir à l'avance combien des régions restantes sont
> concernées : les `.unr` sont chiffrés, la chaîne n'est pas trouvable par un
> grep binaire. Les correctifs les traitent automatiquement au fil de l'eau.

---

## 5. Pièges

### Prefab voisin oublié dans une scène sauvegardée

Le piège le plus coûteux, et le plus discret. Si tu glisses des prefabs de
région dans une **vraie** scène (au lieu d'une scène jetable) et que tu la
sauvegardes sans les retirer, ce terrain se chargera **deux fois** au runtime —
depuis sa propre scène *et* depuis celle du voisin. Résultat : géométrie
dupliquée, z-fighting, mémoire doublée, et c'est très pénible à diagnostiquer
après coup.

**Utilise toujours une scène jetable pour le stitch.**

### Unity ouvert pendant un lot

Le mode batch ne peut pas verrouiller la base d'assets. Les scripts détectent le
cas et refusent de démarrer avec un message explicite — mais ferme Unity avant,
ça évite de perdre le lancement.

### « L'import a échoué » alors qu'il tourne encore

Corrigé le 30/07 dans `import-map.ps1` **et** `import-maps.ps1` : Unity se
relance en processus enfant détaché, donc la commande rendait la main avant même
que le log existe, et le script concluait à un échec. Les deux scripts attendent
maintenant le vrai signal de fin. Si tu vois encore ce symptôme, vérifie que tu
utilises bien la version à jour des scripts.

### Un lot entier échoue d'un coup (données malformées du client)

Si **toutes** les régions d'un lot échouent avec la même exception, ce n'est pas
un problème de région mais un fichier du client qui casse une routine partagée.
Cherche l'exception dans le log du lot :

```bash
grep -E "\[Import\] .* : echec" unity-import-batch-*.log
```

Cas rencontré le 04/08/2026 (`24_18` → `24_26`, 9 régions) :
`IndexOutOfRangeException` dans `L2MaterialBuilder.ProcessProps`, à cause de
`Textures/deco01/frame0*.props.txt`. Ces fichiers ne sont pas au format plat
`clé=valeur` mais **par blocs imbriqués** (meshes multi-matériaux) :

```
Materials[17] =
{
    Materials[0] = { Material=Texture'O_wood1', EnableCollision=true }
```

La ligne `{` n'a pas de `=`. **Corrigé** : les lignes sans `=` sont désormais
ignorées, dans `ProcessProps` et dans `L2T3DStaticMeshImporter`.

> **Regarde plusieurs lots en arrière.** L'échec avait commencé un lot plus tôt
> (`24_18`, 8/9) sans être remarqué. Un lot « 8/9 » passe facilement inaperçu
> quand on ne lit que la dernière ligne.

**Limite connue** : le parseur ne sait toujours pas lire le format à blocs. Ces
meshes multi-matériaux sortiront sans texture (objets gris). Si tu en vois dans
la colonne 24, c'est ça — il faudra alors traiter ce format pour de bon.

### Le reste

Voir la section « Pièges connus et leur correctif » de
[TUTO_IMPORT_MAP.md](TUTO_IMPORT_MAP.md) — matériaux, ordre 01/02/03, terrain
rose, meshes gris, etc.

---

## 6. Après l'import complet

Ce qui reste, par ordre de priorité :

1. **Streaming des régions** — indispensable avant de cocher plus d'une poignée
   de régions. Sans lui, tout charger fait crasher le jeu (~12 Go de texture
   arrays, ~180 000 GameObjects). Plan détaillé dans la todo projet.
2. **Mutualiser les texture arrays MicroSplat** — 81 Mo dupliqués par région,
   ~12 Go au total. Plus gros gain mémoire du projet, prérequis de fait du
   streaming.
3. **Geodata serveur** — déposer les `.l2j` de chaque région dans
   `gameserver/data/geodata/` (pack L2J Interlude). Sans elle, terrain visible
   mais gameplay incohérent.
4. **Peinture des jonctions** visibles (voir §3).
