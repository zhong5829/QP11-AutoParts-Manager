// ========== 全局状态 ==========
const state = {
  details: [],          // 销售明细列表
  selectedClient: null, // 选中的客户 { id, name }
  workers: [],          // 业务员列表
  currentPart: null,    // 当前选中的配件（用于弹窗）
  allParts: [],         // 配件搜索完整结果（用于废品仓过滤）
};

// ========== 前端缓存（sessionStorage，库存数据不缓存保证实时） ==========
const Cache = {
  _prefix: 'qp11_',
  set(key, data, ttlMinutes = 5) {
    const item = { d: data, t: Date.now(), e: ttlMinutes * 60000 };
    try { sessionStorage.setItem(this._prefix + key, JSON.stringify(item)); } catch {}
  },
  get(key) {
    try {
      const raw = sessionStorage.getItem(this._prefix + key);
      if (!raw) return null;
      const item = JSON.parse(raw);
      if (Date.now() - item.t > item.e) { sessionStorage.removeItem(this._prefix + key); return null; }
      return item.d;
    } catch { return null; }
  },
  remove(key) { try { sessionStorage.removeItem(this._prefix + key); } catch {} },
};

// ========== 调试日志工具 ==========
const LOG = {
  _enabled: true,
  prefix: '[QP11-Web]',
  info(...args) { if (this._enabled) console.log(this.prefix, ...args); },
  warn(...args) { if (this._enabled) console.warn(this.prefix, ...args); },
  error(...args) { console.error(this.prefix, ...args); },
  api(method, url, data) {
    this.info(`API ${method} ${url}`, data ? JSON.stringify(data).slice(0, 500) : '');
  },
  resp(url, status, data) {
    const preview = typeof data === 'object' ? JSON.stringify(data).slice(0, 300) : String(data);
    this.info(`API RESP ${url} [${status}]`, preview);
  },
};

// ========== 工具函数 ==========
const $ = (id) => document.getElementById(id);
const API = (path) => `/api${path}`;
const fmt = (n) => n == null ? '0.00' : Number(n).toFixed(2);

/** Token key for localStorage */
const TOKEN_KEY = 'qp11_token';

function showToast(msg, type = '') {
  const t = $('toast');
  t.textContent = msg; t.className = `toast show ${type}`;
  clearTimeout(t._timer);
  t._timer = setTimeout(() => t.classList.remove('show'), 2500);
}

// ========== 登录认证 ==========

/** 获取当前 Token */
function getAuthToken() { return localStorage.getItem(TOKEN_KEY) || ''; }

/** 保存 Token */
function saveAuthToken(token) { localStorage.setItem(TOKEN_KEY, token); }

/** 清除 Token 并显示登录页 */
function logout() {
  localStorage.removeItem(TOKEN_KEY);
  $('loginOverlay').classList.remove('hidden');
  $('loginError').style.display = 'none';
  $('loginPwd').value = '';
  loadLoginUsers(); // 重新加载用户列表
}

/** 隐藏登录遮罩（登录成功后调用） */
function hideLoginOverlay() { $('loginOverlay').classList.add('hidden'); }

/** 执行登录 */
async function doLogin() {
  const user = $('loginUser').value.trim();
  const pwd = $('loginPwd').value;
  const errEl = $('loginError');
  const btn = $('btnLogin');

  if (!user) { errEl.textContent = '请选择用户'; errEl.style.display = 'block'; return; }
  if (!pwd) { errEl.textContent = '请输入密码'; errEl.style.display = 'block'; $('loginPwd').focus(); return; }
  errEl.style.display = 'none';
  btn.disabled = true; btn.textContent = '登录中...';

  try {
    LOG.api('POST', '/auth/login', { username: user });
    const res = await fetch(API('/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: user, Password: pwd })
    });
    const json = await res.json();

    if (res.ok && json.token) {
      saveAuthToken(json.token);
      hideLoginOverlay();
      showToast(`欢迎, ${json.name || json.username}`, 'success');
      // 登录成功后才初始化页面数据
      await loadWorkers();
      setDefaultDates();
    } else {
      errEl.textContent = json.error || '登录失败';
      errEl.style.display = 'block';
      $('loginPwd').value = '';
      $('loginPwd').focus();
    }
  } catch (e) {
    errEl.textContent = '网络错误，请检查连接';
    errEl.style.display = 'block';
    LOG.error('[doLogin]', e);
  } finally {
    btn.disabled = false; btn.textContent = '登 录';
  }
}

/** 加载用户列表到登录下拉框 */
async function loadLoginUsers() {
  try {
    const res = await fetch(API('/auth/users'));
    if (!res.ok) return;
    const json = await res.json();
    const users = json.data || json || [];
    const sel = $('loginUser');
    if (users.length > 0) {
      sel.innerHTML = '<option value="">-- 请选择 --</option>' +
        users.map(u => `<option value="${u.Username || u.username}">${u.Name || u.name || u.Username || u.username}</option>`).join('');
      sel.selectedIndex = 1; // 默认选中第一个用户
    }
    $('loginPwd').focus();
  } catch (e) {
    LOG.error('[loadLoginUsers]', e);
  }
}

/** 原始 fetch（带 token 的封装） */
async function authFetch(url, options = {}) {
  const token = getAuthToken();
  if (token) {
    options.headers = options.headers || {};
    options.headers['Authorization'] = `Bearer ${token}`;
  }
  LOG.api(options.method || 'GET', url);
  const res = await fetch(url, options);

  // 401 → Token 过期或无效，跳转登录
  if (res.status === 401) {
    logout();
    throw new Error('未登录或登录已过期');
  }
  return res;
}

// ========== Tab 切换 ==========
document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    LOG.info('Tab 切换 ->', btn.dataset.tab);
    closeAllModals();
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    btn.classList.add('active');
    $(`tab-${btn.dataset.tab}`).classList.add('active');
  });
});

/** 关闭所有弹窗和下拉 */
function closeAllModals() {
  $('partDialog')?.classList.remove('show');
  $('billDetailModal')?.classList.remove('show');
  $('clientDropdown')?.classList.remove('show');
}

