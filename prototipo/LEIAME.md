# Protótipo em PowerShell

Primeira versão da ideia (antes do programa em C#): scripts que geram atalhos com ícone em mosaico e um painel WPF aberto pelo PowerShell. Mantido só como referência; o programa atual está em [`../app`](../app).

Uso: `powershell -ExecutionPolicy Bypass -File instalar.ps1` (gera os atalhos em `atalhos\`). Requer Windows PowerShell 5.1. Os dados ficam nesta pasta (`pastas.json`, `cache\`, `atalhos\`), ignorados pelo Git.
