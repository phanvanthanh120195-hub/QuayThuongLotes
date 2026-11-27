-- =====================================================================
-- LuckyDraw – 01_schema.sql
-- Chạy trong Supabase → SQL Editor (hoặc psql). Chạy lại được: xoá schema cũ trước.
-- 7 bảng: department, event, prize, participant, draw_result, app_user, audit_log
-- =====================================================================

DROP SCHEMA IF EXISTS luckydraw CASCADE;
CREATE SCHEMA luckydraw;

-- ---------------------------------------------------------------------
-- Bộ phận (dùng chung cho mọi sự kiện)
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.department (
    department_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code          VARCHAR(30)  NOT NULL,
    name_vi       VARCHAR(200) NOT NULL,
    name_zh_tw    VARCHAR(200),
    name_zh_cn    VARCHAR(200),
    name_en       VARCHAR(200),
    sort_order    INT          NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ux_department_code UNIQUE (code)
);

-- ---------------------------------------------------------------------
-- Sự kiện. is_active = sự kiện đang chiếu trên trang chính (tối đa 1)
-- theme_json / sound_json: cấu hình giao diện & âm thanh (xem mục 3.7)
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.event (
    event_id    INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name_vi     VARCHAR(200) NOT NULL,
    name_zh_tw  VARCHAR(200),
    name_zh_cn  VARCHAR(200),
    name_en     VARCHAR(200),
    event_date  DATE         NOT NULL,
    location    VARCHAR(200),
    is_active   BOOLEAN      NOT NULL DEFAULT FALSE,
    theme_json  JSONB        NOT NULL DEFAULT '{}'::jsonb,
    sound_json  JSONB        NOT NULL DEFAULT '{}'::jsonb,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

-- Chỉ một sự kiện được chiếu tại một thời điểm
CREATE UNIQUE INDEX ux_event_one_active ON luckydraw.event ((TRUE)) WHERE is_active;

-- ---------------------------------------------------------------------
-- Giải thưởng
-- draw_type: 1 = Thủ công (MC bấm dừng), 2 = Tự động (tự dừng sau spin_seconds)
-- Thâm niên tính theo tháng tại ngày sự kiện: min <= thâm niên < max (NULL = không giới hạn)
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.prize (
    prize_id              INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id              INT          NOT NULL REFERENCES luckydraw.event (event_id) ON DELETE CASCADE,
    sort_order            INT          NOT NULL DEFAULT 0,
    name_vi               VARCHAR(200) NOT NULL,
    name_zh_tw            VARCHAR(200),
    name_zh_cn            VARCHAR(200),
    name_en               VARCHAR(200),
    -- Phần thưởng của giải (vd. "20 triệu tiền mặt"), 4 ngôn ngữ như tên giải – xem 05_prize_reward.sql
    reward_vi             VARCHAR(300),
    reward_zh_tw          VARCHAR(300),
    reward_zh_cn          VARCHAR(300),
    reward_en             VARCHAR(300),
    quantity              INT          NOT NULL,
    draw_per_turn         INT          NOT NULL DEFAULT 1,
    draw_type             SMALLINT     NOT NULL DEFAULT 1,
    spin_seconds          INT          NOT NULL DEFAULT 5,
    stop_seconds          INT          NOT NULL DEFAULT 2,
    min_seniority_months  INT,
    max_seniority_months  INT,
    image_path            VARCHAR(300),
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ux_prize_id_event   UNIQUE (prize_id, event_id),
    CONSTRAINT ck_prize_quantity   CHECK (quantity > 0),
    CONSTRAINT ck_prize_per_turn   CHECK (draw_per_turn > 0),
    CONSTRAINT ck_prize_draw_type  CHECK (draw_type IN (1, 2)),
    CONSTRAINT ck_prize_seconds    CHECK (spin_seconds BETWEEN 1 AND 120 AND stop_seconds BETWEEN 0 AND 60),
    CONSTRAINT ck_prize_seniority  CHECK (
        (min_seniority_months IS NULL OR min_seniority_months >= 0)
        AND (max_seniority_months IS NULL OR max_seniority_months > 0)
        AND (min_seniority_months IS NULL OR max_seniority_months IS NULL
             OR max_seniority_months > min_seniority_months))
);
CREATE INDEX ix_prize_event ON luckydraw.prize (event_id, sort_order);

-- ---------------------------------------------------------------------
-- Người tham dự (theo từng sự kiện)
-- is_absent = vắng mặt → không được quay nữa
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.participant (
    participant_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id       INT          NOT NULL REFERENCES luckydraw.event (event_id) ON DELETE CASCADE,
    employee_code  VARCHAR(30)  NOT NULL,
    full_name      VARCHAR(200) NOT NULL,
    full_name_zh   VARCHAR(200),             -- 姓名 (tên tiếng Trung)
    department_vi  VARCHAR(200),             -- Bộ phận (ghi thẳng theo file nhân sự)
    department_zh  VARCHAR(200),             -- 現任單位
    position       VARCHAR(200),             -- Chức vụ
    photo_path     VARCHAR(300),             -- Ảnh chân dung (/uploads/{event}/photo/...)
    department_id  INT          REFERENCES luckydraw.department (department_id),  -- danh mục bộ phận (đang ẩn)
    join_date      DATE         NOT NULL,
    is_absent      BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ux_participant_code     UNIQUE (event_id, employee_code),
    CONSTRAINT ux_participant_id_event UNIQUE (participant_id, event_id)
);
CREATE INDEX ix_participant_department ON luckydraw.participant (department_id);

-- ---------------------------------------------------------------------
-- Kết quả quay. Không xoá cứng: xoá mềm bằng is_removed + lý do
-- removed_reason: 1 = Vắng mặt, 2 = Admin xoá, 3 = Reset toàn bộ
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.draw_result (
    result_id       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id        INT          NOT NULL,
    prize_id        INT          NOT NULL,
    participant_id  INT          NOT NULL,
    batch_id        UUID         NOT NULL,
    drawn_at        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    drawn_by        VARCHAR(50)  NOT NULL,
    is_removed      BOOLEAN      NOT NULL DEFAULT FALSE,
    removed_reason  SMALLINT,
    removed_at      TIMESTAMPTZ,
    removed_by      VARCHAR(50),
    -- FK kép: kết quả chỉ gắn được với giải / người của CÙNG sự kiện. Không cascade.
    CONSTRAINT fk_result_prize       FOREIGN KEY (prize_id, event_id)
        REFERENCES luckydraw.prize (prize_id, event_id),
    CONSTRAINT fk_result_participant FOREIGN KEY (participant_id, event_id)
        REFERENCES luckydraw.participant (participant_id, event_id),
    CONSTRAINT ck_result_removed CHECK (
        (NOT is_removed AND removed_reason IS NULL AND removed_at IS NULL AND removed_by IS NULL)
        OR (is_removed AND removed_reason IN (1, 2, 3) AND removed_at IS NOT NULL AND removed_by IS NOT NULL))
);

-- Một người chỉ trúng 1 giải trong 1 sự kiện (kết quả đã xoá mềm thì không tính)
CREATE UNIQUE INDEX ux_draw_result_one_win ON luckydraw.draw_result (event_id, participant_id) WHERE NOT is_removed;
CREATE INDEX ix_draw_result_prize ON luckydraw.draw_result (prize_id) WHERE NOT is_removed;
CREATE INDEX ix_draw_result_event_time ON luckydraw.draw_result (event_id, drawn_at DESC);

-- ---------------------------------------------------------------------
-- Tài khoản đăng nhập. Mật khẩu băm bằng PasswordHasher<T> của ASP.NET Core
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.app_user (
    user_id        INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_name      VARCHAR(50)  NOT NULL,
    display_name   VARCHAR(100),
    password_hash  VARCHAR(500) NOT NULL,
    role           VARCHAR(20)  NOT NULL,
    is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
    last_login_at  TIMESTAMPTZ,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ck_app_user_role CHECK (role IN ('Admin', 'Operator'))
);
CREATE UNIQUE INDEX ux_app_user_name ON luckydraw.app_user (lower(user_name));

-- ---------------------------------------------------------------------
-- Nhật ký thao tác. Không FK tới event để xoá sự kiện vẫn giữ nhật ký.
-- ---------------------------------------------------------------------
CREATE TABLE luckydraw.audit_log (
    audit_id    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id    INT,
    action      VARCHAR(50)  NOT NULL,
    entity      VARCHAR(50),
    entity_id   VARCHAR(50),
    detail      JSONB,
    user_name   VARCHAR(50)  NOT NULL,
    ip_address  VARCHAR(45),
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_audit_log_time ON luckydraw.audit_log (created_at DESC);
CREATE INDEX ix_audit_log_event ON luckydraw.audit_log (event_id, created_at DESC);

-- ---------------------------------------------------------------------
-- Bật RLS cho mọi bảng. Policy chỉ cấp cho role luckydraw_app (03_app_role.sql),
-- nên anon / authenticated của Supabase không đọc được gì kể cả khi lỡ GRANT.
-- ---------------------------------------------------------------------
ALTER TABLE luckydraw.department  ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.event       ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.prize       ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.participant ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.draw_result ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.app_user    ENABLE ROW LEVEL SECURITY;
ALTER TABLE luckydraw.audit_log   ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON SCHEMA luckydraw FROM PUBLIC;
