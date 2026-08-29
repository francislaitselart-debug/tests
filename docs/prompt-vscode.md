# Prompt à coller dans une session Claude Code locale (VS Code)

Ce fichier contient un prompt autonome, à copier tel quel dans une conversation
distincte ouverte sur le dépôt réel de `MyPnParametrages`.

---

## Contexte

Tu interviens sur une application métier dont le front-end est écrit en **JavaScript sans
framework** (+ CSS) et dont le back-end est une **DLL C#** exposant des points d'entrée HTTP.
L'impression se fait actuellement via le navigateur (`window.print()`).

Je veux faire évoluer le composant **`MyPnParametrages`** en lui ajoutant **trois sections** :
**Impressions**, **Diagnostic**, et **Ma session**.

Une spécification détaillée a été rédigée en amont, sans accès au code. Tu as le code sous les
yeux : ton rôle est de **confronter cette spécification à la réalité du projet**, puis
d'implémenter.

Spécification complète, si tu veux la consulter :
`https://github.com/francislaitselart-debug/tests/tree/claude/mypnparametrages-evolution-8oh5kw`

---

## Phase 0 — Reconnaissance (à faire AVANT toute écriture de code)

Ne code rien tant que tu n'as pas répondu à ces questions, en me citant les fichiers et les
lignes concernés :

1. **Où vit `MyPnParametrages` ?** Fichiers, taille, style d'écriture (classes ES, IIFE,
   prototypes, jQuery ?), conventions de nommage (français ou anglais ?).
2. **Comment les sections existantes sont-elles structurées ?** Y a-t-il déjà un mécanisme
   d'onglets ou de panneaux auquel se greffer, ou faut-il le créer ?
3. **Comment les paramètres sont-ils persistés aujourd'hui ?** Quel appel HTTP, quel format,
   quelle gestion d'erreur ? Existe-t-il une couche de transport réutilisable ?
4. **Y a-t-il un mécanisme d'internationalisation ?** Les libellés sont-ils externalisés, ou
   en dur dans le code ? *(Décisif : le sélecteur de langue en dépend entièrement.)*
5. **Le back-end C# expose-t-il déjà une API HTTP JSON ?** Quel routage, quelle
   authentification, quel format d'erreur ? Où ajouterais-tu de nouveaux points d'entrée ?
6. **Comment l'impression est-elle déclenchée aujourd'hui ?** Y a-t-il des feuilles de style
   `@media print` ou `@page` existantes ? Une génération PDF côté serveur quelque part ?
7. **Quel outillage de test existe ?** Runner, tests existants, lint, build.

Termine la phase 0 par : ce qui colle à la spécification, ce qui ne colle pas, et ta
**contre-proposition de découpage** avec ton propre chiffrage.

---

## Contraintes techniques à connaître d'emblée

L'impression navigateur ne permet **pas** trois choses demandées. Ne les promets pas dans
l'interface :

| Besoin | Faisable en `window.print()` ? |
|---|---|
| Marges, orientation, format papier | ✅ via `@page { size / margin }` |
| Logo, titre, en-tête, pied de page | ⚠️ seulement par contournement `<thead>`/`<tfoot>` répétés |
| **Numéros de page `{{page}} / {{pages}}`** | ❌ `counter(page)` et les boîtes de marge `@top-center` ne sont implémentés ni par Chrome ni par Firefox |
| **Choix de l'imprimante** | ❌ aucune API web ne liste ni ne présélectionne une imprimante |
| Échelle d'impression | ❌ non pilotable en CSS (seulement `transform: scale()`, avec effets de bord sur la pagination) |

**Conséquence :** vérifie s'il existe déjà une génération PDF côté C#. Si oui, propose de
brancher les paramètres dessus (tout devient faisable). Sinon, implémente le paramétrage tel
quel mais :
- retire `{{page}}` et `{{pages}}` du menu d'insertion de variables ;
- affiche un encart explicite près des champs d'imprimante : *« Ces réglages sont indicatifs ;
  la boîte de dialogue d'impression de votre navigateur reste maîtresse du choix final. »*

Ne laisse pas l'interface suggérer un pilotage qui n'existe pas.

---

## Section 1 — Impressions

Écran en deux colonnes : formulaire à gauche, **aperçu miniature en direct à droite**
(redessiné à chaque changement, anti-rebond 150 ms). L'aperçu est l'apport ergonomique
principal : personne ne se représente l'effet d'une marge dans l'abstrait.

**Mise en page** — format (A4/A3/A5/Letter/Legal/personnalisé), orientation, 4 marges en mm
avec case « marges liées » qui propage la saisie aux quatre, recto-verso, échelle.

