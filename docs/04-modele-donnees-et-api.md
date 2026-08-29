# 04 — Modèle de données et contrat d'API

## 1. Conventions

- Échanges en **JSON**, encodage **UTF-8**.
- Dates au format **ISO 8601 avec fuseau** (`2026-12-31T23:59:59+01:00`). Le formatage à
  l'affichage relève du client, jamais du serveur.
- Noms de propriétés en `camelCase` côté JSON ; les DTO C# utilisent `PascalCase` avec une
  politique de nommage `JsonNamingPolicy.CamelCase`.
- Toutes les routes sont **authentifiées**. Celles marquées 🔒 exigent en outre le rôle
  administrateur.
- Erreurs au format **RFC 7807** (`application/problem+json`).

## 2. Paramètres d'impression

### 2.1 Structure JSON

```json
{
  "profilId": "fac-2024",
  "nom": "Facture",
  "estDefaut": true,
  "portee": "societe",
  "associationsTypesDocument": ["facture", "avoir"],

  "miseEnPage": {
    "format": "A4",
    "largeurMm": null,
    "hauteurMm": null,
    "orientation": "portrait",
    "margeHautMm": 15,
    "margeBasMm": 15,
    "margeGaucheMm": 12,
    "margeDroiteMm": 12,
    "margesLiees": false,
    "rectoVerso": "simple",
    "echelleMode": "pourcentage",
    "echellePourcent": 100
  },

  "logo": {
    "donnees": "data:image/png;base64,iVBORw0KGgo…",
    "nomFichier": "logo-acme.png",
    "position": "gauche",
    "hauteurMm": 15,
    "afficheSurToutesPages": false
  },

  "entetePied": {
    "titreDocument": "{{titreDocument}}",
    "enteteGauche": "{{societe}}",
    "enteteCentre": "",
    "enteteDroite": "{{date}}",
    "piedGauche": "{{utilisateur}}",
    "piedCentre": "Page {{page}} / {{pages}}",
    "piedDroite": "v{{version}}",
    "hauteurEnteteMm": 20,
    "hauteurPiedMm": 12,
    "traitSousEntete": true,
    "traitSurPied": true,
    "policeEntetePied": "Arial",
    "taillePoliceEntetePt": 8
  },

  "rendu": {
    "couleur": "couleur",
    "qualite": "normale",
    "imprimerImagesFond": false,
    "policeDefaut": "Arial",
    "taillePoliceDefautPt": 10
  },

  "filigrane": {
    "actif": false,
    "texte": "BROUILLON",
    "opacite": 15,
    "rotationDeg": -45,
    "couleur": "#999999"
  },

  "tableaux": {
    "repeterEntetesColonnes": true,
    "eviterCoupureLignes": true,
    "quadrillage": "complet"
  },

  "imprimante": {
    "imprimanteSuggeree": "HP LaserJet Comptabilité",
    "bacPapier": "Bac 2",
    "nombreCopies": 1,
    "assembler": true
  },

  "modifieLe": "2026-08-29T14:12:00+02:00",
  "modifiePar": "flaitselart"
}
```

### 2.2 Points d'entrée

| Méthode | Route | Objet |
|---|---|---|
| `GET` | `/api/parametrages/impression/profils` | Liste des profils visibles (société + personnels) |
| `GET` | `/api/parametrages/impression/profils/{id}` | Un profil |
| `POST` | `/api/parametrages/impression/profils` | Créer |
| `PUT` | `/api/parametrages/impression/profils/{id}` | Modifier |
| `DELETE` | `/api/parametrages/impression/profils/{id}` | Supprimer |
| `POST` | `/api/parametrages/impression/profils/{id}/defaut` | Définir comme défaut |
| `POST` | `/api/parametrages/impression/logo` | Téléverser un logo (`multipart/form-data`) |
| `DELETE` | `/api/parametrages/impression/logo/{id}` | Supprimer un logo |
| `GET` | `/api/parametrages/impression/valeurs-usine` | Valeurs par défaut |

**Stockage du logo** — deux options :

- **Data URI dans le profil** : simple, aucun point d'entrée de fichier, mais alourdit chaque
  lecture du profil (un PNG de 200 Ko devient ~270 Ko de base64, rechargé à chaque ouverture).
- **Fichier référencé** (recommandé) : `POST /logo` renvoie un identifiant, le profil ne porte
  que la référence, le fichier est servi avec un cache HTTP. Préférable dès que le profil est
  lu souvent.

