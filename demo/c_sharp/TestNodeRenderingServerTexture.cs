using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// RenderingServer 百万实例位置纹理示例（每实例每帧只提交 8 字节）。
///
/// 设计思路：
/// 1. CPU 继续使用完整 InstanceData[] 作为权威 ECS 数据，不改变结构体布局。
/// 2. MultiMesh 的 identity Transform 只提交一次，以后不再上传 32MB Transform。
/// 3. 每帧仅从 InstanceData.CurrentPos 提取 Vector2，即 8 字节/实例。
/// 4. GDExtension 使用两个 PackedByteArray 轮换，避免 Image.set_data() 的写时复制。
/// 5. ImageTexture 通过 RenderingServer 更新，canvas shader 按 INSTANCE_ID 读取位置。
/// 6. 固定 CustomAabb，避免错误裁剪，也避免完整 Transform 路径中的 AABB 重算。
///
/// 100 万实例每帧动态提交量：
///     1,000,000 * sizeof(Vector2) = 8,000,000 字节，约 7.63 MiB。
///
/// 构建步骤（在仓库根目录执行）：
///
/// Debug（编辑器运行）：
///     scons platform=windows target=template_debug arch=x86_64 -j4
///     dotnet build demo/GDEXTest.csproj -c Debug
///     完整重启 Godot 编辑器，再打开：
///     res://c_sharp/rendering_server_texture_detailed.tscn
///
/// Release（正式性能测试）：
///     scons platform=windows target=template_release arch=x86_64 -j4
///     dotnet build demo/GDEXTest.csproj -c Release
///     &lt;Godot控制台路径&gt; --headless --path demo --export-release "Windows Desktop" \
///         demo/build/release/GDEXTest.exe
///
/// 导出后运行指定场景：
///     demo/build/release/GDEXTest.console.exe \
///         res://c_sharp/rendering_server_texture_detailed.tscn
///
/// 注意：编辑器 F6 使用 Debug，不适合最终性能结论。GDExtension 被编辑器加载后，
/// 仅重新编译 DLL 不会刷新已注册的方法，必须完整关闭并重启编辑器。
/// </summary>
public partial class TestNodeRenderingServerTexture : Node2D
{
    // 必须与原生桥接读取的布局一致。CurrentPos 必须保持为第一个字段。
    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceData
    {
        public Vector2 CurrentPos;
        public Vector2 TargetPos;
        public Vector2 Velocity;
        public bool Arrived;
    }

    // 直接调用原生同步函数，避免每帧经过 Variant/反射式 GodotObject.Call。
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeTextureSubmitDelegate(
        nint context,
        InstanceData* instances,
        int count,
        int stride,
        out double fillUsec,
        out double submitUsec);

    [Signal]
    public delegate void BenchmarkCompletedEventHandler(
        double logicMs,
        double fillMs,
        double submitMs,
        double totalMs,
        long instanceCount);

    [Export(PropertyHint.Range, "1,1000000,1")]
    public int InstanceCount { get; set; } = 1_000_000;

    [Export(PropertyHint.Range, "0,600,1")]
    public int WarmupFrames { get; set; } = 60;

    [Export(PropertyHint.Range, "1,600,1")]
    public int SampleFrames { get; set; } = 60;

    // 指向场景中的 GDExample 节点。该节点只充当 GDExtension 桥接对象，不参与处理。
    [Export]
    public NodePath NativeBridgePath { get; set; } = "NativeBridge";

    private InstanceData[] instances;
    private Vector2 viewportSize;
    private MultiMeshInstance2D multiMeshInstance;
    private MultiMesh multiMesh;
    private GodotObject nativeBridge;
    private NativeTextureSubmitDelegate nativeSubmit;
    private nint nativeContext;

    private double logicUsec;
    private double fillUsec;
    private double submitUsec;
    private int warmupCount;
    private int sampleCount;

    public override void _Ready()
    {
        InstanceCount = Math.Max(1, InstanceCount);
        WarmupFrames = Math.Max(0, WarmupFrames);
        SampleFrames = Math.Max(1, SampleFrames);
        viewportSize = GetViewportRect().Size;

        multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
        multiMesh = multiMeshInstance.Multimesh;
        nativeBridge = GetNodeOrNull<GodotObject>(NativeBridgePath);

        if (!ValidateNativeBridge())
        {
            SetPhysicsProcess(false);
            return;
        }

        InitializeCpuEntities();
        InitializeStaticMultiMesh();
        InitializePositionTextureBridge();

        GD.Print(
            $"[RenderingServer 8MB] 初始化完成 实例={InstanceCount} " +
            $"动态提交={InstanceCount * sizeof(float) * 2 / (1024.0 * 1024.0):F2}MiB/帧 " +
            $"预热={WarmupFrames}帧 采样={SampleFrames}帧");
    }

    private bool ValidateNativeBridge()
    {
        if (nativeBridge == null)
        {
            GD.PushError("缺少 NativeBridge。请使用配套场景或设置 NativeBridgePath。");
            return false;
        }

        string[] requiredMethods =
        {
            "configure_managed_texture_submit",
            "get_managed_position_texture",
            "get_managed_texture_submit_address",
            "get_native_instance_address"
        };
        foreach (string method in requiredMethods)
        {
            if (!nativeBridge.HasMethod(method))
            {
                GD.PushError(
                    $"GDExtension 缺少方法 {method}。重新构建 Debug DLL 后必须完整重启 Godot 编辑器。");
                return false;
            }
        }
        return true;
    }

