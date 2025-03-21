using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;

namespace New_ZZZF
{
    public class SkillInventoryState : PlayerGameState
    {
        // Token: 0x17000B2F RID: 2863
        // (get) Token: 0x06002EAE RID: 11950 RVA: 0x000C1EE8 File Offset: 0x000C00E8
        public override bool IsMenuState
        {
            get
            {
                return true;
            }
        }

        // Token: 0x17000B30 RID: 2864
        // (get) Token: 0x06002EAF RID: 11951 RVA: 0x000C1EEB File Offset: 0x000C00EB
        // (set) Token: 0x06002EB0 RID: 11952 RVA: 0x000C1EF3 File Offset: 0x000C00F3
        public InventoryLogic InventoryLogic { get; private set; }

        // Token: 0x17000B31 RID: 2865
        // (get) Token: 0x06002EB1 RID: 11953 RVA: 0x000C1EFC File Offset: 0x000C00FC
        // (set) Token: 0x06002EB2 RID: 11954 RVA: 0x000C1F04 File Offset: 0x000C0104
        public IInventoryStateHandler Handler
        {
            get
            {
                return this._handler;
            }
            set
            {
                this._handler = value;
            }
        }

        // Token: 0x06002EB3 RID: 11955 RVA: 0x000C1F0D File Offset: 0x000C010D
        public void InitializeLogic(InventoryLogic inventoryLogic)
        {
            this.InventoryLogic = inventoryLogic;
        }

        // Token: 0x04000DF1 RID: 3569
        private IInventoryStateHandler _handler;
    }

}
