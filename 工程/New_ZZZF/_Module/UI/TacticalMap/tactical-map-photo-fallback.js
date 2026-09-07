(() => {
  'use strict';

  // TacticalMap 的 PNG 服务路径是运行时生成的。若 WebView2 虚拟主机映射暂时不可用，
  // 不让地图永久停留在黑底：从现有 getMapData Request 回退到 RGBA -> PNG data URL。
  // 该补丁只在 URL 图片加载失败时触发，正常路径仍走浏览器原生 PNG 解码。
  const NativeImage = window.Image;
  const srcDescriptor = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src');
  const app = window.game?.app;
  let mapDataPromise = null;

  if (!NativeImage || !srcDescriptor || !srcDescriptor.set || !app?.request) return;

  function getMapData() {
    if (!mapDataPromise) {
      mapDataPromise = app.request('getMapData', {}).catch(error => {
        mapDataPromise = null;
        throw error;
      });
    }
    return mapDataPromise;
  }

  function rgbaToPngDataUrl(base64, width, height) {
    if (!base64 || width <= 0 || height <= 0) return null;
    try {
      const binary = atob(base64);
      const expected = width * height * 4;
      if (binary.length !== expected) return null;

      const bytes = new Uint8ClampedArray(expected);
      for (let i = 0; i < expected; i++) bytes[i] = binary.charCodeAt(i);

      const canvas = document.createElement('canvas');
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext('2d');
      if (!ctx) return null;
      ctx.putImageData(new ImageData(bytes, width, height), 0, 0);
      return canvas.toDataURL('image/png');
    } catch (_) {
      return null;
    }
  }

  function fallbackForUrl(img, url) {
    const lower = String(url || '').toLowerCase();
    let kind = null;
    if (lower.indexOf('/terrain.png') >= 0) kind = 'terrain';
    else if (lower.indexOf('/navmesh.png') >= 0) kind = 'navmesh';
    else if (lower.indexOf('/risk.png') >= 0) kind = 'risk';
    if (!kind) return;

    getMapData().then(data => {
      let base64 = null;
      let width = 0;
      let height = 0;
      if (kind === 'terrain') {
        base64 = data?.terrainBaseRgba;
        width = Number(data?.terrainWidth || 0);
        height = Number(data?.terrainHeight || 0);
      } else if (kind === 'navmesh') {
        base64 = data?.navMeshRgba;
        width = Number(data?.width || 0);
        height = Number(data?.height || 0);
      } else {
        base64 = data?.riskRgba || data?.tacticalRgba;
        width = Number(data?.width || 0);
        height = Number(data?.height || 0);
      }

      const dataUrl = rgbaToPngDataUrl(base64, width, height);
      if (!dataUrl) return;
      srcDescriptor.set.call(img, dataUrl);
    }).catch(() => {});
  }

  window.Image = function(width, height) {
    const img = new NativeImage(width, height);
    let fallbackTriggered = false;

    img.addEventListener('error', () => {
      if (fallbackTriggered) return;
      fallbackTriggered = true;
      try { fallbackForUrl(img, srcDescriptor.get.call(img)); } catch (_) {}
    });

    return img;
  };

  // tactical-map.js is loaded immediately after this file; restore the global constructor
  // after that synchronous initialization so this compatibility layer remains local to startup.
  window.setTimeout(() => { window.Image = NativeImage; }, 0);
})();
