-- =====================================================================
-- LuckyDraw – 02_seed.sql  (CHỈ DÙNG ĐỂ TEST)
-- 5 bộ phận, 1 sự kiện ngày 01/12/2026, 6 giải, 60 người tham dự
--   • NV001–NV050: vào làm từ 01/06/2026 trở về trước  → đủ 6 tháng (NV050 đúng mốc 01/06)
--   • NV051–NV060: vào làm từ 02/06/2026 trở về sau    → chưa đủ 6 tháng (NV051 đúng ngày 02/06)
-- Tài khoản admin KHÔNG tạo ở đây: app C# tự tạo lúc khởi động (mật khẩu phải băm bằng C#).
-- =====================================================================

INSERT INTO luckydraw.department (code, name_vi, name_zh_tw, name_zh_cn, name_en, sort_order) VALUES
    ('ADM', 'Hành chính – Nhân sự', '行政人事部', '行政人事部', 'HR & Admin', 1),
    ('ACC', 'Kế toán',              '會計部',     '会计部',     'Accounting', 2),
    ('IT',  'Công nghệ thông tin',  '資訊部',     '信息部',     'IT', 3),
    ('PRD', 'Sản xuất',             '生產部',     '生产部',     'Production', 4),
    ('SAL', 'Kinh doanh',           '業務部',     '业务部',     'Sales', 5);

INSERT INTO luckydraw.event (name_vi, name_zh_tw, name_zh_cn, name_en, event_date, location, is_active, theme_json, sound_json)
VALUES ('Tiệc tất niên 2026', '2026年尾牙', '2026年尾牙', 'Year End Party 2026', DATE '2026-12-01', 'Hội trường A', TRUE,
        '{"themeKey":"tet-binh-ngo","stageEffect":"fireworks",
          "congratsTitle":{"vi":"Chúc mừng","zhTW":"恭喜","zhCN":"恭喜","en":"Congratulations"},
          "digitBox":{"style":"glass","radius":12,"placeholder":"?","fontFamily":"Anton"},
          "startButton":{"style":2,"label":{"vi":"Bắt đầu","zhTW":"開始","zhCN":"开始","en":"Start"}}}'::jsonb,
        '{"mode":"single","volume":70,"duckOnSpin":true}'::jsonb);

INSERT INTO luckydraw.prize (event_id, sort_order, name_vi, name_zh_tw, name_zh_cn, name_en,
                             quantity, draw_per_turn, draw_type, spin_seconds, stop_seconds,
                             min_seniority_months, max_seniority_months)
SELECT e.event_id, v.*
FROM luckydraw.event e,
     (VALUES (1, 'Giải khuyến khích', '鼓勵獎', '鼓励奖', 'Consolation Prize', 10, 5, 2, 5, 2, NULL::int, 6),
             (2, 'Giải may mắn',      '幸運獎', '幸运奖', 'Lucky Prize',        5, 5, 2, 5, 2, NULL, NULL),
             (3, 'Giải ba',           '三獎',   '三奖',   'Third Prize',        5, 5, 1, 6, 2, NULL, NULL),
             (4, 'Giải nhì',          '二獎',   '二奖',   'Second Prize',       3, 1, 1, 6, 3, NULL, NULL),
             (5, 'Giải nhất',         '頭獎',   '头奖',   'First Prize',        2, 1, 1, 8, 3, 6, NULL),
             (6, 'Giải đặc biệt',     '特別獎', '特别奖', 'Grand Prize',        1, 1, 1, 10, 4, 6, NULL)
     ) AS v(sort_order, name_vi, name_zh_tw, name_zh_cn, name_en, quantity, draw_per_turn, draw_type,
            spin_seconds, stop_seconds, min_m, max_m)
WHERE e.is_active;

INSERT INTO luckydraw.participant (event_id, employee_code, full_name, department_id, join_date)
SELECT e.event_id,
       'NV' || lpad(g::text, 3, '0'),
       (ARRAY['Nguyễn','Trần','Lê','Phạm','Hoàng','Huỳnh','Phan','Vũ','Võ','Đặng','Bùi','Đỗ'])[1 + (g * 7) % 12]
         || ' ' || (ARRAY['Văn','Thị','Minh','Ngọc','Hữu','Thanh','Quốc','Thu','Gia','Bảo'])[1 + (g * 3) % 10]
         || ' ' || (ARRAY['An','Bình','Châu','Dũng','Giang','Hà','Hải','Khánh','Linh','Long','Mai','Nam',
                          'Nhung','Phúc','Quân','Sơn','Tâm','Thảo','Trang','Tuấn','Uyên','Vy','Yến','Đạt'])[1 + (g * 5) % 24],
       (SELECT d.department_id FROM luckydraw.department d ORDER BY d.sort_order OFFSET (g % 5) LIMIT 1),
       CASE WHEN g <= 50 THEN DATE '2026-06-01' - (50 - g) * 37
            ELSE DATE '2026-06-02' + (g - 51) * 15 END
FROM luckydraw.event e, generate_series(1, 60) AS g
WHERE e.is_active;
