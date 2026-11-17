# Option B — boutons en images pré-générées

Chaque bouton est **une seule image** (fond, icône et libellé traduit), générée
à l'avance pour chaque filtre, langue et état. Le problème des libellés de
longueur variable disparaît : chaque image a sa propre largeur, et la barre
aligne simplement les images les unes après les autres.

## Contraintes Panorama retenues

| Point | Réponse |
|---|---|
| Moment de la génération | **À l'avance**, au moment de la livraison |
| Désignation d'une image | Fichier sur disque, chemin **relatif au dossier images** de Panorama |
| Taille d'un objet image | Prend la taille de l'image par défaut. Déformation largeur/hauteur en % et position modifiables en exécution |
| États d'un bouton | **N états par bouton**, dont **un neutre** |
| Infobulle | Souhaitée |

## Chaîne complète

```
 exemple-barre.xml ─┐
 libelles.csv      ─┼─► Générateur C# ─┬─► <dossierSortie>\<Filtre>\<LANG>_<valeur>.png
 icones\*.png      ─┘   (à l'avance)   └─► <dossierSortie>\barre.manifest
                                                        │
                                   Script C# de la vue ◄┘  (exécution)
                                   - choisit l'image selon la langue et l'état
                                   - aligne les boutons (x += largeur + écart)
                                   - au clic : passe à l'état suivant du cycle
                                   - affiche l'infobulle de l'état
```

### Nommage des images

`<dossierSortie>\<idFiltre>\<LANGUE>_<valeur>[@<échelle>x].png`

Par exemple : `BarreFiltres\Evenements\CNX\DE_2.png`, ou `...\CNX\DE_2@2x.png` pour un écran
haute résolution. Le suffixe d'échelle n'apparaît que si `echelles` en déclare plusieurs.

### Manifeste

Le générateur produit un fichier `barre.manifest` (CSV UTF-8). Le script d'exécution n'a
ainsi ni à ouvrir les PNG, ni à traduire quoi que ce soit :

```
Filtre;Variable;Langue;Valeur;Neutre;Fichier;Largeur;Hauteur;Infobulle
CNX;Filtre.Connexion;DE;2;0;CNX\DE_2.png;104;28;Nach Verbindungsstatus filtern
```

La première ligne de chaque filtre donne l'ordre d'affichage. L'ordre de passage des
états au clic vient d'une ligne `#cycle;CNX;0 1 2`.

## Modèle d'état

- **Une variable entière par filtre** (attribut `variable`). Elle contient la valeur de
  l'état courant et sert de source de vérité aux autres traitements.
- **Neutre** : l'état marqué `neutre="true"` (en général la valeur 0) signifie
  « pas de filtrage ». Une commande « Réinitialiser » remet tous les filtres à leur état neutre.
- **Cycle** : chaque clic fait passer à l'état suivant de la liste `cycle`
  (par défaut, l'ordre de déclaration), en revenant au début après le dernier.
- **Lecture globale** : une chaîne `EVT=1;CNX=0;COM=1` pour un échange ou une
  sauvegarde, et une fonction `EtatFiltre("CNX")` pour un traitement ciblé.
  Le masque binaire de l'option D ne convient plus, puisqu'il y a plus de deux états.
- **Changement externe** : si un autre traitement modifie une variable, il suffit
  de rafraîchir l'image du bouton concerné. Ce rafraîchissement peut passer par une
  animation Panorama liée à la variable.

## Grammaire de configuration

Elle est décrite dans [barre-filtres.xsd](barre-filtres.xsd), avec un exemple complet
dans [exemple-barre.xml](exemple-barre.xml) (les 3 filtres actuels, dont deux à 3 états).
J'ai retenu le **XML avec un schéma XSD** pour trois raisons :

- il se lit nativement en .NET 4 (`XmlSerializer`, `XDocument`) sans aucune dépendance ;
- Visual Studio offre l'autocomplétion et la validation à la saisie dès que le schéma est associé au fichier ;
- les références incohérentes (style inconnu, identifiant ou variable en double,
  valeur d'état en double) sont rejetées par le schéma lui-même.

```
BarreFiltres  version langues dossierSortie ecart
├── Charte
│   ├── Police     nom taille unite
│   ├── Geometrie  hauteur rayon margeGauche margeDroite tailleIcone espaceIcone
│   │              largeurMinimale largeurMaximale echelles
│   └── Style*     id herite? fond bordure epaisseurBordure texte couleurIcone gras
├── Textes
│   ├── Fichier*   chemin                      (table Cle;Langue;Libelle existante)
│   └── Texte*     cle  └── T* langue          (textes propres à la barre)
└── Filtre+        id variable libelle icone infobulle cycle?
    └── Etat{2..}  valeur neutre? style libelle? icone? infobulle? + surcharges de style
```

**Résolution d'un état** : on part des attributs du filtre (libellé, icône, infobulle),
l'état les surcharge, puis on applique le style désigné, éventuellement avec héritage
(`herite`), et enfin les surcharges de style posées sur l'état lui-même.

**Règles vérifiées par le générateur** (non exprimables en XSD) : exactement un état
neutre par filtre, des valeurs de `cycle` qui existent, une traduction de chaque clé
dans chaque langue déclarée, des fichiers d'icônes présents.

## Points ouverts

1. **Infobulle** : comment Panorama l'affiche-t-il sur un objet image (propriété
   native de l'objet, ou vue superposée à gérer par script) ?
2. **Survol** : faut-il un visuel de survol ? Il suffirait d'ajouter un style `survol`
   par état et de générer les images `_h`.
3. **Icônes** : sources monochromes (recolorées avec `couleurIcone`) ou en couleur (`origine`) ?
4. **Changement de langue** : quel événement déclenche le rafraîchissement de la barre ?

## Suite proposée

1. Valider la grammaire, ou l'amender.
2. Générateur C# (`csc` .NET 4, sans SDK) : lecture et validation du XML, rendu
   GDI+ des PNG, écriture du manifeste, rapport d'anomalies.
3. Script C# d'exécution : lecture du manifeste, mise en page, cycle des états,
   infobulles. Il reprendra la logique déjà écrite dans `panorama/BarreFiltres.cs`
   et restera isolé de l'API Panorama par des adaptateurs.
