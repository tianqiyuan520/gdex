using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Godot RenderingServer 版本的 Unity BatchRendererGroup Lesson3 示例。
///
/// 对应关系：
/// Unity RegisterMesh/RegisterMaterial -> Godot 持久 MultiMesh + ShaderMaterial
/// Unity GraphicsBuffer              -> 双缓冲 RGBA32F ImageTexture
/// Unity BRG batch/global bounds     -> MultiMesh.InstanceCount + CustomAabb
/// Unity MetadataValue               -> shader uniform + INSTANCE_ID 纹理寻址
/// Unity IJobFor                     -> CPU InstanceData[] 更新循环
/// Unity SetData                     -> GDExtension ImageTexture.update()
///
/// 每实例动态数据为 position.xyz + color_index，共 16 字节。Transform3D identity
/// 缓冲仅初始化一次，不会每帧提交 12 个 float 的完整变换。
///
/// 构建：
///   scons platform=windows target=template_debug arch=x86_64 -j4
///   dotnet build demo/GDEXTest.csproj -c Debug
///   完整重启 Godot 后打开 res://c_sharp/rendering_server_3d_brg.tscn
///
/// Release：
///   scons platform=windows target=template_release arch=x86_64 -j4
///   dotnet build demo/GDEXTest.csproj -c Release
///   使用项目的 Windows Desktop export preset 导出。
/// </summary>
public partial class TestNodeRenderingServer3DBRG : Node3D
{
    [StructLayout(LayoutKind.Sequential)]
    private struct InstanceData3D
    {
        // 原生桥接把结构体开头 16 字节原样复制为 RGBA32F：xyz=位置，w=颜色相位。
        public Vector3 CurrentPos;
        public float ColorPhase;
        public Vector3 BasePos;
        public float DistancePhase;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void NativeSubmitDelegate(
        nint context,
        InstanceData3D* instances,
        int count,
        int stride,
        out double fillUsec,
        out double submitUsec);

    [Export(PropertyHint.Range, "1,100,1")]
    public int XHalfCount { get; set; } = 40;

    [Export(PropertyHint.Range, "1,100,1")]
    public int ZHalfCount { get; set; } = 40;

    public int InstanceCount => XHalfCount * 2 * ZHalfCount * 2;

