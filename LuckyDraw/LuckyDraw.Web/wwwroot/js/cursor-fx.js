/* =====================================================================
   CursorFx – hiệu ứng động đi kèm con trỏ chuột màn chiếu (Admin → Giao diện → Con trỏ chuột).
   Ảnh con trỏ là ảnh tĩnh, nên phần chuyển động vẽ trên 1 canvas phủ toàn màn hình (pointer-events:none → không cản bấm).
   - 'wand'  (Đũa thần pháo bông): đầu đũa liên tục toé tia lửa như que pháo bông, rê chuột thì tia kéo thành vệt;
              ~1,4 giây nổ 1 chùm pháo nhỏ; bấm chuột nổ thêm 1 chùm.
   - 'halo'  (Quyền trượng hào quang): quanh đầu quyền trượng là hào quang như tượng Phật – quầng sáng vàng thở nhẹ,
              tia sáng toả tròn quay chậm, các vòng sáng ngũ sắc, đốm sáng lấp lánh xoay quanh; bấm chuột toả 1 vòng sáng.
   Dừng khi chuột ra khỏi cửa sổ, để yên 10 giây (body.btn-idle) hoặc con trỏ bị ẩn (body.idle). Không còn gì để vẽ thì
   tự tắt vòng vẽ.  draw.js gọi CursorFx.setMode('wand' | 'halo' | 'dragon' | null) theo theme.
   Ngoài ra: CursorFx.setButtonSparkler(true/false) – 2 đốm pháo bông chạy quanh viền nút Bắt đầu.
   - 'dragon' (Rồng vờn ngọc): ngọc lửa là con trỏ; con rồng (ảnh /images/mouse/*.webp) uốn lượn đuổi theo ngọc.
   ===================================================================== */
