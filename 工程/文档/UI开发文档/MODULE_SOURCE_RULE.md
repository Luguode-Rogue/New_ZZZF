# `_Module` 目录：唯一开发修改源

> 生效日期：2026-09-05。此前多次修改丢失的直接原因就是违反了本规则。

## 规则

**`工程/New_ZZZF/_Module/` 是唯一应该手工修改的开发源目录。**

模块根目录下的对应位置（`Modules/New_ZZZF/UI/...`、`ModuleData/...` 等）是**部署产物**，会被同步/构建流程覆盖。直接修改运行目录的文件，改动会在下一次构建或同步时**静默丢失**，且没有任何报错。

## 目录映射

| 开发源（改这里） | 部署产物（不要手改） |
|---|---|
| `工程/New_ZZZF/_Module/UI/**` | `Modules/New_ZZZF/UI/**` |
| `工程/New_ZZZF/_Module/ModuleData/**` | `Modules/New_ZZZF/ModuleData/**` |
| `工程/New_ZZZF/_Module/SubModule.xml` | `Modules/New_ZZZF/SubModule.xml` |
| 框架侧：`BannerlordHtmlUI/_Module/bin/<平台>/web/**` | `Modules/BannerlordHtmlUI/bin/<平台>/web/**` |

框架侧同理：`runtime.js` 是构建产物（由 `runtime-bootstrap/core/i18n.js` 拼接生成），改运行时输入必须改 `runtime-core.js` 源文件，且 `Win64_Shipping_Client` 与 `Gaming.Desktop.x64_Shipping_Client` 两个平台目录要同步修改。

## 判定方法

不确定某个文件是不是部署产物时：

1. 在 `工程/New_ZZZF/_Module/` 下找同名文件——找到，就是镜像关系，改 `_Module` 侧；
2. 在 csproj / Directory.Build.targets 里搜该路径——出现 `Copy`/`Deploy` 目标的就是产物；
3. 改完运行一次构建或同步，**再打开文件确认改动还在**——这是最后一道保险。

## 本次教训（2026-09-05 事故记录）

- `tactical-map.js` 的 canvas 矩形上报、`CustomSkill/index.html` 的输入探针，都曾直接写入 `Modules/New_ZZZF/UI/`，在用户下一次构建 New_ZZZF 后被镜像内容覆盖，导致：
  - 鼠标拦截器收不到 canvas 矩形，静默失效一整轮；
  - CustomSkill 输入探针零输出，浪费一轮诊断；
  - 由于拦截器与探针入口当时都缺少日志，两处静默失败无法区分，多排查了一轮。
- 修复方式：改动全部改写进 `_Module` 镜像，运行目录同步更新；`canvasRect` 命令入口、拦截器命中/未命中路径补齐日志，杜绝静默失败。

## 相关文件

- `BannerlordHtmlUI/DEVELOPMENT_GUIDE.md`：框架侧规范、Chromium 激活硬性边界、Bug 知识库。
- `BannerlordHtmlUI/docs/SURFACES.md`：Surface 架构设计。
