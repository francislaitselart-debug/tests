// ============================================================
//  Time Tracker — Google Apps Script
//  Tableau de bord publié en page web Google
//  Calendrier : Francis PRO
//  Mots-clés suivis : CODRA, TRANSPORTS, SPORTS (ventilés séparément)
//  Fenêtre : 4 dernières semaines complètes
// ============================================================

// ---------- CONFIGURATION -----------------------------------
var CALENDAR_ID = "francis.laitselart@gmail.com"; // ID du calendrier Francis PRO
var KEYWORDS    = ["CODRA", "TRANSPORTS", "SPORTS"]; // Mots-clés à suivre (ordre = ordre d'affichage)
var NB_SEMAINES = 4;                               // Nombre de semaines à analyser

// Objectif hebdomadaire par mot-clé (en minutes) : l'écart est affiché sous le badge de la semaine.
// Ajoutez d'autres mots-clés ici si besoin, ex. "TRANSPORTS": 8 * 60
var OBJECTIFS_HEBDO_MIN = {
  "CODRA" : 37.5 * 60   // 37h30
};

// Couleurs de l'écart
var COULEUR_ECART_PLUS  = "#d93025"; // au-dessus de l'objectif
var COULEUR_ECART_MOINS = "#e37400"; // en dessous de l'objectif
var COULEUR_ECART_ZERO  = "#188038"; // objectif atteint pile

// Couleurs associées à chaque mot-clé (même ordre que KEYWORDS)
var COLORS = {
  "CODRA"      : { header: "#1a73e8", light: "#e8f0fe", border: "#c5d5fb", text: "#1a73e8" },
  "TRANSPORTS" : { header: "#188038", light: "#e6f4ea", border: "#b7dfbf", text: "#137333" },
  "SPORTS"     : { header: "#e37400", light: "#fef3e2", border: "#f9c58d", text: "#b06000" }
};
// ------------------------------------------------------------


// Point d'entrée de la web app (GET request)
function doGet() {
  var html = buildHtml();
  return HtmlService.createHtmlOutput(html)
    .setTitle("Time Tracker — Francis PRO")
    .setXFrameOptionsMode(HtmlService.XFrameOptionsMode.ALLOWALL);
}