(function () {
    'use strict';
    var reduced = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
    var COLORS = ['#fff6d2', '#ffd86b', '#ffb347', '#ff7a5a', '#ff8fd0', '#9be7ff', '#ffffff'];
    var MAX = 700;
    var cv = null, ctx = null, dpr = 1, raf = 0, last = 0;
    var mode = null, inside = false, x = 0, y = 0, px = 0, py = 0, moved = false, nextBurst = 0;
    var fxScale = 1;   // cỡ con trỏ (Admin → Kích thước con trỏ): hào quang, quầng sáng, con rồng to theo
    var parts = [], ripples = [], haloA = 0;   // haloA: độ hiện của hào quang (0..1) để hiện/tắt mượt

    function rnd(a, b) { return a + Math.random() * (b - a); }
    function pick(a) { return a[(Math.random() * a.length) | 0]; }

    function resize() {
        if (!cv) return;
        dpr = Math.min(2, window.devicePixelRatio || 1);
        cv.width = Math.round(innerWidth * dpr);
        cv.height = Math.round(innerHeight * dpr);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    function ensureCanvas() {
        if (cv) return;
        cv = document.createElement('canvas');
        cv.className = 'cursor-fx';
        cv.setAttribute('aria-hidden', 'true');
        document.body.appendChild(cv);
        ctx = cv.getContext('2d');
        resize();
        window.addEventListener('resize', resize);
    }

    function active() {
        var b = document.body.classList;
        return !!mode && inside && !document.hidden && !b.contains('btn-idle') && !b.contains('idle');
    }

    // ------------------------------------------------------------ Đũa thần: tia lửa
    function spark(sx, sy) {
        if (parts.length >= MAX) return;
        var a = Math.random() * Math.PI * 2, v = rnd(0.8, 4.2);
        parts.push({ x: sx, y: sy, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 0.4, life: 0, max: rnd(16, 40),
            c: Math.random() < 0.65 ? pick(COLORS.slice(0, 4)) : pick(COLORS), w: rnd(1, 2.2), g: 0.06 });
    }

    function burst(sx, sy) {
        var n = reduced ? 10 : 26, c1 = pick(COLORS), c2 = pick(COLORS), sp = rnd(2.4, 3.6);
        for (var i = 0; i < n && parts.length < MAX; i++) {
            var a = (i / n) * Math.PI * 2 + rnd(-0.08, 0.08), v = sp * rnd(0.85, 1.1);
            parts.push({ x: sx, y: sy, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: 0, max: rnd(34, 50),
                c: i % 2 ? c1 : c2, w: rnd(1.2, 2), g: 0.045, glitter: Math.random() < 0.5 });
        }
    }

    function wandStep(t, dt) {
        if (moved) {
            var dx = x - px, dy = y - py, d = Math.hypot(dx, dy), steps = Math.min(12, Math.ceil(d / 6));
            for (var s = 1; s <= steps; s++) for (var k = 0; k < 2; k++) spark(px + dx * s / steps, py + dy * s / steps);
            moved = false;
        }
        var rate = (reduced ? 1 : 4) * dt;
        for (var r = 0; r < rate; r++) spark(x, y);
        if (t > nextBurst) { burst(x, y); nextBurst = t + rnd(1100, 1700); }
        px = x; py = y;
        var gr = ctx.createRadialGradient(x, y, 0, x, y, 14 * fxScale);
        gr.addColorStop(0, 'rgba(255,246,210,.85)');
        gr.addColorStop(0.4, 'rgba(255,200,90,.35)');
        gr.addColorStop(1, 'rgba(255,160,60,0)');
        ctx.fillStyle = gr;
        ctx.beginPath(); ctx.arc(x, y, 14 * fxScale, 0, Math.PI * 2); ctx.fill();
    }

    // ------------------------------------------------------------ Quyền trượng: hào quang
    var RAYS = 28, RINGS = [
        { r: 22, c: '255,226,150', a: 0.65, w: 2.6 },   // vàng
        { r: 31, c: '255,170,205', a: 0.42, w: 2.2 },     // hồng
        { r: 39, c: '170,225,255', a: 0.34, w: 2 },   // xanh
        { r: 47, c: '200,255,200', a: 0.22, w: 1.8 }    // lục nhạt
    ];

    function drawHalo(t, A) {
        if (A <= 0.01) return;
        var pulse = (0.88 + 0.12 * Math.sin(t / 650)) * fxScale, R = 74 * pulse, i;

        // 1. quầng sáng nền
        var g = ctx.createRadialGradient(x, y, 0, x, y, R);
        g.addColorStop(0, 'rgba(255,250,228,' + 0.9 * A + ')');
        g.addColorStop(0.25, 'rgba(255,220,130,' + 0.55 * A + ')');
        g.addColorStop(0.6, 'rgba(255,185,100,' + 0.2 * A + ')');
        g.addColorStop(1, 'rgba(255,170,90,0)');
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(x, y, R, 0, Math.PI * 2); ctx.fill();

        // 2. tia sáng toả tròn, dài ngắn xen kẽ, quay chậm
        var rot = t * 0.00022, rg = ctx.createRadialGradient(x, y, 8, x, y, R * 1.05);
        rg.addColorStop(0, 'rgba(255,244,200,' + 0.85 * A + ')');
        rg.addColorStop(0.55, 'rgba(255,210,120,' + 0.38 * A + ')');
        rg.addColorStop(1, 'rgba(255,200,120,0)');
        ctx.fillStyle = rg;
        ctx.beginPath();
        for (i = 0; i < RAYS; i++) {
            var a = rot + i * Math.PI * 2 / RAYS, len = (i % 2 ? 0.62 : 1) * R * 1.05, hw = i % 2 ? 0.035 : 0.05;
            ctx.moveTo(x + Math.cos(a - hw) * 9, y + Math.sin(a - hw) * 9);
            ctx.lineTo(x + Math.cos(a) * len, y + Math.sin(a) * len);
            ctx.lineTo(x + Math.cos(a + hw) * 9, y + Math.sin(a + hw) * 9);
            ctx.closePath();
        }
        ctx.fill();

        // 3. các vòng sáng ngũ sắc, "thở" lệch pha nhau
        for (i = 0; i < RINGS.length; i++) {
            var rn = RINGS[i], k = 0.75 + 0.25 * Math.sin(t / 520 + i * 1.3);
            ctx.strokeStyle = 'rgba(' + rn.c + ',' + rn.a * k * A + ')';
            ctx.lineWidth = rn.w;
            ctx.beginPath(); ctx.arc(x, y, rn.r * pulse, 0, Math.PI * 2); ctx.stroke();
        }

        // 4. đốm sáng lấp lánh xoay ngược chiều tia
        for (i = 0; i < 10; i++) {
            var b = -t * 0.0006 + i * Math.PI * 2 / 10, rr = (i % 2 ? 35 : 52) * pulse,
                tw = 0.5 + 0.5 * Math.sin(t / 180 + i * 2.1), sx = x + Math.cos(b) * rr, sy = y + Math.sin(b) * rr;
            ctx.fillStyle = 'rgba(255,250,230,' + 0.9 * tw * A + ')';
            ctx.beginPath(); ctx.arc(sx, sy, 1.1 + 1.3 * tw, 0, Math.PI * 2); ctx.fill();
        }
    }

    function drawRipples(dt) {
        for (var i = ripples.length - 1; i >= 0; i--) {
            var q = ripples[i];
            q.life += dt;
            if (q.life >= q.max) { ripples.splice(i, 1); continue; }
            var k = q.life / q.max;
            ctx.strokeStyle = 'rgba(255,230,160,' + (1 - k) * 0.8 + ')';
            ctx.lineWidth = 3 * (1 - k) + 0.5;
            ctx.beginPath(); ctx.arc(q.x, q.y, 14 + k * 70, 0, Math.PI * 2); ctx.stroke();
        }
    }

    // ------------------------------------------------------------ Rồng vờn ngọc (ảnh ở /images/mouse)
    // Viên ngọc lửa = chính con trỏ chuột (dragon.png). Canvas vẽ quầng sáng quanh ngọc + con rồng:
    // đầu rồng lái về phía ngọc (xa thì lao tới, gần thì lượn vòng quanh ngọc), thân là chuỗi đốt bám theo đầu
    // với khoảng cách cố định nên tự uốn lượn/cuộn vòng; 2 cặp chân quẫy nhịp nhàng, đuôi lửa ở cuối.
    var DR = { img: {}, loaded: 0, segs: [], vx: 0, vy: 0, a: 0, placed: false, loading: false };
    var DR_FILES = ['head', 'body', 'leg-front', 'leg-back', 'tail-tip'];
    var DR_N = 34, DR_FRONT = 5, DR_BACK = 18;

    function dragonLoad() {
        if (DR.loading) return;
        DR.loading = true;
        DR_FILES.forEach(function (n) {
            var im = new Image();
            im.onload = function () { DR.loaded++; kick(); };
            im.src = '/images/mouse/' + n + '.webp';
            DR.img[n] = im;
        });
    }

    function dragonScale() { return Math.max(0.75, Math.min(1.5, innerWidth / 1920)) * fxScale; }
    function segD(i, S) { return (33 - 15 * i / (DR_N - 1)) * S; }   // đường kính đốt: 33px ở cổ → 18px ở đuôi

    function dragonPlace(S) {
        DR.segs = [];
        var hx = x + 160 * S, hy = y + 60 * S;
        for (var i = 0; i < DR_N; i++) DR.segs.push({ x: hx + i * 10 * S, y: hy + Math.sin(i / 3) * 20 * S });
        DR.vx = -3; DR.vy = 0; DR.placed = true;
    }

    function dragonStep(t, dt, S) {
        var s = DR.segs, h = s[0];
        // mục tiêu: lượn quanh ngọc theo quỹ đạo Lissajous; ngọc ở xa thì lao thẳng tới ngọc
        var R = 78 * S, tx = x + Math.cos(t / 620) * R, ty = y + Math.sin(t / 430) * R * 0.75;
        var dP = Math.hypot(x - h.x, y - h.y);
        if (dP > 240 * S) { tx = x; ty = y; }
        var dx = tx - h.x, dy = ty - h.y, d = Math.hypot(dx, dy) || 1;
        var sp = Math.max(2.6, Math.min(15, d * 0.09)) * S;
        // bơi uốn: lệch hướng nhẹ theo hình sin
        var wig = Math.sin(t / 170) * 0.35, c = Math.cos(wig), sn = Math.sin(wig);
        var ux = (dx * c - dy * sn) / d, uy = (dx * sn + dy * c) / d;
        var k = Math.min(1, 0.075 * dt);
        DR.vx += (ux * sp - DR.vx) * k;
        DR.vy += (uy * sp - DR.vy) * k;
        h.x += DR.vx * dt; h.y += DR.vy * dt;
        for (var i = 1; i < s.length; i++) {           // chuỗi đốt cách đều → thân uốn theo đường đầu đã đi
            var p = s[i], q = s[i - 1], ex = p.x - q.x, ey = p.y - q.y, l = Math.hypot(ex, ey) || 1,
                gap = segD(i, S) * 0.36;
            p.x = q.x + ex / l * gap; p.y = q.y + ey / l * gap;
        }
    }

    function sprite(im, px, py, ang, w, ax, ay, flip, alpha) {
        var hgt = w * im.naturalHeight / im.naturalWidth;
        ctx.save();
        ctx.translate(px, py);
        ctx.rotate(ang);
        if (flip) ctx.scale(1, -1);
        ctx.globalAlpha = alpha;
        ctx.drawImage(im, -ax * w, -ay * hgt, w, hgt);
        ctx.restore();
    }

    function segAngle(i) {                            // hướng tại đốt i (chỉ về phía đầu)
        var s = DR.segs, a = s[Math.max(0, i - 1)], b = s[Math.min(s.length - 1, i + 1)];
        return Math.atan2(a.y - b.y, a.x - b.x);
    }

    // mỗi cặp chân: 1 chân mỗi bên thân, quẫy ngược pha nhau
    function drawLegs(i, name, t, S, A, phase) {
        var p = DR.segs[i], ang = segAngle(i), im = DR.img[name], w = (name === 'leg-front' ? 40 : 36) * S,
            ax = name === 'leg-front' ? 0.34 : 0.71, sw = Math.sin(t / 140 + phase) * 0.38,
            hgt = w * im.naturalHeight / im.naturalWidth;
        for (var side = -1; side <= 1; side += 2) {
            ctx.save();
            ctx.translate(p.x, p.y);
            ctx.rotate(ang);
            ctx.scale(1, side);
            ctx.rotate(0.15 + sw * side);
            ctx.globalAlpha = A;
            ctx.drawImage(im, -ax * w, -0.85 * hgt, w, hgt);
            ctx.restore();
        }
    }

    function drawDragon(t, dt) {
        var target = mode === 'dragon' && active() ? 1 : 0;
        DR.a += (target - DR.a) * Math.min(1, 0.08 * dt);
        if (Math.abs(target - DR.a) < 0.01) DR.a = target;
        if (DR.a <= 0 || DR.loaded < DR_FILES.length) return;
        var S = dragonScale(), A = DR.a, i;
        if (!DR.placed) dragonPlace(S);
        dragonStep(t, dt, S);
        var s = DR.segs, n = s.length;

        // quầng sáng quanh ngọc (con trỏ)
        ctx.globalCompositeOperation = 'lighter';
        var pr = (26 + 5 * Math.sin(t / 300)) * S, g = ctx.createRadialGradient(x, y, 0, x, y, pr);
        g.addColorStop(0, 'rgba(255,240,180,' + 0.75 * A + ')');
        g.addColorStop(0.5, 'rgba(255,140,60,' + 0.35 * A + ')');
        g.addColorStop(1, 'rgba(255,90,40,0)');
        ctx.fillStyle = g;
        ctx.beginPath(); ctx.arc(x, y, pr, 0, Math.PI * 2); ctx.fill();
        ctx.globalCompositeOperation = 'source-over';

        // đuôi → thân (chân vẽ trước đốt mang chân để nằm dưới thân) → đầu
        var ta = segAngle(n - 1);
        sprite(DR.img['tail-tip'], s[n - 1].x, s[n - 1].y, ta, 76 * S, 0.865, 0.5, Math.cos(ta) < 0, A);
        for (i = n - 1; i >= 1; i--) {
            if (i === DR_BACK) drawLegs(i, 'leg-back', t, S, A, Math.PI);
            if (i === DR_FRONT) drawLegs(i, 'leg-front', t, S, A, 0);
            sprite(DR.img.body, s[i].x, s[i].y, segAngle(i), segD(i, S), 0.5, 0.5, false, A);
        }
        var ha = Math.atan2(s[0].y - s[1].y, s[0].x - s[1].x);
        sprite(DR.img.head, s[0].x, s[0].y, ha, 104 * S, 0.42, 0.5, Math.cos(ha) < 0, A);
        ctx.globalAlpha = 1;
    }

    // ------------------------------------------------------------ Pháo bông chạy quanh viền nút Bắt đầu
    // 2 đốm lửa (đối diện nhau) chạy vòng theo đúng dáng nút: đọc clip-path polygon (Kiểu 2/6 lục giác) hoặc
    // border-radius (viên thuốc / bo góc) → đường viền gấp khúc, mỗi đốm toé tia lửa như que pháo bông, hướng ra ngoài.
    // Tạm dừng khi nút ẩn, để yên chuột 10 giây (nút đang mờ), có màn chúc mừng hoặc popup. Bật/tắt: Admin → Nút bắt đầu.
    var btnSpark = false, btnLap = 0, btnPath = null, btnKey = '';

    function cssLen(v, total) { return /%$/.test(v) ? parseFloat(v) / 100 * total : parseFloat(v) || 0; }

    function btnOutline(b) {
        var r = b.getBoundingClientRect(), cs = getComputedStyle(b), cp = cs.clipPath || '', key = [r.left, r.top, r.width, r.height, cp, cs.borderTopLeftRadius].join('|');
        if (key === btnKey && btnPath) return btnPath;
        var pts = [];
        if (cp.indexOf('polygon(') === 0) {
            cp.slice(8, cp.lastIndexOf(')')).split(',').forEach(function (pair) {
                var v = pair.trim().split(/\s+/);
                pts.push({ x: r.left + cssLen(v[0], r.width), y: r.top + cssLen(v[1], r.height) });
            });
        } else {
            var rad = Math.min(parseFloat(cs.borderTopLeftRadius) || 0, r.height / 2, r.width / 2);
            [[r.right - rad, r.top + rad, -90], [r.right - rad, r.bottom - rad, 0], [r.left + rad, r.bottom - rad, 90], [r.left + rad, r.top + rad, 180]]
                .forEach(function (c) {
                    for (var k = 0; k <= 6; k++) {
                        var a = (c[2] + k * 15) * Math.PI / 180;
                        pts.push({ x: c[0] + Math.cos(a) * rad, y: c[1] + Math.sin(a) * rad });
                    }
                });
        }
        pts.push(pts[0]);
        var acc = [0];
        for (var i = 1; i < pts.length; i++) acc.push(acc[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].y - pts[i - 1].y));
        btnKey = key;
        btnPath = { pts: pts, acc: acc, total: acc[acc.length - 1], cx: r.left + r.width / 2, cy: r.top + r.height / 2 };
        return btnPath;
    }

    function outlineAt(P, u) {                         // u: 0..1 quanh chu vi
        var d = ((u % 1) + 1) % 1 * P.total;
        for (var i = 1; i < P.pts.length; i++) {
            if (P.acc[i] >= d) {
                var a = P.pts[i - 1], b = P.pts[i], l = P.acc[i] - P.acc[i - 1], k = l ? (d - P.acc[i - 1]) / l : 0;
                return { x: a.x + (b.x - a.x) * k, y: a.y + (b.y - a.y) * k };
            }
        }
        return P.pts[0];
    }

    function btnVisible() {
        if (!btnSpark || document.hidden) return null;
        var b = document.getElementById('btnStart'), bl = document.body.classList;
        if (!b || b.hidden || !b.offsetParent || bl.contains('btn-idle') || bl.contains('idle')) return null;
        // Nút bị che bởi lớp phủ toàn màn hình (chúc mừng / xác nhận / vinh danh) → không vẽ pháo bông quanh nút
        var overlays = ['congrats', 'confirmModal', 'honor'];
        for (var j = 0; j < overlays.length; j++) {
            var ov = document.getElementById(overlays[j]);
            if (ov && !ov.hidden) return null;
        }
        var modals = document.querySelectorAll('.modal');
        for (var i = 0; i < modals.length; i++) if (!modals[i].hidden) return null;
        return b;
    }

    function btnSparkStep(dt) {
        var b = btnVisible();
        if (!b) return;
        var P = btnOutline(b);
        if (P.total < 10) return;
        btnLap += dt * 16.67 / 2600;                      // 1 vòng ~2,6 giây
        for (var h = 0; h < 2; h++) {
            var q = outlineAt(P, btnLap + h * 0.5), out = Math.atan2(q.y - P.cy, q.x - P.cx);
            var n = (reduced ? 1 : 3) * dt;
            for (var k = 0; k < n && parts.length < MAX; k++) {
                var a = out + rnd(-1.3, 1.3), v = rnd(0.5, 2.8);
                parts.push({ x: q.x, y: q.y, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 0.3, life: 0, max: rnd(12, 30),
                    c: Math.random() < 0.7 ? pick(COLORS.slice(0, 4)) : pick(COLORS), w: rnd(0.9, 1.8), g: 0.05 });
            }
            var gr = ctx.createRadialGradient(q.x, q.y, 0, q.x, q.y, 10);
            gr.addColorStop(0, 'rgba(255,248,220,.95)');
            gr.addColorStop(0.45, 'rgba(255,200,90,.45)');
            gr.addColorStop(1, 'rgba(255,160,60,0)');
            ctx.fillStyle = gr;
            ctx.beginPath(); ctx.arc(q.x, q.y, 10, 0, Math.PI * 2); ctx.fill();
        }
    }

    // ------------------------------------------------------------ vòng vẽ
    function frame(t) {
        raf = 0;
        var dt = last ? Math.min(3, (t - last) / 16.67) : 1, on = active();
        last = t;
        ctx.clearRect(0, 0, cv.width / dpr, cv.height / dpr);
        drawDragon(t, dt);
        ctx.globalCompositeOperation = 'lighter';

        if (mode === 'wand' && on) wandStep(t, dt);
        btnSparkStep(dt);                              // pháo bông quanh viền nút Bắt đầu

        // hào quang hiện/tắt dần trong ~0,3 giây
        var target = mode === 'halo' && on ? 1 : 0;
        haloA += (target - haloA) * Math.min(1, 0.15 * dt);
        if (Math.abs(target - haloA) < 0.01) haloA = target;
        drawHalo(t, haloA);
        drawRipples(dt);

        ctx.lineCap = 'round';
        for (var i = parts.length - 1; i >= 0; i--) {
            var p = parts[i];
            p.life += dt;
            if (p.life >= p.max) { parts[i] = parts[parts.length - 1]; parts.pop(); continue; }
            var f = Math.pow(0.965, dt);
            p.vx *= f; p.vy = p.vy * f + p.g * dt;
            p.x += p.vx * dt; p.y += p.vy * dt;
            var al = 1 - p.life / p.max;
            if (p.glitter && Math.random() < 0.35) al *= 0.25;   // nhấp nháy như kim tuyến
            ctx.globalAlpha = al;
            ctx.strokeStyle = p.c;
            ctx.lineWidth = p.w;
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
            ctx.lineTo(p.x - p.vx * 3, p.y - p.vy * 3);
            ctx.stroke();
        }
        ctx.globalAlpha = 1;
        ctx.globalCompositeOperation = 'source-over';

        if (on || btnSpark || parts.length || ripples.length || haloA > 0 || DR.a > 0) raf = requestAnimationFrame(frame);
        else last = 0;
    }

    function kick() {
        if (!raf && cv) raf = requestAnimationFrame(frame);
    }

    document.addEventListener('mousemove', function (e) {
        if (!mode) return;
        if (!inside) { px = e.clientX; py = e.clientY; }
        x = e.clientX; y = e.clientY;
        inside = true; moved = true;
        kick();
    }, { passive: true });
    document.addEventListener('mouseout', function (e) { if (!e.relatedTarget) { inside = false; kick(); } });
    document.addEventListener('pointerdown', function (e) {
        if (!mode || e.pointerType !== 'mouse') return;
        if (mode === 'wand') burst(e.clientX, e.clientY);           // đũa thần: nổ thêm 1 chùm
        else ripples.push({ x: e.clientX, y: e.clientY, life: 0, max: 32 });   // quyền trượng / rồng: toả 1 vòng sáng
        kick();
    }, { passive: true });

    window.CursorFx = {
        setMode: function (m, scale) {
            fxScale = Math.max(0.5, Math.min(3, scale || 1));
            mode = m === 'wand' || m === 'halo' || m === 'dragon' ? m : null;
            if (mode) ensureCanvas();
            if (mode === 'dragon') dragonLoad();
            kick();
        },
        // pháo bông chạy quanh viền nút Bắt đầu (Admin → Giao diện → Nút bắt đầu)
        setButtonSparkler: function (on) {
            btnSpark = !!on;
            if (btnSpark) { ensureCanvas(); kick(); }
        }
    };
})();
