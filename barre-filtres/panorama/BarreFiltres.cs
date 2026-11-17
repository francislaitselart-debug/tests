// BarreFiltres.cs - Barre de filtres à état (vue Panorama E2 + images), version C#
// ---------------------------------------------------------------------------------
// Différence clé avec la version VBScript : la largeur des libellés est MESURÉE
// pendant l'exécution (TextRenderer / GDI) avec la police réelle de l'objet texte.
// Plus aucune table à régénérer quand un libellé change ou qu'une langue arrive.
// La table pré-calculée (LargeursLibelles.exe) reste utilisable en repli.
//
// Prérequis : le script s'exécute sur le poste client (là où la vue est affichée)
//             et peut référencer System.Drawing et System.Windows.Forms.
//
// Seule la classe dérivée de BarreFiltresBase (section 4) dépend de l'API Panorama.
// Syntaxe limitée à C# 5 pour rester compatible avec les compilateurs .NET 4.x.
// ---------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Panorama.BarreFiltres
{
    // --- 1. Configuration ------------------------------------------------------

    public sealed class DefFiltre
    {
        public readonly string CleTraduction;
        public readonly string VariableEtat;
        public readonly string Icone;

        public DefFiltre(string cleTraduction, string variableEtat, string icone)
        {
            CleTraduction = cleTraduction;
            VariableEtat = variableEtat;
            Icone = icone;
        }
    }

    public sealed class Cotes
    {
        public int Coin = 12;       // largeur des images de coin
        public int MargeG = 10;     // bord gauche -> icône
        public int Icone = 16;      // largeur de l'icône
        public int Espace = 6;      // icône -> texte
        public int MargeD = 12;     // texte -> bord droit
        public int Ecart = 8;       // espace entre deux boutons
        public int TexteDefaut = 80;
        public int TexteMax = 160;  // au-delà, le texte est tronqué
        public int MargeMesure = 2; // pixels de sécurité ajoutés à la mesure
    }

    public sealed class Police
    {
        public string Nom = "Segoe UI";
        public float Taille = 9f;
        public GraphicsUnit Unite = GraphicsUnit.Point;
        public FontStyle Style = FontStyle.Regular;

        public string Cle()
        {
            return Nom + "|" + Taille + "|" + Unite + "|" + Style;
        }
    }

    // --- 2. Mesure de texte (avec cache) ---------------------------------------

    public static class MesureTexte
    {
        private const TextFormatFlags Drapeaux =
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        private static readonly Dictionary<string, int> Cache = new Dictionary<string, int>();
        private static readonly object Verrou = new object();

        /// <summary>Largeur en pixels (96 DPI) de <paramref name="texte"/>, -1 en cas d'échec.</summary>
        public static int Largeur(string texte, Police police)
        {
            if (string.IsNullOrEmpty(texte)) return 0;
            string cle = police.Cle() + "|" + texte;

            lock (Verrou)
            {
                int w;
                if (Cache.TryGetValue(cle, out w)) return w;

                try
                {
                    using (Font f = new Font(police.Nom, police.Taille, police.Style, police.Unite))
                    using (Bitmap bmp = new Bitmap(1, 1))
                    {
                        bmp.SetResolution(96f, 96f);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            w = TextRenderer.MeasureText(g, texte, f,
                                    new Size(int.MaxValue, int.MaxValue), Drapeaux).Width;
                        }
                    }
                }
                catch (Exception)
                {
                    w = -1; // GDI indisponible (ex. exécution côté serveur) : le repli prend le relais
                }
                Cache[cle] = w;
                return w;
            }
        }

        public static void ViderCache()
        {
            lock (Verrou) { Cache.Clear(); }
        }
    }

    // --- 3. Logique de la barre (indépendante de Panorama) ---------------------

    public abstract class BarreFiltresBase
    {
        protected readonly IList<DefFiltre> Filtres;
        protected readonly Cotes C;

        protected BarreFiltresBase(IList<DefFiltre> filtres, Cotes cotes)
        {
            Filtres = filtres;
            C = cotes ?? new Cotes();
        }

        public int Nombre { get { return Filtres.Count; } }

        /// <summary>À appeler à l'ouverture de la vue et sur changement de langue.</summary>
        public void MiseEnPage()
        {
            Police police = PoliceTexte();
            int x = 0;
            for (int i = 0; i < Filtres.Count; i++)
            {
                string libelle = Traduire(Filtres[i].CleTraduction);
                int wTxt = LargeurTexte(Filtres[i].CleTraduction, libelle, police);
                int wBtn = C.MargeG + C.Icone + C.Espace + wTxt + C.MargeD;

                Placer(i, null,        x,                               wBtn);
                Placer(i, "ImgGauche", 0,                               C.Coin);
                Placer(i, "ImgMilieu", C.Coin,                          wBtn - 2 * C.Coin);
                Placer(i, "ImgDroite", wBtn - C.Coin,                   C.Coin);
                Placer(i, "Icone",     C.MargeG,                        C.Icone);
                Placer(i, "Texte",     C.MargeG + C.Icone + C.Espace,   wTxt);

                AfficherLibelle(i, libelle);
                AfficherEtat(i, LireEtat(i));
                x += wBtn + C.Ecart;
            }
        }

        // Mesure réelle -> table pré-calculée (langue, puis max) -> valeur par défaut ; plafonnée.
        protected int LargeurTexte(string cle, string libelle, Police police)
        {
            int w = MesureTexte.Largeur(libelle, police);
            if (w >= 0) w += C.MargeMesure;
            if (w < 0) w = LargeurPreCalculee(cle, LangueCourante());
            if (w < 0) w = C.TexteDefaut;
            return Math.Min(w, C.TexteMax);
        }

        // --- État ---

        public void Basculer(int idx)
        {
            Ecrire(idx, !LireEtat(idx));
        }

        public bool LireEtat(int idx)
        {
            return LireVariable(Filtres[idx].VariableEtat);
        }

        public void Ecrire(int idx, bool actif)
        {
            EcrireVariable(Filtres[idx].VariableEtat, actif);
            AfficherEtat(idx, actif);
        }

        /// <summary>Bit i = filtre i actif.</summary>
        public int Masque
        {
            get
            {
                int m = 0;
                for (int i = 0; i < Filtres.Count; i++) if (LireEtat(i)) m |= 1 << i;
                return m;
            }
            set
            {
                for (int i = 0; i < Filtres.Count; i++) Ecrire(i, (value & (1 << i)) != 0);
            }
        }

        /// <summary>Clés de traduction des filtres actifs (pratique pour journaliser ou construire une requête).</summary>
        public IList<string> FiltresActifs()
        {
            List<string> res = new List<string>();
            for (int i = 0; i < Filtres.Count; i++) if (LireEtat(i)) res.Add(Filtres[i].CleTraduction);
            return res;
        }

        protected void AfficherEtat(int idx, bool actif)
        {
            string s = actif ? "_on" : "_off";
            ChangerImage(idx, "ImgGauche", "fond_g" + s);
            ChangerImage(idx, "ImgMilieu", "fond_m" + s);
            ChangerImage(idx, "ImgDroite", "fond_d" + s);
            ChangerImage(idx, "Icone", Filtres[idx].Icone + s);
        }

        // --- 4. Adaptateurs Panorama (à implémenter) ---

        /// <summary>objet = null : l'instance de vue (x relatif à la barre) ; sinon x relatif au bouton.</summary>
        protected abstract void Placer(int idx, string objet, int x, int largeur);
        protected abstract void ChangerImage(int idx, string objet, string nomImage);
        protected abstract void AfficherLibelle(int idx, string texte);
        protected abstract string LangueCourante();
        protected abstract string Traduire(string cle);
        protected abstract bool LireVariable(string nom);
        protected abstract void EcrireVariable(string nom, bool valeur);

        /// <summary>Police réelle de l'objet texte (idéalement lue sur l'objet Panorama).</summary>
        protected virtual Police PoliceTexte() { return new Police(); }

        /// <summary>Repli : table générée par LargeursLibelles.exe, -1 si absente.</summary>
        protected virtual int LargeurPreCalculee(string cle, string langue) { return -1; }
    }

    // --- 4. Implémentation Panorama E2 -----------------------------------------

    public sealed class BarreFiltresPanorama : BarreFiltresBase
    {
        public BarreFiltresPanorama()
            : base(new List<DefFiltre>
                   {
                       new DefFiltre("FLT_EVT_EN_COURS", "Filtre.EvtEnCours",  "ico_cloche"),
                       new DefFiltre("FLT_CONNEXION",    "Filtre.Connexion",   "ico_wifi"),
                       new DefFiltre("FLT_COMMENTAIRE",  "Filtre.Commentaire", "ico_commentaire")
                   },
                   new Cotes())
        {
        }

        protected override void Placer(int idx, string objet, int x, int largeur)
        {
            // TODO Panorama : positionner / dimensionner l'objet (ou l'instance si objet == null)
        }

        protected override void ChangerImage(int idx, string objet, string nomImage)
        {
            // TODO Panorama (inutile si une animation est liée directement à la variable d'état)
        }

        protected override void AfficherLibelle(int idx, string texte)
        {
            // TODO Panorama (inutile si l'objet texte est déjà lié à la clé de traduction)
        }

        protected override string LangueCourante()
        {
            return "FR"; // TODO Panorama
        }

        protected override string Traduire(string cle)
        {
            return cle;  // TODO Panorama
        }

        protected override bool LireVariable(string nom)
        {
            return false; // TODO Panorama
        }

        protected override void EcrireVariable(string nom, bool valeur)
        {
            // TODO Panorama
        }
    }
}
