-- =====================================================================
-- LuckyDraw – 04_participant_info.sql
-- Thêm thông tin nhân sự trực tiếp vào người tham dự (theo file Excel nhân sự):
--   姓名 / Bộ phận / 現任單位 / Chức vụ / Ảnh chân dung. Danh mục bộ phận cũ vẫn giữ (department_id) nhưng ẩn.
-- Chạy được nhiều lần, không mất dữ liệu.
-- =====================================================================
ALTER TABLE luckydraw.participant
    ADD COLUMN IF NOT EXISTS full_name_zh  VARCHAR(200),
    ADD COLUMN IF NOT EXISTS department_vi VARCHAR(200),
    ADD COLUMN IF NOT EXISTS department_zh VARCHAR(200),
    ADD COLUMN IF NOT EXISTS position      VARCHAR(200),
    ADD COLUMN IF NOT EXISTS photo_path    VARCHAR(300);

SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema = 'luckydraw' AND table_name = 'participant' ORDER BY ordinal_position;
