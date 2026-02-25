# Unity 编译错误修复脚本
# 用途：清理编译缓存并强制重新编译

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Unity 编译错误修复脚本" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# 检查是否在项目根目录
if (-not (Test-Path "Assets")) {
    Write-Host "错误：请在项目根目录运行此脚本！" -ForegroundColor Red
    Write-Host "当前目录：$(Get-Location)" -ForegroundColor Yellow
    exit 1
}

Write-Host "当前目录：$(Get-Location)" -ForegroundColor Green
Write-Host ""

# 警告
Write-Host "警告：此脚本将删除以下文件夹：" -ForegroundColor Yellow
Write-Host "  - Library/" -ForegroundColor Yellow
Write-Host "  - Temp/" -ForegroundColor Yellow
Write-Host "  - obj/" -ForegroundColor Yellow
Write-Host ""
Write-Host "这将强制 Unity 重新编译所有内容。" -ForegroundColor Yellow
Write-Host "首次打开项目可能需要 5-10 分钟。" -ForegroundColor Yellow
Write-Host ""

$confirm = Read-Host "确认继续？(y/n)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "已取消。" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "开始清理..." -ForegroundColor Cyan

# 删除 Library
if (Test-Path "Library") {
    Write-Host "[1/3] 删除 Library 文件夹..." -ForegroundColor Yellow
    try {
        Remove-Item -Recurse -Force "Library" -ErrorAction Stop
        Write-Host "  ✓ Library 已删除" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ 删除 Library 失败：$($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  提示：请关闭 Unity Editor 后重试" -ForegroundColor Yellow
    }
} else {
    Write-Host "[1/3] Library 文件夹不存在，跳过" -ForegroundColor Gray
}

# 删除 Temp
if (Test-Path "Temp") {
    Write-Host "[2/3] 删除 Temp 文件夹..." -ForegroundColor Yellow
    try {
        Remove-Item -Recurse -Force "Temp" -ErrorAction Stop
        Write-Host "  ✓ Temp 已删除" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ 删除 Temp 失败：$($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "[2/3] Temp 文件夹不存在，跳过" -ForegroundColor Gray
}

# 删除 obj
if (Test-Path "obj") {
    Write-Host "[3/3] 删除 obj 文件夹..." -ForegroundColor Yellow
    try {
        Remove-Item -Recurse -Force "obj" -ErrorAction Stop
        Write-Host "  ✓ obj 已删除" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ 删除 obj 失败：$($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "[3/3] obj 文件夹不存在，跳过" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  清理完成！" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步：" -ForegroundColor Cyan
Write-Host "  1. 打开 Unity Hub" -ForegroundColor White
Write-Host "  2. 打开此项目" -ForegroundColor White
Write-Host "  3. 等待重新导入（5-10 分钟）" -ForegroundColor White
Write-Host "  4. 检查 Console 是否有错误" -ForegroundColor White
Write-Host ""
Write-Host "如果还有错误，请查看：FIX_COMPILATION_ERROR.md" -ForegroundColor Yellow
Write-Host ""

# 验证关键文件
Write-Host "验证关键文件..." -ForegroundColor Cyan
$files = @(
    "Assets/Scripts/Bootstrap/Bootstrap.asmdef",
    "Assets/Mirror/Transports/Telepathy/Telepathy/Telepathy.asmdef",
    "Assets/Mirror/Transports/Telepathy/TelepathyTransport.cs"
)

$allExist = $true
foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "  ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $file 不存在！" -ForegroundColor Red
        $allExist = $false
    }
}

if (-not $allExist) {
    Write-Host ""
    Write-Host "警告：某些关键文件缺失！" -ForegroundColor Red
    Write-Host "请确认项目完整性。" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
