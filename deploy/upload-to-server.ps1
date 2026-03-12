# 上传项目到服务器
# 使用方法：./upload-to-server.ps1

param(
    [string]$ServerIP = "121.43.141.248",
    [string]$ServerUser = "root",
    [string]$TargetDir = "/root/Morphis"
)

Write-Host "=== Morphis Project Upload ===" -ForegroundColor Cyan
Write-Host "Server: $ServerUser@$ServerIP" -ForegroundColor Yellow
Write-Host "Target: $TargetDir" -ForegroundColor Yellow
Write-Host ""

# 检查是否安装了 scp
$scpExists = Get-Command scp -ErrorAction SilentlyContinue
if (-not $scpExists) {
    Write-Host "[ERROR] scp command not found!" -ForegroundColor Red
    Write-Host "Please install OpenSSH Client:" -ForegroundColor Yellow
    Write-Host "  Settings -> Apps -> Optional Features -> Add OpenSSH Client" -ForegroundColor White
    exit 1
}

Write-Host "[1/4] Creating archive..." -ForegroundColor Green

# 创建临时目录
$tempDir = Join-Path $env:TEMP "morphis-upload"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# 需要上传的文件和目录
$itemsToUpload = @(
    "Backend",
    "deploy",
    "config.json.example",
    "README.md",
    "QUICK_START.md",
    "DEPLOY_STEPS.md",
    "ARCHITECTURE_VERIFICATION.md"
)

Write-Host "Copying files..." -ForegroundColor Gray
foreach ($item in $itemsToUpload) {
    if (Test-Path $item) {
        Copy-Item $item -Destination $tempDir -Recurse -Force
        Write-Host "  + $item" -ForegroundColor Gray
    }
}

# 创建压缩包
$archivePath = Join-Path $env:TEMP "morphis-backend.zip"
if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

Write-Host "Creating archive: $archivePath" -ForegroundColor Gray
Compress-Archive -Path "$tempDir\*" -DestinationPath $archivePath -Force

Write-Host ""
Write-Host "[2/4] Uploading to server..." -ForegroundColor Green
Write-Host "This may take a few minutes..." -ForegroundColor Yellow

# 上传压缩包
scp $archivePath "${ServerUser}@${ServerIP}:/tmp/morphis-backend.zip"

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Upload failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[3/4] Extracting on server..." -ForegroundColor Green

# 在服务器上解压
$sshCommands = @"
mkdir -p $TargetDir
cd $TargetDir
unzip -o /tmp/morphis-backend.zip
rm /tmp/morphis-backend.zip
chmod +x deploy/*.sh
echo 'Extraction complete'
"@

ssh "${ServerUser}@${ServerIP}" $sshCommands

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Extraction failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/4] Cleaning up..." -ForegroundColor Green
Remove-Item $archivePath -Force
Remove-Item $tempDir -Recurse -Force

Write-Host ""
Write-Host "=== Upload Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Upload Unity Server build:" -ForegroundColor White
Write-Host "   scp -r build.app/MorphisServer ${ServerUser}@${ServerIP}:/root/" -ForegroundColor Gray
Write-Host ""
Write-Host "2. SSH to server and run deployment:" -ForegroundColor White
Write-Host "   ssh ${ServerUser}@${ServerIP}" -ForegroundColor Gray
Write-Host "   cd $TargetDir/deploy" -ForegroundColor Gray
Write-Host "   ./setup-server.sh" -ForegroundColor Gray
Write-Host ""
