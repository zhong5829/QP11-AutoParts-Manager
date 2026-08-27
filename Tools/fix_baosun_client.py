# -*- coding: utf-8 -*-
# 修复存量报损单 client 为空问题：写入"配件报损"特殊客户 cid 03136（2023-07-19 至今惯例）
import pyodbc

conn = pyodbc.connect(
    'Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;',
    autocommit=True
)
cur = conn.cursor()

target_sns = ['00303997', '00303998', '00303999', '00304001', '00304002']

print('=== 修复前 ===')
cur.execute(
    "SELECT sn, datetime, client, worker, operator, flag, type, total FROM bill_sell WHERE sn IN (%s) ORDER BY sn" %
    ','.join("'" + s + "'" for s in target_sns))
for r in cur.fetchall():
    print(' | '.join(str(x) for x in r))

print('\n=== 执行 UPDATE ===')
cur.execute(
    "UPDATE bill_sell SET client='03136' WHERE sn IN (%s) AND (client IS NULL OR client='')" %
    ','.join("'" + s + "'" for s in target_sns))
print(f'受影响行数: {cur.rowcount}')

print('\n=== 修复后 ===')
cur.execute(
    "SELECT sn, datetime, client, worker, operator, flag, type, total FROM bill_sell WHERE sn IN (%s) ORDER BY sn" %
    ','.join("'" + s + "'" for s in target_sns))
for r in cur.fetchall():
    print(' | '.join(str(x) for x in r))

print('\n=== 验证 JOIN client_infor 显示 ===')
cur.execute("""
    SELECT b.sn, b.client, ISNULL(ci.name, '') AS ClientName, b.flag
    FROM bill_sell b LEFT JOIN client_infor ci ON ci.cid = b.client
    WHERE b.sn IN (%s) ORDER BY b.sn""" % ','.join("'" + s + "'" for s in target_sns))
for r in cur.fetchall():
    print(' | '.join(str(x) for x in r))

conn.close()
print('\nDONE')
