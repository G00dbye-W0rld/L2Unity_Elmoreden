# Extracteur des definitions d'effets de skills

Genere les fichiers de `StreamingAssets/Data/LineageEffects` depuis
`LineageSkillEffect.u` du client Orfen, ou 4822 classes `l2_<id>_skill` decrivent
quels emetteurs jouer a l'incantation, au tir et a l'explosion.

Le client Interlude n'a pas ce paquet : la correspondance skill vers effet n'y
existe pas. La source est donc Orfen, et l'adaptation se fait cote serveur.

## Usage

    javac -cp ".;D:/Jeux/MAP_L2Unity/tools/L2pe-bin/L2pe.jar" Dump.java GenSkillEffects.java
    java  -cp ".;D:/Jeux/MAP_L2Unity/tools/L2pe-bin/L2pe.jar" GenSkillEffects \
          "<client Orfen>/system/LineageSkillEffect.u" <dossier de sortie> <liste d ids|->

La liste d'ids est un identifiant par ligne (les valeurs de `skill_visual_effect`
de Skillgrp) ; `-` traite les 4822 classes.

Les autres fichiers sont des outils de diagnostic : `Cls` affiche les proprietes
d'une classe, `Dump` celles de ses emetteurs, `Enums` et `Names` servent a relire
une enumeration du client.

## Pieges

- **Index compact Unreal : bit 7 = signe, bit 6 = continuation.** L'inverse passe
  inapercu quand les deux bits valent 1, et ne se voit que sur les tableaux.
- **L'ordre de la table de noms n'est pas celui de l'enumeration.** Six des sept
  enumerations coincidaient, `EParticleCoordinateSystem` est inversee. Toujours
  relire l'Enum du paquet (`Enums`), dont le bloc porte trois champs compacts
  avant le compteur.
- `SkillID` n'est serialise que s'il differe du defaut : beaucoup de classes ne
  l'ont pas et l'identifiant vient du nom. Le reperage des proprietes par defaut
  se fait donc par balayage, en n'acceptant qu'un bloc entierement connu.
- Les structures nommees (`PawnLightParam`) portent des proprietes taguees
  imbriquees. Seuls `Vector`, `Rotator` et `Color` sont atomiques ; `Plane` non.
- `ChannelingAction`, `UnionTargetAction` et les `*CameraEffectInfoClass` sont
  reconnues mais pas versees dans les trois phases que lit le client. Le
  programme les compte, il ne les perd pas.

## Recettes d'emetteurs

`GenRecipes` ecrit une recette `.uc` par classe d'effet, dans la syntaxe que lit
`L2ParticleEmitterParser`. Meme invocation, avec le paquet d'effets en entree :

    java -cp ".;D:/Jeux/MAP_L2Unity/tools/L2pe-bin/L2pe.jar" GenRecipes \
         "<client>/system/lineageeffect.u" <dossier de sortie> <liste de classes|->

**Interlude d'abord, Orfen en complement.** Des 464 classes que reclament les
definitions, 344 sont dans le client Interlude et 446 dans Orfen (les 344 sont un
sous-ensemble). Les 26 recettes faites a la main venaient d'Interlude : preuve
par les noms d'objets de `el_ice_bolt_ca`, que seule la version Interlude
reproduit. On genere donc depuis Interlude, puis on complete depuis Orfen.

**Les recettes generees sont plus justes que celles faites a la main.** Le
producteur d'origine omettait les valeurs par defaut, or `Range` vaut zero par
defaut cote parseur : un `ColorMultiplierRange` sans sa composante Z eteint le
canal bleu. 28 des 42 lignes des anciennes recettes etaient dans ce cas. Les
recettes generees portent toutes les composantes, telles que le paquet les donne.

Outils de diagnostic ajoutes : `Types` (types d'emetteurs par classe),
`VmRefs` (maillages animes references), `Cls2` (proprietes d'une classe).

## Dependances des recettes : maillages et textures

Les recettes referencent 205 maillages statiques et 230 textures. Un maillage
absent fait echouer l'emetteur avec un message clair
(`Couldn't load emitter mesh ...`) ; une **texture absente ne dit rien** et
produit un sprite invisible. Toujours verifier les deux.

Le chemin attendu par le parseur laisse tomber le groupe : la reference
`LineageEffectsStaticmeshes.Aura.auraburn00` se resout en
`Resources/Data/StaticMeshes/LineageEffectsStaticmeshes/auraburn00.fbx`.

### Maillages (via umodel puis Blender)

    cd "<client>/umodel_win32"
    ./umodel_64.exe -game=l2 -path="<client>/staticmeshes" -export -out=<sortie> \
        LineageEffectsStaticmeshes.usx
    blender.exe --background --python <MAP_L2Unity>/tools/pskx-to-fbx.py -- <sortie>

umodel sort du `.pskx`, le script du projet convertit en `.fbx`. Copier ensuite
a plat dans `Resources/Data/StaticMeshes/<paquet>/`.

### Textures (umodel seul)

    ./umodel_64.exe -game=l2 -path="<client>/SysTextures" -export -png -out=<sortie> \
        LineageEffectsTextures.utx

Attention, ces paquets sont dans **SysTextures**, pas `Textures`. Copier a plat
dans `Resources/Data/SysTextures/<paquet>/`.

### Provenance relevee le 2026-09-27

Interlude ne couvre qu'une partie : 88 maillages sur 155 et 9 textures sur 65,
le reste venant d'Orfen. C'est attendu, puisque 102 des recettes sont elles-memes
completees depuis Orfen.

Les `.png` de `SysTextures` sont **ignores par git** (regle des assets
regenerables) : seuls les `.fbx` sont versionnes, les textures se reproduisent
avec la commande ci-dessus.
