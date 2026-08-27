# -*- coding: utf-8 -*-
"""查询源数据库销售/退货区分字段（utf8连接，SQL端转varbinary，Python端gbk解码）"""
import pymssql

conn = pymssql.connect(
    server='frp-bar.com', port=47017,
    user='QP150', password='Dengpeng0716.',
    database='备用数据', charset='utf8',
    tds_version='7.0', timeout=60
)
cur = conn.cursor()

def decode_gbk(raw):
    """varbinary -> gbk字符串"""
    if raw is None:
        return ''
    if isinstance(raw, str):
        return raw
    try:
        return raw.decode('gbk').rstrip('\x00 ').strip()
    except Exception:
        return repr(raw)

print('=== 1. tbsada.type 取值分布（销售出库明细） ===')
cur.execute("""
SELECT CAST(RTRIM(LTRIM(type)) AS varbinary(20)) AS type_bin, COUNT(*) AS cnt
FROM tbsada
GROUP BY type
ORDER BY cnt DESC
""")
for r in cur.fetchall():
    print(f'  type=[{decode_gbk(r[0])}], count={r[1]}')

print()
print('=== 2. tbistoed.Type 取值分布（采购入库明细） ===')
cur.execute("""
SELECT CAST(RTRIM(LTRIM(Type)) AS varbinary(20)) AS type_bin, COUNT(*) AS cnt
FROM tbistoed
GROUP BY Type
ORDER BY cnt DESC
""")
for r in cur.fetchall():
    print(f'  Type=[{decode_gbk(r[0])}], count={r[1]}')

print()
print('=== 3. tbsada.ckamount 正负分布（销售数量，负数可能是退货） ===')
cur.execute("""
SELECT
    CASE WHEN ckamount > 0 THEN 'positive' WHEN ckamount < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM tbsada
GROUP BY CASE WHEN ckamount > 0 THEN 'positive' WHEN ckamount < 0 THEN 'negative' ELSE 'zero' END
""")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('=== 4. tbistoed.jkamount 正负分布（入库数量，负数可能是退货） ===')
cur.execute("""
SELECT
    CASE WHEN jkamount > 0 THEN 'positive' WHEN jkamount < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM tbistoed
GROUP BY CASE WHEN jkamount > 0 THEN 'positive' WHEN jkamount < 0 THEN 'negative' ELSE 'zero' END
""")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('=== 5. tbsada.oprct 正负分布（销售单价，负数可能是退货） ===')
cur.execute("""
SELECT
    CASE WHEN oprct > 0 THEN 'positive' WHEN oprct < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM tbsada
GROUP BY CASE WHEN oprct > 0 THEN 'positive' WHEN oprct < 0 THEN 'negative' ELSE 'zero' END
""")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]}')

conn.close()
