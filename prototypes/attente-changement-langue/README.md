# Animation d'attente — changement de langue (three.js)

Surcouche affichée pendant `PUT /api/session/langue` et le rechargement des libellés
(bloc ② « Langue et formats » de [Ma session](../../docs/03-session.md)).

**Ce qu'on voit :** une carte 3D porte le code de la langue courante (`FR · Français`) et
se retourne d'abord vers la langue cible, puis parcourt les autres langues tant que le
serveur n'a pas répondu. Un globe de points et un anneau des langues disponibles (la
cible en surbrillance) tournent autour. Le message est écrit **dans la langue cible**.
À la fin, la carte se pose sur la cible, une onde verte valide le changement, puis la
surcouche s'efface en fondu.

## Utilisation

```js
import { LanguageSwitchLoader } from './language-switch-loader.js';

const loader = new LanguageSwitchLoader();          // options : container, languages, accent, success

async function changerLangue(cible) {
  loader.start(langueCourante, cible);             // codes : 'FR', 'EN', 'DE', 'NL'…
  try {
    await fetch('/api/session/langue', { method: 'PUT', body: JSON.stringify({ langue: cible }) });
    await rechargerLibelles(cible);                // les libellés changent derrière la surcouche
  } finally {
    await loader.finish();                         // retournement final, onde, fondu
  }
}
```

| Méthode | Rôle |
|---|---|
| `start(from, to)` | Affiche la surcouche et lance l'animation |
| `finish()` | Promesse résolue une fois la carte posée et la surcouche masquée |
| `dispose()` | Libère le contexte WebGL et retire la surcouche |

- `container` : par défaut `document.body` (plein écran) ; sinon la surcouche couvre
  l'élément donné, qui doit être en `position: relative`.
- La boucle de rendu ne tourne que pendant l'affichage.
- `prefers-reduced-motion` est respecté (rotations ralenties, retournements espacés).
- Accessibilité : `role="status"` + `aria-live="polite"`, le message est lu dans la langue cible.

## Démo

`index.html` : sélecteur de langue et latence serveur simulée réglable.
Les modules ES ne se chargent pas en `file://` : servir le dossier, par exemple
`python -m http.server` puis http://localhost:8000.

## Poste sans accès Internet

L'importmap pointe sur le CDN jsDelivr (`three@0.170.0`). Pour un poste d'exploitation
isolé, copier `three.module.js` (≈ 1,3 Mo, ou `three.module.min.js` ≈ 690 Ko) à côté du
module et modifier la ligne de l'importmap.
