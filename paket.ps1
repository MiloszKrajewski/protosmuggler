$paket_folder = "$PSScriptRoot/.paket"
$paket_executable = "$paket_folder/paket.exe"
$paket_version = "4.8.8"

function Get-Paket {
    if (Test-Path $paket_executable) {
        return
    }

    Push-Location $PSScriptRoot
    Remove-Item -Force -Recurse "$env:TEMP/nuget/paket.bootstrapper" -ErrorAction Ignore | Out-Null
    & nuget install -out "$env:TEMP/nuget" -excludeversion paket.bootstrapper
    New-Item $paket_folder -Force -ItemType Directory -ErrorAction Ignore | Out-Null
    Copy-Item "$env:TEMP/nuget/paket.bootstrapper/tools/*" $paket_folder -Recurse -Force
    Set-Location $paket_folder
    & ./paket.bootstrapper.exe --self
    & ./paket.bootstrapper.exe $paket_version
    Pop-Location
}

Get-Paket
& $paket_executable $args
