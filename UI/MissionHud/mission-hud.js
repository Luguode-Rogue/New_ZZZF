(() => {
  'use strict';

  const app = window.game?.app;
  if (!app?.state) return;

  const hud = document.getElementById('hud');
  if (!hud) return;

  const name = hud.querySelector('[data-role="name"]');
  const hpFill = hud.querySelector('[data-role="hp-fill"]');
  const manaFill = hud.querySelector('[data-role="mana-fill"]');
  const staminaFill = hud.querySelector('[data-role="stamina-fill"]');
  const hpText = hud.querySelector('[data-role="hp-text"]');
  const manaText = hud.querySelector('[data-role="mana-text"]');
  const staminaText = hud.querySelector('[data-role="stamina-text"]');
  const facts = hud.querySelector('[data-role="facts"]');
  const selectedSlot = hud.querySelector('[data-role="selected-slot"]');
  const selectedName = hud.querySelector('[data-role="selected-name"]');
  const selectedCd = hud.querySelector('[data-role="selected-cd"]');
  const selectedDesc = hud.querySelector('[data-role="selected-desc"]');
  const skills = hud.querySelector('[data-role="skills"]');

  let disposed = false;
  let selectedKey = '';

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>\"']/g, ch => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '\"': '&quot;', "'": '&#39;'
    })[ch]);
  }

  function percent(value, max) {
    const v = Number(value || 0);
    const m = Math.max(1, Number(max || 1));
    return Math.max(0, Math.min(100, v / m * 100));
  }

  function number(value) {
    return Math.round(Number(value || 0));
  }

  function cooldown(value) {
    const v = Number(value || 0);
    return v > 0.05 ? v.toFixed(1) + 's' : '就绪';
  }

  function typeLabel(type) {
    const labels = {
      MainActive: '主动',
      SubActive: '副技',
      Passive: '被动',
      Spell: '法术',
      CombatArt: '战技',
      Passive_Spell: '被动法术',
      CombatArt_Spell: '法术战技',
      Spell_CombatArt: '战技法术'
    };
    return labels[type] || type || '';
  }

  function costText(skill) {
    const cost = number(skill?.cost);
    if (cost <= 0) return '无消耗';
    return cost + ((skill?.type || '').indexOf('Spell') >= 0 ? ' 法力' : ' 耐力');
  }

  function renderSkill(item) {
    const skill = item?.skill;
    if (!skill?.equipped) {
      return '<div class="skill empty"><div class="skill-head"><span class="skill-slot">' +
        escapeHtml(item.slot) + '</span><span class="skill-name">空</span></div></div>';
    }

    const selected = item.key === selectedKey ? ' selected' : '';
    const remaining = Number(skill.remaining || 0);
    const cd = remaining > 0.05 ? '<span class="skill-cd">' + escapeHtml(cooldown(remaining)) + '</span>' : '';

    return '<div class="skill' + selected + '">' +
      '<div class="skill-head"><span class="skill-slot">' + escapeHtml(item.slot) + '</span>' +
      '<span class="skill-name" title="' + escapeHtml(skill.description) + '">' + escapeHtml(skill.name || skill.id) + '</span>' + cd + '</div>' +
      '<div class="skill-meta"><span>' + escapeHtml(typeLabel(skill.type)) + '</span><span class="cost">' + escapeHtml(costText(skill)) + '</span></div>' +
      '</div>';
  }

  function render(state) {
    if (disposed) return;
    if (!state?.active) {
      hud.setAttribute('aria-hidden', 'true');
      return;
    }

    hud.setAttribute('aria-hidden', 'false');
    name.textContent = state.heroName || '战场状态';

    hpFill.style.width = percent(state.health, state.maxHealth) + '%';
    manaFill.style.width = percent(state.mana, state.maxMana) + '%';
    staminaFill.style.width = percent(state.stamina, state.maxStamina) + '%';
    hpText.textContent = number(state.health) + ' / ' + number(state.maxHealth);
    manaText.textContent = number(state.mana) + ' / ' + number(state.maxMana);
    staminaText.textContent = number(state.stamina) + ' / ' + number(state.maxStamina);

    facts.innerHTML =
      '<div class="fact"><div class="fact-label">护盾</div><div class="fact-value">' + number(state.shield) + '</div></div>' +
      '<div class="fact"><div class="fact-label">复活</div><div class="fact-value">' + number(state.resurgence) + '</div></div>' +
      '<div class="fact"><div class="fact-label">公共CD</div><div class="fact-value">' + escapeHtml(cooldown(state.globalCooldown)) + '</div></div>' +
      '<div class="fact"><div class="fact-label">战技</div><div class="fact-value">' + (state.combatArtReady ? '可用' : '准备中') + '</div></div>';

    const spell = state.selectedSpell;
    selectedKey = spell?.equipped ? 'spell' + number(state.selectedSpellSlot) : '';
    selectedSlot.textContent = spell?.equipped ? '法术 ' + (number(state.selectedSpellSlot) + 1) : '未选择';
    selectedName.textContent = spell?.equipped ? (spell.name || spell.id || '未知法术') : '空';
    selectedCd.textContent = spell?.equipped ? cooldown(spell.remaining) : '--';
    selectedDesc.textContent = spell?.equipped ? (spell.description || '无说明') : '未选择法术';

    skills.innerHTML = (state.skills || []).map(renderSkill).join('');
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
