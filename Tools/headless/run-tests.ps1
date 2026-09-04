# Unity 에디터를 열지 않고 EditMode 테스트를 돌린다.
#
#   리포 루트에서:  Tools\headless\run-tests.cmd
#
# 러너(.NET 콘솔 앱)를 필요할 때만 빌드하고, 그 다음 소스에서 직접 컴파일한
# 어셈블리를 NUnit 으로 실행한다. Library/ScriptAssemblies 는 쳐다보지 않는다.
#
# 종료 코드: 0 전부 통과 / 1 테스트 실패 / 2 컴파일 실패 / 3 러너 오류

[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RunnerArgs
)

$ErrorActionPreference = 'Stop'

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$toolRoot   = $PSScriptRoot
$project    = Join-Path $toolRoot 'src\Runner\Runner.csproj'
$config     = Join-Path $toolRoot 'headless.config.json'
$buildRoot  = Join-Path $toolRoot '.build\runner'
$runnerDll  = Join-Path $buildRoot 'bin\Runner\release\OJ.Headless.Runner.dll'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'dotnet SDK 를 못 찾았다. https://dotnet.microsoft.com 에서 설치할 것.'
    exit 3
}

# 러너 소스가 바뀌었을 때만 다시 빌드한다. 매 실행마다 MSBuild 를 태우면 왕복이 느려진다.
$needsBuild = $true
if (Test-Path $runnerDll) {
    $builtAt = (Get-Item $runnerDll).LastWriteTimeUtc
    $newest = Get-ChildItem -Path (Join-Path $toolRoot 'src') -Recurse -File |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($newest -and $newest.LastWriteTimeUtc -le $builtAt) { $needsBuild = $false }
}

if ($needsBuild) {
    Write-Host '[러너] 빌드 중...' -ForegroundColor DarkGray
    & dotnet build $project -c Release --artifacts-path $buildRoot --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error '러너 빌드에 실패했다.'
        exit 3
    }
}

if (-not (Test-Path $runnerDll)) {
    Write-Error "러너 산출물이 예상 경로에 없다: $runnerDll"
    exit 3
}

$arguments = @($runnerDll, '--config', $config)
if ($RunnerArgs) { $arguments += $RunnerArgs }

& dotnet @arguments
exit $LASTEXITCODE
