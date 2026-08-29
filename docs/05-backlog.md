# 05 — Backlog, charges et ordonnancement

## 1. Base du chiffrage

- Unité : **jour-homme (j·h)** de développement effectif, un développeur.
- Inclus : conception détaillée, code, tests, corrections de revue.
- Exclus : recette métier, formation, déploiement, réunions.
- Pile retenue : **JavaScript sans framework + CSS, back-end DLL C#**.

> **Révision par rapport à la première estimation.** L'ordre de grandeur annoncé
> initialement (10 à 12 j·h) supposait un framework de composants et des points d'entrée de
> persistance déjà en place. Avec du JavaScript sans framework — chaque formulaire est
> construit et lié à la main — et un back-end à étendre, le chiffrage réaliste est
> **sensiblement supérieur**. La différence tient au coût de la couche vue et à la création
> des points d'entrée C#, non à un élargissement du périmètre.

## 2. Lot 0 — Socle

| # | Tâche | Charge |
|---|---|---|
| 0.1 | Lecture du code de `MyPnParametrages`, conventions, points d'insertion | 0,5 |
| 0.2 | Coquille : navigation par sections, magasin d'état, couche transport HTTP, gestion de l'état « modifié » et de l'abandon | 1,0 |
| | **Sous-total** | **1,5** |

Le lot 0 conditionne tous les autres : les trois sections réutilisent la même coquille.

## 3. Lot 1 — Impressions

| # | Tâche | Charge |
|---|---|---|
| 1.1 | Modèle, valeurs par défaut, normalisation, règles de validation | 0,5 |
| 1.2 | Formulaire mise en page + rendu + tableaux | 0,75 |
| 1.3 | Logo : sélection, glisser-déposer, contrôle de type réel, assainissement SVG, aperçu | 0,5 |
| 1.4 | Titre, en-tête, pied de page + menu d'insertion de variables | 0,75 |
| 1.5 | **Aperçu miniature en direct** | 0,75 |
| 1.6 | Génération de la feuille `@page` + page de test avec règle graduée | 0,5 |
| 1.7 | Profils : création, duplication, suppression, défaut, import/export JSON | 0,75 |
| 1.8 | C# — points d'entrée profils, téléversement de logo, stockage, validation serveur | 1,5 |
| | **Sous-total** | **6,0** |

## 4. Lot 2 — Diagnostic

| # | Tâche | Charge |
|---|---|---|
| 2.1 | Registre déclaratif + moteur (séquencement, délai maximal, annulation, progression) | 0,75 |
| 2.2 | Vue générique : catégories, statuts, détails repliables, score de santé, `aria-live` | 1,0 |
| 2.3 | Tests **A — Connectivité** (7) et **B — Environnement** (7) | 1,0 |
| 2.4 | Tests **C — Fonctionnel** (8) et **D — Performance** (4) | 1,0 |
| 2.5 | Rapport : référence de corrélation, copie, export JSON, historique local | 0,75 |
| 2.6 | C# — points d'entrée `/api/diagnostic/*`, limitation de débit, filtrage des messages | 1,5 |
| | **Sous-total** | **6,0** |

## 5. Lot 3 — Ma session