// ========== 初始化 ==========
async function init() {
  LOG.info('========== 页面初始化开始 ==========');
  bindEvents();
  bindModalCloseOnBackdrop();

  // 检查是否有已保存的 Token
  const token = getAuthToken();
  if (token) {
    // 验证 Token 是否仍然有效
    try {
      const res = await fetch(API('/auth/verify'), { headers: { 'Authorization': `Bearer ${token}` } });
      if (res.ok) {
        hideLoginOverlay(); // Token 有效，隐藏登录页
        await loadWorkers();
        setDefaultDates();
        LOG.info('========== 自动登录成功 ==========');
      } else {
        logout(); // Token 过期
      }
    } catch (e) {
      logout(); // 网络错误也显示登录页（让用户手动登录）
    }
  } else {
    // 无 Token，显示登录页并加载用户列表
    await loadLoginUsers();
    // 回车键：密码框回车触发登录
    $('loginPwd').addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
  }
}
init();

// ---------- 1. 加载业务员（缓存10分钟，后台静默刷新） ----------
async function loadWorkers() {
  // 优先读缓存
  const cached = Cache.get('workers');
  if (cached) { state.workers = cached; renderWorkerSelects(); }

  // 后台静默刷新（保证数据不过期太久）
  try {
    const res = await authFetch(API('/workers'));
    if (!res.ok) return;
    const json = await res.json();
    const data = json.data || json || [];
    state.workers = data;
    Cache.set('workers', data, 10); // 缓存10分钟
    renderWorkerSelects();
  } catch (e) {
    if (!cached) LOG.error('[loadWorkers] 缓存和网络都失败', e);
  }
}

function renderWorkerSelects() {
  const sel1 = $('workerSelect');
  const sel2 = $('qWorker');
  [sel1, sel2].forEach(sel => {
    sel.innerHTML = sel.id === 'qWorker'
      ? '<option value="">全部</option>'
      : '<option value="">请选择</option>';
    state.workers.forEach(w => {
      const opt = document.createElement('option');
      opt.value = w.workid || w.Workid || w.Name || '';
      opt.textContent = w.name || w.Name || '';
      sel.appendChild(opt);
    });
  });
  // 业务员默认选中"邓鹏"
  const defWorker = Array.from(sel1.options).find(o => o.textContent.includes('邓鹏'));
  if (defWorker) sel1.value = defWorker.value;
}

function setDefaultDates() {
  const now = new Date();
  const today = now.toISOString().slice(0, 10);
  $('qStart').value = today;
  $('qEnd').value = today;
  // 开单日期默认当天（对齐桌面端 billDate 默认），可修改补录历史日期
  if ($('billDateInput')) $('billDateInput').value = today;
}

// ========== 事件绑定 ==========
function bindEvents() {
  ['qPartNo', 'qPartName', 'qCartype'].forEach(id => {
    $(id).addEventListener('input', debounce(searchParts, 400));
    $(id).addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); searchParts(); } });
  });

  $('clientInput').addEventListener('input', debounce(searchClients, 350));
  $('clientInput').addEventListener('focus', () => {
    if ($('clientInput').value.trim()) searchClients();
  });

  ['cashPay', 'checksPay', 'zhifubaoPay', 'weixinPay', 'discountRate'].forEach(id => {
    $(id).addEventListener('input', calcPayment);
  });

  document.querySelectorAll('input[name="priceType"]').forEach(r => {
    r.addEventListener('change', onPriceTypeChange);
  });

  $('btnAddDetail').addEventListener('click', addDetailFromDialog);
  $('btnSubmit').addEventListener('click', submitOrder);
  $('btnClear').addEventListener('click', clearAll);
  $('btnQuery').addEventListener('click', (e) => {
    LOG.info('[btnQuery] 查询按钮被点击!');
    queryBills();
  });

  // 弹窗内事件
  setupDlgKeyNav();
  setupDlgClientSearch();
  $('dlgPrice').addEventListener('input', onDlgPriceChange);
  $('chkAutoMatch').addEventListener('change', () => { if ($('chkAutoMatch').checked) tryAutoMatchPrice(); });
  $('btnPriceHistory').addEventListener('click', showPriceHistory);
}

/** 点击弹窗背景关闭弹窗 */
function bindModalCloseOnBackdrop() {
  ['partDialog', 'billDetailModal'].forEach(id => {
    const el = $(id);
    if (!el) return;
    el.addEventListener('click', e => {
      if (e.target === el) el.classList.remove('show');
    });
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') { closeAllModals(); }
  });
}

function debounce(fn, ms) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
}

// ========== 一、配件搜索（对齐桌面端 GetStockListAdvancedAsync） ==========
// 桌面端逻辑：
//   - 编号 → 匹配 partno 字段
//   - 名称 → 匹配 name OR name_py OR carname（3个字段！）
//   - 车型 → 匹配 cartype OR cartype_py（2个字段）
//   - 条件之间用 AND 连接
//   - 数据源：JOIN part_stock LEFT JOIN part_data
//   - 排序：ORDER BY partno ASC
// 匹配模式映射：HTML select selectedIndex → 后端 queryMode
//   0=包含(mode3)  1=左匹配(mode1)  2=右匹配(mode2)  3=精确(mode0)
const MATCH_MODE_MAP = [3, 1, 2, 0]; // selectedIndex → queryMode

async function searchParts() {
  const no = $('qPartNo').value.trim();
  const name = $('qPartName').value.trim();
  const cartype = $('qCartype').value.trim();
  const selIdx = $('qMatchMode').selectedIndex;
  const matchMode = MATCH_MODE_MAP[selIdx] ?? 3;
  const modeNames = ['包含', '左匹配', '右匹配', '精确'];

  LOG.info(`[searchParts] 编号="${no}" | 名称="${name}" | 车型="${cartype}" | 模式=${modeNames[selIdx]}(queryMode=${matchMode})`);

  try {
    // 三个条件分别作为独立参数传递
    const params = new URLSearchParams();
    if (no) params.set('partNo', no);
    if (name) params.set('partName', name);
    if (cartype) params.set('cartype', cartype);
    params.set('matchMode', String(matchMode));

    const url = API(`/parts?${params}`);
    LOG.api('GET', url);

    const res = await authFetch(url);
    LOG.info(`[searchParts] HTTP ${res.status}`);

    if (!res.ok) {
      const errText = await res.text();
      LOG.error(`[searchParts] 失败! ${res.status}`, errText);
      showToast('搜索失败: HTTP ' + res.status, 'error');
      return;
    }

    const json = await res.json();
    LOG.resp('/parts', res.status, json);

    const data = json.data || json;
    LOG.info(`[searchParts] 返回: ${Array.isArray(data) ? data.length + '条' : typeof data}`);
    if (Array.isArray(data) && data.length > 0)
      LOG.info(`[searchParts] 第1条:`, Object.keys(data[0]), data[0]);

    renderParts(data);
  } catch (e) {
    LOG.error('[searchParts] 异常!', e);
    showToast('搜索失败: ' + e.message, 'error');
  }
}