// Construit le HTML complet du tableau de bord
function buildHtml() {
  var data   = getData();
  var now    = new Date();
  var update = Utilities.formatDate(now, "Europe/Paris", "dd/MM/yyyy 'à' HH:mm");

  // --- Métriques globales ---
  var grandTotalMin = 0;
  var grandNbEvents = 0;
  KEYWORDS.forEach(function(kw) {
    grandTotalMin += data.totauxParKeyword[kw].totalMin;
    grandNbEvents += data.totauxParKeyword[kw].nbEvents;
  });
  var moy = data.semaines.length > 0
    ? formatDuree(Math.round(grandTotalMin / data.semaines.length))
    : "—";

  // --- Cartes métriques par keyword ---
  var metricCards = "";
  KEYWORDS.forEach(function(kw) {
    var col = COLORS[kw] || COLORS["CODRA"];
    metricCards += "<div class='metric' style='border-top:3px solid " + col.header + "'>"
      + "<p class='metric-label'>" + kw + " — total période</p>"
      + "<p class='metric-value' style='color:" + col.text + "'>"
      + formatDuree(data.totauxParKeyword[kw].totalMin) + "</p>"
      + "<p class='metric-sub'>" + data.totauxParKeyword[kw].nbEvents + " événement(s)</p>"
      + "</div>";
  });

  // --- Sections par semaine (collapsibles, masquées par défaut) ---
  var sections = "";
  data.semaines.forEach(function(semaine, sIdx) {
    var sId = "s" + sIdx;

    // Badges résumé par keyword dans l'en-tête semaine
    var totauxSpans = "";
    KEYWORDS.forEach(function(kw) {
      var col = COLORS[kw] || COLORS["CODRA"];
      var min = totalMinList(semaine.parKeyword[kw] || []);
      var badge = "<span class='kw-badge' style='background:" + col.light + ";color:" + col.text + ";border:1px solid " + col.border + "'>"
        + kw + " " + formatDuree(min) + "</span>";

      // Écart avec l'objectif hebdo, affiché sous le badge
      if (OBJECTIFS_HEBDO_MIN.hasOwnProperty(kw)) {
        var objectif = OBJECTIFS_HEBDO_MIN[kw];
        var ecart    = min - objectif;
        var couleur  = ecart > 0 ? COULEUR_ECART_PLUS : (ecart < 0 ? COULEUR_ECART_MOINS : COULEUR_ECART_ZERO);
        totauxSpans += "<span class='kw-col'>" + badge
          + "<span class='ecart' style='color:" + couleur + "' title='Objectif : " + formatDuree(objectif) + "'>"
          + "écart " + formatEcart(ecart) + " / " + formatDuree(objectif)
          + "</span></span>";
      } else {
        totauxSpans += badge;
      }
    });

    // En-tête semaine cliquable
    sections += "<div class='semaine-block'>"
      + "<div class='semaine-header' onclick=\"toggle('" + sId + "')\">"
      + "  <span class='toggle-icon' id='icon-" + sId + "'>&#9654;</span>"
      + "  <span class='semaine-title'>S" + semaine.numeroSemaine
      + " &nbsp;<span class='semaine-dates'>" + semaine.debutStr + " → " + semaine.finStr + "</span></span>"
      + "  <span class='semaine-total'>" + formatDuree(semaine.grandTotalMin) + "</span>"
      + "  <span class='kw-badges'>" + totauxSpans + "</span>"
      + "</div>";

    // Contenu semaine — masqué par défaut
    sections += "<div class='semaine-content' id='" + sId + "' style='display:none'>";

    KEYWORDS.forEach(function(kw, kIdx) {
      var col    = COLORS[kw] || COLORS["CODRA"];
      var evList = semaine.parKeyword[kw] || [];
      var kwId   = sId + "k" + kIdx;

      // Sous-en-tête keyword cliquable
      sections += "<div class='kw-block' style='border-left:3px solid " + col.header + "'>"
        + "<div class='kw-header' onclick=\"toggle('" + kwId + "')\" style='background:" + col.light + "'>"
        + "  <span class='toggle-icon' id='icon-" + kwId + "'>&#9654;</span>"
        + "  <span style='color:" + col.text + ";font-weight:600'>" + kw + "</span>"
        + "  <span class='kw-total-badge' style='color:" + col.text + "'>"
        + formatDuree(totalMinList(evList)) + " &nbsp;·&nbsp; " + evList.length + " év.</span>"
        + "</div>";

      // Contenu keyword — masqué par défaut
      sections += "<div class='kw-content' id='" + kwId + "' style='display:none'>";

      if (evList.length === 0) {
        sections += "<div class='ev-empty'>Aucun événement</div>";
      } else {
        sections += "<table class='ev-table'><tbody>";
        evList.forEach(function(ev) {
          sections += "<tr>"
            + "<td class='ev-title'>" + htmlEscape(ev.title) + "</td>"
            + "<td class='ev-date'>" + ev.dateStr + "</td>"
            + "<td class='ev-time'>" + ev.heureDebut + " – " + ev.heureFin + "</td>"
            + "<td class='ev-dur' style='color:" + col.text + "'>" + formatDuree(ev.dureeMin) + "</td>"
            + "</tr>";
        });
        sections += "</tbody></table>";
      }

      sections += "</div></div>"; // kw-content + kw-block
    });

    sections += "</div></div>"; // semaine-content + semaine-block
  });

  // --- Pied totaux ---
  var footerTotaux = "<div class='footer-totaux'>"
    + "<span class='footer-total-label'>Total général (" + NB_SEMAINES + " sem.) : <strong>" + formatDuree(grandTotalMin) + "</strong></span>";
  KEYWORDS.forEach(function(kw) {
    var col = COLORS[kw] || COLORS["CODRA"];
    footerTotaux += "<span class='kw-badge' style='background:" + col.light + ";color:" + col.text + ";border:1px solid " + col.border + "'>"
      + kw + " " + formatDuree(data.totauxParKeyword[kw].totalMin) + "</span>";
  });
  footerTotaux += "</div>";

  // --- JS toggle ---
  var js = "<script>"
    + "function toggle(id){"
    + "  var el=document.getElementById(id);"
    + "  var ic=document.getElementById('icon-'+id);"
    + "  var open=el.style.display==='none';"
    + "  el.style.display=open?'block':'none';"
    + "  ic.innerHTML=open?'&#9660;':'&#9654;';"
    + "}"
    + "function expandAll(){"
    + "  document.querySelectorAll('.semaine-content,.kw-content').forEach(function(e){e.style.display='block';});"
    + "  document.querySelectorAll('.toggle-icon').forEach(function(i){i.innerHTML='&#9660;';});"
    + "}"
    + "function collapseAll(){"
    + "  document.querySelectorAll('.semaine-content,.kw-content').forEach(function(e){e.style.display='none';});"
    + "  document.querySelectorAll('.toggle-icon').forEach(function(i){i.innerHTML='&#9654;';});"
    + "}"
    + "<\/script>";

  // --- CSS ---
  var css = "<style>"
    + "body{font-family:Google Sans,Arial,sans-serif;background:#f8f9fa;margin:0;padding:24px;color:#202124}"
    + "h1{font-size:22px;font-weight:500;margin:0 0 4px}"
    + ".subtitle{font-size:13px;color:#5f6368;margin:0 0 20px}"
    + ".metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin-bottom:20px}"
    + ".metric{background:#fff;border:1px solid #e0e0e0;border-radius:12px;padding:14px 16px}"
    + ".metric-global{border-top:3px solid #5f6368}"
    + ".metric-label{font-size:12px;color:#5f6368;margin:0 0 4px;font-weight:500}"
    + ".metric-value{font-size:22px;font-weight:500;margin:0}"
    + ".metric-sub{font-size:11px;color:#9aa0a6;margin:4px 0 0}"
    + ".toolbar{display:flex;gap:8px;margin-bottom:14px}"
    + ".btn{font-size:12px;padding:5px 14px;border:1px solid #dadce0;border-radius:6px;background:#fff;cursor:pointer;color:#3c4043}"
    + ".btn:hover{background:#f1f3f4}"
    + ".semaine-block{background:#fff;border-radius:10px;box-shadow:0 1px 3px rgba(0,0,0,.1);margin-bottom:10px;overflow:hidden}"
    + ".semaine-header{display:flex;align-items:center;gap:10px;padding:12px 16px;cursor:pointer;user-select:none;background:#f1f3f4}"
    + ".semaine-header:hover{background:#e8eaed}"
    + ".toggle-icon{font-size:10px;color:#5f6368;width:14px;flex-shrink:0}"
    + ".semaine-title{font-size:14px;font-weight:500;flex-shrink:0}"
    + ".semaine-dates{font-size:12px;font-weight:400;color:#5f6368}"
    + ".semaine-total{font-size:14px;font-weight:600;color:#202124;flex-shrink:0}"
    + ".kw-badges{display:flex;flex-wrap:wrap;align-items:flex-start;gap:6px;margin-left:auto}"
    + ".kw-badge{font-size:11px;padding:2px 8px;border-radius:12px;font-weight:500;white-space:nowrap}"
    + ".kw-col{display:inline-flex;flex-direction:column;align-items:center}"
    + ".ecart{font-size:10px;font-weight:600;margin-top:3px;white-space:nowrap}"
    + ".semaine-content{padding:8px 12px 12px}"
    + ".kw-block{border-radius:6px;margin-bottom:6px;overflow:hidden;border:1px solid #e8eaed}"
    + ".kw-header{display:flex;align-items:center;gap:8px;padding:8px 12px;cursor:pointer;user-select:none}"
    + ".kw-header:hover{filter:brightness(.96)}"
    + ".kw-total-badge{margin-left:auto;font-size:12px;font-weight:500}"
    + ".kw-content{padding:0 0 4px}"
    + ".ev-empty{font-size:12px;color:#9aa0a6;font-style:italic;padding:8px 14px}"
    + ".ev-table{width:100%;border-collapse:collapse}"
    + ".ev-table tr{border-top:1px solid #f1f3f4}"
    + ".ev-table td{font-size:12px;padding:6px 14px;color:#3c4043}"
    + ".ev-title{font-weight:500;color:#202124}"
    + ".ev-date,.ev-time{color:#5f6368;white-space:nowrap}"
    + ".ev-dur{text-align:right;font-weight:500;white-space:nowrap}"
    + ".footer-totaux{background:#fff;border-radius:10px;box-shadow:0 1px 3px rgba(0,0,0,.1);padding:12px 16px;margin-top:10px;display:flex;align-items:center;flex-wrap:wrap;gap:8px}"
    + ".footer-total-label{font-size:13px;color:#202124;margin-right:4px}"
    + ".footer{font-size:11px;color:#9aa0a6;margin-top:10px;text-align:right}"
    + "</style>";

  return "<!DOCTYPE html><html lang='fr'><head><meta charset='UTF-8'>"
    + "<meta name='viewport' content='width=device-width, initial-scale=1'>"
    + "<title>Time Tracker — Francis PRO</title>"
    + css + "</head><body>" + js

    + "<h1>Time Tracker — Francis PRO</h1>"
    + "<p class='subtitle'>Calendrier : Francis PRO &nbsp;·&nbsp; Mots-clés : "
    + KEYWORDS.join(", ")
    + " &nbsp;·&nbsp; " + NB_SEMAINES + " dernières semaines</p>"

    + "<div class='metrics'>"
    + "  <div class='metric metric-global'>"
    + "    <p class='metric-label'>Total général période</p>"
    + "    <p class='metric-value' style='color:#3c4043'>" + formatDuree(grandTotalMin) + "</p>"
    + "    <p class='metric-sub'>Moy. " + moy + " / semaine &nbsp;·&nbsp; " + grandNbEvents + " événements</p>"
    + "  </div>"
    + metricCards
    + "</div>"

    + "<div class='toolbar'>"
    + "  <button class='btn' onclick='expandAll()'>&#9660; Tout développer</button>"
    + "  <button class='btn' onclick='collapseAll()'>&#9654; Tout réduire</button>"
    + "</div>"

    + sections
    + footerTotaux

    + "<p class='footer'>Dernière actualisation : " + update
    + " &nbsp;·&nbsp; Actualisez la page pour mettre à jour.</p>"
    + "</body></html>";
}


