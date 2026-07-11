using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public partial class TestNodeNormal : Node2D
{
    // 不通过 RenderingDevice 查询硬件限制；两条 RenderingServer 路径共用相同纹理宽度。
    private const int CompactTextureWidth = 16_384;

    private struct InstanceData
    {
        public Vector2 CurrentPos;
        public Vector2 TargetPos;
        public Vector2 Velocity;
        public bool Arrived;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeSubmitDelegate(
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

    [Export] public Button button;

    [Export(PropertyHint.Range, "1,1000000,1")]
    public int MeshInstanceCount { get; set; } = 20_000;

    [Export]
    public GodotObject NativeSubmitBridge { get; set; }

    // 开关：false = 原版 C# 提交（双缓冲），true = GDExtension 提交
    [Export]
    public bool UseNativePlugin { get; set; } = false;

    [Export]
    public bool UseCompactPositionTexture { get; set; } = false;

    private InstanceData[] instances;
    private float[][] renderBuffers;          // 双缓冲 float[]
    private int activeBufferIndex;
    private NativeSubmitDelegate nativeSubmit;
    private nint nativeSubmitContext;
    private bool compactTextureActive;
    // [csharp-compact-texture] 纯 C# RenderingServer 路径长期复用这些对象。
    // 每个位置 texel 是 X/Y 两个 float，共 8 字节；热路径不会重新创建纹理。
    private byte[] positionData;
    private Image positionImage;
    private ImageTexture positionTexture;
    private int positionTextureWidth;
    private int positionTextureHeight;
    private MultiMeshInstance2D multiMeshInstance;
    private MultiMesh multiMesh;
    private Vector2 viewportSize;
    private double logicTimeUsec;
    private double fillTimeUsec;
    private double submitTimeUsec;
    private int benchmarkWarmupFrames;
    private int benchmarkFrameCount;

    public override void _Ready()
    {
        if (button != null)
            button.Pressed += ShowEntityInfo;

        int count = Math.Max(1, MeshInstanceCount);
        MeshInstanceCount = count;
        instances = new InstanceData[count];
        viewportSize = GetViewportRect().Size;

        multiMeshInstance = GetNode<MultiMeshInstance2D>("MultiMeshInstance2D");
        multiMesh = multiMeshInstance?.Multimesh;
        if (multiMesh == null)
        {
            SetPhysicsProcess(false);
            return;
        }
        multiMesh.InstanceCount = count;
        // Prevent RenderingServer from rescanning one million transforms after
        // every buffer update just to rebuild the MultiMesh bounds.
        multiMesh.CustomAabb = new Aabb(
            new Vector3(-32f, -32f, -1f),
            new Vector3(viewportSize.X + 64f, viewportSize.Y + 64f, 2f));

        // 紧凑纹理只需一份初始化用 identity transform；旧 transform 热路径继续使用双缓冲。
        int totalFloats = count * 8;
        int transformBufferCount = UseCompactPositionTexture ? 1 : 2;
        renderBuffers = new float[transformBufferCount][];
        for (int b = 0; b < transformBufferCount; b++)
        {
            var buffer = new float[totalFloats];
            for (int i = 0; i < count; i++)
            {
                int baseIndex = i * 8;
                buffer[baseIndex] = 1.0f;
                buffer[baseIndex + 5] = 1.0f;
            }
            renderBuffers[b] = buffer;
        }
        activeBufferIndex = 0;

        // 实例数据初始化
        for (int i = 0; i < count; i++)
        {
            instances[i].CurrentPos = new Vector2(
                (i % 50) * 10.0f - 500.0f,
                (i / 50) * 10.0f - 500.0f);
            instances[i].Velocity = new Vector2(
                (float)GD.RandRange(-200.0, 200.0),
                (float)GD.RandRange(-200.0, 200.0));
            instances[i].TargetPos = Vector2.Zero;
            instances[i].Arrived = false;
        }

        if (UseNativePlugin)
        {
            ConfigureNativeSubmit(count);
        }
        else if (UseCompactPositionTexture)
        {
            ConfigureCSharpCompactTexture(count);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Tick(delta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Tick(double delta)
    {
        if (multiMesh == null) return;

        ulong logicStart = Time.GetTicksUsec();
        UpdatePositions((float)delta);
        ulong logicEnd = Time.GetTicksUsec();
        RenderFrame(out double fillUsec, out double submitUsec);

        if (benchmarkWarmupFrames < 60)
        {
            benchmarkWarmupFrames++;
            return;
        }

        logicTimeUsec += logicEnd - logicStart;
        fillTimeUsec += fillUsec;
        submitTimeUsec += submitUsec;
        benchmarkFrameCount++;
        if (benchmarkFrameCount != 60) return;

        double logicMs = logicTimeUsec / benchmarkFrameCount / 1000.0;
        double fillMs = fillTimeUsec / benchmarkFrameCount / 1000.0;
        double submitMs = submitTimeUsec / benchmarkFrameCount / 1000.0;
        double totalMs = logicMs + fillMs + submitMs;
        string mode = compactTextureActive
            ? (UseNativePlugin ? "gdextension_rs_texture_8mb" : "csharp_rs_texture_8mb")
            : nativeSubmit != null ? "gdext_32mb" : "csharp_32mb";
        GD.Print(
            $"[Benchmark][C#] instances={instances.Length} mode={mode} frames={benchmarkFrameCount} " +
            $"logic_avg_ms={logicMs:F4} fill_avg_ms={fillMs:F4} " +
            $"submit_avg_ms={submitMs:F4} total_avg_ms={totalMs:F4}");
        EmitSignal(
            SignalName.BenchmarkCompleted,
            logicMs,
            fillMs,
            submitMs,
            totalMs,
            instances.LongLength);
        logicTimeUsec = 0.0;
        fillTimeUsec = 0.0;
        submitTimeUsec = 0.0;
        benchmarkFrameCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdatePositions(float delta)
    {
        InstanceData[] data = instances;
        float width = viewportSize.X;
        float height = viewportSize.Y;
        for (int i = 0; i < data.Length; i++)
        {
            ref InstanceData instance = ref data[i];
            float x = instance.CurrentPos.X + instance.Velocity.X * delta;
            float y = instance.CurrentPos.Y + instance.Velocity.Y * delta;
            if (x < 0.0f)
            {
                x = 0.0f;
                instance.Velocity.X = -instance.Velocity.X;
            }
            else if (x > width)
            {
                x = width;
                instance.Velocity.X = -instance.Velocity.X;
            }
            if (y < 0.0f)
            {
                y = 0.0f;
                instance.Velocity.Y = -instance.Velocity.Y;
            }
            else if (y > height)
            {
                y = height;
                instance.Velocity.Y = -instance.Velocity.Y;
            }
            instance.CurrentPos.X = x;
            instance.CurrentPos.Y = y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void RenderFrame(out double fillUsec, out double submitUsec)
    {
        ulong renderStart = Time.GetTicksUsec();
        InstanceData[] data = instances; // 移到这里，两个分支共用

        // 纯 C# 非 RD 路径：先形成连续 R32G32 数据，再通过 ImageTexture 高层 API 更新。
        if (UseCompactPositionTexture && !UseNativePlugin && positionImage != null && positionTexture != null)
        {
            fixed (byte* bytes = positionData)
            {
                float* destination = (float*)bytes;
                for (int i = 0; i < data.Length; i++)
                {
                    destination[i * 2] = data[i].CurrentPos.X;
                    destination[i * 2 + 1] = data[i].CurrentPos.Y;
                }
            }

            // submit 包含 byte[] 设置到 Image，以及 ImageTexture.Update 的 CPU 返回时间。
            ulong compactSubmitStart = Time.GetTicksUsec();
            positionImage.SetData(
                positionTextureWidth, positionTextureHeight, false, Image.Format.Rgf, positionData);
            positionTexture.Update(positionImage);
            ulong compactFrameEnd = Time.GetTicksUsec();
            fillUsec = compactSubmitStart - renderStart;
            submitUsec = compactFrameEnd - compactSubmitStart;
            return;
        }

        // ========== GDExtension 路径 ==========
        if (UseNativePlugin && nativeSubmit != null)
        {
            double nativeFillUsec, nativeSubmitUsec;
            fixed (InstanceData* dataPointer = data)
            {
                nativeSubmit(
                    nativeSubmitContext,
                    dataPointer,
                    data.Length,
                    Unsafe.SizeOf<InstanceData>(),
                    out nativeFillUsec,
                    out nativeSubmitUsec);
            }
            ulong end = Time.GetTicksUsec();
            fillUsec = nativeFillUsec;
            submitUsec = Math.Max(nativeSubmitUsec, end - renderStart - fillUsec);
            return;
        }

        // ========== 原版 C# 提交（双缓冲）==========
        float[] buffer = renderBuffers[activeBufferIndex];

        for (int i = 0; i < data.Length; i++)
        {
            int baseIndex = i * 8;
            buffer[baseIndex + 3] = data[i].CurrentPos.X;
            buffer[baseIndex + 7] = data[i].CurrentPos.Y;
        }

        ulong submitStart = Time.GetTicksUsec();
        RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), buffer);
        ulong frameEnd = Time.GetTicksUsec();

        activeBufferIndex ^= 1; // 切换缓冲区

        fillUsec = submitStart - renderStart;
        submitUsec = frameEnd - submitStart;
    }

    private void ConfigureNativeSubmit(int count)
    {
        if (NativeSubmitBridge == null) return;
        long address;
        bool compactBridgeAvailable =
            NativeSubmitBridge.HasMethod("configure_managed_texture_submit") &&
            NativeSubmitBridge.HasMethod("get_managed_position_texture") &&
            NativeSubmitBridge.HasMethod("get_managed_texture_submit_address");
        if (UseCompactPositionTexture && compactBridgeAvailable)
        {
            int textureWidth = Math.Min(count, CompactTextureWidth);
            int textureHeight = (count + textureWidth - 1) / textureWidth;
            NativeSubmitBridge.Call("configure_managed_texture_submit", count, textureWidth, textureHeight);
            ImageTexture positionTexture = NativeSubmitBridge.Call("get_managed_position_texture").As<ImageTexture>();

            // Transform data becomes static; only the compact 8-byte position texture changes.
            RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), renderBuffers[0]);
            var shader = ResourceLoader.Load<Shader>("res://c_sharp/rd_instance.gdshader");
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("instance_positions", positionTexture);
            material.SetShaderParameter("position_texture_width", textureWidth);
            multiMeshInstance.Material = material;
            address = (long)NativeSubmitBridge.Call("get_managed_texture_submit_address");
            compactTextureActive = positionTexture != null && address != 0;
        }
        else
        {
            if (UseCompactPositionTexture)
            {
                GD.PushWarning("GDExtension Debug DLL 不是最新版本，8MB 位置纹理不可用；回退到 32MB Transform 路径。");
            }
            NativeSubmitBridge.Call("configure_managed_submit", multiMesh.GetRid(), count);
            address = (long)NativeSubmitBridge.Call("get_managed_submit_address");
        }
        if (address == 0)
        {
            GD.PushError("GDExtension 提交函数地址无效，停止 benchmark。");
            SetPhysicsProcess(false);
            return;
        }
        nativeSubmitContext = (nint)(long)NativeSubmitBridge.Call("get_native_instance_address");
        nativeSubmit = Marshal.GetDelegateForFunctionPointer<NativeSubmitDelegate>((nint)address);
    }

    private void ConfigureCSharpCompactTexture(int count)
    {
        positionTextureWidth = Math.Min(count, CompactTextureWidth);
        positionTextureHeight = (count + positionTextureWidth - 1) / positionTextureWidth;
        positionData = new byte[positionTextureWidth * positionTextureHeight * sizeof(float) * 2];
        positionImage = Image.CreateFromData(
            positionTextureWidth, positionTextureHeight, false, Image.Format.Rgf, positionData);
        positionTexture = ImageTexture.CreateFromImage(positionImage);
        if (positionImage == null || positionTexture == null)
        {
            GD.PushError("Failed to create the C# RenderingServer compact position texture.");
            SetPhysicsProcess(false);
            return;
        }

        // MultiMesh transform 只初始化一次；每帧位移由 shader 读取 8 字节位置 texel 完成。
        RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), renderBuffers[0]);
        var shader = ResourceLoader.Load<Shader>("res://c_sharp/rd_instance.gdshader");
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("instance_positions", positionTexture);
        material.SetShaderParameter("position_texture_width", positionTextureWidth);
        multiMeshInstance.Material = material;
        compactTextureActive = true;
    }

    public void ShowEntityInfo()
    {
        if (instances == null)
        {
            GD.PushWarning("Entity positions not initialized.");
            return;
        }
        int count = Math.Min(100, instances.Length);
        for (int i = 0; i < count; i++)
            GD.Print(i, "/", instances.Length, " ", instances[i].CurrentPos);
    }
}