function renderParts(parts) {
  // 缓存完整结果，用于废品仓过滤
  state.allParts = Array.isArray(parts) ? parts : [];
  _renderPartsFiltered();
}

/** 根据废品仓复选框状态渲染配件列表 */
function _renderPartsFiltered() {
  const tbody = $('partsBody');
  const hideWaste = $('chkHideWaste')?.checked;
  let parts = state.allParts;

  if (hideWaste) {
    parts = parts.filter(p => (p.Place || '') !== '废品仓');
  }

  if (!parts || parts.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="empty-hint">' + (state.allParts.length > 0 && hideWaste ? '已隐藏全部废品仓配件' : '未找到匹配的配件') + '</td></tr>';
    return;
  }

  tbody.innerHTML = parts.map(p => `
    <tr data-part='${escapeAttr(JSON.stringify(p))}' onclick="onPartClick(this)">
      <td>${escHtml(p.Partno || p.PartNo)}</td>
      <td>${escHtml(p.Name)}</td>
      <td>${escHtml(p.Cartype || p.CarType)}</td>
      <td>${p.Stock ?? p.Amount ?? '-'}</td>
      <td>${fmt(p.LsPrice)}</td>
      <td>${fmt(p.PfPrice)}</td>
      <td>${escHtml(p.Place)}</td>
    </tr>`).join('');
}

/** 切换废品仓显示/隐藏 */
function toggleWasteFilter() {
  _renderPartsFiltered();
}

function onPartClick(row) {
  document.querySelectorAll('#partsBody tr').forEach(r => r.classList.remove('selected'));
  row.classList.add('selected');
  const part = JSON.parse(row.dataset.part);

  const partId = part.Partid || part.PartId;
  const partno = part.PartNo || part.Partno || '';
  const name = part.Name || '';
  const stockAmount = (part.Stock ?? part.Amount ?? 0) || 0;

  // 对齐桌面端 L287-294：库存为0时，打开只读模式查看历史
  if (stockAmount <= 0) {
    openPartDialog(part, true); // readOnly=true
    return;
  }

  // 对齐桌面端 L296-303：废品仓配件需二次确认
  const place = (part.Place || '').trim();
  if (place === '废品仓') {
    if (!confirm(`配件 [${partno} ${name}] 为废品仓库存，是否确定出售？`)) return;
  }

  // 对齐桌面端 L305-329：检查该配件是否已在明细中
  const existingIdx = state.details.findIndex(d => d.PartId === partId);
  if (existingIdx >= 0) {
    if (!confirm(`配件 [${partno} ${name}] 已在明细中，是否修改？`)) return;
    // 是 → 弹出编辑窗口（修改模式）
    openPartDialog(part, false, existingIdx);
    return;
  }

  // 正常新增
  openPartDialog(part, false, -1);
}

// ========== 二、客户搜索 ==========
async function searchClients() {
  const keyword = $('clientInput').value.trim();
  const dd = $('clientDropdown');

  if (!keyword) { dd.classList.remove('show'); dd.innerHTML = ''; return; }

  // 客户搜索缓存5分钟（库存不缓存，但客户名搜索可以缓存）
  const cacheKey = `client_${keyword}`;
  const cached = Cache.get(cacheKey);
  if (cached) { renderClientDropdown(cached, dd); return; }

  const url = API(`/clients?keyword=${encodeURIComponent(keyword)}&limit=10`);

  try {
    const res = await authFetch(url);
    if (!res.ok) return;

    const clients = await res.json();
    const list = clients.data || clients || [];
    Cache.set(cacheKey, list, 5); // 缓存5分钟
    renderClientDropdown(list, dd);
  } catch (e) { LOG.error('[searchClients]', e); }
}

function renderClientDropdown(list, dd) {
  if (!list.length) { dd.classList.remove('show'); return; }
  dd.innerHTML = list.map(c =>
    `<div class="dropdown-item" data-id="${c.Cid}" data-name="${escHtml(c.Name)}"
         onclick="selectClient(this)">${escHtml(c.Name)} (${escHtml(c.Mobile || c.Tel || c.Linkman || '-')})</div>`
  ).join('');
  dd.classList.add('show');
}

function selectClient(el) {
  state.selectedClient = { id: el.dataset.id, name: el.dataset.name };
  $('clientInput').value = el.dataset.name;
  $('clientDropdown').classList.remove('show');
  $('dlgClientName').textContent = el.dataset.name;
}

document.addEventListener('click', e => {
  if (!$('clientInput').contains(e.target) && !$('clientDropdown').contains(e.target))
    $('clientDropdown').classList.remove('show');
});

// ========== 三、添加配件弹窗（完全对齐桌面端 SellEditDialog） ==========

let dlgState = { part: null, lsPrice: 0, pfPrice: 0, syncingPrice: false, selectedClient: null, readOnly: false, editIndex: -1 };

/** 打开弹窗 - 对齐桌面端构造函数
 * @param {object} part - 配件数据
 * @param {boolean} [readOnly=false] - 只读模式（库存为0）
 * @param {number} [editIndex=-1] - 修改模式时明细索引，-1表示新增
 */
