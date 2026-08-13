# Importer et customiser une région de map

Pipeline **client officiel Interlude → Unity**, puis customisation.
Version 2 — réécrite le 2026-07-30 après le premier import réel (16_24/16_25/17_24/17_25
existaient déjà ; 17_23 et 17_22 ont été importées cette session, ce qui a validé et corrigé
tout ce qui suit). La version précédente (2026-07-27) décrivait un pipeline théorique basé
sur **l2pe**, jamais réellement utilisé : il a été remplacé par un outil maison.

## Sommaire

**Partie I — Comprendre**
1. [Il y a deux geodata](#1-il-y-a-deux-geodata-et-elles-sont-incompatibles)
2. [Les maths de la grille](#2-les-maths-de-la-grille)
3. [Ce qui existe déjà](#3-ce-qui-existe-déjà)

**Partie II — L'outillage réel**
4. [Vue d'ensemble du pipeline](#4-vue-densemble-du-pipeline)
5. [Outils et leur emplacement](#5-outils-et-leur-emplacement)

**Partie III — Exécuter**
6. [Import en une commande](#6-import-en-une-commande)
7. [Les étapes du menu L2, en détail](#7-les-étapes-du-menu-l2-en-détail)
8. [Raccorder deux régions (étape 11)](#8-raccorder-deux-régions-étape-11)
9. [Générer la geodata client](#9-générer-la-geodata-client)
10. [Déclarer la région côté serveur](#10-déclarer-la-région-côté-serveur)

**Partie IV — Customiser**
11. [Ce qui est personnalisable sans risque](#11-ce-qui-est-personnalisable-sans-risque)
12. [Static meshes et collision](#12-static-meshes-et-collision)

**Partie V — Pièges connus**
13. [Pièges connus et leur correctif](#13-pièges-connus-et-leur-correctif)

---

# Partie I — Comprendre

## 1. Il y a deux geodata, et elles sont incompatibles

C'est le point qui fait perdre le plus de temps si on l'ignore.

| | Geodata **client** | Geodata **serveur** |
|---|---|---|
| Sert à | pathfinding local | hauteur, ligne de vue, IA des PNJ |
| Format | triplets bruts `short x, y, z` | `.l2j` + `_conv.dat` |
| Produit par | `GeodataGenerator` + `GeodataExporter` (Unity) | **pas par ce projet** |
| Emplacement | export libre sous `Assets/` | `gameserver/data/geodata/` |

**L'outil Unity ne sait pas fabriquer la geodata du serveur.** Pour une nouvelle région,
il faut récupérer le `.l2j` depuis un pack geodata L2J Interlude — pas le générer.

**Bonne nouvelle** : le serveur **ne bloque pas** le déplacement du joueur avec la
geodata. `getValidLocation()` ne sert qu'à valider des positions aléatoires (spawns,
téléports à offset), et `ValidatePosition` fait un contrôle de *vitesse* puis accepte la
position envoyée par le client. C'est donc la collision **côté client** qui fait le
travail pratique. Une région sans `.l2j` reste donc explorable et jouable au quotidien —
seuls la ligne de vue des monstres et le pathfinding des PNJ en pâtissent (voir §12).

---

## 2. Les maths de la grille

Constantes lues dans `GeoStructure.java` et `World.java` :

```
CELL_SIZE  = 16 unités monde        CELL_HEIGHT = 8
1 bloc     = 8 × 8 cellules
1 région   = 256 × 256 blocs
           = 2048 × 2048 cellules
           = 2048 × 16 = 32768 unités   ← TILE_SIZE
```

Bornes du monde : tuiles **X de 16 à 26**, **Y de 10 à 25**.

```
worldX = (tileX - 20) × 32768        tileX = floor(worldX / 32768) + 20
worldY = (tileY - 18) × 32768        tileY = floor(worldY / 32768) + 18
```

Autre repère utile, vérifié empiriquement sur les régions importées : dans Unity, l'axe X
d'une région correspond exactement à une largeur de région (624,15 unités Unity =
32768 UU ÷ 52,5) par rapport à sa voisine dans le même sens. Deux régions adjacentes en X
ont donc des positions de racine séparées d'exactement cette valeur — utile pour vérifier
qu'un import est bien placé sans attendre l'étape 11.

---

## 3. Ce qui existe déjà

```
16_24   17_24
16_25   17_25   17_23   17_22
```

`16_24`, `16_25`, `17_24`, `17_25` existaient avant cette session (zone de Talking Island).
`17_23` (Gludio, côtière) et `17_22` (Gludio, ville) ont été importées cette session.

Chacune existe en trois exemplaires, qui doivent rester alignés :

| Quoi | Où |
|---|---|
| Scène Unity | `Assets/Resources/Scenes/{région}.unity` |
| Données terrain | `Assets/Resources/Data/Maps/{région}/` |
| Geodata serveur | `gameserver/data/geodata/{région}.l2j` + `_conv.dat` |

Le terrain utilise **MicroSplat**. `17_23` et `17_22` n'ont pour l'instant **aucune
geodata serveur** — voir §10.

---

# Partie II — L'outillage réel

## 4. Vue d'ensemble du pipeline

Deux phases bien séparées :

```
┌─────────────────────────────┐        ┌──────────────────────────────┐
│  D:\Jeux\MAP_L2Unity\        │        │  Unity (projet)              │
│  import-map.ps1              │──────▶ │  L2MapBatchImporter          │
│  (extraction, hors Unity)    │        │  (etapes 01 a 07 + scene)    │
└─────────────────────────────┘        └──────────────────────────────┘
```

**Phase 1 (PowerShell, hors Unity)** — à partir du client Interlude officiel :
1. `l2-map-export` (outil Java maison) → métadonnées `.t3d`
2. `umodel` → meshes (`.pskx`) et textures des packages référencés
3. `Blender` (script `pskx-to-fbx.py`) → conversion `.pskx` → `.fbx`
4. `flatten-meshes.ps1` → aplatit la structure `-groups` d'umodel au format attendu
   par l'importeur Unity
5. `l2-map-export -heightmap` → heightmap G16 (umodel et l2tool ne savent pas la lire)
6. `l2-brush-export` (outil Java maison) → `Brushes.json`
7. Copie du tout vers `Assets/Resources/Data/Maps/{région}/Meta/`

**Phase 2 (Unity, en mode batch ou via le menu)** — les 7 premières étapes du menu
`L2`, enchaînées automatiquement par `L2MapBatchImporter`.

Le script PowerShell peut piloter les deux phases d'un coup avec `-RunUnity`.

---

## 5. Outils et leur emplacement

| Outil | Rôle | Emplacement |
|---|---|---|
| **l2-map-export** (jar maison) | métadonnées `.t3d` + heightmap G16 | `D:\Jeux\MAP_L2Unity\tools\l2-map-export\build\libs\` |
| **l2-brush-export** (jar maison) | métadonnées de brushes (géométrie + polygones) | `D:\Jeux\MAP_L2Unity\tools\l2-brush-export\build\libs\` |
| **umodel** | meshes, textures, sons | `D:\Jeux\MAP_L2Unity\Lineage II - The Chaotic Throne - Interlude\umodel_win32\` |
| **Blender 3.6.23** portable + plugin PSK/PSA | conversion `.pskx` → `.fbx` | `D:\Jeux\MAP_L2Unity\blender-3.6.23-windows-x64\` |
| **JDK 17** (Liberica) | exécute les deux jars maison | `C:\Program Files\BellSoft\LibericaJDK-17` |

**l2pe n'est plus utilisé.** L'ancienne version de ce tuto en dépendait ; il a été
remplacé par `l2-map-export`, qui produit directement les métadonnées `.t3d` sans passer
par une interface graphique — plus fiable et scriptable.

Les chemins codés en dur du pipeline pointent tous vers `D:\Jeux\MAP_L2Unity\` :
`L2T3DStaticMeshImporter.ExportRoot`, les sélecteurs de fichiers (`Application.dataPath,
"Resources/Data/Maps"`), et `import-map.ps1` lui-même. Si ce dossier de travail change de
place, ce sont les points à corriger.

---

# Partie III — Exécuter

## 6. Import en une commande

**Le plus simple : double-cliquer.** `D:\Jeux\MAP_L2Unity\importer.bat` pose les questions
(région(s), lancer Unity ou non) sans avoir à connaître la syntaxe des arguments. Détecte
seul une virgule dans la saisie pour basculer entre une région et plusieurs.

**Pour un terminal ou un script**, Unity fermé :

```powershell
D:\Jeux\MAP_L2Unity\importer-map.bat <région> -RunUnity
```

Exemple : `importer-map.bat 17_22 -RunUnity`

Cette commande enchaîne tout : extraction umodel, conversion Blender, heightmap,
brushes, puis les 7 étapes Unity (import des modèles, matériaux, static meshes, terrain,
MicroSplat, paramètres de couches, brushes) et sauvegarde la scène
`Assets/Resources/Scenes/{région}.unity`.

**Le script lit la version d'Unity du projet** dans `ProjectSettings/ProjectVersion.txt`
et refuse de continuer si cette version précise n'est pas installée — voir §13, piège
« mauvaise version d'Unity ».

**Sans `-RunUnity`**, seule la phase 1 (extraction) s'exécute ; les étapes Unity restent
à faire à la main (§7) ou via le menu `L2 > Import > Import complet d'une region`,
qui fait exactement la même chose que `L2MapBatchImporter` mais depuis l'éditeur, avec un
retour plus lisible en cas de problème.

**Relancer une région déjà extraite** : ajoutez `-SkipMeshes` pour sauter l'extraction
umodel/Blender (longue) et ne relancer que la partie Unity — utile après une correction
de code sans changement des données sources.

### Nettoyer une région avant de la réimporter

`L2/Import/00 Scene - Nettoyer les objets generes` vide les objets déjà générés (terrain,
static meshes, brushes) d'une région dans la scène ouverte, sans toucher au reste. Utile
après un import interrompu ou pour repartir d'un état propre sans recréer la scène.

Pour une suppression complète des données d'une région (avant un import « de zéro »),
distinguer précisément ce qui est **exclusif** à cette région de ce qui est **partagé**
avec d'autres — voir §13, piège « suppression imprécise ».

---

## 7. Les étapes du menu L2, en détail

Chaque étape a désormais un double visage : une entrée de menu avec sélecteur de fichier
(pour un usage manuel, région par région), et un *worker* public sans dialogue (appelé
par `L2MapBatchImporter`, ou directement depuis un autre script d'éditeur).

| # | Entrée de menu | Worker | Rôle |
|---|---|---|---|
| 00 | `[Scene] Nettoyer les objets generes` | — | vide les objets d'une region deja generee |
| 01 | `[StaticMeshes] Import Textures and models` | `ImportStaticMeshesFrom` | copie modeles + textures depuis le cache d'export |
| 02 | `[Material] Generate materials` | `SetupMaterials` | cree les materiaux textures, puis rebranche les modeles (02b) |
| 02b | `[Material] Rebrancher les materiaux des modeles` | `RebindModelMaterials` | repare les modeles lies a un materiau vide (voir §13) |
| 03 | `[StaticMeshes] Generate staticmeshes` | `GenerateStaticMeshesFor` | place les objets dans la scene |
| 04 | `[Terrain] Generate terrain` | `GenerateTerrainFor` | heightmap, couches UV, couches deco |
| 05 | `[Terrain] Convert terrain to microsplat` | `ConvertTerrainFor` | bascule le terrain sous MicroSplat |
| 06 | `[Terrain] Update microsplat params` | `UpdateMicrosplatFor` | echelle et teinte par texture |
| 07 | `[Brush] (T3D) Build brushes` | `BuildBrushesFrom` | geometrie des brushes (batiments, volumes) |
| 08 | `[Camera] (T3D) Build cameras` | — | cameras definies dans la map |
| 09 | `[AmbientSound] (T3D) Import sounds` | — | importe les sons d'ambiance |
| 10 | `[AmbientSound] (T3D) Build ambient sounds` | — | les place dans la scene |
| 11 | `[Terrain] Stitch terrain seams` | — | raccorde les bords entre regions (§8) |
| — | `Import complet d'une region (01 a 07)` | `RunImport` | enchaine 01-07 + cree/sauvegarde la scene |

### Ce que `RunImport` fait après l'étape 07

Ces passes n'ont pas d'entrée de menu numérotée : elles sont enchaînées automatiquement
et rendent la région **jouable**, pas seulement visible.

| Passe | Rôle | Pourquoi c'est indispensable |
|---|---|---|
| Troncs d'arbres | désactive le collider du feuillage, pose un cylindre invisible à la base (layer 16 `Unwalkable`) | sans elle, on se cogne dans les branches en l'air et la geodata bloque toute la couronne |
| Sons d'ambiance | construit l'objet `AmbientSounds` (un `AmbientSoundEmitter` par son du `.unr`, jusqu'à ~1500/région) | équivalent automatique des étapes manuelles 09/10 — voir la mise en garde FMOD ci-dessous |
| Éclairages ponctuels | construit l'objet `Lights` (un `Light` Unity par acteur `Light` du `.unr`) | certaines régions (17_23) n'en contiennent aucune côté client d'origine : conteneur vide, pas un bug |
| Plan d'eau | clone l'objet `Water` d'une région de référence (17_25), **échelle et position locale fixes**, en enfant du Terrain | le plugin StylisedWater est trop spécifique pour être recréé à la main ; voir §13 pour l'historique du bug d'échelle |
| Filet de sécurité | clone l'objet `Safenet` d'une région de référence, même principe que l'eau | empêche de tomber dans le vide sous la région |
| Empaquetage prefabs | Terrain (+ Water + Safenet) / StaticMeshes / Brushes / `{région}_AmbientSounds` / Lights sauvegardés sous `Data/Maps/{région}/` | aligne la structure sur 16_25/17_25 ; confort d'édition, pas une nécessité runtime |
| Déclaration de la région | ajout aux **Build Settings** + à la `_mapList` du `SceneLoader` (`Resources/Prefab/Game.prefab`) | `SceneLoader` charge les régions **par nom de scène** : une scène absente des Build Settings ne peut pas se charger du tout |
| Bilan de santé | compte renderers, colliders, objets sur layer 0, matériaux manquants | transforme en une ligne de log ce qui n'était détectable qu'à l'œil, après coup |

**Sons d'ambiance et banques FMOD.** L'objet `AmbientSounds` et ses émetteurs sont
désormais créés automatiquement, mais la banque FMOD Studio n'est aujourd'hui construite
que pour Talking Island (cf. mémoire projet « FMOD banks obsoletes »). Sur une région
Gludio (17_23, 17_25...), les émetteurs existeront bien en scène mais resteront
**silencieux** tant que la banque n'est pas reconstruite pour ces régions — silence
attendu, pas une régression du pipeline.

**Éclairages : conversion approximative, à recalibrer à l'œil.** `LightBrightness` et
`LightRadius` du `.unr` ne sont pas dans les mêmes unités que `Light.intensity` /
`Light.range` d'Unity ; `LightHue`/`LightSaturation` encodent une couleur à l'ancienne
convention Unreal (0-255, saturation inversée). `L2LightBuilder` applique une conversion
raisonnable mais non vérifiée en jeu — deux constantes en tête de fichier
(`RadiusToUnrealUnits`, `BrightnessToIntensity`) à ajuster si l'éclairage de 17_25 (seule
région de référence à contenir de vraies données `Light`) paraît trop faible/fort une fois
importé.

La région est ajoutée à la `_mapList` avec **`enabled = false`**, comme le sont déjà
16_25/17_24/16_24. Passez-la à `true` dans `Resources/Prefab/Game.prefab` quand elle est
validée visuellement et que sa geodata serveur est en place — charger une région au
démarrage est une décision de gameplay, pas une conséquence de l'import.

### Colliders et layers

Deux réglages sans lesquels une région n'est qu'un décor traversable :

- **Colliders** : Unity importe les FBX avec `addCollider = false` par défaut. Le pipeline
  force désormais ce réglage à l'étape 01 (`AssetImporter.ConfigureMeshColliders`), y
  compris sur les modèles déjà présents dont le `.meta` date d'un import antérieur.
  Référence 17_25 : 1115 `MeshCollider` pour 1154 `MeshRenderer`.
- **Layers** : tout arrivait sur le layer 0 (`Default`), donc **invisible** pour le
  `GeodataGenerator`, qui filtre par `walkableMask` / `obstacleMask` / `allowWalkMask`.
  Règles appliquées, relevées sur 17_25 : layer **7** (`StaticMesh`) par défaut, **3**
  (`Terrain`) pour les ponts — surfaces qui doivent rester marchables —, **16**
  (`Unwalkable`) pour les troncs.

**L'ordre 01 → 02 → 03 est strict et automatique** dans `L2MapBatchImporter` : à l'import
d'un FBX, Unity cherche un matériau du même nom dans tout le projet et, faute de le
trouver (les matériaux texturés n'existent qu'après l'étape 02), en crée un vide. L'étape
02 répare ces coquilles vides et réimporte les modèles concernés, mais les objets déjà
placés en scène gardent leurs anciennes références — il faut donc rejouer 03 après 02.
Voir §13 pour le symptôme si cet ordre n'est pas respecté à la main.

**08 reste sans entrée de menu numérotée** : les caméras d'intro (`InterpolationPoint`)
n'ont pas d'automatisation dédiée pour l'instant, contrairement aux sons (09) et
éclairages (10) désormais enchaînés par `RunImport` — voir le tableau ci-dessus.

---

## 8. Raccorder deux régions (étape 11)

`L2/Import/11` copie la ligne de hauteurs de la bordure d'une région vers celle de sa voisine
d'indice supérieur, pour supprimer la fissure verticale entre deux terrains générés
indépendamment. Deux points de mécanique à connaître :

- **Le sens est imposé.** L'algorithme ne regarde que les voisins d'indice **inférieur**
  (`(X-1)_Y` et `X_(Y-1)`). Pour raccorder `17_22` et `17_23`, c'est donc le `TerrainData`
  de **17_23** qui est modifié pour épouser 17_22, jamais l'inverse.
- **Le voisin n'a besoin d'être présent que le temps de l'opération.** L'écriture va dans
  le `TerrainData` (un asset), pas dans la scène. Glissez le prefab de la région voisine
  dans la scène ouverte, lancez `L2/Import/11`, vérifiez la jointure, puis retirez le prefab
  de la hiérarchie et sauvegardez — la correction reste gravée dans l'asset.

Avant de lancer, ajoutez la région à la liste `StitchableRegions` dans
`L2TerrainGeneratorTool.cs` si elle n'y figure pas encore.

`L2/Import/11` ne touche **ni aux textures ni au LOD** : la transition de peinture entre deux
régions reste à faire à la main (peinture manuelle du terrain), et `Terrain.SetNeighbors`
n'est appelé nulle part dans le projet — un défaut de LOD aux jointures resterait
possible à distance.

---

## 9. Générer la geodata client

Composant `GeodataGenerator`. Il travaille par **raycasts** et s'exécute au `Start()` —
donc en Play Mode.

| Champ | Rôle |
|---|---|
| `terrainTransform` | le terrain à analyser |
| `nodeSize` | résolution de la grille (défaut `0.25`) |
| `characterHeight` | hauteur libre exigée pour qu'une case soit marchable |
| `erosionThreshold` | pente au-delà de laquelle on ne passe plus |
| `walkableMask` / `obstacleMask` / `allowWalkMask` | layers pris en compte |
| `export` + `exportPath` | écrit le fichier sous `Application.dataPath` |

`enableCustomGeneratedTerrainWidth` et `…Origin` permettent de ne générer **qu'une
portion** de région — indispensable pour itérer sur une zone précise sans recalculer
32768 unités.

---

## 10. Déclarer la région côté serveur

1. Placer `{région}.l2j` et son `_conv.dat` dans `gameserver/data/geodata/`.
   Le nom est construit par `GeoEngine.java:120` via `Config.GEODATA_TYPE.getFilename()`.
2. Vérifier que la région tombe dans `TILE_X_MIN..MAX` / `TILE_Y_MIN..MAX` — elles
   couvrent déjà tout Interlude, donc aucune modification attendue.

**17_23 et 17_22 n'ont pas encore de `.l2j`** — à obtenir d'un pack geodata L2J Interlude.
Sans lui, ces régions restent explorables (voir §1) mais sans ligne de vue ni pathfinding
PNJ corrects.

---

# Partie IV — Customiser

## 11. Ce qui est personnalisable sans risque

La geodata ne contraint qu'une chose : où l'on peut marcher et ce qui bloque la vue.
Tout le reste est libre.

### Tout le visuel, côté client

Le serveur n'en sait rien : textures et peinture du terrain, végétation, props non
bloquants, éclairage, brouillard, météo, sons d'ambiance, skybox. Vous pouvez refaire
entièrement l'aspect d'une zone sans toucher à un octet de geodata.

### Les données serveur pilotées par coordonnées

| Fichier | Ce que vous pouvez faire |
|---|---|
| `zones/` — **23 types** | PeaceZone, CastleZone, ClanHallZone, SiegeZone, DamageZone, NoLandingZone, FishingZone… polygones `x/y` + `minZ/maxZ` |
| `spawnlist/` | placer PNJ et monstres où vous voulez |
| `teleports.xml`, `instantTeleports.xml` | destinations de gatekeeper, points de restart |
| `castles.xml`, `clanHalls.xml` | **déjà remplis**, données officielles incluses |
| `doors.xml` | portes — avec leur propre collision (voir §12) |
| `staticObjects.xml`, `multisell/`, `buyLists.xml`, `npcs/` | contenu marchand et PNJ |

---

## 12. Static meshes et collision

Vous ajoutez une maison, ou vous récupérez les meshes d'un château. Ce qui se passe
dépend d'un seul facteur : **la geodata serveur connaît-elle déjà ce volume ?**

### Cas 1 — mesh officiel, emplacement officiel, région officielle

La geodata du pack L2J le contient déjà : murs, sols, étages. **Zéro travail.**
C'est le chemin recommandé pour un château et des clan halls.

### Cas 2 — maison custom, ou bâtiment déplacé

Côté client tout fonctionne : collider Unity et geodata client régénérée, le joueur ne
traverse pas les murs. Côté serveur, trois symptômes :

| Usage serveur | Conséquence |
|---|---|
| `getHeight(x,y,z)` | le serveur vous croit au niveau du terrain, pas sur un plancher d'étage |
| `canSeeTarget()` | les monstres vous tirent dessus **à travers les murs** |
| Pathfinding des PNJ | les mobs traversent le bâtiment |

En pratique : un bâtiment décoratif ou de plain-pied passe presque inaperçu. Un bâtiment
à étages rend le Z faux. Une zone de combat rend la ligne de vue absurde.

### Comment les maps existantes gèrent leurs static meshes

Relevé sur la région `17_25` (Talking Island), pour servir de modèle.

**Un prefab unique par région.** `Assets/Resources/Data/Maps/{région}/{région}.prefab`
porte le Terrain ; `StaticMeshes.prefab`, `Brushes.prefab`, `BoxVolumes.prefab`,
`{région}_AmbientSounds.prefab`, `MusicArea.prefab` complètent l'ensemble. Ni le
`Terrain` ni les `Water`/`Marker`/`Safenet` de ces prefabs ne sont produits par le
pipeline d'import — ils sont posés à la main (voir §13, « eau et volumes »).

**Nommage des meshes** : `{package}_s.{Nom}` — par exemple `SI_V_S.SI_H01`
(Speaking Island Village, maison 01), `field_deco_S.Gludio_general_stone2_2`. Le préfixe
correspond au dossier d'export umodel.

### Cas 3 — la voie propre, sans toucher au `.l2j`

**Les portes le font déjà** : `Door.java` appelle `GeoEngine.addGeoObject(this)` à la
fermeture et `removeGeoObject(this)` à l'ouverture. Une porte fermée s'inscrit **dans la
geodata à chaud**.

L'interface `IGeoObject` n'a rien de spécifique aux portes :

```java
int getGeoX(); int getGeoY(); int getGeoZ();
int getHeight();
byte[][] getObjectGeoData();   // l'empreinte au sol
```

Un bâtiment peut donc s'enregistrer de la même façon, sans jamais éditer un `.l2j`.
C'est du code neuf, mais le mécanisme est éprouvé en production par les portes. Le vrai
travail est la génération de l'empreinte `byte[][]`.

> **Recommandation.** Récupérez meshes *et* geodata des régions officielles de château :
> tout concorde, la zone est testable immédiatement. Gardez `IGeoObject` en réserve pour
> le jour où vous voudrez un bâtiment custom réellement solide.

---

# Partie V — Pièges connus

## 13. Pièges connus et leur correctif

### Meshes gris (ordre étape 01 / étape 02)

À l'import d'un FBX, Unity crée un matériau vide s'il n'en trouve pas de même nom dans le
projet. Comme les matériaux texturés n'existent qu'après l'étape 02, l'étape 01 lie les
modèles à des coquilles vides. **Corrigé** par `L2/Import/02b`, appelé automatiquement en fin
d'étape 02 : il supprime ces coquilles quand un remplaçant texturé existe, et réimporte
les modèles concernés.

**Piège résiduel** : si vous relancez 02 (ou 02b) à la main, les objets déjà posés en
scène gardent leurs anciennes références jusqu'au prochain rechargement de scène (ils
tournent au **magenta**, pas au gris). Toujours rejouer l'étape 03 après 02, puis
sauvegarder — et valider en changeant de scène et en revenant, seul test qui révèle
vraiment le problème.

### Bâtiments blancs (`LoadTexture` remonte d'un niveau de trop)

Deux dispositions coexistent selon le package umodel : le `.props.txt` (et le `.mat`
construit à partir de lui) est soit dans un sous-dossier `Materials/` du package de
textures, soit écrit directement à sa racine. `L2MaterialBuilder.LoadTexture` remontait
systématiquement d'un niveau pour trouver la texture — correct pour le premier cas,
mais atterrissant un cran trop haut pour le second. **Corrigé** : la fonction cherche
désormais aux deux emplacements.

Diagnostic si le symptôme réapparaît : comparer le nombre de `.png` et de `.mat` dans un
dossier de textures (`Get-ChildItem -Filter *.png` vs `*.mat`) — un déficit de PNG signale
des matériaux sans réplique visuelle.

### Sol rose (texture sans substitution PBR)

Chaque nouvelle région Gludio peut introduire des textures L2 jamais vues. Sans entrée
dans `textureMatches` (`L2TerrainGeneratorTextureMatcher.cs`), la couche correspondante
n'a aucun `.terrainlayer` généré — la case reste vide dans le tableau de textures lu par
le shader MicroSplat, d'où le rose. **Corrigé pour `GUG102`/`GUS110`** (apparues sur
17_22) ; toute texture Gludio future suivra le même schéma tant qu'elle n'est pas ajoutée.

Diagnostic : compter les fichiers `.terrainlayer` dans `TerrainData/MicroSplatData/` et
vérifier qu'il y en a bien un par index de couche (suffixe `_<index>.terrainlayer`).

### Static meshes tournés de 90°

Constaté visuellement (pas par le calcul — voir plus bas) sur 17_22/17_23 : tous les
static meshes ressortaient tournés de 90° par rapport à leur orientation réelle dans le
client. **Corrigé** par `eulerAngles.y += 90f` dans
`L2MapStaticMeshBuilder.BuildSingleStaticMesh`, appliqué uniquement aux static meshes
(pas aux caméras, qui utilisent la même fonction `VectorUtils.ConvertRotToUnity` sans ce
défaut signalé).

**Leçon de méthode** : vérifier qu'un calcul reproduit fidèlement une valeur source
(ici Pitch/Yaw/Roll → Unity) ne prouve pas sa conformité au rendu réel du client — seule
l'observation visuelle directe le permet. Une vérification arithmétique interne avait
conclu à tort que tout était correct.

### Échelle des textures de peinture (tuiles géantes)

Sur un terrain converti par MicroSplat, deux réglages d'échelle coexistent : le champ
`Size` du Terrain Layer natif d'Unity (visible dans l'outil `Paint Texture`), et l'**UV
Scale par texture de MicroSplat** (matériau `MicroSplat.mat`, onglet `Per-Texture`). Seul
le second pilote le rendu final une fois la conversion faite — régler le premier n'a
aucun effet visible. Réglage manuel : sélectionner le `.mat` **template** (pas une
instance), onglet `Per-Texture`, sélectionner la texture, champ `UV Scale`.

### Mauvaise version d'Unity (compilation cassée)

Le plugin **Beautify** s'injecte dans l'assembly d'URP via un `.asmref` pour accéder à des
membres `internal`. Ouvrir le projet avec une version d'Unity différente de celle du
projet (`ProjectSettings/ProjectVersion.txt`) peut laisser des dossiers
`Library/PackageCache/.del--*` en attente de suppression, contenant des `.asmdef` en
double — la résolution de l'`.asmref` échoue, Beautify retombe dans `Assembly-CSharp`,
perd l'accès aux membres internes, et **tout le projet cesse de compiler**. Le script
`import-map.ps1` lit désormais la version exacte du projet et refuse de continuer si elle
n'est pas installée. En cas de blocage « projet déjà ouvert » après un crash : vérifier
qu'aucun processus Unity ne tourne (`Get-Process Unity`), puis supprimer
`Temp/UnityLockfile`.

### Le code de sortie d'Unity n'est pas fiable

`Unity.exe` se relance en processus enfant et le parent rend 0 quoi qu'il arrive en mode
batch. Le script vérifie donc le résultat réel (scène créée + ligne
`[Import] === <region> : termine` dans le log), pas `$LASTEXITCODE`.

### Suppression imprécise d'une région (fichiers suivis perdus)

Avant de supprimer les données d'une région pour la réimporter, bien distinguer ce qui
lui est **exclusif** de ce qui est **partagé** avec d'autres régions ou déjà présent dans
le dépôt. `git status` seul ne suffit pas : un fichier suivi et non modifié n'apparaît ni
dans les entrées modifiées ni dans les non-suivies — un faux sentiment de sécurité. Avant
toute suppression touchant des dossiers partagés, vérifier avec `git ls-files` (liste
tout ce qui est suivi, modifié ou non) plutôt que `git status` seul. Et ne jamais utiliser
`git checkout -- .` pour « juste restaurer des fichiers supprimés » : cette commande
annule *aussi* toute modification non commitée sur les fichiers déjà suivis.

### Matériau cassé une fois, cassé pour toujours (`ProcessProps`)

`L2MaterialBuilder.ProcessProps` sautait tout matériau déjà présent sur disque
(`overwrite=false`), **qu'il soit correct ou vide**. Un matériau généré vide par un run
antérieur au correctif du `LoadTexture` à deux emplacements (voir plus haut) n'était donc
**jamais régénéré**, même après la correction du code — repéré sur `Ru_wood0022.mat`
(`G_Ruin_T`), vide depuis le 29/07, utilisé 5 fois dans 17_23, alors que sa texture
`RU_wood_002.png` existe juste à côté. **Corrigé** : le saut ne s'applique plus qu'aux
matériaux déjà **texturés** — un matériau vide est désormais toujours retenté.

### Rendu bleuté / trop pâle sur une région neuve

**Diagnostic revu après comparaison des 4 régions de référence.** L'hypothèse initiale
(« il manque des `ReflectionProbe`/`Light`, à poser à la main ») ne tient pas : sur
16_24, 16_25, 17_24, 17_25, **3 régions sur 4 (16_24, 16_25, 17_24) n'ont NI `Light` NI
`ReflectionProbe` du tout** — vérifié directement dans les `.unity` (`grep -c "!u!108"` /
`"!u!215"`). Seule 17_25 en a (40 `Light`, 5 `ReflectionProbe`). Une région sans aucune
des deux n'est donc pas anormale : c'est la situation de la **majorité** des régions
propres, pas une région inachevée. Poser une grille automatique de sondes sur chaque
région importée ne correspondrait pas à cette convention — `L2ReflectionProbeBuilder`
existe (menu `L2/Debug/Light - (Terrain) Build reflection probe grid`) mais n'est
**plus appelé par défaut** dans `RunImport`, à réserver aux cas où on juge, au cas par
cas, qu'une région en tirerait un vrai bénéfice.

Si le halo bleuté persiste sur une région après avoir posé l'eau et le safenet (voir
ci-dessous), comparer d'abord avec 17_24/16_24/16_25 en jeu — si elles ont le même rendu,
ce n'est pas un défaut de 17_23 mais l'apparence normale d'une région sans éclairage
posé à la main.

### Eau et filet de sécurité (`Safenet`)

**Objets FIXES, pas dérivés du terrain de chaque région.** Vérifié sur les 4 régions de
référence : l'objet `Water` a exactement la même échelle locale (104.2, 0.1, 104.2) et la
même position locale en X/Z (52.1, 52.1) partout, seul le Y variant à peine (109.9 à
110.2) ; `Safenet` (un plan Unity par défaut, invisible, avec un `MeshCollider` plein) a
lui aussi une échelle (62.41525) et une position locale (≈312.08, ≈0, ≈312.08)
strictement identiques sur les 4 régions. Ce ne sont donc **pas** des dalles mises à
l'échelle du terrain de chaque région — une première version de `L2WaterBuilder` faisait
cette erreur et produisait une eau bien trop grande sur 17_23.

`L2WaterBuilder` et `L2SafenetBuilder` clonent maintenant ces objets **tels quels**
(même transform locale) depuis une région de référence (17_25 par défaut), en enfant du
`Terrain` de la région ciblée — aucun calcul à partir du `WaterVolume` du `.unr` ou de la
taille du terrain. Un ajustement fin en X/Z peut rester nécessaire selon la position de la
région dans la grille (une région voisine dans une direction différente peut avoir besoin
d'un léger décalage, comme pour le raccord de terrain de l'étape 11 du menu L2).

Le composant `WaterVolumeBase` du plugin StylisedWater a un champ `RealtimeUpdates` :
coché, il reconstruit le mesh de l'eau **à chaque image** (et journalise à chaque fois)
même hors Play Mode — normal pendant le réglage, à décocher une fois la forme validée.

**Automatisé depuis peu, y compris pour les régions déjà importées.** `RunImport`
enchaîne désormais l'eau et le safenet pour toute **nouvelle** région. Pour une région
importée *avant* que ces étapes existent (17_23), ouvrez sa scène puis lancez
**`L2/Retrofit/Ajouter eau + safenet`** : ça n'ajoute que ces deux objets sans
rejouer tout `RunImport` (donc sans régénérer terrain/static meshes/matériaux déjà
valides), et ne resauvegarde que le prefab du Terrain (Water/Safenet y sont enfants).

**Musique : pas encore automatisée, mais la donnée source existe.** Comme l'eau, un
`MusicVolume` référence une géométrie réelle via `Brush=Model'...'` — même mécanique que
les `Brush` déjà exploités par l'étape 07. La zone `MusicArea` de référence (`Church` sur
17_25) reprend cette géométrie réelle (mesh ProBuilder + `MeshCollider` trigger), avec un
identifiant `nMusicID` côté `.unr` qui ne correspond à aucun événement FMOD par nom —
reste à trouver une table de correspondance ID → piste avant de pouvoir brancher l'audio
automatiquement.

### Autres symptômes rapides

| Symptôme | Cause |
|---|---|
| `No t3d for map {nom} at path …` | dossier et fichier `.t3d` doivent porter le même nom |
| `Introuvable : {mapId}.t3d` (PowerShell) | le `.unr` correspondant n'existe pas dans le client |
| Couture verticale entre deux régions | région absente de `StitchableRegions`, ou étape 11 jamais lancée |
| Terrain visible, gameplay incohérent | geodata serveur absente pour cette région (§10) |
| Mobs qui tirent à travers un mur | bâtiment absent de la geodata — cas 2 (§12) |
| `[Brush] ... ne contient aucun polygone` | normal si le `.t3d` vient de `l2-map-export` (pas de bloc `Model`) ; bascule automatique sur `Brushes.json` |
