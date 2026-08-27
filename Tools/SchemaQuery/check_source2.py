import pymssql

conn = pymssql.connect(
    server='frp-bar.com', port=47017, user='QP150',
    password='Dengpeng0716.', database='备用数据',
    tds_version='7.0', charset='utf8', login_timeout=15
)
cur = conn.cursor()

print('=== tbgugys 中 gyno 非空的记录 ===')
cur.execute("SELECT id, LTRIM(RTRIM(gyno)) as gyno, LTRIM(RTRIM(gyname)) as gyname FROM tbgugys WHERE LTRIM(RTRIM(gyno))<>'' ORDER BY id")
for r in cur.fetchall():
    print(f'  id={r[0]}, gyno=[{r[1]}], gyname=[{r[2]}]')

print()
print('=== tbistoed 中去重的 gyno+gyname (gyno非空) ===')
cur.execute("SELECT COUNT(DISTINCT LTRIM(RTRIM(gyno))) FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))<>''")
print(f'  去重 gyno 数量: {cur.fetchone()[0]}')

print()
print('=== tbistoed 去重 gyno+gyname 全部列表 ===')
cur.execute("SELECT DISTINCT LTRIM(RTRIM(gyno)) as gyno, LTRIM(RTRIM(gyname)) as gyname FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))<>'' ORDER BY gyno")
rows = cur.fetchall()
print(f'  共 {len(rows)} 个不同供应商')
for r in rows[:30]:
    print(f'  gyno=[{r[0]}], gyname=[{r[1]}]')
if len(rows) > 30:
    print(f'  ... (还有 {len(rows)-30} 个)')

print()
print('=== 交集检查: tbgugys.gyno 是否在 tbistoed.gyno 中存在 ===')
cur.execute("""
    SELECT DISTINCT LTRIM(RTRIM(a.gyno)) as gyno, LTRIM(RTRIM(a.gyname)) as gyname
    FROM tbgugys a
    WHERE LTRIM(RTRIM(a.gyno))<>''
    AND EXISTS (SELECT 1 FROM tbistoed b WHERE LTRIM(RTRIM(b.gyno)) = LTRIM(RTRIM(a.gyno)))
""")
match_rows = cur.fetchall()
print(f'  交集数量: {len(match_rows)}')
for r in match_rows:
    print(f'  gyno=[{r[0]}], gyname=[{r[1]}]')

print()
print('=== tbistoed 中 gyno 为空但 gyname 非空的去重列表 ===')
cur.execute("SELECT DISTINCT LTRIM(RTRIM(gyname)) as gyname FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))='' AND LTRIM(RTRIM(gyname))<>'' ORDER BY gyname")
rows = cur.fetchall()
print(f'  共 {len(rows)} 个有名称但无编号的供应商')
for r in rows[:20]:
    print(f'  gyname=[{r[0]}]')

conn.close()
