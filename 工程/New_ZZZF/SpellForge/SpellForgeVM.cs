using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace New_ZZZF.SpellForge
{
    /// <summary>
    /// 法术锻造界面数据模型。
    /// 三栏结构（与 SpellForgeScreen.xml 绑定对应）：
    ///   左栏 AvailableNodes —— 可配置组件库（节点 / 组件 / 官方法术）
    ///   中栏 CurrentBuild  —— 当前组装的组件与子法术
    ///   右栏 AllSpells     —— 全部魔法（已注册 + 已合成），可装备 / 编辑
    /// </summary>
    public class SpellForgeVM : ViewModel
    {
        private CustomSkillScreenVM _parent;
        private Action _onClose;

        // 可配置组件/法术库（左侧）
        private MBBindingList<ForgeEntryVM> _availableNodes = new MBBindingList<ForgeEntryVM>();
        // 当前组装（中间）
        private MBBindingList<ForgeEntryVM> _currentBuild = new MBBindingList<ForgeEntryVM>();
        // 全部魔法（右侧：已注册法术 + 合成法术）
        private MBBindingList<ForgeEntryVM> _allSpells = new MBBindingList<ForgeEntryVM>();

        private string _newSpellName = "未命名法术";
        private string _buildDescription = "尚未选择任何组件。";
        private string _validationMessage = "";

        public SpellForgeVM(CustomSkillScreenVM parent, Action onClose = null)
        {
            _parent = parent;
            _onClose = onClose;
            SpellForgeData.EnsureInitialized();
            RefreshAvailableNodes();
            RefreshAllSpells();
        }

        // ============================ 数据源属性（与 XML 绑定一致） ============================
        [DataSourceProperty] public MBBindingList<ForgeEntryVM> AvailableNodes { get => _availableNodes; set => SetField(ref _availableNodes, value); }
        [DataSourceProperty] public MBBindingList<ForgeEntryVM> CurrentBuild { get => _currentBuild; set => SetField(ref _currentBuild, value); }
        [DataSourceProperty] public MBBindingList<ForgeEntryVM> AllSpells { get => _allSpells; set => SetField(ref _allSpells, value); }

        [DataSourceProperty] public string NewSpellName { get => _newSpellName; set => SetField(ref _newSpellName, value); }
        [DataSourceProperty] public string BuildDescription { get => _buildDescription; set => SetField(ref _buildDescription, value); }
        [DataSourceProperty] public string ValidationMessage { get => _validationMessage; set => SetField(ref _validationMessage, value); }

        // ============================ 左栏：可配置组件库 ============================
        private void RefreshAvailableNodes()
        {
            var list = new MBBindingList<ForgeEntryVM>();
            // 节点
            foreach (var n in SpellForgeData.Nodes)
                list.Add(new ForgeEntryVM(this, "node:" + n.NodeId, n.Name.ToString(), "[节点] " + n.Description.ToString()));
            // 组件
            foreach (var c in SpellForgeData.Components)
                list.Add(new ForgeEntryVM(this, "comp:" + c.ComponentId, c.Name.ToString(), "[组件] " + c.Description.ToString()));
            // 官方法术（作为子法术原料）
            foreach (var s in SpellForgeData.OfficialSpells)
                list.Add(new ForgeEntryVM(this, "spell:" + s.SpellId, s.Name.ToString(), "[法术] " + s.Description.ToString()));
            // 官方法术组合（预设配方）
            foreach (var k in SpellForgeData.Combinations)
                list.Add(new ForgeEntryVM(this, "combo:" + k.CombinationId, k.Name.ToString(), "[组合] " + k.Description.ToString()));
            AvailableNodes = list;
            SpellForgeDiag.Log($"RefreshAvailableNodes 完成：Nodes={SpellForgeData.Nodes.Count} Components={SpellForgeData.Components.Count} OfficialSpells={SpellForgeData.OfficialSpells.Count} Combos={SpellForgeData.Combinations.Count} => AvailableNodes.Count={AvailableNodes.Count}");
        }

        // ============================ 右栏：全部魔法 ============================
        private void RefreshAllSpells()
        {
            var list = new MBBindingList<ForgeEntryVM>();
            foreach (var kvp in SkillFactory._skillRegistry)
            {
                if (kvp.Value is CompositeSpell cs)
                    list.Add(new ForgeEntryVM(this, kvp.Key, cs.Text?.ToString() ?? kvp.Key, cs.Description?.ToString() ?? ""));
                else if (kvp.Value.Type == SPSkillType.Spell)
                    list.Add(new ForgeEntryVM(this, kvp.Key, kvp.Value.Text?.ToString() ?? kvp.Key, kvp.Value.Description?.ToString() ?? ""));
            }
            AllSpells = list;
        }

        // ============================ 命令（与 XML 绑定一致） ============================
        /// <summary>左栏点击：将一项加入当前组装</summary>
        public void ExecuteAddNode(object param)
        {
            SpellForgeDiag.Log($"ExecuteAddNode 触发 param=({param?.GetType().Name})'{param}'");
            string id = param as string;
            if (string.IsNullOrEmpty(id)) { SpellForgeDiag.Log("ExecuteAddNode 跳过：id为空"); return; }

            if (id.StartsWith("combo:"))
            {
                // 套用官方组合：清空并填入所需组件 + 基础法术
                var combo = SpellForgeData.GetCombination(id.Substring(6));
                if (combo == null) return;
                CurrentBuild.Clear();
                foreach (var c in combo.RequiredComponents)
                    if (SpellForgeData.GetComponent(c) != null)
                        CurrentBuild.Add(new ForgeEntryVM(this, "comp:" + c, SpellForgeData.GetComponent(c).Name.ToString(), ""));
                if (!string.IsNullOrEmpty(combo.BaseSkillId) && SkillFactory._skillRegistry.ContainsKey(combo.BaseSkillId))
                    CurrentBuild.Add(new ForgeEntryVM(this, "spell:" + combo.BaseSkillId, combo.Name.ToString(), ""));
                NewSpellName = combo.Name.ToString();
                BuildDescription = combo.Description.ToString();
                ValidationMessage = $"已套用组合：{combo.Name}";
                return;
            }

            SpellForgeDiag.Log($"ExecuteAddNode 解析id='{id}' 去重前 CurrentBuild.Count={CurrentBuild.Count}");
            // 避免重复加入（组件/法术）
            if (CurrentBuild.Any(x => x.SkillId == id)) { SpellForgeDiag.Log("ExecuteAddNode 跳过：已存在"); return; }

            if (id.StartsWith("node:"))
            {
                var n = SpellForgeData.GetNode(id.Substring(5));
                if (n != null) CurrentBuild.Add(new ForgeEntryVM(this, id, n.Name.ToString(), "[节点] " + n.Description.ToString()));
            }
            else if (id.StartsWith("comp:"))
            {
                var c = SpellForgeData.GetComponent(id.Substring(5));
                if (c != null) CurrentBuild.Add(new ForgeEntryVM(this, id, c.Name.ToString(), "[组件] " + c.Description.ToString()));
            }
            else if (id.StartsWith("spell:"))
            {
                var sid = id.Substring(6);
                if (SkillFactory._skillRegistry.TryGetValue(sid, out var sp))
                    CurrentBuild.Add(new ForgeEntryVM(this, id, sp.Text?.ToString() ?? sid, "[法术] " + (sp.Description?.ToString() ?? "")));
            }
            RecomputeBuildDescription();
        }

        /// <summary>中栏点击：从当前组装移除一项</summary>
        public void ExecuteRemoveNode(object param)
        {
            string id = param as string;
            if (string.IsNullOrEmpty(id)) return;
            var item = CurrentBuild.FirstOrDefault(x => x.SkillId == id);
            if (item != null) { CurrentBuild.Remove(item); RecomputeBuildDescription(); }
        }

        /// <summary>中栏：清空组装</summary>
        public void ExecuteClearBuild()
        {
            CurrentBuild.Clear();
            NewSpellName = "未命名法术";
            BuildDescription = "尚未选择任何组件。";
            ValidationMessage = "";
        }

        /// <summary>中栏：确认铸造 —— 生成 CompositeSpell 并写入英雄法术槽</summary>
        public void ExecuteConfirmSpell()
        {
            var comps = new List<string>();
            var spells = new List<string>();

            foreach (var entry in CurrentBuild)
            {
                if (entry.SkillId.StartsWith("comp:"))
                {
                    comps.Add(entry.SkillId.Substring(5));
                }
                else if (entry.SkillId.StartsWith("spell:"))
                {
                    spells.Add(entry.SkillId.Substring(6));
                }
                else if (entry.SkillId.StartsWith("combo:"))
                {
                    // 展开预设组合：其 RequiredComponents 作为组件，BaseSkillId 作为子法术底料
                    var combo = SpellForgeData.GetCombination(entry.SkillId.Substring(6));
                    if (combo != null)
                    {
                        if (combo.RequiredComponents != null)
                            comps.AddRange(combo.RequiredComponents);
                        if (!string.IsNullOrEmpty(combo.BaseSkillId) && !spells.Contains(combo.BaseSkillId))
                            spells.Add(combo.BaseSkillId);
                    }
                }
                // node: 节点本身不含可直接注入的组件，仅作为结构占位，此处不展开
            }

            // 去重
            comps = comps.Distinct().ToList();
            spells = spells.Distinct().ToList();

            if (comps.Count == 0 && spells.Count == 0)
            {
                ValidationMessage = "错误：至少需要一个组件或一个子法术";
                return;
            }

            var composite = new CompositeSpell();

            // 加载子法术原料
            foreach (var sid in spells)
            {
                var sub = SkillFactory.Create(sid);
                // Create 查不到时返回的是 NullSkill（不是 null），
                // 直接塞进去会让 componentSpells 混入永远不生效的空技能
                if (sub != null && sub.SkillID != "NullSkill")
                    composite.componentSpells.Add(sub);
            }
            composite.ApplyComponentConfiguration(comps);
            composite.Text = new TextObject(NewSpellName);
            composite.Description = new TextObject(BuildDescription);

            // 注册到 SkillFactory（以旧系统为准，动态注册）
            // 关键：注册表的 key 必须与 composite.SkillID 完全一致。
            // 存档只保存 SkillID（见 SkillConfigManager.CreateSkillElement），
            // 读档时用该字符串回注册表查；若两者不一致则查不到 -> 退化成 NullSkill（Type=None，永远无法施放）。
            string uniqueId = "Composite_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            composite.SetSkillId(uniqueId);
            SkillFactory.RegisterSkill(uniqueId, composite);
            // 记录配方，供读档后重建（否则重启游戏法术就丢了）
            CompositeSpellRegistry.Save(uniqueId, NewSpellName, BuildDescription, comps, spells);

            // 写入英雄法术槽
            var uiData = new SkillUIData
            {
                SkillId = uniqueId,
                SkillName = NewSpellName,
                Description = BuildDescription,
                Type = SPSkillType.Spell,
                IconItemId = "lib_book_open_a",
            };
            _parent?.EquipCompositeSpellToCurrentHero(uiData);

            RefreshAllSpells();
            ValidationMessage = $"锻造完成：{NewSpellName}（已写入法术槽）";
        }

        /// <summary>右栏：把一项法术装备到当前英雄法术槽</summary>
        public void ExecuteEquipSpell(object param)
        {
            string id = param as string;
            if (string.IsNullOrEmpty(id)) return;
            if (!SkillFactory._skillRegistry.TryGetValue(id, out var sk)) return;
            var uiData = SkillUIData.FromSkillBase(sk);
            _parent?.EquipCompositeSpellToCurrentHero(uiData);
            ValidationMessage = $"已装备：{uiData.SkillName}";
        }

        /// <summary>右栏：编辑一项已合成法术（加载其配置到中栏）</summary>
        public void ExecuteEditSpell(object param)
        {
            string id = param as string;
            if (string.IsNullOrEmpty(id)) return;
            if (!SkillFactory._skillRegistry.TryGetValue(id, out var sk) || !(sk is CompositeSpell cs)) return;
            CurrentBuild.Clear();
            foreach (var c in cs.ComponentIds)
                if (SpellForgeData.GetComponent(c) != null)
                    CurrentBuild.Add(new ForgeEntryVM(this, "comp:" + c, SpellForgeData.GetComponent(c).Name.ToString(), ""));
            foreach (var sub in cs.componentSpells)
                CurrentBuild.Add(new ForgeEntryVM(this, "spell:" + sub.SkillID, sub.Text?.ToString() ?? sub.SkillID, ""));
            NewSpellName = cs.Text?.ToString() ?? NewSpellName;
            RecomputeBuildDescription();
            ValidationMessage = $"已载入待编辑：{NewSpellName}";
        }

        /// <summary>关闭界面</summary>
        public void ExecuteClose()
        {
            _parent?.NotifySpellForgeClosed();
            _onClose?.Invoke();
        }

        // ============================ 辅助 ============================
        private void RecomputeBuildDescription()
        {
            if (CurrentBuild.Count == 0) { BuildDescription = "尚未选择任何组件。"; return; }
            BuildDescription = "当前组装：" + string.Join(" + ", CurrentBuild.Select(x => x.SkillName));
        }

        private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged(name);
            }
        }
    }

    /// <summary>锻造列表项（左栏组件库 / 中栏组装 / 右栏全部魔法共用）</summary>
    public class ForgeEntryVM : ViewModel
    {
        private SpellForgeVM _parent;
        public string SkillId { get; }
        private string _skillName;
        private string _description;

        public ForgeEntryVM(SpellForgeVM parent, string id, string name, string desc)
        {
            _parent = parent;
            SkillId = id;
            _skillName = name;
            _description = desc;
        }

        [DataSourceProperty]
        public string SkillName
        {
            get => _skillName;
            set
            {
                if (_skillName != value)
                {
                    _skillName = value;
                    OnPropertyChanged(nameof(SkillName));
                }
            }
        }

        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        // ItemTemplate 内按钮的 DataContext 是 ForgeEntryVM 本身，命令必须定义在这里
        public void ExecuteAddNode() { SpellForgeDiag.Log($"ForgeEntryVM.ExecuteAddNode 转发 SkillId='{SkillId}'"); _parent?.ExecuteAddNode(SkillId); }
        public void ExecuteRemoveNode() { SpellForgeDiag.Log($"ForgeEntryVM.ExecuteRemoveNode 转发 SkillId='{SkillId}'"); _parent?.ExecuteRemoveNode(SkillId); }
        public void ExecuteEquipSpell() { SpellForgeDiag.Log($"ForgeEntryVM.ExecuteEquipSpell 转发 SkillId='{SkillId}'"); _parent?.ExecuteEquipSpell(SkillId); }
        public void ExecuteEditSpell() { SpellForgeDiag.Log($"ForgeEntryVM.ExecuteEditSpell 转发 SkillId='{SkillId}'"); _parent?.ExecuteEditSpell(SkillId); }
    }
}

/// <summary>法术锻造诊断日志（与 SpellForgeVM / ForgeEntryVM 同程序集，供两者调用）</summary>
internal static class SpellForgeDiag
{
    private static readonly string ForgeDiagPath =
        @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\affix_debug.log";
    internal static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(ForgeDiagPath, $"[{System.DateTime.Now:HH:mm:ss.fff}] [SpellForge] {msg}{System.Environment.NewLine}"); } catch { }
    }
}
