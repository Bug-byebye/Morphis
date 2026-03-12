# Verify Assembly Definition Files
Write-Host "=== Verifying Assembly Definitions ===" -ForegroundColor Cyan

# Check AppFlow.asmdef
$appFlowAsmdef = "Assets/Scripts/AppFlow/AppFlow.asmdef"
if (Test-Path $appFlowAsmdef) {
    Write-Host "[OK] AppFlow.asmdef exists" -ForegroundColor Green
    $content = Get-Content $appFlowAsmdef -Raw | ConvertFrom-Json
    Write-Host "  Name: $($content.name)" -ForegroundColor Gray
    Write-Host "  References: $($content.references -join ', ')" -ForegroundColor Gray
} else {
    Write-Host "[ERROR] AppFlow.asmdef not found" -ForegroundColor Red
}

# Check Bootstrap.asmdef
$bootstrapAsmdef = "Assets/Scripts/Bootstrap/Bootstrap.asmdef"
if (Test-Path $bootstrapAsmdef) {
    Write-Host "[OK] Bootstrap.asmdef exists" -ForegroundColor Green
    $content = Get-Content $bootstrapAsmdef -Raw | ConvertFrom-Json
    Write-Host "  Name: $($content.name)" -ForegroundColor Gray
    Write-Host "  References: $($content.references -join ', ')" -ForegroundColor Gray
    
    if ($content.references -contains "Morphis.AppFlow") {
        Write-Host "  [OK] Contains Morphis.AppFlow reference" -ForegroundColor Green
    } else {
        Write-Host "  [ERROR] Missing Morphis.AppFlow reference" -ForegroundColor Red
    }
} else {
    Write-Host "[ERROR] Bootstrap.asmdef not found" -ForegroundColor Red
}

# Check Config.asmdef
$configAsmdef = "Assets/Scripts/Config/Config.asmdef"
if (Test-Path $configAsmdef) {
    Write-Host "[OK] Config.asmdef exists" -ForegroundColor Green
    $content = Get-Content $configAsmdef -Raw | ConvertFrom-Json
    Write-Host "  Name: $($content.name)" -ForegroundColor Gray
} else {
    Write-Host "[ERROR] Config.asmdef not found" -ForegroundColor Red
}

Write-Host "`n=== Assembly Dependencies ===" -ForegroundColor Cyan
Write-Host "Assembly-CSharp (default)" -ForegroundColor Yellow
Write-Host "  Can reference all custom assemblies" -ForegroundColor Gray
Write-Host ""
Write-Host "Morphis.Bootstrap" -ForegroundColor Yellow
Write-Host "  -> Morphis.Config" -ForegroundColor Gray
Write-Host "  -> Morphis.AppFlow" -ForegroundColor Gray
Write-Host "  -> Mirror" -ForegroundColor Gray
Write-Host "  -> Mirror.Transports" -ForegroundColor Gray
Write-Host ""
Write-Host "Morphis.AppFlow" -ForegroundColor Yellow
Write-Host "  -> Morphis.Config" -ForegroundColor Gray
Write-Host "  -> Unity.TextMeshPro" -ForegroundColor Gray
Write-Host "  -> Mirror" -ForegroundColor Gray
Write-Host "  -> Unity.InputSystem" -ForegroundColor Gray
Write-Host ""
Write-Host "Morphis.Config" -ForegroundColor Yellow
Write-Host "  (no external references)" -ForegroundColor Gray

Write-Host "`n=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Open Unity Editor" -ForegroundColor White
Write-Host "2. Wait for assembly recompilation" -ForegroundColor White
Write-Host "3. Check Console for compilation errors" -ForegroundColor White
