# RenderingServer3DBRG Unity Lesson3 视觉复刻设计

## 目标

将 `RenderingServer3DBRG` 的默认呈现调整为 Unity `BatchRendererGroupSetup.cs` Lesson3 示例：80×80 彩色方块组成径向正弦波，并实现原示例的 `cullTest` 与 `motionVectorTest` 对应效果，同时保留 Godot 当前的 RenderingServer + GDExtension 紧凑纹理提交路径。

参考实现为 [RoadToDotsTutorials Lesson3 BatchRendererGroupSetup.cs](https://github.com/lwwhb/RoadToDotsTutorials/blob/main/Assets/EntitiesGraphicsTutorials/Lesson3/Scripts/BatchRendererGroupSetup.cs)。用户给出的本地文件为 `WVAN` 二进制恢复数据，不能作为源码读取，因此以同仓库公开原始文件为准。

## 默认画面与参数

- X 半宽 `XHalfCount = 40`，Z 半宽 `ZHalfCount = 40`，总实例数为 `80 × 80 = 6400`。
- 网格索引范围为 X/Z 的 `[-40, 39]`。
- 默认间距 `SpacingFactor = 1.1`。
- 波浪相位 `s = elapsed × WaveSpeed + distance × WaveFrequency`。
- 默认 `WaveSpeed = 3`、`WaveFrequency = 0.2`、`WaveHeight = 15`。
- 高度 `y = sin(s) × WaveHeight`。
- 默认网格使用接近 Unity 示例的立方体，场景相机完整覆盖波浪方阵。

保留 Inspector 参数，使用户能够调节半宽、间距、波高、波速和径向频率。实例总数由两个半宽派生，不再单独导出 `InstanceCount`，避免配置矛盾。

## 动态数据布局

每实例继续上传一个 RGBA32F texel，共 16 字节：

- `xyz`：当前世界位置。
- `w`：当前相位 `s`。

C# `InstanceData3D` 将 `CurrentPos` 和 `ColorPhase` 放在结构体开头，使前 16 字节与纹理 texel 完全一致。`BasePos` 和距离相位属于 CPU 逻辑数据，位于后续字段，不上传。

C++ `submit_managed_positions_texture_3d` 按实例 stride 读取并复制结构体前 16 字节，不再生成基于实例编号的颜色索引。上传量保持 `实例数 × 16 字节/帧`。

## Unity 动态颜色

shader 从 `data.w` 取得相位并复刻原公式：

```text
r = (sin(s) + 1) / 2
g = (cos(s × 1.1) + 1) / 2
b = (sin(s × 2.2) + 1) × (cos(s × 2.2) + 1) / 4
a = 1
```

移除当前基于实例编号和高度的 hue 色环。材质继续使用不透明深度、背面剔除和常规 3D 光照。

## CullTest

Unity 原始逻辑在 `CullTest` 开启时只保留索引空间中 `sqrt(x² + z²) <= CullRadius` 的实例，默认半径为 30。

Godot 实现采用真实实例数量限制，而不是 shader 丢弃：

1. 初始化时将半径内实例排在数组前部、半径外实例排在后部。
2. 保存 `visibleCullCount`。
3. `CullTest=false` 时 `MultiMesh.VisibleInstanceCount = 全部实例数`。
4. `CullTest=true` 时 `MultiMesh.VisibleInstanceCount = visibleCullCount`。

因此开启剔除后，半径外实例不会进入实际实例绘制；C# 逻辑与纹理上传仍更新全部实例，以保持切换开关简单且计时口径稳定。

## MotionVectorTest

Unity 的 `motionVectorTest` 本身只设置 `BatchDrawCommandFlags.HasMotion`，最终可见效果依赖 Unity 渲染管线的运动矢量和后处理。Godot 4.4 的自定义空间 shader 没有与该 BRG flag 一一对应的公开实例速度输出接口，因此实现等价的可见运动拖影：

- C# 保存前一帧位置数据并通过第二张 RGBA32F 纹理提交。
- shader 同时读取当前和上一帧位置，计算实例运动方向。
- `MotionVectorTest=false` 时只绘制当前位置。
- `MotionVectorTest=true` 时沿上一帧到当前帧方向对方块顶点产生可调拖尾拉伸，并用 `MotionTrailStrength` 控制强度。
- 该效果用于复刻“开启运动矢量后可观察到运动模糊”的呈现，不宣称写入 Godot 原生屏幕速度缓冲。

为避免每帧新增托管分配，当前/上一帧数组和原生纹理都在初始化时创建并持续复用。

## 组件改动

- `demo/c_sharp/TestNodeRenderingServer3DBRG.cs`
  - 改为半宽派生实例数量。
  - 生成 Unity 同款坐标、相位、高度和剔除排序。
  - 控制 `VisibleInstanceCount`。
  - 管理当前/上一帧提交及 motion shader 参数。
- `src/gdexample.h`、`src/gdexample.cpp`
  - `configure_managed_texture_3d_submit` 创建并维护当前/上一帧两张 RGBA32F `ImageTexture`。
  - 保留 `get_managed_position_texture_3d` 返回当前纹理，新增 `get_managed_previous_position_texture_3d` 返回上一帧纹理。
  - 每次提交先用未写入的双缓冲数据更新上一帧纹理，再用本帧填充结果更新当前纹理，随后切换 CPU buffer 索引。
  - 复制 C# 结构开头的 16 字节位置/相位数据。
- `demo/c_sharp/rendering_server_3d_brg.gdshader`
  - 复刻 Unity RGB。
  - 读取上一帧位置并实现可切换拖影。
- `demo/c_sharp/rendering_server_3d_brg.tscn`
  - 设置 Unity 默认半宽与波浪参数。
  - 调整网格尺寸、相机、灯光与环境参数。

## 错误处理与生命周期

- 初始化时验证 GDExtension 是否提供当前/上一帧纹理配置、读取和提交方法；缺失则打印明确错误并停止处理。
- 原生侧检查纹理、Image、实例指针、数量、stride 和目标 buffer 大小。
- C# 节点退出时停止处理；原生 `Ref` 和 `PackedByteArray` 由 bridge 生命周期管理。
- Inspector 中半宽、半径和间距在初始化时钳制到安全范围。

## 测试与验证

1. 先添加源码/场景结构测试，要求 Unity 默认参数、颜色公式、CullTest、MotionVectorTest 和双纹理接口存在，并确认实现前失败。
2. 按 TDD 分别实现数据布局、颜色、剔除、拖影，每一阶段使对应测试转绿。
3. 执行 `dotnet build demo/GDEXTest.csproj -c Release`。
4. 使用项目现有 SCons 配置构建 GDExtension debug/release 目标；若编译环境不可用，明确报告。
5. 若可定位 Godot Mono 可执行文件，启动 `res://c_sharp/rendering_server_3d_brg.tscn` 检查场景加载和运行时错误。

## 非目标

- 不将该场景改写为完整 RenderingDevice 自定义 draw list。
- 不实现 Unity BRG 的 ConstantBuffer/RawBuffer 分批和手动 draw command API。
- 不实现摄像机视锥逐实例剔除；本次只复刻示例中可切换的半径剔除。
- 不声称拖影写入 Godot 原生 motion-vector velocity buffer。
