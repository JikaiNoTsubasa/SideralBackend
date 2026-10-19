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
- `Storage:RootPath` : dossier de stockage local des fichiers envoyés (défaut `storage`, relatif au dossier de l'API ; avatars dans `avatars/`)
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
sideral-jobcode  → logique métier (Managers) : Auth, Users, Wall...
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
| GET/POST | `/api/users` | ADMIN |
| GET/PUT/DELETE | `/api/users/{id}` | ADMIN (suppression logique) |
| GET/POST | `/api/posts` | connecté (mur, paginé, plus récents d'abord) |
| GET/PUT/DELETE | `/api/posts/{id}` | connecté (PUT : auteur ; DELETE : auteur ou ADMIN) |
| PUT/DELETE | `/api/posts/{id}/like` | connecté |
| GET/POST | `/api/posts/{id}/comments` | connecté |
| DELETE | `/api/posts/{id}/comments/{commentId}` | auteur du commentaire, auteur du message ou ADMIN |
