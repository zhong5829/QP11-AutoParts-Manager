# -*- coding: utf-8 -*-
import pyodbc

conn = pyodbc.connect(
    'Driver={SQL Server};Server=192.168.1.86,1433;Database=qipei;Uid=sa;Pwd=593106;',
    autocommit=True
)
cur = conn.cursor()

# 查看收缩前的文件大小（SQL Server 2000 使用 sysfiles）
print('=== 收缩前文件大小 ===')
cur.execute("""
SELECT name, size*8/1024.0 AS SizeMB, FILEPROPERTY(name, 'SpaceUsed')*8/1024.0 AS UsedMB
FROM sysfiles
""")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]:.2f} MB (已用 {r[2]:.2f} MB, 空闲 {r[1]-r[2]:.2f} MB)')

# 截断事务日志（SQL Server 2000 兼容写法）
print('\n=== 截断事务日志 ===')
try:
    cur.execute('BACKUP LOG qipei WITH TRUNCATE_ONLY')
    print('  OK')
except Exception as e:
    print(f'  ERROR: {e}')

# 收缩数据文件到 20 MB
print('\n=== 收缩数据文件 qipei -> 20MB ===')
try:
    cur.execute('DBCC SHRINKFILE (qipei, 20)')
    print('  OK')
except Exception as e:
    print(f'  ERROR: {e}')

# 收缩日志文件到 10 MB
print('\n=== 收缩日志文件 qipei_Log -> 10MB ===')
try:
    cur.execute('DBCC SHRINKFILE (qipei_Log, 10)')
    print('  OK')
except Exception as e:
    print(f'  ERROR: {e}')

# 查看收缩后的文件大小
print('\n=== 收缩后文件大小 ===')
cur.execute("""
SELECT name, size*8/1024.0 AS SizeMB, FILEPROPERTY(name, 'SpaceUsed')*8/1024.0 AS UsedMB
FROM sysfiles
""")
for r in cur.fetchall():
    print(f'  {r[0]}: {r[1]:.2f} MB (已用 {r[2]:.2f} MB, 空闲 {r[1]-r[2]:.2f} MB)')

conn.close()