    [Export(PropertyHint.Range, "0.25,5,0.05")]
    public float SpacingFactor { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float WaveHeight { get; set; } = 15f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float WaveSpeed { get; set; } = 3f;

    [Export(PropertyHint.Range, "0.001,1,0.001")]
    public float WaveFrequency { get; set; } = 0.2f;

    [Export] public bool CullTest { get; set; }

    [Export(PropertyHint.Range, "1,100,0.5")]
    public float CullRadius { get; set; } = 30f;

    [Export] public bool MotionVectorTest { get; set; }

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float MotionTrailStrength { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,600,1")]
    public int WarmupFrames { get; set; } = 60;

    [Export(PropertyHint.Range, "1,600,1")]
    public int SampleFrames { get; set; } = 60;

    [Export] public NodePath NativeBridgePath { get; set; } = "NativeBridge";

    private InstanceData3D[] instances;
    private MultiMeshInstance3D multiMeshInstance;
    private MultiMesh multiMesh;
    private ShaderMaterial shaderMaterial;
    private GodotObject nativeBridge;
    private NativeSubmitDelegate nativeSubmit;
    private nint nativeContext;
    private double elapsed;
    private int gridWidth;
    private int gridDepth;
    private int visibleCullCount;
    private bool lastMotionVectorTest;
    private float lastMotionTrailStrength = float.NaN;

    private double logicUsec;
    private double fillUsec;
    private double submitUsec;
    private int warmupCount;
    private int sampleCount;

    public override void _Ready()
    {
        XHalfCount = Math.Clamp(XHalfCount, 1, 100);
        ZHalfCount = Math.Clamp(ZHalfCount, 1, 100);
        SpacingFactor = Math.Max(0.01f, SpacingFactor);
        CullRadius = Math.Max(1f, CullRadius);
        WarmupFrames = Math.Max(0, WarmupFrames);
        SampleFrames = Math.Max(1, SampleFrames);

        multiMeshInstance = GetNode<MultiMeshInstance3D>("MultiMeshInstance3D");
        multiMesh = multiMeshInstance.Multimesh;
        nativeBridge = GetNodeOrNull<GodotObject>(NativeBridgePath);
        if (!ValidateBridge())
        {
            SetProcess(false);
            return;
        }

        InitializeInstances();
        InitializeStaticBatch();
		if (!InitializeTextureAndMaterial())
		{
			SetProcess(false);
			return;
		}
        ConfigureCamera();
		UpdateRenderingOptions(force: true);
        GD.Print(
            $"[RenderingServer 3D BRG] 初始化完成 实例={InstanceCount} " +
            $"网格={gridWidth}x{gridWidth} 动态提交={InstanceCount * 16 / (1024.0 * 1024.0):F2}MiB/帧");
    }

    private bool ValidateBridge()
    {
        string[] methods =
        {
            "configure_managed_texture_3d_submit",
            "get_managed_position_texture_3d",
            "get_managed_previous_position_texture_3d",
            "get_managed_texture_3d_submit_address",
            "get_native_instance_address"
        };
        if (nativeBridge == null)
        {
            GD.PushError("找不到 3D RenderingServer NativeBridge。");
            return false;
        }
        foreach (string method in methods)
        {
            if (!nativeBridge.HasMethod(method))
            {
                GD.PushError($"GDExtension 缺少 {method}。请重新构建 DLL 并完整重启编辑器。");
                return false;
            }
        }
        return true;
    }

    private void InitializeInstances()
    {
        instances = new InstanceData3D[InstanceCount];
        gridWidth = XHalfCount * 2;
        gridDepth = ZHalfCount * 2;
		int output = 0;

		// Unity cullTest 只保留索引半径内的实例。先写入圆内、再写入圆外，之后只需
		// VisibleInstanceCount 就能真正减少绘制实例，而不必每帧重排纹理。
		for (int pass = 0; pass < 2; pass++)
        {
			for (int source = 0; source < instances.Length; source++)
			{
				int x = source % gridWidth - XHalfCount;
				int z = source / gridWidth - ZHalfCount;
				bool insideCullRadius = x * x + z * z <= CullRadius * CullRadius;
				if ((pass == 0) != insideCullRadius) continue;

				Vector3 position = new(x * SpacingFactor, 0f, z * SpacingFactor);
				instances[output].BasePos = position;
				instances[output].CurrentPos = position;
				instances[output].DistancePhase = position.Length() * WaveFrequency;
				output++;
			}
			if (pass == 0) visibleCullCount = output;
        }
    }

    private void InitializeStaticBatch()
    {
        multiMesh.InstanceCount = InstanceCount;
		float halfExtentX = XHalfCount * SpacingFactor + 2f;
		float halfExtentZ = ZHalfCount * SpacingFactor + 2f;
        multiMesh.CustomAabb = new Aabb(
			new Vector3(-halfExtentX, -WaveHeight - 2f, -halfExtentZ),
			new Vector3(halfExtentX * 2f, WaveHeight * 2f + 4f, halfExtentZ * 2f));

        // Transform3D buffer：3 行 x 4 float。只提交一次 identity。
        var identityTransforms = new float[InstanceCount * 12];
        for (int i = 0; i < InstanceCount; i++)
        {
            int offset = i * 12;
            identityTransforms[offset] = 1f;
            identityTransforms[offset + 5] = 1f;
            identityTransforms[offset + 10] = 1f;
        }
        RenderingServer.MultimeshSetBuffer(multiMesh.GetRid(), identityTransforms);
    }

	private bool InitializeTextureAndMaterial()
    {
        RenderingDevice rd = RenderingServer.GetRenderingDevice();
		if (rd == null)
		{
			GD.PushWarning("当前渲染模式没有全局 RenderingDevice（例如 --headless）；停止 3D BRG 演示。");
			return false;
		}
        int textureWidth = Math.Min(
            InstanceCount,
            (int)rd.LimitGet(RenderingDevice.Limit.MaxTextureSize2D));
        int textureHeight = (InstanceCount + textureWidth - 1) / textureWidth;
        nativeBridge.Call(
            "configure_managed_texture_3d_submit",
            InstanceCount,
            textureWidth,
            textureHeight);

        ImageTexture instanceTexture = nativeBridge
            .Call("get_managed_position_texture_3d")
            .As<ImageTexture>();
        ImageTexture previousInstanceTexture = nativeBridge
            .Call("get_managed_previous_position_texture_3d")
            .As<ImageTexture>();
        long address = (long)nativeBridge.Call("get_managed_texture_3d_submit_address");
        nativeContext = (nint)(long)nativeBridge.Call("get_native_instance_address");
        if (instanceTexture == null || previousInstanceTexture == null || address == 0 || nativeContext == 0)
        {
            throw new InvalidOperationException("3D BRG 原生桥接初始化失败。");
        }
        nativeSubmit = Marshal.GetDelegateForFunctionPointer<NativeSubmitDelegate>((nint)address);

        var shader = ResourceLoader.Load<Shader>("res://c_sharp/rendering_server_3d_brg.gdshader");
		shaderMaterial = new ShaderMaterial { Shader = shader };
		shaderMaterial.SetShaderParameter("instance_data", instanceTexture);
		shaderMaterial.SetShaderParameter("previous_instance_data", previousInstanceTexture);
		shaderMaterial.SetShaderParameter("instance_texture_width", textureWidth);
		multiMeshInstance.MaterialOverride = shaderMaterial;
		return true;
    }

    private void ConfigureCamera()
    {
		float extent = Math.Max(gridWidth, gridDepth) * SpacingFactor;
        Camera3D camera = GetNode<Camera3D>("Camera3D");
        camera.Position = new Vector3(extent * 0.62f, extent * 0.48f, extent * 0.62f);
        camera.Far = Math.Max(1000f, extent * 4f);
        camera.LookAt(Vector3.Zero, Vector3.Up);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void _Process(double delta)
    {
		UpdateRenderingOptions();
        elapsed += delta;
        ulong logicStart = Time.GetTicksUsec();
        UpdateWave((float)elapsed);
        ulong logicEnd = Time.GetTicksUsec();
        Submit(out double frameFillUsec, out double frameSubmitUsec);

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
        GD.Print(
            $"[RenderingServer 3D BRG] 实例={InstanceCount} 帧={sampleCount} " +
            $"逻辑={logicMs:F4}ms 填充RGBA32F={fillMs:F4}ms " +
            $"提交={submitMs:F4}ms CPU总计={logicMs + fillMs + submitMs:F4}ms");
        logicUsec = fillUsec = submitUsec = 0;
        sampleCount = 0;
    }

	private void UpdateRenderingOptions(bool force = false)
	{
		multiMesh.VisibleInstanceCount = CullTest ? visibleCullCount : InstanceCount;
		if (force || MotionVectorTest != lastMotionVectorTest)
		{
			shaderMaterial.SetShaderParameter("motion_vector_test", MotionVectorTest);
			lastMotionVectorTest = MotionVectorTest;
		}
		if (force || !Mathf.IsEqualApprox(MotionTrailStrength, lastMotionTrailStrength))
		{
			shaderMaterial.SetShaderParameter("motion_trail_strength", MotionTrailStrength);
			lastMotionTrailStrength = MotionTrailStrength;
		}
	}

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UpdateWave(float time)
    {
        float animation = time * WaveSpeed;
        for (int i = 0; i < instances.Length; i++)
        {
            ref InstanceData3D instance = ref instances[i];
            instance.CurrentPos.X = instance.BasePos.X;
            instance.ColorPhase = animation + instance.DistancePhase;
            instance.CurrentPos.Y = MathF.Sin(instance.ColorPhase) * WaveHeight;
            instance.CurrentPos.Z = instance.BasePos.Z;
        }
    }

    private unsafe void Submit(out double frameFillUsec, out double frameSubmitUsec)
    {
        fixed (InstanceData3D* pointer = instances)
        {
            nativeSubmit(
                nativeContext,
                pointer,
                instances.Length,
                Unsafe.SizeOf<InstanceData3D>(),
                out frameFillUsec,
                out frameSubmitUsec);
        }
    }
}
