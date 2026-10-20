# Compila o app (C#) em dist\PastasBarra.exe. Nao precisa instalar nada: usa o compilador do Visual Studio Build Tools
# (ou o do .NET Framework, se for o caso).
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$dist = Join-Path $raiz 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

$csc = Get-ChildItem "C:\Program Files*\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $csc) { $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" }

$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$refs = @(
    'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'System.Web.Extensions.dll', 'System.Xaml.dll', 'Microsoft.CSharp.dll',
    'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll', 'WPF\WindowsBase.dll'
) | ForEach-Object { "/r:$fw\$_" }

# icone do programa (4 circulos coloridos), gerado uma vez
$ico = Join-Path $raiz 'app\app.ico'
if (-not (Test-Path $ico)) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 256, 256
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'
    $cores = '#7DD3FC', '#A78BFA', '#FB923C', '#4ADE80'
    for ($i = 0; $i -lt 4; $i++) {
        $b = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($cores[$i]))
        $g.FillEllipse($b, 16 + ($i % 2) * 116, 16 + [Math]::Floor($i / 2) * 116, 108, 108)
    }
    $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($ico); $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$png.Length); $w.Write([uint32]22); $w.Write($png); $w.Close()
}
$fontes = Get-ChildItem (Join-Path $raiz 'app') -Filter *.cs | ForEach-Object FullName
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /nowarn:1701,1702 /langversion:7.3 /main:PastasBarra.Inicio "/win32icon:$ico" "/win32manifest:$raiz\app\app.manifest" "/out:$dist\PastasBarra.exe" @refs @fontes
if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao" }
$hash = (Get-FileHash "$dist\PastasBarra.exe" -Algorithm SHA256).Hash
Set-Content "$dist\PastasBarra.exe.sha256" "$hash  PastasBarra.exe"
Write-Host "SHA256: $hash"
Write-Host "OK: $dist\PastasBarra.exe"
