# Guide — Éditer et embellir une région

Comment retoucher une région **soi-même** dans Unity : textures du sol, plans
d'eau, peinture manuelle, décor.

Complète les deux autres guides :

| Document | Sujet |
|---|---|
| [TUTO_IMPORT_MAP.md](TUTO_IMPORT_MAP.md) | comment **fonctionne** le pipeline |
| [GUIDE_IMPORT_MASSE.md](GUIDE_IMPORT_MASSE.md) | **importer et raccorder** les 153 régions |
| **ce document** | **retoucher** une région à la main |

Établi le 2026-08-04.

---

## 0. La règle à connaître avant tout : qu'est-ce qui survit à quoi ?

C'est **la** chose à comprendre. Chaque type de travail vit dans un fichier
différent, et chaque opération n'en détruit que certains.

| Ton travail | Où il est stocké | Réimport complet | Stitch | Re-substitution |
|---|---|---|---|---|
| Peinture du terrain | `{région}.asset` (splatmaps) | ❌ perdu | ✅ gardé | ✅ gardé |
| Raccord de terrain | `{région}.asset` (hauteurs) | ❌ perdu | — | ✅ gardé |
| Plans d'eau, objets ajoutés | `{région}.prefab` | ❌ perdu | ✅ gardé | ✅ gardé |
| Réglages de textures | l'asset de réglages | ✅ gardé | ✅ gardé | ✅ gardé |
| Correction d'un `.mat` | `Data/Textures/…/Materials/` | ✅ gardé | ✅ gardé | ✅ gardé |

> **Une seule opération est destructrice : le réimport complet.** Tout le reste
> préserve ton travail. Évite donc de réimporter une région que tu as retouchée.

**Conséquence pratique** : tu peux éditer tes maps **avant** de stitcher sans
rien risquer. Le stitch ne touche qu'aux hauteurs de bordure.

---

## 1. Les textures du sol

### 1.1 Comprendre : le « où » et le « quoi »

Un terrain peint, ce sont deux informations distinctes :

- **Le « où »** — les **masques** (splatmaps) disent à quel endroit il y a de
  l'herbe, du sable, de la roche. Ce sont des images 1024×1024, une par couche,
  ~9 couches par région. C'est le travail des level designers de 2006.
- **Le « quoi »** — la **texture** dit à quoi ressemble « l'herbe ».

**Substituer** = changer la texture sans toucher aux masques. Une ligne de
réglage repeint des dizaines de régions en conservant la composition d'origine.

**Peindre** = redessiner les masques. Contrôle total, mais travail manuel.

Les deux sont complémentaires : substitue d'abord (gratuit, global), peins
ensuite là où ça ne suffit pas.

> **MicroSplat n'est pas le système de substitution.** C'est une technologie de
> *rendu* : elle empaquette les textures dans des *texture arrays* pour dessiner
> le terrain en une seule passe. La substitution est une couche ajoutée par
> notre pipeline par-dessus.

### 1.2 L'asset de réglages

Tout se pilote depuis un asset éditable dans l'Inspector — **sans toucher au
code ni recompiler**.

**Première fois :** `Shnok/[Textures] Créer l'asset de réglages (pré-rempli)`

Il crée `L2TerrainTextureSettings.asset` rempli avec les réglages existants.

Trois sections :

| Section | À quoi ça sert |
|---|---|
| **Substitutions globales** | texture L2 → pack PBR → échelle. S'applique partout. |
| **Échelles par défaut, par pack** | le réglage de référence d'un pack. |
| **Surcharges par région** | quand une texture doit rendre différemment ici et là. |

### 1.3 La règle des échelles

L'échelle est le nombre de fois que la texture se répète sur les 624 unités du
terrain. **Échelle 1 = la texture étirée une seule fois** → énorme et floue.
Échelle 64 = motif fin.

L'échelle se résout en trois niveaux, du plus précis au plus général :

1. Surcharge de région
2. Échelle de la substitution (par texture L2)
3. **Échelle par défaut du pack**

> **Laisse l'échelle à `0` pour hériter de celle du pack.** C'est le réglage à
> privilégier : tu règles le pack une fois, toutes ses textures suivent.

