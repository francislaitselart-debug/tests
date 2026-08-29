# 03 — Section « Ma session »

## 1. Objectif

Répondre sans quitter l'application aux questions que l'utilisateur et le support se posent
en permanence : *qui suis-je connecté en tant que ? sur quel environnement ? quelle version ?
mon contrat court-il encore ? quels modules ai-je ?*

C'est aussi l'écran qui porte le bouton **« Copier les informations système »** : une seule
action produit le bloc à coller dans un ticket.

## 2. Organisation

Quatre blocs sur un même écran, du plus immédiat au plus administratif :

```
┌─ Ma session ───────────────────────────────────────────────┐
│ ① Connexion            ② Langue et formats                 │
│ ③ Version et environnement                                 │
│ ④ Licence et modules                                       │
│                          [Copier les informations système] │
└────────────────────────────────────────────────────────────┘
```

## 3. Bloc ① — État de connexion

| Information | Détail |
|---|---|
| État | **Connecté** / Déconnecté, et **en ligne / hors ligne** (`navigator.onLine`) |
| Identité | Nom, prénom, identifiant de connexion, adresse électronique |
| Photo / initiales | Si disponible |
| Rôles et profil | Liste des rôles portés |
| Société / établissement | Rattachement courant, et bouton d'exécution si multi-établissement |
| Début de session | Date et heure de connexion |
| Durée de session | Compteur vivant (`il y a 2 h 14 min`) |
| **Expiration** | **Compte à rebours avant expiration du jeton** + bouton « Prolonger » |
| Dernière connexion réussie | Date, heure, adresse IP, poste |
| Dernier échec de connexion | Date, heure — révèle une tentative d'accès non autorisée |
| Appareils connectés | Liste des sessions actives + « Déconnecter cet appareil » |
| Adresse IP courante | Telle que vue par le serveur |

**Points d'attention :**

- Le **compte à rebours** est l'élément le plus utile du bloc : il transforme la déconnexion
  brutale en milieu de saisie — cause classique de perte de travail — en événement anticipé.
  Prévoir un avertissement discret à 5 minutes de l'échéance.
- « Prolonger la session » appelle le renouvellement du jeton et réinitialise le compteur.
- La déconnexion d'un autre appareil est une **action à confirmer** (fenêtre de confirmation).
- Ne jamais afficher la valeur du jeton, seulement ses métadonnées.

## 4. Bloc ② — Langue et formats

### 4.1 Sélecteur de langue

- Liste des langues disponibles, **chacune affichée dans sa propre langue**
  (`Français`, `English`, `Deutsch`, `Nederlands`) — un utilisateur perdu dans une langue
  qu'il ne lit pas doit pouvoir retrouver la sienne.
- **Application immédiate, sans rechargement de page.**
- **Double persistance** : envoi à `PUT /api/session/langue` (le profil suit l'utilisateur
  d'un poste à l'autre) *et* écriture dans `localStorage` (la langue est correcte dès le
  prochain affichage, avant même la réponse du serveur).
- Ordre de résolution au démarrage : profil serveur → `localStorage` → `navigator.language`
  → langue par défaut de l'application.

> Voir décision **D3** dans [00-cadrage.md](00-cadrage.md) : le coût de ce bloc dépend
> entièrement de l'existence d'un mécanisme d'internationalisation. Si les libellés sont
> aujourd'hui en dur dans le code, le sélecteur ne peut pas fonctionner et le chantier
> déborde du périmètre de ce composant.

### 4.2 Formats régionaux

| Paramètre | Exemple |
|---|---|
| Format de date | `31/12/2026`, `2026-12-31`, `12/31/2026` |
| Format d'heure | 24 h / 12 h |
| Séparateur décimal | `,` / `.` |
| Séparateur de milliers | espace fine / `.` / `,` |
| Devise et sa position | `1 234,56 €` / `€1,234.56` |
| Fuseau horaire | Détecté, modifiable |
| Premier jour de la semaine | Lundi / Dimanche |

Un **exemple vivant** sous chaque champ montre le rendu (`31/12/2026 — 1 234,56 €`) : plus
parlant qu'un nom de format. `Intl.DateTimeFormat` et `Intl.NumberFormat` suffisent, sans
bibliothèque additionnelle.

## 5. Bloc ③ — Version et environnement

