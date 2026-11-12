/**
 * LanguageSwitchLoader — animation d'attente three.js pour le changement de langue.
 *
 * Scène : une carte 3D affiche le code de la langue courante et se retourne
 * (effet « split-flap ») pendant l'attente ; un globe de points et un anneau
 * des langues disponibles tournent autour. À la fin, la carte se pose sur la
 * langue cible, une onde de validation part, puis la surcouche s'efface.
 *
 * Usage :
 *   const loader = new LanguageSwitchLoader();
 *   loader.start('FR', 'EN');
 *   await appliquerLangue('en');   // PUT /api/session/langue + rechargement des libellés
 *   await loader.finish();         // pose la carte sur EN, onde verte, fondu
 *
 * Dépendance : three (ES module). Aucune autre.
 */
import * as THREE from 'three';

export const DEFAULT_LANGUAGES = [
  { code: 'FR', name: 'Français',   msg: 'Changement de langue' },
  { code: 'EN', name: 'English',    msg: 'Switching language' },
  { code: 'DE', name: 'Deutsch',    msg: 'Sprache wird gewechselt' },
  { code: 'NL', name: 'Nederlands', msg: 'Taal wordt gewijzigd' },
  { code: 'ES', name: 'Español',    msg: 'Cambiando de idioma' },
  { code: 'IT', name: 'Italiano',   msg: 'Cambio lingua in corso' },
];

const FLIP_DURATION = 0.55;   // s — durée d'un retournement
const FLIP_INTERVAL = 0.35;   // s — pause entre deux retournements
const FADE_MS = 350;

const easeInOutCubic = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);
const easeOutCubic = (t) => 1 - Math.pow(1 - t, 3);

export class LanguageSwitchLoader {
  /**
   * @param {object} [options]
   * @param {HTMLElement} [options.container=document.body] parent de la surcouche
   * @param {Array<{code:string,name:string,msg:string}>} [options.languages]
   * @param {string} [options.accent='#3fa9f5'] couleur principale
   * @param {string} [options.success='#4cd97b'] couleur de validation
   */
  constructor(options = {}) {
    this.container = options.container || document.body;
    this.languages = options.languages || DEFAULT_LANGUAGES;
    this.accent = options.accent || '#3fa9f5';
    this.successColor = options.success || '#4cd97b';
    this.reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

    this._cardTextures = new Map();
    this._running = false;
    this._clock = new THREE.Clock(false);

    this._buildDom();
    this._buildScene();
  }

  // ───────────────────────────────────────────── API publique

  /** Affiche la surcouche et lance l'animation de `from` vers `to` (codes langue). */
  start(from, to) {
    this._session = (this._session || 0) + 1;
    this.from = this._lang(from);
    this.to = this._lang(to);
    this._finishing = false;
    this._settled = false;
    this._resolveFinish = null;

    this.title.textContent = this.to.msg;
    this.subtitle.textContent = `${this.from.name}  →  ${this.to.name}`;
    this.overlay.setAttribute('aria-label', this.to.msg);

    // Carte : face avant = langue de départ
    this._frontVisible = true;
    this._showing = this.from.code;
    this.card.rotation.set(0, 0, 0);
    this._setFace(true, this.from);
    this._flip = null;
    this._pulse = null;
    this.pulseMesh.visible = false;
    this._highlight(this.to.code);

    this._clock.start();
    this._nextFlipAt = 0.45;

    this.overlay.style.display = 'flex';
    requestAnimationFrame(() => this.overlay.classList.add('lsl-visible'));
    this._resize();
    if (!this._running) {
      this._running = true;
      this.renderer.setAnimationLoop(() => this._tick());
    }
  }

  /** Termine l'animation : pose la carte sur la langue cible puis masque la surcouche. */
  finish() {
    if (!this._running) return Promise.resolve();
    this._finishing = true;
    if (!this._flip) this._nextFlipAt = Math.min(this._nextFlipAt, this._clock.getElapsedTime());
    return new Promise((resolve) => { this._resolveFinish = resolve; });
  }

