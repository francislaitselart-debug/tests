// ============================================================
//  Time Tracker — Google Apps Script
//  Tableau de bord publié en page web Google
//  Calendrier : Francis PRO
//  Catégories suivies : CODRA, TRANSPORTS, SPORTS, CONGÉS/FÉRIÉS (ventilées séparément)
//  Fenêtre : 4 dernières semaines complètes
// ============================================================

// ---------- CONFIGURATION -----------------------------------
var CALENDAR_ID = "francis.laitselart@gmail.com"; // ID du calendrier Francis PRO
var KEYWORDS    = ["CODRA", "TRANSPORTS", "SPORTS", "CONGÉS/FÉRIÉS"]; // Catégories à suivre (ordre = ordre d'affichage)
var NB_SEMAINES = 4;                               // Nombre de semaines à analyser

// Termes recherchés dans le titre pour chaque catégorie.
// Comparaison sans tenir compte des majuscules ni des accents (CONGÉ = Congé = conge).
// Une catégorie absente de cette table est recherchée par son propre nom.
var TERMES = {
  "CONGÉS/FÉRIÉS" : ["CONGE", "CONGES", "FERIE", "FERIES", "FERIEE", "FERIEES"]
};

// Durée comptée pour un événement « journée entière », par jour ouvré (lun–ven).
// Les samedis et dimanches couverts par l'événement ne sont pas comptés.
var DUREE_JOUR_MIN = 7.5 * 60; // 7h30 = 37h30 / 5

// Objectif hebdomadaire par mot-clé (en minutes) : l'écart est affiché sous le badge de la semaine.
// Ajoutez d'autres mots-clés ici si besoin, ex. "TRANSPORTS": 8 * 60
var OBJECTIFS_HEBDO_MIN = {
  "CODRA" : 37.5 * 60   // 37h30
};

// Catégories ajoutées au temps d'une catégorie pour calculer son écart.
// Ici : écart CODRA = (CODRA + CONGÉS/FÉRIÉS) − 37h30
var AJOUTS_ECART = {
  "CODRA" : ["CONGÉS/FÉRIÉS"]
};

// Couleurs de l'écart
var COULEUR_ECART_PLUS  = "#d93025"; // au-dessus de l'objectif
var COULEUR_ECART_MOINS = "#e37400"; // en dessous de l'objectif
var COULEUR_ECART_ZERO  = "#188038"; // objectif atteint pile

// Couleurs associées à chaque mot-clé (même ordre que KEYWORDS)
var COLORS = {
  "CODRA"      : { header: "#1a73e8", light: "#e8f0fe", border: "#c5d5fb", text: "#1a73e8" },
  "TRANSPORTS" : { header: "#188038", light: "#e6f4ea", border: "#b7dfbf", text: "#137333" },
  "SPORTS"     : { header: "#e37400", light: "#fef3e2", border: "#f9c58d", text: "#b06000" },
  "CONGÉS/FÉRIÉS" : { header: "#8430ce", light: "#f3e8fd", border: "#d7aefb", text: "#8430ce" }
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

        // Ajoute les catégories comptées avec celle-ci (ex. congés pour CODRA)
        var ajoutMin = 0;
        var detail   = kw + " " + formatDuree(min);
        (AJOUTS_ECART[kw] || []).forEach(function(kwAjout) {
          var m = totalMinList(semaine.parKeyword[kwAjout] || []);
          ajoutMin += m;
          detail   += " + " + kwAjout + " " + formatDuree(m);
        });

        var ecart    = min + ajoutMin - objectif;
        var couleur  = ecart > 0 ? COULEUR_ECART_PLUS : (ecart < 0 ? COULEUR_ECART_MOINS : COULEUR_ECART_ZERO);
        var mention  = ajoutMin > 0 ? " (congés " + formatDuree(ajoutMin) + " inclus)" : "";
        totauxSpans += "<span class='kw-col'>" + badge
          + "<span class='ecart' style='color:" + couleur + "' title='" + htmlEscape(detail)
          + " − objectif " + formatDuree(objectif) + "'>"
          + "écart " + formatEcart(ecart) + " / " + formatDuree(objectif) + mention
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

  // Une expression régulière par catégorie, compilée une seule fois.
  // Recherche en mot entier pour éviter ex. "SPORTS" dans "TRANSPORTS".
  var REGEX_PAR_KEYWORD = {};
  KEYWORDS.forEach(function(kw) {
    var termes = (TERMES[kw] || [kw]).map(normaliser);
    REGEX_PAR_KEYWORD[kw] = new RegExp("(^|[^A-Z0-9])(" + termes.join("|") + ")([^A-Z0-9]|$)");
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
      var titre    = ev.getTitle();
      var titreNorm = normaliser(titre);
      var mesure   = mesurerEvenement(ev, debut, fin);

      KEYWORDS.forEach(function(kw) {
        if (REGEX_PAR_KEYWORD[kw].test(titreNorm)) {
          parKeyword[kw].push({
            title     : titre,
            dateStr   : Utilities.formatDate(mesure.premierJour, "Europe/Paris", "EEE dd/MM"),
            heureDebut: mesure.heureDebut,
            heureFin  : mesure.heureFin,
            dureeMin  : mesure.dureeMin
          });
          grandTotalMin              += mesure.dureeMin;
          totauxParKeyword[kw].totalMin += mesure.dureeMin;
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


// Met en majuscules et retire les accents : "Congés fériés" -> "CONGES FERIES"
function normaliser(str) {
  return String(str).normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase();
}


// Durée et horaires d'un événement, limités à la semaine [debut, fin].
// Un événement « journée entière » compte DUREE_JOUR_MIN par jour ouvré couvert.
function mesurerEvenement(ev, debut, fin) {
  if (!ev.isAllDayEvent()) {
    var start = ev.getStartTime();
    var end   = ev.getEndTime();
    return {
      premierJour: start,
      heureDebut : Utilities.formatDate(start, "Europe/Paris", "HH:mm"),
      heureFin   : Utilities.formatDate(end,   "Europe/Paris", "HH:mm"),
      dureeMin   : Math.round((end - start) / 60000)
    };
  }

  // Dates de journée entière : fin exclusive (un congé du lundi a pour fin le mardi 00:00)
  var jour   = new Date(Math.max(ev.getAllDayStartDate().getTime(), debut.getTime()));
  var finExc = new Date(Math.min(ev.getAllDayEndDate().getTime(), fin.getTime() + 1));
  jour.setHours(0, 0, 0, 0);
  var premierJour = new Date(jour);
  var nbJours = 0;
  while (jour < finExc) {
    var j = jour.getDay();
    if (j !== 0 && j !== 6) nbJours++;
    jour.setDate(jour.getDate() + 1);
  }
  return {
    premierJour: premierJour,
    heureDebut : "Journ\u00e9e",
    heureFin   : nbJours + " j ouvr\u00e9" + (nbJours > 1 ? "s" : ""),
    dureeMin   : nbJours * DUREE_JOUR_MIN
  };
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
