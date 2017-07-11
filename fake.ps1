$fake_folder = "$PSScriptRoot/.fake"
$fake_executable = "$fake_folder/fake.exe"

function Get-Fake {
    if (Test-Path $fake_executable) {
        return
    }

    Push-Location $PSScriptRoot
    Remove-Item -Force -Recurse "$env:TEMP/nuget/fake" -ErrorAction Ignore | Out-Null
    & nuget install -out "$env:TEMP/nuget" -excludeversion fake
    New-Item $fake_folder -Force -ItemType Directory -ErrorAction Ignore | Out-Null
    Copy-Item "$env:TEMP/nuget/fake/tools/*" $fake_folder -Recurse -Force
    Pop-Location
}

Get-Fake
& $fake_executable $args
