# Option B — boutons en images pré-générées

Chaque bouton est **une seule image** (fond, icône colorisée et libellé traduit),
générée à l'avance pour chaque filtre, langue et état. Le problème des libellés de
longueur variable disparaît : chaque image a sa propre largeur, et la barre aligne
simplement les images les unes après les autres.

## Contraintes Panorama retenues

| Point | Réponse | Traitement |
|---|---|---|
| Génération | À l'avance | `GenerateurBarre.exe`, lancé au moment de la livraison |
| Désignation d'une image | Fichier sur disque, chemin relatif au dossier images de Panorama | Le manifeste donne ce chemin relatif |
| Taille d'un objet image | Prend la taille de l'image. Déformation en % et position modifiables en exécution | Le script positionne seulement. La déformation sert aux échelles > 1 |
| États | N états par bouton, dont un neutre | Variable entière par filtre, cycle de clic configurable |
| Infobulle | Propriété native de l'objet image | Texte traduit fourni par le manifeste |
| Survol | Pas de visuel, seulement le texte | Pas d'image de survol : l'infobulle suffit |
| Icônes | Colorisées à la génération, pas par Panorama | Attribut `couleurIcone` du style (ou `origine`) |
| Langue | Variable système donnant le code langue | `SurChangementLangue()`, codes `fr`, `fr-FR`, `FR` acceptés, puis repli |

## Contenu

| Fichier | Rôle |
|---|---|
| [barre-filtres.xsd](barre-filtres.xsd) | Grammaire de configuration |
| [exemple-barre.xml](exemple-barre.xml) | Exemple : les 3 filtres actuels, dont deux à 3 états |
| [icones/](icones) | Icônes d'exemple, provisoires, à remplacer par les vôtres |
| [generateur/GenerateurBarre.cs](generateur/GenerateurBarre.cs) | Générateur des PNG, du manifeste et de l'aperçu |
| [generateur/build.cmd](generateur/build.cmd) | Compilation avec le `csc` du .NET Framework 4.x, sans SDK |
| [panorama/BarreFiltresImages.cs](panorama/BarreFiltresImages.cs) | Script d'exécution de la vue |

## Chaîne complète

```
 exemple-barre.xml ─┐
 libelles.csv      ─┼─► GenerateurBarre.exe ─┬─► <dossierSortie>\<Filtre>\<LANGUE>_<valeur>.png
 icones\*.png      ─┘   (à l'avance)         ├─► <dossierSortie>\barre.manifest
                                             └─► <dossierSortie>\apercu.html  (contrôle visuel)
                                                          │
                          BarreFiltresImages.cs (vue)  ◄──┘  en exécution
```

## 1. Générer les images

```bat
generateur\build.cmd
generateur\GenerateurBarre.exe exemple-barre.xml /racine:"C:\MonProjet\Images" /nettoyer
```

