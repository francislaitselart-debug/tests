# Barre de filtres à état — Panorama E2 (vue + images)

> **Option retenue : B — images pré-générées.** Voir [option-b/README.md](option-b/README.md)
> (architecture, modèle d'état à N états, grammaire XML/XSD de configuration).
>
> La suite de ce document décrit l'**option D** (fond en 3 images et largeur de texte
> calculée), conservée comme alternative. Sa version C#, avec mesure des libellés en
> exécution, est dans [panorama/BarreFiltres.cs](panorama/BarreFiltres.cs).

Remplacement du composant d'IHM web par une **vue Panorama** dont les boutons sont
faits d'**objets image**, avec gestion du **multilingue** et des **libellés de
longueur variable**.

## Contraintes Panorama confirmées

| Question | Réponse | Conséquence |
|---|---|---|
| Un objet image peut-il être étiré horizontalement ? | Oui | Le fond du bouton tient en 3 images, dont le milieu est étiré |
| Un script peut-il déplacer et redimensionner des objets en exécution ? | Oui | La mise en page de la barre est recalculée par script |
| Un script peut-il mesurer la largeur d'un texte affiché ? | **Non** | Les largeurs sont **calculées à l'avance** par un outil C# |

## Principe

```
┌──╮───────────────────────────╭──┐
│G │ [ico]  Libellé traduit    M │ D│     G, D : coins, largeur fixe
└──╯───────────────────────────╰──┘     M    : milieu, étiré
 ←MARGE_G→←ICONE→←ESPACE→←wTxt→←MARGE_D→
```

`largeur bouton = MARGE_G + ICONE + ESPACE + wTxt + MARGE_D`

`wTxt` est lu dans une table générée (clé × langue → pixels). Si la langue n'y
figure pas, on prend la plus grande largeur toutes langues confondues, puis
une valeur par défaut. La largeur est plafonnée à `L_TXT_MAX`.

## Contenu

| Fichier | Rôle |
|---|---|
| [outil-largeurs/LargeursLibelles.cs](outil-largeurs/LargeursLibelles.cs) | Outil de mesure des libellés (GDI, `TextRenderer`) |
| [outil-largeurs/build.cmd](outil-largeurs/build.cmd) | Compilation avec le `csc.exe` du .NET Framework 4.x, sans SDK |
| [outil-largeurs/exemple-libelles.csv](outil-largeurs/exemple-libelles.csv) | Exemple d'entrée `Cle;Langue;Libelle` |
| [panorama/BarreFiltres.vbs](panorama/BarreFiltres.vbs) | Mise en page, bascule, lecture et écriture de l'état, masque global |

## Mise en œuvre

1. Exporter les traductions des libellés au format `Cle;Langue;Libelle` (UTF-8).
2. Sur un poste Windows :
   ```bat
   build.cmd
   LargeursLibelles.exe libelles.csv /police:"Segoe UI" /taille:9 /unite:pt /vbs:Largeurs.vbs /csv:largeurs.csv
   ```
   Indiquer **la police, la taille et le style exacts** du texte dans Panorama.
   L'outil signale une police absente du poste, car Windows la remplacerait
   sans prévenir et les mesures seraient fausses.
3. Intégrer `Largeurs.vbs` (généré) et `BarreFiltres.vbs` aux scripts de la vue.
4. Compléter la section **ADAPTATEURS PANORAMA** de `BarreFiltres.vbs`. Ce sont
   les seules fonctions qui dépendent de l'API Panorama E2.
5. Appeler `MiseEnPageBarre` à l'ouverture de la vue **et** sur le changement de langue.
6. Sur le clic de chaque instance, appeler `BasculerFiltre <index>`.

Il faut relancer l'étape 2 à chaque modification d'un libellé ou ajout d'une langue.

## État des filtres

- Une **variable par filtre** : c'est la source de vérité, lisible et modifiable par
  n'importe quel autre traitement.
- `MasqueFiltres()` et `AppliquerMasque(m)` lisent ou positionnent tous les filtres en
  une fois : bit *i* = filtre *i* de `ListeFiltres()`.
- Le visuel off/on peut passer par `AfficherEtat` (script) ou par une animation
  Panorama liée directement à la variable. Avec l'animation, l'affichage reste juste
  même quand un autre traitement modifie la variable.

## Points ouverts

- Police, taille et style exacts des libellés dans Panorama.
- Événement ou variable qui signale le changement de langue.
- Source des traductions (table Panorama, fichier, base) pour automatiser l'export CSV.
- Comportement de l'objet texte quand le libellé dépasse `L_TXT_MAX` (coupé ou « … »).
- Validation des mesures : comparer à l'écran un libellé long (allemand) avec la largeur calculée.
