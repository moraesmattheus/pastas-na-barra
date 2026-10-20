param([Parameter(Mandatory)][string]$Grupo)
# Painel da pasta: grade de apps acima da barra de tarefas. Aqui mesmo se edita a pasta:
# arrastar app para dentro = adicionar; botao direito no app = remover/mover; "+" = escolher da lista;
# botao direito no titulo = nova pasta / excluir pasta.
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

$raiz = $PSScriptRoot
$cache = Join-Path $raiz 'cache'
New-Item -ItemType Directory -Force $cache | Out-Null
$marca = Join-Path $cache ('fechou-' + ($Grupo -replace '[^\w]', '_') + '.txt')

# Clicar de novo no icone da pasta com o painel aberto = fechar (o painel ja fechou ao perder o foco).
if (Test-Path $marca) {
    $fechou = [datetime]::FromFileTimeUtc([int64](Get-Content $marca -Raw))
    $inicio = (Get-Process -Id $PID).StartTime.ToUniversalTime()
    if (($inicio - $fechou).TotalMilliseconds -lt 900) { Remove-Item $marca -Force; exit }
}

$arq = Join-Path $raiz 'pastas.json'
$dados = Get-Content $arq -Raw -Encoding UTF8 | ConvertFrom-Json
$script:pastas = @($dados)
foreach ($p in $script:pastas) { $p.apps = @($p.apps) }
$script:g = $script:pastas | Where-Object { $_.nome -eq $Grupo } | Select-Object -First 1
if (-not $script:g) { exit 1 }

$script:ativou = $false
$script:ocupado = $false
$script:todosApps = $null
$conv = [System.Windows.Media.BrushConverter]::new()
$hover = $conv.ConvertFromString('#33FFFFFF')
$branco = [System.Windows.Media.Brushes]::White
$cinza = $conv.ConvertFromString('#99FFFFFF')

function Carregar-Lib {
    if (-not (Get-Module lib)) { Import-Module (Join-Path $raiz 'lib.psm1') -Global -Force }
}
function Salvar {
    Carregar-Lib
    Save-Pastas $script:pastas
    Update-AtalhoPasta $script:g | Out-Null
}
function Em-Dialogo([scriptblock]$bloco) {
    $script:ocupado = $true
    try { & $bloco } finally { $script:ocupado = $false; $janela.Activate() | Out-Null }
}

function Pedir-Texto([string]$titulo) {
    $w = New-Object System.Windows.Window
    $w.Title = $titulo; $w.Width = 320; $w.SizeToContent = 'Height'; $w.Owner = $janela
    $w.WindowStartupLocation = 'CenterScreen'; $w.ResizeMode = 'NoResize'; $w.Topmost = $true
    $pl = New-Object System.Windows.Controls.StackPanel; $pl.Margin = 14
    $tb = New-Object System.Windows.Controls.TextBox; $tb.FontSize = 14; $tb.Padding = 6
    $bt = New-Object System.Windows.Controls.Button; $bt.Content = 'OK'; $bt.Margin = '0,10,0,0'; $bt.Padding = 6; $bt.IsDefault = $true
    $pl.Children.Add($tb) | Out-Null; $pl.Children.Add($bt) | Out-Null; $w.Content = $pl
    $script:resp = $null
    $bt.Add_Click({ $script:resp = $tb.Text.Trim(); $w.Close() })
    $w.Add_KeyDown({ param($s, $e) if ($e.Key -eq 'Escape') { $w.Close() } })
    $w.Add_ContentRendered({ $tb.Focus() | Out-Null })
    $w.ShowDialog() | Out-Null
    $script:resp
}

