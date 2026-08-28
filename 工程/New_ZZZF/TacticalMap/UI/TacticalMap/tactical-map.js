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

  function decodeImage(base64, width, height) {
    if (!base64 || width <= 0 || height <= 0) return null;
    try {
      const binary = atob(base64);
      const expected = width * height * 4;
      if (binary.length !== expected) return null;
      const bytes = new Uint8ClampedArray(expected);
      for (let i = 0; i < expected; i++) bytes[i] = binary.charCodeAt(i);
      const source = document.createElement('canvas');
      source.width = width;
      source.height = height;
      source.getContext('2d').putImageData(new ImageData(bytes, width, height), 0, 0);
      return source;
    } catch (_) { return null; }
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

  function applyStatic(state) {
    staticState = state || null;
    const width = Number(state?.width || 0);
    const height = Number(state?.height || 0);
    terrainCanvas = state ? decodeImage(state.terrainBaseRgba, width, height) : null;
    tacticalCanvas = state ? decodeImage(state.tacticalRgba || state.riskRgba, width, height) : null;
    navMeshCanvas = state ? decodeImage(state.navMeshRgba, width, height) : null;
    scheduleRender();
  }

  function applyRuntime(state) {
    runtimeState = state || null;
    const formations = state?.formations || [];
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

  function updateChrome() {
    const mode = runtimeState?.mode || 'CompactPassive';
    const className = 'map-shell mode-' + mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
    if (root.className !== className) root.className = className;
    root.setAttribute('aria-hidden', 'false');
    modeLabel.textContent = mode === 'FullInteractive' ? '战术操作' : '观察';
    statusText.textContent = staticState?.baked
      ? 'TacticalMap · ' + (modeLabel.textContent || mode)
      : 'TacticalMap · 地形不可用：' + (staticState?.error || 'unknown');
    hint.textContent = runtimeState?.interactive
      ? '左键：移动　中键：镜头　右键：朝向　ESC：退出大图操作'
      : '被动小图　进入操作大图后可下达移动、镜头与朝向命令';
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>'\"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '\"':'&quot;' })[ch]);
  }

  function relationText(f) {
    if (f.player) return '我军';
    if (f.enemy) return '敌军';
    return '友军';
  }

  function updateFormationList() {
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
    const pathText = Array.isArray(f.pathPoints) && f.pathPoints.length > 1
      ? '已找到实际 AI 路径 (' + f.pathPoints.length + ' 点)'
      : '无可用 AI 路径';
    detailBody.innerHTML =
      '<div class="detail-row"><span>关系</span><span>' + relationText(f) + '</span></div>' +
      '<div class="detail-row"><span>编号</span><span>' + escapeHtml(f.name || '-') + '</span></div>' +
      '<div class="detail-row"><span>人数</span><span>' + Number(f.count || 0) + '</span></div>' +
      '<div class="detail-row"><span>位置</span><span>' + Number(f.u || 0).toFixed(3) + ', ' + Number(f.v || 0).toFixed(3) + '</span></div>' +
      '<div class="detail-row"><span>指向</span><span>' + screenFacingU(f.facingU).toFixed(2) + ', ' + Number(f.facingV || 0).toFixed(2) + '</span></div>' +
      '<div class="detail-row"><span>当前命令点</span><span>' + orderText + '</span></div>' +
      '<div class="detail-row"><span>路径</span><span>' + pathText + '</span></div>';
  }

  function drawStaticMap(x, y, w, h) {
    if (!terrainCanvas) {
      ctx.fillStyle = '#0d1519';
      ctx.fillRect(x, y, w, h);
      return;
    }
    ctx.save();
    ctx.translate(x + w, y);
    ctx.scale(-1, 1);
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(terrainCanvas, 0, 0, w, h);

    // The navigation mask is authoritative for walkability. Transparent pixels are on NavMesh;
    // red pixels are outside the engine AI navigation surface.
    if (navMeshCanvas) {
      ctx.globalAlpha = 0.58;
      ctx.drawImage(navMeshCanvas, 0, 0, w, h);
    }

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

    const nx = dx / len, ny = dy / len;
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(ox, oy, 3.5, 0, Math.PI * 2); ctx.fill();
  }

  function drawPath(points, x, y, w, h, color, selected) {
    if (!Array.isArray(points) || points.length < 2) return;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.globalAlpha = selected ? .95 : .72;
    ctx.lineWidth = selected ? 2.2 : 1.5;
    ctx.setLineDash(selected ? [] : [7, 4]);
    ctx.beginPath();
    points.forEach((p, i) => {
      const px = x + screenU(p.u) * w;
      const py = y + Number(p.v || 0) * h;
      if (i === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    });
    ctx.stroke();
    ctx.restore();

    const end = points[points.length - 1];
    const ex = x + screenU(end.u) * w;
    const ey = y + Number(end.v || 0) * h;
    const prev = points[Math.max(0, points.length - 2)];
    const px = x + screenU(prev.u) * w;
    const py = y + Number(prev.v || 0) * h;
    const dx = ex - px, dy = ey - py;
    const len = Math.hypot(dx, dy);
    if (len >= .001) {
      drawArrow(ex - dx / len * 7, ey - dy / len * 7, dx / len, dy / len, 7, color, selected ? 1.5 : 1.0);
    }
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(ex, ey, 3.5, 0, Math.PI * 2); ctx.fill();
  }

  function drawMarkers(x, y, w, h) {
    const s = runtimeState;
    if (!s) return;
    (s.formations || []).forEach((f, index) => {
      const px = x + screenU(f.u) * w, py = y + f.v * h;
      const selected = index === selectedFormation;
      const stroke = f.enemy ? '#ff4c4c' : '#4ade80';
      const path = Array.isArray(f.pathPoints) ? f.pathPoints : [];

      if (path.length > 1) {
        drawPath(path, x, y, w, h, selected ? '#ffe69a' : stroke, selected);
      } else if (f.hasOrder) {
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

  function render() {
    if (!runtimeState?.visible) return;
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
    drawStaticMap(0, 0, rect.width, rect.height);
    drawMarkers(0, 0, rect.width, rect.height);
  }

  function getUv(event) {
    const rect = canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return { u: 0, v: 0 };
    return {
      u: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)),
      v: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height))
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

  window.addEventListener('resize', scheduleRender);
  app.state.subscribe('tacticalMap.static', applyStatic);
  app.state.subscribe('tacticalMap.runtime', applyRuntime);
  app.errors.on(error => clientLog('runtime error=' + (error?.message || error)));

  const initialStatic = app.state.get('tacticalMap.static');
  const initialRuntime = app.state.get('tacticalMap.runtime');
  if (initialStatic) applyStatic(initialStatic);
  if (initialRuntime) applyRuntime(initialRuntime);
})();