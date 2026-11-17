// BarreFiltresImages.cs - Script d'exécution de la barre de filtres (option B)
// ---------------------------------------------------------------------------------
// Lit le barre.manifest produit par GenerateurBarre, puis :
//   - affiche pour chaque filtre l'image correspondant à la langue et à l'état courants ;
//   - aligne les boutons (l'objet image prend la taille de son fichier) ;
//   - pose l'infobulle (propriété native de l'objet image Panorama) ;
//   - au clic, fait passer le filtre à l'état suivant de son cycle ;
//   - expose l'état aux autres traitements (par filtre, ou en chaîne "EVT=1;CNX=0").
//
// La vue contient un objet image par filtre, dans l'ordre du manifeste (rang 0..N-1).
// Seule la classe BarreFiltresPanorama (en bas) dépend de l'API Panorama E2.
// Syntaxe limitée à C# 5.
// ---------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Panorama.BarreFiltres
{
    // =============================================================================
    // Manifeste
    // =============================================================================

    public sealed class ImageBouton
    {
        public string Fichier;      // relatif au dossier images de Panorama
        public int Largeur, Hauteur;
        public bool Tronque;
        public string Infobulle;
    }

    public sealed class FiltreManifeste
    {
        public string Id, Variable;
        public int Rang, Neutre;
        public int[] Cycle;
        // clé : LANGUE|valeur|échelle
        public readonly Dictionary<string, ImageBouton> Images = new Dictionary<string, ImageBouton>(StringComparer.OrdinalIgnoreCase);

        public bool EtatExiste(int valeur, string langue, float echelle)
        {
            return Images.ContainsKey(Cle(langue, valeur, echelle));
        }

        public static string Cle(string langue, int valeur, float echelle)
        {
            return langue + "|" + valeur.ToString(CultureInfo.InvariantCulture) + "|" + echelle.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class Manifeste
    {
        public int Ecart = 8;
        public List<string> Langues = new List<string>();
        public string LangueRepli;
        public List<float> Echelles = new List<float>();
        public List<FiltreManifeste> Filtres = new List<FiltreManifeste>();
        public Dictionary<string, FiltreManifeste> ParId = new Dictionary<string, FiltreManifeste>(StringComparer.OrdinalIgnoreCase);

        public static Manifeste Charger(string chemin)
        {
            Manifeste m = new Manifeste();
            foreach (string brut in File.ReadAllLines(chemin, Encoding.UTF8))
            {
                string l = brut.Trim();
                if (l.Length == 0 || l.StartsWith("# ")) continue;
                List<string> c = Decouper(l);

                if (c[0].StartsWith("#"))
                {
                    switch (c[0])
                    {
                        case "#version":
                            if (c[1] != "1") throw new InvalidDataException("version de manifeste non gérée : " + c[1]);
                            break;
                        case "#ecart": m.Ecart = Entier(c[1]); break;
                        case "#langues": m.Langues = c[1].Split(' ').Where(s => s.Length > 0).ToList(); break;
                        case "#langueRepli": m.LangueRepli = c[1]; break;
                        case "#echelles": m.Echelles = c[1].Split(' ').Where(s => s.Length > 0).Select(Reel).ToList(); break;
                        case "#ordre":
                            for (int i = 1; i < c.Count; i++)
                            {
                                FiltreManifeste f = new FiltreManifeste();
                                f.Id = c[i];
                                f.Rang = i - 1;
                                m.Filtres.Add(f);
                                m.ParId[f.Id] = f;
                            }
                            break;
                        case "#filtre":
                            FiltreManifeste fd = m.ParId[c[1]];
                            fd.Variable = c[2];
                            fd.Neutre = Entier(c[3]);
                            fd.Cycle = c[4].Split(' ').Where(s => s.Length > 0).Select(Entier).ToArray();
                            break;
                    }
                    continue;
                }
                if (c[0] == "Filtre") continue;   // en-tête de colonnes

                // Filtre;Langue;Valeur;Echelle;Fichier;Largeur;Hauteur;Tronque;Infobulle
                ImageBouton img = new ImageBouton();
                img.Fichier = c[4];
                img.Largeur = Entier(c[5]);
                img.Hauteur = Entier(c[6]);
                img.Tronque = c[7] == "1";
                img.Infobulle = c.Count > 8 ? c[8] : "";
                m.ParId[c[0]].Images[FiltreManifeste.Cle(c[1], Entier(c[2]), Reel(c[3]))] = img;
            }
            if (m.LangueRepli == null && m.Langues.Count > 0) m.LangueRepli = m.Langues[0];
            if (m.Echelles.Count == 0) m.Echelles.Add(1f);
            return m;
        }

        private static int Entier(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }
        private static float Reel(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        private static List<string> Decouper(string ligne)
        {
            List<string> champs = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < ligne.Length; i++)
            {
                char ch = ligne[i];
                if (q)
                {
                    if (ch == '"') { if (i + 1 < ligne.Length && ligne[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                    else sb.Append(ch);
                }
                else if (ch == '"') q = true;
                else if (ch == ';') { champs.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(ch);
            }
            champs.Add(sb.ToString());
            return champs;
        }
    }

    // =============================================================================
    // Logique de la barre (indépendante de Panorama)
    // =============================================================================

    public abstract class BarreFiltresImagesBase
    {
        protected readonly Manifeste M;
        private string _langueAffichee;

        protected BarreFiltresImagesBase(string cheminManifeste)
        {
            M = Manifeste.Charger(cheminManifeste);
        }

        public IList<string> Filtres { get { return M.Filtres.Select(f => f.Id).ToList(); } }

        /// <summary>Langue réellement utilisée pour le dernier affichage.</summary>
        public string LangueAffichee { get { return _langueAffichee; } }

        // --- Affichage ---------------------------------------------------------

        /// <summary>
        /// Recalcule toute la barre. À appeler à l'ouverture de la vue, sur changement
        /// de la variable système de langue, et après tout changement d'état
        /// (un état peut changer le libellé, donc la largeur du bouton).
        /// </summary>
        public void MiseEnPage()
        {
            string langue = ResoudreLangue(LireLangueSysteme());
            float echelle = ChoisirEchelle();
            int x = 0;
            foreach (FiltreManifeste f in M.Filtres)
            {
                ImageBouton img = ImagePour(f, langue, LireValeurValide(f), echelle);
                AfficherImage(f.Id, f.Rang, img.Fichier);
                if (echelle != 1f) DeformerImage(f.Id, f.Rang, 100f / echelle);
                PlacerImage(f.Id, f.Rang, x);
                DefinirInfobulle(f.Id, f.Rang, img.Infobulle);
                x += (int)Math.Round(img.Largeur / echelle) + M.Ecart;
            }
            _langueAffichee = langue;
        }

        /// <summary>À brancher sur le changement de la variable système de langue.</summary>
        public void SurChangementLangue()
        {
            if (ResoudreLangue(LireLangueSysteme()) != _langueAffichee) MiseEnPage();
        }

        // --- État ----------------------------------------------------------------

        /// <summary>Clic sur le bouton : état suivant du cycle.</summary>
        public void Clic(string idFiltre)
        {
            FiltreManifeste f = Filtre(idFiltre);
            int courant = LireValeurValide(f);
            int pos = Array.IndexOf(f.Cycle, courant);
            int suivant = f.Cycle[(pos + 1) % f.Cycle.Length];   // pos = -1 (hors cycle) -> premier du cycle
            DefinirEtat(idFiltre, suivant);
        }

        /// <summary>Clic par rang (pratique si l'animation Panorama ne connaît que l'index de l'objet).</summary>
        public void ClicRang(int rang)
        {
            Clic(M.Filtres[rang].Id);
        }

        public int EtatFiltre(string idFiltre)
        {
            return LireValeurValide(Filtre(idFiltre));
        }

        public bool EstNeutre(string idFiltre)
        {
            FiltreManifeste f = Filtre(idFiltre);
            return LireValeurValide(f) == f.Neutre;
        }

        public void DefinirEtat(string idFiltre, int valeur)
        {
            FiltreManifeste f = Filtre(idFiltre);
            if (!f.EtatExiste(valeur, M.LangueRepli, M.Echelles[0]))
                throw new ArgumentOutOfRangeException("valeur", "état " + valeur + " inconnu pour le filtre " + idFiltre);
            EcrireVariable(f.Variable, valeur);
            MiseEnPage();
        }

        /// <summary>Remet tous les filtres à leur état neutre.</summary>
        public void Reinitialiser()
        {
            foreach (FiltreManifeste f in M.Filtres) EcrireVariable(f.Variable, f.Neutre);
            MiseEnPage();
        }

        /// <summary>Vrai si au moins un filtre n'est pas neutre.</summary>
        public bool FiltrageActif
        {
            get { return M.Filtres.Any(f => LireValeurValide(f) != f.Neutre); }
        }

        /// <summary>
        /// État global sous forme "EVT=1;CNX=0;COM=1" (lecture), ou application d'une
        /// telle chaîne (écriture : filtres absents inchangés, valeurs inconnues ignorées).
        /// </summary>
        public string Etats
        {
            get
            {
                return string.Join(";", M.Filtres.Select(f =>
                    f.Id + "=" + LireValeurValide(f).ToString(CultureInfo.InvariantCulture)).ToArray());
            }
            set
            {
                foreach (string paire in (value ?? "").Split(';'))
                {
                    string[] kv = paire.Split('=');
                    int v;
                    FiltreManifeste f;
                    if (kv.Length == 2 && M.ParId.TryGetValue(kv[0].Trim(), out f) &&
                        int.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) &&
                        f.EtatExiste(v, M.LangueRepli, M.Echelles[0]))
                    {
                        EcrireVariable(f.Variable, v);
                    }
                }
                MiseEnPage();
            }
        }

        // --- Internes --------------------------------------------------------------

        private FiltreManifeste Filtre(string id)
        {
            FiltreManifeste f;
            if (!M.ParId.TryGetValue(id, out f)) throw new ArgumentException("filtre inconnu : " + id);
            return f;
        }

        // Valeur de la variable ; neutre si elle ne correspond à aucun état connu.
        private int LireValeurValide(FiltreManifeste f)
        {
            int v = LireVariable(f.Variable);
            return f.EtatExiste(v, M.LangueRepli, M.Echelles[0]) ? v : f.Neutre;
        }

        private ImageBouton ImagePour(FiltreManifeste f, string langue, int valeur, float echelle)
        {
            ImageBouton img;
            if (f.Images.TryGetValue(FiltreManifeste.Cle(langue, valeur, echelle), out img)) return img;
            if (f.Images.TryGetValue(FiltreManifeste.Cle(M.LangueRepli, valeur, echelle), out img)) return img;
            return f.Images[FiltreManifeste.Cle(M.LangueRepli, f.Neutre, M.Echelles[0])];
        }

        /// <summary>
        /// Code langue Panorama -> langue du manifeste.
        /// Essais successifs : code exact ("FR"), préfixe ("fr-FR", "fr_FR" -> "FR"),
        /// table de correspondance (CorrespondanceLangue), puis langue de repli.
        /// </summary>
        protected string ResoudreLangue(string code)
        {
            string c = (code ?? "").Trim().ToUpperInvariant().Replace('_', '-');
            if (M.Langues.Contains(c)) return c;
            if (c.Length >= 2 && M.Langues.Contains(c.Substring(0, 2))) return c.Substring(0, 2);
            string corr = CorrespondanceLangue(code);
            if (corr != null && M.Langues.Contains(corr.ToUpperInvariant())) return corr.ToUpperInvariant();
            return M.LangueRepli;
        }

        private float ChoisirEchelle()
        {
            float voulue = EchelleEcran();
            // plus petite échelle générée >= échelle voulue, sinon la plus grande disponible
            float best = M.Echelles.Max();
            foreach (float e in M.Echelles) if (e >= voulue && e < best) best = e;
            return best;
        }

        // --- Adaptateurs Panorama ----------------------------------------------------

        protected abstract string LireLangueSysteme();
        protected abstract int LireVariable(string nom);
        protected abstract void EcrireVariable(string nom, int valeur);

        /// <summary>Change le fichier de l'objet image (chemin relatif au dossier images Panorama).</summary>
        protected abstract void AfficherImage(string idFiltre, int rang, string fichier);

        /// <summary>Position horizontale de l'objet image dans la vue.</summary>
        protected abstract void PlacerImage(string idFiltre, int rang, int x);

        protected abstract void DefinirInfobulle(string idFiltre, int rang, string texte);

        /// <summary>Déformation en % (largeur et hauteur) — utilisée seulement si échelle ≠ 1.</summary>
        protected virtual void DeformerImage(string idFiltre, int rang, float pourcentage) { }

        /// <summary>Échelle d'affichage souhaitée (1 = 96 DPI).</summary>
        protected virtual float EchelleEcran() { return 1f; }

        /// <summary>Si la variable système ne donne pas un code ISO (ex. LCID 1036), le traduire ici.</summary>
        protected virtual string CorrespondanceLangue(string codeSysteme) { return null; }
    }

    // =============================================================================
    // Implémentation Panorama E2 — à compléter
    // =============================================================================

    public sealed class BarreFiltresPanorama : BarreFiltresImagesBase
    {
        // Chemin du manifeste = <dossier images Panorama>\<dossierSortie>\barre.manifest
        public BarreFiltresPanorama(string cheminManifeste) : base(cheminManifeste) { }

        protected override string LireLangueSysteme()
        {
            return "FR";    // TODO Panorama : lecture de la variable système du code langue
        }

        protected override int LireVariable(string nom)
        {
            return 0;       // TODO Panorama : lecture de la variable entière d'état
        }

        protected override void EcrireVariable(string nom, int valeur)
        {
            // TODO Panorama : écriture de la variable d'état
        }

        protected override void AfficherImage(string idFiltre, int rang, string fichier)
        {
            // TODO Panorama : objet image n° rang -> fichier
        }

        protected override void PlacerImage(string idFiltre, int rang, int x)
        {
            // TODO Panorama : objet image n° rang -> position x
        }

        protected override void DefinirInfobulle(string idFiltre, int rang, string texte)
        {
            // TODO Panorama : objet image n° rang -> infobulle
        }
    }
}
