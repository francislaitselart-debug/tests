# 01 — Section « Impressions »

## 1. Objectif

Centraliser les préférences d'impression de l'application, aujourd'hui subies (réglages du
navigateur, remis à zéro à chaque poste), pour les rendre explicites, persistantes et
partageables.

## 2. Organisation de l'écran

Deux colonnes : le formulaire à gauche, **un aperçu miniature en direct à droite**.
L'aperçu est le principal apport ergonomique de la section : il rend immédiatement lisible
l'effet de marges et d'un pied de page, réglages que personne ne sait se représenter dans
l'abstrait.

```
┌─ Impressions ──────────────────────────────────────────────┐
│ Profil : [ Facture ▾ ] [+ Nouveau] [Dupliquer] [Supprimer] │
├──────────────────────────────┬─────────────────────────────┤
│ ▸ Mise en page               │        ┌───────────┐        │
│ ▸ Logo                       │        │ ▤ logo    │        │
│ ▸ Titre, en-tête, pied       │        │           │        │
│ ▸ Rendu                      │        │ ─── ───   │        │
│ ▸ Filigrane                  │        │ ─────────  │       │
│ ▸ Tableaux                   │        │ ─── ──     │       │
│ ▸ Imprimante                 │        │  page 1/3  │       │
│                              │        └───────────┘        │
│                              │  [Imprimer une page de test]│
├──────────────────────────────┴─────────────────────────────┤
│         [Réinitialiser]      [Annuler]     [Enregistrer]   │
└────────────────────────────────────────────────────────────┘
```

## 3. Paramètres

### 3.1 Mise en page

| Champ | Type | Valeurs | Défaut |
|---|---|---|---|
| `format` | liste | A4, A3, A5, Letter, Legal, Personnalisé | `A4` |
| `largeurMm`, `hauteurMm` | nombre | 50–1200 | — (si `Personnalisé`) |
| `orientation` | liste | `portrait`, `paysage` | `portrait` |
| `margeHautMm` … `margeGaucheMm` | nombre | 0–100, pas de 1 | `10` |
| `margesLiees` | booléen | — | `true` |
| `rectoVerso` | liste | `simple`, `rectoVersoLongBord`, `rectoVersoCourtBord` | `simple` |
| `echelleMode` | liste | `pourcentage`, `ajusterPage`, `ajusterLargeur` | `pourcentage` |
| `echellePourcent` | nombre | 25–200 | `100` |

**Marges liées** : une case à cocher qui propage la valeur saisie aux quatre marges — évite
quatre saisies dans le cas courant.

