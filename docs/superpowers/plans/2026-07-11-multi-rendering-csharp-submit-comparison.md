# MultiRenderingBenchmark C# Submit Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在综合基准中公平比较 C# 直接 RD 提交、C# 经 GDExtension RD 提交和 RenderingServer 位置纹理提交，并移除 32MB MultiMesh 项、补齐中文注释。

**Architecture:** 场景保存两个独立的 `TestNodeRD`，控制器通过描述表把节点、显示名称和回调索引绑定为三个顺序阶段。RD 两条路径共享同一实现类但使用不同 `SubmitMode`；C++ bridge 继续复用原生 `PackedByteArray`。

**Tech Stack:** Godot 4.4 C#、.NET 8、GDExtension C++、Godot shader、PowerShell 结构回归测试。

## Global Constraints

- 三个阶段的动态位置数据均为每实例 8 字节。
- 不新增三缓冲测试，不删除其他场景使用的 `GDExample` 功能。
- 注释解释数据布局、所有权、计时边界和资源生命周期，不逐行翻译语法。

---

### Task 1: 基准结构回归测试与三阶段场景

**Files:**
- Create: `tests/multi_rendering_benchmark_structure.ps1`
- Modify: `demo/benchmark/MultiRenderingBenchmark.cs`
- Modify: `demo/benchmark/multi_rendering_benchmark.tscn`

**Interfaces:**
- Consumes: `TestNodeRD.SubmitMode.CSharpOriginal`、`GDExtensionSingleBuffer` 和现有 benchmark signals。
- Produces: `RenderingDeviceCSharp`、`RenderingDeviceGDExtension`、`RenderingServerTexture` 三阶段顺序基准。

- [ ] **Step 1: 写入失败的结构测试**

测试读取控制器和场景，断言两个 RD 节点、两种模式、三个阶段存在，并断言 `RenderingServerMultiMesh`、`32MB` 不存在。

- [ ] **Step 2: 验证 RED**

Run: `pwsh -NoProfile -File tests/multi_rendering_benchmark_structure.ps1`
Expected: FAIL，指出缺少 `RenderingDeviceCSharp` 或仍存在 32MB 项。

- [ ] **Step 3: 最小实现三阶段控制器与场景**

复制独立 RD 场景节点及 MultiMesh 子资源；控制器分别设置两种 `UploadMode`，用三个独立回调索引累计结果，删除 C++ MultiMesh 阶段。

- [ ] **Step 4: 验证 GREEN**

Run: `pwsh -NoProfile -File tests/multi_rendering_benchmark_structure.ps1`
Expected: PASS。

### Task 2: 提交链路中文注释

**Files:**
- Modify: `demo/c_sharp/TestNodeRD.cs`
- Modify: `src/rd_submit_bridge.h`
- Modify: `src/rd_submit_bridge.cpp`
- Modify: `demo/c_sharp/rd_instance.gdshader`
- Modify: `tests/multi_rendering_benchmark_structure.ps1`

**Interfaces:**
- Consumes: 现有托管数组固定、C ABI 函数指针、`PackedByteArray` 和 shader uniform。
- Produces: 不改变运行行为的中文设计注释。

- [ ] **Step 1: 扩展测试，要求关键中文注释标记**

断言源码包含“每个实例 8 字节”“托管/原生边界”“计时边界”“INSTANCE_ID”等关键说明。

- [ ] **Step 2: 验证 RED**

Run: `pwsh -NoProfile -File tests/multi_rendering_benchmark_structure.ps1`
Expected: FAIL，指出缺少注释说明。

- [ ] **Step 3: 添加最小但详细的中文注释**

在类型、字段、初始化、每帧上传、C++ ABI、buffer 复用、shader 索引映射和释放位置添加说明，不改变公开签名。

- [ ] **Step 4: 验证 GREEN**

Run: `pwsh -NoProfile -File tests/multi_rendering_benchmark_structure.ps1`
Expected: PASS。

### Task 3: 完整验证

**Files:**
- Verify: `demo/GDEXTest.csproj`

**Interfaces:**
- Consumes: Task 1 与 Task 2 的全部改动。
- Produces: 可编译的 Release C# 项目和无空白错误的差异。

- [ ] **Step 1: 运行结构回归测试**

Run: `pwsh -NoProfile -File tests/multi_rendering_benchmark_structure.ps1`
Expected: PASS。

- [ ] **Step 2: 编译 Release**

Run: `dotnet build demo/GDEXTest.csproj -c Release`
Expected: Build succeeded，0 errors。

- [ ] **Step 3: 检查差异**

Run: `git diff --check`
Expected: 无错误输出。