function Escolher-App {
    Carregar-Lib
    if (-not $script:todosApps) { $script:todosApps = @(Get-StartApps | Sort-Object Name) }
    $w = New-Object System.Windows.Window
    $w.Title = 'Adicionar app'; $w.Width = 340; $w.Height = 460; $w.Owner = $janela
    $w.WindowStartupLocation = 'CenterScreen'; $w.Topmost = $true
    $g2 = New-Object System.Windows.Controls.Grid
    foreach ($h in 'Auto', '*', 'Auto') {
        $rd = New-Object System.Windows.Controls.RowDefinition
        if ($h -eq '*') { $rd.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }
        else { $rd.Height = [System.Windows.GridLength]::Auto }
        $g2.RowDefinitions.Add($rd)
    }
    $tb = New-Object System.Windows.Controls.TextBox; $tb.Margin = 10; $tb.Padding = 6; $tb.FontSize = 14
    $lb = New-Object System.Windows.Controls.ListBox; $lb.Margin = '10,0,10,0'; $lb.FontSize = 13
    $bt = New-Object System.Windows.Controls.Button; $bt.Content = 'Adicionar'; $bt.Margin = 10; $bt.Padding = 6; $bt.IsDefault = $true
    [System.Windows.Controls.Grid]::SetRow($lb, 1); [System.Windows.Controls.Grid]::SetRow($bt, 2)
    $g2.Children.Add($tb) | Out-Null; $g2.Children.Add($lb) | Out-Null; $g2.Children.Add($bt) | Out-Null
    $w.Content = $g2
    $encher = {
        $lb.Items.Clear()
        foreach ($a in $script:todosApps) { if (-not $tb.Text -or $a.Name -ilike ('*' + $tb.Text + '*')) { $lb.Items.Add($a.Name) | Out-Null } }
        if ($lb.Items.Count) { $lb.SelectedIndex = 0 }
    }
    & $encher
    $tb.Add_TextChanged($encher)
    $script:resp = $null
    $ok = { if ($lb.SelectedItem) { $script:resp = [string]$lb.SelectedItem; $w.Close() } }
    $bt.Add_Click($ok); $lb.Add_MouseDoubleClick($ok)
    $w.Add_KeyDown({ param($s, $e)
        if ($e.Key -eq 'Escape') { $w.Close() }
        elseif ($e.Key -eq 'Down' -and $lb.SelectedIndex -lt $lb.Items.Count - 1) { $lb.SelectedIndex++ }
        elseif ($e.Key -eq 'Up' -and $lb.SelectedIndex -gt 0) { $lb.SelectedIndex-- } })
    $w.Add_ContentRendered({ $tb.Focus() | Out-Null })
    $w.ShowDialog() | Out-Null
    $script:resp
}

function Adicionar-App($app) {
    Carregar-Lib
    $item = New-ItemApp $app $script:todosApps
    if (-not $item) { return }
    if (@($script:g.apps | Where-Object { $_.alvo -eq $item.alvo }).Count) { return }
    $script:g.apps = @($script:g.apps) + $item
    Salvar; Montar
}

function Mover-App($app, [int]$delta) {
    $lista = @($script:g.apps)
    $i = [array]::IndexOf($lista, $app); $j = $i + $delta
    if ($i -lt 0 -or $j -lt 0 -or $j -ge $lista.Count) { return }
    $lista[$i], $lista[$j] = $lista[$j], $lista[$i]
    $script:g.apps = $lista
    Salvar; Montar
}

function Remover-App($app) {
    $script:g.apps = @($script:g.apps | Where-Object { $_ -ne $app })
    Salvar; Montar
}

function Nova-Pasta {
    Em-Dialogo { $script:novo = Pedir-Texto 'Nome da nova pasta' }
    $nome = $script:novo
    if (-not $nome -or $nome -match '[\\/:*?"<>|]') { return }
    if (@($script:pastas | Where-Object { $_.nome -ieq $nome }).Count) { return }
    Carregar-Lib
    $nova = [pscustomobject]@{ nome = $nome; slug = (Get-NomeSeguro $nome); apps = @() }
    $script:pastas = @($script:pastas) + $nova
    $script:g = $nova
    Save-Pastas $script:pastas
    $lnk = Update-AtalhoPasta $nova
    Start-Process explorer.exe "/select,`"$lnk`""
    [System.Windows.MessageBox]::Show("Pasta '$nome' criada.`n`nArraste o atalho que abriu no Explorador para a barra de tarefas (ou clique direito > Fixar na barra de tarefas).", 'Nova pasta') | Out-Null
    $janela.Close()
}

function Excluir-Pasta {
    $r = [System.Windows.MessageBox]::Show("Excluir a pasta '$($script:g.nome)'?`nOs apps em si nao sao apagados. Depois, desafixe o icone dela na barra.", 'Excluir pasta', 'YesNo')
    if ($r -ne 'Yes') { return }
    $nome = $script:g.nome
    $script:pastas = @($script:pastas | Where-Object { $_.nome -ne $nome })
    Carregar-Lib
    Save-Pastas $script:pastas
    Remove-Item (Join-Path $raiz "atalhos\$nome.lnk") -Force -ErrorAction SilentlyContinue
    $janela.Close()
}

