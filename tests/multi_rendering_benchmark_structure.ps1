$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$controllerPath = Join-Path $repoRoot "demo/benchmark/MultiRenderingBenchmark.cs"
$scenePath = Join-Path $repoRoot "demo/benchmark/multi_rendering_benchmark.tscn"
$rdCSharpPath = Join-Path $repoRoot "demo/c_sharp/TestNodeRD.cs"
$rsCSharpPath = Join-Path $repoRoot "demo/c_sharp/TestNodeNormal.cs"
$rdBridgePath = Join-Path $repoRoot "src/rd_submit_bridge.cpp"
$gdexampleHeaderPath = Join-Path $repoRoot "src/gdexample.h"
$gdexampleSourcePath = Join-Path $repoRoot "src/gdexample.cpp"
$shaderPath = Join-Path $repoRoot "demo/c_sharp/rd_instance.gdshader"

$controller = Get-Content -Raw -Encoding UTF8 $controllerPath
$scene = Get-Content -Raw -Encoding UTF8 $scenePath
$rdCSharp = Get-Content -Raw -Encoding UTF8 $rdCSharpPath
$rsCSharp = Get-Content -Raw -Encoding UTF8 $rsCSharpPath
$rdBridge = Get-Content -Raw -Encoding UTF8 $rdBridgePath
$gdexampleHeader = Get-Content -Raw -Encoding UTF8 $gdexampleHeaderPath
$gdexampleSource = Get-Content -Raw -Encoding UTF8 $gdexampleSourcePath
$shader = Get-Content -Raw -Encoding UTF8 $shaderPath

function Assert-Contains {
	param([string]$Text, [string]$Expected, [string]$Description)
	if (-not $Text.Contains($Expected)) {
		throw "Missing: $Description (expected: $Expected)"
	}
}

function Assert-NotContains {
	param([string]$Text, [string]$Unexpected, [string]$Description)
	if ($Text.Contains($Unexpected)) {
		throw "Unexpected: $Description (found: $Unexpected)"
	}
}

# 综合基准必须使用两个独立 RD 节点，避免阶段切换时复用纹理和桥接状态。
Assert-Contains $scene '[node name="RenderingDeviceCSharp"' "C# direct RD node"
Assert-Contains $scene '[node name="RenderingDeviceGDExtension"' "GDExtension RD node"
Assert-Contains $scene '[node name="RenderingServerCSharp"' "C# RenderingServer node"
Assert-Contains $scene '[node name="RenderingServerGDExtension"' "GDExtension RenderingServer node"
Assert-Contains $controller 'SubmitMode.CSharpOriginal' "C# direct submit mode"
Assert-Contains $controller 'SubmitMode.GDExtensionSingleBuffer' "GDExtension single-buffer mode"
Assert-Contains $controller '"RenderingServer + C# direct (8MB)"' "C# RenderingServer benchmark label"
Assert-Contains $controller '"RenderingServer + GDExtension (8MB)"' "GDExtension RenderingServer benchmark label"

# 公平对比只保留 8 字节/实例的位置纹理路径，不再混入 32 字节/实例的 MultiMesh 路径。
Assert-NotContains $scene 'RenderingServerMultiMesh' "32MB RenderingServer MultiMesh node"
Assert-NotContains $controller '32MB' "32MB benchmark label"

# Stable ASCII tags let Windows PowerShell 5 validate that detailed Chinese explanations exist nearby.
Assert-Contains $rdCSharp '[8-byte-per-instance]' "C# upload layout explanation"
Assert-Contains $rdCSharp '[managed/native-boundary]' "managed/native boundary explanation"
Assert-Contains $rdBridge '[timing-boundary]' "native timing boundary explanation"
Assert-Contains $shader '[INSTANCE_ID-mapping]' "shader INSTANCE_ID mapping explanation"

# The high-level RenderingServer path must create and update its compact texture without borrowing RD.
Assert-Contains $rsCSharp '[csharp-compact-texture]' "C# compact RenderingServer branch"
Assert-Contains $rsCSharp 'positionImage.SetData' "C# Image.SetData upload"
Assert-Contains $rsCSharp 'positionTexture.Update' "C# ImageTexture.Update upload"
Assert-NotContains $rsCSharp 'RenderingServer.GetRenderingDevice' "RenderingDevice access in RenderingServer benchmark"

# Final output must distinguish the actual data path and print all four pairwise comparisons.
Assert-Contains $controller 'RenderingDevice.TextureUpdate' "C# RD submission path label"
Assert-Contains $controller 'Image.SetData -> ImageTexture.Update' "C# RS submission path label"
Assert-Contains $controller 'RenderingDevice::texture_update' "GDExtension RD submission path label"
Assert-Contains $controller 'Image::set_data -> ImageTexture::update' "GDExtension RS submission path label"
Assert-Contains $controller 'PrintComparison("C# submit path: RD vs RS", 0, 1)' "C# RD/RS comparison"
Assert-Contains $controller 'PrintComparison("GDExtension submit path: RD vs RS", 2, 3)' "GDExtension RD/RS comparison"
Assert-Contains $controller 'PrintComparison("RD language boundary: C# vs GDExtension", 0, 2)' "RD language comparison"
Assert-Contains $controller 'PrintComparison("RS language boundary: C# vs GDExtension", 1, 3)' "RS language comparison"

# The fifth stage is a fully native C++ logic + 8MB RenderingServer texture benchmark.
Assert-Contains $scene '[node name="RenderingServerCpp" type="GDExample"' "pure C++ RenderingServer node"
Assert-Contains $scene 'CompactTextureBenchmark = true' "pure C++ compact texture mode"
Assert-Contains $controller '"RenderingServer + pure C++ (8MB)"' "pure C++ benchmark label"
Assert-Contains $controller 'PrintComparison("RS full native: C# vs pure C++", 1, 4)' "C# vs pure C++ comparison"
Assert-Contains $controller 'PrintComparison("RS native submit vs full native C++", 3, 4)' "GDExtension vs pure C++ comparison"
Assert-Contains $gdexampleHeader 'CompactTextureBenchmark' "C++ compact benchmark property"
Assert-Contains $gdexampleSource 'submit_compact_texture_benchmark' "pure C++ compact hot path"

Write-Host "PASS: MultiRenderingBenchmark structure matches the design."
