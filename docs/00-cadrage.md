# 00 — Cadrage

## 1. Besoin exprimé

Le composant `MyPnParametrages` doit accueillir trois nouvelles sections :

1. **Paramétrage des impressions** — marges, orientation par défaut, imprimante par défaut,
   sélection d'un logo, configuration du titre, de l'en-tête et du bas de page.
2. **Diagnostic** — lancement de tests de vérification du bon fonctionnement de l'application.
3. **Ma session** — consultation par l'utilisateur des informations essentielles de sa session :
   état de connexion, langue courante *et sa modification*, informations détaillées de version
   (numéro, options activées, date de validité, date de maintenance).

## 2. Hypothèses de travail

Le code source de `MyPnParametrages` n'était pas accessible au moment de la rédaction.
La spécification s'appuie donc sur les hypothèses suivantes, **à confirmer** :

| # | Hypothèse | Impact si fausse |
|---|---|---|
| H1 | `MyPnParametrages` est un composant JavaScript sans framework, manipulant le DOM directement | Réécriture de la couche vue uniquement ; le modèle et l'API restent valides |
| H2 | Le back-end C# expose déjà des points d'entrée HTTP JSON et une authentification par jeton | Ajout d'une couche de transport à chiffrer en plus |
| H3 | Il existe une notion d'utilisateur authentifié et de société/établissement de rattachement | La portée « société » (D2) tombe |
| H4 | Le composant dispose déjà d'un mécanisme de sauvegarde de paramètres | Étape 1 du backlog à réévaluer à la hausse |
| H5 | Aucune bibliothèque de génération PDF n'est présente côté serveur | Le lot D1-B devient plus court si une bibliothèque existe déjà |

## 3. Principes d'architecture retenus

### 3.1 Séparation modèle / vue / persistance

Chaque section suit le même découpage en trois modules, afin que l'ajout d'une quatrième
section ultérieure ne coûte que la reproduction du motif :

```
mypn-parametrages/
  noyau/
    magasin.js            # état, chargement, sauvegarde, état « modifié »
    transport.js          # appels HTTP vers la DLL C#, gestion des erreurs
    format.js             # formatage dates/nombres selon la locale courante
  sections/
    impressions/
      modele.js           # valeurs par défaut, validation, normalisation
      vue.js              # construction du DOM, liaison aux champs
      apercu.js           # aperçu miniature
      styles-impression.js# génération de la feuille @page
    diagnostic/
      registre.js         # registre déclaratif des tests
      moteur.js           # exécution séquentielle, progression, annulation
      tests/              # un fichier par test
      vue.js
      rapport.js          # export JSON / presse-papiers
    session/
      modele.js
      vue.js
      langue.js           # changement de langue à chaud
```

### 3.2 Un registre déclaratif pour le diagnostic

Le point de conception le plus structurant : les tests de diagnostic sont **déclarés**, pas
codés dans l'interface. Ajouter un test ne demande aucune modification de la vue.
Voir [02-diagnostic.md](02-diagnostic.md) § 2.

### 3.3 Pas de dépendance externe nouvelle côté front

La pile étant du JavaScript sans framework, les trois sections sont réalisables sans
ajouter de bibliothèque tierce. Cela évite d'introduire un outillage de build là où il
n'y en a peut-être pas.

### 3.4 Dégradation propre

