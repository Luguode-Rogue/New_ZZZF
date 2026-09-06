(() => {
  'use strict';

  const app = window.game.app;
  const root = document.getElementById('app');
  const canvas = document.getElementById('mapCanvas');
  const ctx = canvas.getContext('2d');
  const modeLabel = document.getElementById('modeLabel');
  const statusText = document.getElementById('statusText');
  const hint = document.getElementById('hint');
  const formationList = document.getElementById('formationList');
  const detailBody = document.getElementById('detailBody');

  let staticState = null;
  let runtimeState = null;
  let terrainCanvas = null;
  let tacticalCanvas = null;
  let navMeshCanvas = null;
  let selectedFormation = -1;
  let rafPending = false;

  function clientLog(message) {
    try { app.call('clientLog', { message }); } catch (_) {}
  }

  function command(name, payload = {}) {
    return app.call(name, payload).catch(error => {
      clientLog('command ' + name + ' failed: ' + (error?.message || error));
      throw error;
    });
  }

  // Request 通道：getMapData 等数据拉取必须走 app.request（app.call 是 Command 通道，会报 Unknown command）
  function request(name, payload = {}) {
    return app.request(name, payload).catch(error => {
      clientLog('request ' + name + ' failed: ' + (error?.message || error));
      throw error;
    });
  }

  // 地图显示矩形：在 canvas 内按世界纵横比（worldWidth/worldHeight）等比 contain 适配并居中，
  // 其余部分留深色边。底图、标记、点击换算全部以该矩形为基准，避免非方形地图被拉伸。
  function mapRect() {
    const rect = canvas.getBoundingClientRect();
    const ww = Number(staticState?.worldWidth || 0);
    const wh = Number(staticState?.worldHeight || 0);
    if (!(ww > 0) || !(wh > 0) || rect.width <= 0 || rect.height <= 0) {
      return { x: 0, y: 0, w: rect.width, h: rect.height };
    }
    const scale = Math.min(rect.width / ww, rect.height / wh);
    const w = ww * scale;
    const h = wh * scale;
    return { x: (rect.width - w) / 2, y: (rect.height - h) / 2, w, h };
  }

  // Report the map canvas on-screen rectangle: the game thread polls physical clicks against
  // it (native interceptor), because Chromium drops mouse input while unfocused.
  // 上报地图显示矩形（letterbox 内的实际地图区域），而非整个 canvas 元素。
  function reportCanvasRect() {
    try {
      const r = canvas.getBoundingClientRect();
      const m = mapRect();
      if (m.w > 0 && m.h > 0) {
        app.call('canvasRect', { x: r.left + m.x, y: r.top + m.y, w: m.w, h: m.h, dpr: window.devicePixelRatio || 1 }).catch(() => {});
      }
    } catch (_) {}
  }

  function decodeImage(base64, width, height) {
    if (!base64 || width <= 0 || height <= 0) {
      clientLog('decodeImage skipped: base64=' + (base64 ? base64.length : 'null') + ' w=' + width + ' h=' + height);
      return null;
    }
    try {
      const binary = atob(base64);
      const expected = width * height * 4;
      if (binary.length !== expected) {
        clientLog('decodeImage size mismatch: expected=' + expected + ' got=' + binary.length + ' (w=' + width + ' h=' + height + ' b64=' + base64.length + ')');
        return null;
      }
      const bytes = new Uint8ClampedArray(expected);
      for (let i = 0; i < expected; i++) bytes[i] = binary.charCodeAt(i);
      const source = document.createElement('canvas');
      source.width = width;
      source.height = height;
      source.getContext('2d').putImageData(new ImageData(bytes, width, height), 0, 0);
      return source;
    } catch (e) {
      clientLog('decodeImage exception: ' + (e?.message || e));
      return null;
    }
  }

  function screenU(mapU) {
    return Number(mapU || 0);
  }

  function screenFacingU(mapFacingU) {
    return -Number(mapFacingU || 0);
  }

  function scheduleRender() {
    if (rafPending) return;
    rafPending = true;
    requestAnimationFrame(() => { rafPending = false; render(); });
  }

  // 底图经虚拟主机直接以 <img> 加载 PNG（C# 写入 Logs\PhotoMapDump 并注册为 content root）。
  // 彻底替代 4MB Base64 JSON + ExecuteScriptAsync 通道：无大字符串、无跨进程序列化、
  // 无 atob 逐字节解码，浏览器走原生 HTTP+解码管线，主线程与 UI 线程零大负载。
  let appliedPhotoUrl = null;
  let appliedNavUrl = null;
  let appliedRiskUrl = null;

  function loadLayer(url, key) {
    const img = new Image();
    img.onload = () => {
      if (key === 'terrain') terrainCanvas = img;
      else if (key === 'navmesh') navMeshCanvas = img;
      else if (key === 'risk') tacticalCanvas = img;
      clientLog('layer loaded: ' + key + ' ' + img.naturalWidth + 'x' + img.naturalHeight);
      scheduleRender();
    };
    img.onerror = () => clientLog('layer load FAILED: ' + url);
    img.src = url;
  }

  function applyStatic(state) {
    staticState = state || null;
    // photoReady=false（拍照未完成）时 terrain URL 尚为 null，仅黑窗期 navMesh 兜底
    if (state?.photoUrl && state.photoUrl !== appliedPhotoUrl) {
      appliedPhotoUrl = state.photoUrl;
      loadLayer(state.photoUrl, 'terrain');
    }
    if (state?.navMeshUrl && state.navMeshUrl !== appliedNavUrl) {
      appliedNavUrl = state.navMeshUrl;
      loadLayer(state.navMeshUrl, 'navmesh');
    }
    if (state?.enableRisk && state.riskUrl && state.riskUrl !== appliedRiskUrl) {
      appliedRiskUrl = state.riskUrl;
      loadLayer(state.riskUrl, 'risk');
    }
    scheduleRender();
  }

  function applyRuntime(state) {
    runtimeState = state || null;
    const formations = state?.formations || [];
    // 扁平数组还原：agents 每 3 个 float = [u, v, flag]（bit0=玩家队，bit1=中立）。
    // 数值数组比对象数组 JSON 体积小 ~70%。
    if (runtimeState && Array.isArray(runtimeState.agentsFlat)) {
      const flat = runtimeState.agentsFlat;
      const agents = new Array(Math.floor(flat.length / 3));
      for (let i = 0, j = 0; j < agents.length; i += 3, j++) {
        const flags = flat[i + 2];
        agents[j] = { u: flat[i], v: flat[i + 1], player: (flags & 1) !== 0, neutral: (flags & 2) !== 0 };
      }
      runtimeState.agents = agents;
    }
    if (state?.selectedFormation) {
      selectedFormation = formations.findIndex(f => f.player && f.name === state.selectedFormation);
    } else if (selectedFormation >= formations.length) {
      selectedFormation = -1;
    }
    updateChrome();
    updateFormationList();
    updateDetails();
    scheduleRender();
  }

  let lastChromeMode = null;

  function updateChrome() {
    const mode = runtimeState?.mode || 'CompactPassive';
    // runtime 每 0.2s 到达且内容几乎必变，但 chrome 只依赖 mode：
    // 只在 mode 实际变化时才写 DOM / 重报 canvasRect。
    // 原实现每 0.2s 做 1 次同步 + 2 次 setTimeout 上报 = 每秒 15 条 postMessage，
    // 框架对每条都写 INFO 日志（UI 线程 IO），watchdog 实测 UI 线程被堵 3.9s（fps=1/0 卡死直接来源）。
    if (mode === lastChromeMode) return;
    lastChromeMode = mode;
    const className = 'map-shell mode-' + mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
    root.className = className;
    root.setAttribute('aria-hidden', 'false');
    modeLabel.textContent = mode === 'FullInteractive' ? '战术操作' : '观察';
    statusText.textContent = staticState?.baked
      ? 'TacticalMap · ' + (modeLabel.textContent || mode)
      : 'TacticalMap · 地形不可用：' + (staticState?.error || 'unknown');
    hint.textContent = runtimeState?.interactive
      ? '左键：移动　中键：镜头　右键：朝向　ESC：退出大图操作'
      : '被动小图　进入操作大图后可下达移动、镜头与朝向命令';
    // 模式切换后 canvas 布局立即变化：马上上报 + 延迟补报，否则原生点击拦截器
    // 在定时器(3s)到来前仍用旧矩形换算 UV，命令会落到错误的世界坐标。
    reportCanvasRect();
    setTimeout(reportCanvasRect, 50);
    setTimeout(reportCanvasRect, 250);
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>'\\"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '\"':'&quot;' })[ch]);
  }

  function relationText(f) {
    if (f.player) return '我军';
    if (f.enemy) return '敌军';
    return '友军';
  }

  function updateFormationList() {
    // 小地图（被动观察）模式下编队列表不可交互，跳过 DOM 全量重建——
    // runtime 每 200ms 到达一次，重建会持续占用 JS 主线程造成前端卡顿
    if (!root.classList.contains('mode-full-interactive') && !root.classList.contains('mode-full-passive')) {
      return;
    }
    formationList.replaceChildren();
    (runtimeState?.formations || []).forEach((f, index) => {
      const item = document.createElement('div');
      item.className = 'formation-item ' + (f.enemy ? 'enemy' : 'friendly') + (index === selectedFormation ? ' selected' : '');
      item.innerHTML = '<span class="index">' + escapeHtml(f.name || index + 1) + '</span>' +
        '<span><span class="name">' + relationText(f) + '</span><br><span class="meta">编队 ' + escapeHtml(f.name || index + 1) + '</span></span>' +
        '<span class="meta">' + Number(f.count || 0) + '</span>';
      item.addEventListener('click', () => {
        selectedFormation = index;
        if (f.player) command('selectFormation', { name: f.name }).catch(() => {});
        updateFormationList();
        updateDetails();
        scheduleRender();
      });
      formationList.appendChild(item);
    });
  }

  function updateDetails() {
    const formations = runtimeState?.formations || [];
    if (selectedFormation < 0 || !formations[selectedFormation]) {
      detailBody.textContent = '选择一个编队查看详细信息';
      return;
    }
    const f = formations[selectedFormation];
    const orderText = f.hasOrder
      ? Number(f.orderU || 0).toFixed(3) + ', ' + Number(f.orderV || 0).toFixed(3)
      : '无当前目标';
    detailBody.innerHTML =
      '<div class="detail-row"><span>关系</span><span>' + relationText(f) + '</span></div>' +
      '<div class="detail-row"><span>编号</span><span>' + escapeHtml(f.name || '-') + '</span></div>' +
      '<div class="detail-row"><span>人数</span><span>' + Number(f.count || 0) + '</span></div>' +
      '<div class="detail-row"><span>位置</span><span>' + Number(f.u || 0).toFixed(3) + ', ' + Number(f.v || 0).toFixed(3) + '</span></div>' +
      '<div class="detail-row"><span>指向</span><span>' + screenFacingU(f.facingU).toFixed(2) + ', ' + Number(f.facingV || 0).toFixed(2) + '</span></div>' +
      '<div class="detail-row"><span>当前命令点</span><span>' + orderText + '</span></div>';
  }

  function drawStaticMap(x, y, w, h) {
    if (!terrainCanvas) {
      ctx.fillStyle = '#0d1519';
      ctx.fillRect(x, y, w, h);
      // 拍照黑窗期：用 NavMesh 可行区域叠加层兜底提供地形参考
      if (navMeshCanvas) {
        ctx.save();
        ctx.translate(x + w, y);
        ctx.scale(-1, 1);
        ctx.globalAlpha = 0.58;
        ctx.drawImage(navMeshCanvas, 0, 0, w, h);
        ctx.restore();
      }
      return;
    }
    ctx.save();
    ctx.translate(x + w, y);
    ctx.scale(-1, 1);
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(terrainCanvas, 0, 0, w, h);
    // 有真实地形照片后不再叠加 NavMesh 层（红边界污染照片画面）

    if (tacticalCanvas && staticState?.enableRisk) {
      ctx.globalAlpha = 0.46;
      ctx.drawImage(tacticalCanvas, 0, 0, w, h);
    }
    ctx.restore();
    ctx.strokeStyle = 'rgba(225,205,140,.70)';
    ctx.strokeRect(x + .5, y + .5, w - 1, h - 1);
  }

  function drawArrow(x, y, dx, dy, scale, color, width) {
    const len = Math.hypot(dx, dy);
    if (len < .001) return;
    const nx = dx / len, ny = dy / len;
    const ex = x + nx * scale, ey = y + ny * scale;
    const px = -ny, py = nx;
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(ex, ey); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(ex, ey);
    ctx.lineTo(ex - nx * 7 + px * 4, ey - ny * 7 + py * 4);
    ctx.lineTo(ex - nx * 7 - px * 4, ey - ny * 7 - py * 4);
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();
  }

  function drawOrderLine(px, py, ox, oy, color) {
    const dx = ox - px, dy = oy - py;
    const len = Math.hypot(dx, dy);
    if (len < 3) return;
    ctx.save();
    ctx.setLineDash([5, 4]);
    ctx.strokeStyle = color;
    ctx.globalAlpha = .50;
    ctx.lineWidth = 1.0;
    ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(ox, oy); ctx.stroke();
    ctx.restore();

    ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(ox, oy, 3.5, 0, Math.PI * 2); ctx.fill();
  }

  function drawMarkers(x, y, w, h) {
    const s = runtimeState;
    if (!s) return;
    (s.formations || []).forEach((f, index) => {
      const px = x + screenU(f.u) * w, py = y + f.v * h;
      const selected = index === selectedFormation;
      const stroke = f.enemy ? '#ff4c4c' : '#4ade80';

      if (f.hasOrder) {
        drawOrderLine(
          px,
          py,
          x + screenU(f.orderU) * w,
          y + f.orderV * h,
          selected ? '#ffe69a' : stroke);
      }

      const size = Math.max(8, Math.min(17, 8 + Math.sqrt(Math.max(1, Number(f.count || 1))) * .45));
      ctx.strokeStyle = selected ? '#ffe69a' : stroke;
      ctx.lineWidth = selected ? 2.4 : 1.5;
      ctx.strokeRect(px - size, py - size * .62, size * 2, size * 1.24);
      ctx.fillStyle = selected ? '#ffe69a' : '#f4f6f7';
      ctx.font = selected ? 'bold 10px Segoe UI, Arial' : '10px Segoe UI, Arial';
      if (f.name) ctx.fillText(f.name, px + size + 3, py + 3);
      drawArrow(px, py, screenFacingU(f.facingU), Number(f.facingV || 0), size + 5, selected ? '#ffe69a' : stroke, 1.2);
    });

    (s.agents || []).forEach(agent => {
      const px = x + screenU(agent.u) * w, py = y + agent.v * h;
      ctx.fillStyle = agent.neutral ? '#b8bec4' : (agent.player ? '#28dbea' : '#ff3030');
      ctx.beginPath(); ctx.arc(px, py, 2.2, 0, Math.PI * 2); ctx.fill();
    });

    if (s.cameraTarget) {
      const px = x + screenU(s.cameraTarget.u) * w, py = y + s.cameraTarget.v * h;
      ctx.save(); ctx.translate(px, py); ctx.rotate(Math.PI / 4);
      ctx.fillStyle = '#ff9d32'; ctx.fillRect(-6, -6, 12, 12); ctx.restore();
    }

    if (s.player) {
      const px = x + screenU(s.player.u) * w, py = y + s.player.v * h;
      ctx.strokeStyle = '#28dbea'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(px, py, 8, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = '#ffd43b'; ctx.beginPath(); ctx.arc(px, py, 4, 0, Math.PI * 2); ctx.fill();
      drawArrow(px, py, screenFacingU(s.player.facingU), Number(s.player.facingV || 0), 18, '#ffd43b', 1.5);
    }
  }

  // 渲染性能诊断：每 5s 上报一次 raf FPS；单帧 >50ms 限频上报（前端卡顿定位）
  let perfFrames = 0;
  let perfWindowStart = performance.now();
  let lastSlowRenderLog = 0;

  function render() {
    if (!runtimeState?.visible) return;
    const t0 = performance.now();
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.floor(rect.width * dpr));
    const height = Math.max(1, Math.floor(rect.height * dpr));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, rect.height);
    ctx.fillStyle = '#0d1519';
    ctx.fillRect(0, 0, rect.width, rect.height);
    // letterbox：底图/标记/点击都以等比适配的地图矩形为基准
    const m = mapRect();
    drawStaticMap(m.x, m.y, m.w, m.h);
    drawMarkers(m.x, m.y, m.w, m.h);

    perfFrames++;
    const cost = performance.now() - t0;
    const now = t0;
    if (cost > 50 && now - lastSlowRenderLog > 2000) {
      lastSlowRenderLog = now;
      clientLog('render slow: ' + cost.toFixed(0) + 'ms (canvas=' + width + 'x' + height + ')');
    }
    if (now - perfWindowStart >= 5000) {
      const fps = Math.round(perfFrames * 1000 / (now - perfWindowStart));
      clientLog('render fps=' + fps + ' (canvas=' + width + 'x' + height + ')');
      perfFrames = 0;
      perfWindowStart = now;
    }
  }

  function getUv(event) {
    const rect = canvas.getBoundingClientRect();
    const m = mapRect();
    if (m.w <= 0 || m.h <= 0) return { u: 0, v: 0 };
    return {
      u: Math.min(1, Math.max(0, (event.clientX - rect.left - m.x) / m.w)),
      v: Math.min(1, Math.max(0, (event.clientY - rect.top - m.y) / m.h))
    };
  }

  canvas.addEventListener('contextmenu', event => {
    event.preventDefault();
    event.stopPropagation();
  });

  canvas.addEventListener('pointerdown', async event => {
    if (!runtimeState?.interactive) return;
    event.preventDefault();
    event.stopPropagation();
    try { canvas.setPointerCapture(event.pointerId); } catch (_) {}
    const uv = getUv(event);
    try {
      if (event.button === 0) await command('move', uv);
      else if (event.button === 1) await command('camera', uv);
      else if (event.button === 2) await command('face', uv);
    } catch (_) {}
  });

  window.addEventListener('resize', () => { scheduleRender(); reportCanvasRect(); });
  setInterval(reportCanvasRect, 3000);
  reportCanvasRect();
  app.state.subscribe('tacticalMap.static', applyStatic);
  app.state.subscribe('tacticalMap.runtime', applyRuntime);
  app.errors.on(error => clientLog('runtime error=' + (error?.message || error)));

  const initialStatic = app.state.get('tacticalMap.static');
  const initialRuntime = app.state.get('tacticalMap.runtime');
  if (initialStatic) applyStatic(initialStatic);
  if (initialRuntime) applyRuntime(initialRuntime);
})();