| Option | Effet |
|---|---|
| `/racine:` | Dossier images de Panorama. Par défaut : `sortie\` à côté du XML |
| `/xsd:` | Schéma. Par défaut : `barre-filtres.xsd` à côté du XML, puis à côté de l'exe |
| `/nettoyer` | Supprime d'abord les PNG existants des dossiers de filtres (états ou langues retirés) |
| `/apercu-` | Ne produit pas `apercu.html` |

Le générateur valide le XML contre le schéma, puis vérifie les règles que le schéma
ne peut pas exprimer :
- exactement un état neutre par filtre ;
- des valeurs de `cycle` qui existent ;
- toutes les traductions présentes ;
- toutes les icônes présentes ;
- la police installée sur le poste.

**Il n'écrit rien s'il trouve une erreur** et renvoie un code de sortie différent de 0, ce qui permet de l'intégrer à un script de livraison. Il signale aussi les libellés tronqués par `largeurMaximale`.

`apercu.html` affiche la barre à l'état neutre dans chaque langue, puis chaque filtre
avec ses états et ses dimensions. Au survol, on voit l'infobulle.

### Nommage

`<dossierSortie>\<idFiltre>\<LANGUE>_<valeur>.png`, et `…@<échelle>x.png` pour une
échelle différente de 1. Exemple : `BarreFiltres\Evenements\CNX\DE_2.png`.

### Manifeste `barre.manifest` (UTF-8)

```
#version;1
#ecart;8
#langues;FR EN DE ES
#langueRepli;EN
#echelles;1
#ordre;EVT;CNX;COM
#filtre;CNX;Filtre.Connexion;0;0 1 2          ← id ; variable ; valeur neutre ; cycle
Filtre;Langue;Valeur;Echelle;Fichier;Largeur;Hauteur;Tronque;Infobulle
CNX;DE;2;1;BarreFiltres\Evenements\CNX\DE_2.png;112;28;0;Nach Verbindungsstatus filtern
```

## 2. Intégrer dans la vue Panorama

1. Placer **un objet image par filtre** dans la vue, dans l'ordre de `#ordre` (rang 0, 1, 2…).
2. Compléter les adaptateurs de `BarreFiltresPanorama`, en bas de
   `BarreFiltresImages.cs`. C'est la seule partie qui dépend de l'API Panorama :

   | Méthode | À faire |
   |---|---|
   | `LireLangueSysteme()` | Lire la variable système du code langue |
   | `LireVariable` / `EcrireVariable` | Lire et écrire les variables entières d'état |
   | `AfficherImage(id, rang, fichier)` | Changer le fichier de l'objet image n° rang |
   | `PlacerImage(id, rang, x)` | Changer la position horizontale de l'objet |
   | `DefinirInfobulle(id, rang, texte)` | Changer l'infobulle de l'objet |
   | *(facultatif)* `DeformerImage`, `EchelleEcran` | Écrans haute résolution, si `echelles` en déclare plusieurs |
   | *(facultatif)* `CorrespondanceLangue` | Si la variable système ne donne pas un code ISO (ex. LCID 1036 → FR) |

3. Brancher les événements :

   | Événement Panorama | Appel |
   |---|---|
   | Ouverture de la vue | `MiseEnPage()` |
   | Changement de la variable système de langue | `SurChangementLangue()` |
   | Clic sur l'image n° *i* | `ClicRang(i)` ou `Clic("CNX")` |
   | Variable d'état modifiée par un autre traitement | `MiseEnPage()` |

## 3. Utiliser l'état dans d'autres traitements

| Membre | Usage |
|---|---|
| `EtatFiltre("CNX")` | Valeur courante du filtre. Une valeur inconnue est lue comme neutre |
| `EstNeutre("CNX")`, `FiltrageActif` | Tests rapides |
| `DefinirEtat("CNX", 2)` | Positionner un filtre. Une valeur inconnue lève une exception |
| `Etats` | Lecture : `"EVT=1;CNX=0;COM=1"`. Écriture : applique la chaîne, en ignorant les clés et valeurs inconnues |
| `Reinitialiser()` | Remet tous les filtres à l'état neutre |

Les variables Panorama (`Filtre.Connexion`…) restent la source de vérité. Un traitement
peut donc aussi les lire directement, sans passer par la classe.

## Grammaire de configuration

Elle est décrite dans [barre-filtres.xsd](barre-filtres.xsd). Pour l'édition dans **VS Code**,
l'extension *XML* de Red Hat (`redhat.vscode-xml`, recommandée par le dépôt) fournit
l'autocomplétion, l'aide au survol et la validation à la saisie. Il suffit que le fichier
déclare son schéma :
`xsi:schemaLocation="urn:panorama:barre-filtres:1 <chemin relatif>/barre-filtres.xsd"`.

```
BarreFiltres  version langues langueRepli? dossierSortie ecart
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

- **Couleurs** : `#RRGGBB`, `#RRGGBBAA` (AA = opacité) ou `transparent`.
  `couleurIcone="origine"` garde les couleurs de l'icône source.
- **Résolution d'un état** : on part des attributs du filtre (libellé, icône, infobulle),
  l'état les surcharge, puis on applique le style désigné et sa chaîne `herite`, et enfin
  les surcharges de style posées sur l'état lui-même.
- **Textes** : les définitions `<Texte>` du XML l'emportent sur celles des fichiers CSV.

## Points d'attention

- **Police** : générer sur un poste où la police de la charte est installée. Le
  générateur refuse de continuer sinon.
- **Rendu** : le texte est dessiné avec GDI+ (`DrawString`), et non avec GDI, pour garder
  une transparence correcte dans les PNG.
- **Ajout d'une langue** : l'ajouter dans `langues`, compléter les traductions, régénérer.
  Le script n'a pas besoin d'être modifié.
