using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace MiniMesCs;

/// 설비별 최신 생산 실적을 MS-SQL에서 조회해 표에 표시하는 화면.
public class Form1 : Form
{
    // ── 화면 부품 ──
    private readonly Button btnLoad = new();
    private readonly Button btnExit = new();
    private readonly Label lblStatus = new();
    private readonly DataGridView grid = new();
    private readonly CheckBox chkAuto = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new();

    // ── DB 접속 정보 ──
    // appsettings.json 에서 읽는다. 소스에 접속 정보를 두지 않기 위함.
    private readonly string connStr = string.Empty;
    private readonly string configError = string.Empty;

    // ── 조회 쿼리 ──
    // 설비마다 가장 최근(log_id가 가장 큰) 실적 1건씩만 뽑는다.
    private const string Sql = """
        SELECT m.machine_id                 AS [설비코드],
               m.name                       AS [설비명],
               l.status                     AS [상태],
               CAST(ROUND(l.oee * 100, 1) AS DECIMAL(4,1))        AS [OEE],
               CAST(ROUND(m.target_oee * 100, 1) AS DECIMAL(4,1)) AS [목표],
               l.total_count                AS [생산수량],
               l.defect_count               AS [불량수량],
               l.logged_at                  AS [수집시각]
        FROM dbo.machine m
        JOIN dbo.production_log l
          ON l.log_id = (SELECT MAX(log_id) FROM dbo.production_log
                         WHERE machine_id = m.machine_id)
        ORDER BY m.machine_id
        """;

    public Form1()
    {
        // 접속 정보 로드
        try
        {
            connStr = LoadConnectionString();
        }
        catch (Exception ex)
        {
            configError = ex.Message;
        }

        // 창 자체의 설정
        Text = "Mini MES (C# / MS-SQL) — 설비 현황";
        Width = 900;
        Height = 500;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("맑은 고딕", 9.0F);

        // 조회 버튼
        btnLoad.Text = "조회";
        btnLoad.SetBounds(12, 12, 100, 32);
        btnLoad.Click += (_, _) => LoadData();

        // 종료 버튼
        btnExit.Text = "종료";
        btnExit.SetBounds(120, 12, 100, 32);
        btnExit.Click += (_, _) => Close();

        // 자동 새로고침 체크박스
        chkAuto.Text = "자동 새로고침 (5초)";
        chkAuto.SetBounds(232, 18, 170, 24);
        chkAuto.CheckedChanged += (_, _) =>
        {
            refreshTimer.Enabled = chkAuto.Checked;
            if (chkAuto.Checked) LoadData();
        };

        // 타이머 - 5000ms = 5초
        refreshTimer.Interval = 5000;
        refreshTimer.Tick += (_, _) => LoadData();

        // 상태 표시 라벨
        lblStatus.SetBounds(414, 20, 450, 20);
        if (configError.Length > 0)
        {
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = configError;
            btnLoad.Enabled = false;
            chkAuto.Enabled = false;
        }
        else
        {
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Text = "조회 버튼을 누르십시오.";
        }

        // 결과 표
        grid.SetBounds(12, 56, 860, 392);
        grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                    | AnchorStyles.Left | AnchorStyles.Right;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        // 만든 부품을 창에 붙인다.
        Controls.Add(btnLoad);
        Controls.Add(btnExit);
        Controls.Add(chkAuto);
        Controls.Add(lblStatus);
        Controls.Add(grid);
    }

    /// 실행 폴더의 appsettings.json에서 연결 문자열을 읽는다.
    private static string LoadConnectionString()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "appsettings.json 이 없습니다. appsettings.example.json 을 복사해 접속 정보를 입력하십시오.");
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath));
        return doc.RootElement.GetProperty("ConnectionString").GetString()
               ?? throw new InvalidOperationException("appsettings.json 의 ConnectionString 이 비어 있습니다.");
    }

    /// 조회 버튼과 타이머가 함께 호출하는 공통 함수.
    private void LoadData()
    {
        try
        {
            DataTable table = new();

            // using = 다 쓰고 나면 알아서 연결을 닫아준다.
            using (SqlConnection conn = new(connStr))
            {
                conn.Open();
                using SqlDataAdapter adapter = new(Sql, conn);
                adapter.Fill(table);   // 쿼리 결과를 table에 담는다.
            }

            grid.DataSource = table;   // 표에 그대로 연결하면 화면에 뜬다.
            HighlightBelowTarget();

            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Text = $"{table.Rows.Count}개 설비 조회 완료  ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            // 접속 실패, 쿼리 오류 등을 화면에 그대로 보여준다.
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = "조회 실패: " + ex.Message;
            // 끊긴 상태로 5초마다 같은 오류를 반복하지 않도록 자동 새로고침을 끈다.
            chkAuto.Checked = false;
        }
    }

    /// OEE가 목표에 못 미치는 설비는 빨간 글씨로 표시한다.
    private void HighlightBelowTarget()
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            
            if (row.Cells["OEE"].Value is null) continue;

            decimal oee = Convert.ToDecimal(row.Cells["OEE"].Value);
            decimal target = Convert.ToDecimal(row.Cells["목표"].Value);

            // 행 전체가 아니라 OEE 칸만 물들인다.
            DataGridViewCell cell = row.Cells["OEE"];
            Color mark = oee < target ? Color.Firebrick : Color.Black;

            cell.Style.ForeColor = mark;
            // 행이 선택돼 파랗게 반전돼도 색이 보이도록 선택 상태 색도 지정한다.
            cell.Style.SelectionForeColor = mark;
        }
    }
}
