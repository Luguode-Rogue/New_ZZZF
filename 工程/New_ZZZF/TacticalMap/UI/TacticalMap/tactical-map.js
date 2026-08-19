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
  let riskCanvas = null;
  let selectedFormation = -1;
  let interactiveGesture = null;
  let rafPending = false;

  const modeText = {
    CompactPassive: '观察',
    CompactInteractive: '操作',
    FullPassive: '观察',
    FullInteractive: '战术操作',
    Hidden: '隐藏'
  };

  function decodeImage(base64, width, height) {
    if (!base64 || width <= 0 || height <= 0) return null;
    try {
      const binary = atob(base64);
      const expected = width * height * 4;
      if (binary.length !== expected) {
        console.warn(`[TacticalMap] RGBA length mismatch: got=${binary.length}, expected=${expected}`);
        return null;
      }
      const bytes = new Uint8ClampedArray(expected);
      for (let i = 0; i < expected; i++) bytes[i] = binary.charCodeAt(i);
      const source = document.createElement('canvas');
      source.width = width;
      source.height = height;
      const sourceCtx = source.getContext('2d');
      sourceCtx.putImageData(new ImageData(bytes, width, height), 0, 0);
      return source;
    } catch (error) {
      console.error('[TacticalMap] terrain image decode failed', error);
      return null;
    }
  }

  function scheduleRender() {
    if (rafPending) return;
    rafPending = true;
    requestAnimationFrame(() => {
      rafPending = false;
      render();
    });
  }

  function applyStatic(state) {
    staticState = state || null;
    terrainCanvas = state ? decodeImage(state.terrainBaseRgba, Number(state.width), Number(state.height)) : null;
    riskCanvas = state ? decodeImage(state.riskRgba, Number(state.width), Number(state.height)) : null;
    scheduleRender();
  }

  function applyRuntime(state) {
    runtimeState = state || null;
    if (state?.selectedFormation) {
      const index = (state.formations || []).findIndex(f => f.player && f.name === state.selectedFormation);
      selectedFormation = index;
    } else if (selectedFormation >= (state?.formations || []).length) {
      selectedFormation = -1;
    }
    updateChrome();
    updateFormationList();
    updateDetails();
    scheduleRender();
  }

  function updateChrome() {
    const mode = runtimeState?.mode || 'CompactPassive';
    root.className = 'map-shell mode-' + mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
    const visible = !!runtimeState?.visible;
    root.setAttribute('aria-hidden', visible ? 'false' : 'true');
    modeLabel.textContent = modeText[mode] || mode;

    if (!visible) {
      statusText.textContent = 'TacticalMap · 隐藏';
    } else if (!staticState?.baked) {
      const error = staticState?.error ? `：${staticState.error}` : '';
      statusText.textContent = `TacticalMap · 地形不可用${error}`;
    } else if (!terrainCanvas) {
      statusText.textContent = `TacticalMap · 地形数据已到达但解码失败`;
    } else {
      statusText.textContent = `TacticalMap · ${modeText[mode] || mode}`;
    }

    hint.textContent = mode.includes('Interactive')
      ? '左键：移动　中键：镜头　右键：朝向　ESC：退出操作　N 长按：全屏 / 隐藏'
      : 'N 短按：操作　N 长按：全屏 / 隐藏';
  }

  function updateFormationList() {
    formationList.replaceChildren();
    const formations = runtimeState?.formations || [];
    formations.forEach((f, index) => {
      const item = document.createElement('div');
      const selected = index === selectedFormation;
      item.className = `formation-item ${f.enemy ? 'enemy' : 'friendly'} ${selected ? 'selected' : ''}`;
      item.innerHTML = `<span class="index">${escapeHtml(f.name || index + 1)}</span>` +
        `<span><span class="name">${f.enemy ? '敌军' : (f.player ? '我军' : '友军')}</span><br><span class="meta">编队 ${escapeHtml(f.name || index + 1)}</span></span>` +
        `<span class="meta">${Number(f.count || 0)}</span>`;
      item.addEventListener('click', () => {
        selectedFormation = index;
        updateFormationList();
        updateDetails();
        scheduleRender();
        if (f.player) {
          app.call('selectFormation', { name: f.name }).catch(() => {});
        }
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
    const relation = f.player ? '玩家编队' : (f.enemy ? '敌军' : '友军');
    detailBody.innerHTML =
      `<div class="detail-row"><span>关系</span><span>${relation}</span></div>` +
      `<div class="detail-row"><span>编号</span><span>${escapeHtml(f.name || '-')}</span></div>` +
      `<div class="detail-row"><span>人数</span><span>${Number(f.count || 0)}</span></div>` +
      `<div class="detail-row"><span>地图 U</span><span>${Number(f.u || 0).toFixed(3)}</span></div>` +
      `<div class="detail-row"><span>地图 V</span><span>${Number(f.v || 0).toFixed(3)}</span></div>`;
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
  }

  function drawStaticMap(x, y, w, h) {
    if (!terrainCanvas) {
      ctx.fillStyle = '#0d1519';
      ctx.fillRect(x, y, w, h);
      ctx.strokeStyle = 'rgba(225,205,140,.35)';
      ctx.lineWidth = 1;
      ctx.strokeRect(x + .5, y + .5, w - 1, h - 1);
      return;
    }

    ctx.save();
    ctx.translate(x + w, y);
    ctx.scale(-1, 1);
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(terrainCanvas, 0, 0, w, h);
    if (riskCanvas && staticState?.enableRisk) {
      ctx.globalAlpha = 0.42;
      ctx.drawImage(riskCanvas, 0, 0, w, h);
      ctx.globalAlpha = 1;
    }
    ctx.restore();

    ctx.strokeStyle = 'rgba(225,205,140,.70)';
    ctx.lineWidth = 1;
    ctx.strokeRect(x + .5, y + .5, w - 1, h - 1);
  }

  function drawGrid(x, y, w, h) {
    if (terrainCanvas) return;
    ctx.strokeStyle = 'rgba(150,170,180,.08)';
    ctx.lineWidth = 1;
    for (let i = 1; i < 8; i++) {
      const px = x + (w * i) / 8;
      const py = y + (h * i) / 8;
      ctx.beginPath();
      ctx.moveTo(px, y);
      ctx.lineTo(px, y + h);
      ctx.moveTo(x, py);
      ctx.lineTo(x + w, py);
      ctx.stroke();
    }
    ctx.fillStyle = 'rgba(225,235,240,.72)';
    ctx.font = '11px Segoe UI, Arial';
    const error = staticState?.error ? `地形：${staticState.error}` : '等待地形数据';
    ctx.fillText(error, x + 12, y + 22);
  }

  function drawArrow(x, y, dx, dy, scale, color, width) {
    const len = Math.hypot(dx, dy);
    if (len < .001) return;
    const nx = dx / len;
    const ny = dy / len;
    const ex = x + nx * scale;
    const ey = y + ny * scale;
    ctx.strokeStyle = color;
    ctx.lineWidth = width;
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(ex, ey);
    ctx.stroke();
    const px = -ny, py = nx;
    ctx.beginPath();
    ctx.moveTo(ex, ey);
    ctx.lineTo(ex - nx * 7 + px * 4, ey - ny * 7 + py * 4);
    ctx.lineTo(ex - nx * 7 - px * 4, ey - ny * 7 - py * 4);
    ctx.closePath();
    ctx.fillStyle = color;
    ctx.fill();
  }

  function drawMarkers(x, y, w, h) {
    const s = runtimeState;
    if (!s) return;

    (s.formations || []).forEach((f, index) => {
      const px = x + f.u * w;
      const py = y + f.v * h;
      const selected = index === selectedFormation;
      const stroke = f.enemy ? '#ff4c4c' : '#4ade80';
      ctx.strokeStyle = selected ? '#ffe69a' : stroke;
      ctx.lineWidth = selected ? 2.4 : 1.5;
      ctx.strokeRect(px - 8, py - 6, 16, 12);
      if (f.name) {
        ctx.fillStyle = selected ? '#ffe69a' : '#f4f6f7';
        ctx.font = '10px Segoe UI, Arial';
        ctx.fillText(f.name, px + 10, py + 3);
      }
      drawArrow(px, py, Number(f.facingU || 0), Number(f.facingV || 0), 11, stroke, 1.2);
    });

    (s.agents || []).forEach(agent => {
      const px = x + agent.u * w;
      const py = y + agent.v * h;
      ctx.fillStyle = agent.neutral ? '#b8bec4' : (agent.player ? '#28dbea' : '#ff3030');
      ctx.beginPath();
      ctx.arc(px, py, 2.3, 0, Math.PI * 2);
      ctx.fill();
    });

    if (s.cameraTarget) {
      const px = x + s.cameraTarget.u * w;
      const py = y + s.cameraTarget.v * h;
      ctx.save();
      ctx.translate(px, py);
      ctx.rotate(Math.PI / 4);
      ctx.fillStyle = '#ff9d32';
      ctx.fillRect(-6, -6, 12, 12);
      ctx.restore();
    }

    if (s.player) {
      const px = x + s.player.u * w;
      const py = y + s.player.v * h;
      ctx.strokeStyle = '#28dbea';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(px, py, 8, 0, Math.PI * 2);
      ctx.stroke();
      ctx.fillStyle = '#ffd43b';
      ctx.beginPath();
      ctx.arc(px, py, 4, 0, Math.PI * 2);
      ctx.fill();
      drawArrow(px, py, Number(s.player.facingU || 0), Number(s.player.facingV || 0), 18, '#ffd43b', 1.5);
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
    const w = rect.width, h = rect.height;
    ctx.clearRect(0, 0, w, h);
    drawStaticMap(0, 0, w, h);
    drawGrid(0, 0, w, h);
    drawMarkers(0, 0, w, h);

    if (runtimeState.mode.includes('Interactive')) {
      ctx.fillStyle = 'rgba(255,225,135,.05)';
      ctx.fillRect(0, 0, w, h);
    }
  }

  function getUvFromEvent(event) {
    const rect = canvas.getBoundingClientRect();
    return {
      u: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)),
      v: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height))
    };
  }

  canvas.addEventListener('contextmenu', e => e.preventDefault());
  canvas.addEventListener('mousedown', async event => {
    if (!runtimeState?.interactive) return;
    event.preventDefault();
    const uv = getUvFromEvent(event);
    try {
      if (event.button === 0) await app.call('move', uv);
      else if (event.button === 1) await app.call('camera', uv);
      else if (event.button === 2) await app.call('face', uv);
    } catch (e) {
      statusText.textContent = `TacticalMap 命令失败：${e.message || e}`;
    }
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && runtimeState?.interactive) {
      event.preventDefault();
      app.call('escape', {}).catch(() => {});
      return;
    }

    if (event.key.toLowerCase() === 'n' && runtimeState?.interactive && !interactiveGesture) {
      interactiveGesture = { start: performance.now(), longTriggered: false };
      const threshold = 450;
      setTimeout(() => {
        if (!interactiveGesture || interactiveGesture.longTriggered) return;
        if (performance.now() - interactiveGesture.start >= threshold) {
          interactiveGesture.longTriggered = true;
          app.call('longPressNext', {}).catch(() => {});
        }
      }, threshold + 5);
    }
  });

  document.addEventListener('keyup', event => {
    if (event.key.toLowerCase() !== 'n' || !interactiveGesture) return;
    const gesture = interactiveGesture;
    interactiveGesture = null;
    if (!gesture.longTriggered)
      app.call('toggleInteractive', {}).catch(() => {});
  });

  window.addEventListener('resize', scheduleRender);

  app.state.subscribe('tacticalMap.static', applyStatic);
  app.state.subscribe('tacticalMap.runtime', applyRuntime);
  app.errors.on(error => {
    statusText.textContent = `TacticalMap Runtime Error: ${error.message || error}`;
  });

  const initialStatic = app.state.get('tacticalMap.static');
  const initialRuntime = app.state.get('tacticalMap.runtime');
  if (initialStatic) applyStatic(initialStatic);
  if (initialRuntime) applyRuntime(initialRuntime);
})();
