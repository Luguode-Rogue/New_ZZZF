using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public class AgentMissileSpeedData
    {
        public Agent Agent;
        public MissionWeapon Weapon { get; set; }
        public float MissileSpeed { get; set; }
        public AgentMissileSpeedData(MissionWeapon weapon, float missileSpeen, Agent agent)
        {
            Weapon = weapon;
            MissileSpeed = missileSpeen;
            Agent = agent;
        }
    }
}
