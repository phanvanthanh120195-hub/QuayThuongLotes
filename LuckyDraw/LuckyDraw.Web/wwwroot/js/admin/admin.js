// LuckyDraw Admin – hành vi dùng chung (không chứa chữ hiển thị: chữ lấy từ data-* do server render theo ngôn ngữ)
(function () {
    'use strict';

    // Chọn xong là gửi form (đổi sự kiện, lọc…)
    document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
        el.addEventListener('change', function () { el.form && el.form.submit(); });
    });

    // Hỏi lại trước khi gửi form nguy hiểm: <form data-confirm="...">
    document.addEventListener('submit', function (e) {
        var form = e.target;
        var msg = form.getAttribute && form.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) {
            e.preventDefault();
        }
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