function openPartDialog(part, readOnly = false, editIndex = -1) {
  dlgState.part = part;
  dlgState.lsPrice = part.LsPrice ?? 0;
  dlgState.pfPrice = part.PfPrice ?? 0;
  dlgState.syncingPrice = false;
  dlgState.selectedClient = state.selectedClient ? { id: state.selectedClient.id, name: state.selectedClient.name } : null;
  dlgState.readOnly = !!readOnly;
  dlgState.editIndex = editIndex;

  // 配件信息（只读）- 对齐桌面端第52-54行
  $('dlgPno').textContent = part.PartNo || part.Partno || '';
  $('dlgPname').textContent = part.Name || '';

  // 隐藏价格提示
  const hint = $('dlgPriceHint');
  hint.style.display = 'none';
  hint.textContent = '';
  hint.className = 'price-hint';

  // 默认值 - 对齐桌面端第56-58行
  $('rbRetail').checked = true;
  $('dlgPrice').value = dlgState.lsPrice || '';
  $('dlgBillPrice').value = dlgState.lsPrice || '';

  // 清空输入 - 对齐桌面端默认值
  $('dlgAmount').value = '1';
  $('dlgCartype').value = part.Cartype || part.CarType || '';

  // 客户 - 对齐桌面端第60-61行
  $('dlgClient').value = dlgState.selectedClient ? dlgState.selectedClient.name : '';

  // 自动匹配默认勾选
  $('chkAutoMatch').checked = true;

  // ---- 只读模式（对齐桌面端 L84-104：库存为0）----
  const btnOk = $('btnAddDetail');
  if (dlgState.readOnly) {
    // 标题改为历史记录查看
    $('dlgTitle').textContent = `配件历史记录 - ${$('dlgPno').textContent} ${$('dlgPname').textContent}`;
    // 禁用所有输入控件
    $('dlgAmount').disabled = true;
    $('dlgPrice').disabled = true;
    $('dlgBillPrice').disabled = true;
    $('dlgCartype').disabled = true;
    $('rbRetail').disabled = true;
    $('rbWholesale').disabled = true;
    $('chkAutoMatch').disabled = true;
    $('dlgClient').disabled = true;
    // 确定按钮置灰不可点击
    btnOk.disabled = true;
    btnOk.classList.add('btn-disabled');
    // 隐藏历史售价按钮
    $('btnPriceHistory').style.display = 'none';
    // 显示红色提示（对齐桌面端 L101-103）
    hint.textContent = '当前库存为0，仅可查看历史记录';
    hint.style.display = '';
    hint.classList.add('price-hint-danger');
  } else {
    // 正常模式
    $('dlgTitle').textContent = '销售编辑';
    $('dlgAmount').disabled = false;
    $('dlgPrice').disabled = false;
    $('dlgBillPrice').disabled = false;
    $('dlgCartype').disabled = false;
    $('rbRetail').disabled = false;
    $('rbWholesale').disabled = false;
    $('chkAutoMatch').disabled = false;
    $('dlgClient').disabled = false;
    btnOk.disabled = false;
    btnOk.classList.remove('btn-disabled');
    $('btnPriceHistory').style.display = '';
    hint.classList.remove('price-hint-danger');

    // 修改模式：预填已有数据（对齐桌面端 SetEditValues L332-340）
    if (editIndex >= 0 && state.details[editIndex]) {
      const d = state.details[editIndex];
      $('dlgAmount').value = d.Amount || '1';
      $('dlgPrice').value = d.Price || '';
      $('dlgBillPrice').value = d.BillPrice || '';
      $('dlgCartype').value = d.Cartype || '';
      // 取消自动匹配（已有价格）
      $('chkAutoMatch').checked = false;
    }

    // 自动匹配客户历史价格 - 对齐桌面端第107-108行
    if ($('chkAutoMatch').checked && dlgState.selectedClient) {
      tryAutoMatchPrice();
    }
  }

  // 显示弹窗
  $('partDialog').classList.add('show');

  // 加载历史数据 - 对齐桌面端第80-81行
  loadSellHistory();
  loadBuyHistory();

  // 聚焦数量框 - 对齐桌面端第110-111行（只读模式不聚焦）
  if (!dlgState.readOnly) {
    setTimeout(() => { $('dlgAmount').focus(); $('dlgAmount').select(); }, 100);
  }
}

function closePartDialog() {
  $('partDialog').classList.remove('show');
  dlgState.part = null;
}

// ---- 对齐桌面端 RbRetail_Checked / RbWholesale_Checked 第218-234行 ----
function onPriceTypeChange() {
  if (!dlgState.part) return;
  if (dlgState.syncingPrice) return;
  dlgState.syncingPrice = true;

  const type = document.querySelector('input[name="priceType"]:checked').value;
  if (type === 'retail') {
    // 对齐桌面端第221-224行：零售 → 填零售价
    $('dlgPrice').value = dlgState.lsPrice || '';
    $('dlgBillPrice').value = dlgState.lsPrice || '';
  } else {
    // 对齐桌面端第229-233行：批发 → 填批发价
    $('dlgPrice').value = dlgState.pfPrice || '';
    $('dlgBillPrice').value = dlgState.pfPrice || '';
  }

  dlgState.syncingPrice = false;
}

// ---- 对齐桌面端 TxtPrice_TextChanged 第236-242行：销售单价变化自动同步开票单价 ----
function onDlgPriceChange() {
  if (dlgState.syncingPrice) return;
  dlgState.syncingPrice = true;
  $('dlgBillPrice').value = $('dlgPrice').value;
  dlgState.syncingPrice = false;
}

// ---- 对齐桌面端 TxtAmount_PreviewKeyDown / TxtPrice_PreviewKeyDown / TxtBillPrice_PreviewKeyDown ----
// Enter键跳转：数量→单价→开票单价→确认
function setupDlgKeyNav() {
  $('dlgAmount').addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); $('dlgPrice').focus(); $('dlgPrice').select(); }
  });
  $('dlgPrice').addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); $('dlgBillPrice').focus(); $('dlgBillPrice').select(); }
  });
  $('dlgBillPrice').addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); addDetailFromDialog(); }
  });
}

