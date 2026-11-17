// LargeursLibelles.cs
// ---------------------------------------------------------------------------
// Pré-calcul des largeurs (en pixels) des libellés multilingues de la barre
// de filtres Panorama E2.
//
// Panorama ne permet pas de mesurer un texte en exécution : cet outil mesure
// chaque libellé avec le moteur de rendu GDI de Windows (TextRenderer, le même
// que celui des applications Win32) et produit :
//   - un fichier CSV  : Cle;Langue;LargeurPx
//   - un fichier VBS  : fonctions LargeurLibelle(cle, langue) et
//                       LargeurMaxLibelle(cle) à inclure dans les scripts Panorama
//
// Compilation sans SDK (framework .NET 4.x présent sur tout Windows) : voir build.cmd
// Syntaxe volontairement limitée à C# 5 pour rester compatible avec csc.exe v4.
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Panorama.BarreFiltres
{
    internal sealed class Libelle
    {
        public string Cle;
        public string Langue;
        public string Texte;
        public int Largeur;
    }

    internal sealed class Options
    {
        public string Entree;
        public string SortieCsv;
        public string SortieVbs;
        public string Police = "Segoe UI";
        public float Taille = 9f;
        public GraphicsUnit Unite = GraphicsUnit.Point;
        public FontStyle Style = FontStyle.Regular;
        public int Marge = 2;
        public char Separateur = ';';
    }

    internal static class Programme
    {
        private static int Main(string[] args)
        {
            Options opt;
            try
            {
                opt = LireOptions(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Erreur : " + ex.Message);
                Console.Error.WriteLine();
                AfficherAide();
                return 1;
            }

            if (!File.Exists(opt.Entree))
            {
                Console.Error.WriteLine("Fichier introuvable : " + opt.Entree);
                return 2;
            }

            List<Libelle> libelles = LireCsv(opt.Entree, opt.Separateur);
            if (libelles.Count == 0)
            {
                Console.Error.WriteLine("Aucun libellé lu dans " + opt.Entree);
                return 3;
            }

            using (Font police = new Font(opt.Police, opt.Taille, opt.Style, opt.Unite))
            {
                if (!string.Equals(police.Name, opt.Police, StringComparison.OrdinalIgnoreCase))
                {
                    // Windows substitue silencieusement une police absente : les mesures seraient fausses.
                    Console.Error.WriteLine("Attention : police '" + opt.Police +
                                            "' non installée, remplacée par '" + police.Name + "'.");
                }
                Mesurer(libelles, police, opt.Marge);
            }

            if (!string.IsNullOrEmpty(opt.SortieCsv)) EcrireCsv(opt.SortieCsv, libelles, opt.Separateur);
            if (!string.IsNullOrEmpty(opt.SortieVbs)) EcrireVbs(opt.SortieVbs, libelles, opt);

            Console.WriteLine(libelles.Count + " libellé(s) mesuré(s).");
            return 0;
        }

        // --- Mesure ---------------------------------------------------------

        private static void Mesurer(List<Libelle> libelles, Font police, int marge)
        {
            const TextFormatFlags drapeaux = TextFormatFlags.NoPadding |
                                             TextFormatFlags.SingleLine |
                                             TextFormatFlags.NoPrefix;   // '&' affiché tel quel

            // Contexte de mesure à 96 DPI, indépendant de l'écran du poste qui lance l'outil.
            using (Bitmap bmp = new Bitmap(1, 1))
            {
                bmp.SetResolution(96f, 96f);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    foreach (Libelle l in libelles)
                    {
                        Size s = TextRenderer.MeasureText(g, l.Texte, police,
                                                          new Size(int.MaxValue, int.MaxValue), drapeaux);
                        l.Largeur = s.Width + marge;
                    }
                }
            }
        }

        // --- Lecture CSV ----------------------------------------------------
        // Format attendu (UTF-8, ligne d'en-tête facultative) :
        //   Cle;Langue;Libelle
        //   FLT_CONNEXION;FR;Connexion
        // Le libellé peut contenir le séparateur s'il est entre guillemets.

        private static List<Libelle> LireCsv(string chemin, char sep)
        {
            List<Libelle> res = new List<Libelle>();
            string[] lignes = File.ReadAllLines(chemin, Encoding.UTF8);
            for (int i = 0; i < lignes.Length; i++)
            {
                string ligne = lignes[i];
                if (ligne.Trim().Length == 0 || ligne.TrimStart().StartsWith("#")) continue;

                List<string> champs = DecouperCsv(ligne, sep);
                if (champs.Count < 3)
                {
                    Console.Error.WriteLine("Ligne " + (i + 1) + " ignorée (3 champs attendus) : " + ligne);
                    continue;
                }
                if (i == 0 && champs[0].Trim().Equals("Cle", StringComparison.OrdinalIgnoreCase)) continue;

                Libelle l = new Libelle();
                l.Cle = champs[0].Trim();
                l.Langue = champs[1].Trim().ToUpperInvariant();
                l.Texte = champs[2];
                res.Add(l);
            }
            return res;
        }

        private static List<string> DecouperCsv(string ligne, char sep)
        {
            List<string> champs = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool entreGuillemets = false;
            for (int i = 0; i < ligne.Length; i++)
            {
                char c = ligne[i];
                if (entreGuillemets)
                {
                    if (c == '"')
                    {
                        if (i + 1 < ligne.Length && ligne[i + 1] == '"') { sb.Append('"'); i++; }
                        else entreGuillemets = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') entreGuillemets = true;
                else if (c == sep) { champs.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            champs.Add(sb.ToString());
            return champs;
        }

        // --- Écriture -------------------------------------------------------

        private static void EcrireCsv(string chemin, List<Libelle> libelles, char sep)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Cle").Append(sep).Append("Langue").Append(sep).Append("LargeurPx").AppendLine();
            foreach (Libelle l in libelles)
            {
                sb.Append(l.Cle).Append(sep).Append(l.Langue).Append(sep)
                  .Append(l.Largeur.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }
            File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(false));
        }

        private static void EcrireVbs(string chemin, List<Libelle> libelles, Options opt)
        {
            // Largeur maximale par clé, toutes langues confondues (repli si une langue manque).
            Dictionary<string, int> maxParCle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<string> ordreCles = new List<string>();
            foreach (Libelle l in libelles)
            {
                int m;
                if (!maxParCle.TryGetValue(l.Cle, out m)) { ordreCles.Add(l.Cle); m = 0; }
                if (l.Largeur > m) maxParCle[l.Cle] = l.Largeur;
                else maxParCle[l.Cle] = m;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("' ==========================================================================");
            sb.AppendLine("' FICHIER GÉNÉRÉ par LargeursLibelles.exe - ne pas modifier à la main.");
            sb.AppendLine("' Source  : " + Path.GetFileName(opt.Entree));
            sb.AppendLine("' Police  : " + opt.Police + " " +
                          opt.Taille.ToString(CultureInfo.InvariantCulture) +
                          (opt.Unite == GraphicsUnit.Pixel ? "px" : "pt") +
                          (opt.Style == FontStyle.Bold ? " gras" : "") +
                          ", marge +" + opt.Marge + "px");
            sb.AppendLine("' Généré  : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            sb.AppendLine("' ==========================================================================");
            sb.AppendLine();
            sb.AppendLine("' Largeur en pixels du libellé <cle> dans la langue <langue>, -1 si inconnue.");
            sb.AppendLine("Function LargeurLibelle(cle, langue)");
            sb.AppendLine("    Select Case UCase(langue) & \"|\" & UCase(cle)");
            foreach (Libelle l in libelles)
            {
                sb.AppendLine("        Case \"" + EchapperVbs(l.Langue + "|" + l.Cle.ToUpperInvariant()) +
                              "\": LargeurLibelle = " + l.Largeur.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine("        Case Else: LargeurLibelle = -1");
            sb.AppendLine("    End Select");
            sb.AppendLine("End Function");
            sb.AppendLine();
            sb.AppendLine("' Largeur maximale du libellé <cle> toutes langues confondues, -1 si inconnue.");
            sb.AppendLine("Function LargeurMaxLibelle(cle)");
            sb.AppendLine("    Select Case UCase(cle)");
            foreach (string cle in ordreCles)
            {
                sb.AppendLine("        Case \"" + EchapperVbs(cle.ToUpperInvariant()) + "\": LargeurMaxLibelle = " +
                              maxParCle[cle].ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine("        Case Else: LargeurMaxLibelle = -1");
            sb.AppendLine("    End Select");
            sb.AppendLine("End Function");

            // ANSI (page de code du poste) : l'encodage attendu par l'éditeur de scripts VBS Windows.
            File.WriteAllText(chemin, sb.ToString(), Encoding.Default);
        }

        private static string EchapperVbs(string s)
        {
            return s.Replace("\"", "\"\"");
        }

        // --- Ligne de commande ----------------------------------------------

        private static Options LireOptions(string[] args)
        {
            Options o = new Options();
            foreach (string a in args)
            {
                string cle = a, val = null;
                int p = a.IndexOf(':');
                if (a.StartsWith("/") && p > 0) { cle = a.Substring(0, p); val = a.Substring(p + 1).Trim('"'); }

                switch (cle.ToLowerInvariant())
                {
                    case "/police": o.Police = Requis(cle, val); break;
                    case "/taille": o.Taille = float.Parse(Requis(cle, val).Replace(',', '.'), CultureInfo.InvariantCulture); break;
                    case "/unite":
                        string u = Requis(cle, val).ToLowerInvariant();
                        if (u == "px") o.Unite = GraphicsUnit.Pixel;
                        else if (u == "pt") o.Unite = GraphicsUnit.Point;
                        else throw new ArgumentException("/unite doit valoir px ou pt");
                        break;
                    case "/gras": o.Style = FontStyle.Bold; break;
                    case "/marge": o.Marge = int.Parse(Requis(cle, val), CultureInfo.InvariantCulture); break;
                    case "/sep": o.Separateur = Requis(cle, val) == "tab" ? '\t' : Requis(cle, val)[0]; break;
                    case "/csv": o.SortieCsv = Requis(cle, val); break;
                    case "/vbs": o.SortieVbs = Requis(cle, val); break;
                    case "/?":
                    case "/aide": throw new ArgumentException("aide demandée");
                    default:
                        if (a.StartsWith("/")) throw new ArgumentException("option inconnue " + a);
                        if (o.Entree != null) throw new ArgumentException("un seul fichier d'entrée attendu");
                        o.Entree = a;
                        break;
                }
            }
            if (o.Entree == null) throw new ArgumentException("fichier d'entrée manquant");
            if (o.SortieCsv == null && o.SortieVbs == null)
                o.SortieVbs = Path.ChangeExtension(o.Entree, ".largeurs.vbs");
            return o;
        }

        private static string Requis(string cle, string val)
        {
            if (string.IsNullOrEmpty(val)) throw new ArgumentException("valeur manquante pour " + cle);
            return val;
        }

        private static void AfficherAide()
        {
            Console.Error.WriteLine("LargeursLibelles <libelles.csv> [options]");
            Console.Error.WriteLine("  /police:\"Segoe UI\"  police utilisée dans Panorama (défaut Segoe UI)");
            Console.Error.WriteLine("  /taille:9           taille de la police (défaut 9)");
            Console.Error.WriteLine("  /unite:pt|px        unité de la taille (défaut pt)");
            Console.Error.WriteLine("  /gras               police en gras");
            Console.Error.WriteLine("  /marge:2            pixels ajoutés à chaque mesure (défaut 2)");
            Console.Error.WriteLine("  /sep:;              séparateur CSV, ou 'tab' (défaut ;)");
            Console.Error.WriteLine("  /csv:sortie.csv     table Cle;Langue;LargeurPx");
            Console.Error.WriteLine("  /vbs:sortie.vbs     fonctions VBScript (défaut <entree>.largeurs.vbs)");
        }
    }
}
