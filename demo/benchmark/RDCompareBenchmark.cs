using Godot;
using System;

public partial class RDCompareBenchmark : Node2D
{
    private sealed class Stats
    {
        public int Count;
        public double Logic, Fill, Submit, Gpu, Total;
        public int GpuCount;
        public double LogicAverage => Logic / Math.Max(1, Count);
        public double FillAverage => Fill / Math.Max(1, Count);
        public double SubmitAverage => Submit / Math.Max(1, Count);
        public double RenderAverage => FillAverage + SubmitAverage;
        public double GpuAverage => GpuCount > 0 ? Gpu / GpuCount : -1;

        public void Add(double logic, double fill, double submit, double gpu, double total)
        {
            Count++; Logic += logic; Fill += fill; Submit += submit; Total += total;
            if (gpu >= 0) { Gpu += gpu; GpuCount++; }
        }
    }

    [Export(PropertyHint.Range, "1,1000000,1")] public int InstanceCount { get; set; } = 100_000;
    [Export(PropertyHint.Range, "1,20,1")] public int SampleRounds { get; set; } = 5;

    private readonly TestNodeRD[] benchmarks = new TestNodeRD[3];
    private readonly Stats[] stats = { new(), new(), new() };
    private Label results;
    private int stage;

    public override void _EnterTree()
    {
        InstanceCount = Math.Max(1, InstanceCount);
        SampleRounds = Math.Max(1, SampleRounds);
        for (int i = 0; i < benchmarks.Length; i++)
        {
            benchmarks[i] = GetNode<TestNodeRD>($"Benchmark{i}");
            benchmarks[i].InstanceCount = InstanceCount;
            benchmarks[i].UploadMode = (TestNodeRD.SubmitMode)i;
            benchmarks[i].ProcessMode = i == 0 ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            benchmarks[i].Visible = i == 0;
        }
    }

    public override void _Ready()
    {
        results = GetNode<Label>("UI/Panel/Margin/VBox/Results");
        for (int i = 0; i < benchmarks.Length; i++)
        {
            int index = i;
            benchmarks[i].BenchmarkCompleted += (logic, fill, submit, gpu, total, count) =>
                OnSample(index, logic, fill, submit, gpu, total);
        }
        PrintStageStarted();
        UpdateResults();
    }

    private void OnSample(int index, double logic, double fill, double submit, double gpu, double total)
    {
        if (index != stage) return;
        Stats current = stats[index];
        current.Add(logic, fill, submit, gpu, total);
        GD.Print($"[基准测试][RD对比] 模式={benchmarks[index].ModeName} 轮次={current.Count}/{SampleRounds} " +
            $"CPU渲染准备={fill + submit:F4}ms GPU上传={(gpu >= 0 ? $"{gpu:F4}ms" : "不可用")}");
        UpdateResults();
        if (current.Count < SampleRounds) return;

        benchmarks[index].ProcessMode = ProcessModeEnum.Disabled;
        benchmarks[index].Visible = false;
        stage++;
        if (stage >= benchmarks.Length) { PrintSummary(); UpdateResults(); return; }
        Callable.From(StartNextStage).CallDeferred();
    }

    private void StartNextStage()
    {
        benchmarks[stage].Visible = true;
        benchmarks[stage].ProcessMode = ProcessModeEnum.Inherit;
        PrintStageStarted();
        UpdateResults();
    }

    private void PrintStageStarted() => GD.Print(
        $"[基准测试][RD对比] 开始 模式={benchmarks[stage].ModeName} 实例={InstanceCount} 测试轮数={SampleRounds}");

    private void PrintSummary()
    {
        GD.Print($"[基准测试][RD对比] 完成 实例={InstanceCount} 每轮=60帧 轮数={SampleRounds}");
        for (int i = 0; i < stats.Length; i++)
        {
            Stats value = stats[i];
            string speedup = i == 0 ? "基线" : $"{stats[0].RenderAverage / value.RenderAverage:F2}x";
            GD.Print($"[基准测试][RD对比] 模式={benchmarks[i].ModeName} 纯逻辑={value.LogicAverage:F4}ms " +
                $"填充={value.FillAverage:F4}ms 提交={value.SubmitAverage:F4}ms CPU渲染准备={value.RenderAverage:F4}ms " +
                $"GPU上传={(value.GpuAverage >= 0 ? $"{value.GpuAverage:F4}ms" : "不可用")} 相对原版加速={speedup}");
        }
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--rd-benchmark-quit") { GetTree().Quit(); break; }
        }
    }

    private void UpdateResults()
    {
        string text = (stage < benchmarks.Length ? $"正在测试：{benchmarks[stage].ModeName}" : "测试完成") +
            $"\n实例：{InstanceCount:N0}，每种模式：{SampleRounds}轮 x 60帧\n";
        for (int i = 0; i < stats.Length; i++)
        {
            Stats value = stats[i];
            if (value.Count == 0) { text += $"{benchmarks[i].ModeName}：等待测试\n"; continue; }
            string gpu = value.GpuAverage >= 0 ? $"{value.GpuAverage:F4}" : "不可用";
            text += $"{benchmarks[i].ModeName}：逻辑 {value.LogicAverage:F4} | 填充 {value.FillAverage:F4} | " +
                $"提交 {value.SubmitAverage:F4} | CPU准备 {value.RenderAverage:F4} | GPU {gpu} ms ({value.Count}/{SampleRounds})\n";
        }
        results.Text = text;
    }
}