// ---- 弹窗内客户搜索 ----
let dlgClientTimer = null;
function setupDlgClientSearch() {
  const input = $('dlgClient');
  const dropdown = $('dlgClientDropdown');

  input.addEventListener('input', () => {
    clearTimeout(dlgClientTimer);
    const kw = input.value.trim();
    if (!kw.length) { dropdown.classList.remove('show'); dropdown.innerHTML = ''; return; }
    dlgClientTimer = setTimeout(async () => {
      try {
        LOG.info('[dlgClient] 搜索:', kw);
        const res = await authFetch(API(`/clients?keyword=${encodeURIComponent(kw)}`));
        const json = await res.json();
        const list = json.data || json;
        if (!list || !list.length) { dropdown.classList.remove('show'); return; }
        dropdown.innerHTML = list.map(c =>
          `<div class="dropdown-item" data-id="${escAttr(c.Cid||c.ClientId)}" data-name="${escAttr(c.Name||c.ClientName)}">${escHtml(c.Name||c.ClientName)}</div>`
        ).join('');
        dropdown.classList.add('show');
        dropdown.querySelectorAll('.dropdown-item').forEach(item => {
          item.addEventListener('click', () => {
            input.value = item.dataset.name;
            dlgState.selectedClient = { id: item.dataset.id, name: item.dataset.name };
            dropdown.classList.remove('show');
            // 对齐桌面端 CmbClient_ClientSelected 第144-152行：选择客户后重新加载历史+自动匹配
            loadSellHistory();
            if ($('chkAutoMatch').checked) tryAutoMatchPrice();
          });
        });
      } catch {}
    }, 300);
  });

  input.addEventListener('blur', () => setTimeout(() => dropdown.classList.remove('show'), 200));
}

// ---- 对齐桌面端 LoadSellHistoryAsync 第154-198行 ----
async function loadSellHistory() {
  try {
    const tbody = $('sellHistoryBody');
    const clientId = dlgState.selectedClient?.id || '';
    const clientName = dlgState.selectedClient?.name || '';
    const partId = dlgState.part?.PartId || dlgState.part?.Partid;

    let url = API(`/parts/${partId}/sell-history`);
    if (clientName) url += `?clientId=${encodeURIComponent(clientName)}`;

    LOG.info('[dlg] 加载销售历史:', url);
    const res = await authFetch(url);
    const json = await res.json();
    const data = json.data || json || [];

    if (!data.length) {
      tbody.innerHTML = '<tr><td colspan="6" class="empty-hint-sm">暂无销售记录</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(r =>
      `<tr><td>${escHtml(r.Sn||'')}</td><td>${(r.Datetime||'').substring(0,10)}</td>` +
      `<td>${escHtml(r.ClientName||'')}</td>` +
      `<td style="text-align:right">${r.Amount||0}</td><td style="text-align:right">${fmt(r.Price)}</td>` +
      `<td style="text-align:right">${fmt(r.BillPrice)}</td></tr>`
    ).join('');
  } catch(e) { LOG.error('[dlg] 销售历史加载失败', e); }
}

// ---- 对齐桌面端 LoadBuyHistoryAsync 第200-216行 ----
async function loadBuyHistory() {
  try {
    const tbody = $('buyHistoryBody');
    const partId = dlgState.part?.PartId || dlgState.part?.Partid;

    LOG.info('[dlg] 加载采购历史, PartId=', partId);
    const res = await authFetch(API(`/parts/${partId}/buy-history`));
    const json = await res.json();
    const data = json.data || json || [];

    if (!data.length) {
      tbody.innerHTML = '<tr><td colspan="4" class="empty-hint-sm">暂无采购记录</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(r =>
      `<tr><td>${(r.Datetime||'').substring(0,10)}</td><td>${escHtml(r.SupplierName||'')}</td>` +
      `<td style="text-align:right">${r.Amount||0}</td><td style="text-align:right">${fmt(r.Inprice)}</td></tr>`
    ).join('');
  } catch(e) { LOG.error('[dlg] 采购历史加载失败', e); }
}

// ---- 对齐桌面端 TryAutoMatchPriceAsync 第250-276行 ----
async function tryAutoMatchPrice() {
  const clientName = dlgState.selectedClient?.name;
  if (!clientName || !dlgState.part) return;

  try {
    const partId = dlgState.part?.PartId || dlgState.part?.Partid;
    LOG.info('[dlg] 自动匹配历史价 PartId=', partId, 'Client=', clientName);
    const res = await authFetch(API(`/parts/${partId}/sell-history?clientId=${encodeURIComponent(clientName)}`));
    const json = await res.json();
    const data = json.data || json || [];

    if (data.length > 0) {
      const last = data[0]; // 最新一条
      dlgState.syncingPrice = true;
      $('dlgPrice').value = last.Price || '';
      $('dlgBillPrice').value = last.BillPrice || last.Price || '';
      dlgState.syncingPrice = false;

      // 显示提示
      const hint = $('dlgPriceHint');
      hint.textContent = `已匹配 ${clientName} 上次售价: 单价=${last.Price} 开票价=${last.BillPrice || last.Price}`;
      hint.style.display = '';
      LOG.info('[dlg] 自动匹配成功 Price=', last.Price, 'BillPrice=', last.BillPrice);
    }
  } catch(e) { LOG.error('[dlg] 自动匹配失败', e); }
}

// ---- 对齐桌面端 BtnHistory_Click 第304-318行：显示最高/最低价 ----
async function showPriceHistory() {
  if (!dlgState.part) return;
  const partId = dlgState.part?.PartId || dlgState.part?.Partid;
  try {
    const res = await authFetch(API(`/parts/${partId}/price-range`));
    const json = await res.json();
    const d = json.data || json;
    const maxP = d?.maxPrice ?? d?.MaxPrice;
    const minP = d?.minPrice ?? d?.MinPrice;
    if (maxP != null && minP != null) {
      const hint = $('dlgPriceHint');
      hint.textContent = `最高价: ${fmt(maxP)}  最低价: ${fmt(minP)}`;
      hint.style.display = '';
      showToast(`最高价: ${maxP}  最低价: ${minP}`);
    } else {
      showToast('无历史售价记录');
    }
  } catch(e) { LOG.error('[dlg] 历史售价查询失败', e); showToast('查询失败'); }
}