    private void InitializeCpuEntities()
    {
        instances = new InstanceData[InstanceCount];
        var random = new Random(12345);
        for (int i = 0; i < instances.Length; i++)
        {
            instances[i].CurrentPos = new Vector2((i % 50) * 10f, (i / 50) * 10f);
            instances[i].Velocity = new Vector2(
                (float)random.NextDouble() * 400f - 200f,
                (float)random.NextDouble() * 400f - 200f);
        }
    }

    private void InitializeStaticMultiMesh()
    {
        multiMesh.InstanceCount = InstanceCount;

        // shader 会移动 identity 实例，因此显式提供实际世界范围。
        multiMesh.CustomAabb = new Aabb(
            new Vector3(-32f, -32f, -1f),
            new Vector3(viewportSize.X + 64f, viewportSize.Y + 64f, 2f));

        // Transform2D 在 MultiMesh buffer 中占 8 个 float。该 32MB 缓冲只提交一次。
        var identityTransforms = new float[InstanceCount * 8];
        for (int i = 0; i < InstanceCount; i++)
        {
            int offset = i * 8;
            identityTransforms[offset] = 1f;
            identityTransforms[offset + 5] = 1f;
        }
        RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), identityTransforms);
    }

    private void InitializePositionTextureBridge()
    {
        RenderingDevice rd = RenderingServer.GetRenderingDevice();
        int maxTextureWidth = (int)rd.LimitGet(RenderingDevice.Limit.MaxTextureSize2D);
        int textureWidth = Math.Min(InstanceCount, maxTextureWidth);
        int textureHeight = (InstanceCount + textureWidth - 1) / textureWidth;

        // 原生侧创建双 PackedByteArray、Image 和 ImageTexture。
        nativeBridge.Call(
            "configure_managed_texture_submit",
            InstanceCount,
            textureWidth,
            textureHeight);

        ImageTexture positionTexture = nativeBridge
            .Call("get_managed_position_texture")
            .As<ImageTexture>();
        long submitAddress = (long)nativeBridge.Call("get_managed_texture_submit_address");
        nativeContext = (nint)(long)nativeBridge.Call("get_native_instance_address");

        if (positionTexture == null || submitAddress == 0 || nativeContext == 0)
        {
            throw new InvalidOperationException("无法初始化 RenderingServer 位置纹理桥接。");
        }
        nativeSubmit = Marshal.GetDelegateForFunctionPointer<NativeTextureSubmitDelegate>((nint)submitAddress);

        var shader = ResourceLoader.Load<Shader>("res://c_sharp/rd_instance.gdshader");
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("instance_positions", positionTexture);
        material.SetShaderParameter("position_texture_width", textureWidth);
        multiMeshInstance.Material = material;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void _PhysicsProcess(double delta)
    {
        ulong logicStart = Time.GetTicksUsec();
        UpdatePositions((float)delta);
        ulong logicEnd = Time.GetTicksUsec();

        SubmitPositions(out double frameFillUsec, out double frameSubmitUsec);

        if (warmupCount < WarmupFrames)
        {
            warmupCount++;
            return;
        }

        logicUsec += logicEnd - logicStart;
        fillUsec += frameFillUsec;
        submitUsec += frameSubmitUsec;
        sampleCount++;
        if (sampleCount < SampleFrames) return;

        double logicMs = logicUsec / sampleCount / 1000.0;
        double fillMs = fillUsec / sampleCount / 1000.0;
        double submitMs = submitUsec / sampleCount / 1000.0;
        double totalMs = logicMs + fillMs + submitMs;
        GD.Print(
            $"[RenderingServer 8MB] 实例={InstanceCount} 帧={sampleCount} " +
            $"纯逻辑={logicMs:F4}ms 位置提取={fillMs:F4}ms " +
            $"纹理提交={submitMs:F4}ms CPU渲染准备={fillMs + submitMs:F4}ms " +
            $"CPU总计={totalMs:F4}ms");
        EmitSignal(SignalName.BenchmarkCompleted, logicMs, fillMs, submitMs, totalMs, (long)InstanceCount);

        logicUsec = fillUsec = submitUsec = 0;
        sampleCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdatePositions(float delta)
    {
        float width = viewportSize.X;
        float height = viewportSize.Y;
        for (int i = 0; i < instances.Length; i++)
        {
            ref InstanceData instance = ref instances[i];
            float x = instance.CurrentPos.X + instance.Velocity.X * delta;
            float y = instance.CurrentPos.Y + instance.Velocity.Y * delta;
            if (x < 0f) { x = 0f; instance.Velocity.X = -instance.Velocity.X; }
            else if (x > width) { x = width; instance.Velocity.X = -instance.Velocity.X; }
            if (y < 0f) { y = 0f; instance.Velocity.Y = -instance.Velocity.Y; }
            else if (y > height) { y = height; instance.Velocity.Y = -instance.Velocity.Y; }
            instance.CurrentPos.X = x;
            instance.CurrentPos.Y = y;
        }
    }

    private unsafe void SubmitPositions(out double frameFillUsec, out double frameSubmitUsec)
    {
        // fixed 只在同步原生调用期间固定数组。函数返回后原生不再保留该指针。
        fixed (InstanceData* pointer = instances)
        {
            nativeSubmit(
                nativeContext,
                pointer,
                instances.Length,
                Unsafe.SizeOf<InstanceData>(),
                out frameFillUsec,
                out frameSubmitUsec);
        }
    }
}
