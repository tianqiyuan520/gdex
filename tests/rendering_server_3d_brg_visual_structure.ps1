$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csharp = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "demo/c_sharp/TestNodeRenderingServer3DBRG.cs")
$cpp = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "src/gdexample.cpp")
$header = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "src/gdexample.h")
$shader = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "demo/c_sharp/rendering_server_3d_brg.gdshader")
$scene = Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot "demo/c_sharp/rendering_server_3d_brg.tscn")

function Assert-Contains {
	param([string]$Text, [string]$Expected, [string]$Description)
	if (-not $Text.Contains($Expected)) {
		throw "Missing: $Description (expected: $Expected)"
	}
}

Assert-Contains $csharp "public int XHalfCount" "Unity X half-count setting"
Assert-Contains $csharp "public int ZHalfCount" "Unity Z half-count setting"
Assert-Contains $csharp "public float ColorPhase;" "phase in the first RGBA texel"
Assert-Contains $cpp "std::memcpy(destination + i * 4, p_instances + i * p_stride, 4 * sizeof(float));" "16-byte native copy"
Assert-Contains $header "get_managed_previous_position_texture_3d" "previous-frame texture getter declaration"
Assert-Contains $cpp 'D_METHOD("get_managed_previous_position_texture_3d")' "previous-frame texture getter binding"
Assert-Contains $csharp '"get_managed_previous_position_texture_3d"' "C# bridge validation"
Assert-Contains $csharp 'SetShaderParameter("previous_instance_data"' "previous-frame material binding"
Assert-Contains $shader "uniform sampler2D previous_instance_data" "previous-frame shader sampler"
Assert-Contains $csharp "public bool CullTest" "culling toggle"
Assert-Contains $csharp "public float CullRadius" "culling radius"
Assert-Contains $csharp "public bool MotionVectorTest" "motion trail toggle"
Assert-Contains $csharp "public float MotionTrailStrength" "motion trail strength"
Assert-Contains $csharp "multiMesh.VisibleInstanceCount" "real visible instance limit"
Assert-Contains $csharp "WaveHeight { get; set; } = 15f" "Unity wave height"
Assert-Contains $csharp "WaveSpeed { get; set; } = 3f" "Unity wave speed"
Assert-Contains $csharp "WaveFrequency { get; set; } = 0.2f" "Unity radial frequency"
Assert-Contains $shader "(sin(phase) + 1.0) * 0.5" "Unity red formula"
Assert-Contains $shader "(cos(phase * 1.1) + 1.0) * 0.5" "Unity green formula"
Assert-Contains $shader "phase_22 = phase * 2.2" "Unity blue phase multiplier"
Assert-Contains $shader "sin(phase_22)" "Unity blue sine term"
Assert-Contains $shader "cos(phase_22)" "Unity blue cosine term"
Assert-Contains $shader "motion_vector_test" "shader motion toggle"
Assert-Contains $scene "XHalfCount = 40" "scene X half-count"
Assert-Contains $scene "ZHalfCount = 40" "scene Z half-count"
Assert-Contains $csharp "if (rd == null)" "headless RenderingDevice guard"
Assert-Contains $csharp "if (!InitializeTextureAndMaterial())" "initialization failure stops processing"

Write-Host "PASS: RenderingServer3DBRG visual structure matches the design."
