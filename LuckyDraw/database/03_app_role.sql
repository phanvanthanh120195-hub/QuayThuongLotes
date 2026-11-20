-- =====================================================================
-- LuckyDraw – 03_app_role.sql
-- Tạo role riêng cho app C#. ĐỔI MẬT KHẨU trước khi chạy.
-- Bản đã điền mật khẩu thật KHÔNG được commit lên git.
-- Chuỗi kết nối Supabase (Session pooler): Username=luckydraw_app.<project-ref>
-- =====================================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'luckydraw_app') THEN
        CREATE ROLE luckydraw_app LOGIN PASSWORD 'DOI_MAT_KHAU_TRUOC_KHI_CHAY';
    ELSE
        -- Role đã có (chạy lại script): đặt lại mật khẩu cho khớp chuỗi kết nối
        ALTER ROLE luckydraw_app LOGIN PASSWORD 'DOI_MAT_KHAU_TRUOC_KHI_CHAY';
    END IF;
END $$;

GRANT USAGE ON SCHEMA luckydraw TO luckydraw_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON
    luckydraw.department, luckydraw.event, luckydraw.prize,
    luckydraw.participant, luckydraw.draw_result, luckydraw.app_user
TO luckydraw_app;

-- Nhật ký: chỉ được đọc và ghi thêm, không sửa / xoá
GRANT SELECT, INSERT ON luckydraw.audit_log TO luckydraw_app;

-- Cột IDENTITY cần quyền dùng sequence
GRANT USAGE ON ALL SEQUENCES IN SCHEMA luckydraw TO luckydraw_app;

-- Policy RLS: chỉ luckydraw_app được truy cập
DROP POLICY IF EXISTS app_all ON luckydraw.department;
DROP POLICY IF EXISTS app_all ON luckydraw.event;
DROP POLICY IF EXISTS app_all ON luckydraw.prize;
DROP POLICY IF EXISTS app_all ON luckydraw.participant;
DROP POLICY IF EXISTS app_all ON luckydraw.draw_result;
DROP POLICY IF EXISTS app_all ON luckydraw.app_user;
DROP POLICY IF EXISTS app_read ON luckydraw.audit_log;
DROP POLICY IF EXISTS app_insert ON luckydraw.audit_log;

CREATE POLICY app_all ON luckydraw.department  FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_all ON luckydraw.event       FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_all ON luckydraw.prize       FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_all ON luckydraw.participant FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_all ON luckydraw.draw_result FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_all ON luckydraw.app_user    FOR ALL TO luckydraw_app USING (TRUE) WITH CHECK (TRUE);
CREATE POLICY app_read   ON luckydraw.audit_log FOR SELECT TO luckydraw_app USING (TRUE);
CREATE POLICY app_insert ON luckydraw.audit_log FOR INSERT TO luckydraw_app WITH CHECK (TRUE);