⚠️ **Deux barèmes incompatibles cohabitent** dans les valeurs héritées : les
textures de Talking Island (`SL_*`, `Base`, `WR_*`) sont entre **1 et 7**,
celles de Gludio (`GU*`) à **64**. Ce n'est pas artistique, c'est un héritage.
Les valeurs basses sont très probablement fausses — c'était la cause du
problème d'échelle sur Icelandic (`SL_C` était à 1).

**Pour corriger** : mets ces échelles à `0` et ajuste le pack.

### 1.4 La boucle de travail

1. Ouvre la scène d'une région
2. Modifie une valeur dans l'asset
3. `Shnok/[Textures] Re-appliquer les substitutions (scène ouverte)`
4. Regarde, ajuste, recommence

Quelques secondes par essai. Quand tes choix sont figés :
`Shnok/[Textures] Re-appliquer les substitutions (TOUTES les régions)`.

### 1.5 Améliorer une texture existante

Remplace les fichiers du pack dans `Assets/Resources/Data/External/Textures/`,
**en gardant exactement les mêmes noms** (`<nom>_BaseColor.jpg`, `_Bump`,
`_AO`, `_Normal`, `_Roughness`, `_Specular`).

**C'est automatique et rétroactif** : MicroSplat détecte le changement par hash
et repacke ses arrays tout seul. Aucune manipulation.

⚠️ Ça peut recompiler beaucoup de configs — à lancer quand tu n'as pas besoin
de l'éditeur.

### 1.6 Ajouter un nouveau pack PBR

Dépose un dossier dans `Data/External/Textures/` en respectant la convention de
nommage ci-dessus, puis référence-le par son nom dans l'asset.

> Il **n'existe aucun pack de neige** pour l'instant, alors que les textures
> `SCSN*` de Schuttgart (12 régions) en auraient besoin.

### 1.7 Le terrain blanc et miroitant

**Le symptôme** : en orbitant autour d'une région, le terrain bascule du noir au
blanc et renvoie la lumière comme un miroir — au point de ne plus rien pouvoir
juger. Les régions de référence (`16_24`, `16_25`, `17_24`, `17_25`) n'ont pas
le problème ; presque toutes les autres l'ont.

**La cause, en deux temps.**

D'abord, le pipeline force MicroSplat à lire la brillance dans le canal **alpha**
de la texture de base. Une texture opaque a un alpha de 255, donc une brillance
de **1.0** — un miroir. Mesure : `18_19` → **24 couches sur 24** sans carte de
brillance ; `24_18` → **30 sur 30** ; `17_24` → seulement 2 sur 3.

Ensuite — et c'est le vrai blocage — **les régions importées n'ont pas les
réglages par texture activés**. Comparaison des mots-clés du shader :

| | Mots-clés actifs |
|---|---|
| `17_24`, `16_25` (référence) | **20** |
| `18_19`, `24_18` (importées) | **10** |

Les dix manquants sont exactement les réglages par texture : `_PERTEXSMOOTHSTR`,
`_PERTEXNORMSTR`, `_PERTEXTINT`, `_PERTEXBRIGHTNESS`, `_PERTEXCONTRAST`,
`_PERTEXSATURATION`, `_PERTEXHEIGHTOFFSET`, `_PERTEXHEIGHTCONTRAST`, plus
`_TRIPLANAR` et `_TRIPLANARHEIGHTBLEND`.

> **Conséquence à retenir** : sur une région importée, régler une valeur par
> texture dans MicroSplat ne produit **rien** — son shader ne lit même pas ces
> cases. Il faut d'abord activer les fonctionnalités.

**Le correctif** :

`Shnok/[Textures] Aligner MicroSplat sur les références (scène ouverte)`

ou, pour tout traiter d'un coup, la variante `(TOUTES les régions)`.

Il active les fonctionnalités manquantes **et** sème des valeurs saines reprises
des régions de référence. Les deux vont ensemble : un propdata vierge vaut zéro,
et zéro n'est pas neutre — `_PERTEXNORMSTR` à zéro aplatirait tout le relief.

**L'homogénéité est garantie par construction** : la bibliothèque est indexée par
nom de texture finale. Une même texture rencontrée dans deux régions reçoit donc
exactement les mêmes réglages. Régler une texture une fois suffit à la régler
partout.

Les quatre régions de référence (`17_24`, `17_25`, `16_24`, `16_25`) servent de
modèle et ne sont **jamais** modifiées par l'outil.

### 1.8 La limite des 32 textures

