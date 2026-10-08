# Mini-WoW

MMORPG coopératif instancié : un hub social persistant (progression, métiers, commerce) et des expéditions
instanciées jouables en groupe (petites zones ouvertes, mini-boss, boss). Client Unity, serveur C#/.NET
autoritaire, persistance PostgreSQL. Contrôles et combat dans l'esprit MMORPG classique (clic droit pour
cibler/interagir, auto-attack), structure de mission inspirée des jeux coopératifs à expéditions.

## Structure

| Dossier | Contenu |
|---|---|
| `Assets/`, `Packages/`, `ProjectSettings/` | Projet Unity (client) |
| `Shared/GameShared` | Protocole réseau partagé (netstandard2.1, C# 9) |
| `Shared/GameShared.Tests` | Tests du protocole (xUnit) |
| `docker-compose.yml` | PostgreSQL 17 pour le développement local |

Le plan de travail par milestones est décrit dans la proposition d'architecture (M0 à M9).

## Prérequis

- .NET SDK 10 (LTS)
- Docker Desktop
- Unity 6000.3.15f1 (LTS)

## Démarrer la base

```bash
cp .env.example .env        # puis définir MINIWOW_DB_PASSWORD
docker compose up -d
docker compose ps           # postgres doit être "healthy"
```

## Build et tests backend

```bash
dotnet restore MiniWow.Backend.sln
dotnet build MiniWow.Backend.sln -c Release
dotnet test MiniWow.Backend.sln -c Release
```

## Lancer le serveur

```bash
dotnet run --project Server/GameServer
```

Le serveur écoute sur `127.0.0.1:7777` (configurable dans `Server/GameServer/appsettings.json`). Postgres doit
tourner (`docker compose up -d`) : le serveur applique son schéma (`Server/GameServer.Persistence/Migrations`)
au démarrage, avant d'accepter des connexions.

## Comptes et personnages

Un compte (identifiant/mot de passe) peut avoir jusqu'à 8 personnages. Flux réseau : `Register`/`Login` →
`CharacterList` → `CreateCharacter` (optionnel) → `EnterWorldByCharacter`. L'état d'un personnage (niveau, xp,
cuivre, position, inventaire, quêtes) est sauvegardé à la déconnexion et toutes les 60s. Les dépôts (`GameServer.Persistence`)
ont une implémentation Postgres (production) et une implémentation en mémoire (tests, `ServerHarness`, sans Docker).

## Métiers

4 métiers : Minage et Herboristerie (récolte sur des nœuds placés en zone), Forge et Alchimie (recettes :
réactifs → objet). S'apprennent gratuitement auprès d'un PNJ qui les enseigne (Marchand Tom à Greenhaven).
Compétence 1→75, +1 par récolte/fabrication réussie (déterministe, pas de palier de chance). Persisté comme
le reste du personnage.

## Expéditions

"Crypt" (accessible via le portail à Greenhaven) est la première expédition : plusieurs groupes de squelettes,
un mini-boss (Nécromancien renégat, ne termine pas l'expédition à sa mort), puis le boss principal (Gardien
ancien). Entrer y transfère le groupe dans une instance dédiée ; la victoire sur le boss principal referme
l'instance après un court délai et renvoie tout le monde au hub.

## Zone de test "sandbox"

Une zone dédiée à l'essai manuel de (presque) tout le contenu en une seule carte vaste (plusieurs centaines
d'unités, contre quelques dizaines pour Greenhaven) : meutes de loups/sangliers normaux, 2 camps de monstres
**Élite** (Loup alpha, Sanglier enragé — plus résistants qu'un mob normal mais sans mécanique de boss), 8 nœuds
de récolte (minage + herboristerie), un PNJ marchand/formateur de métiers, une PNJ qui propose une quête de
chasse aux loups alpha, et un portail vers l'expédition "crypt". N'est pas le hub par défaut : à lancer
explicitement en remplaçant la zone de démarrage sur la ligne de commande, sans toucher à `appsettings.json` :

```bash
dotnet run --project Server/GameServer -- --GameServer:ZoneId=sandbox
```

Validé en conditions réelles (serveur réel + plusieurs vrais clients TCP via `Tools/LoadBot`) : connexion,
déplacement et visibilité mutuelle fonctionnent sans erreur serveur sur cette carte.

## Client Unity

1. Compiler `Shared/GameShared` : la DLL est copiée automatiquement dans `Client/UnityProject/Assets/Plugins/GameShared/`.
2. Ouvrir `Client/UnityProject` dans Unity 6000.3.15f1.
3. Menu **MiniWow → Build Full Test Scene** : génère la scène de test.
4. Lancer le serveur, puis le mode Play. Écran de connexion (identifiant/mot de passe) puis sélection/création de
   personnage avant d'entrer dans le monde.
5. Contrôles façon MMORPG classique : WASD déplace (relatif à la caméra), clic droit maintenu oriente la caméra,
   un simple clic droit sur une entité la cible **et** déclenche son action par défaut (attaquer, looter, parler,
   récolter) — l'attaque s'enchaîne ensuite automatiquement (auto-attack) tant que la cible reste vivante et à
   portée, jusqu'à Échap ou au changement de cible. Clic gauche sélectionne sans agir. `H` récolte, `R` ouvre le
   panneau des métiers.

## Règles

- Le serveur est la seule source de vérité. Le client ne valide rien.
- Aucun secret dans Git. Les fichiers `.env` et `*.local.json` sont ignorés.
- Le code partagé reste compatible Unity : pas de `record`, `init` ni `namespace` à portée de fichier dans `Shared/GameShared`.