### 2.3 DTO C# (esquisse)

```csharp
public sealed record ProfilImpressionDto
{
    public string   ProfilId { get; init; } = "";
    public string   Nom { get; init; } = "";
    public bool     EstDefaut { get; init; }
    public string   Portee { get; init; } = "utilisateur";   // utilisateur | societe
    public string[] AssociationsTypesDocument { get; init; } = [];

    public MiseEnPageDto  MiseEnPage  { get; init; } = new();
    public LogoDto?       Logo        { get; init; }
    public EntetePiedDto  EntetePied  { get; init; } = new();
    public RenduDto       Rendu       { get; init; } = new();
    public FiligraneDto   Filigrane   { get; init; } = new();
    public TableauxDto    Tableaux    { get; init; } = new();
    public ImprimanteDto  Imprimante  { get; init; } = new();

    public DateTimeOffset ModifieLe  { get; init; }
    public string         ModifiePar { get; init; } = "";
}

public sealed record MiseEnPageDto
{
    public string Format      { get; init; } = "A4";
    public int?   LargeurMm   { get; init; }
    public int?   HauteurMm   { get; init; }
    public string Orientation { get; init; } = "portrait";
    public int    MargeHautMm { get; init; } = 10;
    public int    MargeBasMm  { get; init; } = 10;
    public int    MargeGaucheMm { get; init; } = 10;
    public int    MargeDroiteMm { get; init; } = 10;
    public bool   MargesLiees   { get; init; } = true;
    public string RectoVerso    { get; init; } = "simple";
    public string EchelleMode   { get; init; } = "pourcentage";
    public int    EchellePourcent { get; init; } = 100;
}
```

**La validation doit être répliquée côté serveur** — les bornes de marges, le poids et le type
du logo, l'assainissement du SVG. La validation JavaScript est une commodité d'interface,
jamais une garantie.

## 3. Diagnostic

| Méthode | Route | Réponse |
|---|---|---|
| `GET` | `/api/diagnostic/sante` | `{ "statut": "ok", "horodatage": "…" }` — volontairement minimal et rapide |
| `POST` | `/api/diagnostic/echo` | Renvoie le corps reçu, pour la mesure de latence |
| `GET` | `/api/diagnostic/horloge` | `{ "heureServeur": "2026-08-29T14:37:02+02:00" }` |
| `GET` | `/api/diagnostic/versions` | Versions application, back-end, schéma, moteur de base |
| `GET` | 🔒 `/api/diagnostic/base-donnees` | `{ "accessible": true, "dureeMs": 12, "moteur": "…" }` |
| `GET` | 🔒 `/api/diagnostic/referentiels` | Liste des paramétrages obligatoires manquants |
| `GET` | `/api/diagnostic/droits` | Rôles et écrans accessibles à l'appelant |
| `POST` | `/api/diagnostic/courriel-test` | Envoie un message à l'utilisateur — **débit limité** |
| `POST` | `/api/diagnostic/rapport` | Archive un rapport, renvoie sa référence |

**`/sante` ne doit toucher ni la base ni un service tiers** : c'est le test qui doit répondre
même quand tout le reste est en panne, sinon il ne distingue plus « serveur injoignable » de
« base indisponible ». La vérification de la base a son propre point d'entrée.

### 3.1 Format du rapport

```json
{
  "reference": "DIAG-7F3A-2610",
  "horodatage": "2026-08-29T14:37:00+02:00",
  "scoreSante": 8,
  "scoreMaximum": 10,
  "contexte": {
    "utilisateur": "flaitselart",
    "societe": "ACME SARL",
    "versionApplication": "4.7.2",
    "versionBackend": "4.7.2",
    "environnement": "PRODUCTION",
    "navigateur": "Chrome 141.0.0.0",
    "systeme": "Windows 11",
    "langue": "fr-FR",
    "fuseau": "Europe/Paris"
  },
  "resultats": [
    {
      "id": "api.sante",
      "libelle": "Disponibilité du serveur",
      "categorie": "connectivite",
      "severite": "bloquant",
      "statut": "ok",
      "dureeMs": 142,
      "message": "Le serveur répond normalement.",
      "details": { "codeHttp": 200 }
    },
    {
      "id": "reseau.horloge",
      "libelle": "Synchronisation de l'horloge",
      "categorie": "connectivite",
      "severite": "majeur",
      "statut": "avertissement",
      "dureeMs": 138,
      "message": "L'horloge de ce poste avance de 3 min 12 s sur le serveur.",
      "remediation": "Activez la synchronisation automatique de l'heure dans Windows.",
      "details": { "ecartSecondes": 192 }
    }
  ]
}
```