// ---- 确认添加/修改明细（对齐桌面端 BtnOk_Click L342-383）----
function addDetailFromDialog() {
  const p = dlgState.part;
  if (!p) return;

  // 只读模式不允许添加（库存为0）
  if (dlgState.readOnly) return;

  const amount = parseFloat($('dlgAmount').value) || 0;
  if (amount <= 0) { showToast('请输入有效的销售数量', 'error'); $('dlgAmount').focus(); return; }

  const stockAmount = (p.Stock ?? p.Amount ?? 0) || 0;
  if (amount > stockAmount && stockAmount > 0) {
    showToast(`库存不足！当前库存: ${stockAmount}，已自动调整为1`, 'warn');
    $('dlgAmount').value = '1';
    $('dlgAmount').focus();
    $('dlgAmount').select();
    return;
  }

  const price = parseFloat($('dlgPrice').value) || 0;
  if (price < 0) { showToast('请输入有效的销售单价', 'error'); $('dlgPrice').focus(); return; }
  const billPrice = parseFloat($('dlgBillPrice').value) || price;
  if (billPrice < 0) { showToast('请输入有效的开票单价', 'error'); $('dlgBillPrice').focus(); return; }

  const partId = p.Partid || p.PartId;
  const detailData = {
    PartId: partId,
    PartNo: p.PartNo || p.Partno,
    PartName: p.Name,
    Amount: amount,
    Price: price,
    BillPrice: billPrice,
    SubTotal: Math.round(price * amount * 100) / 100,
    Cartype: $('dlgCartype').value.trim(),
  };

  // 修改模式：更新已有明细（对齐桌面端 L317-326）
  if (dlgState.editIndex >= 0 && state.details[dlgState.editIndex]) {
    const existing = state.details[dlgState.editIndex];
    existing.Price = detailData.Price;
    existing.BillPrice = detailData.BillPrice;
    existing.Amount = detailData.Amount;
    existing.SubTotal = detailData.SubTotal;
    existing.Cartype = detailData.Cartype;
    closePartDialog();
    renderDetails();
    calcTotals();
    showToast(`已修改: ${p.Name} x${amount}`);
    return;
  }

  // 新增模式
  state.details.push(detailData);
  closePartDialog();
  renderDetails();
  calcTotals();
  showToast(`已添加: ${p.Name} x${amount}`);
}

// ========== 四、销售明细渲染 ==========
function renderDetails() {
  const tbody = $('detailBody');
  if (state.details.length === 0) {
    tbody.innerHTML = '<tr><td colspan="8" class="empty-hint">点击上方配件列表添加明细</td></tr>';
    $('detailCount').textContent = '0';
    return;
  }

  tbody.innerHTML = state.details.map((d, i) => `
    <tr>
      <td>${escHtml(d.PartNo)}</td>
      <td>${escHtml(d.PartName)}</td>
      <td style="text-align:center">${d.Amount}</td>
      <td style="text-align:right">${fmt(d.Price)}</td>
      <td style="text-align:right;font-weight:500">${fmt(d.SubTotal)}</td>
      <td>${escHtml(d.Cartype)}</td>
      <td>${escHtml(d.Memo)}</td>
      <td><button class="btn btn-sm btn-danger" onclick="removeDetail(${i})">删</button></td>
    </tr>`).join('');
  $('detailCount').textContent = state.details.length;
}

function removeDetail(index) {
  state.details.splice(index, 1);
  renderDetails();
  calcTotals();
}

// ========== 五、金额计算 ==========
function calcTotals() {
  let total = 0, billTotal = 0, sumAmt = 0;
  state.details.forEach(d => {
    total += d.Price * d.Amount;
    billTotal += d.BillPrice * d.Amount;
    sumAmt += d.Amount;
  });

  // 折扣率=0表示无折扣（原价），>0时乘折扣率（对齐后端逻辑）
  const rawDisc = parseFloat($('discountRate').value);
  const disc = Number.isNaN(rawDisc) || rawDisc === 0 ? 1 : rawDisc;
  total = Math.round(total * disc * 100) / 100;
  billTotal = Math.round(billTotal * disc * 100) / 100;

  $('totalAmount').value = '¥' + fmt(total);
  $('billTotal').value = '¥' + fmt(billTotal);
  $('sumLabel').textContent =
    `合计: ¥${fmt(total)} | 开票合计: ¥${fmt(billTotal)} | 数量: ${sumAmt}`;

  calcPayment();
}

function calcPayment() {
  const rawDisc = parseFloat($('discountRate').value);
  const disc = Number.isNaN(rawDisc) || rawDisc === 0 ? 1 : rawDisc;
  let billTotal = 0;
  state.details.forEach(d => { billTotal += d.BillPrice * d.Amount; });
  billTotal = Math.round(billTotal * disc * 100) / 100;

  const cash = parseFloat($('cashPay').value) || 0;
  const checks = parseFloat($('checksPay').value) || 0;
  const zfb = parseFloat($('zhifubaoPay').value) || 0;
  const wx = parseFloat($('weixinPay').value) || 0;
  const paid = cash + checks + zfb + wx;
  const arrear = Math.max(0, billTotal - paid);

  $('arrearAmt').value = '¥' + fmt(arrear);
  $('totalAmount').value = '¥' + fmt(billTotal);
  $('billTotal').value = '¥' + fmt(billTotal);
  $('sumLabel').textContent =
    `合计: ¥${fmt(billTotal / disc)} | 开票合计: ¥${fmt(billTotal)} | 数量: ${state.details.reduce((s, d) => s + d.Amount, 0)}`;
}

