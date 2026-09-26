# Brief de session, aout a septembre 2026

Session longue, menee sur plusieurs mois. Tout le code est commite ; les assets extraits
du client restent hors du depot, regenerables par les scripts d'import (7,2 Go).

---

## 1. Chaine d'import depuis le client Orfen

Le point de depart : le projet n'avait que 19 modeles de PNJ et une poignee d'objets. Le
client Orfen s'est revele etre la vraie source des assets Classic que lit le client.

Une chaine complete a ete batie : umodel extrait mesh, animations et textures, Blender 3.6
convertit en FBX avec un manifeste JSON, et des menus d'editeur Unity finalisent prefabs,
materiaux et conteneurs d'animation.

- `Tools/Blender/OrfenNpcImport.py` : PNJ et monstres, resolution des animations par dump.
- `Tools/Blender/OrfenItemImport.py` : icones, armes, armures.
- `Tools/NpcModelImporter.cs` et `Tools/OrfenItemImporter.cs` : les menus `L2 > PNJ` et
  `L2 > Objets`.

Resultat : 439 modeles de PNJ, 278 armes, 697 pieces d'armure, 1116 icones, 1767 materiaux
d'armure.

**Pieges rencontres**

- Des modeles Ertheia et Kamael ont ete importes par erreur, races qui n'existent pas sous
  Interlude. Correction par une liste blanche de 14 champs de race, 624 fichiers supprimes.
- Les armes converties avec une armature sortaient de travers ; converties sans os, elles
  gardent l'orientation d'origine.
- Les anciens FBX d'armes sont en centimetres et les nouveaux en metres, d'ou une echelle
  divisee par cent.
- Les armures completes et les boucliers etaient invisibles : `Armorgrp` decrit plusieurs
  pieces par entree et un compteur `;{2}` parasitait le parsing.

---

## 2. Animations des PNJ et des monstres

Audit puis reparation : les conteneurs etaient incomplets (ni marche ni course), et
`fox_m00` jouait les clips de l'elpy. Les PNJ flottaient au-dessus du sol, les monstres
n'etaient pas synchronises entre le degat et l'animation d'attaque, et les PNJ statiques ne
tournaient pas vers le joueur.

Tout cela est corrige, y compris la pose au sol via le `CharacterController` et la vitesse
d'animation d'attaque alignee sur le serveur.

---

## 3. Emotes, de zero a vingt gestes jouables

Le serveur savait deja rediffuser une emote et le client savait la jouer : il manquait le
sens client vers serveur, plus tout le cablage des animations.

- Paquet `RequestSocialAction` (opcode `0x1b`), plage serveur elargie de 2-13 a 2-21.
- Rien n'est cable a la main : la table `ActionName` du client dit lesquelles sont des
  emotes, sous quel identifiant les envoyer et sous quelle commande de chat.
- Declenchement par l'onglet Social de la fenetre d'actions ou par `/socialbow`.
- Trois emotes a deux (saluts croises, tape dans la main, danse) : paquet
  `RequestCoupleAction` (opcode `0xc9`), confirmation via la fenetre `ConfirmDlg` existante,
  diffusion synchronisee avec les deux personnages tournes l'un vers l'autre.
- Didascalies dans le canal Role Play : *Nom salue de la main*.
- Les armes se rangent pendant le geste, sauf victoire et charge.
- Noms d'action en francais par fichier d'override.

**Pieges rencontres**

- L'identifiant 15 servait a l'effet de montee de niveau alors que la table du client y
  place la Timidite. L'effet de niveau a ete deplace sur 100.
- Les conteneurs d'emotes des dix races jouables etaient entierement vides, et les quatre
  races orc et chaman pointaient vers celui de la magicienne, d'ou des T-poses.
  L'emplacement dans le conteneur doit etre l'identifiant du serveur, sinon un salut joue
  une danse.
- Les clips pris dans le client Interlude ont le meme nombre d'os mais une hierarchie
  differente : ils ne s'appliquent pas au rig du projet et donnent une T-pose silencieuse.
  Tout doit venir d'Orfen.
- L'import fige les animations a 24 images par seconde alors que le client dessine les douze
  emotes d'origine entre 6 et 20, et les huit modernes a 30.

---

## 4. Deplacement et synchronisation multijoueur

Trois defauts distincts, decouverts en cascade.

- **L'intention bloquee.** Le client pilote le deplacement par direction et non par
  destination : le serveur ne recoit jamais d'evenement d'arrivee et l'intention du joueur
  restait `MOVE_TO` pour toujours. Toutes les verifications heritees d'aCis qui exigent un
  personnage au repos refusaient alors l'action, en silence.
