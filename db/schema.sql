-- =====================================================
--  Mini MES (MS-SQL) — 스키마 및 설비 기준정보
--  SSMS 또는 Azure Data Studio에서 실행하십시오.
--  생산 실적(production_log)은 collector.py가 적재합니다.
-- =====================================================

IF DB_ID('mini_mes') IS NULL
    CREATE DATABASE mini_mes;
GO

USE mini_mes;
GO

IF OBJECT_ID('dbo.production_log', 'U') IS NOT NULL DROP TABLE dbo.production_log;
IF OBJECT_ID('dbo.machine', 'U')        IS NOT NULL DROP TABLE dbo.machine;
GO

-- 설비 마스터 --------------------------------------------------
CREATE TABLE dbo.machine (
    machine_id  VARCHAR(10)  NOT NULL,   -- 설비 코드
    name        NVARCHAR(50) NOT NULL,   -- 설비명
    type        VARCHAR(20)  NOT NULL,   -- 설비 종류
    target_oee  DECIMAL(4,3) NOT NULL,   -- 목표 OEE (0~1)
    CONSTRAINT pk_machine PRIMARY KEY (machine_id)
);
GO

-- 생산 실적 로그 ------------------------------------------------
CREATE TABLE dbo.production_log (
    log_id       INT IDENTITY(1,1) NOT NULL,
    machine_id   VARCHAR(10)  NOT NULL,
    logged_at    DATETIME2    NOT NULL,  -- 수집 시각
    status       VARCHAR(20)  NOT NULL,  -- RUNNING / IDLE / DOWN
    availability DECIMAL(5,4) NOT NULL,  -- 가용성
    performance  DECIMAL(5,4) NOT NULL,  -- 성능
    quality      DECIMAL(5,4) NOT NULL,  -- 품질
    oee          DECIMAL(5,4) NOT NULL,  -- 가용성 × 성능 × 품질
    total_count  INT          NOT NULL,  -- 총 생산 수량
    defect_count INT          NOT NULL,  -- 불량 수량
    CONSTRAINT pk_production_log PRIMARY KEY (log_id),
    CONSTRAINT fk_log_machine FOREIGN KEY (machine_id)
        REFERENCES dbo.machine (machine_id)
);
GO

-- 실적은 설비별·시간순으로 조회되므로 두 컬럼을 묶어 인덱스를 만든다.
CREATE INDEX ix_log_machine_time ON dbo.production_log (machine_id, logged_at);
GO

-- 설비 4대 ------------------------------------------------------
INSERT INTO dbo.machine (machine_id, name, type, target_oee) VALUES
    ('M-001', N'프레스기 1호기',   'PRESS',   0.750),
    ('M-002', N'용접기 1호기',     'WELD',    0.780),
    ('M-003', N'사출성형기 1호기', 'INJECT',  0.700),
    ('M-004', N'검사기 1호기',     'INSPECT', 0.900);
GO
