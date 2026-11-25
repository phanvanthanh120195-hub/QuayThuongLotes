// LuckyDraw Admin – hành vi dùng chung (không chứa chữ hiển thị: chữ lấy từ data-* do server render theo ngôn ngữ)
(function () {
    'use strict';

    // Chọn xong là gửi form (đổi sự kiện, lọc…)
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () { el.form && el.form.submit(); });
    });

    // Hỏi lại trước khi gửi form nguy hiểm: <form data-confirm="...">
    // Dùng popup #confirmModal (_AdminLayout) thay cho confirm() của trình duyệt; thiếu popup thì quay về confirm().
    var cfmEl = document.getElementById('confirmModal');
    var cfmModal = cfmEl && window.bootstrap ? bootstrap.Modal.getOrCreateInstance(cfmEl) : null;
    var cfmPending = null;   // { form, submitter } đang chờ bấm Đồng ý
    if (cfmModal) {
        document.getElementById('confirmModalOk').addEventListener('click', function () {
            var p = cfmPending;
            cfmPending = null;
            cfmModal.hide();
            if (!p) return;
            p.form._confirmed = true;
            // requestSubmit giữ nút đã bấm (name/value) và chạy lại validate; trình duyệt cũ thì submit() thẳng
            if (p.form.requestSubmit) p.form.requestSubmit(p.submitter || undefined);
            else p.form.submit();
            // Form không hợp lệ thì không có sự kiện submit → xoá cờ để lần sau vẫn hỏi lại
            p.form._confirmed = false;
        });
        cfmEl.addEventListener('shown.bs.modal', function () { document.getElementById('confirmModalOk').focus(); });
        cfmEl.addEventListener('hidden.bs.modal', function () { cfmPending = null; });
    }
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var msg = form.getAttribute && form.getAttribute('data-confirm');
        if (!msg) return;
        if (form._confirmed) { form._confirmed = false; return; }   // đã bấm Đồng ý → cho gửi
        e.preventDefault();
        if (!cfmModal) { if (window.confirm(msg)) { form._confirmed = true; form.requestSubmit ? form.requestSubmit(e.submitter || undefined) : form.submit(); } return; }
        cfmPending = { form: form, submitter: e.submitter };
        document.getElementById('confirmModalMsg').textContent = msg;
        cfmModal.show();
    }, true);

    // Menu bên trên điện thoại
    var body = document.body;
    document.querySelectorAll('[data-sidebar-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () { body.classList.toggle('sidebar-open'); });
    });
    var backdrop = document.querySelector('.side-backdrop');
    if (backdrop) backdrop.addEventListener('click', function () { body.classList.remove('sidebar-open'); });

    // Nút xác nhận chỉ bật khi gõ đúng chữ yêu cầu: <input data-confirm-text="Tên" data-confirm-target="#btn">
    document.querySelectorAll('[data-confirm-text]').forEach(function (input) {
        var target = document.querySelector(input.getAttribute('data-confirm-target'));
        var expected = input.getAttribute('data-confirm-text');
        if (!target) return;
        var check = function () { target.disabled = input.value.trim() !== expected; };
        input.addEventListener('input', check);
        check();
    });

    // Xem trước ảnh trước khi upload: <input type=file data-preview="#img">
    document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
        input.addEventListener('change', function () {
            var img = document.querySelector(input.getAttribute('data-preview'));
            if (!img || !input.files || !input.files[0]) return;
            img.src = URL.createObjectURL(input.files[0]);
            img.hidden = false;
        });
    });

    // Ô nhập đường dẫn ảnh + xem trước:
    // <input data-image-path data-img="#a, #b" data-img-empty="#khiKhongCoAnh" data-img-error="#baoLoi">
    // Chuẩn hoá giống server (ImagePathInput.Normalize): "\" → "/", bỏ "~" / "wwwroot", thêm "/" ở đầu; URL http(s) giữ nguyên.
    function normalizeImagePath(s) {
        s = (s || '').trim().replace(/\\/g, '/');
        if (!s) return '';
        if (/^https?:\/\//i.test(s)) return s;
        if (s.indexOf('~/') === 0) s = s.slice(1);
        s = s.replace(/^\/?wwwroot\//i, '/');
        return s.charAt(0) === '/' ? s : '/' + s;
    }
    document.querySelectorAll('input[data-image-path]').forEach(function (input) {
        var all = function (attr) { var sel = input.getAttribute(attr); return sel ? Array.prototype.slice.call(document.querySelectorAll(sel)) : []; };
        var imgs = all('data-img'), empty = all('data-img-empty'), err = all('data-img-error');
        if (!imgs.length) return;
        var show = function (ok) {
            imgs.forEach(function (i) { i.hidden = !ok; });
            empty.forEach(function (e) { e.hidden = ok; });
        };
        var showErr = function (on) { err.forEach(function (e) { e.hidden = !on; }); };
        imgs[0].addEventListener('load', function () { show(true); showErr(false); });
        imgs[0].addEventListener('error', function () { if (imgs[0].getAttribute('src')) { show(false); showErr(true); } });
        var apply = function () {
            var p = normalizeImagePath(input.value);
            if (!p) { showErr(false); show(false); imgs.forEach(function (i) { i.removeAttribute('src'); }); return; }
            if (imgs[0].getAttribute('src') === p) return;
            showErr(false);
            imgs.forEach(function (i) { i.src = p; });
        };
        var timer;
        input.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(apply, 300); });
        input.addEventListener('change', apply);
        // Ảnh đã lưu nhưng hỏng sẵn từ lúc mở trang
        if (imgs[0].getAttribute('src') && imgs[0].complete && !imgs[0].naturalWidth) { show(false); showErr(true); }
    });

    // Xem trước tên 4 ngôn ngữ: <div data-lang-preview> … <span data-lang="vi|zhTW|zhCN|en">
    document.querySelectorAll('[data-lang-preview]').forEach(function (box) {
        var get = function (n) { var el = document.querySelector('[name="' + n + '"]'); return el ? el.value.trim() : ''; };
        var set = function (key, value, fallback) {
            var el = box.querySelector('[data-lang="' + key + '"]');
            if (!el) return;
            el.textContent = value || fallback || '—';
            el.classList.toggle('fallback', !value);
        };
        var update = function () {
            var vi = get('NameVi'), tw = get('NameZhTw'), cn = get('NameZhCn'), en = get('NameEn');
            set('vi', vi);
            set('zhTW', tw, vi);
            set('zhCN', cn, tw || vi);
            set('en', en, vi);
        };
        document.addEventListener('input', update);
        update();
    });

    // Ẩn/hiện ô theo kiểu quay: <div data-show-when="DrawType=2">
    document.querySelectorAll('[data-show-when]').forEach(function (block) {
        var parts = block.getAttribute('data-show-when').split('=');
        var radios = document.querySelectorAll('input[name="' + parts[0] + '"]');
        var update = function () {
            var checked = document.querySelector('input[name="' + parts[0] + '"]:checked');
            block.hidden = !checked || checked.value !== parts[1];
        };
        radios.forEach(function (r) { r.addEventListener('change', update); });
        update();
    });
})();
