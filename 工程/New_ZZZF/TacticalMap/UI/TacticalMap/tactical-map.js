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
  let rafPending = false;
  let nGesture = null;
  let lastRuntimeLogSignature = null;
  const LONG_PRESS_MS = 450;

  canvas.tabIndex = 0;

  function clientLog(message) {
    try { app.call('clientLog', { message }); } catch (_) {}
  }

  function command(name, payload = {}) {
    return app.call(name, payload).catch(error => {
      clientLog('command ' + name + ' failed: ' + (error?.message || error));
      throw error;
    });
  }

  clientLog('JS boot. document=' + document.readyState + ', url=' + location.href);

  function decodeImage(base64, width, height) {
    if (!base64 || width <= 0 || height <= 0) return null;
    try {
      const binary = atob(base64);
      const expected = width * height * 4;
      if (binary.length !== expected) {
        clientLog('decodeImage length mismatch got=' + binary.length + ' expected=' + expected);
        return null;
      }
      const bytes = new Uint8ClampedArray(expected);
      for (let i = 0; i < expected; i++) bytes[i] = binary.charCodeAt(i);
      const source = document.createElement('canvas');
      source.width = width;
      source.height = height;
      source.getContext('2d').putImageData(new ImageData(bytes, width, height), 0, 0);
      clientLog('decodeImage success ' + width + 'x' + height);
      return source;
    } catch (error) {
      clientLog('decodeImage exception ' + (error?.message || error));
      return null;
    }
  }

  function scheduleRender() {
    if (rafPending) return;
    rafPending = true;
    requestAnimationFrame(() => { rafPending = false; render(); });
  }

  function applyStatic(state) {
    staticState = state || null;
    clientLog('state.static received baked=' + !!state?.baked + ' size=' + Number(state?.width || 0) + 'x' + Number(state?.height || 0));
    terrainCanvas = state ? decodeImage(state.terrainBaseRgba, Number(state.width), Number(state.height)) : null;
    riskCanvas = state ? decodeImage(state.riskRgba, Number(state.width), Number(state.height)) : null;
    scheduleRender();
  }

  function applyRuntime(state) {
    runtimeState = state || null;
    const signature = [state?.mode || '<null>', !!state?.interactive, !!state?.visible].join('|');
    if (signature !== lastRuntimeLogSignature) {
      lastRuntimeLogSignature = signature;
      clientLog('state.runtime changed mode=' + (state?.mode || '<null>') + ' interactive=' + !!state?.interactive + ' visible=' + !!state?.visible);
    }

    if (state?.selectedFormation) {
      selectedFormation = (state.formations || []).findIndex(f => f.player && f.name === state.selectedFormation);
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
    const className = 'map-shell mode-' + mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
    if (root.className !== className) root.className = className;
    const visible = !!runtimeState?.visible;
    root.setAttribute('aria-hidden', visible ? 'false' : 'true');
    modeLabel.textContent = ({ CompactPassive:'观察', CompactInteractive:'操作', FullPassive:'观察', FullInteractive:'战术操作', Hidden:'隐藏' })[mode] || mode;
    if (!visible) statusText.textContent = 'TacticalMap · 隐藏';
    else if (!staticState?.baked) statusText.textContent = 'TacticalMap · 地形不可用：' + (staticState?.error || 'unknown');
    else if (!terrainCanvas) statusText.textContent = 'TacticalMap · 地形数据已到达但解码失败';
    else statusText.textContent = 'TacticalMap · ' + (modeLabel.textContent || mode);
    hint.textContent = runtimeState?.interactive
      ? '左键：移动　中键：镜头　右键：朝向　ESC：退出操作　N：切换操作　N 长按：全屏 / 隐藏'
      : 'N 短按：操作　N 长按：全屏 / 隐藏';
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' })[ch]);
  }

  function updateFormationList() {
    formationList.replaceChildren();
    (runtimeState?.formations || []).forEach((f, index) => {
      const item = document.createElement('div');
      item.className = 'formation-item ' + (f.enemy ? 'enemy' : 'friendly') + (index === selectedFormation ? ' selected' : '');
      item.innerHTML = '<span class="index">' + escapeHtml(f.name || index + 1) + '</span>' +
        '<span><span class="name">' + (f.enemy ? '敌军' : (f.player ? '我军' : '友军')) + '</span><br><span class="meta">编队 ' + escapeHtml(f.name || index + 1) + '</span></span>' +
        '<span class="meta">' + Number(f.count || 0) + '</span>';
      item.addEventListener('click', () => {
        selectedFormation = index;
        clientLog('formation item click index=' + index + ' player=' + !!f.player);
        if (f.player) command('selectFormation', { name: f.name }).catch(() => {});
        updateFormationList();
        updateDetails();
        scheduleRender();
        canvas.focus();
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
    detailBody.innerHTML =
      '<div class="detail-row"><span>关系</span><span>' + (f.player ? '玩家编队' : (f.enemy ? '敌军' : '友军')) + '</span></div>' +
      '<div class="detail-row"><span>编号</span><span>' + escapeHtml(f.name || '-') + '</span></div>' +
      '<div class="detail-row"><span>人数</span><span>' + Number(f.count || 0) + '</span></div>' +
      '<div class="detail-row"><span>地图 U</span><span>' + Number(f.u || 0).toFixed(3) + '</span></div>' +
      '<div class="detail-row"><span>地图 V</span><span>' + Number(f.v || 0).toFixed(3) + '</span></div>';
  }

  function drawStaticMap(x, y, w, h) {
    if (!terrainCanvas) {
      ctx.fillStyle = '#0d1519'; ctx.fillRect(x, y, w, h);
      return;
    }
    ctx.save();
    ctx.translate(x + w, y);
    ctx.scale(-1, 1);
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(terrainCanvas, 0, 0, w, h);
    if (riskCanvas && staticState?.enableRisk) { ctx.globalAlpha = .42; ctx.drawImage(riskCanvas, 0, 0, w, h); }
    ctx.restore();
    ctx.strokeStyle = 'rgba(225,205,140,.70)';
    ctx.strokeRect(x + .5, y + .5, w - 1, h - 1);
  }

  function drawArrow(x, y, dx, dy, scale, color, width) {
    const len = Math.hypot(dx, dy);
    if (len < .001) return;
    const nx = dx / len, ny = dy / len, ex = x + nx * scale, ey = y + ny * scale, px = -ny, py = nx;
    ctx.strokeStyle = color; ctx.lineWidth = width;
    ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(ex, ey); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(ex, ey); ctx.lineTo(ex - nx * 7 + px * 4, ey - ny * 7 + py * 4); ctx.lineTo(ex - nx * 7 - px * 4, ey - ny * 7 - py * 4); ctx.closePath();
    ctx.fillStyle = color; ctx.fill();
  }

  function drawMarkers(x, y, w, h) {
    const s = runtimeState;
    if (!s) return;
    (s.formations || []).forEach((f, index) => {
      const px = x + f.u * w, py = y + f.v * h, selected = index === selectedFormation;
      const stroke = f.enemy ? '#ff4c4c' : '#4ade80';
      ctx.strokeStyle = selected ? '#ffe69a' : stroke; ctx.lineWidth = selected ? 2.4 : 1.5; ctx.strokeRect(px - 8, py - 6, 16, 12);
      if (f.name) { ctx.fillStyle = selected ? '#ffe69a' : '#f4f6f7'; ctx.font = '10px Segoe UI, Arial'; ctx.fillText(f.name, px + 10, py + 3); }
      drawArrow(px, py, Number(f.facingU || 0), Number(f.facingV || 0), 11, stroke, 1.2);
    });
    (s.agents || []).forEach(agent => {
      const px = x + agent.u * w, py = y + agent.v * h;
      ctx.fillStyle = agent.neutral ? '#b8bec4' : (agent.player ? '#28dbea' : '#ff3030');
      ctx.beginPath(); ctx.arc(px, py, 2.3, 0, Math.PI * 2); ctx.fill();
    });
    if (s.cameraTarget) {
      const px = x + s.cameraTarget.u * w, py = y + s.cameraTarget.v * h;
      ctx.save(); ctx.translate(px, py); ctx.rotate(Math.PI / 4); ctx.fillStyle = '#ff9d32'; ctx.fillRect(-6, -6, 12, 12); ctx.restore();
    }
    if (s.player) {
      const px = x + s.player.u * w, py = y + s.player.v * h;
      ctx.strokeStyle = '#28dbea'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(px, py, 8, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = '#ffd43b'; ctx.beginPath(); ctx.arc(px, py, 4, 0, Math.PI * 2); ctx.fill();
      drawArrow(px, py, Number(s.player.facingU || 0), Number(s.player.facingV || 0), 18, '#ffd43b', 1.5);
    }
  }

  function render() {
    if (!runtimeState?.visible) return;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.floor(rect.width * dpr));
    const height = Math.max(1, Math.floor(rect.height * dpr));
    if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
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
    clientLog('contextmenu button=' + event.button + ' interactive=' + !!runtimeState?.interactive);
  });

  canvas.addEventListener('pointerdown', async event => {
    clientLog('pointerdown button=' + event.button + ' pointerType=' + event.pointerType + ' interactive=' + !!runtimeState?.interactive);
    if (!runtimeState?.interactive) return;
    event.preventDefault();
    event.stopPropagation();
    canvas.focus({ preventScroll: true });
    try { canvas.setPointerCapture(event.pointerId); } catch (_) {}
    const uv = getUv(event);
    try {
      if (event.button === 0) await command('move', uv);
      else if (event.button === 1) await command('camera', uv);
      else if (event.button === 2) await command('face', uv);
    } catch (_) {}
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && runtimeState?.interactive) {
      clientLog('ESC keydown in Interactive');
      event.preventDefault();
      event.stopPropagation();
      command('escape').catch(() => {});
      return;
    }

    if (event.key.toLowerCase() !== 'n' || !runtimeState?.interactive || nGesture) return;
    clientLog('N keydown in Interactive');
    event.preventDefault();
    event.stopPropagation();
    nGesture = { start: performance.now(), longTriggered: false };
    setTimeout(() => {
      if (!nGesture || nGesture.longTriggered) return;
      if (performance.now() - nGesture.start >= LONG_PRESS_MS) {
        nGesture.longTriggered = true;
        clientLog('N long -> longPressNext');
        command('longPressNext').catch(() => {});
      }
    }, LONG_PRESS_MS + 5);
  });

  document.addEventListener('keyup', event => {
    if (event.key.toLowerCase() !== 'n' || !nGesture) return;
    event.preventDefault();
    event.stopPropagation();
    const gesture = nGesture;
    nGesture = null;
    if (!gesture.longTriggered) {
      clientLog('N short -> toggleInteractive');
      command('toggleInteractive').catch(() => {});
    }
  });

  window.addEventListener('resize', scheduleRender);
  app.state.subscribe('tacticalMap.static', applyStatic);
  app.state.subscribe('tacticalMap.runtime', applyRuntime);
  app.errors.on(error => clientLog('runtime error=' + (error?.message || error)));

  const initialStatic = app.state.get('tacticalMap.static');
  const initialRuntime = app.state.get('tacticalMap.runtime');
  if (initialStatic) applyStatic(initialStatic);
  if (initialRuntime) applyRuntime(initialRuntime);
  clientLog('JS event handlers installed.');
})();