# ---------- janela ----------
$janela = New-Object System.Windows.Window
$janela.WindowStyle = 'None'; $janela.AllowsTransparency = $true; $janela.Background = [System.Windows.Media.Brushes]::Transparent
$janela.ShowInTaskbar = $false; $janela.Topmost = $true; $janela.SizeToContent = 'WidthAndHeight'
$janela.ResizeMode = 'NoResize'; $janela.UseLayoutRounding = $true; $janela.AllowDrop = $true

$moldura = New-Object System.Windows.Controls.Border
$moldura.CornerRadius = 18; $moldura.Padding = 16; $moldura.Margin = 10
$moldura.Background = $conv.ConvertFromString('#EE1F2230')
$moldura.BorderBrush = $conv.ConvertFromString('#33FFFFFF'); $moldura.BorderThickness = 1
$sombra = New-Object System.Windows.Media.Effects.DropShadowEffect
$sombra.BlurRadius = 24; $sombra.ShadowDepth = 4; $sombra.Opacity = 0.5
$moldura.Effect = $sombra

$pilha = New-Object System.Windows.Controls.StackPanel
$titulo = New-Object System.Windows.Controls.TextBlock
$titulo.Foreground = $branco; $titulo.FontSize = 15; $titulo.FontWeight = 'SemiBold'
$titulo.HorizontalAlignment = 'Center'; $titulo.Margin = '0,0,0,12'; $titulo.Background = [System.Windows.Media.Brushes]::Transparent
$menuTitulo = New-Object System.Windows.Controls.ContextMenu
$miNova = New-Object System.Windows.Controls.MenuItem; $miNova.Header = 'Nova pasta...'; $miNova.Add_Click({ Nova-Pasta })
$miExcl = New-Object System.Windows.Controls.MenuItem; $miExcl.Header = 'Excluir esta pasta'; $miExcl.Add_Click({ Excluir-Pasta })
$menuTitulo.Items.Add($miNova) | Out-Null; $menuTitulo.Items.Add($miExcl) | Out-Null
$menuTitulo.Add_Opened({ $script:ocupado = $true }); $menuTitulo.Add_Closed({ $script:ocupado = $false })
$titulo.ContextMenu = $menuTitulo

$grade = New-Object System.Windows.Controls.Primitives.UniformGrid
$dica = New-Object System.Windows.Controls.TextBlock
$dica.Text = 'Arraste apps para c' + [char]0xE1 + ' ' + [char]0xB7 + ' bot' + [char]0xE3 + 'o direito para editar'
$dica.Foreground = $cinza; $dica.FontSize = 10.5; $dica.HorizontalAlignment = 'Center'; $dica.Margin = '0,10,0,0'
$pilha.Children.Add($titulo) | Out-Null; $pilha.Children.Add($grade) | Out-Null; $pilha.Children.Add($dica) | Out-Null
$moldura.Child = $pilha; $janela.Content = $moldura

function Nova-Celula($texto, $img) {
    $celula = New-Object System.Windows.Controls.Border
    $celula.Width = 92; $celula.Padding = '4,8,4,6'; $celula.CornerRadius = 12
    $celula.Background = [System.Windows.Media.Brushes]::Transparent; $celula.Cursor = 'Hand'
    $col = New-Object System.Windows.Controls.StackPanel
    $col.Children.Add($img) | Out-Null
    $nome = New-Object System.Windows.Controls.TextBlock
    $nome.Text = $texto; $nome.Foreground = $branco; $nome.FontSize = 11.5
    $nome.TextAlignment = 'Center'; $nome.TextWrapping = 'Wrap'; $nome.TextTrimming = 'CharacterEllipsis'
    $nome.MaxHeight = 32; $nome.Margin = '0,6,0,0'
    $col.Children.Add($nome) | Out-Null; $celula.Child = $col
    $celula.Add_MouseEnter({ param($s, $e) $s.Background = $hover })
    $celula.Add_MouseLeave({ param($s, $e) $s.Background = [System.Windows.Media.Brushes]::Transparent })
    $celula
}

