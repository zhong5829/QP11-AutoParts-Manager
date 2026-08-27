import pymssql

conn = pymssql.connect(server='192.168.1.86', port=1433, user='sa', password='593106', database='qipei')
cur = conn.cursor()

tables = ['supplier_infor', 'bill_buy', 'detail_buy']

for t in tables:
    cur.execute(f'SELECT COUNT(*) FROM {t}')
    before = cur.fetchone()[0]
    cur.execute(f'DELETE FROM {t}')
    conn.commit()
    print(f'{t}: 已清空（删除前 {before} 条）')

conn.close()
print('完成！')