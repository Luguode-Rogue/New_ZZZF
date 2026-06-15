using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// 战场法力/耐力条 ViewModel
    /// 从 AgentSkillComponent 读取当前玩家的法力/耐力值并绑定到 GauntletUI 进度条
    /// </summary>
    public class NewZZZF_MissionAgentStatusVM : ViewModel
    {
        // ---- 法力 ----
        private float _manaCurrent = 0f;
        private float _manaMax = 100f;
        private string _manaText = "法力  0/100";

        // ---- 耐力 ----
        private float _staminaCurrent = 0f;
        private float _staminaMax = 100f;
        private string _staminaText = "耐力  0/100";

        [DataSourceProperty]
        public float ManaCurrent
        {
            get => _manaCurrent;
            set
            {
                if (_manaCurrent != value)
                {
                    _manaCurrent = value;
                    OnPropertyChangedWithValue(value, nameof(ManaCurrent));
                }
            }
        }

        [DataSourceProperty]
        public float ManaMax
        {
            get => _manaMax;
            set
            {
                if (_manaMax != value)
                {
                    _manaMax = value;
                    OnPropertyChangedWithValue(value, nameof(ManaMax));
                }
            }
        }

        [DataSourceProperty]
        public string ManaText
        {
            get => _manaText;
            set
            {
                if (_manaText != value)
                {
                    _manaText = value;
                    OnPropertyChangedWithValue(value, nameof(ManaText));
                }
            }
        }

        [DataSourceProperty]
        public float StaminaCurrent
        {
            get => _staminaCurrent;
            set
            {
                if (_staminaCurrent != value)
                {
                    _staminaCurrent = value;
                    OnPropertyChangedWithValue(value, nameof(StaminaCurrent));
                }
            }
        }

        [DataSourceProperty]
        public float StaminaMax
        {
            get => _staminaMax;
            set
            {
                if (_staminaMax != value)
                {
                    _staminaMax = value;
                    OnPropertyChangedWithValue(value, nameof(StaminaMax));
                }
            }
        }

        [DataSourceProperty]
        public string StaminaText
        {
            get => _staminaText;
            set
            {
                if (_staminaText != value)
                {
                    _staminaText = value;
                    OnPropertyChangedWithValue(value, nameof(StaminaText));
                }
            }
        }

        /// <summary>
        /// 从 AgentSkillComponent 更新所有绑定值
        /// </summary>
        public void UpdateFromComponent(AgentSkillComponent comp)
        {
            if (comp == null)
            {
                ManaCurrent = 0;
                StaminaCurrent = 0;
                ManaText = "法力  --/--";
                StaminaText = "耐力  --/--";
                return;
            }

            ManaCurrent = comp._currentMana;
            StaminaCurrent = comp._currentStamina;
            ManaText = $"法力  {(int)comp._currentMana}/{(int)ManaMax}";
            StaminaText = $"耐力  {(int)comp._currentStamina}/{(int)StaminaMax}";
        }
    }
}