MicroSplat Core gère **32 textures maximum** par terrain. Le module « 256
Textures » qui lèverait cette limite est payant et **n'est pas installé** (seuls
`core`, `triplanar` et `urp2022` le sont).

**Dix régions dépassent déjà cette limite** :

| Couches | Régions |
|---|---|
| 42 | `22_14` |
| 36 | `24_24`, `23_14`, `22_23`, `20_17` |
| 33 | `23_13`, `22_21`, `21_14`, `18_23`, **`16_25`** |

⚠️ `16_25` est une de tes **régions de référence** — à vérifier avant de trop
t'appuyer dessus.

Pour ces régions, il faut fusionner des couches proches (deux herbes quasi
identiques → une seule) avant d'espérer un rendu correct.

### 1.9 Pourquoi un MicroSplat par région, et pas un seul partagé

La documentation MicroSplat est explicite : des terrains ne peuvent partager un
MicroSplat que s'ils ont **exactement les mêmes couches, dans le même ordre**.

Nos régions ont de 3 à 42 couches, toutes différentes. Un MicroSplat unique est
donc exclu — et avec 379 textures distinctes pour une limite de 32, ce n'était de
toute façon pas envisageable.

> **L'homogénéité ne vient donc pas du partage, mais de la propagation** : chaque
> région garde son MicroSplat, et l'alignement sur les références y réinjecte les
> mêmes réglages pour les mêmes textures. Le résultat visuel est le même, sans la
> contrainte de couches identiques.

> ✅ **Opération non destructrice** : contrairement à la re-substitution, elle ne
> régénère **pas** `MicroSplatData/`. Tous tes réglages MicroSplat faits à la
> main dans le prefab sont conservés. Tu peux donc la lancer à tout moment, y
> compris sur une région déjà retouchée.

C'est un **correctif d'affichage**, pas un embellissement : il rend le sol mat
pour que tu puisses travailler. Substituer la texture par un pack PBR lui rendra
ensuite un vrai rendu.

Les imports **futurs** l'appliquent automatiquement — c'est uniquement pour les
régions déjà importées.

---

## 2. Les plans d'eau

### 2.1 Ce qu'est l'objet `Water`

C'est un GameObject ordinaire, **enfant du Terrain** dans `{région}.prefab`, sur
le layer **4 (Water)**. Il porte :

| Composant | Rôle |
|---|---|
| `WaterVolumeTransforms` | génère le maillage de la surface |
| `WaterVolumeHelper` | lie le rendu au volume |
| MeshRenderer | matériau `example-water-01.mat` (plugin StylisedWater) |
| MeshFilter | **sans maillage** — il est généré par le composant |

Valeurs de référence (copiées de `17_25`) :

```
Position locale : 52.1 / ~110 / 52.1
Échelle         : 104.2 / 0.1 / 104.2
```

### 2.2 Pourquoi tu as des lacs indésirables

Le pipeline copie l'objet `Water` d'une région de référence **à l'identique**,
avec la même hauteur, sur **toutes** les régions.

C'est faux, et je l'assume : la mesure sur les données du client montre que

- **126 régions sur 149** ont réellement de l'eau — donc **23 n'en ont pas** et
  en ont reçu une quand même ;
- les hauteurs réelles vont de **-2301 à -4768** unités Unreal, soit environ
  **45 unités Unity d'écart**.

Une région dont l'eau devrait être basse reçoit celle de `17_25`, bien plus
haute → elle noie le terrain.

### 2.3 Corriger, cas par cas

Ouvre le prefab de la région, sélectionne l'objet `Water` :

- **Pas d'eau du tout ?** Supprime l'objet.
- **Trop haute / trop basse ?** Ajuste le **Y** de la position locale.
- **Trop grande ?** Réduis l'échelle **X** et **Z** (garde Y à 0.1).

### 2.4 Plusieurs plans d'eau (un lac + une mer)

**Rien n'impose que `Water` soit unique.** Tu peux :

1. Sélectionner l'objet `Water`, **Ctrl+D** pour le dupliquer
2. Renommer la copie (`Lac_nord`, `Riviere`…) — le nom est libre
3. Lui donner **sa propre hauteur** (Y) et **sa propre taille** (échelle X/Z)

Une mer large et basse couvrant la région, plus un petit lac haut perché dans
une vallée : parfaitement faisable.

