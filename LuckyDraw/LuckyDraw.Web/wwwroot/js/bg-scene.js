// LuckyDraw – hiệu ứng sống động cho ẢNH NỀN CÓ SẴN của chủ đề: 2 con dê thỉnh thoảng chớp mắt.
// Dê nằm sẵn trong ảnh nền, nên ta đặt "mí mắt" khớp đúng toạ độ mắt trên ảnh:
//   mí cùng màu da (lấy màu từ ảnh) khép xuống trong ~0,2 giây vào lúc ngẫu nhiên.
// Toạ độ tính theo pixel ẢNH GỐC; ảnh vẽ kiểu background-size: cover nên đổi ra màn hình bằng scale + offset.
// Ảnh nền khác (admin tải lên / chủ đề khác) thì không chạy vì toạ độ không còn đúng.
(function () {
    'use strict';

    var SCENES = {
        'tet-dinh-mui.jpg': {
            w: 2752, h: 1536,
            // tâm mắt, kích thước mí (px ảnh gốc), góc nghiêng theo dáng mắt
            eyes: [
                { cx: 617, cy: 805, w: 60, h: 34, rot: 22 },
                { cx: 2134, cy: 805, w: 60, h: 34, rot: -22 }
            ]
        }
    };

    var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var root = null, scene = null, url = '', lids = [], timers = [], rt = 0;

    function layer() {
        if (!root) {
            root = document.createElement('div');
            root.className = 'bg-scene';
            root.setAttribute('aria-hidden', 'true');
            var host = document.querySelector('.bg-layer');
            if (!host) return null;
            host.insertBefore(root, host.querySelector('.bg-img'));
        }
        return root;
    }

    function clear() {
        timers.forEach(clearTimeout);
        timers = [];
        lids = [];
        if (root) root.innerHTML = '';
    }

    /** Ảnh vẽ kiểu cover, căn giữa → tỉ lệ và độ lệch so với màn hình. */
    function fit() {
        // đo đúng khung đang vẽ ảnh nền (.bg-layer) – cùng cách tính với background-size: cover
        var host = root.parentNode, W = host.clientWidth || window.innerWidth, H = host.clientHeight || window.innerHeight;
        var s = Math.max(W / scene.w, H / scene.h);
        return { s: s, ox: (W - scene.w * s) / 2, oy: (H - scene.h * s) / 2 };
    }

    /** Màu da quanh mắt: trung bình một ô nhỏ ngay dưới mắt, đọc từ chính ảnh nền. */
    function sampleColors(img) {
        try {
            var c = document.createElement('canvas');
            c.width = scene.w; c.height = scene.h;
            var g = c.getContext('2d');
            g.drawImage(img, 0, 0);
            scene.eyes.forEach(function (e) {
                var d = g.getImageData(e.cx - 8, e.cy + 24, 16, 6).data, r = 0, gg = 0, b = 0, n = d.length / 4;
                for (var i = 0; i < d.length; i += 4) { r += d[i]; gg += d[i + 1]; b += d[i + 2]; }
                e.color = 'rgb(' + Math.round(r / n) + ',' + Math.round(gg / n) + ',' + Math.round(b / n) + ')';
            });
        } catch (err) { /* không đọc được ảnh → dùng màu mặc định */ }
    }

    function build() {
        clear();
        if (!scene || !layer()) return;
        var f = fit(), s = f.s;

        scene.eyes.forEach(function (e, i) {
            // Lớp ngoài: đúng khung mắt, nghiêng theo dáng mắt quanh TÂM; lớp trong (mí) khép từ mép trên xuống
            var eye = document.createElement('div');
            eye.className = 'bg-eye';
            eye.style.left = (f.ox + (e.cx - e.w / 2) * s) + 'px';
            eye.style.top = (f.oy + (e.cy - e.h / 2) * s) + 'px';
            eye.style.width = (e.w * s) + 'px';
            eye.style.height = (e.h * s) + 'px';
            eye.style.transform = 'rotate(' + e.rot + 'deg)';
            var lid = document.createElement('div');
            lid.className = 'bg-lid';
            if (e.color) lid.style.setProperty('--lid', e.color);
            eye.appendChild(lid);
            root.appendChild(eye);
            lids[i] = lid;
            if (!reduced) scheduleBlink(i, 2000 + Math.random() * 4000);
        });
    }

    function blink(i) {
        var lid = lids[i];
        if (!lid) return;
        lid.classList.remove('blink');
        void lid.offsetWidth;                     // chạy lại animation
        lid.classList.add('blink');
    }

    /** Chớp vào lúc ngẫu nhiên 4–9 giây; khoảng 1/5 số lần chớp đôi. */
    function scheduleBlink(i, delay) {
        timers.push(setTimeout(function () {
            if (!document.hidden) {
                blink(i);
                if (Math.random() < 0.2) timers.push(setTimeout(function () { blink(i); }, 320));
            }
            scheduleBlink(i, 4000 + Math.random() * 5000);
        }, delay));
    }

    /**
     * Gọi mỗi khi đổi giao diện. bgCss: giá trị background của chủ đề (vd. "url('/images/themes/tet-dinh-mui.jpg') …"),
     * null khi admin dùng ảnh nền tải lên → tắt hiệu ứng.
     */
    function apply(bgCss) {
        var m = bgCss && /url\(['"]?([^'")]+)['"]?\)/.exec(bgCss);
        var path = m ? m[1] : '', file = path.split('/').pop();
        var next = SCENES[file] || null;
        if (next === scene && path === url) return;
        scene = next;
        url = path;
        clear();
        if (!scene) return;
        var img = new Image();
        img.onload = function () { if (scene === next) { sampleColors(img); build(); } };
        img.src = path;
    }

    window.addEventListener('resize', function () {
        clearTimeout(rt);
        rt = setTimeout(function () { if (scene) build(); }, 150);
    });

    window.BgScene = { apply: apply };
})();
