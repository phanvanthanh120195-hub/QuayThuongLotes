-- =====================================================================
-- LuckyDraw – 05_prize_reward.sql
-- Thêm "phần thưởng" cho từng giải (vd. Giải nhất: 20 triệu tiền mặt), 4 ngôn ngữ như tên giải.
-- Hiển thị trong Admin › Giải thưởng và bảng "Thông tin giải thưởng" trên màn chiếu.
-- Chạy được nhiều lần, không mất dữ liệu. PHẢI chạy trước khi dùng bản app có tính năng này.
--
-- Vị trí cột: PostgreSQL không hỗ trợ "ADD COLUMN ... AFTER name_en" (khác MySQL) – ALTER luôn thêm vào CUỐI bảng.
-- App đọc/ghi theo TÊN cột nên thứ tự không ảnh hưởng gì. Cài mới bằng 01_schema.sql thì 4 cột đã nằm ngay sau name_en.
-- =====================================================================
ALTER TABLE luckydraw.prize
    ADD COLUMN IF NOT EXISTS reward_vi    VARCHAR(300),
    ADD COLUMN IF NOT EXISTS reward_zh_tw VARCHAR(300),
    ADD COLUMN IF NOT EXISTS reward_zh_cn VARCHAR(300),
    ADD COLUMN IF NOT EXISTS reward_en    VARCHAR(300);

SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema = 'luckydraw' AND table_name = 'prize' AND column_name LIKE 'reward%' ORDER BY ordinal_position;
