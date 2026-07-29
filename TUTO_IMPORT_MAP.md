# Importer et customiser une région de map

Pipeline **client officiel Interlude → Unity**, puis customisation.
Établi par audit du code du projet (2026-07-27).

> **Statut de vérification.** Chemins, formats, constantes, valeurs codées en dur et
> ordre du pipeline ont été lus directement dans le code — marqués ✅.
> Ce qui reste à confirmer à la première exécution réelle est marqué ⚠️.

## Sommaire

**Partie I — Comprendre**
1. [Il y a deux geodata](#1-il-y-a-deux-geodata-et-elles-sont-incompatibles)
2. [Les maths de la grille](#2-les-maths-de-la-grille)
3. [Ce qui existe déjà](#3-ce-qui-existe-déjà)

**Partie II — Préparer**
4. [Outils externes](#4-outils-externes)
5. [Les chemins codés en dur à corriger](#5-les-chemins-codés-en-dur-à-corriger)
6. [Extraire depuis le client officiel](#6-extraire-depuis-le-client-officiel)

**Partie III — Exécuter**
7. [Pas à pas, du début à la fin](#7-pas-à-pas-du-début-à-la-fin)
8. [Les 11 étapes du menu Shnok](#8-les-11-étapes-du-menu-shnok)
9. [Générer la geodata client](#9-générer-la-geodata-client)
10. [Déclarer la région côté serveur](#10-déclarer-la-région-côté-serveur)

**Partie IV — Customiser**
11. [Ce qui est personnalisable sans risque](#11-ce-qui-est-personnalisable-sans-risque)
12. [Static meshes et collision](#12-static-meshes-et-collision)
13. [Pièges connus](#13-pièges-connus)

---

# Partie I — Comprendre

## 1. Il y a deux geodata, et elles sont incompatibles

C'est le point qui fait perdre le plus de temps si on l'ignore.

| | Geodata **client** | Geodata **serveur** |
|---|---|---|
| Sert à | pathfinding local | hauteur, ligne de vue, IA des PNJ |
| Format | triplets bruts `short x, y, z` ✅ | `.l2j` + `_conv.dat` ✅ |
| Produit par | `GeodataGenerator` + `GeodataExporter` ✅ | **pas par ce projet** |
| Emplacement | export libre sous `Assets/` ✅ | `gameserver/data/geodata/` ✅ |

**L'outil Unity ne sait pas fabriquer la geodata du serveur.** Pour une nouvelle région,
il faut récupérer le `.l2j` depuis un pack geodata L2J Interlude — pas le générer.

**Bonne nouvelle** ✅ : le serveur **ne bloque pas** le déplacement du joueur avec la
geodata. `getValidLocation()` ne sert qu'à valider des positions aléatoires (spawns,
téléports à offset), et `ValidatePosition` fait un contrôle de *vitesse* puis accepte la
position envoyée par le client. C'est donc la collision **côté client** qui fait le
travail pratique — et celle-là, vous la maîtrisez entièrement.

---

## 2. Les maths de la grille

Constantes lues dans `GeoStructure.java` et `World.java` ✅ :

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

| Lieu | Coordonnées | Région | Présente ? |
|---|---|---|---|
| Talking Island village | x −84000, y 244000 | `17_25` | oui |
| Château de Gludio | x −18000, y 110000 | `19_21` | non |

---

## 3. Ce qui existe déjà

Quatre régions contiguës seulement, autour de Talking Island :

```
16_24   17_24
16_25   17_25
```

Chacune existe en trois exemplaires, qui doivent rester alignés :

| Quoi | Où |
|---|---|
| Scène Unity | `Assets/Resources/Scenes/{région}.unity` |
| Données terrain | `Assets/Resources/Data/Maps/{région}/` |
| Geodata serveur | `gameserver/data/geodata/{région}.l2j` + `_conv.dat` |

Le terrain utilise **MicroSplat**.

---

# Partie II — Préparer

## 4. Outils externes

| Outil | Sert à |
|---|---|
| **l2pe** | métadonnées : `TerrainInfo0`, acteurs StaticMesh, props de matériaux |
| **umodel** | textures, modèles, sons |
| **l2tool** | heightmap au format G16 |
| **Blender 3.6.1** + plugin PSK/PSA | conversion des modèles 3D |
| Convertisseur DDS | conversion des textures |
| **l2-brush-export** (jar, dépôt `shnok/l2-brush-export`) | métadonnées des brushes |

---

## 5. Les chemins codés en dur

> ✅ **Déjà corrigés le 2026-07-27** — ils pointaient vers la machine de l'auteur
> d'origine (`D:\Stock\…`, `G:\Stock\…`). Ils visent désormais le dossier de travail
> `D:\Jeux\MAP_L2Unity\` (voir son propre `README.md`).

| Fichier | Ligne | Pointe vers |
|---|---|---|
| `L2T3DStaticMeshImporter.cs` | 21 | `MAP_L2Unity\export` |
| `L2JSONBrushImporter.cs` | 21 | `MAP_L2Unity\export` |
| `L2JSONAmbientSoundImporter.cs` | 18 | `MAP_L2Unity\export_sound` |
| `L2JSONAmbientSoundImporter.cs` | 20 | `MAP_L2Unity\export_sound\unityexport` |
| `Tools/Blender/DetailMeshExport.py` | 11–12 | `MAP_L2Unity/export/field_deco_S` |

`umodel.cfg` a également été repointé sur `MAP_L2Unity\export`.

### Deux valeurs figées, plus subtiles

Celles-ci n'échouent pas : elles travaillent silencieusement à côté.

**Étape 03 — mauvaise map.** `L2TerrainGeneratorTool.cs:112` contient
`data.mapName = "l2_lobby";`. Contrairement aux étapes 04/05/06 qui demandent le
fichier, l'étape 03 génère **toujours** les static meshes de `l2_lobby`, quelle que soit
la map sur laquelle vous travaillez.

**Étape 11 — couture entre régions.** `L2TerrainGeneratorTool.cs:128-136` : la liste des
régions à raccorder est écrite en dur (`17_25`, `16_25`, `16_24`, `17_24`). Ajouter une
région oblige à y ajouter sa ligne.

---

## 6. Extraire depuis le client officiel

1. Exporter `TerrainInfo0` et les acteurs StaticMesh depuis le dossier `system` du
   client, avec **l2pe** — c'est lui qui produit les métadonnées, pas UnrealEd.
2. Extraire StaticMeshes, textures et sons avec **umodel**.
3. Extraire la heightmap G16 avec **l2tool**.
4. Convertir textures et modèles si nécessaire.

Puis placer le fichier de métadonnées à l'emplacement **exact** attendu :

```
Assets/Resources/Data/Maps/{nom}/Meta/{nom}.t3d
```

✅ Chemin codé dans `StaticMeshUtils.GetT3DPath()` — le dossier et le fichier doivent
porter le même nom. Nommez la map avec l'identifiant de région.

Le parser lit le bloc `Begin Actor Class=TerrainInfo Name=TerrainInfo0`, d'où il tire
l'échelle du terrain, sa position, les couches UV et les couches de déco. ✅

### Arborescence attendue par l'étape 01

L'importateur lit, sous la racine d'export umodel :

```
{export}/{dossier}/{nom}.fbx                         ← le mesh
{export}/{dossier}/StaticMesh/{nom}.props.txt        ← infos de texture
{export}/{dossierTexture}/{nom}.png                  ← les textures
{export}/{dossierTexture}/Materials/{nom}.props.txt  ← props de matériau
```

⚠️ Le dossier de textures est déduit du dossier de mesh par convention de suffixe
(`_t`, `_tx`) — à confirmer à l'usage.

---

# Partie III — Exécuter

## 7. Pas à pas, du début à la fin

**1. Choisir la région.** Prenez les coordonnées connues du lieu visé et appliquez la
formule de la section 2. Notez l'identifiant `{X}_{Y}` : il servira de nom partout —
dossier, `.t3d`, scène, geodata.

**2. Récupérer la geodata serveur.** Avant tout travail Unity. Placez `{région}.l2j` et
son `_conv.dat` dans `gameserver/data/geodata/`. Si vous ne l'avez pas, arrêtez-vous
ici : le reste serait du décor sans gameplay correct.

**3. Extraire les données du client.** l2pe, umodel, l2tool. Rangez les sorties umodel
dans un dossier unique — c'est lui que vous indiquerez ensuite.

**4. Corriger les chemins codés en dur.** Section 5. Cinq minutes qui évitent une heure
d'incompréhension.

**5. Placer le `.t3d`.** Dans `Assets/Resources/Data/Maps/{région}/Meta/{région}.t3d`.
Si le nom du dossier et celui du fichier diffèrent, l'outil ne trouvera rien.

**6. Dérouler les étapes 01 → 06.** Menu `Shnok`, dans l'ordre. Les étapes 04, 05 et 06
demandent le `.t3d`. À la fin, vous avez un terrain texturé sous MicroSplat — vérifiez
visuellement avant de continuer.

**7. Décor et ambiance — étapes 07 → 10.** Brushes, caméras, sons. Les brushes ont leur
chaîne à part : compiler `l2-brush-export`, exporter les métadonnées, importer les
textures via `L2BrushImporter`, construire avec `L2BrushBuilder`, sauvegarder le prefab.

**8. Raccorder les bords — étape 11.** Uniquement après avoir ajouté votre région à la
liste codée en dur. Sinon l'étape s'exécute sans elle et la couture reste.

**9. Créer la scène.** Sauvegardez sous `Assets/Resources/Scenes/{région}.unity`, puis
ajoutez-la aux Build Settings comme les quatre autres.

**10. Générer la geodata client.** Composant `GeodataGenerator`, en Play Mode. Commencez
sur une portion réduite pour valider vos réglages.

**11. Tester en réseau.** Déplacez-vous dans la région **avant** toute customisation.
C'est le point de contrôle : si ça marche ici et casse plus tard, la cause est votre
modification.

---

## 8. Les 11 étapes du menu Shnok

Les numéros ne sont pas décoratifs : c'est l'ordre d'exécution. ✅

| # | Entrée de menu | Rôle |
|---|---|---|
| 01 | `[StaticMeshes] Import Textures and models` | meshes + textures extraits du client |
| 02 | `[Material] Generate materials` | crée les matériaux |
| 03 | `[StaticMeshes] Generate staticmeshes` | instancie les meshes — **map figée sur `l2_lobby`** |
| 04 | `[Terrain] Generate terrain` | **cœur** : heightmap, couches UV, couches déco |
| 05 | `[Terrain] Convert terrain to microsplat` | bascule sous MicroSplat |
| 06 | `[Terrain] Update microsplat params` | échelles et paramètres par texture |
| 07 | `[Brush] (T3D) Build brushes` | géométrie brush — bâtiments, volumes |
| 08 | `[Camera] (T3D) Build cameras` | caméras définies dans la map |
| 09 | `[AmbientSound] (T3D) Import sounds` | importe les sons d'ambiance |
| 10 | `[AmbientSound] (T3D) Build ambient sounds` | les place dans la scène |
| 11 | `[Terrain] Stitch terrain seams` | **raccorde les bords** — liste codée en dur |

### Les entrées `[Debug]`

| Entrée | Rôle |
|---|---|
| `Rescale Meshes` | corrige les meshes sous `StaticMeshes` dont l'échelle est tombée sous 0.1 |
| `Rescale Decos` / `Rescale Trunks` | même correction sur décos et troncs |
| `Add trunks to trees` | ajoute les troncs manquants aux arbres |
| `Convert terrain to mesh` | contourne la limite URP (voir ci-dessous) |
| `Generate deco layer mesh` | génère le mesh de la couche de déco |
| `Upgrade unity2022 transparent mats` | migration de matériaux transparents |
| `(JSON) Build brushes / sounds / meshes` | variantes JSON des builders T3D |

**Pourquoi convertir le terrain en mesh ?** URP limite le nombre de couches pouvant être
éclairées sur un terrain Unity en éclairage temps réel. Le passage en mesh contourne
cette limite — il reste à sauvegarder le GameObject en prefab.

---

## 9. Générer la geodata client

Composant `GeodataGenerator`. Il travaille par **raycasts** et s'exécute au `Start()` —
donc en Play Mode. ✅

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
32768 unités. ✅

---

## 10. Déclarer la région côté serveur

1. Placer `{région}.l2j` et son `_conv.dat` dans `gameserver/data/geodata/`. ✅
   Le nom est construit par `GeoEngine.java:120` via `Config.GEODATA_TYPE.getFilename()`.
2. Vérifier que la région tombe dans `TILE_X_MIN..MAX` / `TILE_Y_MIN..MAX` — elles
   couvrent déjà tout Interlude, donc aucune modification attendue. ✅

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

### Comment les maps existantes gèrent leurs static meshes ✅

Relevé sur la région `17_25` (Talking Island), pour servir de modèle.

**Un prefab unique par région.** `Assets/Resources/Data/Maps/{région}/StaticMeshes.prefab`
regroupe tout : 1288 objets, 1154 `MeshRenderer`, 2230 `MeshCollider` (plusieurs par
objet quand le mesh est découpé). À côté, `Brushes.prefab`, `BoxVolumes.prefab`,
`{région}_AmbientSounds.prefab`, `MusicArea.prefab`.

**Répartition par layer** — c'est ce qui pilote la geodata client :

| Layer | Nom | Objets | Rôle |
|---|---|---|---|
| 7 | `StaticMesh` | 1015 | le gros du décor, bâtiments compris |
| 16 | `Unwalkable` | 149 | surfaces sur lesquelles on ne doit pas marcher |
| 19 | `StreetLight` | 26 | lampadaires |
| 18 | `Light` | 14 | lumières |
| 3 | `Terrain` | 10 | éléments de terrain |

Ces layers correspondent directement aux masques du `GeodataGenerator`
(`walkableMask`, `obstacleMask`, `allowWalkMask`). **Un bâtiment ajouté doit donc être
placé sur le bon layer**, sinon il n'apparaîtra pas dans la geodata client.

**Nommage des meshes** : `{package}_s.{Nom}` — par exemple `SI_V_S.SI_H01`
(Speaking Island Village, maison 01), `SI_H01_Rf` (son toit), `speaking_magic_s.SI_Magic_Hall`.
Le préfixe correspond au dossier d'export umodel.

> **À noter** : `SI_V_S.SI_Agit` et `SI_V_S.SI_CH_Body` sont déjà présents dans `17_25` —
> ce sont des **clan halls** (*agit* est le terme L2 pour clan hall, *CH* pour Clan Hall).
> Des bâtiments de ce type existent donc déjà dans une région que vous maîtrisez.

**Placement** : décrit dans `Meta/StaticMeshActor.json`. Racine = origine de la région,
puis un tableau `staticMeshes[]` :

```json
{"x":-81920.0,"y":245760.0,"z":0.0,"staticMeshes":[
  {"staticMesh":"speaking_magic_s.SI_Magic_Hall",
   "actorClass":"Engine.StaticMeshActor",
   "x":-8294.398,"y":2799.375,"z":-2390.0,
   "yaw":22528,"roll":0,"pitch":0}
]}
```

Les rotations sont en unités Unreal (65536 = 360°). C'est ce fichier qu'on enrichit pour
poser un bâtiment supplémentaire.

### Cas 3 — la voie propre, sans toucher au `.l2j`

**Les portes le font déjà** ✅ : `Door.java` appelle `GeoEngine.addGeoObject(this)` à la
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
travail est la génération de l'empreinte `byte[][]`. ⚠️

> **Recommandation.** Récupérez meshes *et* geodata des régions officielles de château :
> tout concorde, la zone est testable immédiatement. Gardez `IGeoObject` en réserve pour
> le jour où vous voudrez un bâtiment custom réellement solide — chantier isolé, qui ne
> bloque rien aujourd'hui.

---

## 13. Pièges connus

| Symptôme | Cause |
|---|---|
| `No t3d for map {nom} at path …` | dossier et fichier doivent porter le même nom |
| L'étape 01 ne trouve aucun mesh | `dataFolder` encore sur `D:\Stock\…` (§5) |
| L'étape 03 travaille sur la mauvaise map | `mapName` figé sur `l2_lobby` (§5) |
| Couture verticale entre deux régions | région absente de la liste codée en dur (§5) |
| Terrain visible, gameplay incohérent | geodata serveur absente pour cette région |
| Mobs qui tirent à travers un mur | bâtiment absent de la geodata — cas 2 (§12) |
| Particules invisibles depuis un mesh | `Read/Write Enabled` non coché — au prix d'une copie du mesh en RAM |

### Reste à confirmer ⚠️

1. Convention exacte de suffixe des dossiers de textures à l'étape 01.
2. Provenance du `.l2j` pour une nouvelle région — pack L2J existant ou conversion.
3. Temps de génération réel sur une région complète.
4. Faisabilité de l'empreinte `byte[][]` pour un bâtiment.
