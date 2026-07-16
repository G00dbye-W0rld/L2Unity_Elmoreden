# L2Unity — Guide d'installation et de lancement

Ce dossier contient un MMORPG inspiré de Lineage 2 (projet fan, non officiel), composé de **trois programmes séparés** qui doivent tourner ensemble :

| Dossier | Rôle |
|---|---|
| `l2-unity-main/l2-unity/` | Le **client** — le jeu lui-même, dans Unity |
| `l2-unity-gameserver-master/gameserver/` | Le **gameserver** — gère le monde, les personnages, les combats |
| `l2-unity-loginserver-main/loginserver/` | Le **loginserver** — gère les comptes et la connexion |

Ce guide part du principe que tout est déjà installé sur cette machine (JDK, base de données, Unity) et t'explique comment **lancer** le jeu, étape par étape. Si tu repars de zéro sur une autre machine, lis d'abord la section [Prérequis](#prérequis).

---

## Sommaire

1. [Prérequis](#prérequis)
2. [Étape 1 — Préparer la base de données (une seule fois)](#étape-1--préparer-la-base-de-données-une-seule-fois)
3. [Étape 2 — Vérifier que tout est prêt](#étape-2--vérifier-que-tout-est-prêt)
4. [Étape 3 — Lancer les serveurs](#étape-3--lancer-les-serveurs)
5. [Étape 4 — Lancer le client Unity et jouer](#étape-4--lancer-le-client-unity-et-jouer)
6. [Arrêter les serveurs](#arrêter-les-serveurs)
7. [Dépannage](#dépannage)

---

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

*Ce projet est un projet fan/apprentissage inspiré de Lineage 2 (NCSoft). Les trois composants (client, gameserver, loginserver) sont des dépôts distincts assemblés dans ce dossier.*
