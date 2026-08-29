# 02 — Section « Diagnostic »

## 1. Objectif

Permettre à un utilisateur — ou à un technicien du support qui le guide au téléphone — de
répondre en moins d'une minute à la question : *« l'application fonctionne-t-elle
correctement sur ce poste, et sinon, où est-ce que ça coince ? »*

Le livrable réellement utile de cette section n'est pas l'affichage des voyants verts : c'est
le **rapport exportable** que l'utilisateur joint à son ticket. Il économise l'aller-retour
« quelle est votre version ? quel navigateur ? » qui coûte une journée à chaque incident.

## 2. Architecture : un registre déclaratif

Le principe structurant : **les tests sont déclarés, l'interface est générique**. Ajouter un
test consiste à déposer un fichier dans `tests/` et à l'enregistrer ; l'interface, la barre de
progression, l'export et l'historique n'ont pas à être touchés.

### 2.1 Contrat d'un test

```js
/**
 * @typedef {Object} ResultatDiagnostic
 * @property {'ok'|'avertissement'|'echec'|'nonApplicable'} statut
 * @property {string}  message      Une phrase, lisible par un non-technicien.
 * @property {Object} [details]     Données structurées, repliées par défaut.
 * @property {string} [remediation] Ce que l'utilisateur peut faire lui-même.
 */

/**
 * @typedef {Object} TestDiagnostic
 * @property {string} id                     Identifiant stable, ex. 'api.sante'
 * @property {string} libelle
 * @property {'connectivite'|'environnement'|'fonctionnel'|'performance'} categorie
 * @property {'bloquant'|'majeur'|'mineur'} severite
 * @property {number} timeoutMs              Défaut 10000
 * @property {boolean} [confidentiel]        Exclu du rapport exporté
 * @property {() => boolean|Promise<boolean>} [estApplicable]
 * @property {(signal: AbortSignal) => Promise<ResultatDiagnostic>} executer
 */
```

### 2.2 Registre

```js
// registre.js
const tests = new Map();

export function enregistrer(test) {
  if (tests.has(test.id)) throw new Error(`Test déjà enregistré : ${test.id}`);
  tests.set(test.id, { timeoutMs: 10000, severite: 'majeur', ...test });
}

export function lister({ categorie } = {}) {
  const tous = [...tests.values()];
  return categorie ? tous.filter(t => t.categorie === categorie) : tous;
}
```

### 2.3 Moteur d'exécution

Responsabilités : exécution **séquentielle** (pour ne pas fausser les mesures de latence),
application du délai maximal par test, capture des exceptions, émission d'événements de
progression, et **annulation** par `AbortController`.

```js
// moteur.js
export async function executerTous(listeTests, { surProgression, signal }) {
  const resultats = [];
  for (const [index, test] of listeTests.entries()) {
    if (signal?.aborted) break;
    surProgression?.({ index, total: listeTests.length, test, phase: 'debut' });

    const debut = performance.now();
    let resultat;
    try {
      if (test.estApplicable && !(await test.estApplicable())) {
        resultat = { statut: 'nonApplicable', message: 'Non applicable sur ce poste.' };
      } else {
        resultat = await avecDelaiMax(test.executer(signal), test.timeoutMs);
      }
    } catch (erreur) {
      resultat = {
        statut: 'echec',
        message: erreur.name === 'TimeoutError'
          ? `Aucune réponse au bout de ${test.timeoutMs / 1000} s.`
          : erreur.message,
      };
    }
    resultat.dureeMs = Math.round(performance.now() - debut);
    resultats.push({ test, resultat });
    surProgression?.({ index, total: listeTests.length, test, phase: 'fin', resultat });
  }
  return resultats;
}
```

**Un test qui lève une exception ne doit jamais interrompre la campagne** — c'est la règle
qui différencie un outil de diagnostic exploitable d'un gadget qui s'arrête au premier
problème, c'est-à-dire précisément quand on en a besoin.

