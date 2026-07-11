# Zorg320.Utilitaire.FichierTemporaire

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![Build](https://github.com/BrunoUnterberger/Zorg320.Utilitaire.FichierTemporaire/workflows/.NET/badge.svg)

## 📋 Description

Zorg320.Utilitaire.FichierTemporaire est une solution .NET 8 complète pour la gestion sécurisée des fichiers temporaires. Elle offre une API REST pour uploader et télécharger des fichiers avec chiffrement intégré, ainsi qu'un worker d'arrière-plan pour le nettoyage automatique et la rotation des clés de chiffrement.

## ✨ Fonctionnalités principales

- **API REST** pour l'upload, la vérification et le téléchargement de fichiers
- **Chiffrement** automatique des fichiers stockés
- **Gestion des clés** de chiffrement avec rotation automatique
- **Nettoyage** automatique des fichiers temporaires expirés
- **Logging** structuré avec Serilog
- **Validation** des requêtes avec FluentValidation
- **Documentation Swagger** de l'API

## 🏗️ Architecture

La solution est composée de 3 projets principaux :

### 1. **Zorg320.Utilitaire.FichierTemporaire.Api**
API REST basée sur FastEndpoints pour :
- Upload de fichiers
- Vérification de disponibilité d'un fichier
- Téléchargement de fichiers

### 2. **Zorg320.Utilitaire.FichierTemporaire.Noyau**
Bibliothèque partagée contenant :
- Interfaces applicatives
- Configurations typées
- Services de chiffrement
- Services de stockage
- Gestion des clés cryptographiques
- Entités métier
- Exceptions personnalisées

### 3. **Zorg320.Utilitaire.FichierTemporaire.Worker**
Service d'arrière-plan (Hosted Service) pour :
- **Nettoyage** automatique des fichiers temporaires
- **Rotation des clés** de chiffrement

## 🛠️ Technologie

- **Framework** : .NET 8.0
- **API** : FastEndpoints 5.x avec Swagger
- **Architecture** : MediatR (CQRS)
- **Validation** : FluentValidation 11.x
- **Logging** : Serilog 8.x (Console + File)
- **Chiffrement** : Infrastructure intégrée
- **Requêtes HTTP** : Flurl 4.x

## 🔐 Sécurité

- Chiffrement des fichiers au repos
- Gestion centralisée des clés
- Rotation automatique des clés
- Validation stricte des entrées
- Logging structuré des opérations

## 📁 Configuration

Les fichiers de configuration se trouvent dans les `appsettings.json` :

- **Stockage** : Chemin racine, extensions des fichiers chiffrés et métadonnées
- **Chiffrement** : Taille des chunks pour le chiffrement par streaming
- **Clés** : Chemin du trousseau de clés
- **Nettoyage** : Intervalle de nettoyage des fichiers temporaires
- **Serilog** : Configuration du logging (Console et File)

```json
{
  "Stockage": {
    "CheminRacine": "C:/temp/fichiers-temporaires",
    "ExtensionFichier": ".enc",
    "ExtensionMetadonnees": ".meta"
  },
  "Chiffrement": {
    "TailleChunkOctets": 1048576
  },
  "Cles": {
    "CheminFichierTrousseau": "C:/temp/fichiers-temporaires/cles/trousseau.json"
  },
  "Nettoyage": {
    "IntervalleMinutes": 60
  }
}
```

## 🚀 Démarrage

### Prérequis
- .NET 8.0 SDK ou supérieur

### Installation
```bash
dotnet restore
```

### Lancement de l'API
```bash
cd src/Zorg320.Utilitaire.FichierTemporaire.Api
dotnet run
```

L'API sera disponible sur `https://localhost:5001` avec la documentation Swagger sur `/swagger`

### Lancement du Worker
```bash
cd src/Zorg320.Utilitaire.FichierTemporaire.Worker
dotnet run
```

## 📊 Endpoints API

| Méthode | Route | Description |
|---------|-------|-------------|
| `POST` | `/v1/fichiers` | Upload d'un fichier avec ses limites d'expiration |
| `HEAD` | `/v1/fichiers/{identifiant}` | Vérifier la disponibilité sans consommer d'accès |
| `GET` | `/v1/fichiers/{identifiant}` | Télécharger le fichier déchiffré en streaming |

> Voir la documentation Swagger sur `/swagger` pour le détail des paramètres et des réponses.

## 📝 Logs

Les logs sont générés dans le répertoire `logs/` avec rotation quotidienne et rétention de 30 jours.

## 📄 Licence

Propriétaire - Zorg320

## 👥 Contributeurs

Équipe Zorg320
