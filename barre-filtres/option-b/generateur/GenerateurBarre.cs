// GenerateurBarre.cs
// ---------------------------------------------------------------------------------
// Générateur des images de la barre de filtres Panorama E2 (option B).
//
// Entrée  : fichier XML conforme à barre-filtres.xsd
// Sorties : <racine>\<dossierSortie>\<Filtre>\<LANGUE>_<valeur>[@<échelle>x].png
//           <racine>\<dossierSortie>\barre.manifest   (lu par le script d'exécution)
//           <racine>\<dossierSortie>\apercu.html      (planche de contrôle visuel)
//
// Rien n'est écrit si la configuration contient une erreur.
// Compilation sans SDK : build.cmd (csc .NET Framework 4.x). Syntaxe limitée à C# 5.
// ---------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Panorama.BarreFiltres.Generateur
{
    // =============================================================================
    // Modèle
    // =============================================================================

    internal sealed class Charte
    {
        public string Police;
        public float Taille;
        public GraphicsUnit Unite;
        public int Hauteur, Rayon, MargeG, MargeD, TailleIcone, EspaceIcone, LargeurMin, LargeurMax;
        public List<float> Echelles = new List<float>();
        public Dictionary<string, XElement> Styles = new Dictionary<string, XElement>();
    }

    internal sealed class StyleResolu
    {
        public Color Fond = Color.White;
        public Color Bordure = Color.Transparent;
        public float EpaisseurBordure;
        public Color Texte = Color.Black;
        public Color? CouleurIcone;           // null = couleurs d'origine
        public bool Gras;
    }

    internal sealed class EtatDef
    {
        public int Valeur;
        public bool Neutre;
        public string Libelle, Icone, Infobulle;
        public StyleResolu Style;
    }

    internal sealed class FiltreDef
    {
        public string Id, Variable;
        public List<EtatDef> Etats = new List<EtatDef>();
        public List<int> Cycle = new List<int>();
    }

    internal sealed class Config
    {
        public string Fichier, Dossier;           // fichier XML et son dossier
        public List<string> Langues = new List<string>();
        public string LangueRepli, DossierSortie;
        public int Ecart;
        public Charte Charte = new Charte();
        public Dictionary<string, Dictionary<string, string>> Textes =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        public List<FiltreDef> Filtres = new List<FiltreDef>();
    }

    internal sealed class ImageProduite
    {
        public FiltreDef Filtre;
        public EtatDef Etat;
        public string Langue, Fichier, Infobulle;
        public float Echelle;
        public int Largeur, Hauteur;
        public bool Tronque;
    }

    // =============================================================================
    // Programme
    // =============================================================================

    internal static class Programme
    {
        public const string Ns = "urn:panorama:barre-filtres:1";
        private static readonly List<string> Erreurs = new List<string>();
        private static readonly List<string> Avertissements = new List<string>();

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }   // accents en console
            string xml = null, racine = null, xsd = null;
            bool apercu = true, nettoyer = false;
            foreach (string a in args)
            {
                if (a.StartsWith("/racine:", StringComparison.OrdinalIgnoreCase)) racine = a.Substring(8).Trim('"');
                else if (a.StartsWith("/xsd:", StringComparison.OrdinalIgnoreCase)) xsd = a.Substring(5).Trim('"');
                else if (a.Equals("/apercu-", StringComparison.OrdinalIgnoreCase)) apercu = false;
                else if (a.Equals("/nettoyer", StringComparison.OrdinalIgnoreCase)) nettoyer = true;
                else if (a.StartsWith("/")) { Aide(); return 1; }
                else xml = a;
            }
            if (xml == null) { Aide(); return 1; }
            xml = Path.GetFullPath(Chemin(xml));
            if (!File.Exists(xml)) { Console.Error.WriteLine("Fichier introuvable : " + xml); return 2; }
            if (racine == null) racine = Path.Combine(Path.GetDirectoryName(xml), "sortie");

            Config cfg;
            try
            {
                XDocument doc = ChargerEtValider(xml, xsd);
                if (Erreurs.Count > 0) return Bilan(3);
                cfg = Lire(doc, xml);
                Verifier(cfg);
                if (Erreurs.Count > 0) return Bilan(3);
            }
            catch (Exception ex)
            {
                Erreurs.Add(ex.Message);
                return Bilan(3);
            }

            string dossier = Path.Combine(Path.GetFullPath(Chemin(racine)), Chemin(cfg.DossierSortie));
            List<ImageProduite> images = Generer(cfg, dossier, nettoyer);
            if (Erreurs.Count > 0) return Bilan(4);

            EcrireManifeste(cfg, images, Path.Combine(dossier, "barre.manifest"));
            if (apercu) EcrireApercu(cfg, images, Path.Combine(dossier, "apercu.html"));

            Console.WriteLine(images.Count + " image(s) générée(s) dans " + dossier);
            return Bilan(0);
        }

        private static void Aide()
        {
            Console.Error.WriteLine("GenerateurBarre <config.xml> [/racine:<dossier images Panorama>] [/xsd:<schéma>] [/nettoyer] [/apercu-]");
            Console.Error.WriteLine("  /racine   dossier images de Panorama (défaut : <dossier du XML>\\sortie)");
            Console.Error.WriteLine("  /xsd      schéma (défaut : barre-filtres.xsd à côté du XML, puis à côté de l'exe)");
            Console.Error.WriteLine("  /nettoyer supprime les PNG existants des dossiers de filtres avant génération");
            Console.Error.WriteLine("  /apercu-  ne pas produire apercu.html");
        }

        private static int Bilan(int code)
        {
            foreach (string a in Avertissements) Console.Error.WriteLine("Avertissement : " + a);
            foreach (string e in Erreurs) Console.Error.WriteLine("ERREUR : " + e);
            if (Erreurs.Count > 0)
            {
                Console.Error.WriteLine(Erreurs.Count + " erreur(s) : génération annulée ou incomplète.");
                return code == 0 ? 3 : code;
            }
            return code;
        }

        // Les chemins du XML utilisent '\' (Windows) ; on les rend portables.
        private static string Chemin(string p)
        {
            return p.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

        // =========================================================================
        // Chargement, validation XSD
        // =========================================================================

        private static XDocument ChargerEtValider(string xml, string xsd)
        {
            if (xsd == null)
            {
                string[] candidats =
                {
                    Path.Combine(Path.GetDirectoryName(xml), "barre-filtres.xsd"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "barre-filtres.xsd")
                };
                xsd = candidats.FirstOrDefault(File.Exists);
            }

            XDocument doc = XDocument.Load(xml, LoadOptions.SetLineInfo);
            if (xsd == null)
            {
                Avertissements.Add("schéma barre-filtres.xsd introuvable : validation XSD non effectuée.");
                return doc;
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(Ns, Path.GetFullPath(Chemin(xsd)));
            doc.Validate(schemas, (o, e) =>
            {
                IXmlLineInfo li = o as IXmlLineInfo;
                string pos = li != null && li.HasLineInfo() ? " (ligne " + li.LineNumber + ")" : "";
                if (e.Severity == XmlSeverityType.Error) Erreurs.Add("XSD" + pos + " : " + e.Message);
                else Avertissements.Add("XSD" + pos + " : " + e.Message);
            });
            return doc;
        }

        private static XName N(string local) { return XName.Get(local, Ns); }

        private static string A(XElement e, string nom) { XAttribute a = e.Attribute(nom); return a == null ? null : a.Value; }

        private static int I(XElement e, string nom, int defaut)
        {
            string v = A(e, nom);
            return v == null ? defaut : int.Parse(v, CultureInfo.InvariantCulture);
        }

        private static float F(XElement e, string nom, float defaut)
        {
            string v = A(e, nom);
            return v == null ? defaut : float.Parse(v, CultureInfo.InvariantCulture);
        }

        private static int Ligne(XObject o)
        {
            IXmlLineInfo li = o;
            return li.HasLineInfo() ? li.LineNumber : 0;
        }

        // =========================================================================
        // Lecture du modèle
        // =========================================================================

        private static Config Lire(XDocument doc, string fichier)
        {
            Config c = new Config();
            c.Fichier = fichier;
            c.Dossier = Path.GetDirectoryName(fichier);
            XElement r = doc.Root;
            if (r == null || r.Name != N("BarreFiltres")) throw new InvalidDataException("racine <BarreFiltres> attendue (espace de noms " + Ns + ")");

            c.Langues = A(r, "langues").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            c.LangueRepli = A(r, "langueRepli") ?? c.Langues[0];
            c.DossierSortie = A(r, "dossierSortie");
            c.Ecart = I(r, "ecart", 8);

            // Charte
            XElement ch = r.Element(N("Charte"));
            XElement po = ch.Element(N("Police"));
            XElement ge = ch.Element(N("Geometrie"));
            Charte k = c.Charte;
            k.Police = A(po, "nom");
            k.Taille = F(po, "taille", 9f);
            k.Unite = A(po, "unite") == "px" ? GraphicsUnit.Pixel : GraphicsUnit.Point;
            k.Hauteur = I(ge, "hauteur", 28);
            k.Rayon = I(ge, "rayon", 0);
            k.MargeG = I(ge, "margeGauche", 10);
            k.MargeD = I(ge, "margeDroite", 12);
            k.TailleIcone = I(ge, "tailleIcone", 16);
            k.EspaceIcone = I(ge, "espaceIcone", 6);
            k.LargeurMin = I(ge, "largeurMinimale", 0);
            k.LargeurMax = I(ge, "largeurMaximale", 0);
            foreach (string s in (A(ge, "echelles") ?? "1").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                float f = float.Parse(s, CultureInfo.InvariantCulture);
                if (f <= 0) Erreurs.Add("échelle invalide : " + s);
                else if (!k.Echelles.Contains(f)) k.Echelles.Add(f);
            }
            foreach (XElement st in ch.Elements(N("Style"))) k.Styles[A(st, "id")] = st;

            // Textes
            XElement tx = r.Element(N("Textes"));
            if (tx != null)
            {
                foreach (XElement f in tx.Elements(N("Fichier")))
                    LireCsvTextes(c, Path.Combine(c.Dossier, Chemin(A(f, "chemin"))));
                foreach (XElement t in tx.Elements(N("Texte")))
                    foreach (XElement tt in t.Elements(N("T")))
                        AjouterTexte(c, A(t, "cle"), A(tt, "langue"), tt.Value);
            }

            // Filtres
            foreach (XElement fe in r.Elements(N("Filtre")))
            {
                FiltreDef f = new FiltreDef();
                f.Id = A(fe, "id");
                f.Variable = A(fe, "variable");
                foreach (XElement ee in fe.Elements(N("Etat")))
                {
                    EtatDef e = new EtatDef();
                    e.Valeur = I(ee, "valeur", 0);
                    e.Neutre = A(ee, "neutre") == "true" || A(ee, "neutre") == "1";
                    e.Libelle = A(ee, "libelle") ?? A(fe, "libelle");
                    e.Icone = A(ee, "icone") ?? A(fe, "icone");
                    e.Infobulle = A(ee, "infobulle") ?? A(fe, "infobulle");
                    e.Style = ResoudreStyle(k, A(ee, "style"), ee, "filtre " + f.Id + " état " + e.Valeur);
                    f.Etats.Add(e);
                }
                string cy = A(fe, "cycle");
                if (cy == null) f.Cycle = f.Etats.Select(e => e.Valeur).ToList();
                else
                    foreach (string s in cy.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        f.Cycle.Add(int.Parse(s, CultureInfo.InvariantCulture));
                c.Filtres.Add(f);
            }
            return c;
        }

        private static void AjouterTexte(Config c, string cle, string langue, string texte)
        {
            Dictionary<string, string> parLangue;
            if (!c.Textes.TryGetValue(cle, out parLangue))
            {
                parLangue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                c.Textes[cle] = parLangue;
            }
            parLangue[langue.Trim().ToUpperInvariant()] = texte;   // le dernier défini l'emporte (XML après CSV)
        }

        private static void LireCsvTextes(Config c, string chemin)
        {
            if (!File.Exists(chemin)) { Erreurs.Add("table de textes introuvable : " + chemin); return; }
            string[] lignes = File.ReadAllLines(chemin, Encoding.UTF8);
            for (int i = 0; i < lignes.Length; i++)
            {
                string l = lignes[i];
                if (l.Trim().Length == 0 || l.TrimStart().StartsWith("#")) continue;
                List<string> ch = Csv.Decouper(l, ';');
                if (ch.Count < 3) { Avertissements.Add(Path.GetFileName(chemin) + " ligne " + (i + 1) + " ignorée"); continue; }
                if (i == 0 && ch[0].Trim().Equals("Cle", StringComparison.OrdinalIgnoreCase)) continue;
                AjouterTexte(c, ch[0].Trim(), ch[1], ch[2]);
            }
        }

        // Style : chaîne d'héritage (du plus général au plus précis), puis surcharges posées sur l'état.
        private static StyleResolu ResoudreStyle(Charte k, string id, XElement surcharge, string contexte)
        {
            List<XElement> chaine = new List<XElement>();
            HashSet<string> vus = new HashSet<string>();
            string courant = id;
            while (courant != null)
            {
                XElement st;
                if (!k.Styles.TryGetValue(courant, out st)) { Erreurs.Add(contexte + " : style inconnu '" + courant + "'"); break; }
                if (!vus.Add(courant)) { Erreurs.Add(contexte + " : héritage circulaire sur le style '" + courant + "'"); break; }
                chaine.Insert(0, st);
                courant = A(st, "herite");
            }
            chaine.Add(surcharge);

            StyleResolu r = new StyleResolu();
            foreach (XElement e in chaine)
            {
                string v;
                if ((v = A(e, "fond")) != null) r.Fond = Couleur(v);
                if ((v = A(e, "bordure")) != null) r.Bordure = Couleur(v);
                if ((v = A(e, "epaisseurBordure")) != null) r.EpaisseurBordure = float.Parse(v, CultureInfo.InvariantCulture);
                if ((v = A(e, "texte")) != null) r.Texte = Couleur(v);
                if ((v = A(e, "couleurIcone")) != null) r.CouleurIcone = v == "origine" ? (Color?)null : Couleur(v);
                if ((v = A(e, "gras")) != null) r.Gras = v == "true" || v == "1";
            }
            return r;
        }

        // #RRGGBB, #RRGGBBAA ou "transparent"
        private static Color Couleur(string v)
        {
            if (v == "transparent") return Color.Transparent;
            int r = Convert.ToInt32(v.Substring(1, 2), 16);
            int g = Convert.ToInt32(v.Substring(3, 2), 16);
            int b = Convert.ToInt32(v.Substring(5, 2), 16);
            int a = v.Length >= 9 ? Convert.ToInt32(v.Substring(7, 2), 16) : 255;
            return Color.FromArgb(a, r, g, b);
        }

        // =========================================================================
        // Règles non exprimables en XSD
        // =========================================================================

        private static void Verifier(Config c)
        {
            if (!c.Langues.Contains(c.LangueRepli)) Erreurs.Add("langueRepli '" + c.LangueRepli + "' absente de langues");
            if (FontFamily.Families.All(ff => !ff.Name.Equals(c.Charte.Police, StringComparison.OrdinalIgnoreCase)))
                Erreurs.Add("police '" + c.Charte.Police + "' non installée sur ce poste : le rendu serait faux");

            foreach (FiltreDef f in c.Filtres)
            {
                string ctx = "filtre " + f.Id;
                int nbNeutres = f.Etats.Count(e => e.Neutre);
                if (nbNeutres != 1) Erreurs.Add(ctx + " : " + nbNeutres + " état(s) neutre(s), exactement 1 attendu");

                foreach (int v in f.Cycle)
                    if (f.Etats.All(e => e.Valeur != v)) Erreurs.Add(ctx + " : cycle cite la valeur " + v + " qui n'est pas un état déclaré");
                if (f.Cycle.Distinct().Count() != f.Cycle.Count) Erreurs.Add(ctx + " : valeur répétée dans cycle");
                foreach (EtatDef e in f.Etats)
                    if (!f.Cycle.Contains(e.Valeur))
                        Avertissements.Add(ctx + " : l'état " + e.Valeur + " n'est pas dans le cycle (atteignable seulement par programme)");

                foreach (EtatDef e in f.Etats)
                {
                    string ctxE = ctx + " état " + e.Valeur;
                    if (e.Libelle == null) Erreurs.Add(ctxE + " : aucun libellé");
                    else VerifierTraductions(c, e.Libelle, ctxE);
                    if (e.Infobulle != null) VerifierTraductions(c, e.Infobulle, ctxE + " (infobulle)");
                    if (e.Icone != null && !File.Exists(CheminIcone(c, e.Icone)))
                        Erreurs.Add(ctxE + " : icône introuvable " + CheminIcone(c, e.Icone));
                }
            }
        }

        private static void VerifierTraductions(Config c, string cle, string ctx)
        {
            Dictionary<string, string> t;
            if (!c.Textes.TryGetValue(cle, out t)) { Erreurs.Add(ctx + " : texte '" + cle + "' non défini"); return; }
            foreach (string l in c.Langues)
                if (!t.ContainsKey(l)) Erreurs.Add(ctx + " : texte '" + cle + "' non traduit en " + l);
        }

        private static string CheminIcone(Config c, string icone)
        {
            return Path.Combine(c.Dossier, Chemin(icone));
        }

        // =========================================================================
        // Génération
        // =========================================================================

        private static List<ImageProduite> Generer(Config c, string dossier, bool nettoyer)
        {
            List<ImageProduite> res = new List<ImageProduite>();
            Dictionary<string, Image> icones = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (FiltreDef f in c.Filtres)
                {
                    string dosF = Path.Combine(dossier, f.Id);
                    Directory.CreateDirectory(dosF);
                    if (nettoyer) foreach (string p in Directory.GetFiles(dosF, "*.png")) File.Delete(p);

                    foreach (string langue in c.Langues)
                        foreach (EtatDef e in f.Etats)
                            foreach (float ech in c.Charte.Echelles)
                            {
                                Image ico = null;
                                if (e.Icone != null && !icones.TryGetValue(e.Icone, out ico))
                                {
                                    ico = Image.FromFile(CheminIcone(c, e.Icone));
                                    icones[e.Icone] = ico;
                                }

                                ImageProduite ip = new ImageProduite();
                                ip.Filtre = f; ip.Etat = e; ip.Langue = langue; ip.Echelle = ech;
                                ip.Infobulle = e.Infobulle == null ? "" : c.Textes[e.Infobulle][langue];
                                string nom = langue + "_" + e.Valeur.ToString(CultureInfo.InvariantCulture) +
                                             (ech == 1f ? "" : "@" + ech.ToString(CultureInfo.InvariantCulture) + "x") + ".png";
                                ip.Fichier = Path.Combine(Chemin(c.DossierSortie), Path.Combine(f.Id, nom)).Replace('/', '\\');

                                bool tronque;
                                using (Bitmap bmp = Rendu.Bouton(c.Textes[e.Libelle][langue], ico, e.Style, c.Charte, ech, out tronque))
                                {
                                    bmp.Save(Path.Combine(dosF, nom), ImageFormat.Png);
                                    ip.Largeur = bmp.Width;
                                    ip.Hauteur = bmp.Height;
                                }
                                ip.Tronque = tronque;
                                if (tronque && ech == c.Charte.Echelles[0])
                                    Avertissements.Add("filtre " + f.Id + " état " + e.Valeur + " " + langue +
                                                       " : libellé tronqué (largeurMaximale)");
                                res.Add(ip);
                            }
                }
            }
            catch (Exception ex)
            {
                Erreurs.Add("génération : " + ex.Message);
            }
            finally
            {
                foreach (Image i in icones.Values) i.Dispose();
            }
            return res;
        }

        // =========================================================================
        // Manifeste et aperçu
        // =========================================================================

        private static void EcrireManifeste(Config c, List<ImageProduite> images, string chemin)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# barre.manifest - GÉNÉRÉ par GenerateurBarre depuis " + Path.GetFileName(c.Fichier) + " - ne pas modifier");
            sb.AppendLine("#version;1");
            sb.AppendLine("#ecart;" + c.Ecart.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("#langues;" + string.Join(" ", c.Langues.ToArray()));
            sb.AppendLine("#langueRepli;" + c.LangueRepli);
            sb.AppendLine("#echelles;" + string.Join(" ", c.Charte.Echelles.Select(e => e.ToString(CultureInfo.InvariantCulture)).ToArray()));
            sb.AppendLine("#ordre;" + string.Join(";", c.Filtres.Select(f => f.Id).ToArray()));
            foreach (FiltreDef f in c.Filtres)
            {
                sb.AppendLine("#filtre;" + f.Id + ";" + f.Variable + ";" +
                              f.Etats.First(e => e.Neutre).Valeur.ToString(CultureInfo.InvariantCulture) + ";" +
                              string.Join(" ", f.Cycle.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray()));
            }
            sb.AppendLine("Filtre;Langue;Valeur;Echelle;Fichier;Largeur;Hauteur;Tronque;Infobulle");
            foreach (ImageProduite i in images)
            {
                sb.Append(i.Filtre.Id).Append(';')
                  .Append(i.Langue).Append(';')
                  .Append(i.Etat.Valeur.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(i.Echelle.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(i.Fichier).Append(';')
                  .Append(i.Largeur.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(i.Hauteur.ToString(CultureInfo.InvariantCulture)).Append(';')
                  .Append(i.Tronque ? "1" : "0").Append(';')
                  .Append(Csv.Echapper(i.Infobulle, ';')).AppendLine();
            }
            File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(true));
        }

        private static void EcrireApercu(Config c, List<ImageProduite> images, string chemin)
        {
            float ech = c.Charte.Echelles[0];
            string prefixe = Chemin(c.DossierSortie).Replace('\\', '/').TrimEnd('/') + "/";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>Aperçu barre de filtres</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;margin:24px;color:#222}" +
                          "table{border-collapse:collapse;margin-bottom:28px}td,th{border:1px solid #ddd;padding:8px;vertical-align:middle}" +
                          "th{background:#f4f4f4;text-align:left}" +
                          "td.img{background-image:linear-gradient(45deg,#eee 25%,transparent 25%,transparent 75%,#eee 75%)," +
                          "linear-gradient(45deg,#eee 25%,transparent 25%,transparent 75%,#eee 75%);background-size:12px 12px;" +
                          "background-position:0 0,6px 6px}small{color:#777}.tr{color:#c00}" +
                          ".barre{display:flex;gap:" + c.Ecart + "px;margin:6px 0 24px}</style></head><body>");
            sb.AppendLine("<h1>Barre de filtres — aperçu</h1><p><small>" + Html(Path.GetFileName(c.Fichier)) +
                          " · échelle " + ech.ToString(CultureInfo.InvariantCulture) +
                          " · survol d'une image : infobulle</small></p>");

            sb.AppendLine("<h2>Barre à l'état neutre</h2>");
            foreach (string l in c.Langues)
            {
                sb.Append("<div><small>" + l + "</small><div class=\"barre\">");
                foreach (FiltreDef f in c.Filtres)
                {
                    ImageProduite i = images.First(x => x.Filtre == f && x.Langue == l && x.Etat.Neutre && x.Echelle == ech);
                    sb.Append(Img(i, prefixe));
                }
                sb.AppendLine("</div></div>");
            }

            foreach (FiltreDef f in c.Filtres)
            {
                sb.AppendLine("<h2>" + Html(f.Id) + " <small>variable " + Html(f.Variable) + " · cycle " +
                              string.Join(" → ", f.Cycle.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray()) + "</small></h2>");
                sb.Append("<table><tr><th>État</th>");
                foreach (string l in c.Langues) sb.Append("<th>" + l + "</th>");
                sb.AppendLine("</tr>");
                foreach (EtatDef e in f.Etats)
                {
                    sb.Append("<tr><th>" + e.Valeur + (e.Neutre ? " (neutre)" : "") + "</th>");
                    foreach (string l in c.Langues)
                    {
                        ImageProduite i = images.First(x => x.Filtre == f && x.Langue == l && x.Etat == e && x.Echelle == ech);
                        sb.Append("<td class=\"img\">" + Img(i, prefixe) + "<br><small" + (i.Tronque ? " class=\"tr\"" : "") + ">" +
                                  i.Largeur + "×" + i.Hauteur + (i.Tronque ? " tronqué" : "") + "</small></td>");
                    }
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Img(ImageProduite i, string prefixe)
        {
            string rel = i.Fichier.Replace('\\', '/');
            if (rel.StartsWith(prefixe)) rel = rel.Substring(prefixe.Length);
            return "<img src=\"" + Html(rel) + "\" width=\"" + (int)Math.Round(i.Largeur / i.Echelle) +
                   "\" title=\"" + Html(i.Infobulle) + "\" alt=\"" + Html(i.Filtre.Id) + "\">";
        }

        private static string Html(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }

    // =============================================================================
    // Rendu GDI+
    // =============================================================================

    internal static class Rendu
    {
        // GDI+ (DrawString) plutôt que GDI (TextRenderer) : GDI ne gère pas la couche alpha
        // et laisserait des pixels transparents dans le texte des PNG.
        public static Bitmap Bouton(string texte, Image icone, StyleResolu st, Charte ch, float s, out bool tronque)
        {
            tronque = false;
            int h = Arrondi(ch.Hauteur * s);
            float margeG = ch.MargeG * s, margeD = ch.MargeD * s;
            float partIcone = icone != null ? (ch.TailleIcone + ch.EspaceIcone) * s : 0f;

            using (Font police = new Font(ch.Police, ch.Taille * s, st.Gras ? FontStyle.Bold : FontStyle.Regular, ch.Unite))
            using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                fmt.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                fmt.LineAlignment = StringAlignment.Center;
                fmt.Alignment = StringAlignment.Near;
                fmt.Trimming = StringTrimming.EllipsisCharacter;

                float lTexte;
                using (Bitmap m = new Bitmap(1, 1))
                {
                    m.SetResolution(96f, 96f);
                    using (Graphics g = Graphics.FromImage(m))
                    {
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        lTexte = (float)Math.Ceiling(g.MeasureString(texte, police, PointF.Empty, fmt).Width) + 1f;
                    }
                }

                int w = (int)Math.Ceiling(margeG + partIcone + lTexte + margeD);
                if (ch.LargeurMin > 0) w = Math.Max(w, Arrondi(ch.LargeurMin * s));
                if (ch.LargeurMax > 0 && w > Arrondi(ch.LargeurMax * s))
                {
                    w = Arrondi(ch.LargeurMax * s);
                    lTexte = w - margeG - partIcone - margeD;
                    tronque = true;
                }

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                bmp.SetResolution(96f, 96f);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    // Fond et bordure (la bordure est tracée à l'intérieur de l'image)
                    float ep = st.EpaisseurBordure * s;
                    float ins = ep / 2f;
                    RectangleF rf = new RectangleF(ins, ins, w - ep, h - ep);
                    using (GraphicsPath p = Arrondis(rf, Math.Min(ch.Rayon * s, rf.Height / 2f)))
                    {
                        if (st.Fond.A > 0) using (SolidBrush b = new SolidBrush(st.Fond)) g.FillPath(b, p);
                        if (ep > 0 && st.Bordure.A > 0) using (Pen pen = new Pen(st.Bordure, ep)) g.DrawPath(pen, p);
                    }

                    // Icône
                    if (icone != null)
                    {
                        float ti = ch.TailleIcone * s;
                        RectangleF ri = new RectangleF(margeG, (h - ti) / 2f, ti, ti);
                        DessinerIcone(g, icone, ri, st.CouleurIcone);
                    }

                    // Texte
                    RectangleF rt = new RectangleF(margeG + partIcone, 0, lTexte, h);
                    using (SolidBrush b = new SolidBrush(st.Texte)) g.DrawString(texte, police, b, rt, fmt);
                }
                return bmp;
            }
        }

        // Recolorisation : RVB remplacés par la couleur, alpha de l'icône multiplié par celui de la couleur.
        private static void DessinerIcone(Graphics g, Image ico, RectangleF r, Color? couleur)
        {
            Rectangle dest = new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
            if (couleur == null)
            {
                g.DrawImage(ico, dest);
                return;
            }
            Color c = couleur.Value;
            ColorMatrix m = new ColorMatrix(new[]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, c.A / 255f, 0 },
                new float[] { c.R / 255f, c.G / 255f, c.B / 255f, 0, 1 }
            });
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(m);
                ia.SetWrapMode(WrapMode.TileFlipXY);   // évite un liseré sur les bords lors du redimensionnement
                g.DrawImage(ico, dest, 0, 0, ico.Width, ico.Height, GraphicsUnit.Pixel, ia);
            }
        }

        private static GraphicsPath Arrondis(RectangleF r, float rayon)
        {
            GraphicsPath p = new GraphicsPath();
            if (rayon <= 0.5f) { p.AddRectangle(r); return p; }
            float d = rayon * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static int Arrondi(float v) { return (int)Math.Round(v); }
    }

    // =============================================================================
    // CSV (séparateur ';', guillemets doublés)
    // =============================================================================

    internal static class Csv
    {
        public static List<string> Decouper(string ligne, char sep)
        {
            List<string> champs = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < ligne.Length; i++)
            {
                char c = ligne[i];
                if (q)
                {
                    if (c == '"') { if (i + 1 < ligne.Length && ligne[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                    else sb.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == sep) { champs.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            champs.Add(sb.ToString());
            return champs;
        }

        public static string Echapper(string s, char sep)
        {
            if (s == null) return "";
            if (s.IndexOf(sep) < 0 && s.IndexOf('"') < 0 && s.IndexOf('\n') < 0) return s;
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
