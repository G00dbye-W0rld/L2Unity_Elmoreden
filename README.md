# L2Unity — Guide d'installation, de lancement et d'ajout de contenu

Ce dossier contient un MMORPG inspiré de Lineage 2 (projet fan, non officiel), composé de **trois programmes séparés** qui doivent tourner ensemble :

| Dossier | Rôle |
|---|---|
| `l2-unity-main/l2-unity/` | Le **client** — le jeu lui-même, dans Unity |
| `l2-unity-gameserver-master/gameserver/` | Le **gameserver** — gère le monde, les personnages, les combats |
| `l2-unity-loginserver-main/loginserver/` | Le **loginserver** — gère les comptes et la connexion |

Ce guide est en deux parties : la **Partie 1** explique comment **lancer** le jeu quand tout est déjà installé (si tu repars de zéro sur une autre machine, lis d'abord la section [Prérequis](#prérequis)). La **Partie 2** explique comment **ajouter du contenu** au jeu (PNJ, monstres...).

---

## Sommaire

### Partie 1 — Lancer le jeu
1. [Prérequis](#prérequis)
2. [Étape 1 — Préparer la base de données (une seule fois)](#étape-1--préparer-la-base-de-données-une-seule-fois)
3. [Étape 2 — Vérifier que tout est prêt](#étape-2--vérifier-que-tout-est-prêt)
4. [Étape 3 — Lancer les serveurs](#étape-3--lancer-les-serveurs)
5. [Étape 4 — Lancer le client Unity et jouer](#étape-4--lancer-le-client-unity-et-jouer)
6. [Arrêter les serveurs](#arrêter-les-serveurs)
7. [Dépannage](#dépannage)

### Partie 2 — Ajouter du contenu
8. [Ajouter un PNJ ou un monstre custom](#ajouter-un-pnj-ou-un-monstre-custom)
9. [Importer et customiser une région de map](#importer-et-customiser-une-région-de-map)

---

# Partie 1 — Lancer le jeu

## Prérequis

À installer une seule fois, avant la toute première utilisation :

- **JDK 21** (Liberica recommandé) — utilisé pour compiler et lancer les deux serveurs. Il n'a pas besoin d'être le JDK "par défaut" du système : chaque serveur pointe déjà vers son propre JDK dans son fichier `gradle.properties`.
- **Un serveur MySQL/MariaDB** — WampServer est utilisé sur cette machine (MariaDB, port `3307`). phpMyAdmin est accessible via `localhost/phpmyadmin5.2.3/` pour inspecter la base à la main si besoin.
- **Unity Hub** + **Unity `6000.0.28f1`** (version exacte requise, visible dans `l2-unity-main/l2-unity/ProjectSettings/ProjectVersion.txt`).
- **Git for Windows** (fournit Git Bash) — nécessaire pour les scripts `.sh`. Les scripts `.bat` fonctionnent sans ça, directement dans l'invite de commandes Windows.

---

## Étape 1 — Préparer la base de données (une seule fois)

Les deux serveurs stockent tout (comptes, personnages, objets...) dans une base MariaDB nommée `l2unity`. Si elle existe déjà sur cette machine, tu peux passer directement à l'[Étape 2](#étape-2--vérifier-que-tout-est-prêt).

1. **Démarre WampServer** et attends que l'icône soit verte (tous les services actifs).
2. **Crée la base** `l2unity` en charset `utf8mb3` (important : les tables utilisent le moteur MyISAM, qui limite la taille des index — `utf8mb4` provoque une erreur "clé trop longue"). Via phpMyAdmin ou en ligne de commande :
   ```sql
   CREATE DATABASE l2unity CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci;
   ```
3. **Lance l'installeur de tables**, dans `l2-unity-gameserver-master/gameserver/db/tools/` :
   - Windows : double-clique `database_installer.bat`
   - Git Bash / Linux / Mac : `./database_installer.sh`

   Choisis l'option **`f` (full install)** au premier lancement. Le script crée une soixantaine de tables.
4. **Vérifie la config** des deux serveurs — normalement déjà correcte sur cette machine :
   - `l2-unity-gameserver-master/gameserver/conf/server.properties` : `URL`, `Login`, `Password`
   - `l2-unity-loginserver-main/loginserver/conf/server.properties` : `database.jdbc.url`, `database.jdbc.username`, `database.jdbc.password`

   Les deux doivent pointer vers la **même** base (même host, même port, même nom de base).

---

## Étape 2 — Vérifier que tout est prêt

Avant de lancer les serveurs, un script de diagnostic vérifie automatiquement : JDK configurés, cohérence de la config base de données entre les deux serveurs, base joignable et peuplée, ports réseau libres.

Dans un terminal Git Bash, à la racine du projet :

```bash
./setup-check.sh
```

Chaque ligne affiche `[OK]`, `[!]` (avertissement, pas forcément bloquant) ou `[X]` (problème à corriger). Le script ne modifie rien tout seul — il ne fait que diagnostiquer et te dire quoi faire si quelque chose manque.

---

## Étape 3 — Lancer les serveurs

Double-clique **`start-servers.bat`** (ou lance `./start-servers.sh` dans Git Bash).

Deux fenêtres s'ouvrent, une par serveur :

- **Loginserver** : attends de voir `Login server listening on port 2107.`
- **Gameserver** : le chargement prend 30 à 60 secondes (skills, objets, cartes...). Attends de voir tout en bas :
  ```
  Registered as server: [1] Bartz.
  ```
  Cette ligne confirme que le gameserver s'est bien enregistré auprès du loginserver — **c'est le signal que tout est prêt à jouer**.

> Astuce : après un premier lancement, les suivants sont nettement plus rapides (Gradle garde le résultat de compilation en cache).

Besoin de relancer un seul des deux serveurs (après un crash, par exemple) ? Utilise `start-loginserver.bat`/`.sh` ou `start-gameserver.bat`/`.sh` individuellement.

---

## Étape 4 — Lancer le client Unity et jouer

1. Ouvre **Unity Hub** → *Add project from disk* → sélectionne `l2-unity-main/l2-unity/`.
2. Unity Hub doit proposer automatiquement la version `6000.0.28f1`. Ouvre le projet.
3. **Premier import** : peut prendre 5 à 15 minutes (réimport des assets/shaders). Les fois suivantes sont quasi instantanées.
4. Dans la fenêtre *Project*, ouvre `Assets/Resources/Scenes/Menu.unity` (double-clic).
5. Clique sur **Play** ▶ en haut de l'éditeur.
6. Dans le jeu : crée un compte → connecte-toi → sélectionne le serveur (**Bartz**, il doit apparaître en ligne) → crée un personnage → entre dans le monde.

---

## Arrêter les serveurs

Ferme simplement les deux fenêtres de terminal (loginserver et gameserver), ou fais `Ctrl+C` dans chacune. Il n'y a rien d'autre à nettoyer.

---

## Dépannage

| Symptôme | Cause probable | Solution |
|---|---|---|
| `Address already in use: bind` au démarrage d'un serveur | Un autre serveur (ou un ancien lancement) occupe déjà le port | Ferme les anciennes fenêtres de terminal, ou vérifie les processus actifs |
| Le gameserver refuse de compiler avec une erreur bizarre (Lombok, `ExceptionInInitializerError`...) alors que la config est correcte | Un ancien "daemon" Gradle tourne encore en arrière-plan et ignore les derniers réglages | Dans le dossier du serveur concerné : `./gradlew --stop`, puis relance |
| `setup-check.sh` signale la base "vide ou introuvable" alors qu'elle existe | WampServer pas démarré, ou mauvais port/identifiants dans `conf/server.properties` | Vérifie que WampServer tourne, relance `setup-check.sh` |
| Le client Unity n'arrive pas à se connecter | Le loginserver n'est pas encore prêt, ou son adresse/port a changé | Vérifie que la fenêtre loginserver affiche bien `listening on port 2107` avant de lancer Unity |
| Le serveur "Bartz" n'apparaît pas dans la liste des serveurs en jeu | Le gameserver n'a pas fini de démarrer ou n'a pas réussi à s'enregistrer | Attends la ligne `Registered as server: [1] Bartz.` dans sa fenêtre avant de jouer |

---

# Partie 2 — Ajouter du contenu

## Ajouter un PNJ ou un monstre custom

Il y a deux façons de faire, selon si le PNJ doit **réutiliser une apparence déjà présente dans le jeu** (méthode simple, 100% côté serveur) ou avoir **un visuel qui n'existe encore nulle part** (méthode complète, nécessite aussi Unity).

> **Choisis d'abord un ID libre**, par exemple `900001` (un nombre qui n'est utilisé par aucun PNJ officiel de Lineage 2, pour éviter tout conflit).

### Méthode simple (recommandée) : réutiliser une apparence existante

C'est déjà comme ça que fonctionne la Boutique GM de ce projet (`npc id="900000"`) : un PNJ avec ses propres stats/IA/nom, qui **affiche l'apparence d'un autre PNJ déjà existant**, sans toucher à un seul fichier côté client. Le mécanisme s'appelle `idTemplate`.

#### 1. Définir le PNJ côté serveur

Fichier : `l2-unity-gameserver-master/gameserver/data/xml/npcs/` (un des fichiers existants, ou un nouveau).

```xml
<npc id="900001" idTemplate="20001" name="Mon PNJ Custom" title="" alias="mon_pnj_custom">
    <set name="usingServerSideName" val="true"/>
    <set name="usingServerSideTitle" val="true"/>
    <set name="type" val="Monster"/>   <!-- "Folk" pour un PNJ non-hostile (marchand, garde...), "Monster" pour un ennemi -->
    <set name="level" val="10"/>
    <set name="radius" val="10.0"/>    <!-- taille du collider (unités L2, pas de conversion a faire ici) -->
    <set name="height" val="25.0"/>
    <set name="hp" val="500.0"/>
    <set name="mp" val="100.0"/>
    <set name="pAtk" val="20.0"/>
    <set name="pDef" val="40.0"/>
    <set name="runSpd" val="60"/>
    <set name="walkSpd" val="25"/>
</npc>
```

Les deux points clés :
- **`idTemplate="20001"`** : "affiche-toi comme le PNJ 20001 (le Gremlin)". Le client ne reçoit **jamais** le vrai id (`900001`) — seulement `20001` — donc il n'a besoin d'aucune nouvelle donnée pour savoir quel modèle 3D afficher : il connaît déjà le Gremlin. Choisis n'importe quel `id` de PNJ existant dont tu veux réutiliser le look.
- **`usingServerSideName`/`usingServerSideTitle` à `true`** : sans ça, le nom/titre affichés seraient ceux du PNJ réutilisé (ici "Gremlin"), pas "Mon PNJ Custom". Avec `true`, le serveur envoie ton propre nom/titre directement, sans passer par les fichiers client.

`radius`/`height` restent les **vraies valeurs de ce PNJ** (pas celles du 20001) — le serveur les envoie indépendamment de l'apparence affichée, donc un PNJ custom peut avoir un collider différent de celui dont il emprunte le visuel.

#### 2. Le faire apparaître dans le monde

Sans cette étape, le PNJ existe en théorie mais n'apparaît jamais en jeu. Fichier : `l2-unity-gameserver-master/gameserver/data/xml/spawnlist/<région>.xml` (le fichier correspond à la zone de la carte où le PNJ doit apparaître).

```xml
<territory name="mon_pnj_zone" minZ="-3648" maxZ="-3448">
    <node x="-91080" y="248292"/>
    <node x="-90792" y="247860"/>
    <node x="-90136" y="248300"/>
    <node x="-90432" y="248720"/>
</territory>
<npcmaker name="mon_pnj_zone_m1" territory="mon_pnj_zone" maximumNpcs="1">
    <ai type="default_maker"/>
    <npc id="900001" total="1" respawn="1min"/>
</npcmaker>
```

- `territory` : une zone polygonale (liste de points x/y) où le PNJ peut apparaître.
- `npcmaker` : combien d'exemplaires (`total`) et le délai avant réapparition après la mort (`respawn`).

Pour trouver des coordonnées x/y valides sans en connaître à l'avance, le plus simple est de repérer la position d'un `<territory>` déjà existant proche de l'endroit voulu (dans le même fichier de région) et de s'en inspirer, ou de demander de l'aide pour retrouver une position précise en jeu.

#### 3. Tester

1. Redémarre le gameserver (il charge les XML une seule fois, au démarrage). Si un comportement bizarre apparaît alors que la config semble correcte, `./gradlew --stop` avant de relancer (cf. [Dépannage](#dépannage)).
2. Relance le Play Mode dans Unity.
3. Va à l'endroit où le spawn a été placé.

C'est tout — pas de fichier côté client (`Npcgrp_Classic.txt`/`NpcName_Classic-eu.txt`) à toucher.

### Méthode complète : un tout nouveau visuel

Si le PNJ a besoin d'un modèle 3D qui **n'existe encore nulle part** dans le jeu, `idTemplate` ne peut pas aider (il ne fait que pointer vers une apparence déjà connue du client) — il faut alors remplir les 4 endroits où un PNJ est défini :

1. **Stats côté serveur** — même fichier XML qu'au-dessus, mais **sans** `idTemplate` (ou avec `idTemplate` égal au nouvel `id` lui-même, ce qui revient au même).
2. **Spawn** — identique à l'étape 2 ci-dessus.
3. **Apparence côté client** — nouvelle entrée dans `l2-unity-main/l2-unity/Assets/StreamingAssets/Data/Meta/Npcgrp_Classic.txt`, avec le **même `npc_id`** que le serveur. Le champ `mesh_name` doit pointer vers le nouveau prefab (voir point 5), et `collision_radius`/`collision_height` doivent recopier le **même nombre brut** que `radius`/`height` du XML serveur (pas de conversion à faire, elle est automatique) :
   ```
   npc_begin	npc_id=900001	class_name=[LineageMonster.mon_pnj_custom]	mesh_name=[LineageMonsters.mon_nouveau_modele]	collision_radius=10.0	collision_radius_2=10.0	collision_height=25.0	collision_height_2=25.0	npc_type=monster_normal	npc_end
   ```
   > Sans cette entrée, le PNJ ne spawnera pas du tout côté client (juste une erreur silencieuse dans les logs) — c'est l'oubli le plus facile à faire.
4. **Nom affiché côté client** — nouvelle entrée dans `l2-unity-main/l2-unity/Assets/StreamingAssets/Data/Meta/NpcName_Classic-eu.txt` :
   ```
   npc_begin	id=900001	name=[Mon PNJ Custom]	nick=[ Lvl: 10]	nickcolor=9CE8A9FF	npc_end
   ```
5. **Le modèle 3D lui-même** — créer un nouveau prefab Unity avec animation et collider, une étape bien plus impliquée, hors du cadre de ce guide simple. Le point de départ le plus sûr est de partir du prefab d'un PNJ existant qui a une structure proche (mêmes composants) et de le personnaliser à partir de là.
6. **Tester** — identique à l'étape 3 ci-dessus.

---

## Importer et customiser une région de map

Le monde du jeu est découpé en **régions** de 32768 unités de côté. Quatre seulement sont
présentes aujourd'hui (`16_24`, `16_25`, `17_24`, `17_25` — la zone de Talking Island),
chacune existant en trois exemplaires qui doivent rester alignés : la scène Unity, les
données de terrain, et la geodata côté serveur.

Ajouter une région (pour un château ou des clan halls, par exemple) ou customiser une
région existante passe par un pipeline complet, avec quelques pièges sérieux — dont
**cinq chemins codés en dur** pointant vers la machine de l'auteur d'origine, à corriger
avant toute exécution.

> 📄 **[TUTO_IMPORT_MAP.md](TUTO_IMPORT_MAP.md)** — guide complet en 13 sections :
> extraction depuis le client officiel, les 11 étapes du menu `Shnok`, génération de la
> geodata, et surtout **ce qui est personnalisable sans jamais toucher à la geodata**.

Deux points à connaître avant de s'y lancer :

- **Il y a deux geodata**, incompatibles. Celle du client (pathfinding) est générée par le
  projet ; celle du serveur (`.l2j`, hauteur et ligne de vue) ne l'est pas — il faut la
  récupérer d'un pack L2J Interlude.
- **Beaucoup de choses se customisent sans elle** : tout le visuel, plus les zones, spawns,
  téléports, portes et contenu marchand, tous pilotés par coordonnées dans les XML serveur.

---

*Ce projet est un projet fan/apprentissage inspiré de Lineage 2 (NCSoft). Les trois composants (client, gameserver, loginserver) sont des dépôts distincts assemblés dans ce dossier.*