**Garde-fou** : avertissement non bloquant si une marge descend sous 5 mm (zone non
imprimable de la plupart des imprimantes à jet d'encre).

### 3.2 Logo

| Champ | Type | Contrainte |
|---|---|---|
| `logoDonnees` | chaîne | Data URI base64, ou identifiant de fichier serveur |
| `logoNomFichier` | chaîne | informatif |
| `logoPosition` | liste | `gauche`, `centre`, `droite` |
| `logoHauteurMm` | nombre | 5–40, défaut `15` |
| `logoAfficheSurToutesPages` | booléen | défaut `false` (première page seulement) |

- Formats acceptés : **PNG, JPEG, SVG**. Poids maximal : **512 Ko**.
- Dépôt par sélection de fichier **et** par glisser-déposer.
- Validation côté client (type MIME réel lu sur les premiers octets, pas seulement
  l'extension) **et** côté serveur.
- Le SVG doit être **assaini avant stockage** (suppression des `<script>`, `<foreignObject>`
  et des attributs `on*`) : un SVG est un document exécutable.
- Aperçu immédiat, avec bouton de suppression.

### 3.3 Titre, en-tête et pied de page

Trois zones (gauche / centre / droite) pour l'en-tête, trois pour le pied, plus un champ
`titreDocument`. Chaque zone est un champ texte acceptant des **variables**, insérables via
un menu « Insérer une variable » plutôt que saisies de mémoire :

| Variable | Contenu |
|---|---|
| `{{page}}` | Numéro de la page courante |
| `{{pages}}` | Nombre total de pages |
| `{{date}}` | Date d'impression, formatée selon la locale |
| `{{heure}}` | Heure d'impression |
| `{{utilisateur}}` | Nom de l'utilisateur connecté |
| `{{societe}}` | Raison sociale |
| `{{etablissement}}` | Établissement de rattachement |
| `{{titreDocument}}` | Titre du document |
| `{{version}}` | Version de l'application |

Champs complémentaires : `hauteurEnteteMm`, `hauteurPiedMm`, `traitSousEntete` (booléen),
`traitSurPied` (booléen), `policeEntetePied`, `taillePoliceEntetePt`.

> ⚠️ **`{{page}}` et `{{pages}}` ne sont pas calculables en impression navigateur** — voir
> décision **D1** dans [00-cadrage.md](00-cadrage.md). Si l'option D1-A est retenue, ces deux
> variables doivent être retirées du menu d'insertion, sous peine de promettre à
> l'utilisateur un résultat que le produit ne sait pas rendre.

### 3.4 Rendu

| Champ | Valeurs | Défaut |
|---|---|---|
| `couleur` | `couleur`, `noirEtBlanc` | `couleur` |
| `qualite` | `brouillon`, `normale`, `haute` | `normale` |
| `imprimerImagesFond` | booléen | `false` |
| `policeDefaut` | liste | héritée du thème |
| `taillePoliceDefautPt` | 6–18 | `10` |

### 3.5 Filigrane

`filigraneActif` (booléen), `filigraneTexte` (ex. « BROUILLON », « COPIE », « CONFIDENTIEL »),
`filigraneOpacite` (5–50 %), `filigraneRotationDeg` (−90 à 90), `filigraneCouleur`.

### 3.6 Tableaux

- `repeterEntetesColonnes` (booléen, défaut `true`) — `<thead>` répété sur chaque page.
- `eviterCoupureLignes` (booléen, défaut `true`) — `break-inside: avoid` sur les `<tr>`.
- `quadrillage` : `complet`, `horizontalSeul`, `aucun`.

### 3.7 Imprimante

| Champ | Type |
|---|---|
| `imprimanteSuggeree` | chaîne libre |
| `bacPapier` | chaîne libre |
| `nombreCopies` | 1–99, défaut `1` |
| `assembler` | booléen |

> ⚠️ En impression navigateur, ces champs sont **purement informatifs** : aucune API web ne
> permet de présélectionner une imprimante. L'interface doit le dire explicitement — un
> encart « Ces réglages sont indicatifs ; la boîte de dialogue d'impression de votre
> navigateur reste maîtresse du choix final » — plutôt que de laisser croire à un pilotage
> effectif. Avec l'option D1-B et une impression serveur, ils redeviennent pilotables.

### 3.8 Profils

Un profil est un jeu complet de paramètres, nommé. Cas d'usage : « Facture » en portrait avec
logo et mentions légales, « Liste interne » en paysage sans logo et marges réduites.

- Opérations : créer, dupliquer, renommer, supprimer, définir comme défaut.
- Association facultative d'un profil à un **type de document** (`associationsTypesDocument`),
  pour que l'application sélectionne automatiquement le bon profil.
- Un profil `Par défaut` non supprimable est toujours présent.

## 4. Application des paramètres à l'impression (option D1-A)

Génération dynamique d'une feuille de style dédiée, régénérée à chaque changement de profil :

```js
// styles-impression.js
const ID_STYLE = 'mypn-styles-impression';

export function appliquerStylesImpression(p) {
  let style = document.getElementById(ID_STYLE);
  if (!style) {
    style = document.createElement('style');
    style.id = ID_STYLE;
    document.head.appendChild(style);
  }
  const taille = p.format === 'Personnalise'
    ? `${p.largeurMm}mm ${p.hauteurMm}mm`
    : `${p.format} ${p.orientation === 'paysage' ? 'landscape' : 'portrait'}`;

  style.textContent = `
    @page {
      size: ${taille};
      margin: ${p.margeHautMm}mm ${p.margeDroiteMm}mm ${p.margeBasMm}mm ${p.margeGaucheMm}mm;
    }
    @media print {
      body { font-size: ${p.taillePoliceDefautPt}pt; }
      ${p.couleur === 'noirEtBlanc' ? 'body { filter: grayscale(1); }' : ''}
      ${p.imprimerImagesFond ? 'body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }' : ''}
      ${p.repeterEntetesColonnes ? 'thead { display: table-header-group; }' : ''}
      ${p.eviterCoupureLignes ? 'tr, li { break-inside: avoid; }' : ''}
    }`;
}
```

**Points d'attention :**

- `@page { size }` n'est honoré que si la feuille est présente **avant** l'appel à `print()`.
- L'échelle (`echellePourcent`) n'est pas pilotable en CSS d'impression ; elle ne peut être
  appliquée qu'à un conteneur via `transform: scale()`, avec des effets de bord sur la
  pagination. À réserver à l'option D1-B.
- Le filigrane s'obtient par un pseudo-élément fixe en `@media print`, mais il ne se répète
  pas de façon fiable sur toutes les pages selon les moteurs.

## 5. Règles de validation

| Règle | Message |
|---|---|
| Somme des marges horizontales < largeur du format | « Les marges gauche et droite dépassent la largeur de la page. » |
| Somme des marges verticales + en-tête + pied < hauteur | « Il ne reste pas de place pour le contenu. » |
| Marge < 5 mm | Avertissement : « Certaines imprimantes ne savent pas imprimer sous 5 mm du bord. » |
| Logo > 512 Ko | « Le logo dépasse 512 Ko. » |
| Logo de type non autorisé | « Formats acceptés : PNG, JPEG, SVG. » |
| Nom de profil vide ou déjà utilisé | « Ce nom de profil est déjà utilisé. » |
| Variable inconnue dans une zone | Avertissement : « La variable {{xxx}} n'est pas reconnue. » |

## 6. Confort d'usage

- **Aperçu en direct** — redessiné à chaque modification (avec anti-rebond de 150 ms).
- **Page de test** — imprime un document témoin contenant une règle graduée en millimètres,
  l'en-tête, le pied de page et le logo, afin de vérifier le rendu réel sur l'imprimante.
- **Import / export d'un profil** en JSON — permet à un support de transmettre une
  configuration prête à l'emploi.
- **Réinitialisation** aux valeurs d'usine ou aux valeurs de la société (selon D2).
