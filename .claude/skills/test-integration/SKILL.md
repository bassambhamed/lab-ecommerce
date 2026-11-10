---
name: test-integration
description: Écrit des tests d'intégration xUnit pour les API ECommerce (Catalog.Api, Ordering.Api) avec WebApplicationFactory, une base en mémoire isolée par test et un faux Catalog pour Ordering. À utiliser pour créer un projet de tests, ajouter un test d'API ou reproduire un bug d'API par un test rouge.
argument-hint: "[API ou scénario à tester]"
allowed-tools: Bash(dotnet new xunit *), Bash(dotnet sln *), Bash(dotnet add * reference *), Bash(dotnet build *), Bash(dotnet test *)
---
# Tests d'intégration ECommerce

Demande : $ARGUMENTS

## Les pièges, réglés une fois pour toutes

1. **Un projet de tests par API** : `tests/ECommerce.Catalog.Api.Tests` et
   `tests/ECommerce.Ordering.Api.Tests`. Les deux API ont chacune une classe `Program` :
   un même projet de tests qui référencerait les deux ne compilerait pas (type ambigu).
2. **Création** : `dotnet new xunit -o tests/<Projet>`, référence vers l'API testée,
   paquet `Microsoft.AspNetCore.Mvc.Testing` dans la version des paquets ASP.NET Core du
   dépôt (10.0.x : regarder `Microsoft.AspNetCore.OpenApi` dans le `.csproj` de l'API),
   puis `dotnet sln ECommerce.slnx add tests/<Projet>/<Projet>.csproj`.
3. **`WebApplicationFactory<Program>`** : depuis .NET 10, `Program` est public. N'ajoute
   pas `public partial class Program` à l'API.
4. **Une base isolée par test** : `Program.cs` utilise une base en mémoire nommée
   (`"catalog"`, `"ordering"`), partagée par tous les hôtes du processus de test. Dans
   `ConfigureTestServices`, retire `DbContextOptions<T>` et
   `IDbContextOptionsConfiguration<T>`, puis réenregistre le contexte avec un nom unique.
   Crée une fabrique **par test** (constructeur de la classe de test, `IDisposable`).
5. **Ordering appelle Catalog** : fournis l'adresse que l'AppHost injecte d'habitude
   (`UseSetting("services:catalog:http:0", "http://catalog.test")`) et remplace le
   gestionnaire HTTP du client typé par un **faux Catalog** :
   `services.AddHttpClient<CatalogServiceClient>().ConfigurePrimaryHttpMessageHandler(...)`.
   Aucun test n'appelle le vrai Catalog.
6. **Écrire le JSON exact** envoyé par le client (chaîne brute `"""..."""`) : c'est le
   seul moyen de tester une propriété absente ou mal typée. Vérifie d'abord le code HTTP,
   puis le corps. `Status` voyage en chaîne : désérialise dans un `record` local au test,
   qui représente la vue du consommateur.
7. **Nommage** : `Methode_Scenario_Resultat`, par exemple `MissingItemsProperty_Returns400`.

Le fichier [modeles.md](modeles.md) de ce skill contient la fabrique, le faux Catalog et un
test d'exemple, vérifiés sur ce dépôt. Pars de ces modèles.

## Rouge, puis vert

- Un test qui reproduit un bug doit d'abord **échouer** : lance-le et montre l'échec
  (code attendu, code obtenu) avant de toucher au code.
- Arrête-toi après le rouge si on te le demande : le test est commité tel quel, il devient
  la spécification.
- Pour passer au vert, corrige le **code de production**. Ne modifie jamais un test pour le
  faire passer. Si un test te semble faux, arrête-toi et explique pourquoi.

## Vérification et restitution

Lance `dotnet test ECommerce.slnx` et montre le résumé de chaque projet (réussis, échoués,
total). Liste les tests ajoutés avec le scénario couvert par chacun.
