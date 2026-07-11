# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commandes essentielles

```bash
# Restaurer les dépendances
dotnet restore Zorg320.Utilitaire.FichierTemporaire.sln

# Compiler la solution
dotnet build Zorg320.Utilitaire.FichierTemporaire.sln

# Lancer tous les tests
dotnet test Zorg320.Utilitaire.FichierTemporaire.sln --verbosity normal

# Lancer un test unitaire spécifique (par nom de méthode)
dotnet test tests/Zorg320.Utilitaire.FichierTemporaire.Tests --filter "NomDuTest"

# Lancer l'API
dotnet run --project src/Zorg320.Utilitaire.FichierTemporaire.Api

# Lancer le Worker
dotnet run --project src/Zorg320.Utilitaire.FichierTemporaire.Worker
```

L'API démarre sur `https://localhost:5001` avec Swagger sur `/swagger`.

## Architecture

La solution est en **Clean Architecture** avec 3 projets principaux :

### Noyau (bibliothèque partagée)
Contient les contrats et les implémentations d'infrastructure :
- `Application/Interfaces/` — interfaces `IServiceChiffrement`, `IServiceStockage`, `IGestionnaireCles`
- `Domaine/Entites/MetadonneesFichier.cs` — entité centrale avec la logique d'expiration
- `Domaine/Exceptions/` — `FichierExpireException`, `FichierIntrouvableException`
- `Infrastructure/` — implémentations concrètes (chiffrement AES-256-GCM, stockage fichier, gestion des clés)
- `Configuration/` — classes de configuration typées (`ConfigurationStockage`, `ConfigurationChiffrement`, `ConfigurationCles`, `ConfigurationNettoyage`)

### Api
- FastEndpoints (pas de contrôleurs MVC) avec CQRS via MediatR
- `Endpoints/V1/` — endpoints FastEndpoints qui délèguent à MediatR
- `Application/Upload/` et `Application/Telechargement/` — commandes/requêtes MediatR + validateurs FluentValidation
- Authentification OIDC/JWT optionnelle, activée via `appsettings.json` → section `"Authentification"` → `"Activer": true`
- `CorrelationIdMiddleware.cs` — injecte un ID de corrélation dans chaque requête

### Worker
Deux `BackgroundService` hébergés :
- `WorkerNettoyage` — supprime périodiquement les fichiers expirés (intervalle configurable)
- `WorkerRotationCles` — effectue la rotation des clés de chiffrement

## Chiffrement

Format de fichier chiffré : chaque fichier est découpé en chunks (défaut 1 Mo), chaque chunk étant chiffré avec AES-256-GCM indépendamment. La clé de chiffrement par fichier est dérivée de la clé maître via HKDF-SHA-256 avec un sel aléatoire unique stocké dans les métadonnées.

Format binaire :
```
[4 octets LE : nombre de chunks]
[Pour chaque chunk : 12 nonce + 16 tag + 4 longueur + N données chiffrées]
```

## Cycle de vie d'un fichier

1. Upload → génération d'un GUID comme identifiant, chiffrement, écriture `.enc` + `.meta`
2. Téléchargement → lecture métadonnées, vérification expiration, déchiffrement en streaming
3. Expiration → basée sur `DateExpiration` (durée de vie) et/ou `NombreAccesMax`
4. Nettoyage → `WorkerNettoyage` supprime les fichiers dont `MetadonneesFichier.EstExpire()` retourne `true`

## Tests

Framework : xunit + NSubstitute (mocks) + FluentAssertions. Les tests couvrent les gestionnaires MediatR, les validateurs FluentValidation et l'entité domaine.

## Configuration

Les deux projets exécutables ont leur propre `appsettings.json`. Les sections clés sont les mêmes dans les deux :
- `Stockage` — chemin racine, extensions `.enc` et `.meta`
- `Chiffrement` — taille des chunks
- `Cles` — chemin du fichier trousseau JSON
- `Nettoyage` — intervalle en minutes (Worker uniquement)
- `Authentification` — activation OIDC, autorité, audience, scopes (Api uniquement)