## 3. Catalogue des tests proposés

### A. Connectivité et back-end

| id | Libellé | Vérifie | Sévérité |
|---|---|---|---|
| `api.sante` | Disponibilité du serveur | Réponse de `GET /api/diagnostic/sante` et son délai | bloquant |
| `api.latence` | Temps de réponse réseau | Moyenne et écart sur 5 appels `POST /api/diagnostic/echo` | mineur |
| `api.versions` | Cohérence des versions | Version front == version back attendue | majeur |
| `api.baseDonnees` | Accès à la base de données | Requête témoin côté serveur + son temps | bloquant |
| `auth.jeton` | Validité de l'authentification | Jeton présent, non expiré, temps restant | bloquant |
| `auth.renouvellement` | Renouvellement du jeton | Le rafraîchissement aboutit | majeur |
| `reseau.horloge` | Synchronisation de l'horloge | **Écart entre l'heure du poste et l'heure serveur** | majeur |

> `reseau.horloge` mérite une mention : une dérive d'horloge de quelques minutes provoque des
> expirations de session inexpliquées et des erreurs de signature. C'est un incident fréquent,
> coûteux à diagnostiquer, et trivial à détecter — le serveur renvoie son heure, on compare.

### B. Environnement du poste

| id | Libellé | Vérifie |
|---|---|---|
| `nav.identite` | Navigateur et version | Nom, version, moteur ; avertit si version non supportée |
| `nav.fonctionnalites` | Fonctionnalités requises | `fetch`, `WebSocket`, `IndexedDB`, presse-papiers, `Intl` |
| `nav.stockage` | Stockage local | Écriture/lecture témoin ; quota utilisé et disponible |
| `nav.cookies` | Cookies | Activés, et cookies tiers si nécessaires |
| `nav.affichage` | Affichage | Résolution, zoom, ratio de pixels, mode sombre |
| `nav.locale` | Locale et fuseau | Locale détectée, fuseau horaire, décalage UTC |
| `nav.impression` | Support de l'impression | Présence de l'API, prise en compte de `@page` |

### C. Fonctionnel applicatif

| id | Libellé | Vérifie |
|---|---|---|
| `fonc.impression` | Chaîne d'impression | Génération d'un document témoin avec le profil courant |
| `fonc.televersement` | Envoi de fichier | Aller-retour d'un fichier de quelques kilo-octets |
| `fonc.telechargement` | Réception de fichier | Récupération d'un fichier témoin |
| `fonc.export` | Exports | Génération d'un CSV et d'un XLSX témoins |
| `fonc.courriel` | Envoi de courriel | Message de test adressé à l'utilisateur lui-même |
| `fonc.droits` | Droits et permissions | Rôles portés, écrans accessibles |
| `fonc.referentiels` | Intégrité des référentiels | Paramétrages obligatoires non renseignés |
| `fonc.tempsReel` | Canal temps réel | Ouverture WebSocket et aller-retour (si applicable) |

### D. Performance et santé

| id | Libellé | Vérifie |
|---|---|---|
| `perf.chargement` | Temps de chargement | `PerformanceNavigationTiming` de la session |
| `perf.memoire` | Mémoire JavaScript | `performance.memory` quand disponible (Chromium) |
| `perf.erreurs` | Erreurs de la session | Nombre d'erreurs JS collectées depuis l'ouverture |
| `perf.cache` | Cache applicatif | Version en cache vs version servie, état du *service worker* |

**Total : 26 tests.** Un lot initial de 8 à 10 (les `bloquant` et `majeur` de A et B) couvre
déjà l'essentiel des incidents courants.

## 4. Interface

