# Primeira vez: cria pastas.json a partir do config.json (modelo inicial) e gera os atalhos em .\atalhos.
# Depois disso, as pastas se editam direto no painel (na barra de tarefas). Para recriar tudo do zero: -Recriar
param([switch]$Recriar)
Import-Module "$PSScriptRoot\lib.psm1" -Force

if ($Recriar -or -not (Test-Path $ArqPastas)) {
    $config = Get-Content (Join-Path $Raiz 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $todos = Get-StartApps
    $pastas = foreach ($g in $config.grupos) {
        $itens = foreach ($app in $g.apps) {
            $item = New-ItemApp $app $todos
            if ($item) { $item } else { Write-Warning "[$($g.nome)] app não encontrado: '$app'" }
        }
        [pscustomobject]@{ nome = $g.nome; slug = (Get-NomeSeguro $g.nome); apps = @($itens) }
    }
    Save-Pastas $pastas
}

foreach ($g in Get-Pastas) {
    Update-AtalhoPasta $g | Out-Null
    Write-Host "OK  $($g.nome): $(@($g.apps).Count) apps"
}
Write-Host "`nAtalhos em: $PastaAtalhos"
Write-Host "Arraste cada atalho para a barra de tarefas (ou clique direito > Fixar na barra de tarefas)."
