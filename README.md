# MyPnParametrages — Évolution

Spécification fonctionnelle et technique de l'ajout de trois sections au composant
de paramétrage `MyPnParametrages` :

1. **Impressions** — préférences d'impression (mise en page, logo, en-tête/pied de page)
2. **Diagnostic** — auto-tests de bon fonctionnement de l'application
3. **Ma session** — état de connexion, langue, version, licence et modules

> **Statut : cadrage.** Ce dépôt ne contient volontairement **aucune implémentation**.
> Il porte la spécification, le modèle de données, le contrat d'API et le backlog chiffré,
> destinés à être appliqués sur le code réel de `MyPnParametrages`.

## Contexte technique retenu

| Élément | Valeur |
|---|---|
| Front-end | JavaScript (sans framework) + CSS |
| Back-end | DLL C# exposant des points d'entrée HTTP |
| Impression | Navigateur (`window.print()`) |
| Langue de l'interface | Français (multilingue prévu) |

## Sommaire

| Document | Objet |
|---|---|
| [docs/00-cadrage.md](docs/00-cadrage.md) | Périmètre, hypothèses, principes d'architecture, risques |
| [docs/01-impressions.md](docs/01-impressions.md) | Section Impressions — spécification détaillée |
| [docs/02-diagnostic.md](docs/02-diagnostic.md) | Section Diagnostic — moteur de tests et catalogue |
| [docs/03-session.md](docs/03-session.md) | Section Ma session — connexion, langue, version, licence |
| [docs/04-modele-donnees-et-api.md](docs/04-modele-donnees-et-api.md) | Modèle de données JSON et contrat d'API C# |
| [docs/05-backlog.md](docs/05-backlog.md) | Lots, tâches, charges et ordonnancement |
| [docs/prompt-vscode.md](docs/prompt-vscode.md) | Prompt autonome à coller dans une session Claude Code locale |
| [docs/sg/SG-01-parametrage-profils.md](docs/sg/SG-01-parametrage-profils.md) | SG de l'affichage Paramétrage des profils (code livré v1.3 + 2026-10-08) |
| [docs/sg/SG-02-parametrage-utilisateurs.md](docs/sg/SG-02-parametrage-utilisateurs.md) | SG de l'affichage Paramétrage des utilisateurs (code livré v1.3 + 2026-10-08) |

## Décisions en attente

Trois points bloquent le chiffrage définitif et sont détaillés dans
[docs/00-cadrage.md](docs/00-cadrage.md) :

- **D1** — Mode de génération des documents imprimés (navigateur seul vs PDF produit par le back-end C#)
- **D2** — Portée des paramètres (utilisateur, société, ou les deux avec héritage)
- **D3** — Existence et forme de l'infrastructure d'internationalisation actuelle