  /** Libère toutes les ressources WebGL et retire la surcouche du DOM. */
  dispose() {
    this.renderer.setAnimationLoop(null);
    this._running = false;
    this._ro?.disconnect();
    this.scene.traverse((o) => {
      o.geometry?.dispose();
      const mats = Array.isArray(o.material) ? o.material : o.material ? [o.material] : [];
      mats.forEach((m) => { m.map?.dispose(); m.dispose(); });
    });
    this._cardTextures.forEach((t) => t.dispose());
    this.renderer.dispose();
    this.overlay.remove();
  }

  // ───────────────────────────────────────────── DOM

  _buildDom() {
    if (!document.getElementById('lsl-style')) {
      const style = document.createElement('style');
      style.id = 'lsl-style';
      style.textContent = `
        .lsl-overlay{position:fixed;inset:0;z-index:9999;display:none;flex-direction:column;
          align-items:center;justify-content:center;gap:4px;background:rgba(8,13,24,.72);
          backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px);opacity:0;
          transition:opacity ${FADE_MS}ms ease;font-family:system-ui,"Segoe UI",Roboto,sans-serif}
        .lsl-overlay.lsl-local{position:absolute}
        .lsl-overlay.lsl-visible{opacity:1}
        .lsl-stage{width:min(78vw,360px);aspect-ratio:1}
        .lsl-stage canvas{width:100%;height:100%;display:block}
        .lsl-title{color:#eef3fb;font-size:18px;font-weight:600;letter-spacing:.2px;margin-top:-8px}
        .lsl-title::after{content:"";display:inline-block;width:1.2em;text-align:left;
          animation:lsl-dots 1.2s steps(4,end) infinite}
        .lsl-sub{color:#93a4bf;font-size:13px}
        @keyframes lsl-dots{0%{content:""}25%{content:"."}50%{content:".."}75%{content:"..."}}
        @media (prefers-reduced-motion: reduce){.lsl-title::after{animation:none;content:"…"}}
      `;
      document.head.appendChild(style);
    }
    this.overlay = document.createElement('div');
    this.overlay.className = 'lsl-overlay' + (this.container === document.body ? '' : ' lsl-local');
    this.overlay.setAttribute('role', 'status');
    this.overlay.setAttribute('aria-live', 'polite');
    this.stage = document.createElement('div');
    this.stage.className = 'lsl-stage';
    this.title = document.createElement('div');
    this.title.className = 'lsl-title';
    this.subtitle = document.createElement('div');
    this.subtitle.className = 'lsl-sub';
    this.overlay.append(this.stage, this.title, this.subtitle);
    this.container.appendChild(this.overlay);
  }

  // ───────────────────────────────────────────── Scène three.js

