# 技术参考

这里保存可以跨多个功能复用的 Bannerlord / UI / MCM / API 知识。

## 快速入口

- [API兼容性统一入口](API兼容性_统一入口.md)：MissingMethodException、MissingFieldException、版本差异与 Compatibility 处理。
- `API迁移记忆库.md`：原始 Bannerlord API 迁移经验，完整保留。
- `MCM_INTEGRATION_PROJECT.md`
- `MCM文档编写_c9753007.md`
- `LegacyWorld_MCM技术模式参考手册.md`
- `New_ZZZF_技能界面完整参考手册.md`

## 规则

技术参考描述“可复用知识”，不描述某一次 Bug 的完整排错过程。完整失败方案仍放在 [Bug 修复经验库](../历史/BUG_HISTORY.md) 和原始记录中。

每个技术参考尽量标注：

- 适用 Bannerlord/API 版本
- 是否经过当前工程验证
- 来源
- 已知版本差异
- 是否存在 fallback

版本不明或仅来自历史探索的内容，不应直接作为当前实现规范。
