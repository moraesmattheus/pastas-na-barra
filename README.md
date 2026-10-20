# Pastas na barra (personalizar-windows)

Pastas de apps na barra de tarefas do Windows 11, como no celular. A barra nativa não suporta pastas, então cada pasta é um **atalho fixado na barra** com ícone em mosaico (3x3) que abre um painel acima dela. Um pequeno programa na bandeja cuida de tudo.

## Instalar
Compile (veja abaixo) ou baixe o `PastasBarra.exe` e abra: ele pergunta se quer instalar (sem administrador, em `%LOCALAPPDATA%\Programs\PastasBarra`), cria o atalho no Menu Iniciar e aparece em *Aplicativos instalados* para desinstalar.

## Como funciona
1. Na primeira abertura, um assistente detecta seus apps e sugere pastas (IA, Redes Sociais, Jogos, Dev...). Você marca o que quer.
2. Uma tela mostra o ícone de cada pasta: arraste-o para a barra de tarefas (o Windows só permite fixar assim).
3. Clicar na pasta abre o painel: apps com nome, pontinho azul nos que estão abertos.

## Personalizar (tudo no painel ou em Configurações)
- Arrastar app para dentro do painel, **+** para escolher da lista, botão direito para mover/remover.
- Engrenagem ⚙ no painel: modo de edição com ✕, arrastar para reordenar, 8 cores, renomear, nova pasta, excluir e **nome no ícone** (mostra o nome da pasta embaixo do mosaico).
- No modo ⚙ as mudanças ficam como rascunho: **Salvar alterações** grava, **Descartar** volta ao que estava (ao sair sem salvar, o programa pergunta).
- Configurações (ícone da bandeja): gerenciar pastas, fixar, iniciar com o Windows, atualizações.

## Privacidade
O programa roda no seu computador: não coleta nem envia dados seus. A **única** conexão com a internet é a leitura do número da última versão no GitHub, e só acontece se você ligar "Avisar quando houver versão nova" (ou clicar em "Verificar atualizações agora"). Suas pastas ficam em `%APPDATA%\PastasBarra`.

## Avisos de atualização
Em *Configurações* há "Avisar quando houver versão nova" (o instalador também pergunta; vem desligado). Com a opção ligada, a cada ~6 horas o programa lê o número da última Release deste repositório; se houver versão nova, mostra um aviso na bandeja e abre a página de download no navegador. **O programa não baixa nem troca o próprio `.exe`**: isso é o que antivírus tratam como malware, então a atualização 100% automática só deve voltar depois que o `.exe` estiver assinado digitalmente. Há também "Verificar atualizações agora".

Para publicar uma versão: suba `Versao` em `app/Instalador.cs` e `app/Info.cs`, faça commit e crie a tag `vX.Y.Z` (`git tag v0.2.0 && git push origin v0.2.0`); o GitHub Actions compila e cria a Release com o `.exe` e o hash. Para atualizar uma cópia instalada: baixe o novo `.exe`, abra e escolha "Atualizar".

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
A primeira versão, em PowerShell, está em [`prototipo/`](prototipo/LEIAME.md).

## Antivírus e confiança (leia antes de distribuir)
Um `.exe` novo, sem assinatura digital, que fica na bandeja, inicia com o Windows e mexe na barra de tarefas pode ser marcado por antivírus como suspeito (o McAfee já bloqueou uma versão minúscula do lançador). Isso é falso positivo, mas só some de forma confiável com **assinatura digital + reputação**.

Já feito no código, para não ter comportamento típico de malware: sem `.exe` auxiliar; sem PowerShell nem janelas ocultas; **o programa não baixa, não executa nem renomeia nenhum `.exe`** (nem para se atualizar); o desinstalador roda no próprio lugar e não agenda exclusões no reinício; não lê títulos de janelas (o pontinho "aberto" usa só o nome do processo); não pede administrador; iniciar com o Windows e o aviso de versão são opções visíveis; lista de apps lida direto do shell do Windows; nome, empresa, versão, ícone e manifesto no `.exe`; build automatizado com SHA-256 (`dist\PastasBarra.exe.sha256`) e workflow do GitHub Actions.

**Ainda assim não há garantia:** sem assinatura digital, qualquer antivírus pode marcar um programa novo. Se acontecer, não adicione exclusões por padrão: confira o hash (`PastasBarra.exe.sha256`), compare com a Release e envie o arquivo como falso positivo (Microsoft: https://www.microsoft.com/wdsi/filesubmission ; McAfee: pelo programa ou site deles).

Caminhos para assinar (decisão do autor):
1. **Projeto aberto + SignPath Foundation** (gratuito para código aberto; exige licença aprovada pela OSI, repositório público e build automatizado). É o caminho mais barato. Verifique os critérios atuais em signpath.org.
2. **Microsoft Artifact Signing (ex-Trusted Signing)**: barato, mas para pessoa física só nos EUA e Canadá; para empresa, em uma lista de países (confira se o Brasil consta).
3. **Certificado comercial de assinatura de código** (pago, anual).
4. **Microsoft Store (MSIX)**: a Loja assina o pacote.
Além disso: enviar o arquivo como falso positivo ao McAfee, ao Microsoft Defender e a outros fornecedores; assinado ou não, a reputação no SmartScreen cresce com downloads.

## Licença
Copyright 2026 Mattheus Moraes. Licenciado sob a [Apache License 2.0](LICENSE). A licença mantém você como autor: toda redistribuição precisa preservar o aviso de copyright e o arquivo [NOTICE](NOTICE), mudanças precisam ser indicadas, e o nome/marca "Pastas na barra" não é licenciado para uso em obras derivadas (seção 6). O direito autoral sobre o código é seu automaticamente; a licença só define o que os outros podem fazer. Isto não é aconselhamento jurídico.