  _buildScene() {
    const accent = new THREE.Color(this.accent);

    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setClearColor(0x000000, 0);
    this.stage.appendChild(this.renderer.domElement);

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(38, 1, 0.1, 50);
    this.camera.position.set(0, 0.4, 8.4);
    this.camera.lookAt(0, 0, 0);

    // Globe de points (sphère de Fibonacci)
    const N = 700, R = 1.95, pos = new Float32Array(N * 3);
    const golden = Math.PI * (3 - Math.sqrt(5));
    for (let i = 0; i < N; i++) {
      const y = 1 - (i / (N - 1)) * 2, r = Math.sqrt(1 - y * y), th = golden * i;
      pos.set([Math.cos(th) * r * R, y * R, Math.sin(th) * r * R], i * 3);
    }
    const gGeo = new THREE.BufferGeometry();
    gGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    this.globe = new THREE.Group();
    this.globe.add(new THREE.Points(gGeo, new THREE.PointsMaterial({
      color: accent, size: 0.035, transparent: true, opacity: 0.55,
      depthWrite: false, blending: THREE.AdditiveBlending,
    })));
    // Méridiens / équateur
    const circle = new THREE.BufferGeometry().setFromPoints(
      new THREE.EllipseCurve(0, 0, R, R, 0, Math.PI * 2).getPoints(96));
    const lineMat = new THREE.LineBasicMaterial({ color: accent, transparent: true, opacity: 0.18 });
    [[Math.PI / 2, 0], [0, 0], [0, Math.PI / 3], [0, -Math.PI / 3]].forEach(([rx, ry]) => {
      const l = new THREE.LineLoop(circle, lineMat);
      l.rotation.set(rx, ry, 0);
      this.globe.add(l);
    });
    this.globe.rotation.z = 0.35;
    this.scene.add(this.globe);

    // Anneau des langues disponibles
    this.orbit = new THREE.Group();
    this.orbit.rotation.set(0.32, 0, -0.18);
    this.chips = new Map();
    const ringR = 2.4;
    this.languages.forEach((lang, i) => {
      const a = (i / this.languages.length) * Math.PI * 2;
      const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
        map: this._chipTexture(lang.code), transparent: true, depthWrite: false,
      }));
      sprite.position.set(Math.cos(a) * ringR, 0, Math.sin(a) * ringR);
      sprite.scale.set(0.62, 0.31, 1);
      this.orbit.add(sprite);
      this.chips.set(lang.code, sprite);
    });
    const ring = new THREE.LineLoop(
      new THREE.BufferGeometry().setFromPoints(
        new THREE.EllipseCurve(0, 0, ringR, ringR, 0, Math.PI * 2).getPoints(128)),
      new THREE.LineBasicMaterial({ color: accent, transparent: true, opacity: 0.3 }));
    ring.rotation.x = Math.PI / 2;
    this.orbit.add(ring);
    this.scene.add(this.orbit);

    // Carte centrale (faces +z / -z texturées)
    const edge = new THREE.MeshBasicMaterial({ color: accent.clone().multiplyScalar(0.55) });
    this.frontMat = new THREE.MeshBasicMaterial();
    this.backMat = new THREE.MeshBasicMaterial();
    this.card = new THREE.Mesh(new THREE.BoxGeometry(1.75, 1.1, 0.06),
      [edge, edge, edge, edge, this.frontMat, this.backMat]);
    this.cardHolder = new THREE.Group();
    this.cardHolder.add(this.card);
    this.scene.add(this.cardHolder);

    // Onde de validation
    this.pulseMesh = new THREE.Mesh(
      new THREE.RingGeometry(0.98, 1.0, 96),
      new THREE.MeshBasicMaterial({ color: this.successColor, transparent: true, depthWrite: false }));
    this.pulseMesh.visible = false;
    this.scene.add(this.pulseMesh);

    this._ro = new ResizeObserver(() => this._resize());
    this._ro.observe(this.stage);
  }

  _resize() {
    const w = this.stage.clientWidth, h = this.stage.clientHeight;
    if (!w || !h) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  // ───────────────────────────────────────────── Textures (canvas 2D)

  _cardTexture(lang) {
    if (this._cardTextures.has(lang.code)) return this._cardTextures.get(lang.code);
    const c = document.createElement('canvas');
    c.width = 512; c.height = 322;
    const g = c.getContext('2d');
    const grd = g.createLinearGradient(0, 0, 512, 322);
    grd.addColorStop(0, '#1f2d4a');
    grd.addColorStop(1, '#0d1524');
    g.fillStyle = grd;
    g.fillRect(0, 0, 512, 322);
    g.strokeStyle = this.accent;
    g.lineWidth = 8;
    g.strokeRect(14, 14, 484, 294);
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillStyle = '#ffffff';
    g.font = '700 150px system-ui, "Segoe UI", Roboto, sans-serif';
    g.fillText(lang.code, 256, 140);
    g.fillStyle = this.accent;
    g.font = '500 44px system-ui, "Segoe UI", Roboto, sans-serif';
    g.fillText(lang.name, 256, 252);
    const tex = new THREE.CanvasTexture(c);
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = this.renderer.capabilities.getMaxAnisotropy();
    this._cardTextures.set(lang.code, tex);
    return tex;
  }

  _chipTexture(code) {
    const c = document.createElement('canvas');
    c.width = 256; c.height = 128;
    const g = c.getContext('2d');
    g.fillStyle = 'rgba(14,22,38,0.92)';
    g.strokeStyle = this.accent;
    g.lineWidth = 6;
    g.beginPath();
    g.roundRect(8, 8, 240, 112, 56);
    g.fill();
    g.stroke();
    g.fillStyle = '#e6eefb';
    g.font = '700 64px system-ui, "Segoe UI", Roboto, sans-serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(code, 128, 68);
    const tex = new THREE.CanvasTexture(c);
    tex.colorSpace = THREE.SRGBColorSpace;
    return tex;
  }

  // ───────────────────────────────────────────── Logique d'animation

  _lang(code) {
    const c = String(code).toUpperCase();
    return this.languages.find((l) => l.code === c) || { code: c, name: c, msg: '…' };
  }

  _setFace(front, lang) {
    const mat = front ? this.frontMat : this.backMat;
    mat.map = this._cardTexture(lang);
    mat.needsUpdate = true;
  }

  _highlight(code) {
    this.chips.forEach((s, c) => {
      const on = c === code;
      s.material.opacity = on ? 1 : 0.45;
      s.userData.base = on ? 0.82 : 0.62;
    });
  }

  _startFlip(lang, t) {
    // On écrit la langue suivante sur la face cachée, puis on tourne de 180°.
    this._setFace(!this._frontVisible, lang);
    const y = this.card.rotation.y;
    this._flip = { t0: t, from: y, to: y + Math.PI, lang };
  }

  _pickShuffle() {
    const pool = this.languages.filter((l) => l.code !== this._showing && l.code !== this.to.code);
    // Le premier retournement montre toujours la cible : c'est le sens de l'animation.
    if (this._showing === this.from.code || !pool.length) return this.to;
    return Math.random() < 0.4 ? this.to : pool[Math.floor(Math.random() * pool.length)];
  }

  _tick() {
    const t = this._clock.getElapsedTime();
    const spin = this.reducedMotion ? 0.15 : 1;

    // Ambiance
    this.globe.rotation.y = t * 0.25 * spin;
    this.orbit.rotation.y = -t * 0.4 * spin;
    this.cardHolder.position.y = Math.sin(t * 1.6) * 0.06 * spin;
    this.cardHolder.rotation.x = Math.sin(t * 1.1) * 0.08 * spin;
    this.chips.forEach((s) => {
      const k = s.userData.base * (1 + Math.sin(t * 3 + s.position.x) * 0.03);
      s.scale.set(k, k / 2, 1);
    });

    // Retournements de la carte
    if (this._flip) {
      const p = Math.min((t - this._flip.t0) / FLIP_DURATION, 1);
      this.card.rotation.y = this._flip.from + (this._flip.to - this._flip.from) * easeInOutCubic(p);
      if (p >= 1) {
        this._frontVisible = !this._frontVisible;
        this._showing = this._flip.lang.code;
        this._flip = null;
        if (this._finishing && this._showing === this.to.code) this._settle(t);
        else this._nextFlipAt = t + (this.reducedMotion ? FLIP_INTERVAL * 3 : FLIP_INTERVAL);
      }
    } else if (!this._settled && t >= this._nextFlipAt) {
      if (this._finishing) {
        if (this._showing === this.to.code) this._settle(t);
        else this._startFlip(this.to, t);
      } else {
        this._startFlip(this._pickShuffle(), t);
      }
    }

    // Onde de validation puis fondu
    if (this._pulse) {
      const p = Math.min((t - this._pulse.t0) / 0.8, 1);
      const s = 1.1 + easeOutCubic(p) * 1.9;
      this.pulseMesh.scale.set(s, s, 1);
      this.pulseMesh.material.opacity = 1 - p;
      if (p >= 1 && !this._pulse.done) {
        this._pulse.done = true;
        this._hide();
      }
    }

    this.renderer.render(this.scene, this.camera);
  }

  _settle(t) {
    this._settled = true;
    this._pulse = { t0: t, done: false };
    this.pulseMesh.visible = true;
    this.subtitle.textContent = `✓ ${this.to.name}`;
  }

  _hide() {
    this.overlay.classList.remove('lsl-visible');
    const session = this._session;
    setTimeout(() => {
      if (session !== this._session) return; // un nouveau start() a eu lieu pendant le fondu
      this.overlay.style.display = 'none';
      this.renderer.setAnimationLoop(null);
      this._running = false;
      this._clock.stop();
      const r = this._resolveFinish;
      this._resolveFinish = null;
      r?.();
    }, FADE_MS);
  }
}