| Information | Exemple |
|---|---|
| Version applicative | `4.7.2` |
| Numéro de build | `2026.08.24.1731` |
| Empreinte de commit | `a3f91c2` |
| Date de compilation | `24/08/2026 17:31` |
| Version du back-end | `4.7.2` — **signalée en rouge si différente du front** |
| Version des DLL principales | Nom + version de chaque assembly notable |
| Version du schéma de base | `147` |
| Version du moteur de base | `SQL Server 2022 (16.0.4165)` |
| **Environnement** | `PRODUCTION` / `RECETTE` / `DÉVELOPPEMENT` |
| Nom du serveur | `SRV-APP-02` |
| Notes de version | Lien vers le journal des modifications |

**L'environnement doit être un bandeau coloré, pas une ligne de tableau** : vert pour la
production, orange pour la recette, bleu pour le développement. La confusion entre recette et
production est une source classique d'incidents ; un indicateur discret ne suffit pas.

## 6. Bloc ④ — Licence et modules

### 6.1 Licence

| Information | Comportement |
|---|---|
| Numéro de licence | Affiché, copiable |
| Raison sociale du titulaire | — |
| Type de contrat | Perpétuelle, abonnement, évaluation |
| **Date de début de validité** | — |
| **Date de fin de validité** | **Avertissement orange à 60 j, rouge à 30 j, bandeau à 7 j** |
| **Date de fin de maintenance** | Même logique d'alerte |
| Postes / utilisateurs autorisés | `18 / 25 utilisés`, avec barre de progression |
| Sessions simultanées autorisées | `4 / 10` |
| Date de dernière vérification | Horodatage du dernier contrôle de licence |

Les alertes d'échéance sont **la raison d'être du bloc** : une licence qui expire un vendredi
soir sans que personne n'ait été prévenu bloque l'exploitation le lundi matin.

### 6.2 Modules et options activés

Tableau : **Module** · **État** · **Échéance propre** · **Description**

```
  Facturation            ✅ Activé        —              Émission et suivi des factures
  Gestion des stocks     ✅ Activé        31/12/2026     Inventaires et mouvements
  Comptabilité           ✅ Activé        —              Export FEC, lettrage
  Multi-établissements   ❌ Non souscrit  —              Gestion de plusieurs sites
  Portail client         ⚠️ Évaluation    15/09/2026     Accès externe pour vos clients
  Interface bancaire     ✅ Activé        30/06/2027     Import des relevés
```

Un module en évaluation ou proche de son échéance propre est mis en évidence. Un filtre
« Afficher seulement les modules activés » aide quand le catalogue est long.

## 7. Bouton « Copier les informations système »

Produit en une action un bloc texte prêt à coller :

```
── Informations système — MyPn ──────────────────────
Généré le          : 29/08/2026 14:37 (UTC+02:00)
Utilisateur        : Francis Laitselart (flaitselart)
Société            : ACME SARL — Établissement Paris
Rôles              : Administrateur, Comptable
─────────────────────────────────────────────────────
Application        : 4.7.2  (build 2026.08.24.1731, a3f91c2)
Back-end           : 4.7.2
Schéma de base     : 147  (SQL Server 2022 16.0.4165)
Environnement      : PRODUCTION  (SRV-APP-02)
─────────────────────────────────────────────────────
Licence            : LIC-2024-8891  —  Abonnement
Validité           : 01/01/2026 → 31/12/2026
Maintenance        : jusqu'au 31/12/2026
Utilisateurs       : 18 / 25
Modules activés    : Facturation, Stocks, Comptabilité,
                     Portail client (éval.), Interface bancaire
─────────────────────────────────────────────────────
Navigateur         : Chrome 141.0.0.0 (Blink) — Windows 11
Affichage          : 1920×1080 @ 125 %
Langue / fuseau    : fr-FR / Europe/Paris
Dernier diagnostic : DIAG-7F3A-2610 — 8/10
─────────────────────────────────────────────────────
```

Le contenu est soumis aux **mêmes règles de confidentialité** que le rapport de diagnostic
(voir [02-diagnostic.md](02-diagnostic.md) § 6) : aucun jeton, aucune chaîne de connexion.

## 8. Compléments utiles

- **Coordonnées du support** — téléphone, adresse électronique, horaires, lien de prise de
  main à distance ; l'utilisateur en panne n'a pas à les chercher ailleurs.
- **Mentions légales et RGPD** — éditeur, hébergeur, lien vers la politique de confidentialité,
  bouton d'export de ses propres données personnelles.
- **Conditions d'utilisation** — version acceptée et sa date.
- **Raccourcis clavier** — la liste, souvent introuvable, a sa place ici.