## 4. Session, version et licence

| Méthode | Route | Objet |
|---|---|---|
| `GET` | `/api/session/moi` | Identité, rôles, société, dates de session, expiration |
| `POST` | `/api/session/prolonger` | Renouvelle le jeton, renvoie la nouvelle échéance |
| `GET` | `/api/session/appareils` | Sessions actives de l'utilisateur |
| `DELETE` | `/api/session/appareils/{id}` | Déconnecte un appareil |
| `PUT` | `/api/session/langue` | `{ "langue": "fr-FR" }` |
| `PUT` | `/api/session/formats` | Formats régionaux |
| `GET` | `/api/version` | Versions et environnement |
| `GET` | `/api/licence` | Licence et modules |

### 4.1 `GET /api/session/moi`

```json
{
  "authentifie": true,
  "utilisateur": {
    "identifiant": "flaitselart",
    "nom": "Laitselart",
    "prenom": "Francis",
    "courriel": "francis.laitselart@exemple.fr",
    "roles": ["Administrateur", "Comptable"]
  },
  "societe": { "code": "ACME", "raisonSociale": "ACME SARL", "etablissement": "Paris" },
  "session": {
    "ouverteLe": "2026-08-29T12:23:00+02:00",
    "expireLe": "2026-08-29T16:23:00+02:00",
    "adresseIp": "203.0.113.42",
    "derniereConnexionReussie": "2026-08-28T08:41:00+02:00",
    "dernierEchecConnexion": null
  },
  "preferences": {
    "langue": "fr-FR",
    "fuseau": "Europe/Paris",
    "formatDate": "dd/MM/yyyy",
    "formatHeure": "24h",
    "separateurDecimal": ",",
    "devise": "EUR",
    "premierJourSemaine": 1
  }
}
```

### 4.2 `GET /api/version`

```json
{
  "application": { "version": "4.7.2", "build": "2026.08.24.1731",
                   "commit": "a3f91c2", "dateCompilation": "2026-08-24T17:31:00+02:00" },
  "backend":     { "version": "4.7.2", "assemblies": [
                     { "nom": "MyPn.Core", "version": "4.7.2.0" },
                     { "nom": "MyPn.Impression", "version": "4.7.1.0" } ] },
  "baseDonnees": { "versionSchema": 147, "moteur": "SQL Server 2022", "edition": "16.0.4165" },
  "environnement": { "type": "PRODUCTION", "serveur": "SRV-APP-02" },
  "lienNotesDeVersion": "https://…/changelog/4.7.2"
}
```

Les versions d'assembly s'obtiennent en C# par
`Assembly.GetExecutingAssembly().GetName().Version` ou, pour la version informationnelle
(incluant l'empreinte de commit), par `AssemblyInformationalVersionAttribute`.

### 4.3 `GET /api/licence`

```json
{
  "numero": "LIC-2024-8891",
  "titulaire": "ACME SARL",
  "typeContrat": "abonnement",
  "debutValidite": "2026-01-01T00:00:00+01:00",
  "finValidite": "2026-12-31T23:59:59+01:00",
  "finMaintenance": "2026-12-31T23:59:59+01:00",
  "utilisateursAutorises": 25,
  "utilisateursConsommes": 18,
  "sessionsSimultaneesAutorisees": 10,
  "sessionsSimultaneesEnCours": 4,
  "derniereVerification": "2026-08-29T06:00:00+02:00",
  "modules": [
    { "code": "FACT", "libelle": "Facturation", "etat": "actif",
      "echeance": null, "description": "Émission et suivi des factures" },
    { "code": "PORT", "libelle": "Portail client", "etat": "evaluation",
      "echeance": "2026-09-15T23:59:59+02:00", "description": "Accès externe pour vos clients" },
    { "code": "MULTI", "libelle": "Multi-établissements", "etat": "nonSouscrit",
      "echeance": null, "description": "Gestion de plusieurs sites" }
  ]
}
```

**Le calcul des seuils d'alerte (60 / 30 / 7 jours) appartient au client**, à partir des dates
brutes : le serveur reste ainsi indépendant de la politique d'affichage, et l'alerte suit le
fuseau réel de l'utilisateur.