// ========== 六、确认开单 ==========
async function submitOrder() {
  if (!state.selectedClient) { showToast('请先选择客户', 'error'); return; }
  if (state.details.length === 0) { showToast('请至少添加一条明细', 'error'); return; }

  const workerId = $('workerSelect').value || '';
  const rawDisc = parseFloat($('discountRate').value);
  const disc = Number.isNaN(rawDisc) || rawDisc === 0 ? 1 : rawDisc;
  let total = 0, billTotal = 0;
  state.details.forEach(d => { total += d.Price * d.Amount; billTotal += d.BillPrice * d.Amount; });

  // 开单日期：用户所选日期 + 当前时刻（对齐桌面端 billDate = 所选日期 + 当前时间，支持补录历史日期）
  let datetime = null;
  const dateVal = $('billDateInput')?.value;
  if (dateVal) {
    const now = new Date();
    const pad = n => String(n).padStart(2, '0');
    datetime = `${dateVal}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`;
  }

  const order = {
    ClientId: state.selectedClient.id,
    WorkerId: workerId,
    DiscountRate: disc,
    Datetime: datetime,
    Total: total * disc,
    BillTotal: billTotal * disc,
    Cash: parseFloat($('cashPay').value) || 0,
    Checks: parseFloat($('checksPay').value) || 0,
    Zhifubao: parseFloat($('zhifubaoPay').value) || 0,
    Weixin: parseFloat($('weixinPay').value) || 0,
    Memo: $('memoText').value.trim(),
    Details: state.details.map(d => ({
      PartId: d.PartId,
      PartNo: d.PartNo,
      PartName: d.PartName,
      Amount: d.Amount,
      Price: d.Price,
      BillPrice: d.BillPrice,
      Cartype: d.Cartype,
      CarMark: d.CarMark,
      Memo: d.Memo,
    })),
  };

  try {
    showToast('正在提交...');
    const res = await authFetch(API('/sell/orders'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(order),
    });

    const result = await res.json();
    LOG.resp('/sell/orders', res.status, result);
    if (!res.ok) throw new Error(result.error || result.message || `HTTP ${res.status}` + (result.details ? ' | ' + JSON.stringify(result.details) : ''));

    showToast(`开单成功！单号: ${result.sn}`, 'success');
    clearAll();
  } catch (e) {
    showToast('开单失败: ' + e.message, 'error');
  }
}

function clearAll() {
  state.details = [];
  state.selectedClient = null;
  $('clientInput').value = '';
  $('discountRate').value = '0';
  if ($('billDateInput')) $('billDateInput').value = new Date().toISOString().slice(0, 10);
  $('cashPay').value = '0'; $('checksPay').value = '0';
  $('zhifubaoPay').value = '0'; $('weixinPay').value = '0';
  $('memoText').value = '';
  // 业务员默认选中"邓鹏"
  const defWorker = Array.from($('workerSelect').options).find(o => o.textContent.includes('邓鹏'));
  if (defWorker) $('workerSelect').value = defWorker.value;
  $('qPartNo').value = ''; $('qPartName').value = ''; $('qCartype').value = '';
  $('partsBody').innerHTML = '<tr><td colspan="7" class="empty-hint">输入条件后回车或等待自动搜索...</td></tr>';
  renderDetails();
  calcTotals();
}

// ========== 七、单据查询 ==========
async function queryBills() {
  const start = $('qStart').value;
  const end = $('qEnd').value;
  const client = $('qClient').value.trim();
  const worker = $('qWorker').value;

  const params = new URLSearchParams({
    page: '1', pageSize: '20',
    ...(start && { start }),
    ...(end && { end }),
    ...(client && { client }),
    ...(worker && { worker }),
  });

  try {
    const res = await authFetch(API(`/sell/orders?${params}`));
    if (!res.ok) throw new Error(`HTTP ${res.status}`);

    const json = await res.json();
    const bills = json.data || json.items || json;
    renderBills(bills, json.page, json.pageSize, json.total);
  } catch (e) {
    showToast('查询失败: ' + e.message, 'error');
  }
}

function renderBills(bills, page, pageSize, total) {
  const tbody = $('billBody');
  if (!bills || bills.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="empty-hint">没有找到单据</td></tr>';
    $('pagination').innerHTML = '';
    return;
  }

  const flagText = { 0: '草稿', 1: '已确认', 2: '已作废', 3: '退货' };

  tbody.innerHTML = bills.map((b) => `<tr onclick="showBillDetail('${escAttr(b.Sn)}', this)"
      data-sn="${escAttr(b.Sn)}" data-dt="${escAttr(b.Datetime)}" data-client="${escAttr(b.Client)}"
      data-worker="${escAttr(b.Worker)}" data-total="${b.Total ?? ''}" data-billtotal="${b.BillTotal ?? ''}" data-flag="${b.Flag ?? 0}">
      <td>${escHtml(b.Sn)}</td>
      <td>${formatDate(b.Datetime)}</td>
      <td>${escHtml(b.Client)}</td>
      <td>${escHtml(b.Worker)}</td>
      <td style="text-align:right">${fmt(b.Total)}</td>
      <td style="text-align:right;color:#1565c0;font-weight:600">${fmt(b.BillTotal)}</td>
      <td><span class="flag-badge flag-${b.Flag ?? 0}">${flagText[b.Flag] || ''}</span></td>
    </tr>`).join('');

  const totalPages = Math.ceil((total || bills.length) / (pageSize || 20));
  renderPagination(page || 1, totalPages);
}

function renderPagination(current, total) {
  if (total <= 1) { $('pagination').innerHTML = `<span class="page-info">共 ${current}/${total} 页</span>`; return; }
  let html = '';
  html += `<button onclick="goPage(${current - 1})" ${current <= 1 ? 'disabled' : ''}>上一页</button>`;
  for (let i = Math.max(1, current - 2); i <= Math.min(total, current + 2); i++) {
    html += `<button onclick="goPage(${i})" ${i === current ? 'disabled' : ''}>${i}</button>`;
  }
  html += `<button onclick="goPage(${current + 1})" ${current >= total ? 'disabled' : ''}>下一页</button>`;
  html += `<span class="page-info">第 ${current}/${total} 页</span>`;
  $('pagination').innerHTML = html;
}

async function goPage(page) {
  if (page < 1) return;
  const params = new URLSearchParams({
    page: String(page), pageSize: '20',
    ...($('qStart').value && { start: $('qStart').value }),
    ...($('qEnd').value && { end: $('qEnd').value }),
    ...($('qClient').value.trim() && { client: $('qClient').value.trim() }),
    ...($('qWorker').value && { worker: $('qWorker').value }),
  });
  try {
    const res = await authFetch(API(`/sell/orders?${params}`));
    const json = await res.json();
    const bills = json.data || json.items || json;
    renderBills(bills, json.page, json.pageSize, json.total);
  } catch (e) { showToast('翻页失败', 'error'); }
}

