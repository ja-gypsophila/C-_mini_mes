# collector.py — 설비 측정값을 생성해 MS-SQL에 적재한다
import configparser
import random
import time
from pathlib import Path

import pyodbc

CONFIG_PATH = Path(__file__).with_name("config.ini")

MACHINES = ["M-001", "M-002", "M-003", "M-004"]

# MySQL 판과 달리 자리표시자가 %s 가 아니라 ? 이다.
INSERT_SQL = """
    INSERT INTO dbo.production_log
        (machine_id, logged_at, status,
         availability, performance, quality, oee,
         total_count, defect_count)
    VALUES
        (?, SYSDATETIME(), ?, ?, ?, ?, ?, ?, ?)
"""


def load_config():
    """config.ini에서 접속 정보와 수집 주기를 읽는다."""
    if not CONFIG_PATH.exists():
        raise SystemExit(
            f"설정 파일이 없습니다: {CONFIG_PATH}\n"
            "config.ini.example 을 config.ini 로 복사한 뒤 접속 정보를 입력하십시오."
        )

    parser = configparser.ConfigParser()
    parser.read(CONFIG_PATH, encoding="utf-8")

    db = parser["mssql"]

    required = ("driver", "server", "database")
    missing = [k for k in required if k not in db]
    if missing:
        raise SystemExit(
            f"config.ini의 [mssql]에 다음 항목이 없습니다: {', '.join(missing)}"
        )

    parts = [
        f"DRIVER={{{db['driver']}}}",
        f"SERVER={db['server']}",
        f"DATABASE={db['database']}",
    ]

    if db.getboolean("trusted_connection", fallback=True):
        # Windows 인증
        parts.append("Trusted_Connection=yes")
    else:
        # SQL 인증 — 이때는 계정 정보가 반드시 있어야 한다.
        missing = [k for k in ("user", "password") if k not in db]
        if missing:
            raise SystemExit(
                "trusted_connection = no 이면 "
                f"다음 항목이 필요합니다: {', '.join(missing)}"
            )
        parts.append(f"UID={db['user']}")
        parts.append(f"PWD={db['password']}")

    if db.getboolean("trust_server_certificate", fallback=True):
        parts.append("TrustServerCertificate=yes")

    conn_str = ";".join(parts) + ";"
    interval = parser.getint("collector", "interval_sec", fallback=5)
    return conn_str, interval


def make_reading(machine_id):
    """설비 한 대의 측정값 1건을 만든다."""
    status = random.choices(["RUNNING", "IDLE", "DOWN"], weights=[85, 10, 5])[0]

    # 가용성은 실가동시간의 비율이므로 정지 상태에서 높게 나오면 안 된다.
    if status == "RUNNING":
        availability = round(random.uniform(0.80, 0.97), 4)
    else:
        availability = round(random.uniform(0.40, 0.70), 4)

    performance = round(random.uniform(0.40, 0.70), 4)
    quality = round(random.uniform(0.960, 0.999), 4)
    oee = round(availability * performance * quality, 4)

    total_count = random.randint(500, 1600)
    # 불량 수량을 따로 뽑으면 품질률과 어긋나므로 품질률에서 유도한다.
    defect_count = round(total_count * (1 - quality))

    return (machine_id, status, availability, performance,
            quality, oee, total_count, defect_count)


def main():
    conn_str, interval_sec = load_config()

    conn = pyodbc.connect(conn_str)
    cursor = conn.cursor()
    # 여러 건을 한 번에 보낼 때 왕복을 줄여준다.
    cursor.fast_executemany = True

    print(f"수집 시작 - {interval_sec}초 간격. 중지하려면 Ctrl+C")

    try:
        while True:
            # 설비 4대를 한 번의 왕복으로 적재한다.
            rows = [make_reading(m) for m in MACHINES]
            cursor.executemany(INSERT_SQL, rows)
            conn.commit()
            print(f"{time.strftime('%H:%M:%S')} {len(rows)}건 적재")
            time.sleep(interval_sec)

    except KeyboardInterrupt:
        print("\n수집을 중지했습니다.")

    finally:
        cursor.close()
        conn.close()


if __name__ == "__main__":
    main()
