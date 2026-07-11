# Unity BRG Benchmark Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Unity Lesson3 BatchRendererGroup 增加 60 帧预热、60 帧采样的分阶段 CPU 平均耗时日志。

**Architecture:** 保持现有 Job 和 SetData 顺序，使用 Stopwatch 在调度前、调度后、Complete 后和 SetData 后采样。累计逻辑、填充和提交 tick，每 60 个有效帧统一转换为毫秒并输出。

**Tech Stack:** Unity C#、Burst Jobs、GraphicsBuffer、PowerShell 结构测试。

## Global Constraints

- 不拆分或改写原 Burst Job。
- 不更改渲染、剔除、实例或材质行为。
- 输出四位小数，预热和采样均为 60 帧。

---

### Task 1: 失败的日志结构测试

**Files:**
- Create: `tests/unity_brg_benchmark_logging_structure.ps1`
- Test: `E:/UnityProject/RoadToDotsTutorials-main/Assets/EntitiesGraphicsTutorials/Lesson3/Scripts/BatchRendererGroupSetup.cs`

- [ ] 写测试断言 Stopwatch、预热/采样常量、四个边界时间戳和目标中文日志存在。
- [ ] 运行测试并确认因缺少 benchmark 字段失败。

### Task 2: 最小计时实现

**Files:**
- Modify: `E:/UnityProject/RoadToDotsTutorials-main/Assets/EntitiesGraphicsTutorials/Lesson3/Scripts/BatchRendererGroupSetup.cs`

- [ ] 新增计时常量、累计字段和 tick 转毫秒 helper。
- [ ] 在 Update 保持原顺序插入时间戳，预热后累计。
- [ ] 每 60 帧使用 `UnityEngine.Debug.Log` 输出平均值并清零。
- [ ] 运行结构测试并确认通过。

### Task 3: 验证

- [ ] 检查外部脚本尾随空白和括号平衡。
- [ ] 搜索 Unity Editor；若可用则执行 batchmode 工程编译检查，否则报告环境限制。
