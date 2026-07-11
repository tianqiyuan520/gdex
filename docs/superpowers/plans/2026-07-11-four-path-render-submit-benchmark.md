# Four-Path Render Submit Benchmark Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在综合基准中公平比较 C# RD、C# RenderingServer、GDExtension RD、GDExtension RenderingServer 四条 8MB 位置纹理提交路径。

**Architecture:** 两个 `TestNodeRD` 节点承担 RD 两项，两个 `TestNodeNormal` 节点承担 RenderingServer 两项。`TestNodeNormal` 新增纯 C# 紧凑位置纹理分支，并与原生紧凑纹理分支共享 shader 和数据布局。

**Tech Stack:** Godot 4.4 C#、.NET 8、GDExtension C++、PowerShell 结构测试。

## Global Constraints

- 四项动态位置数据均为每实例 8 字节。
- C# RenderingServer 路径不得调用 `RenderingServer.GetRenderingDevice()`。
- 紧凑路径只保留一份初始化用的 MultiMesh identity transform buffer。
- 不删除独立 RD 场景或旧 32MB 路径。

---

### Task 1: 四阶段结构测试与场景控制器

**Files:**
- Modify: `tests/multi_rendering_benchmark_structure.ps1`
- Modify: `demo/benchmark/MultiRenderingBenchmark.cs`
- Modify: `demo/benchmark/multi_rendering_benchmark.tscn`

**Interfaces:**
- Produces: 四个独立 benchmark 节点与固定索引信号映射。

- [ ] 扩展结构测试，断言四个节点、四个名称和两种 RS 模式。
- [ ] 运行测试并确认因缺少 C# RenderingServer 节点而失败。
- [ ] 修改控制器与场景，加入四阶段并删除重复三阶段映射。
- [ ] 运行测试并确认通过。

### Task 2: C# RenderingServer 紧凑纹理提交

**Files:**
- Modify: `demo/c_sharp/TestNodeNormal.cs`
- Modify: `tests/multi_rendering_benchmark_structure.ps1`

**Interfaces:**
- Produces: `UseCompactPositionTexture=true` 且 `UseNativePlugin=false` 时的纯 C# `Image.SetData` / `ImageTexture.Update` 路径。

- [ ] 增加失败断言，要求纯 C# 紧凑路径、固定纹理宽度策略和更新调用存在。
- [ ] 运行测试并确认失败原因是行为尚未实现。
- [ ] 初始化复用的 `byte[]`、`Image`、`ImageTexture` 与 shader；热路径以 unsafe 写入 X/Y 后更新纹理。
- [ ] 紧凑模式只分配一份 identity transform buffer，非紧凑旧模式维持双缓冲。
- [ ] 运行结构测试并确认通过。

### Task 3: 完整验证

**Files:**
- Verify: `demo/GDEXTest.csproj`

**Interfaces:**
- Produces: 可编译的四路径综合基准。

- [ ] 运行结构回归测试。
- [ ] 执行 `dotnet build demo/GDEXTest.csproj -c Release` 并要求 0 错误。
- [ ] 检查本次目标文件无尾随空格。

### Task 4: 提交链路与成对比较输出

**Files:**
- Modify: `tests/multi_rendering_benchmark_structure.ps1`
- Modify: `demo/benchmark/MultiRenderingBenchmark.cs`

**Interfaces:**
- Produces: 四条完整提交链路说明，以及 C#路径、GDExtension路径、RD语言边界、RS语言边界四组成对比较。

- [ ] 扩展结构测试，要求四条链路文本、比较方法和四组固定索引映射存在。
- [ ] 运行测试并确认因输出尚未实现而失败。
- [ ] 实现安全的倍数/百分比计算与详细指标输出。
- [ ] 重跑结构测试与 Release 编译并确认通过。

### Task 5: 纯 C++ RenderingServer 8MB 基准

**Files:**
- Modify: `tests/multi_rendering_benchmark_structure.ps1`
- Modify: `src/gdexample.h`
- Modify: `src/gdexample.cpp`
- Modify: `demo/benchmark/MultiRenderingBenchmark.cs`
- Modify: `demo/benchmark/multi_rendering_benchmark.tscn`

**Interfaces:**
- Produces: `GDExample.CompactTextureBenchmark` 属性和第五阶段纯 C++ 位置纹理 benchmark。

- [ ] 增加失败的结构测试，要求 C++ 节点、属性、热路径和两组新增比较存在。
- [ ] 运行测试并确认因纯 C++ 模式缺失而失败。
- [ ] 在 `GDExample` 初始化纯 C++ 紧凑纹理资源，并在物理帧分别统计逻辑、填充和提交。
- [ ] 将第五节点接入综合控制器，输出完整链路及两组成对比较。
- [ ] 重建 GDExtension debug DLL，再运行结构测试与 C# Release 编译。
