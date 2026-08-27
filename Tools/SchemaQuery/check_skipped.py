import pyodbc

# 源库连接 (和迁移程序一致，使用 ODBC Driver)
src_conn_str = (
    'DRIVER={SQL Server};'
    'SERVER=frp-bar.com,47017;'
    'DATABASE=备用数据;'
    'UID=QP150;'
    'PWD=Dengpeng0716.;'
    'TrustServerCertificate=yes;'
    'Connect Timeout=30;'
)
conn = pyodbc.connect(src_conn_str)
cur = conn.cursor()

print('=' * 60)
print('一、配件数据 tbprnoty 跳过分析')
print('  跳过条件: 目标库已存在相同 partno (全新库 => 源库 nno 重复)')
print('=' * 60)

# 查询 tbprnoty 中 nno 重复的记录
cur.execute("""
    SELECT LTRIM(RTRIM(nno)) AS nno, COUNT(*) as cnt
    FROM tbprnoty
    WHERE nno IS NOT NULL AND LTRIM(RTRIM(nno)) <> ''
    GROUP BY LTRIM(RTRIM(nno))
    HAVING COUNT(*) > 1
    ORDER BY cnt DESC
""")
duplicates = cur.fetchall()
total_dup = sum(r[1] - 1 for r in duplicates)
print(f'重复 nno 的组数: {len(duplicates)}, 预计跳过: {total_dup} 条')
print()

if duplicates:
    print('重复的 nno 列表:')
    for r in duplicates:
        nno = r[0]
        cnt = r[1]
        print(f'  nno=[{nno}], 出现 {cnt} 次 (跳过 {cnt-1} 条)')
        cur.execute("SELECT id, nno, na1, ty, fa FROM tbprnoty WHERE LTRIM(RTRIM(nno))=? ORDER BY id", (nno,))
        for detail in cur.fetchall():
            na1 = detail[2].strip() if detail[2] else ''
            ty = detail[3].strip() if detail[3] else ''
            fa = detail[4].strip() if detail[4] else ''
            print(f'    id={detail[0]}, nno=[{detail[1].strip()}], na1=[{na1}], ty=[{ty}], fa=[{fa}]')

print()
print('=' * 60)
print('二、供应商数据跳过分析')
print('=' * 60)

# 第1步: 从 tbistoed 提取去重的供应商 (和代码逻辑一致)
cur.execute("""
    SELECT DISTINCT LTRIM(RTRIM(gyno)) AS gyno, LTRIM(RTRIM(gyname)) AS gyname
    FROM tbistoed
    WHERE cno IS NOT NULL AND cno <> ''
""")
distinct_suppliers = [(r[0] or '', r[1] or '') for r in cur.fetchall()]

# 第2步: 合并 tbgugys 中 gyno 非空但 tbistoed 中未出现的供应商
cur.execute("SELECT LTRIM(RTRIM(gyno)) AS gyno, LTRIM(RTRIM(gyname)) AS gyname FROM tbgugys WHERE LTRIM(RTRIM(gyno)) <> ''")
gys_rows = cur.fetchall()

existing_gynos = set()
for s in distinct_suppliers:
    g = s[0].strip() if s[0] else ''
    if g:
        existing_gynos.add(g.lower())

for gys in gys_rows:
    gyno = (gys[0] or '').strip()
    if gyno and gyno.lower() not in existing_gynos:
        distinct_suppliers.append((gyno, gys[1] or ''))
        existing_gynos.add(gyno.lower())

print(f'distinctSuppliers 总数: {len(distinct_suppliers)}')

# 构建 gysByName (和代码一致)
cur.execute("SELECT id, gyno, gyname FROM tbgugys")
gys_all = cur.fetchall()
gys_by_name = {}
for row in gys_all:
    gyname = (row[2] or '').strip() if row[2] else ''
    if gyname:
        gys_by_name[gyname.lower()] = row

# 分析跳过情况
skipped_no_match = 0
skipped_dup_sid = 0

seen_sids = set()
skipped_no_match_list = []
skipped_dup_sid_list = []

for row in distinct_suppliers:
    gyno = (row[0] or '').strip()
    gyname = (row[1] or '').strip()

    # gyno 为空时的处理
    if not gyno:
        if gyname and gyname.lower() in gys_by_name:
            gys_row = gys_by_name[gyname.lower()]
            gyno = (gys_row[1] or '').strip() if gys_row[1] else ''
            if not gyno:
                gyno = 'S' + str(gys_row[0])
        else:
            skipped_no_match += 1
            skipped_no_match_list.append({'gyno': '', 'gyname': gyname})
            continue

    # 检查 sid 重复
    sid_key = gyno.lower()
    if sid_key in seen_sids:
        skipped_dup_sid += 1
        skipped_dup_sid_list.append({'gyno': gyno, 'gyname': gyname})
    else:
        seen_sids.add(sid_key)

print(f'  跳过原因1 - gyno为空且gyname无法在tbgugys匹配: {skipped_no_match} 条')
print(f'  跳过原因2 - sid重复(同一gyno不同gyname组合): {skipped_dup_sid} 条')
print(f'  总跳过: {skipped_no_match + skipped_dup_sid} 条')
print()

if skipped_no_match_list:
    print('--- 跳过原因1: gyno为空且gyname无法匹配 ---')
    for item in skipped_no_match_list:
        print(f'  gyno=[空], gyname=[{item["gyname"]}]')
    print()

if skipped_dup_sid_list:
    print('--- 跳过原因2: sid重复 (同一gyno出现多次，首次已插入) ---')
    for item in skipped_dup_sid_list:
        print(f'  gyno=[{item["gyno"]}], gyname=[{item["gyname"]}]')
    print()

# 额外分析: gyno为空且gyname能在tbgugys匹配的情况
print('--- 补充: gyno为空但gyname能匹配tbgugys的(用S+id生成sid) ---')
count_generated = 0
for row in distinct_suppliers:
    gyno = (row[0] or '').strip()
    gyname = (row[1] or '').strip()
    if not gyno and gyname:
        if gyname.lower() in gys_by_name:
            gys_row = gys_by_name[gyname.lower()]
            real_gyno = (gys_row[1] or '').strip() if gys_row[1] else ''
            if not real_gyno:
                print(f'  gyname=[{gyname}] -> 生成 sid=S{gys_row[0]}')
                count_generated += 1
if count_generated == 0:
    print('  (无)')
print()

# 验证总数
cur.execute("SELECT COUNT(*) FROM tbprnoty WHERE nno IS NOT NULL AND LTRIM(RTRIM(nno)) <> ''")
total_parts = cur.fetchone()[0]
print(f'验证: tbprnoty 总数(非空nno) = {total_parts}, 迁移25900+跳过3 = {25900+3}')

conn.close()
print('\n完成。')
