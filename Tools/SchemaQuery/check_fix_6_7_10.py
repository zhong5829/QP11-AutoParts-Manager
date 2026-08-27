import pymssql
import pyodbc

# 源库查询
src = pymssql.connect(
    server='frp-bar.com', port=47017, user='QP150',
    password='Dengpeng0716.', database='备用数据',
    tds_version='7.0', charset='utf8', login_timeout=15
)
src_cur = src.cursor()

print('=== 1. 源库 tbisto 多仓位配件统计 ===')
src_cur.execute("""
    SELECT nno, COUNT(*) as cnt
    FROM tbisto
    WHERE nno IS NOT NULL AND nno<>''
    GROUP BY nno
    HAVING COUNT(*) > 1
    ORDER BY cnt DESC
""")
multi = src_cur.fetchall()
print(f'  tbisto 中有多仓位的配件数: {len(multi)}')
src_cur.execute("SELECT COUNT(DISTINCT nno) FROM tbisto WHERE nno IS NOT NULL AND nno<>''")
total_parts = src_cur.fetchone()[0]
print(f'  tbisto 中配件总数(去重): {total_parts}')
if multi:
    print(f'  多仓位配件样本 (前5):')
    for r in multi[:5]:
        print(f'    nno=[{r[0].strip()}], 仓位数={r[1]}')
    print(f'\n  样本 nno 的明细:')
    src_cur.execute("""
        SELECT nno, LTRIM(RTRIM(posi)), kcamount
        FROM tbisto WHERE nno=%s ORDER BY posi
    """, (multi[0][0],))
    for r in src_cur.fetchall():
        print(f'    nno=[{r[0].strip()}], posi=[{r[1]}], kcamount={r[2]}')

print('\n=== 2. 源库 tbposi 数据样本 ===')
src_cur.execute("SELECT TOP 10 LTRIM(RTRIM(nno)), LTRIM(RTRIM(posi)), isfree FROM tbposi WHERE nno IS NOT NULL AND nno<>''")
for r in src_cur.fetchall():
    print(f'  nno=[{r[0]}], posi=[{r[1]}], isfree={r[2]}')
src_cur.execute("SELECT COUNT(*) FROM tbposi WHERE nno IS NOT NULL AND nno<>''")
print(f'  tbposi 总行数: {src_cur.fetchone()[0]}')
src_cur.execute("SELECT COUNT(DISTINCT nno) FROM tbposi WHERE nno IS NOT NULL AND nno<>''")
print(f'  tbposi 配件数(去重): {src_cur.fetchone()[0]}')

src.close()

# 目标库查询
tgt = pyodbc.connect(
    'Driver={SQL Server};Server=192.168.1.86,1433;Database=opt;Uid=sa;Pwd=593106;',
    timeout=15
)
tgt_cur = tgt.cursor()

print('\n=== 3. 目标库 part_stock 主键约束 ===')
tgt_cur.execute("""
    SELECT k.COLUMN_NAME, k.ORDINAL_POSITION
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS t
    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
        ON t.CONSTRAINT_NAME = k.CONSTRAINT_NAME
        AND t.TABLE_SCHEMA = k.TABLE_SCHEMA
    WHERE t.TABLE_NAME='part_stock' AND t.CONSTRAINT_TYPE='PRIMARY KEY'
    ORDER BY k.ORDINAL_POSITION
""")
pk = tgt_cur.fetchall()
print(f'  part_stock 主键列: {[(r[0], r[1]) for r in pk]}')

print('\n=== 4. 目标库 part_stock 现有数据 ===')
tgt_cur.execute("SELECT COUNT(*) FROM part_stock")
print(f'  part_stock 总行数: {tgt_cur.fetchone()[0]}')
tgt_cur.execute("SELECT COUNT(DISTINCT partid) FROM part_stock")
print(f'  part_stock partid 去重数: {tgt_cur.fetchone()[0]}')
tgt_cur.execute("SELECT TOP 5 partid, place, amount FROM part_stock")
for r in tgt_cur.fetchall():
    print(f'  partid={r[0]}, place=[{r[1]}], amount={r[2]}')

print('\n=== 5. 目标库 supplier_infor 确认无 zq/note 列 ===')
tgt_cur.execute("""
    SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='supplier_infor'
    AND COLUMN_NAME IN ('zq','note','memo','remark','credit_days','account_period')
""")
missing = tgt_cur.fetchall()
print(f'  匹配 zq/note/memo/remark/credit_days/account_period 的列: {[r[0] for r in missing]}')
print('  (空列表 = 目标表确实没有这些列)')

tgt.close()
print('\n查询完成。')