function Montar {
    $titulo.Text = $script:g.nome
    $grade.Children.Clear()
    $apps = @($script:g.apps)
    $grade.Columns = [Math]::Min(5, [Math]::Max(2, $apps.Count + 1))
    foreach ($app in $apps) {
        $img = New-Object System.Windows.Controls.Image
        $img.Width = 48; $img.Height = 48; $img.HorizontalAlignment = 'Center'
        [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($img, 'HighQuality')
        if ($app.icone -and (Test-Path $app.icone)) {
            $bi = New-Object System.Windows.Media.Imaging.BitmapImage
            $bi.BeginInit(); $bi.CacheOption = 'OnLoad'; $bi.UriSource = [Uri]$app.icone; $bi.EndInit()
            $img.Source = $bi
        }
        $celula = Nova-Celula $app.nome $img
        $celula.Tag = $app

        $cm = New-Object System.Windows.Controls.ContextMenu
        $mi1 = New-Object System.Windows.Controls.MenuItem; $mi1.Header = 'Mover para a esquerda'; $mi1.Tag = $app
        $mi1.Add_Click({ param($s, $e) Mover-App $s.Tag -1 })
        $mi2 = New-Object System.Windows.Controls.MenuItem; $mi2.Header = 'Mover para a direita'; $mi2.Tag = $app
        $mi2.Add_Click({ param($s, $e) Mover-App $s.Tag 1 })
        $mi3 = New-Object System.Windows.Controls.MenuItem; $mi3.Header = 'Remover da pasta'; $mi3.Tag = $app
        $mi3.Add_Click({ param($s, $e) Remover-App $s.Tag })
        foreach ($m in $mi1, $mi2, $mi3) { $cm.Items.Add($m) | Out-Null }
        $cm.Add_Opened({ $script:ocupado = $true }); $cm.Add_Closed({ $script:ocupado = $false })
        $celula.ContextMenu = $cm

        $celula.Add_MouseLeftButtonUp({
            param($s, $e)
            $a = $s.Tag
            try {
                if ($a.args) { Start-Process -FilePath $a.alvo -ArgumentList $a.args }
                elseif ($a.tipo -eq 'shell') { Start-Process explorer.exe $a.alvo }
                else { Start-Process -FilePath $a.alvo }
            } catch { }
            $janela.Close()
        })
        $grade.Children.Add($celula) | Out-Null
    }
    # botao "+"
    $mais = New-Object System.Windows.Controls.TextBlock
    $mais.Text = '+'; $mais.Foreground = $cinza; $mais.FontSize = 34; $mais.HorizontalAlignment = 'Center'
    $mais.Height = 48; $mais.LineHeight = 44; $mais.LineStackingStrategy = 'BlockLineHeight'
    $cel = Nova-Celula 'Adicionar' $mais
    $cel.Add_MouseLeftButtonUp({
        Em-Dialogo { $script:escolhido = Escolher-App }
        if ($script:escolhido) { Adicionar-App $script:escolhido }
    })
    $grade.Children.Add($cel) | Out-Null
    $janela.UpdateLayout()
    Reposicionar
}

# Posiciona acima da barra de tarefas, centralizado no ponto onde o mouse clicou.
$cursor = [System.Windows.Forms.Cursor]::Position
$tela = [System.Windows.Forms.Screen]::FromPoint($cursor)
$escala = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width / [System.Windows.SystemParameters]::PrimaryScreenWidth
function Reposicionar {
    if (-not $janela.IsLoaded) { return }
    $w = $janela.ActualWidth; $h = $janela.ActualHeight
    $area = $tela.WorkingArea
    $x = ($cursor.X / $escala) - $w / 2
    $janela.Left = [Math]::Max($area.Left / $escala, [Math]::Min($area.Right / $escala - $w, $x))
    $janela.Top = $area.Bottom / $escala - $h
}

$janela.Add_Drop({
    param($s, $e)
    $arquivos = $e.Data.GetData([System.Windows.DataFormats]::FileDrop)
    foreach ($f in @($arquivos)) {
        $nome = [System.IO.Path]::GetFileNameWithoutExtension($f)
        Adicionar-App ([pscustomobject]@{ nome = $nome; caminho = $f; args = '' })
    }
})
$janela.Add_ContentRendered({ Reposicionar; $janela.Activate() | Out-Null })
$janela.Add_SizeChanged({ Reposicionar })
$janela.Add_Activated({ $script:ativou = $true })
$janela.Add_Deactivated({ if ($script:ativou -and -not $script:ocupado) { $janela.Close() } })
$janela.Add_KeyDown({ param($s, $e) if ($e.Key -eq 'Escape') { $janela.Close() } })
$janela.Add_Closed({ Set-Content $marca ([datetime]::UtcNow.ToFileTimeUtc()) })

Montar
$janela.Left = -10000; $janela.Top = -10000
$janela.ShowDialog() | Out-Null
