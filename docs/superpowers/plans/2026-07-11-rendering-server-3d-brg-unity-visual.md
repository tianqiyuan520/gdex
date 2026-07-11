# RenderingServer3DBRG Unity Visual Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 Godot RenderingServer3DBRG 呈现 Unity Lesson3 的 80×80 动态彩色方块波浪，并支持半径剔除与运动拖影。

**Architecture:** C# 生成按可见性排序的实例逻辑数据，并将位置与相位放在结构体前 16 字节。GDExtension 维护当前/上一帧 RGBA32F 纹理，shader 读取两帧数据复刻 Unity RGB 和可切换拖影，MultiMesh.VisibleInstanceCount 实现真实实例数量剔除。

**Tech Stack:** Godot 4.4 Mono、.NET 8、GDExtension C++、Godot spatial shader、PowerShell 结构回归测试、SCons。

## Global Constraints

- 默认参数严格采用 Unity 示例：40×2、40×2、间距 1.1、振幅 15、速度 3、径向频率 0.2。
- 每实例动态上传固定为 RGBA32F 16 字节。
- 不改写为 RenderingDevice 自定义 draw list，不宣称写入 Godot 原生速度缓冲。

---

### Task 1: 结构回归测试和 16 字节相位布局

**Files:**
- Create: `tests/rendering_server_3d_brg_visual_structure.ps1`
- Modify: `demo/c_sharp/TestNodeRenderingServer3DBRG.cs`
- Modify: `src/gdexample.cpp`

**Interfaces:**
- Produces: `InstanceData3D { Vector3 CurrentPos; float ColorPhase; ... }`，原生提交复制结构前 16 字节。

- [ ] 写测试断言默认半宽参数、`ColorPhase` 和 16 字节 memcpy，运行并观察失败。
- [ ] 最小修改 C# 布局与 C++ 填充，运行测试转绿。

### Task 2: 当前/上一帧双纹理

**Files:**
- Modify: `src/gdexample.h`
- Modify: `src/gdexample.cpp`
- Modify: `demo/c_sharp/TestNodeRenderingServer3DBRG.cs`

**Interfaces:**
- Produces: `get_managed_previous_position_texture_3d()`；当前提交函数每帧更新 previous/current 两张纹理。

- [ ] 扩展测试要求新接口和两个 shader sampler，运行并观察失败。
- [ ] 在 bridge 创建、绑定、返回和更新双纹理；C# 验证接口并绑定材质，运行测试转绿。

### Task 3: Unity 波浪、RGB、剔除和拖影

**Files:**
- Modify: `demo/c_sharp/TestNodeRenderingServer3DBRG.cs`
- Modify: `demo/c_sharp/rendering_server_3d_brg.gdshader`
- Modify: `demo/c_sharp/rendering_server_3d_brg.tscn`

**Interfaces:**
- Produces: `CullTest`、`CullRadius`、`MotionVectorTest`、`MotionTrailStrength` Inspector 参数及 Unity 同款公式。

- [ ] 扩展测试断言 Unity RGB 三公式、半径排序、VisibleInstanceCount 和拖影开关，运行并观察失败。
- [ ] 实现初始化排序、相位更新、材质参数、shader 拖尾和场景默认值，运行测试转绿。

### Task 4: 完整验证

**Files:**
- Verify: all modified files。

- [ ] 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tests/rendering_server_3d_brg_visual_structure.ps1`，预期 PASS。
- [ ] 运行 `dotnet build demo/GDEXTest.csproj -c Release --no-restore`，预期 0 errors。
- [ ] 运行可用的 SCons debug 构建命令，预期成功；若工具不可用则记录环境限制。
- [ ] 定位 Godot Mono 可执行文件并启动目标场景；若不可用则记录环境限制。
- [ ] 对目标文件执行尾随空白与差异检查。
