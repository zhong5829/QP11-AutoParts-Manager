using System.Data.Odbc;
var cn = new OdbcConnection("Driver={SQL Server};Server=192.168.83.128,8829;Database=qipei;Uid=sa;Pwd=593106;");
cn.Open();

// 1. 查A076配件的库存情况（模拟allParts的SQL）
Console.WriteLine("=== A076配件库存（allParts模拟SQL） ===");
var cmd1 = new OdbcCommand(@"SELECT d.partid, d.partno, d.name, d.cartype, d.part_tm,
         ISNULL(SUM(s.amount),0) AS amount,
         ISNULL(MAX(CASE WHEN s.lsprice>0 THEN s.lsprice END),0) AS stock_lsprice,
         ISNULL(MAX(CASE WHEN s.pfprice>0 THEN s.pfprice END),0) AS stock_pfprice
  FROM part_data d
  INNER JOIN part_stock s ON d.partid=s.partid AND ISNULL(s.place,'')<>'废品仓'
  WHERE ISNULL(d.DEL,'0')='0' AND d.partno LIKE '%A076%'
  GROUP BY d.partid, d.partno, d.name, d.carname, d.cartype, d.part_tm", cn);
var r1 = cmd1.ExecuteReader();
while(r1.Read())
    Console.WriteLine($"partid={r1["partid"]}|partno={r1["partno"]}|part_tm={r1["part_tm"]}|name={r1["name"]}|cartype={r1["cartype"]}|amount={r1["amount"]}");
r1.Close();

// 2. 查A076配件的原始库存明细
Console.WriteLine("\n=== A076库存明细 ===");
var cmd2 = new OdbcCommand(@"SELECT s.partid, s.place, s.amount, s.lsprice, s.pfprice FROM part_stock s
  INNER JOIN part_data d ON s.partid=d.partid
  WHERE d.partno LIKE '%A076%' AND ISNULL(d.DEL,'0')='0'", cn);
var r2 = cmd2.ExecuteReader();
while(r2.Read())
    Console.WriteLine($"partid={r2["partid"]}|place={r2["place"]}|amount={r2["amount"]}|lsprice={r2["lsprice"]}|pfprice={r2["pfprice"]}");
r2.Close();

cn.Close();
