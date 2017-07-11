Param(
    [string] $root = "."
)

$protoc_folder = "$PSScriptRoot/.tools/protoc"
$protoc_executable = "$protoc_folder/bin/protoc.exe"
$protoc_includes = "$protoc_folder/include"
$protoc_version = "3.2.0"
$protoc_url = "https://github.com/google/protobuf/releases/download/v$protoc_version/protoc-$protoc_version-win32.zip"

function Unzip([string] $zipfile, [string] $outpath) {
	Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}

function Exec([scriptblock] $script) {
    Invoke-Command $script
    if ($LASTEXITCODE -ne 0) {
        throw "Invoking script failed with error code $LASTEXITCODE"
    }
}

function Get-ProtoC() {
    if (Test-Path $protoc_executable) {
        return
    }
    $zip = "${env:TEMP}/protoc.zip"
    Write-Host -ForegroundColor Gray "Downloading '$protoc_url'..."
    Invoke-WebRequest "$protoc_url" -OutFile "$zip"
    Unzip "$zip" $protoc_folder
    Remove-Item "$zip"
}

function Test-Parent([string] $parent, [string] $child) {
    $parent = (Get-Item $parent).FullName.ToLower()
    $child = (Get-Item $child).FullName.ToLower()
    return $child.StartsWith($parent)
}

function Invoke-ProtoC([string] $root, [switch] $tree = $false) {
    if ($tree -eq $true) {
        Invoke-ProtoC "$root" $false
        Get-ChildItem -Path "$root" -Directory -Recurse | ForEach-Object {
            Invoke-ProtoC $_.FullName $false
        }
    } else {
        $folder = (Get-Item $root).FullName
        if (Test-Parent $protoc_includes $folder) {
            Write-Host -ForegroundColor DarkGray "Skipping '$folder'..."
            return
        }
        $protos = Get-ChildItem -File "$folder/*.proto"
        $count = $protos.Count
        if ($count -gt 0) {
            Write-Host -ForegroundColor Cyan "Processing '$folder' ($count files)..."
            Exec { & "$protoc_executable" "--csharp_out=$folder" "-I$protoc_includes" "-I$folder" $protos }
        }
    }
}

Get-ProtoC
Invoke-ProtoC -root $root -tree
