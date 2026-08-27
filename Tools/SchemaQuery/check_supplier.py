import pymssql

# 连接目标库
conn = pymssql.connect(server='192.168.1.86', port=1433, user='sa', password='593106', database='qipei')
cur = conn.cursor()

print('=== 目标库 supplier_infor 前15条 ===')
cur.execute('SELECT TOP 15 sid, name FROM supplier_infor ORDER BY sid')
for r in cur.fetchall():
    print(f'  sid=[{r[0]}], name=[{r[1]}]')

print()
cur.execute('SELECT COUNT(*) FROM supplier_infor')
print(f'=== supplier_infor 总数: {cur.fetchone()[0]} ===')

cur.execute("SELECT COUNT(*) FROM supplier_infor WHERE sid LIKE 'S%'")
print(f'=== sid 以 S 开头: {cur.fetchone()[0]} ===')

print()
print('=== 目标库 bill_buy 供应商字段样本 ===')
cur.execute('SELECT TOP 10 sn, supplier, total FROM bill_buy ORDER BY sn')
for r in cur.fetchall():
    print(f'  sn=[{r[0]}], supplier=[{r[1]}], total={r[2]}')

print()
cur.execute("SELECT COUNT(*) FROM bill_buy WHERE supplier IS NULL OR LTRIM(RTRIM(supplier))=''")
print(f'=== bill_buy supplier 为空: {cur.fetchone()[0]} ===')

cur.execute('SELECT COUNT(*) FROM bill_buy')
print(f'=== bill_buy 总数: {cur.fetchone()[0]} ===')

print()
print('=== bill_buy supplier 非空的样本 ===')
cur.execute("SELECT TOP 10 sn, supplier FROM bill_buy WHERE LTRIM(RTRIM(supplier))<>'' ORDER BY sn")
for r in cur.fetchall():
    print(f'  sn=[{r[0]}], supplier=[{r[1]}]')

conn.close()

print()
print('========================================')
# 连接源库
conn2 = pymssql.connect(server='frp-bar.com', port=47017, user='QP150', password='Dengpeng0716.', database='备用数据')
cur2 = conn2.cursor()

print('=== 源库 tbgugys 前10条 ===')
cur2.execute('SELECT TOP 10 id, gyno, gyname FROM tbgugys ORDER BY id')
for r in cur2.fetchall():
    print(f'  id={r[0]}, gyno=[{r[1]}], gyname=[{r[2]}]')

print()
cur2.execute('SELECT COUNT(*) FROM tbgugys')
print(f'=== tbgugys 总数: {cur2.fetchone()[0]} ===')

cur2.execute("SELECT COUNT(*) FROM tbgugys WHERE LTRIM(RTRIM(gyno))=''")
print(f'=== gyno 为空: {cur2.fetchone()[0]} ===')

print()
print('=== 源库 tbistoed gyno/gyname 样本 ===')
cur2.execute('SELECT TOP 10 id, cno, gyno, gyname FROM tbistoed ORDER BY id')
for r in cur2.fetchall():
    print(f'  id={r[0]}, cno=[{r[1]}], gyno=[{r[2]}], gyname=[{r[3]}]')

print()
cur2.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>''")
print(f'=== tbistoed 有cno的记录: {cur2.fetchone()[0]} ===')

cur2.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))=''")
print(f'=== tbistoed gyno为空: {cur2.fetchone()[0]} ===')

cur2.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyname))=''")
print(f'=== tbistoed gyname为空: {cur2.fetchone()[0]} ===')

print()
print('=== 源库 tbistoed 按 gyno 分组统计 ===')
cur2.execute("SELECT TOP 10 gyno, gyname, COUNT(*) as cnt FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' GROUP BY gyno, gyname ORDER BY cnt DESC")
for r in cur2.fetchall():
    print(f'  gyno=[{r[0]}], gyname=[{r[1]}], count={r[2]}')

conn2.close()
