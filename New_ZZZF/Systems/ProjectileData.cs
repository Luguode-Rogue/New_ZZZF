using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public class ProjectileData
    {
        // 基础属性
        public string Name { get; set; }
        public Agent CasterAgent { get; set; }
        public Agent TargetAgent { get; set; }//目标与地点二选一
        public Vec3 TargetPos { get; set; }//目标与地点二选一
        public float SpawnTime { get; set; }
        public float BaseSpeed = 60f;
        public float MaxTurnRate = 90f;

        // 新增可扩展属性
        public float Lifetime = 5f;          // 默认存在时间5秒
        public Vec3 BaseColor = new Vec3(1, 0, 0); // RGB基础颜色
        public float SpeedMultiplier = 1f;    // 速度倍率
        public bool IsHoming = true;          // 是否自动追踪
        public float SpiralIntensity = 0f;    // 螺旋强度（0=无螺旋）

        // 动态计算属性
        public float Age => Mission.Current.CurrentTime - SpawnTime;
        public float RemainingTime => Lifetime - Age;
    }
}
