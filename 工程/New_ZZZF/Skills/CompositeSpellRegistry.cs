using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace New_ZZZF
{
    /// <summary>
    /// 自创组合法术的“配方存档”。
    ///
    /// 背景：SkillConfigManager 存档时只写 SkillBase.SkillID 这一个字符串
    /// （见 SkillConfigManager.CreateSkillElement），读档时再用这个字符串回
    /// SkillFactory._skillRegistry 里查实例。而 _skillRegistry 是静态硬编码表，
    /// 进程重启后只剩内置法术 —— 自创的 Composite_xxxx 查不到，
    /// 于是退化成 NullSkill（Type=None），表现就是“法术槽里有东西但按右键没反应”。
    ///
    /// 因此必须把配方（组件ID + 子法术ID）单独落盘，并在游戏启动时
    /// 重新构造 CompositeSpell 注册回 _skillRegistry，ID 保持不变。
    /// </summary>
    public static class CompositeSpellRegistry
    {
        private class Recipe
        {
            public string Id;
            public string Name;
            public string Description;
            public List<string> Components = new List<string>();
            public List<string> Spells = new List<string>();
        }

        private static readonly Dictionary<string, Recipe> _recipes =
            new Dictionary<string, Recipe>(StringComparer.OrdinalIgnoreCase);

        private static bool _loaded;

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord", "Configs", "New_ZZZF");
                return Path.Combine(dir, "composite_spells.xml");
            }
        }

        /// <summary>保存一条配方并立即落盘。</summary>
        public static void Save(string id, string name, string description,
                                List<string> components, List<string> spells)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _recipes[id] = new Recipe
            {
                Id = id,
                Name = name ?? id,
                Description = description ?? string.Empty,
                Components = new List<string>(components ?? new List<string>()),
                Spells = new List<string>(spells ?? new List<string>())
            };

            Persist();
        }

        private static void Persist()
        {
            try
            {
                var root = new XElement("CompositeSpells");
                foreach (var r in _recipes.Values)
                {
                    root.Add(new XElement("Spell",
                        new XAttribute("id", r.Id),
                        new XElement("Name", r.Name),
                        new XElement("Description", r.Description),
                        new XElement("Components",
                            r.Components.Select(c => new XElement("Component", c))),
                        new XElement("Spells",
                            r.Spells.Select(s => new XElement("SubSpell", s)))));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                new XDocument(root).Save(FilePath);
            }
            catch (Exception ex)
            {
                Debug.Print($"[CompositeSpellRegistry] 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读档/启动时调用：把所有配方重建成 CompositeSpell 并注册回 SkillFactory。
        /// 幂等，可重复调用。
        /// </summary>
        public static void LoadAndRegisterAll()
        {
            if (!_loaded)
            {
                _loaded = true;
                LoadFromDisk();
            }

            SpellForgeData.EnsureInitialized();

            foreach (var r in _recipes.Values)
            {
                var composite = new CompositeSpell();
                composite.SetSkillId(r.Id);

                foreach (var sid in r.Spells)
                {
                    var sub = SkillFactory.Create(sid);
                    // Create 找不到时会返回 NullSkill，不能当作子法术塞进去
                    if (sub != null && sub.SkillID != "NullSkill")
                        composite.componentSpells.Add(sub);
                }

                composite.ApplyComponentConfiguration(r.Components);
                composite.Text = new TextObject(r.Name);
                composite.Description = new TextObject(r.Description);

                SkillFactory.RegisterSkill(r.Id, composite);
            }

            if (_recipes.Count > 0)
                Debug.Print($"[CompositeSpellRegistry] 已重建自创法术 {_recipes.Count} 个");
        }

        private static void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                var doc = XDocument.Load(FilePath);
                if (doc.Root == null) return;

                foreach (var e in doc.Root.Elements("Spell"))
                {
                    string id = (string)e.Attribute("id");
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    _recipes[id] = new Recipe
                    {
                        Id = id,
                        Name = (string)e.Element("Name") ?? id,
                        Description = (string)e.Element("Description") ?? string.Empty,
                        Components = e.Element("Components")?.Elements("Component")
                                        .Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList()
                                     ?? new List<string>(),
                        Spells = e.Element("Spells")?.Elements("SubSpell")
                                    .Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList()
                                 ?? new List<string>()
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[CompositeSpellRegistry] 读取失败: {ex.Message}");
            }
        }
    }
}
