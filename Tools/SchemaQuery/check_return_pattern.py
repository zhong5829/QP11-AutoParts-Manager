# -*- coding: utf-8 -*-
"""
查询源库退货模式 + 本地库退货单特征
1. 源库 tbsada：按 paper 分组，统计纯负单/纯正单/混合单的数量
2. 源库 tbistoed：同上
3. 本地库 bill_sell：total 正负分布、flag 分布
4. 本地库 bill_buy：total 正负分布、flag 分布
"""
import pymssql

def decode_gbk(raw):
    if raw is None:
        return ''
    if isinstance(raw, str):
        return raw
    try:
        return raw.decode('gbk').rstrip('\x00 ').strip()
    except Exception:
        return repr(raw)

# ========== 1. 源库 tbsada 退货模式 ==========
print('=' * 60)
print('1. 源库 tbsada 按 paper 分组的退货模式')
print('=' * 60)
src = pymssql.connect(
    server='frp-bar.com', port=47017,
    user='QP150', password='Dengpeng0716.',
    database='备用数据', charset='utf8',
    tds_version='7.0', timeout=120
)
scur = src.cursor()

# 统计每个 paper 的正负明细分布
scur.execute("""
SELECT paper_sign, COUNT(*) AS bill_cnt, SUM(detail_cnt) AS total_details
FROM (
    SELECT paper,
           CASE
               WHEN MIN(ckamount) >= 0 AND MAX(ckamount) >= 0 THEN 'all_positive'
               WHEN MIN(ckamount) <= 0 AND MAX(ckamount) <= 0 THEN 'all_negative'
               ELSE 'mixed'
           END AS paper_sign,
           COUNT(*) AS detail_cnt
    FROM tbsada
    WHERE paper IS NOT NULL AND paper != ''
    GROUP BY paper
) t
GROUP BY paper_sign
ORDER BY bill_cnt DESC
""")
for r in scur.fetchall():
    print(f'  {r[0]}: {r[1]} 张单据, {r[2]} 条明细')

print()
print('--- tbsada 纯负单（整单退货）样本（前5张）---')
scur.execute("""
SELECT TOP 5 paper, COUNT(*) AS cnt, SUM(ckamount) AS total_amt
FROM tbsada
WHERE paper IN (
    SELECT paper FROM (
        SELECT paper FROM tbsada
        WHERE paper IS NOT NULL AND paper != ''
        GROUP BY paper
        HAVING MIN(ckamount) <= 0 AND MAX(ckamount) <= 0
    ) x
)
GROUP BY paper
ORDER BY paper
""")
for r in scur.fetchall():
    print(f'  paper=[{decode_gbk(r[0] if isinstance(r[0], bytes) else r[0].encode("latin1") if isinstance(r[0], str) else r[0])}], 明细数={r[1]}, 总数量={r[2]}')

# ========== 2. 源库 tbistoed 退货模式 ==========
print()
print('=' * 60)
print('2. 源库 tbistoed 按 cno 分组的退货模式')
print('=' * 60)
scur.execute("""
SELECT cno_sign, COUNT(*) AS bill_cnt, SUM(detail_cnt) AS total_details
FROM (
    SELECT cno,
           CASE
               WHEN MIN(jkamount) > 0 AND MAX(jkamount) > 0 THEN 'all_positive'
               WHEN MIN(jkamount) < 0 AND MAX(jkamount) < 0 THEN 'all_negative'
               ELSE 'mixed_or_zero'
           END AS cno_sign,
           COUNT(*) AS detail_cnt
    FROM tbistoed
    WHERE cno IS NOT NULL AND cno != ''
    GROUP BY cno
) t
GROUP BY cno_sign
ORDER BY bill_cnt DESC
""")
for r in scur.fetchall():
    print(f'  {r[0]}: {r[1]} 张单据, {r[2]} 条明细')

src.close()

# ========== 3. 本地库 bill_sell 特征 ==========
print()
print('=' * 60)
print('3. 本地库 bill_sell.total 正负分布（验证退货是否已迁移）')
print('=' * 60)
tgt = pymssql.connect(
    server='192.168.1.86', port=1433,
    user='sa', password='593106',
    database='qipei', charset='utf8'
)
tcur = tgt.cursor()

tcur.execute("""
SELECT
    CASE WHEN total > 0 THEN 'positive' WHEN total < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM bill_sell
GROUP BY CASE WHEN total > 0 THEN 'positive' WHEN total < 0 THEN 'negative' ELSE 'zero' END
""")
for r in tcur.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('--- 本地库 bill_sell.flag x total正负 交叉分布 ---')
tcur.execute("""
SELECT flag,
       CASE WHEN total > 0 THEN 'pos' WHEN total < 0 THEN 'neg' ELSE 'zero' END AS sign_val,
       COUNT(*) AS cnt
FROM bill_sell
GROUP BY flag, CASE WHEN total > 0 THEN 'pos' WHEN total < 0 THEN 'neg' ELSE 'zero' END
ORDER BY flag, sign_val
""")
for r in tcur.fetchall():
    print(f'  flag={r[0]}, total={r[1]}: {r[2]}')

print()
print('--- 本地库 detail_sell.amount 正负分布 ---')
tcur.execute("""
SELECT
    CASE WHEN amount > 0 THEN 'positive' WHEN amount < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM detail_sell
GROUP BY CASE WHEN amount > 0 THEN 'positive' WHEN amount < 0 THEN 'negative' ELSE 'zero' END
""")
for r in tcur.fetchall():
    print(f'  {r[0]}: {r[1]}')

# ========== 4. 本地库 bill_buy 特征 ==========
print()
print('=' * 60)
print('4. 本地库 bill_buy.total 正负分布')
print('=' * 60)
tcur.execute("""
SELECT
    CASE WHEN total > 0 THEN 'positive' WHEN total < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM bill_buy
GROUP BY CASE WHEN total > 0 THEN 'positive' WHEN total < 0 THEN 'negative' ELSE 'zero' END
""")
for r in tcur.fetchall():
    print(f'  {r[0]}: {r[1]}')

print()
print('--- 本地库 detail_buy.amount 正负分布 ---')
tcur.execute("""
SELECT
    CASE WHEN amount > 0 THEN 'positive' WHEN amount < 0 THEN 'negative' ELSE 'zero' END AS sign_val,
    COUNT(*) AS cnt
FROM detail_buy
GROUP BY CASE WHEN amount > 0 THEN 'positive' WHEN amount < 0 THEN 'negative' ELSE 'zero' END
""")
for r in tcur.fetchall():
    print(f'  {r[0]}: {r[1]}')

tgt.close()
