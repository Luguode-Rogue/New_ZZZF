using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.BattlefieldControl
{
    internal static class BattlefieldControlPanel
    {
        internal static void Open(BattlefieldControlMissionLogic logic)
        {
            List<Formation> selected = logic.Selected();
            if (selected.Count == 0)
            {
                BattlefieldControlMissionLogic.Notify("请先用原生命令界面选择你能指挥的编队，再按 Ctrl+K。");
                return;
            }
            var options = new List<InquiryElement>
            {
                new InquiryElement("attack", "指定敌方编队攻击", null),
                new InquiryElement("allow", "只使用所选武器类型", null),
                new InquiryElement("deny", "禁用所选武器类型", null),
                new InquiryElement("clearTarget", "解除目标限制", null),
                new InquiryElement("clearAllow", "解除武器白名单（保留禁用）", null),
                new InquiryElement("clearDeny", "解除武器黑名单（保留允许）", null),
                new InquiryElement("clearWeapons", "解除武器规则", null),
                new InquiryElement("clearAll", "解除全部规则", null)
            };
            Choose("战场控制", "应用于当前选中的 " + selected.Count + " 个编队。", options, 1, rows =>
            {
                if (!logic.IsBattle || !ReferenceEquals(logic, BattlefieldControlMissionLogic.Active)) return;
                switch ((string)rows[0].Identifier)
                {
                    case "attack": Targets(logic, selected); break;
                    case "allow": Weapons(logic, selected, true); break;
                    case "deny": Weapons(logic, selected, false); break;
                    case "clearTarget": logic.Clear(selected, true, false); break;
                    case "clearAllow": logic.ClearWeaponSide(selected, true); break;
                    case "clearDeny": logic.ClearWeaponSide(selected, false); break;
                    case "clearWeapons": logic.Clear(selected, false, true); break;
                    case "clearAll": logic.Clear(selected, true, true); break;
                }
            });
        }

        private static void Targets(BattlefieldControlMissionLogic logic, List<Formation> selected)
        {
            var options = new List<InquiryElement>();
            foreach (Team team in Mission.Current.Teams)
            {
                if (!team.IsEnemyOf(Mission.Current.PlayerTeam)) continue;
                foreach (Formation f in team.FormationsIncludingEmpty)
                    if (f.CountOfUnits > 0)
                        options.Add(new InquiryElement(f,
                            "敌方队伍 " + team.TeamIndex + " / " + f.FormationIndex +
                            "（" + f.CountOfUnits + " 人）", null));
            }
            if (options.Count == 0) { BattlefieldControlMissionLogic.Notify("没有可攻击的敌方编队。"); return; }
            Choose("攻击目标", "只选择该编队内的敌人；目标清空后恢复自动选敌。", options, 1,
                rows => { if (logic.IsBattle) logic.Attack(selected, (Formation)rows[0].Identifier); });
        }

        private static void Weapons(BattlefieldControlMissionLogic logic, List<Formation> selected, bool allowOnly)
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement(ControlledWeaponKind.Sword, "剑 / 匕首（单手、双手）", null),
                new InquiryElement(ControlledWeaponKind.Axe, "斧（单手、双手）", null),
                new InquiryElement(ControlledWeaponKind.Mace, "锤 / 镐（单手、双手）", null),
                new InquiryElement(ControlledWeaponKind.Polearm, "长杆 / 骑枪（近战用法）", null),
                new InquiryElement(ControlledWeaponKind.Bow, "弓", null),
                new InquiryElement(ControlledWeaponKind.Crossbow, "弩", null),
                new InquiryElement(ControlledWeaponKind.Throwing, "投掷（标枪、飞斧、飞刀、石块）", null),
                new InquiryElement(ControlledWeaponKind.OtherRanged, "其他远程武器", null)
            };
            if (!allowOnly) options.Add(new InquiryElement(ControlledWeaponKind.Shield, "盾牌", null));
            Choose(allowOnly ? "只使用这些武器" : "不使用这些装备",
                allowOnly ? "可多选，只约束携带所选类型的士兵。保留黑名单，禁用优先；盾牌另行禁用。无合法武器时收起武器。" :
                "可多选，保留白名单，禁用优先。无合法武器时收起武器。新选择替换旧黑名单。",
                options, options.Count, rows =>
                {
                    if (!logic.IsBattle) return;
                    ControlledWeaponKind kinds = ControlledWeaponKind.None;
                    foreach (InquiryElement row in rows) kinds |= (ControlledWeaponKind)row.Identifier;
                    logic.SetWeapons(selected, allowOnly, kinds);
                });
        }

        private static void Choose(string title, string description, List<InquiryElement> options,
            int max, Action<List<InquiryElement>> action)
        {
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title, description, options, true, 1, max, "确定", "取消", rows =>
                {
                    if (rows != null && rows.Count > 0) action(rows);
                }, null), true);
        }
    }
}
