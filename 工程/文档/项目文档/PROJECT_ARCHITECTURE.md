# New_ZZZF 项目架构

## 总体原则

项目按“功能域 + 基础技术 + 表现层 + 历史知识”组织，而不是让每个功能都建立一套独立基础设施。

```text
SubModule / Bootstrap
        |
        +-------------------+
        |                   |
   Gameplay Core       UI / HtmlUI
        |                   |
        +---------+---------+
                  |
             Shared Services
       Config / Compatibility / Data
                  |
          Historical Knowledge
```

## 功能域

### TacticalMap

战场地图、Terrain、Agent、Formation、Order、Camera 和 UI。

### 技能系统

技能数据、效果、触发、持久化和技能 UI。

### SpellForge / 其他系统

各自维护功能主文档；共享 API、配置、UI 等基础知识统一放到技术参考层。

## UI 边界

UI 层负责：

- 状态展示
- 用户输入
- 与游戏侧的消息/命令交互

UI 层不负责：

- Mission 核心状态的最终所有权
- Campaign/战斗逻辑
- 数据持久化的最终实现

HtmlUI 重制尤其需要遵守这一边界，避免 WebView 生命周期和游戏逻辑生命周期互相锁死。

## 兼容层

Bannerlord 多版本支持时，版本敏感 API、反射字段、类型差异集中到兼容层或明确的 Adapter 中。不要让版本判断散落在业务代码中。

重点包括：

- UI 几何类型 / API
- Mission / Scene 私有字段
- DLC 可选 API
- MCM / UIExtenderEx 等外部依赖

## 日志

生产运行默认关闭每帧 / 高频状态日志。需要排查问题时，以短时间的临时高详细度日志为主，并在修复后恢复默认级别。

## 文档对应

- 当前项目入口：`工程/文档/README.md`
- 当前功能：`工程/文档/功能/`
- 技术参考：`工程/文档/技术参考/`
- 历史经验：`工程/文档/历史/`