Toute information indisponible (licence non renseignée, point d'entrée de diagnostic absent)
s'affiche comme « non disponible » plutôt que de faire échouer la section entière.

## 4. Décisions en attente

### D1 — Mode de génération des documents imprimés

**C'est la décision la plus structurante du projet.** L'impression a été annoncée comme
« navigateur » (`window.print()`). Or trois des besoins exprimés ne sont **pas réalisables
de façon fiable** dans ce mode :

| Besoin | Réalisable en impression navigateur ? |
|---|---|
| Marges | ✅ Oui, via `@page { margin: … }` |
| Orientation par défaut | ✅ Oui, via `@page { size: A4 landscape }` |
| Format de papier | ✅ Oui, via `@page { size: … }` |
| Logo en en-tête | ⚠️ Partiellement — voir ci-dessous |
| Titre / en-tête / pied de page personnalisés | ⚠️ Partiellement |
| **Numéro de page `{{page}} / {{pages}}`** | ❌ **Non** |
| **Imprimante par défaut** | ❌ **Non** |

**Détail des deux impossibilités :**

- **Imprimante par défaut.** Aucune API navigateur ne permet de lister les imprimantes ni
  d'en présélectionner une. La boîte de dialogue native reprend systématiquement la main.
  Le paramètre ne peut être qu'*indicatif*.
- **Numérotation des pages.** Les boîtes de marge CSS (`@top-center`, `@bottom-right`) et le
  compteur `counter(page)` ne sont pas implémentés par Chrome ni Firefox. Les seuls numéros
  de page obtenables sont ceux que le navigateur insère lui-même dans ses propres en-têtes,
  non personnalisables et désactivables par l'utilisateur.
- **En-tête/pied répétés.** Un contournement existe : envelopper le contenu dans un `<table>`
  dont le `<thead>` et le `<tfoot>` se répètent sur chaque page. Il fonctionne, mais contraint
  la structure HTML des documents imprimés et supporte mal les mises en page complexes.

**Deux options :**

| | **D1-A — Navigateur seul** | **D1-B — PDF généré par le back-end C#** |
|---|---|---|
| Principe | Feuille `@page` injectée + `window.print()` | La DLL C# produit le PDF, le navigateur l'affiche/l'imprime |
| Marges, orientation, format | ✅ | ✅ |
| Logo, en-tête, pied de page | ⚠️ contournement `thead`/`tfoot` | ✅ maîtrise totale |
| Numéros de page | ❌ | ✅ |
| Filigrane | ⚠️ approximatif | ✅ |
| Imprimante par défaut | ❌ | ⚠️ possible seulement si le serveur imprime lui-même |
| Rendu identique sur tous les postes | ❌ dépend du navigateur | ✅ |
| Charge supplémentaire | — | + 3 à 4 j (bibliothèque + gabarits) |

**Recommandation : D1-B**, avec une bibliothèque .NET de génération PDF (QuestPDF sous
licence communautaire, ou iText). C'est la seule voie qui honore réellement l'ensemble
« logo + titre + en-tête + pied de page » demandé. L'interface de paramétrage, elle, est
**identique dans les deux cas** : seul le consommateur des paramètres change. La section
Impressions peut donc être développée avant que D1 soit tranché.

### D2 — Portée des paramètres

Trois modèles possibles :

- **Utilisateur seul** — le plus simple, chacun règle ses impressions.
- **Société seule** — cohérence garantie (le logo et le pied de page sont institutionnels).
- **Société + surcharge utilisateur avec héritage** — recommandé : le logo et les mentions
  légales sont fixés par l'administrateur, les marges et l'orientation restent réglables
  par chacun.

La recommandation est le troisième modèle, avec un indicateur visuel « hérité / personnalisé »
par champ et un bouton « Réinitialiser aux valeurs de la société ».

### D3 — Internationalisation existante

Le changement de langue à chaud (section Ma session) suppose un mécanisme de traduction.
S'il n'existe pas, il faut le créer — ce qui déborde largement du périmètre de ce composant.
Trois cas :

- **Mécanisme existant** → le sélecteur s'y branche : ~0,5 j.
- **Aucun mécanisme, langue unique** → le sélecteur affiche la langue mais ne la modifie pas ;
  la modification est signalée comme « à venir » : ~0,2 j.
- **Aucun mécanisme mais multilingue attendu** → chantier séparé à chiffrer, hors périmètre.

## 5. Exigences transverses

- **Accessibilité** — navigation clavier complète, libellés `<label>` associés, rôles ARIA sur
  les onglets (`role="tablist"`/`role="tab"`/`role="tabpanel"`), annonces des résultats de
  diagnostic via `aria-live="polite"`.
- **Sécurité** — aucune donnée sensible (jeton, mot de passe, chaîne de connexion) ne doit
  apparaître dans le rapport de diagnostic exportable. Voir [02-diagnostic.md](02-diagnostic.md) § 6.
- **Compatibilité** — cible à confirmer ; la spécification suppose des navigateurs à moteur
  Chromium/Gecko/WebKit récents.
- **État « modifié »** — toute section modifiée doit avertir avant abandon des changements.

## 6. Hors périmètre

- Création d'un mécanisme d'internationalisation complet (voir D3).
- Impression silencieuse ou pilotage matériel d'imprimante depuis le navigateur.
- Refonte graphique de `MyPnParametrages` au-delà de l'intégration des trois sections.
- Gestion des droits d'administration (on suppose l'existence d'un rôle administrateur).
