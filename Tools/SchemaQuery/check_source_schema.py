# -*- coding: utf-8 -*-
"""查询源数据库 tbsada / tbistoed 表结构，定位销售/退货区分字段"""
import pymssql

conn = pymssql.connect(
    server='frp-bar.com', port=47017,
    user='QP150', password='Dengpeng0716.',
    database='备用数据', charset='utf8',
    tds_version='7.1'
)
cur = conn.cursor()

print('=== 1. tbsada 表结构（销售出库明细） ===')
cur.execute("""
SELECT c.name,
       t.name + '(' + CAST(c.length AS varchar) + ')' AS type_info,
       c.isnullable
FROM syscolumns c JOIN systypes t ON c.xtype=t.xtype
WHERE c.id=OBJECT_ID('tbsada') ORDER BY c.colid
""")
for r in cur.fetchall():
    print(f'  {r[0]:15s} | {r[1]:25s} | nullable={r[2]}')

print()
print('=== 2. tbistoed 表结构（采购入库明细） ===')
cur.execute("""
SELECT c.name,
       t.name + '(' + CAST(c.length AS varchar) + ')' AS type_info,
       c.isnullable
FROM syscolumns c JOIN systypes t ON c.xtype=t.xtype
WHERE c.id=OBJECT_ID('tbistoed') ORDER BY c.colid
""")
for r in cur.fetchall():
    print(f'  {r[0]:15s} | {r[1]:25s} | nullable={r[2]}')

conn.close()