| # | Tâche | Charge |
|---|---|---|
| 3.1 | Bloc connexion : identité, rôles, compte à rebours, prolongation, appareils connectés | 1,0 |
| 3.2 | Langue et formats régionaux (**hors création du mécanisme d'i18n** — voir D3) | 0,75 |
| 3.3 | Bloc version et environnement, avec bandeau coloré et alerte de désynchronisation | 0,5 |
| 3.4 | Bloc licence et modules, avec alertes d'échéance à 60/30/7 j | 0,75 |
| 3.5 | Bouton « Copier les informations système » | 0,25 |
| 3.6 | C# — `/api/session/*`, `/api/version`, `/api/licence` | 1,5 |
| | **Sous-total** | **4,75** |

## 6. Lot 4 — Transverse

| # | Tâche | Charge |
|---|---|---|
| 4.1 | Tests unitaires : validation d'impression, moteur de diagnostic, formatage régional | 1,0 |
| 4.2 | Tests de bout en bout sur les trois parcours principaux | 0,5 |
| 4.3 | Accessibilité : navigation clavier, rôles ARIA, contrastes | 0,5 |
| 4.4 | Externalisation des libellés des trois sections | 0,5 |
| | **Sous-total** | **2,5** |

## 7. Lot 5 — Finalisation

| # | Tâche | Charge |
|---|---|---|
| 5.1 | Documentation utilisateur et administrateur | 0,5 |
| 5.2 | Revue de code, reprises | 0,5 |
| | **Sous-total** | **1,0** |

## 8. Synthèse

| Lot | Charge |
|---|---|
| 0 — Socle | 1,5 |
| 1 — Impressions | 6,0 |
| 2 — Diagnostic | 6,0 |
| 3 — Ma session | 4,75 |
| 4 — Transverse | 2,5 |
| 5 — Finalisation | 1,0 |
| **Total** | **21,75 j·h**, soit **≈ 4,5 semaines** pour un développeur |

### Options chiffrées séparément

| Option | Charge | Commentaire |
|---|---|---|
| **D1-B — Génération PDF côté C#** | **+4,0** | Bibliothèque, gabarits, en-tête/pied/numérotation, aperçu. **Seule voie honorant l'ensemble du besoin d'impression.** |
| Création d'un mécanisme d'i18n | non chiffré | Chantier distinct, hors périmètre (voir D3) |
| Archivage serveur des rapports de diagnostic | +0,75 | Table, purge, écran de consultation |
| Impression serveur pilotant l'imprimante (CUPS/IPP) | +2,5 | Rend le choix d'imprimante réellement effectif |

## 9. Version minimale viable

Si le besoin est de livrer vite pour valider l'usage, ce sous-ensemble couvre l'essentiel :

| Lot | Contenu retenu | Charge |
|---|---|---|
| 0 | Socle complet | 1,5 |
| 1 | Mise en page, logo, en-tête/pied — **sans** profils, filigrane, aperçu, import/export | 2,75 |
| 2 | Moteur + vue + **10 tests** (A et B) + copie du rapport | 2,75 |
| 3 | Connexion, langue, version, licence — **sans** appareils connectés ni formats régionaux | 2,25 |
| 4 | Tests unitaires du moteur et de la validation | 0,75 |
| | **Total version minimale** | **10,0 j·h** |

Le reste (profils, aperçu en direct, 16 tests supplémentaires, formats régionaux, appareils
connectés, filigrane) constitue un second incrément de **≈ 11,75 j·h**.

## 10. Ordonnancement proposé

```
Semaine 1   ██ Lot 0            Socle
            ████████ Lot 1      Impressions (front)
Semaine 2   ██ Lot 1            Impressions (C#)
            ██████████ Lot 2    Diagnostic (moteur + vue + tests A/B)
Semaine 3   ████ Lot 2          Diagnostic (tests C/D + rapport + C#)
            ██████ Lot 3        Ma session (front)
Semaine 4   ███ Lot 3           Ma session (C#)
            █████ Lot 4         Transverse
Semaine 5   ██ Lot 5            Finalisation
```

**Parallélisation** — les parties C# (1.8, 2.6, 3.6 : 4,5 j·h au total) sont indépendantes du
front une fois le contrat d'API figé. À deux développeurs, le chemin critique tombe à
**≈ 3 semaines**.

**Ordre recommandé des sections** : Diagnostic **avant** Impressions si l'objectif prioritaire
est de réduire la charge du support — c'est la section au meilleur rapport valeur/charge, et
elle est utile dès le premier incident. Impressions d'abord si la demande vient des
utilisateurs finaux.

## 11. Risques

| # | Risque | Probabilité | Impact | Parade |
|---|---|---|---|---|
| R1 | **D1 non tranchée** : la section Impressions promet un rendu que l'impression navigateur ne sait pas produire (numéros de page, en-tête maîtrisé) | Élevée | Élevé | Trancher D1 **avant le lot 1**. Si D1-A, retirer `{{page}}`/`{{pages}}` du menu de variables. |
| R2 | Aucun mécanisme d'i18n : le sélecteur de langue ne peut pas fonctionner | Moyenne | Moyen | Vérifier dès le lot 0. Repli : affichage seul, modification signalée « à venir ». |
| R3 | Charge back-end sous-estimée si la DLL n'expose pas déjà de couche HTTP JSON | Moyenne | Élevé | Vérifier au lot 0. Peut ajouter 2 à 3 j·h. |
| R4 | Fuite d'informations sensibles par le rapport de diagnostic | Faible | **Élevé** | Liste blanche des champs exportés, revue de sécurité dédiée avant livraison du lot 2. |
| R5 | Rendu d'impression divergent entre navigateurs | Élevée en D1-A | Moyen | Recette sur les navigateurs cibles ; ou basculer en D1-B. |
| R6 | Les 26 tests de diagnostic supposent des points d'entrée serveur qui n'existent pas | Moyenne | Moyen | Chaque test dégrade proprement en `nonApplicable`. |
| R7 | Le composant existant n'est pas structuré pour accueillir des sections | Moyenne | Moyen | Arbitré au lot 0 ; peut transformer 0.2 en petite refonte (+1 j·h). |

## 12. Prochaines étapes

1. Trancher **D1** (mode de génération des documents) — bloquant pour le lot 1.
2. Trancher **D2** (portée des paramètres) — impacte 1.1 et 1.8.
3. Vérifier **D3** (mécanisme d'i18n) — impacte 3.2.
4. Donner accès au code de `MyPnParametrages` pour consolider le chiffrage du lot 0.
5. Arbitrer entre **version minimale (10 j·h)** et **périmètre complet (21,75 j·h)**.
