# -*- coding: utf-8 -*-
import pyodbc

conn = pyodbc.connect(
    'Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;',
    autocommit=True
)
cur = conn.cursor()

# 查看 user_infor 表结构和数据
print('=== user_infor 表结构 ===')
cur.execute("SELECT c.name, t.name + '(' + CAST(c.length as varchar) + ')' as type FROM syscolumns c JOIN systypes t ON c.xtype=t.xtype WHERE c.id=OBJECT_ID('user_infor') ORDER BY c.colorder")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

print('\n=== user_infor 数据 ===')
cur.execute("SELECT * FROM user_infor")
cols = [d[0] for d in cur.description]
print(' | '.join(cols))
for r in cur.fetchall():
    print(' | '.join(str(x) for x in r))

# 查看其他可能需要保留的配置表
for tbl in ['business_type', 'groups', 'desktop', 'mnu', 'serialnumber', 'serialnumber_new', 'friends', 'BJ', 'CLASSES', 'business_infor']:
    print(f'\n=== {tbl} 数据(前5行) ===')
    try:
        cur.execute(f"SELECT TOP 5 * FROM [{tbl}]")
        cols = [d[0] for d in cur.description]
        print(' | '.join(cols))
        for r in cur.fetchall():
            print(' | '.join(str(x)[:30] for x in r))
    except Exception as e:
        print(f'  ERROR: {e}')

conn.close()
