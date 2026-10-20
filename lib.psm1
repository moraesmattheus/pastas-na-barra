# Funções compartilhadas: extrair ícones do Windows e resolver apps pelo nome.

$global:Raiz = $PSScriptRoot
$global:PastaCache = Join-Path $Raiz 'cache'
$global:PastaIcones = Join-Path $PastaCache 'icones'

Add-Type -AssemblyName System.Drawing

if (-not ('ShellIcone' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ShellIcone {
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }
    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

    // Devolve o ícone (com transparência) de qualquer item do shell: arquivo, atalho, pasta ou shell:AppsFolder\...
    public static Bitmap Obter(string caminho, int tam) {
        Guid iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
        IShellItemImageFactory f;
        SHCreateItemFromParsingName(caminho, IntPtr.Zero, ref iid, out f);
        IntPtr hbm;
        if (f.GetImage(new SIZE(tam, tam), 0, out hbm) != 0) return null;
        try {
            using (Bitmap src = Image.FromHbitmap(hbm)) {
                Rectangle r = new Rectangle(0, 0, src.Width, src.Height);
                BitmapData d = src.LockBits(r, ImageLockMode.ReadOnly, src.PixelFormat);
                try {
                    if (src.PixelFormat != PixelFormat.Format32bppArgb && src.PixelFormat != PixelFormat.Format32bppRgb)
                        return new Bitmap(src);
                    using (Bitmap viaAlpha = new Bitmap(src.Width, src.Height, d.Stride, PixelFormat.Format32bppArgb, d.Scan0))
                        return new Bitmap(viaAlpha);
                } finally { src.UnlockBits(d); }
            }
        } finally { DeleteObject(hbm); }
    }
}
'@
}

function Get-NomeSeguro([string]$texto) {
    ($texto -replace '[^\p{L}\p{Nd}]+', '-').Trim('-').ToLower()
}

# Procura um app do Menu Iniciar pelo nome (exato primeiro, depois "contém").
function Find-AppIniciar([string]$nome, $todos) {
    $exato = $todos | Where-Object { $_.Name -ieq $nome } | Select-Object -First 1
    if ($exato) { return $exato }
    $todos | Where-Object { $_.Name -ilike "*$nome*" } | Sort-Object { $_.Name.Length } | Select-Object -First 1
}

function Save-IconePng([string]$caminhoShell, [string]$destino, [int]$tam = 96) {
    try {
        $bmp = [ShellIcone]::Obter($caminhoShell, $tam)
        if (-not $bmp) { return $false }
        $bmp.Save($destino, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        return $true
    } catch { return $false }
}

$global:ArqPastas = Join-Path $Raiz 'pastas.json'
$global:PastaAtalhos = Join-Path $Raiz 'atalhos'

function Get-Pastas {
    if (-not (Test-Path $ArqPastas)) { return @() }
    $d = Get-Content $ArqPastas -Raw -Encoding UTF8 | ConvertFrom-Json
    @($d) | ForEach-Object { $_.apps = @($_.apps); $_ }
}

function Save-Pastas($pastas) {
    $lista = @($pastas | ForEach-Object { [pscustomobject]@{ nome = $_.nome; slug = $_.slug; apps = @($_.apps) } })
    ConvertTo-Json -InputObject $lista -Depth 6 | Set-Content $ArqPastas -Encoding UTF8
}

# Cria o item de app. $app = nome do Menu Iniciar (string) ou objeto { nome, caminho, args }.
function New-ItemApp($app, $todos) {
    $nome = if ($app -is [string]) { $app } else { $app.nome }
    $argsApp = if ($app -is [string]) { '' } else { [string]$app.args }
    if ($app -isnot [string] -and $app.caminho) {
        $alvo = [string]$app.caminho; $tipo = 'arquivo'
    } else {
        if (-not $todos) { $todos = Get-StartApps }
        $achado = Find-AppIniciar $nome $todos
        if (-not $achado) { return $null }
        $nome = $achado.Name; $alvo = "shell:AppsFolder\$($achado.AppID)"; $tipo = 'shell'
    }
    New-Item -ItemType Directory -Force $PastaIcones | Out-Null
    $png = Join-Path $PastaIcones ('app-' + (Get-NomeSeguro $nome) + '-' + [Math]::Abs($alvo.GetHashCode()) + '.png')
    if (-not (Save-IconePng $alvo $png)) { $png = $null }
    [pscustomobject]@{ nome = $nome; alvo = $alvo; args = $argsApp; tipo = $tipo; icone = $png }
}

function New-IconeMosaico([string[]]$pngs, [string]$destinoIco) {
    $t = 256
    $bmp = New-Object System.Drawing.Bitmap $t, $t, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    $r = 56; $m = 8; $w = $t - 2 * $m
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, $r, $r, 180, 90); $path.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
    $path.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $path.AddArc($m, $m + $w - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 52, 56, 68))), $path)
    $cel = 56; $esp = 10; $ini = ($t - (3 * $cel + 2 * $esp)) / 2
    $validos = @($pngs | Where-Object { $_ -and (Test-Path $_) })
    for ($i = 0; $i -lt [Math]::Min(9, $validos.Count); $i++) {
        $x = $ini + ($i % 3) * ($cel + $esp); $y = $ini + [Math]::Floor($i / 3) * ($cel + $esp)
        $img = [System.Drawing.Image]::FromFile($validos[$i])
        $g.DrawImage($img, [int]$x, [int]$y, $cel, $cel); $img.Dispose()
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($destinoIco); $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
    $bw.Write($png); $bw.Close(); $fs.Close()
}

# Regenera o icone-mosaico da pasta e o atalho (que e o que se fixa na barra).
function Update-AtalhoPasta($grupo) {
    New-Item -ItemType Directory -Force $PastaIcones, $PastaAtalhos | Out-Null
    $ico = Join-Path $PastaIcones "_pasta-$($grupo.slug).ico"
    New-IconeMosaico @($grupo.apps | ForEach-Object { $_.icone }) $ico
    $lnk = Join-Path $PastaAtalhos "$($grupo.nome).lnk"
    $sh = New-Object -ComObject WScript.Shell
    $a = $sh.CreateShortcut($lnk)
    $a.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
    $a.Arguments = "//B //Nologo `"$(Join-Path $Raiz 'abrir.vbs')`" `"$($grupo.nome)`""
    $a.IconLocation = $ico
    $a.WorkingDirectory = $Raiz
    $a.Description = "Pasta $($grupo.nome)"
    $a.Save()
    $lnk
}

Export-ModuleMember -Function *
