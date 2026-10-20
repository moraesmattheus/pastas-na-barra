# Pastas na barra (personalizar-windows)

Pastas de apps na barra de tarefas do Windows 11, como no celular. A barra nativa não suporta pastas, então cada pasta é um **atalho fixado na barra** com ícone em mosaico (3x3) que abre um painel acima dela. Um pequeno programa na bandeja cuida de tudo.

## Instalar
Baixe `PastasBarra.exe` e abra: ele pergunta se quer instalar (sem administrador, em `%LOCALAPPDATA%\Programs\PastasBarra`), cria o atalho no Menu Iniciar e aparece em *Aplicativos instalados* para desinstalar.

## Como funciona
1. Na primeira abertura, um assistente detecta seus apps e sugere pastas (IA, Redes Sociais, Jogos, Dev...). Você marca o que quer.
2. Uma tela mostra o ícone de cada pasta: arraste-o para a barra de tarefas (o Windows só permite fixar assim).
3. Clicar na pasta abre o painel: apps com nome, pontinho azul nos que estão abertos.

## Personalizar (tudo no painel ou em Configurações)
- Arrastar app para dentro do painel, **+** para escolher da lista, botão direito para mover/remover.
- Engrenagem ⚙ no painel: modo de edição com ✕, arrastar para reordenar, 8 cores, renomear, nova pasta, excluir e **nome no ícone** (mostra o nome da pasta embaixo do mosaico).
- Configurações (ícone da bandeja): gerenciar pastas, fixar, iniciar com o Windows.

## Compilar
```
powershell -ExecutionPolicy Bypass -File build.ps1
```
Gera `dist\PastasBarra.exe` (precisa só do Visual Studio Build Tools ou do .NET Framework 4.x; sem instalar SDK). Dados em `%APPDATA%\PastasBarra`.

## Limites conhecidos
- Fixar o ícone na barra é um passo manual (restrição do Windows 11).
- Apps abertos continuam aparecendo soltos na barra nativa (esconder isso exige um mod do Windhawk; planejado).
- Se recompilar o `.exe` com os ícones fixados, a barra pode mostrar ícones em branco até reiniciar o Explorer.

## Protótipo
`painel.ps1`, `lib.psm1`, `instalar.ps1` e `abrir.vbs` são o protótipo em PowerShell que originou o app.

## Antivírus e confiança (leia antes de distribuir)
Um `.exe` novo, sem assinatura digital, que fica na bandeja, inicia com o Windows e mexe na barra de tarefas pode ser marcado por antivírus como suspeito (o McAfee já bloqueou uma versão minúscula do lançador). Isso é falso positivo, mas só some de forma confiável com **assinatura digital + reputação**.

Já feito no código: sem `.exe` auxiliar, sem PowerShell escondido (a lista de apps vem do shell do Windows), sem pedir administrador, instalação por usuário, nome/empresa/versão/ícone/manifesto, build reprodutível pelo `build.ps1` com SHA-256 (`dist\PastasBarra.exe.sha256`) e workflow do GitHub Actions (`.github/workflows/build.yml`, ainda não testado na nuvem).

Caminhos para assinar (decisão do autor):
1. **Projeto aberto + SignPath Foundation** (gratuito para código aberto; exige licença aprovada pela OSI, repositório público e build automatizado). É o caminho mais barato. Verifique os critérios atuais em signpath.org.
2. **Microsoft Artifact Signing (ex-Trusted Signing)**: barato, mas para pessoa física só nos EUA e Canadá; para empresa, em uma lista de países (confira se o Brasil consta).
3. **Certificado comercial de assinatura de código** (pago, anual).
4. **Microsoft Store (MSIX)**: a Loja assina o pacote.
Além disso: enviar o arquivo como falso positivo ao McAfee, ao Microsoft Defender e a outros fornecedores; assinado ou não, a reputação no SmartScreen cresce com downloads.

## Licença
Copyright 2026 Mattheus Moraes. Licenciado sob a [Apache License 2.0](LICENSE). A licença mantém você como autor: toda redistribuição precisa preservar o aviso de copyright e o arquivo [NOTICE](NOTICE), mudanças precisam ser indicadas, e o nome/marca "Pastas na barra" não é licenciado para uso em obras derivadas (seção 6). O direito autoral sobre o código é seu automaticamente; a licença só define o que os outros podem fazer. Isto não é aconselhamento jurídico.