**Logo** — PNG/JPEG/SVG, 512 Ko max, glisser-déposer, position (gauche/centre/droite), hauteur
en mm, première page seulement ou toutes. Contrôle du **type MIME réel sur les premiers
octets**, pas de l'extension. **Assainis le SVG avant stockage** (retire `<script>`,
`<foreignObject>`, attributs `on*`) : un SVG est un document exécutable.

**Titre, en-tête, pied de page** — 3 zones en-tête + 3 zones pied (gauche/centre/droite) +
titre du document. Chaque zone accepte des variables, insérables via un **menu « Insérer une
variable »** plutôt que saisies de mémoire : `{{date}}`, `{{heure}}`, `{{utilisateur}}`,
`{{societe}}`, `{{etablissement}}`, `{{titreDocument}}`, `{{version}}` (+ `{{page}}`/`{{pages}}`
seulement si génération PDF serveur). Hauteurs d'en-tête et de pied, traits de séparation,
police et corps.

**Rendu** — couleur/N&B, qualité, impression des images de fond, police et corps par défaut.

**Filigrane** — texte (BROUILLON / COPIE / CONFIDENTIEL), opacité, rotation, couleur.

**Tableaux** — répéter les en-têtes de colonnes (`thead { display: table-header-group }`),
éviter la coupure des lignes (`break-inside: avoid`), quadrillage.

**Imprimante** — imprimante suggérée, bac, nombre de copies, assemblage. *Informatif.*

**Profils** — jeux de paramètres nommés (« Facture », « Devis », « Liste interne »),
créer/dupliquer/renommer/supprimer, définir par défaut, association à des types de document,
import/export JSON. Un profil « Par défaut » non supprimable existe toujours.

**Validations** — somme des marges < dimension de la page ; avertissement sous 5 mm (zone non
imprimable) ; poids et type du logo ; unicité du nom de profil ; variable inconnue signalée.
**Réplique toutes ces validations côté C#** : celles du JavaScript sont une commodité
d'interface, pas une garantie.

**Application des paramètres** — génère dynamiquement une balise `<style>` unique, régénérée à
chaque changement de profil, contenant la règle `@page`. Elle doit être présente **avant**
l'appel à `print()`, sinon elle est ignorée.

**Confort** — bouton « Imprimer une page de test » produisant un document témoin avec une
**règle graduée en millimètres**, l'en-tête, le pied et le logo, pour vérifier le rendu réel.

---

## Section 2 — Diagnostic

Objectif : qu'un utilisateur, ou un technicien qui le guide au téléphone, sache en moins d'une
minute si l'application fonctionne sur ce poste et, sinon, où ça coince.

Le livrable réellement utile n'est pas l'affichage des voyants verts, c'est le **rapport
exportable** joint au ticket : il supprime l'aller-retour « quelle version ? quel
navigateur ? ».

### Architecture imposée : registre déclaratif

Les tests sont **déclarés**, l'interface est **générique**. Ajouter un test = déposer un
fichier et l'enregistrer, sans toucher à la vue, à la progression, à l'export ni à
l'historique.