// Somme les durées d'une liste d'événements
function totalMinList(evList) {
  var t = 0;
  evList.forEach(function(ev) { t += ev.dureeMin; });
  return t;
}


// Récupère et structure les données depuis Google Calendar
function getData() {
  var cal = CalendarApp.getCalendarById(CALENDAR_ID);
  if (!cal) throw new Error("Calendrier introuvable : " + CALENDAR_ID);

  var semaines = [];
  var totauxParKeyword = {};
  KEYWORDS.forEach(function(kw) {
    totauxParKeyword[kw] = { totalMin: 0, nbEvents: 0 };
  });

  var today = new Date();
  var jourSemaine  = today.getDay();
  var diffLundi    = (jourSemaine === 0) ? -6 : 1 - jourSemaine;
  var lundiCourant = new Date(today);
  lundiCourant.setHours(0, 0, 0, 0);
  lundiCourant.setDate(lundiCourant.getDate() + diffLundi);

  for (var s = NB_SEMAINES - 1; s >= 0; s--) {
    var debut = new Date(lundiCourant);
    debut.setDate(debut.getDate() - s * 7);

    var fin = new Date(debut);
    fin.setDate(fin.getDate() + 7);
    fin.setMilliseconds(fin.getMilliseconds() - 1);

    var allEvents   = cal.getEvents(debut, fin);
    var parKeyword  = {};
    var grandTotalMin = 0;

    KEYWORDS.forEach(function(kw) { parKeyword[kw] = []; });

    allEvents.forEach(function(ev) {
      var titre     = ev.getTitle();
      var titreUp   = titre.toUpperCase();
      var start     = ev.getStartTime();
      var end       = ev.getEndTime();
      var dureeMin  = Math.round((end - start) / 60000);

      KEYWORDS.forEach(function(kw) {
        // Recherche mot entier pour éviter ex. "SPORTS" dans "TRANSPORTS"
        var regex = new RegExp("(^|[^A-Z])" + kw.toUpperCase() + "([^A-Z]|$)");
        if (regex.test(titreUp)) {
          parKeyword[kw].push({
            title     : titre,
            dateStr   : Utilities.formatDate(start, "Europe/Paris", "EEE dd/MM"),
            heureDebut: Utilities.formatDate(start, "Europe/Paris", "HH:mm"),
            heureFin  : Utilities.formatDate(end,   "Europe/Paris", "HH:mm"),
            dureeMin  : dureeMin
          });
          grandTotalMin              += dureeMin;
          totauxParKeyword[kw].totalMin += dureeMin;
          totauxParKeyword[kw].nbEvents += 1;
        }
      });
    });

    semaines.push({
      numeroSemaine : getNumeroSemaine(debut),
      debutStr      : Utilities.formatDate(debut, "Europe/Paris", "dd/MM/yyyy"),
      finStr        : Utilities.formatDate(fin,   "Europe/Paris", "dd/MM/yyyy"),
      grandTotalMin : grandTotalMin,
      parKeyword    : parKeyword
    });
  }

  return { semaines: semaines, totauxParKeyword: totauxParKeyword };
}


// Calcule le numéro de semaine ISO 8601
function getNumeroSemaine(date) {
  var d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  var dayNum = d.getUTCDay() || 7;
  d.setUTCDate(d.getUTCDate() + 4 - dayNum);
  var yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
}


// Formate une durée en minutes → "XhYY" ou "Ymin"
function formatDuree(totalMin) {
  if (totalMin === 0) return "0min";
  var h = Math.floor(totalMin / 60);
  var m = totalMin % 60;
  if (h === 0) return m + "min";
  return h + "h" + (m > 0 ? (m < 10 ? "0" + m : "" + m) : "");
}


// Formate un écart signé en minutes → "+5h30", "−1h30", "±0"
function formatEcart(ecartMin) {
  if (ecartMin === 0) return "±0";
  var signe = ecartMin > 0 ? "+" : "−";
  return signe + formatDuree(Math.abs(ecartMin));
}


// Échappe les caractères HTML
function htmlEscape(str) {
  return str.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;");
}