```
┌─ Diagnostic ───────────────────────────────────────────────┐
│  Santé globale :  ●●●●●●●●○○  8/10        [Tout relancer]   │
│  Dernier passage : 29/08/2026 14:32 — Réf. DIAG-7F3A-2610   │
├────────────────────────────────────────────────────────────┤
│  ▾ Connectivité                                     6/7    │
│    ✅ Disponibilité du serveur          142 ms       [▸]   │
│    ✅ Validité de l'authentification    exp. 42 min  [▸]   │
│    ⚠️ Synchronisation de l'horloge      +3 min 12 s  [▾]   │
│        L'horloge de ce poste avance de 3 min sur le         │
│        serveur. Cela peut provoquer des déconnexions.       │
│        → Activez la synchronisation automatique de l'heure  │
│          dans les paramètres de Windows.        [Relancer]  │
│  ▸ Environnement du poste                           7/7    │
│  ▸ Fonctionnel                          ⏳ en cours 3/8    │
│  ▸ Performance                          ⏸ en attente       │
├────────────────────────────────────────────────────────────┤
│  ███████████████░░░░░░░  16/26          [Interrompre]      │
├────────────────────────────────────────────────────────────┤
│  [Copier le rapport]  [Exporter en JSON]  [Historique]     │
└────────────────────────────────────────────────────────────┘
```

**Règles d'affichage :**

- Statuts : ✅ `ok` · ⚠️ `avertissement` · ❌ `echec` · ⏭️ `nonApplicable` · ⏳ en cours.
- Chaque ligne porte sa **durée** et un bouton **« Relancer »** individuel.
- Le détail est replié par défaut ; il s'ouvre automatiquement pour les échecs.
- Le champ `remediation` est mis en avant : *ce que l'utilisateur peut faire lui-même* est
  plus utile que le message d'erreur technique.
- Le **score de santé** pondère par sévérité : un `bloquant` en échec plafonne le score.
- Zone `aria-live="polite"` annonçant l'avancement pour les lecteurs d'écran.
- Les tests écrivant des données (`fonc.courriel`, `fonc.televersement`) sont **décochés par
  défaut** et signalés comme tels : on n'envoie pas un courriel à l'insu de l'utilisateur.

## 5. Rapport et historique

**Référence de corrélation** — chaque campagne reçoit un identifiant court
(`DIAG-7F3A-2610`), affiché à l'écran et présent dans le rapport. L'utilisateur le dicte au
support, qui retrouve immédiatement le contexte.

**Contenu du rapport** : référence, horodatage, identité de l'utilisateur, version de
l'application, version du back-end, environnement, navigateur, système, puis chaque test avec
son statut, sa durée, son message et ses détails.

**Trois sorties** :
- `Copier le rapport` — texte formaté, prêt à coller dans un courriel ou un ticket ;
- `Exporter en JSON` — exploitable par un outil ;
- envoi facultatif à `POST /api/diagnostic/rapport` pour archivage côté serveur.

**Historique** — les 10 dernières campagnes conservées localement, ce qui permet de répondre
à « depuis quand ? » et de distinguer une panne d'une lenteur installée.

## 6. Confidentialité

Le rapport est destiné à être transmis à des tiers. Règles impératives :

1. **Aucun secret dans le rapport.** Jetons, mots de passe, chaînes de connexion, clés d'API,
   en-têtes `Authorization` : jamais. Un test qui manipule un jeton n'en rapporte que les
   métadonnées (validité, échéance), jamais la valeur.
2. Un test marqué `confidentiel: true` est exécuté et affiché à l'écran mais **exclu de
   l'export**.
3. Les messages d'erreur serveur sont **filtrés** avant affichage : pas de trace d'exécution
   ni de chemin de fichier serveur, qui renseigneraient un attaquant sur l'infrastructure.
4. Les points d'entrée `/api/diagnostic/*` sont **authentifiés** et ceux qui exposent des
   informations d'infrastructure (`baseDonnees`, `referentiels`) **réservés aux
   administrateurs**.
5. Une **limitation de débit** protège `fonc.courriel` d'un usage détourné en outil d'envoi.
