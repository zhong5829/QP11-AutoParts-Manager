import pymssql

# 连接源库 - 尝试不同 TDS 版本
for tds in ['7.1', '7.0', '8.0', '7.2', '7.3']:
    try:
        print(f'尝试 TDS version={tds}...')
        conn = pymssql.connect(
            server='frp-bar.com',
            port=47017,
            user='QP150',
            password='Dengpeng0716.',
            database='备用数据',
            tds_version=tds,
            charset='utf8',
            login_timeout=15
        )
        print(f'成功! TDS={tds}')
        cur = conn.cursor()

        print()
        print('=== 源库 tbgugys 前10条 ===')
        cur.execute('SELECT TOP 10 id, gyno, gyname FROM tbgugys ORDER BY id')
        for r in cur.fetchall():
            print(f'  id={r[0]}, gyno=[{r[1]}], gyname=[{r[2]}]')

        print()
        cur.execute('SELECT COUNT(*) FROM tbgugys')
        print(f'=== tbgugys 总数: {cur.fetchone()[0]} ===')

        cur.execute("SELECT COUNT(*) FROM tbgugys WHERE LTRIM(RTRIM(gyno))=''")
        print(f'=== gyno 为空: {cur.fetchone()[0]} ===')

        print()
        print('=== 源库 tbistoed gyno/gyname 样本 ===')
        cur.execute('SELECT TOP 10 id, cno, gyno, gyname FROM tbistoed ORDER BY id')
        for r in cur.fetchall():
            print(f'  id={r[0]}, cno=[{r[1]}], gyno=[{r[2]}], gyname=[{r[3]}]')

        print()
        cur.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>''")
        print(f'=== tbistoed 有cno的记录: {cur.fetchone()[0]} ===')

        cur.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))=''")
        print(f'=== tbistoed gyno为空: {cur.fetchone()[0]} ===')

        cur.execute("SELECT COUNT(*) FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyname))=''")
        print(f'=== tbistoed gyname为空: {cur.fetchone()[0]} ===')

        print()
        print('=== 源库 tbistoed 按 gyno 分组统计（前15）===')
        cur.execute("SELECT TOP 15 gyno, gyname, COUNT(*) as cnt FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' GROUP BY gyno, gyname ORDER BY cnt DESC")
        for r in cur.fetchall():
            print(f'  gyno=[{r[0]}], gyname=[{r[1]}], count={r[2]}')

        print()
        print('=== 源库 tbistoed gyno 非空但 gyname 为空的 ===')
        cur.execute("SELECT TOP 10 gyno, gyname, COUNT(*) as cnt FROM tbistoed WHERE cno IS NOT NULL AND cno<>'' AND LTRIM(RTRIM(gyno))<>'' AND LTRIM(RTRIM(gyname))='' GROUP BY gyno, gyname ORDER BY cnt DESC")
        for r in cur.fetchall():
            print(f'  gyno=[{r[0]}], gyname=[{r[1]}], count={r[2]}')

        conn.close()
        break
    except Exception as e:
        print(f'失败: {e}')
        print()