> **Pourquoi ça vaut mieux qu'une automatisation** : le client ne fournit
> **qu'une seule** hauteur d'eau par région, alors que certaines en ont
> visiblement plusieurs. Aucun script ne ferait mieux que ton œil.

⚠️ **Le nom `Water` est réservé** : l'outil de rattrapage
`Shnok/[Retrofit] Ajouter eau + safenet` écrase l'objet portant ce nom exact.
Tes copies renommées ne risquent rien.

### 2.5 Le piège `RealtimeUpdates`

Le composant a une case **`RealtimeUpdates`** : cochée, il reconstruit le
maillage de l'eau **à chaque image** et journalise à chaque fois, même hors
Play Mode. Normal pendant le réglage, **à décocher une fois la forme validée**.

---

## 3. Peindre le terrain à la main

L'outil natif d'Unity fonctionne : MicroSplat lit les mêmes splatmaps
(`terrainData.alphamapTextures`) que celles qu'écrit *Paint Terrain*.

**Comment faire :** sélectionne le Terrain → onglet **Paint Terrain** →
**Paint Texture** → choisis une couche → peins.

Trois limites :

- Tu ne peux peindre qu'avec les **couches déjà présentes** sur la région
  (celles héritées du client). En ajouter une passe par MicroSplat.
- La peinture est **conservée** par le stitch et par la re-substitution.
- Elle est **perdue par un réimport complet**.

---

## 4. Corriger les matériaux (objets rouges, gris)

### 4.1 Les surfaces rouges

Le rouge est un **matériau de secours** : `Assets/Prefab/Red.mat`. Il s'applique
quand la texture d'un *brush* est introuvable — souvent parce que la référence
est **morte dans le client lui-même** (vérifié : `SSQ_ground01`,
`dark_dgn_009`… n'existent dans aucun `.utx`).

Il touche **127 régions** et concerne aussi bien du ciel que **des sols et des
murs de donjon** — ne le rends donc pas transparent, tu créerais des trous.

Deux approches :

- **Globale** : donne à `Red.mat` une apparence de pierre neutre. Un seul
  fichier, 127 régions corrigées d'un coup.
- **Cas par cas** : assigne un matériau adapté sur l'objet concerné.

### 4.2 Les objets gris

Un objet gris = un matériau **sans texture**. Corrige l'asset `.mat` dans
`Data/Textures/<package>/Materials/`.

> ✅ Corriger le `.mat` → **partagé entre toutes les régions, versionné, survit
> aux réimports** (le pipeline saute les matériaux déjà texturés).
> ❌ Corriger les slots sur l'objet posé dans la scène → **perdu au réimport**.

**Limite connue** : les meshes multi-matériaux (format props « à blocs ») n'ont
qu'un seul `.mat` généré là où ils en attendent jusqu'à 17. Ils sortiront gris
et demandent une correction manuelle.

---

## 5. Ordre de travail conseillé

1. **Textures** — substitutions et échelles, région par région
2. **Eau** — ajuster, supprimer, dupliquer selon le terrain
3. **Matériaux** — rouge et gris
4. **Stitch** — le raccord des régions
5. **Peinture manuelle** — les finitions

Les étapes 1 à 3 ne touchent ni aux hauteurs ni aux splatmaps : elles peuvent se
faire **avant ou après** le stitch, sans rien perdre. Seule la peinture gagne à
venir après le raccord, pour peindre par-dessus une jointure déjà correcte.

**Ne réimporte jamais une région retouchée** — c'est la seule opération qui
détruit ce travail.

---

## 6. Aide-mémoire des entrées de menu

| Menu | Effet |
|---|---|
| `Shnok/[Textures] Créer l'asset de réglages` | crée la table éditable, pré-remplie |
| `Shnok/[Textures] Aligner MicroSplat sur les références (scène ouverte)` | active les réglages par texture, sème les valeurs des régions de référence |
| `Shnok/[Textures] Aligner MicroSplat sur les références (TOUTES les régions)` | idem sur tout le monde |
| `Shnok/[Textures] Re-appliquer… (scène ouverte)` | applique tes réglages à la région ouverte |
| `Shnok/[Textures] Re-appliquer… (TOUTES les régions)` | idem sur tout le monde |
| `Shnok/[Retrofit] Ajouter eau + safenet` | repose `Water` et `Safenet` (écrase ces noms) |
| `Shnok/11. [Terrain] Stitch terrain seams` | raccorde les régions chargées dans la scène |