// 单据详情（懒加载：先显示摘要，再异步加载明细）
async function showBillDetail(sn, rowEl) {
  const flagMap = { 0: '草稿', 1: '已确认', 2: '已作废', 3: '退货' };
  let hasSummary = false;

  // 立即用行数据显示摘要（零网络延迟）
  if (rowEl) {
    const rowSummary = {
      Sn: rowEl.dataset.sn, Datetime: rowEl.dataset.dt,
      Client: rowEl.dataset.client, Worker: rowEl.dataset.worker,
      Total: rowEl.dataset.total, BillTotal: rowEl.dataset.billtotal,
      Flag: parseInt(rowEl.dataset.flag) || 0,
    };
    renderBillDetailHead(rowSummary, flagMap);
    $('billDetailBody').innerHTML += '<div id="detailLoading" style="text-align:center;padding:20px;color:#999">加载明细中...</div>';
    $('billDetailModal').classList.add('show');
    hasSummary = true;
  }

  // 异步加载完整详情（含明细行）
  try {
    const res = await authFetch(API(`/sell/orders/${encodeURIComponent(sn)}`));
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    renderBillDetail(data);
  } catch (e) {
    if (!hasSummary) showToast('获取详情失败', 'error');
    else {
      const loadingEl = document.getElementById('detailLoading');
      if (loadingEl) loadingEl.textContent = '加载失败，请重试';
    }
  }
}

/** 渲染单据头部摘要（从列表行数据直接取） */
function renderBillDetailHead(b, flagMap) {
  const html = `
    <div class="bill-detail-head">
      <bdp><label>单号</label><value>${escHtml(b.Sn)}</value></bdp>
      <bdp><label>日期</label><value>${formatDate(b.Datetime)}</value></bdp>
      <bdp><label>客户</label><value>${escHtml(b.Client)}</value></bdp>
      <bdp><label>业务员</label><value>${escHtml(b.Worker)}</value></bdp>
      <bdp><label>状态</label><value><span class="flag-badge flag-${b.Flag ?? 0}">${flagMap[b.Flag] || ''}</span></value></bdp>
      <bdp><label>总额</label><value>${fmt(b.Total)}</value></bdp>
      <bdp><label>开票总额</label><value>${fmt(b.BillTotal)}</value></bdp>
    </div>`;
  $('billDetailBody').innerHTML = html;
}

function closeBillDetail() {
  $('billDetailModal').classList.remove('show');
}

function renderBillDetail(data) {
  const b = data.bill || data;
  const details = data.details || [];
  const flagMap = { 0: '草稿', 1: '已确认', 2: '已作废', 3: '退货' };

  let html = `
    <div class="bill-detail-head">
      <bdp><label>单号</label><value>${escHtml(b.Sn)}</value></bdp>
      <bdp><label>日期</label><value>${formatDate(b.Datetime)}</value></bdp>
      <bdp><label>客户</label><value>${escHtml(b.Client)}</value></bdp>
      <bdp><label>业务员</label><value>${escHtml(b.Worker)}</value></bdp>
      <bdp><label>状态</label><value><span class="flag-badge flag-${b.Flag ?? 0}">${flagMap[b.Flag] || ''}</span></value></bdp>
      <bdp><label>总额</label><value>${fmt(b.Total)}</value></bdp>
      <bdp><label>开票总额</label><value>${fmt(b.BillTotal)}</value></bdp>
      <bdp><label>备注</label><value>${escHtml(b.Memo) || '-'}</value></bdp>
    </div>
    <div class="table-wrap">
      <table class="data-table">
        <thead><tr>
          <th>编号</th><th>名称</th><th>数量</th><th>单价</th>
          <th>开票单价</th><th>小计</th><th>车型</th><th>车牌</th><th>备注</th>
        </tr></thead>
        <tbody>`;

  if (details.length === 0) {
    html += '<tr><td colspan="9" class="empty-hint">无明细</td></tr>';
  } else {
    details.forEach(d => {
      html += `<tr>
        <td>${escHtml(d.Partno || d.PartNo)}</td>
        <td>${escHtml(d.Name || d.PartName)}</td>
        <td style="text-align:center">${d.Amount}</td>
        <td style="text-align:right">${fmt(d.Price)}</td>
        <td style="text-align:right">${fmt(d.BillPrice)}</td>
        <td style="text-align:right;font-weight:500">${fmt(d.Stotal || d.SubTotal)}</td>
        <td>${escHtml(d.Cartype)}</td>
        <td>${escHtml(d.CarMark)}</td>
        <td>${escHtml(d.Memo)}</td>
      </tr>`;
    });
  }
  html += '</tbody></table></div>';

  if ((b.Flag ?? 0) !== 2) {
    html += `<div style="margin-top:12px;text-align:right">
      <button class="btn btn-sm btn-primary" onclick="printBill('${escAttr(b.Sn)}')">打印单据</button>
    </div>`;
  }

  $('billDetailBody').innerHTML = html;
}

async function printBill(sn) {
  try {
    showToast('正在发送打印...', 'info');
    const res = await authFetch(API(`/sell/print/${encodeURIComponent(sn)}`), { method: 'POST' });
    const json = await res.json();
    if (json.success) {
      showToast(json.message || '已发送至打印机', 'success');
    } else {
      showToast('打印失败: ' + (json.error || '未知错误'), 'error');
    }
  } catch (e) {
    LOG.error('[print] 打印失败', e);
    showToast('打印请求失败: ' + e.message, 'error');
  }
}

// ========== HTML 转义工具 ==========
function escHtml(s) { if (!s) return ''; return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
function escAttr(s) { return (s || '').replace(/'/g,"&#39;").replace(/"/g,'&quot;'); }
function escapeAttr(s) { return (s || '').replace(/'/g,"\\'").replace(/\\/g,'\\\\'); }
function formatDate(d) {
  if (!d) return '-';
  if (typeof d === 'string') return d.slice(0, 10);
  return new Date(d).toISOString().slice(0, 10);
}
