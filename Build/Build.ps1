
function EnsureDownloaded([string]$sourceURL, [string]$destPath) {
	if (!(Test-Path $destPath)) {
		$fileName = Split-Path $destPath -Leaf
		Write-Verbose -Message ("Downloading " + $fileName + "...")
		try {
			(New-Object System.Net.WebClient).DownloadFile($sourceURL, $destPath)
		} catch {
			Throw "Could not download " + $fileName + "."
		}
	}
}

if(!$PSScriptRoot){
    $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
}

$NUGET_EXE = Join-Path $PSScriptRoot "nuget.exe"
$NUGET_URL = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"

if (!(Test-Path $NUGET_EXE)) {
    Write-Verbose -Message "Trying to find nuget.exe in PATH..."
    $existingPaths = $Env:Path -Split ';' | Where-Object { (![string]::IsNullOrEmpty($_)) -and (Test-Path $_) }
    $NUGET_EXE_IN_PATH = Get-ChildItem -Path $existingPaths -Filter "nuget.exe" | Select -First 1
    if ($NUGET_EXE_IN_PATH -ne $null -and (Test-Path $NUGET_EXE_IN_PATH.FullName)) {
        Write-Verbose -Message "Found in PATH at $($NUGET_EXE_IN_PATH.FullName)."
        $NUGET_EXE = $NUGET_EXE_IN_PATH.FullName
    }
}

# Try download NuGet.exe if it doesn't exist
EnsureDownloaded $NUGET_URL $NUGET_EXE

& $NUGET_EXE pack ..\Proliferate\Proliferate.csproj -verbosity detailed