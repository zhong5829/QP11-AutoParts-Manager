$conn = New-Object System.Data.Odbc.OdbcConnection
$conn.ConnectionString = "Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 10 sn, client, worker, flag, datetime FROM bill_sell WHERE flag=3 ORDER BY datetime DESC"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    $sn = $r['sn']
    $client = $r['client']
    $worker = $r['worker']
    $flag = $r['flag']
    $dt = $r['datetime']
    Write-Host "sn=$sn client=[$client] worker=[$worker] flag=$flag dt=$dt"
}
$r.Close()
$conn.Close()
