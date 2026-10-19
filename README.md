# SideralBackend
Sideral is an enterprise social network with project management and document management system

## Démarrage

### Prérequis
- .NET SDK 10
- PostgreSQL
- `dotnet-ef` (`dotnet tool install -g dotnet-ef`) pour les migrations

### Configuration
```bash
cp sideral-api/appsettings.Development.example.json sideral-api/appsettings.Development.json
```
`appsettings.Development.json` n'est pas versionné. À renseigner :
- `ConnectionStrings:Sideral` : chaîne de connexion PostgreSQL
- `JwtSettings:Secret` : secret aléatoire d'au moins 32 octets (`openssl rand -base64 48`)
- `Cors:AllowedOrigins` : URL du frontend (`http://localhost:4200` en dev)
- `Storage:RootPath` : dossier de stockage local des fichiers envoyés (défaut `storage`, relatif au dossier de l'API ; avatars dans `avatars/`, fichiers de la GED dans `documents/`). **À sauvegarder avec la base.**
- `Seed:Admin` : administrateur créé au démarrage **si la table `users` est vide** (changement de mot de passe obligatoire à la première connexion)

### Base de données
Le schéma vient des migrations EF Core (`sideral-context/Migrations`). Les tables et colonnes sont en snake_case.
```bash
# Script SQL idempotent (déjà généré dans sql/sideral-schema.sql)
dotnet ef migrations script --idempotent --project sideral-context --startup-project sideral-context -o sql/sideral-schema.sql

# Ou application directe
SIDERAL_CONNECTION_STRING="Host=...;Database=sideral;Username=...;Password=..." \
  dotnet ef database update --project sideral-context --startup-project sideral-context

# Nouvelle migration
dotnet ef migrations add <Nom> --project sideral-context --startup-project sideral-context -o Migrations
```

### Lancement
```bash
dotnet watch run --project sideral-api      # http://localhost:5000
```
- Documentation OpenAPI (dev) : http://localhost:5000/scalar
- Requêtes d'exemple : `sideral-api/sideral-api.http`
- Hash d'un mot de passe (insertion SQL manuelle) : `dotnet run --project sideral-api -- --hash "motdepasse"`

## Architecture

```
dependancies     → infrastructure générique : ApiException, hash PBKDF2, pagination, marqueurs IScoped/ISingleton
sideral-context  → DbContext EF Core (PostgreSQL), entités, migrations
sideral-jobcode  → logique métier (Managers) : Auth, Users, Groups, Wall, Dms (GED)...
sideral-api      → contrôleurs fins, DTOs, pipeline ASP.NET Core
```
- Les classes qui implémentent `IScoped` / `ISingleton` (dans `dependancies` et `sideral-jobcode`) sont enregistrées automatiquement dans l'injection de dépendances.
- Les erreurs métier sont levées avec `ApiException` et renvoyées en `application/problem+json` (`detail`, `errorCode`, `traceId`).
- Les routes sont authentifiées par défaut (`BaseController` porte `[Authorize]`) ; utiliser `[AllowAnonymous]` pour les exceptions.
- Pagination : `?page=&limit=&search=`, avec les métadonnées dans les en-têtes `page`, `page-size`, `page-count` et `total`.

### Authentification
- `POST /api/auth/login` `{login, password}` (login ou email) → bearer JWT (15 min) + refresh token (7 j)
- `POST /api/auth/refresh` `{refreshToken}` → nouvelle paire. Le refresh token change à chaque appel ; réutiliser un ancien token révoque toutes les sessions de l'utilisateur.
- `POST /api/auth/logout` `{refreshToken}`
- Les refresh tokens sont stockés hashés (SHA-256), et les mots de passe en PBKDF2 (même format que evo-api).
- Les rôles (`ADMIN`, `USER`) sont dans le claim `role` ; la policy `Admin` protège `/api/users`.

### Routes
| Méthode | Route | Accès |
|---|---|---|
| GET | `/api/ping` | public |
| GET/PUT | `/api/me` | connecté |
| PUT | `/api/me/password` | connecté |
| PUT | `/api/me/preferences` | connecté (thème `light`/`dark`/`system`, couleur principale) |
| PUT/DELETE | `/api/me/avatar` | connecté (multipart, champ `file` : JPEG/PNG/WebP, 2 Mo max) |
| GET | `/api/avatars/{fichier}` | public (noms aléatoires non devinables, cache immutable) |
| GET | `/api/roles` | connecté |
| GET/POST | `/api/users` | ADMIN (création : identifiant généré automatiquement) |
| GET | `/api/users/login-suggestion?firstname=&lastname=` | ADMIN (identifiant qui sera attribué : 1re lettre du prénom + 2 premières du nom, en majuscules ; si pris, lettres suivantes du nom) |
| GET/PUT/DELETE | `/api/users/{id}` | ADMIN (suppression logique) |
| GET/POST | `/api/posts` | connecté (mur, paginé, plus récents d'abord ; `isAnnouncement` réservé aux ADMIN) |
| GET/PUT/DELETE | `/api/posts/{id}` | connecté (PUT : auteur ; DELETE : auteur ou ADMIN) |
| PUT/DELETE | `/api/posts/{id}/like` | connecté |
| GET/POST | `/api/posts/{id}/comments` | connecté |
| DELETE | `/api/posts/{id}/comments/{commentId}` | auteur du commentaire, auteur du message ou ADMIN |
| GET | `/api/link-preview?url=` | connecté, 60 / min / utilisateur (aperçu d'un lien du mur ; 204 si indisponible) |
| GET/POST | `/api/groups` | ADMIN (groupes d'utilisateurs) |
| GET/PUT/DELETE | `/api/groups/{id}` | ADMIN |
| GET | `/api/principals?search=&users=&groups=` | connecté (recherche d'utilisateurs / groupes pour les droits) |
| GET | `/api/folders/root` | connecté (dossiers racine pour un ADMIN, dossiers partagés sinon) |
| GET | `/api/folders/{id}` | lecteur (contenu : sous-dossiers visibles + documents) |
| POST | `/api/folders` | gestionnaire du parent (racine : ADMIN) |
| PUT/DELETE | `/api/folders/{id}` | gestionnaire (renommer / corbeille) |
| PUT | `/api/folders/{id}/move` | gestionnaire du dossier et de la destination |
| GET/PUT | `/api/folders/{id}/permissions` | lecteur / gestionnaire |
| GET | `/api/folders/{id}/activity` | lecteur (paginé) |
| POST | `/api/folders/{id}/documents` | contributeur (multipart `file`, tous types, 100 Mo max) |
| GET | `/api/documents/{id}` | lecteur (fiche + versions) |
| PUT | `/api/documents/{id}` | contributeur (nom, description) |
| PUT | `/api/documents/{id}/move` | gestionnaire du dossier + contributeur de la destination |
| DELETE | `/api/documents/{id}` | gestionnaire (corbeille) |
| POST/DELETE | `/api/documents/{id}/checkout` | contributeur / auteur de l'extraction, gestionnaire ou ADMIN |
| POST | `/api/documents/{id}/versions` | contributeur (checkin multipart `file`, `major`, `comment`) |
| POST | `/api/documents/{id}/versions/{versionId}/restore` | contributeur |
| GET | `/api/documents/{id}/link?versionId=&download=` | lecteur (jeton signé, 1 h) |
| GET | `/api/documents/content?token=` | public, jeton signé (droits revérifiés) |
| GET | `/api/documents/{id}/activity` | lecteur (paginé) |
| GET | `/api/documents/checked-out` | connecté (mes extractions) |
| GET | `/api/documents/search?q=` | connecté (nom, description et contenu ; résultats filtrés par les droits) |
| GET/DELETE | `/api/trash` | connecté (éléments gérables / vider) |
| POST | `/api/trash/{folders\|documents}/{id}/restore` | gestionnaire |
| DELETE | `/api/trash/{folders\|documents}/{id}` | gestionnaire (suppression définitive) |

| GET/PUT | `/api/admin/fulltext` | ADMIN (configuration du serveur full text : activation, URL, code application) |
| POST | `/api/admin/fulltext/check` | ADMIN (test de connexion, URL saisie ou enregistrée) |
| GET | `/api/admin/fulltext/stats` | ADMIN (documents par état d'indexation, dernier / prochain passage de l'indexeur) |
| GET | `/api/admin/fulltext/queue?status=PENDING\|FAILED&page=&limit=` | ADMIN (documents en attente et / ou en échec, paginés) |
| POST | `/api/admin/fulltext/reindex?onlyFailed=` | ADMIN (remise en file d'indexation) |

### Aperçus de liens (mur)
- `LinkPreviewManager` : YouTube via oEmbed (`youtube.com/oembed`, miniature `i.ytimg.com`), autres sites via les balises OpenGraph / Twitter / `<title>` (512 Ko lus au maximum, 6 s, 3 redirections). Cache mémoire 6 h (30 min pour un échec).
- Protection SSRF : client HTTP `linkpreview` avec `SafeHttpHandler`, qui refuse de se connecter aux adresses privées, locales, lien local (métadonnées cloud) ou réservées, vérifiées sur l'adresse réellement connectée (redirections et DNS rebinding compris).
- Les images d'aperçu sont chargées directement par le navigateur depuis le site d'origine (`referrerpolicy="no-referrer"`).

### Full text (SokaFT)
- Serveur : [SokaFT](https://github.com/JikaiNoTsubasa/SokaFT). Sideral appelle `POST {url}/api/index/queue {name, content, identifier, application}` ; `identifier` = GUID propre à chaque document (`documents.full_text_identifier`, index unique), renvoyé par la recherche pour retrouver le document, `application` = code application, `content` = **texte extrait par Sideral** (SokaFT n'extrait pas le texte des fichiers).
- Configuration en base (`app_settings`, clés `fulltext.*`) via Admin > Full Text Server : activation, URL, code application et planification de l'indexeur (délai entre deux passages, 30 s par défaut ; documents par lot, 10 ; délai max. entre deux tentatives, 60 min). SokaFT n'a pas d'authentification : aucune clé d'API.
- Extraction (`DocumentTextExtractor`) : texte brut, PDF (PdfPig), docx / xlsx / pptx, odt / ods / odp ; autres formats : seul le nom est indexé. 2 millions de caractères maximum.
- Indexation (`FullTextIndexer`, tâche de fond) : chaque document a un état `full_text_status` (`PENDING` / `INDEXED` / `FAILED`). Nouveau document, nouvelle version, restauration, renommage → `PENDING` et réveil immédiat de la tâche ; sinon un passage selon la planification, lot plein = passage suivant immédiat. Serveur injoignable → nouvelle tentative après 1, 2, 4… min, jusqu'au plafond configuré. La planification est relue à chaque passage (enregistrer la configuration réveille la tâche). Rien n'est envoyé tant que le full text est désactivé. Changer le code application remet tous les documents en attente.
- Recherche (`DocumentSearchManager`) : nom / description dans la base Sideral (tous les mots) + contenu via `POST {url}/api/index/search {search, application}` (texte échappé en syntaxe Lucene, joker `*` en fin de mot conservé). Les identifiants renvoyés sont retrouvés par `full_text_identifier` ; on ne garde que les documents hors corbeille dans un dossier lisible par l'utilisateur. Serveur indisponible : résultats sur le nom seulement, avec un avertissement. Limites SokaFT : 10 résultats maximum, sans tri par pertinence.
- Test de connexion : `GET {url}/api/health`, qui doit répondre 2xx (404 = mauvaise URL ou serveur qui n'est pas SokaFT).

### GED (documents)
- **Droits** : par dossier, à des utilisateurs ou des groupes, niveaux `Read` (lecteur) < `Write` (contributeur) < `Manage` (gestionnaire). Un sous-dossier hérite des droits de son parent (`inherit_permissions`), en plus des siens ; l'héritage peut être désactivé. Les ADMIN sont gestionnaires partout et seuls à créer des dossiers racine.
- Calcul : `DmsAccess` charge l'arbre complet des dossiers (colonnes légères) et les droits de l'utilisateur à chaque requête (`FolderTree`). Suffisant pour quelques milliers de dossiers ; au-delà, prévoir un chemin matérialisé.
- **Versions** majeure/mineure (1.0, 1.1, 2.0) ; la version courante est la plus haute. Restaurer une version crée une nouvelle version mineure qui réutilise le même fichier.
- **Extraction** (checkout) : verrou posé par une mise à jour conditionnelle ; tant qu'il est posé, seul son auteur peut modifier le document. L'archivage (`POST versions`) libère le verrou ; déposer une version sans extraction est permis si personne n'a extrait le document.
- **Fichiers** : `{Storage:RootPath}/documents/{2 car.}/{guid}`, type MIME déterminé par signature binaire (sert uniquement à l'aperçu ; les fichiers texte, SVG et HTML compris, sont servis en `text/plain`). Le contenu est servi via un lien signé HMAC (clé dérivée du secret JWT) utilisable dans `<img>`/`<iframe>`/`<video>`, avec `nosniff` et `Content-Security-Policy: sandbox` (sauf PDF).
- **Corbeille** : suppression logique (`deleted_on`) ; le contenu d'un dossier supprimé le suit. La suppression définitive efface en cascade sous-dossiers, documents, versions, droits, journal, puis les fichiers.
- En production, ajuster la taille maximale des requêtes du reverse proxy (100 Mo par fichier).
