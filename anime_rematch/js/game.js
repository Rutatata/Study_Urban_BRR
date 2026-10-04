'use strict';
(function () {
  // ---------- constants ----------
  const W = 1000, H = 600;                 // pitch size (world units)
  const GY1 = H / 2 - 72, GY2 = H / 2 + 72; // goal mouth
  const GH = 80, GD = 36, WALLH = 55;       // goal height / depth, wall height
  const BOX_D = 150, BOX_W = 170;           // penalty box depth / half-width
  const TILT = 0.62, PK = 0.00055;          // camera tilt and fake perspective
  const GRAV = 950, BR = 6, PR = 11;
  const DIFF = {
    easy:   { react: 1.7, acc: 0.5,  tackle: 0.5, gk: -0.18, speed: 0.9 },
    normal: { react: 1.0, acc: 0.78, tackle: 1.0, gk: 0,     speed: 1 },
    hard:   { react: 0.6, acc: 0.95, tackle: 1.6, gk: 0.1,   speed: 1.07 },
  };
  const MATE_DIFF = { react: 0.9, acc: 0.8, tackle: 1.0, gk: 0, speed: 1 };

  const $ = (id) => document.getElementById(id);
  const cv = $('game'), ctx = cv.getContext('2d');
  const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
  const lerp = (a, b, t) => a + (b - a) * t;
  const rnd = (a, b) => a + Math.random() * (b - a);
  const hyp = Math.hypot;
  const norm = (x, y) => { const l = Math.hypot(x, y) || 1; return { x: x / l, y: y / l, l }; };
  function segDist(px, py, ax, ay, bx, by) {
    const dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy || 1;
    const t = clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1);
    return hyp(px - ax - dx * t, py - ay - dy * t);
  }

  // ---------- canvas / camera ----------
  let cw = 0, chh = 0, Z = 1, dpr = 1;
  const cam = { x: W / 2, y: H / 2, sx: 0, sy: 0 };
  function resize() {
    dpr = Math.min(2, window.devicePixelRatio || 1);
    cw = innerWidth; chh = innerHeight;
    cv.width = cw * dpr; cv.height = chh * dpr;
    cv.style.width = cw + 'px'; cv.style.height = chh + 'px';
    Z = Math.max(cw / 820, chh / 560);
  }
  addEventListener('resize', resize);
  resize();

  const pf = (y) => 1 + (y - cam.y) * PK;
  function P(x, y, z) {
    const f = pf(y);
    return [cw / 2 + (x - cam.x) * Z * f + cam.sx, chh / 2 + (y - cam.y) * Z * TILT - (z || 0) * Z * f + cam.sy];
  }
  function toWorld(sx, sy) {
    const y = cam.y + (sy - chh / 2 - cam.sy) / (Z * TILT);
    return { x: cam.x + (sx - cw / 2 - cam.sx) / (Z * pf(y)), y };
  }
  function path(pts, close) {
    ctx.beginPath();
    pts.forEach((p, i) => { const s = P(p[0], p[1], p[2]); i ? ctx.lineTo(s[0], s[1]) : ctx.moveTo(s[0], s[1]); });
    if (close) ctx.closePath();
  }
  function poly(pts, fill, stroke, lw) {
    path(pts, true);
    if (fill) { ctx.fillStyle = fill; ctx.fill(); }
    if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lw || 1; ctx.stroke(); }
  }
  function lines(pts, stroke, lw, close) {
    path(pts, close); ctx.strokeStyle = stroke; ctx.lineWidth = lw; ctx.stroke();
  }
  function ellipsePts(cx, cy, r, a0 = 0, a1 = Math.PI * 2, n = 40) {
    const o = [];
    for (let i = 0; i <= n; i++) { const a = a0 + (a1 - a0) * i / n; o.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r, 0]); }
    return o;
  }

  // ---------- audio ----------
  let AC = null;
  function audioInit() {
    if (!AC) { try { AC = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { /* no audio */ } }
    if (AC && AC.state === 'suspended') AC.resume();
  }
  function tone(f, d, type = 'square', v = 0.08, slide = 0) {
    if (!AC) return;
    const t = AC.currentTime, o = AC.createOscillator(), g = AC.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, f + slide), t + d);
    g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + d);
    o.connect(g).connect(AC.destination); o.start(t); o.stop(t + d);
  }
  function noise(d, v = 0.15, hp = 800) {
    if (!AC) return;
    const n = (AC.sampleRate * d) | 0, buf = AC.createBuffer(1, n, AC.sampleRate), a = buf.getChannelData(0);
    for (let i = 0; i < n; i++) a[i] = (Math.random() * 2 - 1) * (1 - i / n);
    const s = AC.createBufferSource(); s.buffer = buf;
    const f = AC.createBiquadFilter(); f.type = 'highpass'; f.frequency.value = hp;
    const g = AC.createGain(); g.gain.value = v;
    s.connect(f).connect(g).connect(AC.destination); s.start();
  }
  const SFX = {
    kick: (p) => { noise(0.08, 0.12 + 0.2 * p, 300); tone(150, 0.1, 'sine', 0.25, -90); },
    pass: () => { noise(0.05, 0.1, 700); tone(220, 0.06, 'sine', 0.12, -60); },
    tackle: () => noise(0.18, 0.18, 200),
    wall: () => tone(90, 0.08, 'triangle', 0.12),
    whistle: () => { tone(2100, 0.18, 'sine', 0.07); setTimeout(() => tone(2100, 0.35, 'sine', 0.07), 230); },
    goal: () => { noise(2, 0.16, 500); [523, 659, 784, 1046].forEach((f, i) => setTimeout(() => tone(f, 0.25, 'square', 0.05), i * 110)); },
    superS: () => { tone(180, 0.9, 'sawtooth', 0.06, 900); noise(0.7, 0.12, 1500); },
    save: () => tone(330, 0.15, 'square', 0.06, 200),
    steal: () => { noise(0.12, 0.2, 300); tone(500, 0.1, 'square', 0.05, 300); },
  };

  // ---------- state ----------
  const G = {
    mode: 'menu', teams: [], players: [], user: null, time: 180, matchLen: 180, golden: false,
    modeT: 0, timeScale: 1, popups: [], particles: [], cutin: null, shake: 0, diff: 'normal',
    goals: [], userChar: null, crowdJump: 0, menuT: 0,
  };
  const ball = { x: W / 2, y: H / 2, z: 0, vx: 0, vy: 0, vz: 0, px: 0, py: 0, owner: null, lastTouch: null,
    kind: null, noPickP: null, noPickT: 0, inNet: 0, super: null, spin: 0, passTo: null };

  // crowd heads behind the far wall
  const crowd = [];
  for (let r = 0; r < 6; r++) for (let i = 0; i < 70; i++) {
    crowd.push({ x: -150 + i * 19 + rnd(-6, 6) + (r % 2) * 9, row: r, c: `hsl(${(Math.random() * 360) | 0},70%,${45 + Math.random() * 25}%)`, ph: Math.random() * 6 });
  }

  // ---------- team setup ----------
  function mkPlayer(c, team, role, num) {
    const s = c.stats;
    return { ch: c, team, role, num, x: 0, y: 0, vx: 0, vy: 0, dvx: 0, dvy: 0, fx: team.side, fy: 0, run: 0,
      slideT: 0, recT: 0, stunT: 0, kickT: 0, diveT: 0, dodgeT: 0, tackleCD: 0, dodgeCD: 0, diveCD: 0, immT: 0, callT: 0,
      energy: 0, stamina: 100, holdT: 0, decT: 0, tx: 0, ty: 0, sprint: false, celebrate: false, isUser: false,
      maxSp: 145 + s.speed * 8, shotP: 0.8 + s.shot * 0.035, passA: s.pass, tackleR: 20 + s.tackle * 1.4, gk: s.keeper };
  }
  function mkTeam(i, chars, color, gkColor, name, side, userChar) {
    const t = { i, side, color, gkColor, name, score: 0, players: [], chaser: null, chaser2: null, D: MATE_DIFF };
    const gkC = chars.filter((c) => c !== userChar).sort((a, b) => b.stats.keeper - a.stats.keeper)[0];
    let rest = chars.filter((c) => c !== gkC);
    if (userChar) { rest = rest.filter((c) => c !== userChar); rest.push(userChar); }
    t.players.push(mkPlayer(gkC, t, 'GK', 1));
    ['D', 'L', 'R', 'F'].forEach((r, k) => t.players.push(mkPlayer(rest[k], t, r, [4, 7, 11, 10][k])));
    return t;
  }
  function setupMatch(userChar) {
    const pool = ROSTER.filter((c) => c.id !== userChar.id).sort(() => Math.random() - 0.5);
    const A = mkTeam(0, [userChar, ...pool.slice(0, 4)], '#2f86ff', '#ffd23f', 'СИНИЕ ДРАКОНЫ', 1, userChar);
    const B = mkTeam(1, pool.slice(4, 9), '#ff3b5c', '#3dffb0', 'АЛЫЕ ОНИ', -1, null);
    B.D = DIFF[G.diff];
    G.teams = [A, B];
    G.players = [...A.players, ...B.players];
    G.user = A.players.find((p) => p.ch === userChar);
    G.user.isUser = true;
    G.time = G.matchLen; G.golden = false; G.goals = []; G.popups = []; G.particles = []; G.cutin = null;
    resetKickoff(Math.random() < 0.5 ? 0 : 1);
  }
  const ux2x = (t, ux) => (t.side > 0 ? ux * W : W - ux * W);
  function resetKickoff(ti) {
    const base = { GK: [0.03, 0.5], D: [0.17, 0.5], L: [0.3, 0.22], R: [0.3, 0.78], F: [0.4, 0.5] };
    for (const p of G.players) {
      const b = base[p.role];
      p.x = ux2x(p.team, b[0]); p.y = b[1] * H;
      p.vx = p.vy = p.dvx = p.dvy = 0; p.fx = p.team.side; p.fy = 0;
      p.slideT = p.recT = p.stunT = p.kickT = p.diveT = p.dodgeT = p.immT = p.holdT = p.callT = 0;
      p.celebrate = false;
    }
    const taker = G.teams[ti].players.find((p) => p.role === 'F');
    taker.x = W / 2 - G.teams[ti].side * 14; taker.y = H / 2;
    Object.assign(ball, { vx: 0, vy: 0, vz: 0, z: 0, inNet: 0, super: null, owner: taker, lastTouch: taker, kind: 'drib', noPickT: 0 });
    ball.x = taker.x; ball.y = taker.y;
    G.mode = 'kickoff'; G.modeT = 1.4; G.timeScale = 1;
  }

  // ---------- popups / particles ----------
  function popup(text, color = '#ffd23f', size = 1, sub = '') {
    G.popups = G.popups.filter((p) => p.size > 1.5);
    G.popups.push({ text, sub, color, size, t: size > 1.5 ? 2.6 : 1.1, max: size > 1.5 ? 2.6 : 1.1 });
  }
  function addP(x, y, z, vx, vy, vz, life, color, size, type) {
    if (G.particles.length > 600) return;
    G.particles.push({ x, y, z, vx, vy, vz, life, max: life, color, size, type });
  }
  function burst(x, y, z, color, n, sp, type = 'spark') {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, s = rnd(0.3, 1) * sp;
      addP(x, y, z, Math.cos(a) * s, Math.sin(a) * s, rnd(0, sp), rnd(0.3, 0.8), color, rnd(2, 4), type);
    }
  }

  // ---------- ball actions ----------
  function kick(p, nx, ny, speed, vz, kind) {
    const err = kind === 'pass' || kind === 'lob' ? (10 - p.passA) * 0.012 * rnd(-1, 1) : 0;
    const a = Math.atan2(ny, nx) + err;
    ball.owner = null;
    ball.vx = Math.cos(a) * speed; ball.vy = Math.sin(a) * speed; ball.vz = vz;
    ball.z = Math.max(ball.z, 2);
    ball.noPickP = p; ball.noPickT = 0.22; ball.lastTouch = p; ball.kind = kind; ball.super = null; ball.age = 0;
    p.kickT = 0.25; p.fx = Math.cos(a); p.fy = Math.sin(a); p.holdT = 0;
  }
  function shootAt(p, tx, ty, pw, h) {
    const n = norm(tx - p.x, ty - p.y);
    const sp = (380 + pw * 560) * p.shotP;
    const t = clamp(n.l / sp, 0.08, 1.2);
    const vz = clamp((h - ball.z + 0.5 * GRAV * t * t) / t, 20, 420);
    kick(p, n.x, n.y, sp, vz, 'shot');
    SFX.kick(pw); G.shake = Math.max(G.shake, 5 * pw);
    burst(ball.x, ball.y, ball.z + 4, '#fff', 8, 120);
  }
  function passTo(p, m, lob) {
    const tx = m.x + (m.vx || 0) * 0.32, ty = m.y + (m.vy || 0) * 0.32;
    const n = norm(tx - p.x, ty - p.y);
    if (lob) { const T = clamp(n.l / 500, 0.5, 0.9); kick(p, n.x, n.y, clamp(n.l / T, 200, 760), GRAV * T / 2, 'lob'); }
    else kick(p, n.x, n.y, clamp(220 + n.l * 1.25, 260, 720), 25, 'pass');
    ball.passTo = m.team ? m : null;
    SFX.pass();
  }
  function superShot(p, tx, ty) {
    p.energy = 0;
    SFX.superS();
    G.cutin = { p, t: 1.3, max: 1.3, lines: Array.from({ length: 26 }, () => [Math.random(), rnd(0.2, 1)]), done() {
      if (ball.owner !== p) return;
      shootAt(p, tx, ty, 1.3, rnd(25, 50));
      ball.super = { c1: p.ch.special.color, c2: p.ch.special.color2 };
      G.shake = 16;
      burst(ball.x, ball.y, 20, p.ch.special.color, 30, 300);
    } };
  }

  // ---------- helpers ----------
  const opps = (t) => G.teams[1 - t.i].players;
  const oppGoalX = (t) => (t.side > 0 ? W : 0);
  const ownGoalX = (t) => (t.side > 0 ? 0 : W);
  function inOwnBox(p) { return Math.abs(p.x - ownGoalX(p.team)) < BOX_D && Math.abs(p.y - H / 2) < BOX_W; }
  function nearestOpp(p) {
    let best = null, bd = 1e9;
    for (const o of opps(p.team)) { const d = hyp(o.x - p.x, o.y - p.y); if (d < bd) { bd = d; best = o; } }
    return { p: best, d: bd };
  }
  function bestPass(p) {
    const t = p.team, s = t.side;
    let best = null, bs = -1e9;
    for (const m of t.players) {
      if (m === p || m.stunT > 0 || (m.role === 'GK' && p.role !== 'GK')) continue;
      const d = hyp(m.x - p.x, m.y - p.y);
      if (d < 50 || d > 560) continue;
      let sc = (m.x - p.x) * s * 0.7 - d * 0.12 + Math.min(nearestOpp(m).d, 140) * 0.8 + (m.isUser ? 30 : 0);
      for (const o of opps(t)) if (segDist(o.x, o.y, p.x, p.y, m.x, m.y) < 22) sc -= 120;
      if (sc > bs) { bs = sc; best = m; }
    }
    return best;
  }
  function seek(p, tx, ty, arrive = 30) {
    const d = norm(tx - p.x, ty - p.y);
    const m = d.l < 4 ? 0 : clamp(d.l / arrive, 0, 1);
    p.dvx = d.x * m; p.dvy = d.y * m;
  }
  function slot(p) {
    const t = p.team, bx = t.side > 0 ? ball.x / W : 1 - ball.x / W, by = ball.y / H;
    const own = ball.owner && ball.owner.team === t, adv = own ? 0.1 : -0.04;
    const S = {
      D: [0.15 + 0.3 * bx, 0.5 + (by - 0.5) * 0.35],
      L: [0.26 + 0.42 * bx + adv, 0.2 + (by - 0.5) * 0.3],
      R: [0.26 + 0.42 * bx + adv, 0.8 + (by - 0.5) * 0.3],
      F: [0.42 + 0.42 * bx + adv * 1.5, 0.5 + (by - 0.5) * 0.5],
    }[p.role];
    return { x: ux2x(t, clamp(S[0], 0.07, 0.9)), y: clamp(S[1], 0.08, 0.92) * H };
  }
  function startSlide(p, dx, dy) {
    const n = norm(dx, dy);
    p.slideT = 0.38; p.vx = n.x * 350; p.vy = n.y * 350; p.tackleCD = 1.1; p.fx = n.x; p.fy = n.y;
    SFX.tackle();
    burst(p.x, p.y, 0, '#c9b38a', 6, 60, 'dust');
  }
  function startDodge(p, dx, dy) {
    const n = norm(dx, dy);
    p.dodgeT = 0.2; p.vx = n.x * 400; p.vy = n.y * 400; p.dodgeCD = 0.9; p.immT = Math.max(p.immT, 0.3);
    burst(p.x, p.y, 10, p.ch.special.color, 8, 80);
  }
  function startDive(p, dx, dy) {
    const n = norm(dx, dy);
    const sp = Math.min(300 + p.gk * 14, n.l / 0.3);
    p.diveT = 0.45; p.vx = n.x * sp; p.vy = n.y * sp; p.diveCD = 1.1;
    if (Math.abs(n.x) > 0.2) p.fx = Math.sign(n.x);
  }

  // ---------- AI ----------
  function aiGK(p, dt, D) {
    const t = p.team, s = t.side, b = ball, ogx = ownGoalX(t);
    p.sprint = false;
    if (b.owner === p) {
      p.dvx = p.dvy = 0;
      p.holdT -= dt;
      if (p.holdT <= 0) {
        const m = bestPass(p);
        if (m) passTo(p, m, hyp(m.x - p.x, m.y - p.y) > 260);
        else kick(p, s, rnd(-0.3, 0.3), 650, 260, 'lob');
      }
      return;
    }
    let tx = ogx + s * 18, ty = H / 2 + (b.y - H / 2) * 0.45, sweeping = false;
    const incoming = !b.owner && b.vx * s < -200 && b.age > 0.14 * D.react;
    if (incoming) {
      const tt = (ogx + s * 14 - b.x) / b.vx;
      if (tt > 0 && tt < 1.2) {
        const py = b.y + b.vy * tt;
        if (py > GY1 - 30 && py < GY2 + 30) {
          ty = py; tx = ogx + s * 14;
          const reactOK = tt < 0.42 * (1 + D.gk);
          if (Math.abs(py - p.y) > 22 && reactOK && p.diveCD <= 0 && p.diveT <= 0) startDive(p, tx - p.x, py - p.y);
        }
      }
    } else {
      const inBox = Math.abs(b.x - ogx) < BOX_D && Math.abs(b.y - H / 2) < BOX_W;
      if (!b.owner && inBox) {
        const myd = hyp(b.x - p.x, b.y - p.y);
        let od = 1e9; for (const o of opps(t)) od = Math.min(od, hyp(o.x - b.x, o.y - b.y));
        if (myd < od + 10) { tx = b.x; ty = b.y; p.sprint = true; sweeping = true; }
      } else if (b.owner && b.owner.team !== t && Math.abs(b.owner.x - ogx) < BOX_D * 0.9 && Math.abs(b.owner.y - H / 2) < BOX_W) {
        tx = lerp(ogx + s * 18, b.owner.x, 0.35); ty = lerp(H / 2, b.owner.y, 0.6);
        if (hyp(b.owner.x - p.x, b.owner.y - p.y) < 30 && p.tackleCD <= 0 && Math.random() < dt * 1.5) startSlide(p, b.owner.x - p.x, b.owner.y - p.y);
      }
    }
    if (!sweeping) ty = clamp(ty, GY1 - 20, GY2 + 20);
    tx = s > 0 ? clamp(tx, 8, BOX_D) : clamp(tx, W - BOX_D, W - 8);
    seek(p, tx, ty, 15);
  }

  function aiCarry(p, dt, D) {
    const t = p.team, s = t.side, gx = oppGoalX(t);
    const dg = hyp(gx - p.x, H / 2 - p.y);
    const opp = nearestOpp(p);
    p.decT -= dt;
    if (p.decT <= 0) {
      p.decT = rnd(0.2, 0.4) * D.react;
      const u = G.user;
      if (u && u.team === t && u !== p && u.callT > 0) { passTo(p, u, hyp(u.x - p.x, u.y - p.y) > 300); u.callT = 0; return; }
      if (dg < 330) {
        const keeper = opps(t)[0];
        let ty = keeper.y < H / 2 ? rnd(H / 2 + 20, GY2 - 10) : rnd(GY1 + 10, H / 2 - 20);
        ty += rnd(-1, 1) * (1 - D.acc) * 90;
        if (p.energy >= 100 && dg < 260 && Math.random() < 0.5) { superShot(p, gx + s * 10, ty); return; }
        if (Math.random() < (dg < 200 ? 0.75 : 0.35)) { shootAt(p, gx + s * 10, ty, rnd(0.55, 1), rnd(10, 60)); return; }
      }
      if (opp.d < 65 && Math.random() < 0.6) { const m = bestPass(p); if (m) { passTo(p, m, false); return; } }
      if (Math.random() < 0.07) { const m = bestPass(p); if (m && (m.x - p.x) * s > 80) { passTo(p, m, Math.random() < 0.3); return; } }
      let tx = gx - s * 90, ty = H / 2 + (p.y - H / 2) * 0.5;
      if (opp.p && opp.d < 110) ty += norm(p.x - opp.p.x, p.y - opp.p.y).y * 120;
      p.tx = tx; p.ty = clamp(ty, 30, H - 30);
      if (opp.d < 38 && p.dodgeCD <= 0 && Math.random() < 0.35) {
        const n = norm(p.x - opp.p.x, p.y - opp.p.y);
        startDodge(p, s * 0.7 - n.y * 0.1, n.y || rnd(-1, 1));
      }
    }
    seek(p, p.tx, p.ty, 20);
    p.sprint = opp.d > 70;
  }

  function aiField(p, dt, D) {
    const t = p.team, s = t.side, b = ball, c = b.owner;
    p.sprint = false;
    if (c === p) return aiCarry(p, dt, D);
    const ourBall = c && c.team === t, theirBall = c && c.team !== t;
    if (!ourBall && t.chaser === p) {
      let tx = b.x + b.vx * 0.22, ty = b.y + b.vy * 0.22;
      if (theirBall) { tx = c.x + c.vx * 0.15 - s * 6; ty = c.y + c.vy * 0.15; }
      seek(p, tx, ty, 10); p.sprint = true;
      if (theirBall) {
        const d = hyp(c.x - p.x, c.y - p.y);
        if (d < p.tackleR + 18 && p.tackleCD <= 0 && c.immT <= 0 && Math.random() < dt * 2.4 * D.tackle) {
          startSlide(p, c.x + c.vx * 0.1 - p.x, c.y + c.vy * 0.1 - p.y);
        }
      }
      return;
    }
    if (theirBall && t.chaser2 === p) {
      const gx = ownGoalX(t);
      seek(p, lerp(c.x, gx, 0.3), lerp(c.y, H / 2, 0.3), 20);
      return;
    }
    const sp = slot(p);
    seek(p, sp.x, sp.y, 40);
    if (ourBall) p.sprint = hyp(sp.x - p.x, sp.y - p.y) > 120;
  }

  function computeChasers() {
    const b = ball;
    const tx = b.x + (b.owner ? 0 : b.vx * 0.25), ty = b.y + (b.owner ? 0 : b.vy * 0.25);
    for (const t of G.teams) {
      const c = t.players.filter((p) => p.role !== 'GK' && p.stunT <= 0)
        .map((p) => ({ p, d: hyp(p.x - tx, p.y - ty) })).sort((a, z) => a.d - z.d);
      t.chaser = c[0] ? c[0].p : null;
      t.chaser2 = c[1] ? c[1].p : null;
      if (t.chaser2 && t.chaser2.isUser && c[2]) t.chaser2 = c[2].p;
    }
  }

  // ---------- input ----------
  const keys = {}, mouse = { x: 0, y: 0, used: false, wx: 0, wy: 0 };
  let charging = false, chargeT = 0;
  const playing = () => G.mode === 'play' && !G.cutin;

  function aimPoint(kind) {
    const u = G.user;
    if (mouse.used) return { x: mouse.wx, y: mouse.wy };
    if (kind === 'shot') return { x: oppGoalX(u.team), y: clamp(u.y + u.fy * 250, GY1 + 15, GY2 - 15) };
    return { x: u.x + u.fx * 220, y: u.y + u.fy * 220 };
  }
  function userNear() { const u = G.user; return !ball.owner && hyp(ball.x - u.x, ball.y - u.y) < 34 && ball.z < 60 && u.stunT <= 0 && u.slideT <= 0; }
  function chargeStart() { if (playing() && !charging) { charging = true; chargeT = 0; } }
  function chargeRelease() {
    if (!charging) return;
    charging = false;
    if (!playing()) return;
    const u = G.user, c = clamp(chargeT / 0.8, 0.15, 1), a = aimPoint('shot');
    const volley = ball.owner !== u && userNear();
    if (ball.owner === u || volley) {
      shootAt(u, a.x, a.y, c, 15 + c * 58 + rnd(-8, 8));
      if (volley) popup('С ЛЁТА!', '#29e6ff');
    }
  }
  function passTarget() {
    const u = G.user, a = aimPoint('pass'), dir = norm(a.x - u.x, a.y - u.y);
    let best = null, bs = 1e9;
    for (const m of u.team.players) {
      if (m === u) continue;
      const v = norm(m.x - u.x, m.y - u.y);
      const ang = Math.acos(clamp(v.x * dir.x + v.y * dir.y, -1, 1));
      if (ang < 0.55) { const sc = ang * 300 + v.l * 0.3; if (sc < bs) { bs = sc; best = m; } }
    }
    return { best, a };
  }
  function userPass(lob) {
    const u = G.user;
    if (!playing() || (ball.owner !== u && !userNear())) return;
    const { best, a } = passTarget();
    if (best) passTo(u, best, lob);
    else passTo(u, { x: a.x, y: a.y }, lob);
  }
  function userSpace() {
    const u = G.user;
    if (!playing() || u.stunT > 0 || u.recT > 0 || u.slideT > 0) return;
    const mx = u.dvx || u.fx, my = u.dvy || u.fy;
    if (ball.owner === u) { if (u.dodgeCD <= 0) startDodge(u, mx, my); }
    else if (u.tackleCD <= 0) startSlide(u, mx, my);
  }
  function userSuper() {
    const u = G.user;
    if (!playing()) return;
    if (u.energy < 100) { popup('ЭНЕРГИЯ НЕ ЗАПОЛНЕНА', '#aaa', 0.6); return; }
    if (ball.owner !== u) { popup('НУЖЕН МЯЧ!', '#aaa', 0.6); return; }
    const a = aimPoint('shot');
    superShot(u, a.x, a.y);
  }

  addEventListener('keydown', (e) => {
    keys[e.code] = true;
    if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
    if (e.repeat) return;
    if (e.code === 'Escape' || e.code === 'KeyP') { togglePause(); return; }
    if (!G.user) return;
    if (e.code === 'Space') userSpace();
    else if (e.code === 'KeyK') userPass(false);
    else if (e.code === 'KeyE' || e.code === 'KeyL') userPass(true);
    else if (e.code === 'KeyQ') userSuper();
    else if (e.code === 'KeyC') { if (playing()) { G.user.callT = 1.5; popup('ДАЙ ПАС!', '#29e6ff', 0.6); } }
    else if (e.code === 'KeyJ') { mouse.used = false; chargeStart(); }
  });
  addEventListener('keyup', (e) => { keys[e.code] = false; if (e.code === 'KeyJ') chargeRelease(); });
  cv.addEventListener('mousemove', (e) => { mouse.x = e.clientX; mouse.y = e.clientY; mouse.used = true; });
  cv.addEventListener('mousedown', (e) => {
    audioInit();
    if (!G.user) return;
    mouse.used = true;
    if (e.button === 0) chargeStart();
    else if (e.button === 2) userPass(false);
  });
  addEventListener('mouseup', (e) => { if (e.button === 0) chargeRelease(); });
  cv.addEventListener('contextmenu', (e) => e.preventDefault());
  addEventListener('blur', () => { for (const k in keys) keys[k] = false; if (G.mode === 'play') togglePause(); });

  function userInput(dt) {
    const u = G.user;
    const ix = (keys.KeyD || keys.ArrowRight ? 1 : 0) - (keys.KeyA || keys.ArrowLeft ? 1 : 0);
    const iy = (keys.KeyS || keys.ArrowDown ? 1 : 0) - (keys.KeyW || keys.ArrowUp ? 1 : 0);
    const n = norm(ix, iy);
    u.dvx = ix || iy ? n.x : 0; u.dvy = ix || iy ? n.y : 0;
    const want = (keys.ShiftLeft || keys.ShiftRight) && (ix || iy);
    if (want && u.stamina > 3) { u.sprint = true; u.stamina -= 30 * dt; }
    else { u.sprint = false; u.stamina = Math.min(100, u.stamina + 15 * dt); }
    if (charging) chargeT += dt;
  }

  // ---------- simulation ----------
  function physP(p, dt) {
    if (p.slideT > 0) { p.slideT -= dt; if (p.slideT <= 0) p.recT = 0.32; }
    if (p.diveT > 0) { p.diveT -= dt; if (p.diveT <= 0) p.recT = 0.45; }
    if (p.dodgeT > 0) p.dodgeT -= dt;
    for (const k of ['recT', 'stunT', 'kickT', 'tackleCD', 'dodgeCD', 'diveCD', 'immT', 'callT']) if (p[k] > 0) p[k] -= dt;
    if (p.slideT > 0 || p.diveT > 0 || p.dodgeT > 0) {
      const k = Math.exp(-(p.dodgeT > 0 ? 1 : 2.6) * dt); p.vx *= k; p.vy *= k;
    } else if (p.recT > 0 || p.stunT > 0) {
      const k = Math.exp(-8 * dt); p.vx *= k; p.vy *= k;
    } else {
      const ms = p.maxSp * (p.sprint ? 1.35 : 1) * (ball.owner === p ? 0.88 : 1) * p.team.D.speed;
      const a = Math.min(1, dt * 9);
      p.vx += (p.dvx * ms - p.vx) * a; p.vy += (p.dvy * ms - p.vy) * a;
    }
    p.x = clamp(p.x + p.vx * dt, PR, W - PR);
    p.y = clamp(p.y + p.vy * dt, PR, H - PR);
    const sp = hyp(p.vx, p.vy);
    p.run += sp * dt * 0.09;
    if (p.isUser && charging && mouse.used) {
      const a = norm(mouse.wx - p.x, mouse.wy - p.y); p.fx = a.x; p.fy = a.y;
    } else if (sp > 20 && p.slideT <= 0 && p.diveT <= 0 && p.kickT <= 0) { p.fx = p.vx / sp; p.fy = p.vy / sp; }
  }
  function separate() {
    const ps = G.players;
    for (let i = 0; i < ps.length; i++) for (let j = i + 1; j < ps.length; j++) {
      const a = ps[i], b = ps[j], dx = b.x - a.x, dy = b.y - a.y, d = hyp(dx, dy);
      if (d > 0 && d < PR * 2) { const push = (PR * 2 - d) / 2, nx = dx / d, ny = dy / d; a.x -= nx * push; a.y -= ny * push; b.x += nx * push; b.y += ny * push; }
    }
  }
  function tackles() {
    for (const p of G.players) {
      if (p.slideT <= 0) continue;
      const c = ball.owner;
      if (c && c.team !== p.team) {
        const immune = c.immT > 0 || c.dodgeT > 0 || (c.role === 'GK' && c.holdT > 0);
        if (!immune && hyp(c.x - p.x, c.y - p.y) < p.tackleR) {
          const n = norm(p.vx, p.vy);
          c.stunT = 0.8;
          ball.owner = null;
          ball.vx = n.x * 190 + c.vx * 0.3; ball.vy = n.y * 190 + c.vy * 0.3; ball.vz = 90;
          ball.lastTouch = p; ball.kind = 'loose'; ball.noPickP = c; ball.noPickT = 0.6;
          p.energy += 15; G.shake = Math.max(G.shake, 6);
          SFX.steal();
          burst(c.x, c.y, 20, '#ffd23f', 14, 160, 'star');
          if (p.isUser || c.isUser || Math.random() < 0.5) popup(p.isUser ? 'ОТБОР!' : c.isUser ? 'ПОТЕРЯ!' : 'ОТБОР!', p.team.i === 0 ? '#29e6ff' : '#ff5a7a', 0.8);
        }
      } else if (!c && !ball.inNet && ball.z < 18 && hyp(ball.x - p.x, ball.y - p.y) < p.tackleR && !(ball.noPickP === p && ball.noPickT > 0)) {
        const n = norm(p.vx, p.vy);
        ball.vx = n.x * 280; ball.vy = n.y * 280; ball.vz = 40;
        ball.lastTouch = p; ball.kind = 'loose'; ball.noPickP = p; ball.noPickT = 0.15;
        SFX.kick(0.3);
      }
    }
  }

  function updateBall(dt) {
    const b = ball;
    b.px = b.x; b.py = b.y; b.age = (b.age || 0) + dt;
    if (b.noPickT > 0) b.noPickT -= dt;
    if (b.owner) {
      const p = b.owner;
      const holding = p.role === 'GK' && p.holdT > 0;
      const d = holding ? 9 : 15;
      b.x = p.x + p.fx * d; b.y = p.y + p.fy * d * 0.8;
      b.z = holding ? 24 : Math.abs(Math.sin(p.run * 1.2)) * 3;
      b.vx = p.vx; b.vy = p.vy; b.vz = 0;
      b.spin += hyp(p.vx, p.vy) * dt * 0.06;
      return;
    }
    b.vz -= GRAV * dt;
    b.x += b.vx * dt; b.y += b.vy * dt; b.z += b.vz * dt;
    if (b.z < 0) { b.z = 0; b.vz = b.vz < -70 ? -b.vz * 0.5 : 0; }
    const k = Math.exp(-(b.z < 1 ? 1.15 : 0.15) * dt);
    b.vx *= k; b.vy *= k;
    const spd = hyp(b.vx, b.vy);
    b.spin += spd * dt * 0.06;
    if (b.super && spd < 300) b.super = null;
    if (spd > 520 || b.super) {
      addP(b.x, b.y, b.z, 0, 0, 0, b.super ? 0.4 : 0.18, b.super ? (Math.random() < 0.5 ? b.super.c1 : b.super.c2) : 'rgba(255,255,255,0.8)', b.super ? 7 : 4, 'trail');
    }

    if (b.inNet) {
      const gx = b.inNet < 0 ? 0 : W, s = b.inNet;
      const xin = s < 0 ? clamp(b.x, -GD + BR, 2) : clamp(b.x, W - 2, W + GD - BR);
      if (xin !== b.x) { b.x = xin; b.vx *= -0.2; }
      if (b.y < GY1 + BR) { b.y = GY1 + BR; b.vy *= -0.3; }
      if (b.y > GY2 - BR) { b.y = GY2 - BR; b.vy *= -0.3; }
      if (b.z > GH - BR) { b.z = GH - BR; b.vz = -Math.abs(b.vz) * 0.2; }
      void gx;
      return;
    }
    let bounced = false;
    if (b.y < BR) { b.y = BR; b.vy = Math.abs(b.vy) * 0.75; bounced = true; }
    if (b.y > H - BR) { b.y = H - BR; b.vy = -Math.abs(b.vy) * 0.75; bounced = true; }
    const mouth = b.y > GY1 + 2 && b.y < GY2 - 2 && b.z < GH - BR;
    if (b.x < BR && !mouth) { b.x = BR; b.vx = Math.abs(b.vx) * 0.72; bounced = true; }
    if (b.x > W - BR && !mouth) { b.x = W - BR; b.vx = -Math.abs(b.vx) * 0.72; bounced = true; }
    if (bounced && spd > 160) { SFX.wall(); if (spd > 450) G.shake = Math.max(G.shake, 3); }
    if (b.x < -BR) { b.inNet = -1; scoreGoal(G.teams.find((t) => t.side < 0)); }
    else if (b.x > W + BR) { b.inNet = 1; scoreGoal(G.teams.find((t) => t.side > 0)); }
  }

  function gain(p) {
    const b = ball, prev = b.lastTouch;
    if (prev && prev !== p && prev.team === p.team && (b.kind === 'pass' || b.kind === 'lob')) {
      prev.energy += prev.isUser ? 12 : 7;
      if (p.isUser) p.energy += 4;
    }
    if (prev && prev.team !== p.team && (b.kind === 'pass' || b.kind === 'lob') && (p.isUser || prev.isUser)) popup('ПЕРЕХВАТ!', p.team.i === 0 ? '#29e6ff' : '#ff5a7a', 0.8);
    b.owner = p; b.super = null; b.lastTouch = p; b.kind = 'drib';
    p.immT = Math.max(p.immT, 0.3); p.decT = rnd(0.15, 0.3);
    if (p.role === 'GK' && inOwnBox(p)) { p.holdT = 0.9; p.immT = 1.0; }
  }
  function pickup() {
    const b = ball;
    if (b.owner || b.inNet) return;
    let best = null, bd = 1e9;
    for (const p of G.players) {
      if (p.stunT > 0 || p.recT > 0 || p.slideT > 0) continue;
      if (b.noPickP === p && b.noPickT > 0) continue;
      const gk = p.role === 'GK' && inOwnBox(p);
      const reach = gk ? (p.diveT > 0 ? 34 : 24) : 17, zr = gk ? 80 : 26;
      const d = segDist(p.x, p.y, b.px, b.py, b.x, b.y);
      if (d < reach && b.z < zr && d < bd) { bd = d; best = p; }
    }
    if (!best) return;
    const spd = hyp(b.vx, b.vy), foe = b.lastTouch && b.lastTouch.team !== best.team;
    const gk = best.role === 'GK' && inOwnBox(best);
    if (gk && foe && spd > 260) {
      const reach = best.diveT > 0 ? 34 : 24;
      let pc = 0.55 + best.gk * 0.04 - (spd - 500) / 1000 + best.team.D.gk + (best.diveT > 0 ? 0.05 : 0) - (b.super ? 0.4 : 0) - (bd / reach) * 0.45;
      pc = clamp(pc, 0.05, 0.95);
      if (Math.random() < pc) {
        gain(best); best.energy += 15;
        popup('СЕЙВ!', '#3dffb0', 0.9); SFX.save();
        burst(best.x, best.y, 30, '#fff', 12, 140, 'star');
      } else if (b.super) {
        best.stunT = 0.9; b.noPickP = best; b.noPickT = 1; b.vx *= 0.9; b.vy *= 0.9;
        popup('ПРОБИЛ!', '#ff3b8d', 0.9);
      } else {
        b.vx = -b.vx * 0.35; b.vy = b.vy * 0.3 + rnd(-260, 260); b.vz = rnd(120, 260);
        b.lastTouch = best; b.kind = 'parry'; b.noPickP = best; b.noPickT = 0.5;
        popup('ОТБИЛ!', '#3dffb0', 0.8); SFX.save();
        burst(best.x, best.y, 30, '#fff', 8, 120, 'star');
      }
      return;
    }
    if (b.super && foe) {
      best.stunT = 0.8; b.noPickP = best; b.noPickT = 0.5;
      burst(best.x, best.y, 20, b.super.c1, 14, 180, 'star');
      return;
    }
    if (foe && spd > 620 && Math.random() < 0.4) {
      b.vx *= -0.3; b.vy = b.vy * 0.4 + rnd(-150, 150); b.vz = 120;
      b.lastTouch = best; b.kind = 'loose'; b.noPickP = best; b.noPickT = 0.25;
      return;
    }
    gain(best);
  }

  function scoreGoal(team) {
    if (G.mode !== 'play') return;
    team.score++;
    const lt = ball.lastTouch, own = lt && lt.team !== team;
    const scorer = own ? null : lt;
    const mins = G.golden ? 'ЗГ' : fmtTime(G.matchLen - G.time);
    G.goals.push({ team: team.i, name: lt ? lt.ch.name : '?', own, time: mins, super: !!ball.super });
    G.mode = 'goal'; G.modeT = 3.4; G.kickTeam = 1 - team.i;
    G.shake = 18; G.crowdJump = 3;
    SFX.goal();
    popup(own ? 'АВТОГОЛ!' : 'ГОЛ!!!', team.color, 2, own ? `${lt.ch.name} в свои ворота` : `${lt.ch.name} — «${lt.ch.quote}»`);
    if (scorer) scorer.celebrate = true;
    for (let i = 0; i < 70; i++) addP(rnd(ball.x - 60, ball.x + 60), rnd(GY1, GY2), rnd(40, 120), rnd(-80, 80), rnd(-80, 80), rnd(50, 250), rnd(1, 2.5), Math.random() < 0.5 ? team.color : '#ffd23f', rnd(3, 5), 'confetti');
  }
  const fmtTime = (t) => { t = Math.max(0, Math.ceil(t)); return `${(t / 60) | 0}:${String(t % 60).padStart(2, '0')}`; };

  function updateParticles(dt) {
    for (const p of G.particles) {
      p.life -= dt;
      if (p.type === 'trail') continue;
      p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
      p.vz -= (p.type === 'confetti' ? 120 : 500) * dt;
      if (p.z < 0) { p.z = 0; p.vz = 0; p.vx *= 0.8; p.vy *= 0.8; }
    }
    G.particles = G.particles.filter((p) => p.life > 0);
  }

  function update(rdt) {
    if (G.cutin) {
      G.cutin.t -= rdt;
      if (G.cutin.t <= 0) { const c = G.cutin; G.cutin = null; c.done(); }
      return;
    }
    G.modeT -= rdt;
    for (const p of G.popups) p.t -= rdt;
    G.popups = G.popups.filter((p) => p.t > 0);
    G.crowdJump = Math.max(0, G.crowdJump - rdt);
    G.shake *= Math.exp(-6 * rdt);

    if (G.mode === 'kickoff') {
      if (G.modeT <= 0) { G.mode = 'play'; SFX.whistle(); popup('ВПЕРЁД!', '#fff', 1); }
      updateCamera(rdt);
      return;
    }
    const dt = rdt * (G.mode === 'goal' && G.modeT > 2.6 ? 0.35 : 1);
    if (G.mode === 'goal') {
      for (const p of G.players) {
        if (p.celebrate) { seek(p, p.x + p.team.side * 60, p.y < H / 2 ? 40 : H - 40, 30); p.sprint = true; }
        else { p.dvx = p.dvy = 0; }
        physP(p, dt);
      }
      updateBall(dt);
      updateParticles(dt);
      updateCamera(rdt);
      if (G.modeT <= 0) {
        if (G.golden || G.time <= 0) endMatch();
        else resetKickoff(G.kickTeam);
      }
      return;
    }
    // play
    if (!G.golden) {
      G.time -= dt;
      if (G.time <= 0) {
        G.time = 0;
        if (G.teams[0].score === G.teams[1].score) { G.golden = true; popup('ЗОЛОТОЙ ГОЛ!', '#ffd23f', 1.2, 'кто забьёт — тот победил'); SFX.whistle(); }
        else { SFX.whistle(); endMatch(); return; }
      }
    }
    userInput(dt);
    mouseWorld();
    computeChasers();
    for (const p of G.players) {
      if (p.isUser) continue;
      if (p.role === 'GK') aiGK(p, dt, p.team.D); else aiField(p, dt, p.team.D);
    }
    for (const p of G.players) { physP(p, dt); p.energy = Math.min(100, p.energy + dt * (p.isUser ? 1.2 : 0.5)); }
    separate();
    tackles();
    updateBall(dt);
    pickup();
    updateParticles(dt);
    updateCamera(rdt);
  }
  function mouseWorld() { const w = toWorld(mouse.x, mouse.y); mouse.wx = w.x; mouse.wy = w.y; }

  function updateCamera(rdt) {
    const u = G.user;
    let fx = ball.x, fy = ball.y;
    if (u && G.mode !== 'goal') { fx = lerp(u.x, ball.x, 0.4); fy = lerp(u.y, ball.y, 0.4); }
    const vw = cw / Z, vh = chh / (Z * TILT);
    fx = vw < W + 160 ? clamp(fx, vw / 2 - 80, W - vw / 2 + 80) : W / 2;
    fy = vh < H + 160 ? clamp(fy, vh / 2 - 110, H - vh / 2 + 50) : H / 2 - 30;
    const k = 1 - Math.exp(-4 * rdt);
    cam.x = lerp(cam.x, fx, k); cam.y = lerp(cam.y, fy, k);
    cam.sx = rnd(-1, 1) * G.shake; cam.sy = rnd(-1, 1) * G.shake;
  }

  // ---------- rendering ----------
  function drawBackground() {
    const g = ctx.createLinearGradient(0, 0, 0, chh);
    g.addColorStop(0, '#1b0b3a'); g.addColorStop(0.5, '#3a1460'); g.addColorStop(1, '#0d0820');
    ctx.fillStyle = g; ctx.fillRect(0, 0, cw, chh);
    // stand slope + crowd
    const t = performance.now() / 1000;
    poly([[-300, -10, WALLH], [W + 300, -10, WALLH], [W + 300, -10, 260], [-300, -10, 260]], '#22123f');
    for (const c of crowd) {
      const jump = G.crowdJump > 0 ? Math.abs(Math.sin(t * 9 + c.ph)) * 10 : Math.abs(Math.sin(t * 2 + c.ph)) * 1.5;
      const [sx, sy] = P(c.x, -10, WALLH + 20 + c.row * 30 + jump);
      const r = 6.5 * Z;
      ctx.fillStyle = c.c; ctx.fillRect(sx - r, sy, r * 2, r * 2.2);
      ctx.fillStyle = '#f2c9a0'; ctx.beginPath(); ctx.arc(sx, sy - r * 0.2, r * 0.75, 0, 7); ctx.fill();
    }
    // floodlights glow
    for (const lx of [100, W - 100]) {
      const [sx, sy] = P(lx, -10, 300);
      const lg = ctx.createRadialGradient(sx, sy, 0, sx, sy, 220 * Z);
      lg.addColorStop(0, 'rgba(255,255,230,0.35)'); lg.addColorStop(1, 'rgba(255,255,230,0)');
      ctx.fillStyle = lg; ctx.fillRect(sx - 220 * Z, sy - 220 * Z, 440 * Z, 440 * Z);
    }
  }
  function drawField() {
    // surrounding floor
    poly([[-200, -10, 0], [W + 200, -10, 0], [W + 200, H + 200, 0], [-200, H + 200, 0]], '#1d4a2a');
    // stripes
    for (let i = 0; i < 10; i++) poly([[i * 100, 0, 0], [i * 100 + 100, 0, 0], [i * 100 + 100, H, 0], [i * 100, H, 0]], i % 2 ? '#3fae4f' : '#47bd57');
    const lc = 'rgba(255,255,255,0.85)', lw = 2.5 * Z;
    lines([[0, 0, 0], [W, 0, 0], [W, H, 0], [0, H, 0]], lc, lw, true);
    lines([[W / 2, 0, 0], [W / 2, H, 0]], lc, lw);
    lines(ellipsePts(W / 2, H / 2, 70), lc, lw);
    for (const s of [0, 1]) {
      const x0 = s ? W : 0, d = s ? -1 : 1;
      lines([[x0, H / 2 - BOX_W, 0], [x0 + d * BOX_D, H / 2 - BOX_W, 0], [x0 + d * BOX_D, H / 2 + BOX_W, 0], [x0, H / 2 + BOX_W, 0]], lc, lw);
      lines([[x0, GY1 - 25, 0], [x0 + d * 55, GY1 - 25, 0], [x0 + d * 55, GY2 + 25, 0], [x0, GY2 + 25, 0]], lc, lw);
      lines(ellipsePts(x0 + d * BOX_D, H / 2, 55, s ? Math.PI / 2 + 0.6 : -Math.PI / 2 + 0.6, s ? Math.PI * 1.5 - 0.6 : Math.PI / 2 - 0.6, 20), lc, lw);
    }
    const [cx, cy] = P(W / 2, H / 2, 0);
    ctx.fillStyle = lc; ctx.beginPath(); ctx.ellipse(cx, cy, 4 * Z, 3 * Z, 0, 0, 7); ctx.fill();
    // center logo
    ctx.save(); ctx.globalAlpha = 0.18; ctx.fillStyle = '#fff'; ctx.font = `${30 * Z}px Bangers, sans-serif`; ctx.textAlign = 'center';
    ctx.fillText('ANIME LEAGUE', cx, cy + 10 * Z); ctx.restore();
  }
  function drawWalls() {
    // far wall with ad boards
    poly([[0, 0, 0], [W, 0, 0], [W, 0, WALLH], [0, 0, WALLH]], '#170c33');
    const ads = ['ANIME REMATCH', 'ラーメン', 'SHONEN JUMP CUP', 'ゴール!', 'KAWAII ENERGY', '必殺シュート'];
    for (let i = 0; i < 6; i++) {
      const x0 = i * (W / 6);
      poly([[x0 + 4, 0, 8], [x0 + W / 6 - 4, 0, 8], [x0 + W / 6 - 4, 0, WALLH - 8], [x0 + 4, 0, WALLH - 8]], i % 2 ? '#ff3b8d' : '#29a3ff');
      const [sx, sy] = P(x0 + W / 12, 0, WALLH / 2 - 6);
      ctx.fillStyle = '#fff'; ctx.font = `${14 * Z}px "Russo One", sans-serif`; ctx.textAlign = 'center'; ctx.fillText(ads[i], sx, sy);
    }
    lines([[0, 0, WALLH], [W, 0, WALLH]], '#9ff', 2 * Z);
    // side walls (glass)
    for (const x of [0, W]) {
      for (const seg of [[0, GY1, 0, WALLH], [GY2, H, 0, WALLH], [GY1, GY2, GH, WALLH]]) {
        poly([[x, seg[0], seg[2]], [x, seg[1], seg[2]], [x, seg[1], seg[3]], [x, seg[0], seg[3]]], 'rgba(150,220,255,0.12)', 'rgba(170,240,255,0.5)', 1.5 * Z);
      }
    }
  }
  function drawGoal(side) {
    const x = side < 0 ? 0 : W, bx = x + side * GD;
    const net = 'rgba(255,255,255,0.18)', nl = 'rgba(255,255,255,0.45)';
    poly([[x, GY1, 0], [bx, GY1, 0], [bx, GY1, GH * 0.75], [x, GY1, GH]], net, nl, Z);
    poly([[bx, GY1, 0], [bx, GY2, 0], [bx, GY2, GH * 0.75], [bx, GY1, GH * 0.75]], net, nl, Z);
    poly([[x, GY1, GH], [bx, GY1, GH * 0.75], [bx, GY2, GH * 0.75], [x, GY2, GH]], net, nl, Z);
    for (let i = 1; i < 8; i++) {
      const y = lerp(GY1, GY2, i / 8);
      lines([[bx, y, 0], [bx, y, GH * 0.75], [x, y, GH]], nl, Z * 0.6);
    }
    for (let i = 1; i < 4; i++) lines([[bx, GY1, GH * 0.75 * i / 4], [bx, GY2, GH * 0.75 * i / 4]], nl, Z * 0.6);
  }
  function drawGoalFront(side) {
    const x = side < 0 ? 0 : W, bx = x + side * GD;
    poly([[x, GY2, 0], [bx, GY2, 0], [bx, GY2, GH * 0.75], [x, GY2, GH]], 'rgba(255,255,255,0.12)', 'rgba(255,255,255,0.4)', Z);
    lines([[x, GY1, 0], [x, GY1, GH], [x, GY2, GH], [x, GY2, 0]], '#fff', 4 * Z);
  }
  function drawNearWall() {
    poly([[-50, H, 0], [W + 50, H, 0], [W + 50, H, 28], [-50, H, 28]], 'rgba(150,220,255,0.10)', 'rgba(170,240,255,0.55)', 2 * Z);
  }

  function drawPlayer(p) {
    const [sx, sy] = P(p.x, p.y, 0), f = pf(p.y);
    const sp = hyp(p.vx, p.vy);
    let st = 'idle';
    if (p.celebrate) st = 'celebrate';
    else if (p.stunT > 0) st = 'stun';
    else if (p.diveT > 0) st = 'dive';
    else if (p.slideT > 0) st = 'slide';
    else if (p.kickT > 0) st = 'kick';
    else if (sp > 25) st = 'run';
    // marker ring
    ctx.save();
    ctx.lineWidth = 2.5 * Z;
    ctx.strokeStyle = p.isUser ? '#ffd23f' : p.team.color;
    ctx.globalAlpha = p.isUser ? 1 : 0.6;
    ctx.beginPath(); ctx.ellipse(sx, sy, 14 * Z * f, 14 * Z * f * TILT, 0, 0, 7); ctx.stroke();
    if (p.isUser && charging) {
      ctx.strokeStyle = '#ff3b8d'; ctx.lineWidth = 4 * Z;
      ctx.beginPath(); ctx.ellipse(sx, sy, 18 * Z * f, 18 * Z * f * TILT, 0, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * clamp(chargeT / 0.8, 0, 1)); ctx.stroke();
    }
    ctx.restore();
    const scale = Z * f * 0.62;
    if (p.dodgeT > 0) { // afterimage
      ctx.save(); ctx.globalAlpha = 0.35;
      window.drawCharacter(ctx, sx - p.vx * 0.05 * Z, sy - p.vy * 0.05 * Z * TILT, p.ch, { scale, facing: p.fx >= 0 ? 1 : -1, runPhase: p.run, moving: true, state: 'run', teamColor: p.role === 'GK' ? p.team.gkColor : p.team.color });
      ctx.restore();
    }
    window.drawCharacter(ctx, sx, sy, p.ch, {
      scale, facing: p.fx >= 0 ? 1 : -1, runPhase: p.run, moving: sp > 25, state: st,
      teamColor: p.role === 'GK' ? p.team.gkColor : p.team.color, number: p.num,
      aura: p.energy >= 100 || (G.cutin && G.cutin.p === p),
    });
    if (p.isUser) {
      const hy = sy - 74 * scale - 8 * Z;
      ctx.fillStyle = '#ffd23f'; ctx.strokeStyle = '#000'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(sx - 7 * Z, hy - 8 * Z); ctx.lineTo(sx + 7 * Z, hy - 8 * Z); ctx.lineTo(sx, hy); ctx.closePath(); ctx.fill(); ctx.stroke();
      if (p.callT > 0) { ctx.font = `${11 * Z}px "Russo One"`; ctx.textAlign = 'center'; ctx.fillStyle = '#29e6ff'; ctx.fillText('ПАС!', sx, hy - 12 * Z); }
    }
  }
  function drawBall() {
    const b = ball, f = pf(b.y);
    const [gx, gy] = P(b.x, b.y, 0);
    const r = BR * Z * f * 1.15;
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.beginPath(); ctx.ellipse(gx, gy, r * (1.1 - Math.min(b.z, 150) / 300), r * 0.55, 0, 0, 7); ctx.fill();
    const [x, y] = P(b.x, b.y, b.z + BR);
    if (b.super) {
      const g = ctx.createRadialGradient(x, y, 0, x, y, r * 4);
      g.addColorStop(0, b.super.c2); g.addColorStop(0.4, b.super.c1); g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, r * 4, 0, 7); ctx.fill();
    }
    ctx.fillStyle = '#fff'; ctx.strokeStyle = '#1a1020'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill(); ctx.stroke();
    ctx.fillStyle = '#222';
    for (let k = 0; k < 3; k++) {
      const a = b.spin + k * 2.1, cx = Math.cos(a) * r * 0.5;
      if (Math.cos(a) > -0.2) { ctx.beginPath(); ctx.arc(x + cx, y + Math.sin(a * 0.7 + k) * r * 0.4, r * 0.28, 0, 7); ctx.fill(); }
    }
  }
  function drawParticle(p) {
    const [x, y] = P(p.x, p.y, p.z), a = clamp(p.life / p.max, 0, 1), s = p.size * Z;
    ctx.globalAlpha = a;
    ctx.fillStyle = p.color;
    if (p.type === 'star') {
      ctx.beginPath();
      for (let i = 0; i < 8; i++) { const rr = i % 2 ? s * 0.4 : s * 1.3, an = i * Math.PI / 4 + p.life * 6; ctx.lineTo(x + Math.cos(an) * rr, y + Math.sin(an) * rr); }
      ctx.fill();
    } else if (p.type === 'confetti') {
      ctx.fillRect(x - s, y - s * 0.5, s * 2, s * Math.abs(Math.sin(p.life * 10)) + 1);
    } else {
      ctx.beginPath(); ctx.arc(x, y, s * (p.type === 'trail' ? a : 1), 0, 7); ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
  function drawAim() {
    const u = G.user;
    if (!u || G.mode !== 'play' || G.cutin) return;
    if (ball.owner === u) {
      const { best } = passTarget();
      if (best) {
        const [x, y] = P(best.x, best.y, 0);
        ctx.strokeStyle = '#29e6ff'; ctx.lineWidth = 2 * Z; ctx.setLineDash([4 * Z, 4 * Z]);
        ctx.beginPath(); ctx.ellipse(x, y, 18 * Z, 18 * Z * TILT, 0, 0, 7); ctx.stroke(); ctx.setLineDash([]);
      }
    }
    if (mouse.used && (ball.owner === u || charging)) {
      const [ax, ay] = P(mouse.wx, mouse.wy, 0), [ux, uy] = P(u.x, u.y, 0);
      const c = charging ? clamp(chargeT / 0.8, 0, 1) : 0;
      ctx.strokeStyle = charging ? `hsl(${50 - c * 50},100%,60%)` : 'rgba(255,255,255,0.5)';
      ctx.lineWidth = (1.5 + c * 3) * Z; ctx.setLineDash([6 * Z, 6 * Z]);
      ctx.beginPath(); ctx.moveTo(ux, uy); ctx.lineTo(ax, ay); ctx.stroke(); ctx.setLineDash([]);
      ctx.beginPath(); ctx.arc(ax, ay, 6 * Z, 0, 7); ctx.stroke();
    }
  }
  function speedLines(cx, cy, color, alpha, n = 40) {
    ctx.save(); ctx.globalAlpha = alpha; ctx.strokeStyle = color;
    const R = Math.hypot(cw, chh);
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, r0 = R * rnd(0.25, 0.45);
      ctx.lineWidth = rnd(1, 4);
      ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * r0, cy + Math.sin(a) * r0); ctx.lineTo(cx + Math.cos(a) * R, cy + Math.sin(a) * R); ctx.stroke();
    }
    ctx.restore();
  }
  function txt(s, x, y, size, fill, align = 'center', stroke = '#000') {
    ctx.font = `${size}px "Russo One", sans-serif`; ctx.textAlign = align; ctx.textBaseline = 'middle';
    ctx.lineJoin = 'round'; ctx.lineWidth = Math.max(3, size / 7); ctx.strokeStyle = stroke; ctx.strokeText(s, x, y);
    ctx.fillStyle = fill; ctx.fillText(s, x, y);
  }
  function skewBox(x, y, w, h, fill) {
    ctx.fillStyle = fill; ctx.beginPath();
    ctx.moveTo(x + 10, y); ctx.lineTo(x + w + 10, y); ctx.lineTo(x + w - 10, y + h); ctx.lineTo(x - 10, y + h); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = '#000'; ctx.lineWidth = 3; ctx.stroke();
  }
  function drawHUD() {
    const [A, B] = G.teams, cx = cw / 2, small = cw < 700;
    const nw = small ? 0 : 200;
    if (nw) { skewBox(cx - 70 - nw, 12, nw, 40, A.color); skewBox(cx + 70, 12, nw, 40, B.color); }
    skewBox(cx - 70, 8, 140, 48, '#120b2a');
    if (nw) { txt(A.name, cx - 70 - nw / 2, 33, 16, '#fff'); txt(B.name, cx + 70 + nw / 2, 33, 16, '#fff'); }
    txt(`${A.score}`, cx - 42, 33, 30, '#fff'); txt(`${B.score}`, cx + 42, 33, 30, '#fff');
    txt(G.golden ? 'ЗГ' : fmtTime(G.time), cx, 33, 18, G.time < 30 && !G.golden ? '#ff5a7a' : '#ffd23f');
    if (G.golden) txt('ЗОЛОТОЙ ГОЛ', cx, 70, 14, '#ffd23f');

    const u = G.user, px = 70, py = chh - 70;
    ctx.save();
    ctx.beginPath(); ctx.arc(px, py, 48, 0, 7);
    const g = ctx.createRadialGradient(px, py, 5, px, py, 48); g.addColorStop(0, u.ch.special.color); g.addColorStop(1, '#120b2a');
    ctx.fillStyle = g; ctx.fill(); ctx.lineWidth = 4; ctx.strokeStyle = '#000'; ctx.stroke(); ctx.clip();
    window.drawPortrait(ctx, px, py + 6, 92, u.ch);
    ctx.restore();
    txt(u.ch.name, px + 58, py - 34, 18, '#fff', 'left');
    const bar = (y, v, col, label) => {
      ctx.fillStyle = '#000'; ctx.fillRect(px + 56, y, 180, 12);
      ctx.fillStyle = col; ctx.fillRect(px + 58, y + 2, 176 * clamp(v / 100, 0, 1), 8);
      txt(label, px + 242, y + 6, 11, '#fff', 'left');
    };
    bar(py - 16, u.stamina, '#3dffb0', 'СПРИНТ');
    const full = u.energy >= 100, pulse = 0.5 + 0.5 * Math.sin(performance.now() / 120);
    bar(py + 4, u.energy, full ? `hsl(${(performance.now() / 5) % 360},100%,60%)` : u.ch.special.color, full ? '' : 'ЭНЕРГИЯ');
    if (full) txt(`Q — ${u.ch.special.name}!`, px + 58, py + 34, 14 + pulse * 2, '#ffd23f', 'left');
    if (G.matchLen - G.time < 12 && !G.golden) {
      txt('ЛКМ — удар · ПКМ — пас · Пробел — подкат · Q — супер · Esc — помощь', cx, chh - 22, 13, '#fff');
    }
    for (const p of G.popups) {
      const k = 1 - p.t / p.max, big = p.size > 1.5;
      const sc = k < 0.12 ? lerp(2.2, 1, k / 0.12) : 1;
      ctx.save(); ctx.globalAlpha = p.t < 0.3 ? p.t / 0.3 : 1;
      const y = big ? chh * 0.4 : chh * 0.28 - k * 30;
      ctx.translate(cx, y); ctx.scale(sc, sc); ctx.rotate(big ? -0.06 : 0);
      txt(p.text, 0, 0, (big ? Math.min(140, cw / 7) : 34) * (big ? 1 : p.size), p.color);
      if (p.sub) txt(p.sub, 0, big ? Math.min(90, cw / 11) : 30, big ? 20 : 15, '#fff');
      ctx.restore();
    }
    if (G.mode === 'kickoff') txt(G.modeT > 0.7 ? 'ГОТОВЬСЯ...' : 'СТАРТ!', cx, chh * 0.3, 44, '#fff');
  }
  function drawCutin() {
    const c = G.cutin, k = 1 - c.t / c.max, ch = c.p.ch, cy = chh / 2;
    ctx.fillStyle = 'rgba(5,0,15,0.6)'; ctx.fillRect(0, 0, cw, chh);
    const bh = Math.min(260, chh * 0.42), slide = k < 0.15 ? (1 - k / 0.15) : 0;
    ctx.save();
    ctx.translate(-slide * cw, 0);
    ctx.beginPath(); ctx.moveTo(0, cy - bh / 2 + 30); ctx.lineTo(cw, cy - bh / 2 - 30); ctx.lineTo(cw, cy + bh / 2 - 30); ctx.lineTo(0, cy + bh / 2 + 30); ctx.closePath();
    const g = ctx.createLinearGradient(0, 0, cw, 0); g.addColorStop(0, ch.special.color); g.addColorStop(1, ch.special.color2);
    ctx.fillStyle = g; ctx.fill(); ctx.lineWidth = 6; ctx.strokeStyle = '#000'; ctx.stroke();
    ctx.save(); ctx.clip();
    ctx.strokeStyle = 'rgba(255,255,255,0.6)';
    for (const [ly, len] of c.lines) {
      const y = cy - bh / 2 + ly * bh, x = ((k * 3 + ly) % 1) * cw * 1.5 - cw * 0.25;
      ctx.lineWidth = 2 + len * 4; ctx.beginPath(); ctx.moveTo(cw - x, y); ctx.lineTo(cw - x + len * 300, y); ctx.stroke();
    }
    window.drawPortrait(ctx, cw * 0.22 + (1 - Math.min(1, k * 4)) * -200, cy + 10, bh * 1.25, ch);
    ctx.restore();
    const fs = Math.min(64, cw / 14);
    txt(ch.special.name.toUpperCase(), cw * 0.62, cy - 10, fs, '#fff');
    txt(`${ch.name} · ${c.p.team.name}`, cw * 0.62, cy + fs * 0.75, 18, '#ffd23f');
    txt(`«${ch.quote}»`, cw * 0.62, cy + fs * 0.75 + 26, 14, '#fff');
    ctx.restore();
    if (k > 0.88) { ctx.fillStyle = `rgba(255,255,255,${(k - 0.88) / 0.12})`; ctx.fillRect(0, 0, cw, chh); }
  }

  function render() {
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    drawBackground();
    drawWalls();
    drawField();
    if (!G.teams.length) return;
    drawGoal(-1); drawGoal(1);
    drawAim();
    const ents = G.players.map((p) => ({ y: p.y, p }));
    ents.push({ y: ball.y + 0.5, ball: true });
    ents.sort((a, b) => a.y - b.y);
    const parts = G.particles;
    for (const e of ents) e.ball ? drawBall() : drawPlayer(e.p);
    drawGoalFront(-1); drawGoalFront(1);
    drawNearWall();
    for (const p of parts) drawParticle(p);
    if (ball.super && !ball.owner) { const [x, y] = P(ball.x, ball.y, ball.z); speedLines(x, y, ball.super.c2, 0.35, 30); }
    if (G.mode === 'goal' && G.modeT > 1.8) speedLines(cw / 2, chh * 0.4, '#fff', 0.5, 50);
    drawHUD();
    if (G.cutin) drawCutin();
  }

  // ---------- loop ----------
  let last = performance.now();
  function frame(now) {
    const rdt = Math.min(0.05, (now - last) / 1000); last = now;
    if (G.mode === 'menu' || G.mode === 'end') { G.menuT += rdt; cam.x = W / 2 + Math.sin(G.menuT * 0.2) * 120; cam.y = H / 2 - 40; cam.sx = cam.sy = 0; }
    else if (G.mode !== 'paused') update(rdt);
    render();
    requestAnimationFrame(frame);
  }

  // ---------- UI screens ----------
  const screens = ['title', 'select', 'pause', 'end'];
  function show(id) { for (const s of screens) $(s).classList.toggle('show', s === id); }
  const howHTML = $('howTpl').innerHTML;
  $('how').innerHTML = howHTML;
  document.querySelector('#pause .how-copy').innerHTML = howHTML;
  $('btnHow').onclick = () => $('how').classList.toggle('hidden');
  $('btnPlay').onclick = () => { audioInit(); buildSelect(); show('select'); };
  $('btnBack').onclick = () => show('title');
  $('btnGo').onclick = startGame;
  $('btnResume').onclick = togglePause;
  $('btnQuit').onclick = () => { G.mode = 'menu'; G.teams = []; G.players = []; G.user = null; show('title'); };
  $('btnRematch').onclick = () => { show(null); setupMatch(G.userChar); };
  $('btnPick').onclick = () => { G.teams = []; G.players = []; G.user = null; buildSelect(); show('select'); };

  let selected = null;
  function buildSelect() {
    const grid = $('grid');
    if (!grid.children.length) {
      for (const c of ROSTER) {
        const d = document.createElement('div'); d.className = 'card';
        const cvs = document.createElement('canvas'); cvs.width = 240; cvs.height = 240;
        const x = cvs.getContext('2d'); x.scale(2, 2);
        const g = x.createLinearGradient(0, 0, 0, 120); g.addColorStop(0, c.special.color); g.addColorStop(1, '#170d38');
        x.fillStyle = g; x.fillRect(0, 0, 120, 120);
        window.drawPortrait(x, 60, 64, 110, c);
        const nm = document.createElement('div'); nm.className = 'nm'; nm.textContent = c.name;
        d.append(cvs, nm);
        d.onclick = () => pick(c, d);
        grid.append(d);
      }
    }
    const idx = selected ? ROSTER.indexOf(selected) : 0;
    pick(ROSTER[idx], grid.children[idx]);
  }
  function pick(c, el) {
    selected = c;
    for (const n of $('grid').children) n.classList.toggle('sel', n === el);
    const x = $('infoPortrait').getContext('2d');
    x.clearRect(0, 0, 220, 220);
    window.drawPortrait(x, 110, 118, 200, c);
    $('infoName').textContent = c.name;
    $('infoAnime').textContent = c.anime;
    const L = { speed: 'Скорость', shot: 'Удар', pass: 'Пас', tackle: 'Отбор', keeper: 'Вратарь' };
    $('infoStats').innerHTML = Object.keys(L).map((k) => `<div class="stat"><span>${L[k]}</span><div class="bar"><i style="width:${c.stats[k] * 10}%"></i></div></div>`).join('');
    $('infoSpecial').innerHTML = `<b>Супер-удар: ${c.special.name}</b><br>«${c.quote}»`;
  }
  function startGame() {
    audioInit();
    G.diff = document.querySelector('input[name=diff]:checked').value;
    G.matchLen = +document.querySelector('input[name=len]:checked').value;
    G.userChar = selected;
    show(null);
    setupMatch(selected);
  }
  function togglePause() {
    if (G.mode === 'paused') { G.mode = G.prevMode; show(null); last = performance.now(); }
    else if (['play', 'kickoff', 'goal'].includes(G.mode)) { charging = false; G.prevMode = G.mode; G.mode = 'paused'; show('pause'); }
  }
  function endMatch() {
    G.mode = 'end';
    const [A, B] = G.teams;
    const win = A.score > B.score;
    $('endTitle').textContent = win ? 'ПОБЕДА!' : 'ПОРАЖЕНИЕ...';
    $('endTitle').style.color = win ? '#ffd23f' : '#ff5a7a';
    $('endScore').innerHTML = `<span style="color:${A.color}">${A.score}</span> : <span style="color:${B.color}">${B.score}</span>`;
    $('endList').innerHTML = `<b>Голы</b><br>` + (G.goals.length ? G.goals.map((g) =>
      `<span style="color:${G.teams[g.team].color}">●</span> ${g.time} — ${g.name}${g.own ? ' (автогол)' : ''}${g.super ? ' ⚡ супер-удар' : ''}`).join('<br>') : 'Голов не было');
    show('end');
  }

  // test hook for automated checks
  window.__AR = { G, ball, setupMatch, keys, mouse, update };

  requestAnimationFrame(frame);
})();
