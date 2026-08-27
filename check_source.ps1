$cs = 'Server=frp-bar.com,47017;Database=备用数据;User Id=QP150;Password=Dengpeng0716.;TrustServerCertificate=True;Encrypt=False;'
$c = New-Object System.Data.SqlClient.SqlConnection($cs)
try {
    $c.Open()
    Write-Host "连接成功"
} catch {
    Write-Host "连接失败: $_"
    exit
}
$cmd = $c.CreateCommand()

# 1. tbisto 表中 gm-8-048 的记录
$cmd.CommandText = "SELECT * FROM tbisto WHERE nno LIKE '%gm-8-048%'"
$r = $cmd.ExecuteReader()
$hasRows = $r.HasRows
while ($r.Read()) {
    Write-Host "tbisto 记录:"
    for ($i = 0; $i -lt $r.FieldCount; $i++) {
        Write-Host "  $($r.GetName($i)) = $($r[$i])"
    }
    Write-Host "---"
}
$r.Close()
if (-not $hasRows) { Write-Host "tbisto 表中无 gm-8-048 记录" }

Write-Host ""

# 2. tbprnoty 表中 gm-8-048 的记录
$cmd.CommandText = "SELECT nno, na1, ty, unit, iprc, iprj, oprc FROM tbprnoty WHERE nno LIKE '%gm-8-048%'"
$r = $cmd.ExecuteReader()
$hasRows2 = $r.HasRows
while ($r.Read()) {
    Write-Host "tbprnoty 记录: nno=$($r['nno']) na1=$($r['na1']) ty=$($r['ty']) unit=$($r['unit']) iprc=$($r['iprc']) iprj=$($r['iprj']) oprc=$($r['oprc'])"
}
$r.Close()
if (-not $hasRows2) { Write-Host "tbprnoty 表中无 gm-8-048 记录" }

Write-Host ""

# 3. 统计 tbisto
$cmd.CommandText = "SELECT COUNT(1) AS total, SUM(CASE WHEN kcamount > 0 THEN 1 ELSE 0 END) AS gt0, SUM(CASE WHEN kcamount <= 0 OR kcamount IS NULL THEN 1 ELSE 0 END) AS le0 FROM tbisto"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "tbisto 统计: 总行数=$($r['total']) kcamount>0的=$($r['gt0']) kcamount<=0或NULL的=$($r['le0'])"
}
$r.Close()

Write-Host ""

# 4. nno 为空的行数
$cmd.CommandText = "SELECT COUNT(1) FROM tbisto WHERE nno IS NULL OR LTRIM(RTRIM(nno)) = ''"
Write-Host "tbisto 中 nno 为空或空白的行数: $($cmd.ExecuteScalar())"

$c.Close()