Contrat d'un test :
- `id` (stable, ex. `api.sante`), `libelle`, `categorie`, `severite` (bloquant/majeur/mineur)
- `timeoutMs` (défaut 10000), `confidentiel` (exclu de l'export)
- `estApplicable()` optionnel
- `executer(signal) -> Promise<{ statut, message, details?, remediation? }>`
- `statut` ∈ `ok` | `avertissement` | `echec` | `nonApplicable`

Moteur : exécution **séquentielle** (sinon les mesures de latence sont faussées), délai maximal
par test, **annulation par `AbortController`**, événements de progression.
**Règle absolue : un test qui lève une exception ne doit jamais interrompre la campagne** —
c'est ce qui sépare un outil exploitable d'un gadget qui s'arrête au premier problème, donc
précisément quand on en a besoin.

### Catalogue (26 tests ; commence par les 14 de A et B)

**A — Connectivité** : disponibilité serveur · latence (moyenne sur 5 appels) · cohérence des
versions front/back · accès base de données · validité du jeton et temps restant ·
renouvellement du jeton · **écart d'horloge poste/serveur**.

> L'écart d'horloge mérite d'exister : une dérive de quelques minutes provoque des expirations
> de session inexpliquées, coûteuses à diagnostiquer et triviales à détecter.

**B — Environnement** : navigateur et version · fonctionnalités requises (`fetch`, WebSocket,
IndexedDB, presse-papiers, `Intl`) · stockage local et quota · cookies · affichage (résolution,
zoom, ratio de pixels) · locale et fuseau · support de l'impression.

**C — Fonctionnel** : chaîne d'impression · envoi de fichier · réception de fichier · exports
CSV/XLSX · envoi de courriel de test · droits et écrans accessibles · référentiels obligatoires
manquants · canal temps réel.

**D — Performance** : temps de chargement (`PerformanceNavigationTiming`) · mémoire JS
(`performance.memory`, Chromium seulement) · erreurs JS de la session · état du cache et du
service worker.

### Interface

Statuts ✅ ⚠️ ❌ ⏭️ ⏳, durée et bouton « Relancer » par ligne, détails repliés sauf en cas
d'échec, barre de progression avec **bouton Interrompre**, **score de santé** pondéré par
sévérité (un `bloquant` en échec plafonne le score), zone `aria-live="polite"`.

Mets le champ `remediation` en avant : *ce que l'utilisateur peut faire lui-même* vaut mieux
qu'un message d'erreur technique.

Les tests qui écrivent (courriel, téléversement) sont **décochés par défaut** et signalés : on
n'envoie pas un courriel à l'insu de l'utilisateur.

### Rapport

**Référence de corrélation** courte (`DIAG-7F3A-2610`) affichée à l'écran et présente dans le
rapport : l'utilisateur la dicte au support, qui retrouve le contexte immédiatement.

Trois sorties : copie en texte formaté (prêt à coller dans un ticket), export JSON, envoi
facultatif au serveur pour archivage. Historique des 10 dernières campagnes en local, pour
répondre à « depuis quand ? ».

### Confidentialité — impératif

Le rapport est destiné à sortir de l'entreprise.

1. **Aucun secret** : jetons, mots de passe, chaînes de connexion, clés d'API, en-têtes
   `Authorization`. Un test qui manipule un jeton n'en rapporte que les métadonnées
   (validité, échéance), jamais la valeur.
2. Un test `confidentiel: true` s'affiche à l'écran mais est **exclu de l'export**.
3. **Filtre les messages d'erreur serveur** avant affichage : ni trace d'exécution, ni chemin
   de fichier serveur — ce sont des renseignements pour un attaquant.
4. Les points d'entrée exposant l'infrastructure (base de données, référentiels) sont
   **réservés aux administrateurs**.
5. **Limite le débit** du test d'envoi de courriel, sous peine d'en faire un outil d'envoi
   détourné.

---

## Section 3 — Ma session

Quatre blocs sur un écran.

**① Connexion** — identité, rôles, société/établissement, état en ligne/hors ligne
(`navigator.onLine`), heure de connexion, durée, **compte à rebours avant expiration du jeton
avec bouton « Prolonger »** (+ avertissement discret à 5 minutes : transforme la déconnexion
brutale en pleine saisie en événement anticipé), dernière connexion réussie, dernier échec
(révèle une tentative d'accès), appareils connectés avec déconnexion à distance **sous
confirmation**, adresse IP vue par le serveur. **Jamais la valeur du jeton.**

**② Langue et formats** — sélecteur avec chaque langue **écrite dans sa propre langue**
(`Français`, `English`, `Deutsch`) : un utilisateur perdu dans une langue qu'il ne lit pas doit
retrouver la sienne. **Application immédiate sans rechargement.** **Double persistance** :
profil serveur (la langue suit l'utilisateur de poste en poste) + `localStorage` (correcte dès
le prochain affichage, sans attendre le serveur). Résolution au démarrage : profil →
`localStorage` → `navigator.language` → défaut.
Formats régionaux : date, heure 12/24 h, séparateur décimal et de milliers, devise, fuseau,
premier jour de la semaine — avec **un exemple vivant sous chaque champ** (`31/12/2026 —
1 234,56 €`), plus parlant qu'un nom de format. `Intl.DateTimeFormat` et `Intl.NumberFormat`
suffisent, sans bibliothèque.

**③ Version et environnement** — version applicative, build, empreinte de commit, date de
compilation, version back-end (**en rouge si différente du front**), versions des DLL
principales, version du schéma de base et moteur, nom du serveur, lien vers les notes de
version.
**L'environnement doit être un bandeau coloré, pas une ligne de tableau** : vert PRODUCTION,
orange RECETTE, bleu DÉVELOPPEMENT. La confusion recette/production est une source classique
d'incidents ; un indicateur discret ne suffit pas.

**④ Licence et modules** — numéro, titulaire, type de contrat, début et **fin de validité**,
**fin de maintenance**, utilisateurs autorisés/consommés avec barre, sessions simultanées,
date de dernière vérification.
**Alertes d'échéance à 60 j (orange), 30 j (rouge), 7 j (bandeau)** — c'est la raison d'être du
bloc : une licence qui expire un vendredi soir sans prévenir bloque l'exploitation le lundi.
Calcule les seuils **côté client** à partir des dates brutes, pour suivre le fuseau réel.
Tableau des modules : libellé, état (actif / évaluation / non souscrit), **échéance propre**,
description.

**Bouton « Copier les informations système »** — produit en une action un bloc texte prêt à
coller dans un ticket : utilisateur, société, rôles, versions, environnement, licence, modules,
navigateur, affichage, langue et fuseau, référence du dernier diagnostic. **Mêmes règles de
confidentialité que le rapport de diagnostic.**

---

## Architecture cible

Chaque section suit le même découpage, pour qu'une quatrième ne coûte que la reproduction du
motif :

```
mypn-parametrages/
  noyau/       magasin.js (état, chargement, sauvegarde, état « modifié »)
               transport.js (appels HTTP, erreurs)
               format.js (dates/nombres selon la locale)
  sections/
    impressions/  modele.js  vue.js  apercu.js  styles-impression.js
    diagnostic/   registre.js  moteur.js  tests/  vue.js  rapport.js
    session/      modele.js  vue.js  langue.js
```

**Adapte ce découpage aux conventions réelles du projet** — s'il a déjà une organisation, suis-la
plutôt que d'importer celle-ci.

**N'ajoute aucune dépendance front.** Les trois sections sont réalisables en JavaScript nu ;
inutile d'introduire un outillage de build là où il n'y en a peut-être pas.

**Dégradation propre** : toute information indisponible (licence non renseignée, point d'entrée
absent) s'affiche « non disponible » ou `nonApplicable`, sans faire échouer la section entière.

---

## Points d'entrée C# à créer

Routes indicatives, à aligner sur le routage existant. Toutes authentifiées ; 🔒 = réservé aux
administrateurs. Dates en **ISO 8601 avec fuseau**, JSON en `camelCase`, erreurs en RFC 7807.

**Impressions** — `GET|POST /parametrages/impression/profils`,
`GET|PUT|DELETE …/profils/{id}`, `POST …/profils/{id}/defaut`,
`POST|DELETE …/logo`, `GET …/valeurs-usine`.

**Diagnostic** — `GET /diagnostic/sante` · `POST /diagnostic/echo` ·
`GET /diagnostic/horloge` · `GET /diagnostic/versions` · 🔒 `GET /diagnostic/base-donnees` ·
🔒 `GET /diagnostic/referentiels` · `GET /diagnostic/droits` ·
`POST /diagnostic/courriel-test` (débit limité) · `POST /diagnostic/rapport`.

> `/sante` ne doit toucher **ni la base ni un service tiers** : c'est le test qui doit répondre
> quand tout le reste est en panne, sinon il ne distingue plus « serveur injoignable » de
> « base indisponible ». La base a son propre point d'entrée.

**Session** — `GET /session/moi` · `POST /session/prolonger` ·
`GET|DELETE /session/appareils[/{id}]` · `PUT /session/langue` · `PUT /session/formats` ·
`GET /version` · `GET /licence`.

Les versions d'assembly s'obtiennent par `Assembly.GetName().Version` ou, avec l'empreinte de
commit, par `AssemblyInformationalVersionAttribute`.

---

## Exigences transverses

- **Accessibilité** : navigation clavier complète, `<label>` associés, `role="tablist"` /
  `"tab"` / `"tabpanel"` sur les onglets, `aria-live="polite"` sur les résultats de diagnostic.
- **État « modifié »** : avertir avant d'abandonner des changements non enregistrés.
- **Libellés externalisés** dès l'écriture, même si l'internationalisation n'existe pas encore.
- **Tests unitaires** au minimum sur : validation des paramètres d'impression, moteur de
  diagnostic (délai maximal, annulation, test qui lève une exception), formatage régional.

---

## Méthode de travail attendue

1. Fais la **phase 0** et présente-moi tes constats avant d'écrire une ligne.
2. Propose ton **découpage en lots avec ton chiffrage**, à partir du code réel.
3. Implémente **une section à la fois**, en commençant par le socle commun. Fais-moi valider
   chaque section avant de passer à la suivante.
4. **Ordre recommandé : Diagnostic avant Impressions** si l'objectif est d'alléger la charge du
   support — c'est le meilleur rapport valeur/charge et c'est utile dès le premier incident.
   Impressions d'abord si la demande vient des utilisateurs finaux.
5. Signale-moi tout écart entre cette spécification et ce que le code permet réellement,
   **plutôt que de contourner silencieusement**.

## Questions à me poser avant de coder

- Faut-il des **profils d'impression multiples**, ou un jeu unique suffit-il ?
- Les paramètres sont-ils **par utilisateur, par société, ou hérités** (société → surcharge
  utilisateur) ?
- Une **génération PDF côté serveur** existe-t-elle ou est-elle envisageable ? *(Cela change
  tout pour l'en-tête, le pied de page et la numérotation.)*
- Quels navigateurs faut-il supporter ?
- Le test d'envoi de courriel est-il souhaité, ou trop intrusif dans votre contexte ?