- **L'arret non diffuse.** La premiere version du correctif court-circuitait la diffusion de
  l'arret aux autres clients, qui continuaient d'extrapoler.
- **La destination imposee.** Le serveur decrivait le deplacement d'un joueur par un
  `MoveToLocation`, donc une destination deja perimee, ce qui rejouait son trajet en boucle
  chez les autres joueurs.

---

## 5. Objets, equipement et interface

- **Infobulle** au survol, inventaire et boutique : nom, description, statistiques et
  panoplie. Bleu pour ce qui est porte, gris clair pour le potentiel. La ligne de type donne
  l'arme (arc, dague, hast, epees jumelles) et non le mot "arme".
- **Panoplies** : 51 jeux generes depuis `armorSets.xml` du serveur.
- **Vignette de grade** du client posee au coin de l'icone.
- **Compteur de pile** masque a un exemplaire, "99+" au-dela.
- **Bouclier** : orientation calculee depuis le maillage. Quatre allers-retours ont ete
  necessaires ; l'axe le plus long est instable sur un bouclier rond, ou hauteur et largeur
  sont a deux millimetres pres.
- **Armes au sol** : invisibles parce que l'echelle du prefab, qui porte le facteur d'import
  du FBX, etait remplacee au lieu d'etre multipliee.

**Deux causes de fond** : `ItemStatData` n'etait jamais lu, la casse des cles ne
correspondant pas ; et `ItemNameTable` est videe apres le chargement, singleton compris,
donc l'interroger au runtime plantait l'inventaire.

---

## 6. Serveur : boutique, base, interactions

- **Achat** : une garde refusait toute quantite superieure a 1 sur un objet non empilable.
- **Vente** : elle fonctionnait mais l'inventaire mis a jour n'etait jamais renvoye.
- **Surcharge** : le poids n'etait pas recalcule dans tous les cas, d'ou une penalite de
  vitesse intermittente.
- **Base de donnees** : `items.item_id` etait en SMALLINT, donc tout objet d'identifiant
  superieur a 65535 etait enregistre comme 65535. Les meubles de salle de clan
  disparaissaient. Colonne passee en INT UNSIGNED.
- **Distance de dialogue** : on peut desormais s'approcher davantage d'un marchand derriere
  son comptoir.
- **Creation de personnage** : les trois methodes de personnalisation testaient la nullite
  de l'apparence apres l'avoir dereferencee, ce qui cassait l'ecran.

---

## 7. Decisions et travaux annexes

- **Salle de clan** : la fenetre dediee est abandonnee, le HTML convient.
- **Git** : seul le code est versionne. Les 7,2 Go d'assets extraits sont ignores et
  regenerables par les scripts.
- **Todo** : point du 19 septembre mis a jour, pistes de distribution notees (chargement
  asynchrone, sortie des modeles de Resources, investissement memoire), et une section
  complete de traduction decoupee en douze lots atteignables.

---

## Outils ajoutes

- `L2 > PNJ > Importer les modeles Orfen convertis` et `Reconstruire les conteneurs`
- `L2 > Objets > Importer les objets Orfen convertis`, `Rafraichir les materiaux`,
  `Reimporter les armes converties`, `Recaler l'echelle`, `Reorienter les boucliers`,
  `Regler les icones importees`
- `L2 > Animations > Extraire les emotes recuperees du client` et `Brancher les emotes`
- Scripts Blender : `OrfenNpcImport.py`, `OrfenItemImport.py`, `OrfenEmoteImport.py`

---

## Ce qui reste ouvert

1. **Voix des emotes et banks FMOD.** 238 voix extraites et deposees, le code les appelle
   deja ; restent les evenements a creer dans Studio et les banks a reconstruire.
2. **Frequence d'images.** Mesure du 26 septembre : 36 ms par image en moyenne, 27 par
   seconde, et 10 gels depassant 250 ms dont un de 6,7 secondes. Ces gels se confondent avec
   un defaut de reseau. A rapprocher des 47 577 lumieres importees et du diagnostic TDR.
3. **Panels d'icones** : enchantement, niveau, attribut. Les vignettes de grade sont faites.
4. **Effets d'ambiance du monde** : les emetteurs decoratifs du client (torches, brasiers,
   embruns) ne sont pas importes. Zero `ParticleSystem` dans les scenes.
5. **Paquet `NpcSay`** ignore par le client : 1348 paquets perdus en une session.
6. **Douze lots de traduction** deja decoupes dans la todo.
7. **Reste d'import** : Hardin, 8 pieces d'armure, 7 icones, un modele au sol, les versions
   remasterisees des 40 anciens PNJ, les textures par PNJ et 27 coiffures ou visages.
8. **A reproduire** : deconnexion en modifiant la salle de clan, et echec de `//spawn` sur le
   GM shop.
