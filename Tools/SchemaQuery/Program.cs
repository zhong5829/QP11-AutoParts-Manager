using System;
using System.Data.Odbc;
using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        // 当前项目数据库连接
        var connStr = "Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;";
        using var conn = new OdbcConnection(connStr);
        conn.Open();
        Console.WriteLine("Connected to current project database successfully.");

        var sb = new StringBuilder();

        // 获取所有用户表
        var tables = new System.Collections.Generic.List<string>();
        using (var cmd = new OdbcCommand(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        Console.WriteLine($"Found {tables.Count} tables.");

        foreach (var table in tables)
        {
            sb.AppendLine($"=== {table} ===");
            using var colCmd = new OdbcCommand(
                "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=? ORDER BY ORDINAL_POSITION", conn);
            colCmd.Parameters.AddWithValue("table", table);
            using var colReader = colCmd.ExecuteReader();
            {
                while (colReader.Read())
                {
                    var name = colReader.GetString(0);
                    var dtype = colReader.GetString(1);
                    var len = colReader.IsDBNull(2) ? "" : colReader.GetValue(2).ToString();
                    var nullable = colReader.GetString(3);
                    sb.AppendLine($"  {name}|{dtype}|{len}|{nullable}");
                }
            }
            sb.AppendLine();
        }

        // 同时获取每个表的行数
        sb.AppendLine();
        sb.AppendLine("=== TABLE ROW COUNTS ===");
        foreach (var table in tables)
        {
            try
            {
                using var countCmd = new OdbcCommand($"SELECT COUNT(*) FROM [{table}]", conn);
                var count = countCmd.ExecuteScalar();
                sb.AppendLine($"  {table}: {count} rows");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  {table}: ERROR - {ex.Message}");
            }
        }

        File.WriteAllText(@"f:\qp11\Tools\SchemaQuery\current_db_schema.txt", sb.ToString(), Encoding.UTF8);
        Console.WriteLine("Done. Output written to current_db_schema.txt");
    }
}