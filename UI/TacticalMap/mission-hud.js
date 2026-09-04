(() => {
  'use strict';

  const app = window.game?.app;
  if (!app?.state) return;

  const root = document.getElementById('missionHud');
  if (!root) return;

  const title = root.querySelector('[data-role="title"]');
  const hpFill = root.querySelector('[data-role="hp-fill"]');
  const hpValue = root.querySelector('[data-role="hp-value"]');
  const manaFill = root.querySelector('[data-role="mana-fill"]');
  const manaValue = root.querySelector('[data-role="mana-value"]');
  const staminaFill = root.querySelector('[data-role="stamina-fill"]');
  const staminaValue = root.querySelector('[data-role="stamina-value"]');
  const meta = root.querySelector('[data-role="meta"]');
  const selected = root.querySelector('[data-role="selected"]');
  const skills = root.querySelector('[data-role="skills"]');

  let disposed = false;

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>\"']/g, ch => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '\"': '&quot;', "'": '&#39;'
    })[ch]);
  }

  function pct(value, max) {
    const v = Number(value || 0);
    const m = Math.max(1, Number(max || 1));
    return Math.max(0, Math.min(100, v / m * 100));
  }

  function formatTime(value) {
    const v = Number(value || 0);
    return v > 0.05 ? v.toFixed(1) + 's' : '就绪';
  }

  function typeLabel(type) {
    const map = {
      MainActive: '主动',
      SubActive: '副技',
      Passive: '被动',
      Spell: '法术',
      CombatArt: '战技',
      Passive_Spell: '被动法术',
      CombatArt_Spell: '法术战技',
      Spell_CombatArt: '战技法术'
    };
    return map[type] || type || '';
  }

  function skillMarkup(item) {
    const skill = item?.skill;
    if (!skill?.equipped) {
      return '<div class="mhud-skill mhud-skill-empty">' +
        '<div class="mhud-skill-head"><span class="mhud-skill-slot">' + escapeHtml(item.slot) + '</span>' +
        '<span class="mhud-skill-name">空</span></div></div>';
    }
    const remaining = Number(skill.remaining || 0);
    const selectedClass = item.key === currentSelectedKey ? ' selected' : '';
    const cd = remaining > 0.05 ? '<span class="mhud-skill-cd">' + escapeHtml(formatTime(remaining)) + '</span>' : '';
    return '<div class="mhud-skill' + selectedClass + '">' +
      '<div class="mhud-skill-head"><span class="mhud-skill-slot">' + escapeHtml(item.slot) + '</span>' +
      '<span class="mhud-skill-name" title="' + escapeHtml(skill.description) + '">' + escapeHtml(skill.name || skill.id) + '</span>' + cd + '</div>' +
      '<div class="mhud-skill-meta"><span>' + escapeHtml(typeLabel(skill.type)) + '</span>' +
      '<span class="cost">' + (Number(skill.cost || 0) > 0 ? escapeHtml(Math.round(skill.cost) + ((skill.type || '').indexOf('Spell') >= 0 ? ' 法力' : ' 耐力')) : '无消耗') + '</span></div>' +
      '</div>';
  }

  let currentSelectedKey = '';

  function render(state) {
    if (disposed) return;
    if (!state?.active) {
      root.setAttribute('aria-hidden', 'true');
      return;
    }

    root.setAttribute('aria-hidden', 'false');
    title.textContent = state.heroName || '战场状态';

    hpFill.style.width = pct(state.health, state.maxHealth) + '%';
    hpValue.textContent = Math.round(state.health) + ' / ' + Math.round(state.maxHealth);
    manaFill.style.width = pct(state.mana, state.maxMana) + '%';
    manaValue.textContent = Math.round(state.mana) + ' / ' + Math.round(state.maxMana);
    staminaFill.style.width = pct(state.stamina, state.maxStamina) + '%';
    staminaValue.textContent = Math.round(state.stamina) + ' / ' + Math.round(state.maxStamina);

    meta.innerHTML =
      '<span class="mhud-pill">护盾 <b>' + Math.round(Number(state.shield || 0)) + '</b></span>' +
      '<span class="mhud-pill">复活 <b>' + Math.max(0, Number(state.resurgence || 0)) + '</b></span>' +
      '<span class="mhud-pill">公共CD <b>' + escapeHtml(formatTime(state.globalCooldown)) + '</b></span>' +
      '<span class="mhud-pill">战技 <b>' + (state.combatArtReady ? '可用' : '准备中') + '</b></span>';

    const selectedSpell = state.selectedSpell;
    currentSelectedKey = selectedSpell?.equipped ? 'spell' + Number(state.selectedSpellSlot || 0) : '';
    if (selectedSpell?.equipped) {
      selected.innerHTML =
        '<div class="mhud-section-title">当前选中法术 · ' + (Number(state.selectedSpellSlot || 0) + 1) + '</div>' +
        '<div class="mhud-selected-name">' + escapeHtml(selectedSpell.name || selectedSpell.id) + '</div>' +
        '<div class="mhud-selected-info"><span>' + escapeHtml(typeLabel(selectedSpell.type)) + '</span>' +
        '<span>' + (Number(selectedSpell.cost || 0) > 0 ? escapeHtml(Math.round(selectedSpell.cost) + ' 法力') : '无消耗') + '</span>' +
        '<span class="mhud-selected-cd">' + escapeHtml(formatTime(selectedSpell.remaining)) + '</span></div>';
    } else {
      selected.innerHTML = '<div class="mhud-section-title">当前选中法术</div><div class="mhud-selected-name">空</div>';
    }

    skills.innerHTML = (state.skills || []).map(skillMarkup).join('');
  }

  let off = null;
  try {
    off = app.state.subscribe('missionHud', render);
    render(app.state.get('missionHud'));
  } catch (_) {
    render(null);
  }

  window.addEventListener('pagehide', () => {
    disposed = true;
    try { off?.(); } catch (_) {}
    off = null;
  }, { once: true });
})();
