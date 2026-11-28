// LuckyDraw – màn chiếu.
// Kết quả do server quyết định (POST /api/draw) ngay khi bấm Bắt đầu; trang chỉ diễn hiệu ứng rồi dừng đúng mã đã quay.
// Không viết cứng chữ hiển thị ở đây: mọi chữ lấy từ window.LD.i18n do server render theo ngôn ngữ.
(function () {
    'use strict';

    var LD = window.LD;
    var $ = function (s) { return document.querySelector(s); };
    var $$ = function (s) { return Array.prototype.slice.call(document.querySelectorAll(s)); };
    function T(key) {
        var s = LD.i18n[key] || key, args = arguments;
        return s.replace(/\{(\d)\}/g, function (_, i) { var v = args[+i + 1]; return v == null ? '' : v; });
    }
    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }
    /**
     * Phím Bắt đầu / Dừng: Space hoặc Enter (kể cả nút bấm USB to giả lập phím Space).
     * Xét cả e.code (phím vật lý): khi bộ gõ tiếng Hoa / tiếng Việt của Windows đang bật,
     * trình duyệt báo e.key = "Process" (keyCode 229) thay vì " ", nhưng e.code vẫn là "Space".
     */
    function isStartKey(e) {
        return e.code === 'Space' || e.code === 'Enter' || e.code === 'NumpadEnter'
            || e.key === ' ' || e.key === 'Spacebar' || e.key === 'Enter'
            || e.keyCode === 32 || e.keyCode === 13;
    }
    var lastStartKey = 0;

    var store = {
        get: function (k) { try { return localStorage.getItem(k); } catch (e) { return null; } },
        set: function (k, v) { try { localStorage.setItem(k, v); } catch (e) { /* bỏ qua */ } }
    };

    var state = {
        ev: null,
        prizes: [],
        idx: 0,
        results: [],
        phase: 'idle',          // idle | spinning | stopping | congrats
        req: null,
        pendingDraw: null,      // { prizeId, requestId } lượt quay gửi chưa chắc thành công (lỗi mạng)
        lastRes: null,
        freshBatch: null,
        autoTimer: 0,
        spinRaf: 0,
        slotCount: 1,
        viewPrizeId: null,      // tab đang xem trong bảng "Danh sách trúng giải"
        needRefresh: false,
        needReload: false,
        allDone: null,          // tất cả giải đã đủ người? (null = chưa tải lần nào)
        honorPending: false,    // vừa xong hết giải → chờ về màn chính để tự mở vinh danh
        honorTimer: 0
    };

    // ------------------------------------------------------------------ API
    function api(url, opts) {
        opts = opts || {};
        var init = { method: opts.method || 'GET', headers: { Accept: 'application/json' }, credentials: 'same-origin' };
        if (opts.body !== undefined) {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(opts.body);
        }
        if (init.method !== 'GET') init.headers.RequestVerificationToken = LD.token;
        // opts.timeout: quá thời gian thì huỷ, coi như lỗi mạng
        var ctl = opts.timeout && window.AbortController ? new AbortController() : null, tm = 0;
        if (ctl) { init.signal = ctl.signal; tm = setTimeout(function () { ctl.abort(); }, opts.timeout); }
        function netErr() {
            clearTimeout(tm);
            var err = new Error(T('networkError'));
            err.network = true;     // không tới được máy chủ (rớt mạng / quá thời gian)
            throw err;
        }
        return fetch(url, init).then(function (r) {
            return r.text().then(function (t) {
                clearTimeout(tm);
                var data = null;
                try { data = t ? JSON.parse(t) : null; } catch (e) { /* không phải JSON */ }
                if (!r.ok) {
                    var msg = (data && data.message) || (r.status === 401 || r.status === 403 ? T('sessionExpired') : T('networkError'));
                    var err = new Error(msg);
                    err.status = r.status;
                    throw err;
                }
                return data;
            }, netErr);
        }, netErr);
    }

    /** Lỗi tạm thời đáng thử lại: rớt mạng tới máy chủ, hoặc máy chủ không tới được database (503). */
    function isTransient(e) {
        return !!(e && (e.network || e.status === 502 || e.status === 503 || e.status === 504));
    }

    // ------------------------------------------------------------------ Trạng thái kết nối
    // Hỏi /api/health định kỳ (10 giây; khi đang lỗi thì 3 giây). Mất kết nối → hiện dải đỏ ở trên cùng;
    // có lại → báo xanh vài giây rồi tải lại dữ liệu cho khớp.
    var Conn = {
        ok: true,
        timer: 0,
        hideTimer: 0,
        start: function () {
            var self = this;
            window.addEventListener('online', function () { self.check(); });
            window.addEventListener('offline', function () { self.set(false, 'server'); });
            this.check();
        },
        check: function () {
            var self = this;
            clearTimeout(this.timer);
            api('/api/health', { timeout: 6000 }).then(function () {
                self.set(true);
            }, function (e) {
                self.set(false, e.network ? 'server' : 'db');
            }).then(function () {
                clearTimeout(self.timer);
                self.timer = setTimeout(function () { self.check(); }, self.ok ? 10000 : 3000);
            });
        },
        /** kind: 'server' | 'db' | 'retry' (đang thử quay lại) */
        set: function (ok, kind) {
            var box = $('#connStatus'), was = this.ok;
            this.ok = ok;
            if (!box) return;
            clearTimeout(this.hideTimer);
            if (!ok) {
                box.textContent = T(kind === 'db' ? 'connDb' : kind === 'retry' ? 'connRetry' : 'connServer');
                box.className = 'conn-status bad';
                box.hidden = false;
                if (was) { clearTimeout(this.timer); this.timer = setTimeout(this.check.bind(this), 3000); }
                return;
            }
            if (was) { box.hidden = true; return; }
            box.textContent = T('connBack');
            box.className = 'conn-status good';
            box.hidden = false;
            this.hideTimer = setTimeout(function () { box.hidden = true; }, 3000);
            if (!state.ev) return;
            if (state.phase === 'idle') refreshData(false); else state.needRefresh = true;
        }
    };

    /** Mã lượt quay (UUID v4). Không dùng crypto.randomUUID vì nó cần HTTPS, còn màn chiếu hay mở qua http://IP-LAN. */
    function newRequestId() {
        var b = new Uint8Array(16);
        window.crypto.getRandomValues(b);
        b[6] = (b[6] & 0x0f) | 0x40;
        b[8] = (b[8] & 0x3f) | 0x80;
        var h = Array.prototype.map.call(b, function (x) { return (x + 256).toString(16).slice(1); }).join('');
        return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20);
    }

    var DRAW_RETRY_MS = 60000;   // thử lại tối đa 60 giây trong lúc các ô vẫn quay
    /**
     * Gửi lệnh quay; gặp lỗi mạng thì tự thử lại với CÙNG mã lượt quay.
     * Server thấy mã đã ghi → trả lại đúng kết quả cũ, nên không bao giờ quay trùng.
     * Bỏ cuộc thì vẫn giữ mã (state.pendingDraw): bấm quay lại cùng giải sẽ dùng lại mã đó.
     */
    function drawWithRetry(prizeId) {
        var pd = state.pendingDraw;
        if (!pd || pd.prizeId !== prizeId) pd = state.pendingDraw = { prizeId: prizeId, requestId: newRequestId() };
        var body = { prizeId: prizeId, requestId: pd.requestId }, t0 = Date.now(), n = 0;
        return new Promise(function (resolve, reject) {
            (function attempt() {
                api('/api/draw', { method: 'POST', body: body, timeout: 20000 }).then(function (res) {
                    if (state.pendingDraw === pd) state.pendingDraw = null;
                    if (n > 0) Conn.check();
                    resolve(res);
                }, function (e) {
                    if (!isTransient(e)) { if (state.pendingDraw === pd) state.pendingDraw = null; reject(e); return; }
                    if (Date.now() - t0 > DRAW_RETRY_MS) { reject(e); return; }
                    n++;
                    Conn.set(false, 'retry');
                    setTimeout(attempt, Math.min(5000, 1000 * n));
                });
            })();
        });
    }

    // ------------------------------------------------------------------ Chữ theo ngôn ngữ trong theme_json
    function loc(t) {
        if (!t) return '';
        var c = LD.culture;
        if (c === 'zh-TW') return t.zhTW || t.vi || '';
        if (c === 'zh-CN') return t.zhCN || t.zhTW || t.vi || '';
        if (c.indexOf('en') === 0) return t.en || t.vi || '';
        return t.vi || '';
    }
    var fontVar = function (f) { return '"' + String(f || 'Anton').replace(/"/g, '') + '"'; };

    function applyTheme() {
        var ev = state.ev, t = ev.theme, p = ev.preset, root = document.documentElement.style, body = document.body;
        root.setProperty('--bg', p.bg);
        root.setProperty('--accent', p.accent);
        body.classList.toggle('light', !!p.light);
        root.setProperty('--title-font', fontVar(t.titleFont));
        root.setProperty('--title-color', t.titleColor || p.accent);
        root.setProperty('--title-size', String(t.titleSize || 64));
        root.setProperty('--digit-font', fontVar(t.digitBox && t.digitBox.fontFamily));
        root.setProperty('--digit-radius', ((t.digitBox && t.digitBox.radius) || 0) + '%');
        root.setProperty('--bg-blur', (t.backgroundBlur || 0) + 'px');
        root.setProperty('--bg-dim', t.backgroundPath ? String((t.backgroundDim || 0) / 100) : '0');
        $('.bg-img').style.backgroundImage = t.backgroundPath ? 'url("' + encodeURI(t.backgroundPath) + '")' : 'none';
        // Mây đung đưa / dê chớp mắt – chỉ với ảnh nền có sẵn của chủ đề (ảnh tải lên thì tắt)
        if (window.BgScene) BgScene.apply(t.backgroundPath ? null : p.bg);
        var logo = $('#logo');
        if (t.logoPath) { logo.src = t.logoPath; logo.hidden = false; } else { logo.hidden = true; }
        // Dòng phụ tiếng Trung chữ nhỏ dưới tên sự kiện (server chọn sẵn, null = không hiện)
        var sub = $('#eventSub');
        if (sub) { sub.textContent = ev.subName || ''; sub.hidden = !ev.subName; }
        applyCursor(t.cursor, t.cursorSize);
        if (window.CursorFx) CursorFx.setButtonSparkler(!t.startButton || t.startButton.sparkler !== false);
        $('#btnStart').classList.remove('s1', 's2', 's3', 's4', 's5', 's6');
        $('#btnStart').classList.add('s' + ((t.startButton && t.startButton.style) || 2));
        $('#eventTitle').textContent = ev.name;
        $('#eventTitle').setAttribute('data-text', ev.name);   // lớp mặt chữ vàng của tiêu đề 3D (::after)
        body.classList.toggle('no-title-banner', t.titleBanner === false);   // bảng hiệu đỏ viền vàng sau tiêu đề
        document.title = ev.name + ' · LuckyDraw';
        StageFx.setSpeed(t.fireworksSpeed || 100);
        StageFx.setAmbient(t.stageEffect);
    }

    // ------------------------------------------------------------------ Dữ liệu
    function cur() { return state.prizes[state.idx] || null; }

    function loadAll() {
        return api('/api/view/event').then(function (ev) {
            if (state.ev && state.ev.eventId !== ev.eventId) { location.reload(); return; }
            state.ev = ev;
            applyTheme();
            Sound.configure(ev.sound);
            return refreshData(true);
        }).catch(function (e) {
            if (e.status === 404) location.reload(); else toast(e.message, true);
        });
    }

    function refreshData(rebuild) {
        var keepId = cur() ? cur().prizeId : null;
        return Promise.all([api('/api/view/prizes'), api('/api/view/results')]).then(function (r) {
            state.prizes = r[0] || [];
            state.results = r[1] || [];
            var i = keepId == null ? -1 : state.prizes.findIndex(function (p) { return p.prizeId === keepId; });
            if (i < 0) {
                i = state.prizes.findIndex(function (p) { return p.remaining > 0; });
                if (i < 0) i = Math.max(0, state.prizes.length - 1);
                rebuild = true;
            }
            var prizeChanged = i !== state.idx;
            state.idx = i;
            var viewOk = state.prizes.some(function (p) { return p.prizeId === state.viewPrizeId; });
            if (prizeChanged || !viewOk) state.viewPrizeId = cur() ? cur().prizeId : null;
            var want = wantedSlots();
            if (rebuild || prizeChanged || (state.lastRes == null && want !== state.slotCount)) buildSlots(want);
            renderAll();
            Sound.update();
            trackAllDone();
        }).catch(function (e) { toast(e.message, true); });
    }

    function wantedSlots() {
        var p = cur();
        if (!p) return 1;
        return Math.max(1, p.remaining > 0 ? p.nextTake : p.drawPerTurn);
    }

    // ------------------------------------------------------------------ Ô quay số
    function padCode(code) {
        var len = state.ev.codeLength;
        code = String(code || '').toUpperCase();
        return code.length >= len ? code.slice(code.length - len) : new Array(len - code.length + 1).join(' ') + code;
    }

    function digitStyle() {
        var s = state.ev && state.ev.theme.digitBox && state.ev.theme.digitBox.style;
        return s === 'gold' || s === 'drum' ? s : 'glass';
    }

    function sizeSlots() {
        var n = state.slotCount, len = state.ev ? state.ev.codeLength : 4;
        var col = document.querySelector('.draw-col');
        var availW = (col ? col.clientWidth : window.innerWidth * 0.6) * 0.94;   // khu quay số = cột phải
        // Các dãy ô xếp chồng theo hàng dọc; nhiều hơn 4 dãy thì chia 2 cột (mỗi cột tối đa 5 dãy, rồi 3 cột…)
        var cols = n <= 4 ? 1 : Math.ceil(n / 5);
        var slotsEl = $('#slots');
        if (slotsEl) slotsEl.style.setProperty('--cols', String(cols));
        var drum = digitStyle() === 'drum';                  // trống 3D: ô rộng hơn + khung vàng dày
        var extra = drum ? 1 : n === 1 ? 0 : 0.75;           // khung + khoảng cách giữa các dãy ô
        var wBox = availW / (cols * (len * (drum ? 1.16 : 1.1) + extra));
        var hi = Math.floor(Math.max(22, Math.min(wBox, n === 1 ? 180 : 110))), lo = 22;
        var rootStyle = document.documentElement.style, main = document.querySelector('.stage-main');
        function fits(b) {
            rootStyle.setProperty('--box', b + 'px');
            // cả khu chính lẫn cột quay số (ô + nút + dòng tiến độ) đều không được tràn
            return (!main || main.scrollHeight <= main.clientHeight + 1) &&
                (!col || col.scrollHeight <= col.clientHeight + 1);
        }
        // Tìm cỡ ô LỚN NHẤT mà toàn bộ khu chính vẫn vừa màn hình (bảng kết quả có chiều cao cố định)
        if (fits(hi)) return;
        while (hi - lo > 1) {
            var mid = (lo + hi) >> 1;
            if (fits(mid)) lo = mid; else hi = mid;
        }
        fits(lo);
    }

    function buildSlots(n) {
        state.slotCount = n;
        state.lastRes = null;
        var wrap = $('#slots'), len = state.ev.codeLength;
        var ph = (state.ev.theme.digitBox && state.ev.theme.digitBox.placeholder) || '?';
        wrap.innerHTML = '';
        state.drum = digitStyle() === 'drum';
        wrap.className = 'slots ' + digitStyle();
        for (var s = 0; s < n; s++) {
            var slot = el('div', 'slot' + (n > 1 ? ' multi' : ''));
            for (var i = 0; i < len; i++) {
                var b = el('div', 'box');
                b.dataset.pos = i;
                var r = el('span', 'reel');
                if (state.drum) {                            // trống 3D: 5 tấm số (2 trên, giữa, 2 dưới)
                    for (var k = 0; k < 5; k++) r.appendChild(el('i', null, k === 2 ? ph : ''));
                    drumPlace(r, 0);
                } else {
                    r.appendChild(el('i', null, ph));
                    r.appendChild(el('i'));
                }
                b.appendChild(r);
                slot.appendChild(b);
            }
            wrap.appendChild(slot);
        }
        sizeSlots();
    }

    // ------------------------------------------------------------------ Cuộn ô số (kiểu vòng quay dọc)
    // 1) Đang quay: mọi ô cuộn ĐỒNG BỘ qua dãy ký tự A→Z rồi 0→9, ký tự mới trồi lên từ dưới.
    // 2) Bấm dừng: các ô tách nhịp (vị trí + tốc độ ngẫu nhiên).
    // 3) Dừng lần lượt từ trái sang phải: mỗi cột hãm lại, nảy nhẹ đúng vào ký tự trúng rồi loé sáng.
    var REEL_SPEED = 18;      // ký tự / giây khi quay đồng bộ
    var LAND_MS = 560;        // thời gian một ô hãm lại
    var reel = { seq: ['0'], pos: 0, speed: 0, start: 0, last: 0 };

    function reelSeq() {
        var set = {};
        (state.ev.codeChars || []).forEach(function (s) {
            for (var i = 0; i < s.length; i++) if (s.charAt(i) !== ' ') set[s.charAt(i)] = 1;
        });
        var arr = Object.keys(set);
        if (!arr.length) arr = '0123456789'.split('');
        function rank(c) { return /[A-Z]/.test(c) ? 0 : /[0-9]/.test(c) ? 1 : 2; }
        return arr.sort(function (a, b) { return rank(a) - rank(b) || (a < b ? -1 : a > b ? 1 : 0); });
    }

    // Trống 3D: đặt 5 tấm quanh trục. Tấm k (0..4, giữa = 2) nghiêng (k-2-frac)*36° → frac tăng thì số trôi lên,
    // số kế tiếp trồi từ dưới (giống dải cuộn phẳng). Tấm quay ra sau (>90°) tự ẩn nhờ backface-visibility.
    var DRUM_STEP = 36;
    function drumPlace(r, frac) {
        for (var k = 0; k < r.children.length; k++)
            r.children[k].style.transform = 'rotateX(' + (-(k - 2 - frac) * DRUM_STEP).toFixed(2) + 'deg) translateZ(var(--r))';
    }

    function boxStatic(b, ch) {
        var r = b.firstChild;
        if (state.drum) {
            for (var k = 0; k < r.children.length; k++) r.children[k].textContent = k === 2 && ch !== ' ' ? ch : '';
            drumPlace(r, 0);
            b.shownIdx = null;
            return;
        }
        r.style.transform = '';
        r.children[0].textContent = ch === ' ' ? '' : ch;
        r.children[1].textContent = '';
        b.shownIdx = null;
    }

    function boxRender(b, pos) {
        var seq = reel.seq, n = seq.length, i = Math.floor(pos), idx = ((i % n) + n) % n;
        var r = b.firstChild;
        if (state.drum) {
            if (b.shownIdx !== idx) {
                b.shownIdx = idx;
                for (var k = 0; k < r.children.length; k++) r.children[k].textContent = seq[(((idx + k - 2) % n) + n) % n];
            }
            drumPlace(r, pos - i);
            return;
        }
        if (b.shownIdx !== idx) {
            b.shownIdx = idx;
            r.children[0].textContent = seq[idx];
            r.children[1].textContent = seq[(idx + 1) % n];
        }
        r.style.transform = 'translateY(' + (-(pos - i) * 100).toFixed(2) + '%)';
    }

    function easeOutBack(u) { var c1 = 1.4, c3 = c1 + 1; return 1 + c3 * Math.pow(u - 1, 3) + c1 * Math.pow(u - 1, 2); }

    function startSpin() {
        reel.seq = reelSeq();
        reel.pos = Math.random() * reel.seq.length;
        reel.speed = 0;
        reel.start = reel.last = performance.now();
        $$('#slots .box').forEach(function (b) {
            b.classList.remove('locked', 'blank', 'hit');
            b.classList.add('spin');
            b.locked = false;
            b.rs = null;
            b.shownIdx = null;
        });
        $('#slots').classList.remove('done');
        cancelAnimationFrame(state.spinRaf);
        state.spinRaf = requestAnimationFrame(reelFrame);
    }

    function reelFrame(t) {
        var dt = Math.min(0.05, (t - reel.last) / 1000);
        reel.last = t;
        reel.speed = REEL_SPEED * Math.min(1, 0.25 + (t - reel.start) / 700);   // tăng tốc trong 0,7 giây đầu
        reel.pos += reel.speed * dt;
        $$('#slots .box.spin').forEach(function (b) {
            var rs = b.rs;
            if (!rs) { boxRender(b, reel.pos); return; }
            if (rs.land) {
                var u = (t - rs.land.t0) / LAND_MS;
                if (u >= 1) { finishLand(b, rs.land.k); return; }
                rs.pos = rs.land.p0 + (rs.land.k - rs.land.p0) * easeOutBack(Math.max(0, u));
            } else {
                rs.pos += rs.speed * dt;
            }
            boxRender(b, rs.pos);
        });
        state.spinRaf = requestAnimationFrame(reelFrame);
    }

    /** Tách nhịp: mỗi ô có vị trí và tốc độ riêng. */
    function desync() {
        var n = reel.seq.length;
        $$('#slots .box.spin').forEach(function (b) {
            b.rs = { pos: reel.pos + 1 + Math.random() * (n - 1), speed: REEL_SPEED * (1.05 + Math.random() * 0.45) };
        });
    }

    /** Bắt đầu hãm một ô vào ký tự ch. */
    function land(b, ch) {
        if (!b.rs) b.rs = { pos: reel.pos, speed: REEL_SPEED };
        var t = reel.seq.indexOf(ch);
        if (ch === ' ' || t < 0) {               // ô đệm trống / ký tự lạ: hiện ngay
            boxStatic(b, ch);
            b.classList.remove('spin');
            b.classList.add('locked');
            b.classList.toggle('blank', ch === ' ');
            b.locked = true;
            b.rs = null;
            return;
        }
        var n = reel.seq.length, p0 = b.rs.pos;
        var k = Math.floor(p0 + 2);              // trượt thêm ít nhất 2 ký tự rồi mới dừng
        while (((k % n) + n) % n !== t) k++;
        b.rs.land = { p0: p0, k: k, t0: performance.now() };
    }

    function finishLand(b, k) {
        boxRender(b, k);
        b.rs = null;
        b.locked = true;
        b.classList.remove('spin');
        b.classList.add('locked', 'hit');
        setTimeout(function () { b.classList.remove('hit'); }, 650);
    }

    // Dừng lần lượt từng cột ô từ trái sang phải trong khoảng "thời gian dừng"
    function lockAll(winners, durationMs) {
        return new Promise(function (resolve) {
            var slots = $$('#slots .slot'), len = state.ev.codeLength;
            var codes = winners.map(function (w) { return padCode(w.code); });
            var per = durationMs / len;
            desync();
            for (var i = 0; i < len; i++) {
                (function (i) {
                    setTimeout(function () {
                        slots.forEach(function (slot, si) {
                            var b = slot.children[i];
                            if (b) land(b, codes[si] ? codes[si].charAt(i) : ' ');
                        });
                        setTimeout(function () {
                            if (i === 0) Sound.loop('spin', false);   // cột đầu (chữ V) chốt số → tắt nhạc "Khi đang quay"
                            Sound.sfx('land', 0.6);
                        }, LAND_MS * 0.55);
                    }, per * (i + 1) - LAND_MS * 0.5);
                })(i);
            }
            setTimeout(function () {
                cancelAnimationFrame(state.spinRaf);
                // Chốt chắc chắn mọi ô (phòng khi tab bị ẩn làm khung hình chậm)
                slots.forEach(function (slot, si) {
                    Array.prototype.forEach.call(slot.children, function (b, i) {
                        if (b.locked) return;
                        var ch = codes[si] ? codes[si].charAt(i) : ' ';
                        boxStatic(b, ch);
                        b.classList.remove('spin');
                        b.classList.add('locked');
                        b.classList.toggle('blank', ch === ' ');
                        b.locked = true;
                    });
                });
                resolve();
            }, per * len + LAND_MS * 0.5 + 420);
        });
    }

    // ------------------------------------------------------------------ Quay
    function onStart() {
        if (state.phase === 'congrats') { closeCongrats(); return; }
        if (!LD.canDraw) return;
        var p = cur();
        if (!p) return;
        if (state.phase === 'spinning') { if (p.drawType !== 2) doStop(); return; }
        if (state.phase !== 'idle') return;
        if (p.remaining <= 0) { goNextUnfinished(); return; }

        state.phase = 'spinning';
        state.freshBatch = null;
        Sound.sfx('click');
        Sound.duck(true);
        Sound.holdBg();                                   // quay → tạm dừng nhạc nền
        Sound.loop('spin', true);
        buildSlots(p.nextTake);
        startSpin();
        StageFx.spin(state.ev.theme.spinEffect !== 'none');
        renderAll();

        state.req = drawWithRetry(p.prizeId);
        state.req.catch(function (e) {
            abortSpin();
            toast(isTransient(e) ? T('drawFailed') : e.message, true);
            refreshData(true);
        });
        if (p.drawType === 2) state.autoTimer = setTimeout(doStop, Math.max(1, p.spinSeconds) * 1000);
    }

    function abortSpin() {
        cancelAnimationFrame(state.spinRaf);
        StageFx.spin(false);
        clearTimeout(state.autoTimer);
        Sound.loop('spin', false);
        Sound.stopSfx('stopping');
        Sound.duck(false);
        Sound.resumeBgLater(10000);
        state.phase = 'idle';
        buildSlots(wantedSlots());
        renderAll();
    }

    function doStop() {
        if (state.phase !== 'spinning') return;
        state.phase = 'stopping';
        clearTimeout(state.autoTimer);
        Sound.sfx('stopping', 0.8);
        renderButton();
        var p = cur();
        state.req.then(function (res) {
            if (res.winners.length !== $$('#slots .slot').length) { buildSlots(res.winners.length); startSpin(); }
            return lockAll(res.winners, Math.max(800, (p ? p.stopSeconds : 2) * 1000)).then(function () { reveal(res); });
        }, function () { /* lỗi đã xử lý ở onStart */ });
    }

    function reveal(res) {
        Sound.loop('spin', false);
        Sound.stopSfx('stopping');
        StageFx.spin(false);
        state.phase = 'congrats';
        state.lastRes = res;
        state.freshBatch = res.batchId;
        state.viewPrizeId = cur() ? cur().prizeId : null;
        $('#slots').classList.add('done');
        Sound.sfx('reveal');
        StageFx.burst(state.ev.theme.congratsEffect || 'confetti');
        renderButton();
        setTimeout(function () {
            if (state.phase !== 'congrats') return;
            showCongrats(res);
            Sound.sfx('congrats');
        }, 650);
        refreshData(false);
    }

    // Sau khi quay xong: màn chúc mừng toàn màn hình với nút Xác nhận / Bỏ qua
    function showCongrats(res) {
        showLastFull(res.winners, 'draw');
    }

    /** Ảnh tròn của một người; không có ảnh → chữ cái đầu của tên. */
    function photoEl(w, cls) {
        var box = el('div', cls);
        if (w.photo) {
            var img = document.createElement('img');
            img.src = w.photo;
            img.alt = '';
            box.appendChild(img);
        } else {
            box.classList.add('ph');
            var parts = (w.name || '').trim().split(/\s+/);
            box.textContent = (parts[parts.length - 1] || '?').charAt(0);
        }
        return box;
    }

    function closeCongrats() {
        $('#congrats').hidden = true;
        state.phase = 'idle';
        Sound.stopSfx('congrats');
        Sound.duck(false);
        if (Sound.hold) Sound.resumeBgLater(10000);       // bấm Xác nhận (đóng màn chúc mừng) + 10 giây không quay tiếp → phát tiếp nhạc nền
        if (state.needReload) { state.needReload = false; loadAll(); }
        else { state.needRefresh = false; refreshData(false); }
        renderAll();
    }

    function goNextUnfinished() {
        var n = state.prizes.length;
        for (var k = 1; k <= n; k++) {
            var j = (state.idx + k) % n;
            if (state.prizes[j].remaining > 0) { selectPrize(j); return; }
        }
    }

    function selectPrize(i) {
        if (state.phase !== 'idle' || i < 0 || i >= state.prizes.length) return;
        state.idx = i;
        state.viewPrizeId = state.prizes[i].prizeId;
        state.freshBatch = null;
        buildSlots(wantedSlots());
        renderAll();
        Sound.update();
    }

    // ------------------------------------------------------------------ Vắng mặt – quay lại
    /** "Bỏ qua" ở màn chúc mừng: xác nhận → huỷ kết quả + đánh dấu vắng mặt → quay người khác. */
    function skipWinner(w) {
        confirmAbsent(w).then(function (ok) {
            if (!ok) return;
            closeCongrats();
            api('/api/results/' + w.resultId + '/absent', { method: 'POST' }).then(function () {
                toast(T('absentDone'));
                state.freshBatch = null;
                return refreshData(true);
            }).catch(function (e) { toast(e.message, true); refreshData(true); });
        });
    }

    // ------------------------------------------------------------------ Popup xác nhận vắng mặt (thay cho window.confirm)
    var cfm = { resolve: null, last: null };
    function confirmAbsent(person) {
        var box = $('#confirmModal'), info = $('#cfmPerson');
        info.innerHTML = '';
        info.appendChild(photoEl(person, 'cfm-photo'));
        var txt = el('div', 'cfm-who');
        txt.appendChild(el('div', 'cfm-code', person.code));
        txt.appendChild(el('div', 'cfm-name', person.name));
        if (person.department) txt.appendChild(el('div', 'cfm-sub', person.department));
        info.appendChild(txt);
        if (cfm.resolve) cfm.resolve(false);
        cfm.last = document.activeElement;
        box.hidden = false;
        setTimeout(function () { box.querySelector('[data-cfm="no"]').focus(); }, 30);
        return new Promise(function (resolve) { cfm.resolve = resolve; });
    }
    function closeConfirm(ok) {
        var box = $('#confirmModal');
        if (box.hidden) return;
        box.hidden = true;
        var r = cfm.resolve;
        cfm.resolve = null;
        if (cfm.last && cfm.last.focus) try { cfm.last.focus(); } catch (e) { /* bỏ qua */ }
        if (r) r(ok);
    }

    // ------------------------------------------------------------------ Hiển thị
    function renderAll() {
        renderPrize();
        renderButton();
        if (state.ev) sizeSlots();   // tính lại cỡ ô cho vừa khu quay số (cột phải)
    }

    function renderPrize() {
        var p = cur(), idle = state.phase === 'idle';
        $('#prev').disabled = !idle || state.idx <= 0;
        $('#next').disabled = !idle || state.idx >= state.prizes.length - 1;
        var img = $('#prizeImg'), medal = $('#medal');
        if (!p) {
            $('#prizeName').textContent = T('noPrizes');
            img.hidden = true;
            medal.hidden = true;
            $('#prizeReward').hidden = true;
            $('#progressTxt').textContent = '';
            $('#warn').style.visibility = 'hidden';
            return;
        }
        $('#prizeName').textContent = p.name;
        // Có ảnh phần thưởng thì hiện ảnh, không thì hiện huy hiệu ghi số suất của giải
        if (p.imagePath) { img.src = p.imagePath; img.hidden = false; medal.hidden = true; }
        else { img.hidden = true; medal.hidden = false; $('#medalNum').textContent = p.quantity; }
        // Dưới ảnh: tên phần thưởng (vd. "Tivi Xiaomi") + số suất
        $('#prizeReward').hidden = false;
        $('#prizeRewardName').textContent = p.reward || '';
        $('#prizeRewardName').hidden = !p.reward;
        $('#prizeQty').textContent = T('prizeQty', p.quantity, p.drawPerTurn);
        $('#progressTxt').textContent = T('progress', p.won, p.quantity);
        // Giữ chỗ cố định cho dòng cảnh báo (ẩn bằng visibility) để bố cục không nhảy khi nó hiện / ẩn
        var warn = $('#warn');
        warn.style.visibility = (p.remaining > 0 && p.eligible < p.nextTake) ? 'visible' : 'hidden';
        warn.textContent = T('notEnough', p.eligible);
    }

    // Con trỏ chuột theo theme. [điểm bấm x, y, đuôi file] – khớp ThemeOptions.Cursors (ThemeController.cs)
    var CURSORS = { gold: [3, 2], wand: [5, 5], blossom: [16, 16], lantern: [16, 1], coin: [16, 16], rose: [3, 2], scepter: [15, 15],
        dragon: [20, 20, 'png'] };
    var cursorToken = 0;
    function applyCursor(key, sizePct) {
        var root = document.documentElement.style, h = CURSORS[key || 'gold'], k = (sizePct || 100) / 100, token = ++cursorToken;
        // hiệu ứng động kèm con trỏ: đũa thần → tia pháo bông, quyền trượng → hào quang, rồng → rồng vờn ngọc (to theo cỡ con trỏ)
        if (window.CursorFx) CursorFx.setMode({ wand: 'wand', scepter: 'halo', dragon: 'dragon' }[key] || null, k);
        if (!h) {                                          // 'system': con trỏ mặc định của máy
            root.setProperty('--cur-default', 'default');
            root.setProperty('--cur-pointer', 'pointer');
            return;
        }
        var u = '/images/cursor/' + (key || 'gold'), ext = '.' + (h[2] || 'svg') + '?v=3';   // ?v= đổi khi sửa ảnh con trỏ → trình duyệt tải ảnh mới
        function set(a, b, f) {
            var hs = ' ' + Math.round(h[0] * f) + ' ' + Math.round(h[1] * f);
            root.setProperty('--cur-default', "url('" + a + "')" + hs + ', default');
            root.setProperty('--cur-pointer', "url('" + b + "')" + hs + ', pointer');
        }
        set(u + ext, u + '-hover' + ext, 1);               // cỡ gốc trước, phóng to xong thì thay
        if (Math.abs(k - 1) < 0.01) return;
        // Trình duyệt không phóng to ảnh con trỏ bằng CSS → vẽ lại ảnh ở cỡ mới ra PNG (data URL). Tối đa 128px (giới hạn của Chrome).
        function scaled(src, cb) {
            var im = new Image();
            im.onload = function () {
                var f = Math.min(k, 128 / Math.max(im.naturalWidth, im.naturalHeight)),
                    c = document.createElement('canvas'), g = c.getContext('2d');
                c.width = Math.round(im.naturalWidth * f); c.height = Math.round(im.naturalHeight * f);
                g.imageSmoothingQuality = 'high';
                g.drawImage(im, 0, 0, c.width, c.height);
                cb(c.toDataURL('image/png'), f);
            };
            im.src = src;
        }
        scaled(u + ext, function (a, f) {
            scaled(u + '-hover' + ext, function (b) { if (token === cursorToken) set(a, b, f); });
        });
    }

    function renderButton() {
        var b = $('#btnStart'), p = cur();
        document.body.classList.toggle('drawing', state.phase === 'spinning' || state.phase === 'stopping');
        b.classList.remove('stop');
        if (!LD.canDraw) { b.hidden = true; return; }
        b.hidden = false;
        b.disabled = false;
        var label = loc(state.ev && state.ev.theme.startButton && state.ev.theme.startButton.label) || T('start');
        switch (state.phase) {
            case 'spinning':
                if (p && p.drawType !== 2) { b.textContent = T('stop'); b.classList.add('stop'); }
                else { b.textContent = T('drawing'); b.disabled = true; }
                break;
            case 'stopping':
                b.textContent = T('drawing');
                b.disabled = true;
                break;
            case 'congrats':
                b.textContent = T('continue');
                break;
            default:
                if (!p) { b.textContent = label; b.disabled = true; }
                else if (p.remaining > 0) b.textContent = label;
                else if (state.prizes.some(function (x) { return x.remaining > 0; })) b.textContent = T('nextPrize');
                else { b.textContent = T('allDone'); b.disabled = true; }
        }
    }

    // ------------------------------------------------------------------ Bảng thông tin giải thưởng (chỉ xem)
    function openPrizes() {
        var body = $('#prizeRows'), qty = 0, won = 0;
        body.innerHTML = '';
        state.prizes.slice().reverse().forEach(function (p, i) {
            qty += p.quantity;
            won += p.won;
            var tr = el('tr');
            if (cur() && cur().prizeId === p.prizeId) tr.className = 'current';
            var n = el('td', 'c');
            n.appendChild(el('span', 'dm-num', String(i + 1)));
            tr.appendChild(n);

            var name = el('td'), wrap = el('div', 'dm-prize');
            if (p.imagePath) {
                var img = document.createElement('img');
                img.src = p.imagePath;
                img.alt = '';
                wrap.appendChild(img);
            } else {
                wrap.appendChild(el('span', 'dm-medal', String(p.quantity)));
            }
            var txt = el('div', 'dm-prize-text');
            txt.appendChild(el('b', null, p.name));
            if (p.reward) txt.appendChild(el('span', 'dm-reward', p.reward));   // vd. "20 triệu tiền mặt"
            wrap.appendChild(txt);
            name.appendChild(wrap);
            tr.appendChild(name);

            tr.appendChild(el('td', 'c dm-strong', String(p.quantity)));
            tr.appendChild(el('td', 'c', String(p.drawPerTurn)));

            var prog = el('td'), bar = el('div', 'dm-progress');
            var fill = el('i');
            fill.style.width = (p.quantity ? Math.min(100, p.won * 100 / p.quantity) : 0) + '%';
            bar.appendChild(fill);
            prog.appendChild(el('span', 'dm-prog-txt', p.won + ' / ' + p.quantity));
            prog.appendChild(bar);
            tr.appendChild(prog);

            var rem = el('td', 'c');
            rem.appendChild(p.remaining > 0 ? el('span', 'dm-code', String(p.remaining)) : el('span', 'dm-tag done', T('doneTag')));
            tr.appendChild(rem);

            body.appendChild(tr);
        });
        if (!state.prizes.length) {
            var tr = el('tr'), td = el('td', 'dm-empty-cell', T('noPrizes'));
            td.colSpan = 6;
            tr.appendChild(td);
            body.appendChild(tr);
        }
        $('#prizeTotal').innerHTML = '';
        $('#prizeTotal').appendChild(boldText(T('prizeTotal', '\u0001' + qty + '\u0001', '\u0001' + state.prizes.length + '\u0001', '\u0001' + won + '\u0001')));
        $('#prizeModal').hidden = false;
    }

    /** Chuỗi có số được bao bởi \u0001 → in đậm phần số. */
    function boldText(str) {
        var frag = document.createDocumentFragment();
        str.split('\u0001').forEach(function (part, i) {
            frag.appendChild(i % 2 ? el('b', null, part) : document.createTextNode(part));
        });
        return frag;
    }

    // ------------------------------------------------------------------ "Kết quả tổng": bảng kết quả quay số
    function openSummary() {
        var sel = $('#sumPrize'), keep = sel.value;
        sel.innerHTML = '';
        var all = el('option', null, T('allPrizes'));
        all.value = '';
        sel.appendChild(all);
        state.prizes.forEach(function (p) {
            var o = el('option', null, p.name + ' (' + p.won + '/' + p.quantity + ')');
            o.value = String(p.prizeId);
            sel.appendChild(o);
        });
        sel.value = keep && state.prizes.some(function (p) { return String(p.prizeId) === keep; }) ? keep : '';
        renderSummary();
        $('#sumModal').hidden = false;
    }

    // Bỏ dấu tiếng Việt + chữ thường để tìm không dấu ("nguyen van a" khớp "Nguyễn Văn A")
    function fold(s) {
        return String(s || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
    }

    function renderSummary() {
        var q = fold(($('#sumSearch').value || '').trim()), pid = $('#sumPrize').value;
        var prizeCount = {};
        state.results.forEach(function (r) { prizeCount[r.prizeId] = 1; });
        $('#sumTotal').innerHTML = '';
        $('#sumTotal').appendChild(boldText(T('sumTotal', '\u0001' + state.results.length + '\u0001', '\u0001' + Object.keys(prizeCount).length + '\u0001')));

        var total = state.results.length, body = $('#sumRows'), shown = 0;
        body.innerHTML = '';
        state.results.forEach(function (r, i) {               // mới nhất trước, STT giảm dần như thứ tự quay
            if (pid && String(r.prizeId) !== pid) return;
            if (q && fold([r.code, r.name, r.department, r.prizeName].join(' ')).indexOf(q) < 0) return;
            shown++;
            var tr = el('tr');
            var n = el('td', 'c');
            n.appendChild(el('span', 'dm-num', String(total - i)));
            tr.appendChild(n);
            var who = el('td'), w = el('div', 'dm-person');
            w.appendChild(photoEl(r, 'dm-photo'));
            w.appendChild(el('b', null, r.name));
            who.appendChild(w);
            tr.appendChild(who);
            tr.appendChild(el('td')).appendChild(el('span', 'dm-code', r.code));
            tr.appendChild(el('td', 'dm-muted', r.department || ''));
            tr.appendChild(el('td')).appendChild(el('span', 'dm-tag prize', r.prizeName));
            body.appendChild(tr);
        });
        $('#sumEmpty').hidden = shown > 0;
    }

    // ------------------------------------------------------------------ "Kết quả vừa quay": màn chúc mừng toàn màn hình
    function openLast() {
        if (state.phase !== 'idle') return;
        api('/api/view/results/last').then(function (winners) {
            if (!winners || !winners.length) { toast(T('noLast')); return; }
            state.phase = 'congrats';
            showLastFull(winners, 'last');
            renderButton();
        }).catch(function (e) { toast(e.message, true); });
    }

    /**
     * mode 'draw': ngay sau lượt quay – [Xác nhận] giữ kết quả, [Bỏ qua] huỷ kết quả (người vắng mặt) để quay người khác.
     * mode 'last': mở lại từ nút "Kết quả vừa quay" – [Xem tất cả] [Đóng].
     */
    function showLastFull(winners, mode) {
        var box = $('#congrats'), wrap = el('div', 'cgf');
        var bgImg = $('.bg-img').style.backgroundImage;
        box.style.background = bgImg && bgImg !== 'none' ? bgImg + ' center / cover no-repeat, var(--bg)' : '';
        var hello = loc(state.ev.theme.congratsTitle) || T('congrats');
        if (winners.length === 1) {
            // 1 người trúng: đặt trong khung vàng kiểu khung tranh (hoạ tiết 4 góc, ánh kim chạy, băng "Chúc mừng")
            var w = winners[0], fr = el('div', 'cgf-frame');
            ['tl', 'tr', 'bl', 'br'].forEach(function (k) { fr.appendChild(el('span', 'cf-corner ' + k)); });
            fr.appendChild(el('span', 'cf-shine'));
            fr.appendChild(el('div', 'cgf-hello cf-ribbon', hello));
            if (w.photo) fr.appendChild(photoEl(w, 'cgf-photo'));
            fr.appendChild(el('div', 'cgf-code', w.code));
            fr.appendChild(el('div', 'cgf-name', w.name));
            if (w.department) fr.appendChild(el('div', 'cgf-pos', w.department));
            fr.appendChild(el('div', 'cgf-won', T('wonLabel')));
            fr.appendChild(el('div', 'cgf-prize', w.prizeName));
            wrap.appendChild(fr);
        } else {
            wrap.appendChild(el('div', 'cgf-hello', hello));
            wrap.appendChild(el('div', 'cgf-won', T('wonLabel')));
            wrap.appendChild(el('div', 'cgf-prize', winners[0].prizeName));
            var grid = el('div', 'cgf-grid' + (winners.length > 8 ? ' dense' : '')), anyPhoto = winners.some(function (x) { return !!x.photo; });
            winners.forEach(function (x, i) {
                var c = el('div', 'cgf-card');
                c.style.animationDelay = Math.min(i * 70, 2000) + 'ms';
                c.style.setProperty('--len', String(Math.max(4, String(x.code || '').length)));   // co chữ mã theo độ dài
                if (anyPhoto) c.appendChild(photoEl(x, 'cgf-mini-photo'));
                c.appendChild(el('div', 'cgf-code', x.code));
                c.appendChild(el('div', 'cgf-name', x.name));
                if (x.department) c.appendChild(el('div', 'cgf-pos', x.department));
                grid.appendChild(c);
            });
            wrap.appendChild(grid);
        }
        var actions = el('div', 'cgf-actions');
        function action(label, cls, fn) {
            var b = el('button', 'cgf-btn ' + cls, label);
            b.type = 'button';
            b.addEventListener('click', function (e) { e.stopPropagation(); fn(); });
            actions.appendChild(b);
        }
        if (mode === 'draw') {
            action(T('confirm'), 'gold', closeCongrats);
            // Bỏ qua chỉ cho MC/Admin và chỉ khi lượt này có 1 người (nhiều người thì huỷ từng người ở bảng kết quả)
            if (LD.canDraw && winners.length === 1) action(T('skip'), 'ghost', function () { skipWinner(winners[0]); });
        } else {
            action(T('viewAll'), 'ghost', function () { closeCongrats(); openSummary(); });
            action(T('close'), 'gold', closeCongrats);
        }
        box.innerHTML = '';
        box.className = 'congrats full';
        box.appendChild(wrap);
        box.appendChild(actions);
        box.hidden = false;
    }

    var toastTimer = 0;
    function toast(msg, isErr) {
        var t = $('#toast');
        t.textContent = msg;
        t.className = 'toast' + (isErr ? ' err' : '');
        t.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.hidden = true; }, isErr ? 6000 : 3500);
    }

    // ------------------------------------------------------------------ Âm thanh
    var Sound = {
        cfg: null,
        muted: store.get('ld-muted') === '1',
        unlocked: false,
        ducked: false,
        bg: new Audio(),
        current: null,
        pi: 0,
        loops: {},
        configure: function (cfg) {
            this.cfg = cfg || { mode: 'none', volume: 70, playlist: [], prizeTracks: {}, effects: {} };
            // Tải trước các hiệu ứng (file ngắn) để lần phát đầu không bị trễ
            this.preloaded = [];
            for (var k in this.cfg.effects) {
                var pa = new Audio();
                pa.preload = 'auto';
                pa.src = this.cfg.effects[k];
                this.preloaded.push(pa);
            }
            this.pi = 0;
            this.current = undefined;
            this.update();
            this.renderButton();
        },
        base: function () { return this.muted || !this.cfg ? 0 : Math.max(0, Math.min(1, this.cfg.volume / 100)); },
        applyVolume: function () {
            var v = this.base();
            this.bg.volume = v * (this.ducked && this.cfg && this.cfg.duckOnSpin ? 0.25 : 1);
            for (var k in this.loops) this.loops[k].volume = v;
        },
        pickSrc: function () {
            var c = this.cfg, p = cur();
            if (!c || c.mode === 'none') return null;
            // Nhạc nền chung của sự kiện: dùng khi giải chưa có nhạc riêng / danh sách phát trống
            var main = c.main || null;
            if (c.mode === 'perPrize') { this.bg.loop = true; return (p && c.prizeTracks[String(p.prizeId)]) || main; }
            if (!c.playlist.length) { this.bg.loop = true; return main; }
            this.bg.loop = c.playlist.length === 1;
            return c.playlist[this.pi % c.playlist.length];
        },
        update: function () {
            var src = this.pickSrc();
            if (src !== this.current) {
                this.current = src;
                if (src) { this.bg.src = src; if (!this.muted) this.tryPlay(); }
                else { this.bg.pause(); this.bg.removeAttribute('src'); }
            }
            $('#soundHint').hidden = !(src && !this.unlocked && !this.muted);
        },
        // Vừa mở trang đã thử phát nhạc nền; trình duyệt chặn tự phát thì chờ lần bấm/phím đầu tiên (unlock)
        // Tạm dừng nhạc nền khi quay; quay xong hẹn giờ phát tiếp (đúng chỗ đang dừng). Quay tiếp trước khi hết giờ → huỷ hẹn.
        hold: false,
        resumeTimer: 0,
        holdBg: function () {
            clearTimeout(this.resumeTimer);
            this.hold = true;
            if (!this.bg.paused) this.bg.pause();
        },
        resumeBgLater: function (ms) {
            var self = this;
            clearTimeout(this.resumeTimer);
            this.resumeTimer = setTimeout(function () {
                self.hold = false;
                if (self.current && !self.muted && self.unlocked) self.play();
            }, ms);
        },
        tryPlay: function () {
            if (this.hold) return;
            if (this.unlocked) { this.play(); return; }
            var self = this;
            this.applyVolume();
            var pr = this.bg.play();
            if (pr && pr.then) pr.then(function () { self.unlocked = true; $('#soundHint').hidden = true; }, function () { });
        },
        play: function () {
            if (this.hold) return;                        // đang quay: giữ im nhạc nền
            this.applyVolume();
            var pr = this.bg.play();
            if (pr && pr.catch) pr.catch(function () { /* trình duyệt chặn tự phát */ });
        },
        next: function () {
            var c = this.cfg;
            if (!c || c.mode !== 'single' || c.playlist.length < 2) return;
            if (c.playlistEnd === 'shuffle') {
                var n;
                do { n = (Math.random() * c.playlist.length) | 0; } while (n === this.pi % c.playlist.length);
                this.pi = n;
            } else this.pi++;
            this.update();
        },
        unlock: function () {
            if (this.unlocked) return;
            this.unlocked = true;
            if (this.current && !this.muted) this.play();
            $('#soundHint').hidden = true;
        },
        last: {},
        sfx: function (name, vol) {
            var src = this.cfg && this.cfg.effects[name];
            if (!src || this.muted || !this.unlocked) return;
            var a = new Audio(src);
            a.volume = this.base() * (vol || 1);
            this.last[name] = a;   // giữ lại để dừng được (vd. tiếng reo hò khi đóng màn chúc mừng)
            var pr = a.play();
            if (pr && pr.catch) pr.catch(function () { });
        },
        /** Dừng hiệu ứng đang phát, giảm dần âm lượng cho êm. */
        stopSfx: function (name) {
            var a = this.last[name];
            delete this.last[name];
            if (!a || a.paused) return;
            var t = setInterval(function () {
                if (a.volume <= 0.06) { clearInterval(t); a.pause(); return; }
                a.volume = Math.max(0, a.volume - 0.06);
            }, 30);
        },
        loop: function (name, on) {
            var a = this.loops[name];
            if (!on) { if (a) { a.pause(); delete this.loops[name]; } return; }
            var src = this.cfg && this.cfg.effects[name];
            if (!src || this.muted || !this.unlocked || a) return;
            a = new Audio(src);
            a.loop = true;
            a.volume = this.base();
            this.loops[name] = a;
            var pr = a.play();
            if (pr && pr.catch) pr.catch(function () { });
        },
        duck: function (on) { this.ducked = on; this.applyVolume(); },
        toggle: function () {
            this.muted = !this.muted;
            store.set('ld-muted', this.muted ? '1' : '0');
            if (this.muted) {
                this.bg.pause();
                for (var k in this.loops) this.loops[k].pause();
                for (var n in this.last) this.last[n].pause();
                this.loops = {};
                this.last = {};
            }
            else { this.unlock(); if (this.current) this.play(); }
            this.applyVolume();
            this.renderButton();
            this.update();
        },
        renderButton: function () {
            var b = $('#btnSound');
            b.title = this.muted ? T('soundOn') : T('soundOff');
            $('#icoSoundOn').style.display = this.muted ? 'none' : '';
            $('#icoSoundOff').style.display = this.muted ? '' : 'none';
        }
    };
    Sound.bg.addEventListener('ended', function () { Sound.next(); });

    // ------------------------------------------------------------------ Vinh danh cuối chương trình
    // Tự mở 5 giây sau khi giải cuối cùng quay đủ người (và MC đã đóng màn chúc mừng lượt cuối).
    // Danh sách người trúng theo thứ tự giải chạy dần lên như "credits" phim; hết thì dừng ở lời chúc 10 giây
    // rồi tự đóng. Chỉ dịch chuyển 1 lớp bằng transform (GPU) nên máy yếu vẫn mượt. Phím V: mở / đóng bằng tay.
    var HONOR_DELAY_MS = 5000, HONOR_END_MS = 10000;

    function allPrizesDone() {
        return state.prizes.length > 0 && state.prizes.every(function (p) { return p.remaining <= 0; });
    }

    /**
     * Gọi sau mỗi lần tải dữ liệu. Chỉ hẹn mở vinh danh khi vừa CHUYỂN từ "còn giải" sang "xong hết" trong lúc
     * đang chiếu (tải lại trang khi đã xong hết thì không tự mở). Huỷ hẹn nếu lại có giải còn suất (vd. vắng mặt).
     */
    function trackAllDone() {
        var done = allPrizesDone();
        if (done && state.allDone === false) state.honorPending = true;
        if (!done) { state.honorPending = false; clearTimeout(state.honorTimer); }
        state.allDone = done;
        var b = $('#btnHonor');
        if (b) {
            b.disabled = !done;
            b.title = b.getAttribute(done ? 'data-title-on' : 'data-title-off');
        }
        autoHonor();
    }

    function autoHonor() {
        if (!state.honorPending || state.phase !== 'idle' || Honor.open) return;
        clearTimeout(state.honorTimer);
        state.honorTimer = setTimeout(function () {
            if (!state.honorPending || state.phase !== 'idle' || !allPrizesDone()) return;
            state.honorPending = false;
            Honor.show();
        }, HONOR_DELAY_MS);
    }

    var Honor = {
        open: false,
        paused: false,
        speed: 1,            // hệ số tốc độ (↑ ↓)
        y: 0,                // đã cuộn bao nhiêu px
        hold: 0,             // thời điểm (performance.now) tự đóng khi đang dừng ở lời chúc cuối; 0 = đang chạy
        raf: 0,
        last: 0,
        show: function () {
            if (this.open || state.phase !== 'idle' || !state.ev || !allPrizesDone()) return;   // chỉ khi đã quay xong hết
            var self = this;
            state.honorPending = false;          // mở bằng tay (phím V) thì không tự mở lại nữa
            clearTimeout(state.honorTimer);
            $$('.modal').forEach(function (m) { m.hidden = true; });
            refreshData(false).then(function () {
                state.honorPending = false;
                clearTimeout(state.honorTimer);
                if (self.open || state.phase !== 'idle') return;
                self.render();
                self.start();
            });
        },
        render: function () {
            var track = $('#honorTrack'), total = 0, by = {};
            track.innerHTML = '';
            $('#honorTitle').textContent = state.ev.name;
            state.results.forEach(function (r) { (by[r.prizeId] = by[r.prizeId] || []).push(r); });
            state.prizes.forEach(function (p) {
                var list = by[p.prizeId];
                if (!list || !list.length) return;
                list.sort(function (a, b) { return a.resultId - b.resultId; });   // theo thứ tự đã quay
                total += list.length;
                var sec = el('section', 'honor-prize' + (list.length <= 3 ? ' few' : ''));
                var head = el('div', 'hp-head');
                if (p.imagePath) {
                    var im = document.createElement('img');
                    im.className = 'hp-img';
                    im.src = p.imagePath;
                    im.alt = '';
                    head.appendChild(im);
                }
                var txt = el('div', 'hp-text');
                txt.appendChild(el('h3', 'hp-name gold-text', p.name));
                txt.appendChild(el('div', 'hp-count', T('honorCount', list.length)));
                head.appendChild(txt);
                sec.appendChild(head);
                var grid = el('div', 'hp-grid');
                list.forEach(function (w) {
                    var card = el('div', 'hp-card');
                    var ph = photoEl(w, 'hp-photo'), img = ph.querySelector('img');
                    if (img) img.loading = 'lazy';
                    card.appendChild(ph);
                    var info = el('div', 'hp-info');
                    info.appendChild(el('div', 'hp-person', w.name));
                    var meta = w.department || '';
                    if (meta) info.appendChild(el('div', 'hp-meta', meta));
                    info.appendChild(el('div', 'hp-code', w.code));
                    card.appendChild(info);
                    grid.appendChild(card);
                });
                sec.appendChild(grid);
                track.appendChild(sec);
            });
            var end = el('div', 'honor-end');
            end.appendChild(el('div', 'he-big gold-text', total ? T('honorThanks') : T('honorEmpty')));
            track.appendChild(end);
        },
        start: function () {
            this.open = true;
            this.paused = false;
            this.hold = 0;
            $('#honor').hidden = false;
            this.fit();
            this.y = -this.viewH * 0.8;             // bắt đầu từ gần đáy màn hình
            this.apply();
            StageFx.setAmbient('none');             // nền bị che hết → tắt hiệu ứng nền cho nhẹ máy
            StageFx.burst(state.ev.theme.congratsEffect || 'confetti');
            Sound.duck(false);
            Sound.sfx('congrats');
            this.last = performance.now();
            this.raf = requestAnimationFrame(this.frame);
        },
        close: function () {
            if (!this.open) return;
            this.open = false;
            cancelAnimationFrame(this.raf);
            $('#honor').hidden = true;
            $('#honorTrack').innerHTML = '';
            StageFx.setAmbient(state.ev.theme.stageEffect);
            Sound.stopSfx('congrats');
            if (state.needReload) { state.needReload = false; loadAll(); }
        },
        /** Đo lại khung nhìn; lời chúc cuối cao bằng khung nhìn để dừng đúng giữa màn hình. */
        fit: function () {
            this.viewH = $('#honorView').clientHeight;
            var end = $('#honorTrack .honor-end');
            if (end) end.style.minHeight = this.viewH + 'px';
        },
        maxY: function () { return Math.max(0, $('#honorTrack').scrollHeight - this.viewH); },
        apply: function () { $('#honorTrack').style.transform = 'translate3d(0,' + (-this.y).toFixed(1) + 'px,0)'; },
        frame: function (now) {
            var h = Honor, dt = Math.min(100, now - h.last);
            h.last = now;
            if (!h.open) return;
            if (h.paused && h.hold > 0) h.hold += dt;        // tạm dừng thì hoãn luôn giờ tự đóng
            if (!h.paused) {
                if (h.hold > 0) {
                    // đếm theo đồng hồ thật (không cộng dồn theo khung hình) → đúng 10 giây kể cả máy yếu ít khung hình
                    if (now >= h.hold) { h.close(); return; }    // dừng ở lời chúc đủ lâu → tự đóng
                } else {
                    h.y += h.viewH * 0.06 * h.speed * dt / 1000;   // ~65px/giây ở màn 1080p
                    var max = h.maxY();
                    if (h.y >= max) { h.y = max; h.hold = now + HONOR_END_MS; }
                }
                h.apply();
            }
            h.raf = requestAnimationFrame(h.frame);
        },
        scrollBy: function (dy) {
            this.y = Math.max(-this.viewH * 0.8, Math.min(this.maxY(), this.y + dy));
            this.hold = 0;
            this.apply();
        },
        key: function (e) {
            var k = e.key;
            if (k === 'Escape' || k === 'v' || k === 'V') this.close();
            else if (isStartKey(e)) { if (!e.repeat) this.paused = !this.paused; }
            else if (k === 'ArrowUp') this.speed = Math.min(4, this.speed * 1.25);
            else if (k === 'ArrowDown') this.speed = Math.max(0.25, this.speed / 1.25);
            else if (k === 'PageDown') this.scrollBy(this.viewH * 0.8);
            else if (k === 'PageUp') this.scrollBy(-this.viewH * 0.8);
            else if (k === 'f' || k === 'F') { toggleFullscreen(); return; }
            else if (k === 'm' || k === 'M') { Sound.toggle(); return; }
            else return;
            e.preventDefault();
        }
    };

    // ------------------------------------------------------------------ Người tham dự
    var people = { page: 1, q: '', timer: 0, size: 50, req: 0 };
    function openPeople() {
        $('#peopleModal').hidden = false;
        $('#peopleSearch').value = people.q;
        loadPeople(1);
        setTimeout(function () { $('#peopleSearch').focus(); }, 50);
    }
    function loadPeople(page) {
        people.page = page;
        var req = ++people.req;
        api('/api/view/participants?page=' + page + '&q=' + encodeURIComponent(people.q)).then(function (d) {
            if (req !== people.req) return;                    // bỏ kết quả của lần gõ tìm cũ
            var body = $('#peopleRows'), base = (d.page - 1) * people.size;
            body.innerHTML = '';
            d.items.forEach(function (p, i) {
                var tr = el('tr');
                var n = el('td', 'c');
                n.appendChild(el('span', 'dm-num', String(base + i + 1)));
                tr.appendChild(n);
                var who = el('td'), w = el('div', 'dm-person');
                w.appendChild(photoEl(p, 'dm-photo'));
                w.appendChild(el('b', null, p.name));
                who.appendChild(w);
                tr.appendChild(who);
                tr.appendChild(el('td')).appendChild(el('span', 'dm-code', p.code));
                tr.appendChild(el('td', 'dm-muted', p.department || ''));
                var label = p.status === 'won' ? (p.prize || T('statusWon')) : p.status === 'absent' ? T('statusAbsent') : T('statusWaiting');
                tr.appendChild(el('td')).appendChild(el('span', 'dm-tag ' + p.status, label));
                body.appendChild(tr);
            });
            $('#peopleEmpty').hidden = d.items.length > 0;
            $('#peopleTotal').innerHTML = '';
            $('#peopleTotal').appendChild(boldText(T('peopleTotal', '\u0001' + d.total + '\u0001', '\u0001' + d.page + '\u0001', '\u0001' + Math.max(1, d.totalPages) + '\u0001')));
            renderPager($('#peoplePager'), d.page, Math.max(1, d.totalPages), loadPeople);
            var wrap = body.closest('.dm-body');
            if (wrap) wrap.scrollTop = 0;
        }).catch(function (e) { toast(e.message, true); });
    }

    /** Phân trang: ‹ 1 … 4 5 6 … 20 › */
    function renderPager(box, page, total, go) {
        box.innerHTML = '';
        if (total <= 1) return;
        function btn(label, target, cls, title) {
            var b = el('button', 'dm-page' + (cls ? ' ' + cls : ''), label);
            b.type = 'button';
            if (title) b.title = title;
            if (target == null) b.disabled = true;
            else b.addEventListener('click', function () { go(target); });
            box.appendChild(b);
        }
        btn('‹', page > 1 ? page - 1 : null, 'nav', T('prevPage'));
        var pages = [1], from = Math.max(2, page - 2), to = Math.min(total - 1, page + 2);
        if (from > 2) pages.push('…');
        for (var i = from; i <= to; i++) pages.push(i);
        if (to < total - 1) pages.push('…');
        if (total > 1) pages.push(total);
        pages.forEach(function (x) {
            if (x === '…') box.appendChild(el('span', 'dm-gap', '…'));
            else btn(String(x), x === page ? null : x, x === page ? 'active' : '');
        });
        btn('›', page < total ? page + 1 : null, 'nav', T('nextPage'));
    }

    // ------------------------------------------------------------------ Toàn màn hình & ẩn chuột
    function toggleFullscreen() {
        if (document.fullscreenElement) document.exitFullscreen();
        else if (document.documentElement.requestFullscreen) document.documentElement.requestFullscreen();
    }
    var idleTimer = 0;
    var btnIdleTimer = 0;
    function poke() {
        document.body.classList.remove('idle', 'btn-idle');
        clearTimeout(idleTimer);
        if (document.fullscreenElement) idleTimer = setTimeout(function () { document.body.classList.add('idle'); }, 2800);
        // nút Bắt đầu: không động chuột 10 giây thì mờ đi (cả khi không toàn màn hình)
        clearTimeout(btnIdleTimer);
        btnIdleTimer = setTimeout(function () { document.body.classList.add('btn-idle'); }, 10000);
    }

    // ------------------------------------------------------------------ Realtime (SignalR)
    function connectHub() {
        if (!window.signalR) return;
        // Thử kết nối lại mãi (mặc định SignalR bỏ cuộc sau ~4 lần), giãn dần tới 10 giây
        var conn = new signalR.HubConnectionBuilder().withUrl('/hubs/draw')
            .withAutomaticReconnect({ nextRetryDelayInMilliseconds: function (ctx) { return Math.min(10000, 1000 * (ctx.previousRetryCount + 1)); } })
            .build();
        conn.on('ResultChanged', function (eventId) {
            if (!state.ev || eventId !== state.ev.eventId) return;
            if (state.phase === 'idle') refreshData(false); else state.needRefresh = true;
        });
        conn.on('ConfigChanged', function (eventId) {
            if (!state.ev || eventId !== state.ev.eventId) return;
            if (state.phase === 'idle' && !Honor.open) loadAll(); else state.needReload = true;
        });
        conn.on('ActiveEventChanged', function () { if (state.phase === 'idle') location.reload(); else state.needReload = true; });
        conn.onreconnecting(function () { Conn.check(); });
        conn.onreconnected(function () { if (state.phase === 'idle') loadAll(); else state.needReload = true; });
        var start = function () { conn.start().catch(function () { setTimeout(start, 5000); }); };
        conn.onclose(function () { setTimeout(start, 5000); });   // phòng khi vẫn bị đóng hẳn
        start();
    }

    // ------------------------------------------------------------------ Khởi động
    function bind() {
        $('#btnStart').addEventListener('click', onStart);
        $('#prev').addEventListener('click', function () { selectPrize(state.idx - 1); });
        $('#next').addEventListener('click', function () { selectPrize(state.idx + 1); });
        $('#congrats').addEventListener('click', function () { if (state.phase === 'congrats') closeCongrats(); });
        $('#btnSound').addEventListener('click', function () { Sound.toggle(); });
        $('#btnFull').addEventListener('click', toggleFullscreen);
        $('#btnPeople').addEventListener('click', openPeople);
        $('#btnSummary').addEventListener('click', openSummary);
        $('#btnPrizes').addEventListener('click', openPrizes);
        $('#sumSearch').addEventListener('input', renderSummary);
        $('#sumPrize').addEventListener('change', renderSummary);
        $('#btnLast').addEventListener('click', openLast);
        $('#btnHonor').addEventListener('click', function () { Honor.show(); });
        $('#honorClose').addEventListener('click', function () { Honor.close(); });
        $('#honorView').addEventListener('click', function () { Honor.paused = !Honor.paused; });
        $('#honorView').addEventListener('wheel', function (e) { e.preventDefault(); Honor.scrollBy(e.deltaY); }, { passive: false });
        $$('.modal').forEach(function (m) {
            m.addEventListener('click', function (e) {
                if (e.target === m || (e.target.closest && e.target.closest('[data-close]'))) m.hidden = true;
            });
        });
        $('#peopleSearch').addEventListener('input', function (e) {
            clearTimeout(people.timer);
            people.timer = setTimeout(function () { people.q = e.target.value.trim(); loadPeople(1); }, 300);
        });
        ['pointerdown', 'keydown', 'touchend'].forEach(function (ev) {
            document.addEventListener(ev, function () { Sound.unlock(); }, { passive: true });
        });
        document.addEventListener('mousemove', poke);
        document.addEventListener('pointerdown', poke);
        poke();                                            // bắt đầu đếm 10 giây ngay khi mở trang
        document.addEventListener('fullscreenchange', poke);
        $('#confirmModal').addEventListener('click', function (e) {
            if (e.target === this) { closeConfirm(false); return; }
            var b = e.target.closest && e.target.closest('[data-cfm]');
            if (b) closeConfirm(b.getAttribute('data-cfm') === 'yes');
        });
        document.addEventListener('keydown', function (e) {
            if (!$('#confirmModal').hidden) {
                if (e.key === 'Escape') { e.preventDefault(); closeConfirm(false); }
                else if (e.key === 'Tab') {             // giữ focus trong popup
                    var btns = $$('#confirmModal [data-cfm]'), i = btns.indexOf(document.activeElement);
                    e.preventDefault();
                    btns[(i + (e.shiftKey ? -1 : 1) + btns.length) % btns.length].focus();
                }
                return;                                    // Enter/Space: để nút đang focus tự xử lý
            }
            if (Honor.open) { Honor.key(e); return; }
            // Chỉ coi là "đang gõ chữ" khi ô nhập còn hiện trên màn hình (ô tìm kiếm của popup đã đóng vẫn có thể giữ focus)
            var t = e.target;
            var typing = (/^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName) && t.offsetParent !== null)
                || !!(t.closest && t.closest('.lang-pick'));                    // đang thao tác menu ngôn ngữ
            var modalOpen = $$('.modal').some(function (m) { return !m.hidden; });
            if (e.key === 'Escape') { $$('.modal').forEach(function (m) { m.hidden = true; }); if (state.phase === 'congrats') closeCongrats(); return; }
            if (typing || modalOpen) return;
            if (isStartKey(e)) {
                e.preventDefault();
                // Focus còn ở 1 nút vừa bấm (vd. "Kết quả vừa quay") → bỏ focus để Space không bấm lại nút đó
                if (/^(BUTTON|A)$/.test(t.tagName)) t.blur();
                if (e.repeat || Date.now() - lastStartKey < 350) return;   // giữ nút / nút bị nảy → chỉ tính 1 lần
                lastStartKey = Date.now();
                onStart();
            }
            else if (e.key === 'ArrowLeft') selectPrize(state.idx - 1);
            else if (e.key === 'ArrowRight') selectPrize(state.idx + 1);
            else if (e.key === 'f' || e.key === 'F') toggleFullscreen();
            else if (e.key === 'm' || e.key === 'M') Sound.toggle();
            else if (e.key === 'v' || e.key === 'V') Honor.show();
        });
        // Nút bấm USB dạng CHUỘT (gửi nút giữa / Back / Forward) cũng là Bắt đầu / Dừng – khỏi cài X-Mouse.
        // e.button: 1 = nút giữa, 3 = Back, 4 = Forward. Chặn tác dụng gốc: Back/Forward làm trình duyệt lùi trang,
        // nút giữa bật chế độ cuộn tự động.
        var isStartButton = function (e) { return e.button === 1 || e.button === 3 || e.button === 4; };
        document.addEventListener('mousedown', function (e) {
            if (!isStartButton(e)) return;
            e.preventDefault();
            if (Date.now() - lastStartKey < 350) return;           // nút bị nảy → chỉ tính 1 lần
            lastStartKey = Date.now();
            if (!$('#confirmModal').hidden) return;
            if (Honor.open) { Honor.paused = !Honor.paused; return; }
            if ($$('.modal').some(function (m) { return !m.hidden; })) return;
            onStart();
        });
        ['mouseup', 'auxclick'].forEach(function (type) {
            document.addEventListener(type, function (e) { if (isStartButton(e)) e.preventDefault(); });
        });

        var rt = 0;
        window.addEventListener('resize', function () {
            clearTimeout(rt);
            rt = setTimeout(function () { sizeSlots(); if (Honor.open) Honor.fit(); }, 120);
        });
        if (/[?&]keys=1\b/.test(location.search)) keyDebug();
    }

    /**
     * Soi phím: mở màn chiếu với ?keys=1 (vd. https://.../?keys=1) rồi bấm nút / phím bất kỳ →
     * góc dưới bên trái hiện phím trình duyệt nhận được (để kiểm tra nút bấm USB gửi phím gì).
     */
    function keyDebug() {
        var box = el('div', 'key-debug');
        box.textContent = 'Bấm nút / phím bất kỳ để kiểm tra…';
        document.body.appendChild(box);
        var n = 0;
        ['keydown', 'keyup'].forEach(function (type) {
            document.addEventListener(type, function (e) {
                n++;
                box.textContent = '#' + n + ' ' + type + '  key="' + e.key + '"  code="' + e.code + '"  keyCode=' + e.keyCode
                    + (e.repeat ? '  (lặp)' : '') + '  →  ' + (isStartKey(e) ? 'BẮT ĐẦU/DỪNG ✓' : 'không dùng');
                box.classList.toggle('ok', isStartKey(e));
            }, true);
        });
        var names = { 0: 'trái', 1: 'giữa', 2: 'phải', 3: 'Back', 4: 'Forward' };
        document.addEventListener('mousedown', function (e) {
            n++;
            var ok = e.button === 1 || e.button === 3 || e.button === 4;
            box.textContent = '#' + n + ' chuột  button=' + e.button + ' (' + (names[e.button] || '?') + ')  →  '
                + (ok ? 'BẮT ĐẦU/DỪNG ✓' : 'không dùng');
            box.classList.toggle('ok', ok);
        }, true);
        document.addEventListener('wheel', function (e) {
            n++;
            box.textContent = '#' + n + ' lăn chuột  deltaY=' + Math.round(e.deltaY) + '  →  không dùng';
            box.classList.remove('ok');
        }, { capture: true, passive: true });
    }

    StageFx.init($('#fxAmbient'), $('#fxBurst'));
    bind();
    Sound.renderButton();
    loadAll();
    connectHub();
    Conn.start();
})();
