' ==========================================================================
' BarreFiltres.vbs - Barre de filtres à état (vue Panorama E2 + images)
' --------------------------------------------------------------------------
' Contenu :
'   1. Configuration des filtres et des cotes de mise en page
'   2. Mise en page : calcul de la largeur de chaque bouton selon la langue
'   3. Gestion de l'état : bascule, lecture, écriture, masque global
'   4. ADAPTATEURS PANORAMA : seules fonctions à écrire avec l'API Panorama E2
'
' Dépendance : Largeurs.vbs, généré par LargeursLibelles.exe
'              (fonctions LargeurLibelle et LargeurMaxLibelle)
'
' Structure attendue d'une instance de bouton (vue "BtnFiltre") :
'   ImgGauche  image coin gauche   (largeur fixe COIN)
'   ImgMilieu  image centrale      (étirée horizontalement)
'   ImgDroite  image coin droit    (largeur fixe COIN)
'   Icone      image de l'icône    (largeur fixe ICONE)
'   Texte      texte du libellé    (largeur fixée par le script)
' Chaque image existe en variante "_off" et "_on".
' ==========================================================================
Option Explicit

' --- 1. Configuration -----------------------------------------------------

' Cotes en pixels (à aligner sur la maquette)
Const COIN      = 12    ' largeur des images de coin
Const MARGE_G   = 10    ' bord gauche -> icône
Const ICONE     = 16    ' largeur de l'icône
Const ESPACE    = 6     ' icône -> texte
Const MARGE_D   = 12    ' texte -> bord droit
Const ECART     = 8     ' espace entre deux boutons
Const L_TXT_DEF = 80    ' largeur de texte si aucune mesure n'est connue
Const L_TXT_MAX = 160   ' largeur de texte maximale (au-delà : texte tronqué)

' Un filtre = clé de traduction | variable d'état | nom de l'icône
' L'ordre du tableau donne l'ordre d'affichage et le rang du bit dans le masque.
Function ListeFiltres()
    ListeFiltres = Array( _
        Array("FLT_EVT_EN_COURS", "Filtre.EvtEnCours",  "ico_cloche"), _
        Array("FLT_CONNEXION",    "Filtre.Connexion",   "ico_wifi"), _
        Array("FLT_COMMENTAIRE",  "Filtre.Commentaire", "ico_commentaire") _
    )
End Function

Const F_CLE = 0, F_VAR = 1, F_ICONE = 2

' --- 2. Mise en page ------------------------------------------------------
' À appeler à l'ouverture de la vue ET sur l'événement de changement de langue.

Sub MiseEnPageBarre()
    Dim filtres, i, x, langue, wTxt, wBtn
    filtres = ListeFiltres()
    langue  = LangueCourante()
    x = 0
    For i = 0 To UBound(filtres)
        wTxt = LargeurTexte(filtres(i)(F_CLE), langue)
        wBtn = MARGE_G + ICONE + ESPACE + wTxt + MARGE_D
        AppliquerGeometrie i, x, wBtn, wTxt
        AfficherLibelle i, Traduire(filtres(i)(F_CLE))
        AfficherEtat i, LireEtat(i)
        x = x + wBtn + ECART
    Next
End Sub

' Largeur du texte : mesure de la langue courante,
' sinon la plus grande toutes langues confondues, sinon la valeur par défaut.
Function LargeurTexte(cle, langue)
    Dim w
    w = LargeurLibelle(cle, langue)
    If w < 0 Then w = LargeurMaxLibelle(cle)
    If w < 0 Then w = L_TXT_DEF
    If w > L_TXT_MAX Then w = L_TXT_MAX
    LargeurTexte = w
End Function

Sub AppliquerGeometrie(idx, x, wBtn, wTxt)
    PlacerObjet idx, "",          x,                      wBtn
    PlacerObjet idx, "ImgGauche", 0,                      COIN
    PlacerObjet idx, "ImgMilieu", COIN,                   wBtn - 2 * COIN
    PlacerObjet idx, "ImgDroite", wBtn - COIN,            COIN
    PlacerObjet idx, "Icone",     MARGE_G,                ICONE
    PlacerObjet idx, "Texte",     MARGE_G + ICONE + ESPACE, wTxt
End Sub

' --- 3. État --------------------------------------------------------------

' Clic sur un bouton (à appeler depuis l'animation "clic" de l'instance)
Sub BasculerFiltre(idx)
    EcrireEtat idx, Not LireEtat(idx)
End Sub

Function LireEtat(idx)
    Dim filtres
    filtres = ListeFiltres()
    LireEtat = CBool(LireVariable(filtres(idx)(F_VAR)))
End Function

Sub EcrireEtat(idx, actif)
    Dim filtres
    filtres = ListeFiltres()
    EcrireVariable filtres(idx)(F_VAR), CBool(actif)
    AfficherEtat idx, CBool(actif)
End Sub

' Masque global : bit i = filtre i actif (lecture en une fois par d'autres traitements)
Function MasqueFiltres()
    Dim filtres, i, m
    filtres = ListeFiltres()
    m = 0
    For i = 0 To UBound(filtres)
        If LireEtat(i) Then m = m Or (2 ^ i)
    Next
    MasqueFiltres = m
End Function

Sub AppliquerMasque(m)
    Dim filtres, i
    filtres = ListeFiltres()
    For i = 0 To UBound(filtres)
        EcrireEtat i, ((m And (2 ^ i)) <> 0)
    Next
End Sub

' Bascule visuelle off/on des quatre images d'un bouton
Sub AfficherEtat(idx, actif)
    Dim suffixe, filtres
    filtres = ListeFiltres()
    If actif Then suffixe = "_on" Else suffixe = "_off"
    ChangerImage idx, "ImgGauche", "fond_g" & suffixe
    ChangerImage idx, "ImgMilieu", "fond_m" & suffixe
    ChangerImage idx, "ImgDroite", "fond_d" & suffixe
    ChangerImage idx, "Icone",     filtres(idx)(F_ICONE) & suffixe
End Sub

' ==========================================================================
' 4. ADAPTATEURS PANORAMA - à compléter avec la syntaxe Panorama E2
'    Rien d'autre dans ce fichier ne dépend de l'API Panorama.
' ==========================================================================

' Positionne et dimensionne horizontalement un objet du bouton idx.
' objet = "" désigne l'instance de vue elle-même (x relatif à la barre),
' sinon x est relatif au bouton.
Sub PlacerObjet(idx, objet, x, largeur)
    ' TODO Panorama : ex. Instance(idx).Objet(objet).Left = x / .Width = largeur
End Sub

' Remplace l'image affichée par un objet image du bouton idx.
Sub ChangerImage(idx, objet, nomImage)
    ' TODO Panorama (inutile si la bascule off/on est faite par une animation
    '      Panorama liée directement à la variable d'état)
End Sub

' Affiche le libellé traduit dans l'objet "Texte" du bouton idx.
Sub AfficherLibelle(idx, texte)
    ' TODO Panorama (inutile si l'objet texte est déjà lié à la clé de traduction)
End Sub

Function LangueCourante()
    LangueCourante = "FR"   ' TODO Panorama : code langue du poste (FR, EN, DE...)
End Function

Function Traduire(cle)
    Traduire = cle          ' TODO Panorama : texte de la clé dans la langue courante
End Function

Function LireVariable(nom)
    LireVariable = False    ' TODO Panorama : lecture de la variable d'état
End Function

Sub EcrireVariable(nom, valeur)
    ' TODO Panorama : écriture de la variable d'état
End Sub
