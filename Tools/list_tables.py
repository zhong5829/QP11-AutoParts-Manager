# -*- coding: utf-8 -*-
import pyodbc

conn = pyodbc.connect(
    'Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;',
    autocommit=True
)
cur = conn.cursor()

cur.execute("SELECT name FROM sysobjects WHERE type='U' AND name NOT LIKE 'dtproperties' AND name NOT LIKE 'sys%' ORDER BY name")
tables = [r[0] for r in cur.fetchall()]

print('=== 所有用户表及行数 ===')
for t in tables:
    try:
        cur.execute(f"SELECT COUNT(1) FROM [{t}]")
        cnt = cur.fetchone()[0]
        print(f'{t}: {cnt}')
    except Exception as e:
        print(f'{t}: ERROR {e}')

conn.close()
