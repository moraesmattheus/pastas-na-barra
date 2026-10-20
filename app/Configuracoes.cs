using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace PastasBarra
{
    // Janela de configuracoes moderna estilo Windows 11 Fluent UI:
    // Pastas, Aparencia & Vidro e Modo Reflexao / Foco de Tempo
    public class ConfiguracoesJanela : Window
    {
        readonly StackPanel _containerCards = new StackPanel();
        readonly Grid _painelPastas = new Grid();
        readonly Grid _painelAparencia = new Grid();
        readonly Grid _painelReflexao = new Grid();

        readonly Border _abaPastasBtn;
        readonly Border _abaAparenciaBtn;
        readonly Border _abaReflexaoBtn;

        Border _previewCard;
        TextBlock _previewTxtOpacidade;

        Border _btnVidro;
        Border _btnSolido;
        readonly List<Tuple<int, Border>> _botoesCantos = new List<Tuple<int, Border>>();

        Pasta _selecionada;
        int _abaAtiva = 0; // 0 = Pastas, 1 = Aparencia, 2 = Reflexao

        public ConfiguracoesJanela()
        {
            Title = "Pastas na barra · Configurações";
            Width = 680;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = true;
            ResizeMode = ResizeMode.CanMinimize;

            // Define AppUserModelID unico para a janela de configuracoes nao agrupar com pastas da barra
            SourceInitialized += (s, e) =>
            {
                try
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero) Shell.DefinirAppIdJanela(hwnd, "PastasBarra.Configuracoes");
                }
                catch { }
            };

            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

            var moldura = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = Ui.CorFundoJanela,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12),
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };

            var gridPrincipal = new Grid();
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // cabecalho
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // abas
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // corpo
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // rodape

            // Cabecalho arrastavel com titulo, minimizar e fechar
            var cabecalho = new Grid { Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            cabecalho.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

            var tituloStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var iconeDots = new Canvas { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0) };
            var coresDots = new[] { "#7DD3FC", "#A78BFA", "#FB923C", "#4ADE80" };
            for (int i = 0; i < 4; i++)
            {
                var el = new Ellipse { Width = 6, Height = 6, Fill = Ui.Pincel(coresDots[i]) };
                Canvas.SetLeft(el, (i % 2) * 7);
                Canvas.SetTop(el, (i / 2) * 7);
                iconeDots.Children.Add(el);
            }
            tituloStack.Children.Add(iconeDots);
            tituloStack.Children.Add(new TextBlock
            {
                Text = "Configurações · Pastas na barra",
                Foreground = Ui.CorTexto,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            cabecalho.Children.Add(tituloStack);

            // Botoes minimizar e fechar estilo Windows 11
            var botoesCabecalho = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

            var btnMinimizar = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "—", Foreground = Ui.CorSubtexto, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            btnMinimizar.MouseEnter += (s, e) => btnMinimizar.Background = Ui.CorCardHover;
            btnMinimizar.MouseLeave += (s, e) => btnMinimizar.Background = Brushes.Transparent;
            btnMinimizar.MouseLeftButtonUp += (s, e) => WindowState = WindowState.Minimized;
            botoesCabecalho.Children.Add(btnMinimizar);

            var btnFechar = new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "✕", Foreground = Ui.CorSubtexto, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            var txtX = (TextBlock)btnFechar.Child;
            btnFechar.MouseEnter += (s, e) => { btnFechar.Background = Ui.Pincel("#E81123"); txtX.Foreground = Brushes.White; };
            btnFechar.MouseLeave += (s, e) => { btnFechar.Background = Brushes.Transparent; txtX.Foreground = Ui.CorSubtexto; };
            btnFechar.MouseLeftButtonUp += (s, e) => Close();
            botoesCabecalho.Children.Add(btnFechar);

            cabecalho.Children.Add(botoesCabecalho);

            // Abas de navegacao: [ 📁 Minhas Pastas ] [ ✨ Aparência & Vidro ] [ 📊 Reflexão & Foco ]
            var barraAbas = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 4) };
            _abaPastasBtn = CriarBotaoAba("📁  Minhas Pastas", true, () => AlternarAba(0));
            _abaAparenciaBtn = CriarBotaoAba("✨  Aparência & Vidro", false, () => AlternarAba(1));
            _abaReflexaoBtn = CriarBotaoAba("📊  Reflexão & Foco", false, () => AlternarAba(2));
            barraAbas.Children.Add(_abaPastasBtn);
            barraAbas.Children.Add(_abaAparenciaBtn);
            barraAbas.Children.Add(_abaReflexaoBtn);

            // Container do corpo (alterna entre as 3 abas sem destruicao de arvore)
            var containerCorpo = new Grid { Margin = new Thickness(16, 4, 16, 10) };
            MontarPainelPastas();
            MontarPainelAparencia();
            MontarPainelReflexao();

            containerCorpo.Children.Add(_painelPastas);
            containerCorpo.Children.Add(_painelAparencia);
            containerCorpo.Children.Add(_painelReflexao);
            _painelAparencia.Visibility = Visibility.Collapsed;
            _painelReflexao.Visibility = Visibility.Collapsed;

            // Rodape
            var rodape = new Border
            {
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 14),
                Background = Ui.CorCard
            };
            var gridRodape = new Grid();
            gridRodape.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridRodape.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var opcoes = new StackPanel();
            var inicio = Ui.CaixaSelecao("Iniciar junto com o Windows", Shell.IniciarComWindows, v => Shell.IniciarComWindows = v);
            var auto = Ui.CaixaSelecao("Avisar quando houver nova versão", Repo.Dados.AtualizarAuto, v => { Repo.Dados.AtualizarAuto = v; Repo.Salvar(); });
            opcoes.Children.Add(inicio);
            opcoes.Children.Add(auto);
            gridRodape.Children.Add(opcoes);

            var rodapeBotoes = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rodapeBotoes, 1);
            var btnFixar = Ui.Botao("Fixar tudo / Separador", () => App.MostrarFixar(Repo.Dados.Pastas));
            var verificar = Ui.Botao("Atualizações", () => App.VerificarAtualizacao(true));
            var versaoTxt = new TextBlock
            {
                Text = "v" + Instalador.Versao,
                Foreground = Ui.CorSubtexto,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            rodapeBotoes.Children.Add(btnFixar);
            rodapeBotoes.Children.Add(verificar);
            rodapeBotoes.Children.Add(versaoTxt);
            gridRodape.Children.Add(rodapeBotoes);
            rodape.Child = gridRodape;

            Grid.SetRow(cabecalho, 0);
            Grid.SetRow(barraAbas, 1);
            Grid.SetRow(containerCorpo, 2);
            Grid.SetRow(rodape, 3);
            gridPrincipal.Children.Add(cabecalho);
            gridPrincipal.Children.Add(barraAbas);
            gridPrincipal.Children.Add(containerCorpo);
            gridPrincipal.Children.Add(rodape);

            moldura.Child = gridPrincipal;
            Content = moldura;

            Recarregar();
            Activated += (s, e) => Recarregar();
        }

        Border CriarBotaoAba(string texto, bool ativo, Action acao)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = ativo ? Ui.CorCardHover : Brushes.Transparent,
                BorderBrush = ativo ? Ui.CorDestaque : Brushes.Transparent,
                BorderThickness = new Thickness(ativo ? 1 : 0),
                Cursor = Cursors.Hand
            };
            var t = new TextBlock
            {
                Text = texto,
                Foreground = ativo ? Ui.CorTexto : Ui.CorSubtexto,
                FontSize = 12.5,
                FontWeight = ativo ? FontWeights.SemiBold : FontWeights.Normal
            };
            b.Child = t;
            b.MouseLeftButtonUp += (s, e) => acao();
            return b;
        }

        void AlternarAba(int indice)
        {
            _abaAtiva = indice;
            _painelPastas.Visibility = indice == 0 ? Visibility.Visible : Visibility.Collapsed;
            _painelAparencia.Visibility = indice == 1 ? Visibility.Visible : Visibility.Collapsed;
            _painelReflexao.Visibility = indice == 2 ? Visibility.Visible : Visibility.Collapsed;

            AtualizarEstiloAba(_abaPastasBtn, indice == 0);
            AtualizarEstiloAba(_abaAparenciaBtn, indice == 1);
            AtualizarEstiloAba(_abaReflexaoBtn, indice == 2);

            if (indice == 1) AtualizarPreview();
            if (indice == 2)
            {
                RastreadorTempo.SalvarParcial();
                MontarPainelReflexaoRecarregar();
            }
        }

        void AtualizarEstiloAba(Border b, bool ativo)
        {
            b.Background = ativo ? Ui.CorCardHover : Brushes.Transparent;
            b.BorderBrush = ativo ? Ui.CorDestaque : Brushes.Transparent;
            b.BorderThickness = new Thickness(ativo ? 1 : 0);
            var t = b.Child as TextBlock;
            if (t != null)
            {
                t.Foreground = ativo ? Ui.CorTexto : Ui.CorSubtexto;
                t.FontWeight = ativo ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        // ---------- ABA 1: GERENCIAMENTO DE PASTAS ----------
        void MontarPainelPastas()
        {
            _painelPastas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _painelPastas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });

            var painelEsquerdo = new Grid();
            painelEsquerdo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            painelEsquerdo.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tituloLista = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            tituloLista.Children.Add(new TextBlock { Text = "Suas Pastas", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
            tituloLista.Children.Add(new TextBlock { Text = "Selecione uma pasta para gerenciar ou personalizar", FontSize = 11.5, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 0) });
            painelEsquerdo.Children.Add(tituloLista);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _containerCards, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetRow(scroll, 1);
            painelEsquerdo.Children.Add(scroll);
            _painelPastas.Children.Add(painelEsquerdo);

            var botoes = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 32, 0, 0) };
            Grid.SetColumn(botoes, 1);

            botoes.Children.Add(Ui.Botao("+ Nova pasta", NovaPasta, true));
            botoes.Children.Add(Ui.Botao("Renomear", Renomear));
            botoes.Children.Add(Ui.Botao("Mudar cor", MudarCor));
            botoes.Children.Add(Ui.Botao("Texto no ícone", DefinirRotulo));
            botoes.Children.Add(Ui.Botao("Fixar esta pasta...", Fixar));
            botoes.Children.Add(Ui.Botao("Excluir pasta", Excluir, false, true));

            var sepAcoes = new Border { Height = 1, Background = Ui.CorBorda, Margin = new Thickness(4, 10, 4, 10) };
            botoes.Children.Add(sepAcoes);

            botoes.Children.Add(Ui.Botao("Sugestões automáticas...", () => App.AbrirAssistente()));
            if (Instalador.RodandoInstalado())
                botoes.Children.Add(Ui.Botao("Desinstalar...", () => Process.Start(Instalador.ExeAtual, "--desinstalar")));
            else
                botoes.Children.Add(Ui.Botao("Instalar no PC...", InstalarAgora));

            _painelPastas.Children.Add(botoes);
        }

        // ---------- ABA 2: APARÊNCIA, VIDRO & PROFUNDIDADE ----------
        void MontarPainelAparencia()
        {
            _painelAparencia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            _painelAparencia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });

            var scrollControles = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 14, 0) };
            var stackControles = new StackPanel();

            // 1. Estilo de Fundo (Vidro vs Solido)
            var cardEstilo = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stEstilo = new StackPanel();
            stEstilo.Children.Add(new TextBlock { Text = "Material do Fundo", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
            stEstilo.Children.Add(new TextBlock { Text = "Translúcido com leve passagem de luz ou acetinado sólido.", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 8), TextWrapping = TextWrapping.Wrap });

            var rowEstilos = new StackPanel { Orientation = Orientation.Horizontal };
            _btnVidro = Ui.Botao("💎 Vidro Translúcido", () =>
            {
                Repo.Dados.EstiloVisual = "Vidro";
                Repo.Salvar();
                AtualizarBotoesEstilo();
                AtualizarPreview();
            }, Repo.Dados.EstiloVisual != "Solido");

            _btnSolido = Ui.Botao("🏛 Sólido Acetinado", () =>
            {
                Repo.Dados.EstiloVisual = "Solido";
                Repo.Salvar();
                AtualizarBotoesEstilo();
                AtualizarPreview();
            }, Repo.Dados.EstiloVisual == "Solido");

            rowEstilos.Children.Add(_btnVidro);
            rowEstilos.Children.Add(_btnSolido);
            stEstilo.Children.Add(rowEstilos);
            cardEstilo.Child = stEstilo;
            stackControles.Children.Add(cardEstilo);

            // 2. Opacidade do Vidro (Slider 70% a 100%)
            var cardOpacidade = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stOpacidade = new StackPanel();
            var topoOpac = new Grid();
            topoOpac.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topoOpac.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titOpac = new TextBlock { Text = "Nível de Transparência", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto };
            _previewTxtOpacidade = new TextBlock
            {
                Text = Repo.Dados.OpacidadeVidro + "%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Ui.CorDestaque
            };
            Grid.SetColumn(_previewTxtOpacidade, 1);
            topoOpac.Children.Add(titOpac);
            topoOpac.Children.Add(_previewTxtOpacidade);
            stOpacidade.Children.Add(topoOpac);

            stOpacidade.Children.Add(new TextBlock { Text = "Controle o nível de passagem de luz e contraste através das pastas.", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 8), TextWrapping = TextWrapping.Wrap });

            var slider = new Slider
            {
                Minimum = 70,
                Maximum = 100,
                Value = Repo.Dados.OpacidadeVidro,
                TickFrequency = 5,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 4, 0, 4)
            };
            slider.ValueChanged += (s, e) =>
            {
                int val = (int)Math.Round(slider.Value);
                Repo.Dados.OpacidadeVidro = val;
                if (_previewTxtOpacidade != null) _previewTxtOpacidade.Text = val + "%";
                Repo.Salvar();
                AtualizarPreview();
            };
            stOpacidade.Children.Add(slider);
            cardOpacidade.Child = stOpacidade;
            stackControles.Children.Add(cardOpacidade);

            // 3. Bisel Reflexivo de Vidro
            var cardBisel = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stBisel = new StackPanel();
            stBisel.Children.Add(new TextBlock { Text = "Reflexo e Acabamento", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
            var cbBisel = Ui.CaixaSelecao("Bisel reflexivo de luz nas bordas", Repo.Dados.BiselVidro, v =>
            {
                Repo.Dados.BiselVidro = v;
                Repo.Salvar();
                AtualizarPreview();
            });
            stBisel.Children.Add(cbBisel);
            stBisel.Children.Add(new TextBlock { Text = "Projeta um reflexo sutil de iluminação simulando o bisel de vidro lapidado.", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(22, 0, 0, 0), TextWrapping = TextWrapping.Wrap });
            cardBisel.Child = stBisel;
            stackControles.Children.Add(cardBisel);

            // 4. Arredondamento dos Cantos
            var cardCantos = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stCantos = new StackPanel();
            stCantos.Children.Add(new TextBlock { Text = "Curvatura dos Cantos", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
            stCantos.Children.Add(new TextBlock { Text = "Ajuste o raio de arredondamento das bordas das janelas.", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 8) });

            var rowCantos = new StackPanel { Orientation = Orientation.Horizontal };
            _botoesCantos.Clear();
            Action<int, string> addOpcaoCanto = (raio, label) =>
            {
                var btn = Ui.Botao(label, () =>
                {
                    Repo.Dados.CantosArredondados = raio;
                    Repo.Salvar();
                    AtualizarBotoesCantos();
                    AtualizarPreview();
                }, Repo.Dados.CantosArredondados == raio);
                _botoesCantos.Add(Tuple.Create(raio, btn));
                rowCantos.Children.Add(btn);
            };
            addOpcaoCanto(12, "Compacto (12px)");
            addOpcaoCanto(18, "Padrão (18px)");
            addOpcaoCanto(24, "Suave (24px)");

            stCantos.Children.Add(rowCantos);
            cardCantos.Child = stCantos;
            stackControles.Children.Add(cardCantos);

            scrollControles.Content = stackControles;
            _painelAparencia.Children.Add(scrollControles);

            // Lado direito: Pré-visualização ao vivo
            var containerPreview = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(containerPreview, 1);

            containerPreview.Children.Add(new TextBlock
            {
                Text = "Pré-visualização ao Vivo",
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            _previewCard = new Border
            {
                Width = 240,
                Padding = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stPreview = new StackPanel();
            stPreview.Children.Add(new TextBlock
            {
                Text = "Produtividade",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var wrapApps = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Action<string, string> addAppMock = (nome, emoji) =>
            {
                var cel = new Border
                {
                    Width = 66,
                    Padding = new Thickness(4, 6, 4, 6),
                    CornerRadius = new CornerRadius(10),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(3)
                };
                var cSt = new StackPanel();
                cSt.Children.Add(new TextBlock { Text = emoji, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center });
                cSt.Children.Add(new TextBlock { Text = nome, FontSize = 10.5, Foreground = Ui.CorTexto, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
                cel.Child = cSt;
                cel.MouseEnter += (s, e) => { cel.Background = Ui.CorCardHover; if (Repo.Dados.BiselVidro) cel.BorderBrush = Ui.Pincel("#30FFFFFF"); };
                cel.MouseLeave += (s, e) => { cel.Background = Brushes.Transparent; cel.BorderBrush = Brushes.Transparent; };
                wrapApps.Children.Add(cel);
            };
            addAppMock("Notas", "📝");
            addAppMock("Calc", "🔢");
            addAppMock("Web", "🌐");
            stPreview.Children.Add(wrapApps);

            _previewCard.Child = stPreview;
            containerPreview.Children.Add(_previewCard);

            containerPreview.Children.Add(new TextBlock
            {
                Text = "Aceleração por hardware ativa · 0% CPU",
                FontSize = 10.5,
                Foreground = Ui.CorSubtexto,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            });

            _painelAparencia.Children.Add(containerPreview);
            AtualizarPreview();
        }

        void AtualizarBotoesEstilo()
        {
            bool solido = Repo.Dados.EstiloVisual == "Solido";
            DefinirDestaqueBotao(_btnVidro, !solido);
            DefinirDestaqueBotao(_btnSolido, solido);
        }

        void AtualizarBotoesCantos()
        {
            foreach (var item in _botoesCantos)
            {
                DefinirDestaqueBotao(item.Item2, Repo.Dados.CantosArredondados == item.Item1);
            }
        }

        void DefinirDestaqueBotao(Border b, bool destaque)
        {
            if (b == null) return;
            b.Background = destaque ? Ui.Pincel("#2563EB") : Ui.Pincel(Ui.TemaEscuro ? "#1FFFFFFF" : "#0A000000");
            b.BorderThickness = new Thickness(destaque ? 0 : 1);
            var t = b.Child as TextBlock;
            if (t != null)
            {
                t.Foreground = destaque ? Brushes.White : Ui.CorTexto;
                t.FontWeight = destaque ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        void AtualizarPreview()
        {
            if (_previewCard == null) return;
            _previewCard.CornerRadius = Ui.CantosPainel;
            _previewCard.Background = Ui.FundoPainel("#3B82F6");
            _previewCard.BorderBrush = Ui.BordaPainel();
            _previewCard.BorderThickness = new Thickness(1);
            _previewCard.Effect = Ui.SombraProfundidade;
        }

        // ---------- ABA 3: REFLEXÃO & FOCO DE TEMPO ----------
        void MontarPainelReflexao()
        {
            _painelReflexao.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // cabecalho reflexao
            _painelReflexao.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // lista apps
            _painelReflexao.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // insight

            // Cabecalho da Reflexao
            var cardCab = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var gridCab = new Grid();
            gridCab.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridCab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titStack = new StackPanel();
            titStack.Children.Add(new TextBlock { Text = "Reflexão & Tempo de Foco", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
            titStack.Children.Add(new TextBlock { Text = "Acompanhe onde seu tempo foi investido hoje. 100% privado no seu computador.", FontSize = 11.5, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 0) });
            gridCab.Children.Add(titStack);

            var toggleRastrear = Ui.CaixaSelecao("Ativar rastreamento", Repo.Dados.RastrearTempo, v =>
            {
                Repo.Dados.RastrearTempo = v;
                if (v) RastreadorTempo.Iniciar();
                else RastreadorTempo.Parar();
                Repo.Salvar();
                MontarPainelReflexaoRecarregar();
            });
            toggleRastrear.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(toggleRastrear, 1);
            gridCab.Children.Add(toggleRastrear);
            cardCab.Child = gridCab;
            _painelReflexao.Children.Add(cardCab);

            if (!Repo.Dados.RastrearTempo)
            {
                var avisoDesativado = new TextBlock
                {
                    Text = "O rastreamento de foco está desativado.\nMarque 'Ativar rastreamento' acima para começar a registrar o tempo dos aplicativos.",
                    Foreground = Ui.CorSubtexto,
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };
                Grid.SetRow(avisoDesativado, 1);
                _painelReflexao.Children.Add(avisoDesativado);
                return;
            }

            // Dados de hoje
            var diaHoje = DateTime.Today.ToString("yyyy-MM-dd");
            var registroDia = Repo.Dados.HistoricoUso.FirstOrDefault(d => d.Data == diaHoje);

            if (registroDia == null || registroDia.Apps.Count == 0)
            {
                var vazio = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                vazio.Children.Add(new TextBlock { Text = "🌱", FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center });
                vazio.Children.Add(new TextBlock
                {
                    Text = "Comece seu dia!\nConforme você alternar entre seus programas, seu resumo de foco aparecerá aqui.",
                    Foreground = Ui.CorSubtexto,
                    FontSize = 12.5,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                });
                Grid.SetRow(vazio, 1);
                _painelReflexao.Children.Add(vazio);
                return;
            }

            var appsOrdenados = registroDia.Apps.OrderByDescending(a => a.Segundos).ToList();
            int totalSegundos = appsOrdenados.Sum(a => a.Segundos);
            int maiorSegundos = appsOrdenados.First().Segundos;

            var stackLista = new StackPanel();

            // Cartao de resumo do dia
            var cardTotal = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.Pincel(Ui.TemaEscuro ? "#203B82F6" : "#123B82F6"),
                BorderBrush = Ui.Pincel("#3B82F6"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stTotal = new StackPanel { Orientation = Orientation.Horizontal };
            stTotal.Children.Add(new TextBlock { Text = "⏱️ Tempo ativo hoje: ", FontSize = 13, Foreground = Ui.CorTexto, FontWeight = FontWeights.SemiBold });
            stTotal.Children.Add(new TextBlock { Text = RastreadorTempo.FormatarTempo(totalSegundos), FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = Ui.CorDestaque });
            stTotal.Children.Add(new TextBlock { Text = "  ·  " + appsOrdenados.Count + " aplicativos", FontSize = 12, Foreground = Ui.CorSubtexto, VerticalAlignment = VerticalAlignment.Center });
            cardTotal.Child = stTotal;
            stackLista.Children.Add(cardTotal);

            // Linhas dos aplicativos com barras de progresso proporcionais
            foreach (var app in appsOrdenados.Take(7))
            {
                var itemApp = app;
                double proporcao = maiorSegundos > 0 ? (double)itemApp.Segundos / maiorSegundos : 0;
                double percentualTotal = totalSegundos > 0 ? ((double)itemApp.Segundos / totalSegundos) * 100 : 0;

                var cardLinha = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = Ui.CorCard,
                    BorderBrush = Ui.CorBorda,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var gItem = new Grid();
                gItem.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                gItem.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var topoItem = new Grid();
                topoItem.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                topoItem.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nomeStack = new StackPanel { Orientation = Orientation.Horizontal };
                nomeStack.Children.Add(new TextBlock { Text = itemApp.Nome, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Ui.CorTexto });
                nomeStack.Children.Add(new TextBlock { Text = " (" + itemApp.Processo + ")", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(4, 1, 0, 0) });
                topoItem.Children.Add(nomeStack);

                var tempoStack = new StackPanel { Orientation = Orientation.Horizontal };
                Grid.SetColumn(tempoStack, 1);
                tempoStack.Children.Add(new TextBlock { Text = RastreadorTempo.FormatarTempo(itemApp.Segundos), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Ui.CorDestaque });
                tempoStack.Children.Add(new TextBlock { Text = "  " + Math.Round(percentualTotal) + "%", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(2, 1, 0, 0) });
                topoItem.Children.Add(tempoStack);
                gItem.Children.Add(topoItem);

                // Barra de progresso visual
                var barraTrilho = new Border
                {
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    Background = Ui.Pincel(Ui.TemaEscuro ? "#20FFFFFF" : "#12000000"),
                    Margin = new Thickness(0, 6, 0, 0)
                };
                var barraProgresso = new Border
                {
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    Background = Ui.CorDestaque,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(12, 380 * proporcao)
                };
                barraTrilho.Child = barraProgresso;
                Grid.SetRow(barraTrilho, 1);
                gItem.Children.Add(barraTrilho);

                cardLinha.Child = gItem;
                stackLista.Children.Add(cardLinha);
            }

            var scrollReflexao = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stackLista, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(scrollReflexao, 1);
            _painelReflexao.Children.Add(scrollReflexao);

            // Dica de insight no rodape
            var topApp = appsOrdenados.First();
            var cardInsight = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = Ui.CorCardHover,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var stInsight = new StackPanel { Orientation = Orientation.Horizontal };
            stInsight.Children.Add(new TextBlock { Text = "💡 ", FontSize = 13 });
            stInsight.Children.Add(new TextBlock
            {
                Text = "Mais utilizado hoje: " + topApp.Nome + " (" + RastreadorTempo.FormatarTempo(topApp.Segundos) + "). Dica: fixe-o em uma pasta para abrir instantaneamente.",
                FontSize = 11.5,
                Foreground = Ui.CorSubtexto,
                TextWrapping = TextWrapping.Wrap
            });
            cardInsight.Child = stInsight;
            Grid.SetRow(cardInsight, 2);
            _painelReflexao.Children.Add(cardInsight);
        }

        void MontarPainelReflexaoRecarregar()
        {
            _painelReflexao.Children.Clear();
            _painelReflexao.RowDefinitions.Clear();
            MontarPainelReflexao();
        }

        Pasta Selecionada
        {
            get
            {
                if (_selecionada != null && Repo.Dados.Pastas.Any(p => p.Id == _selecionada.Id))
                    return _selecionada;
                return Repo.Dados.Pastas.FirstOrDefault();
            }
        }

        void Recarregar()
        {
            var idSel = Selecionada != null ? Selecionada.Id : null;
            _containerCards.Children.Clear();

            if (Repo.Dados.Pastas.Count == 0)
            {
                var vazio = new TextBlock
                {
                    Text = "Nenhuma pasta criada ainda.\nClique em '+ Nova pasta' ao lado para começar.",
                    Foreground = Ui.CorSubtexto,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                };
                _containerCards.Children.Add(vazio);
                return;
            }

            foreach (var p in Repo.Dados.Pastas)
            {
                var pasta = p;
                bool isSel = pasta.Id == idSel;

                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = isSel ? Ui.CorCardHover : Ui.CorCard,
                    BorderBrush = isSel ? Ui.CorDestaque : Ui.CorBorda,
                    BorderThickness = new Thickness(isSel ? 1.5 : 1),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand
                };

                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Bolinha da cor da pasta
                var dotCor = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = Ui.Pincel(pasta.Cor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                itemGrid.Children.Add(dotCor);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(info, 1);

                info.Children.Add(new TextBlock
                {
                    Text = pasta.Nome,
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.CorTexto
                });

                string rotuloTexto = pasta.MostrarNome
                    ? " · Rótulo: " + (string.IsNullOrEmpty(pasta.Rotulo) ? Shell.ObterTextoIcone(pasta) : pasta.Rotulo)
                    : " · Sem rótulo";

                info.Children.Add(new TextBlock
                {
                    Text = pasta.Apps.Count + " aplicativos" + rotuloTexto,
                    FontSize = 11.5,
                    Foreground = Ui.CorSubtexto,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                itemGrid.Children.Add(info);
                card.Child = itemGrid;

                card.MouseEnter += (s, e) => { if (_selecionada != pasta) card.Background = Ui.CorCardHover; };
                card.MouseLeave += (s, e) => { if (_selecionada != pasta) card.Background = Ui.CorCard; };
                card.MouseLeftButtonUp += (s, e) =>
                {
                    _selecionada = pasta;
                    Recarregar();
                };

                _containerCards.Children.Add(card);
            }
        }

        void NovaPasta()
        {
            var nome = Ui.PedirTexto(this, "Nome da nova pasta", "");
            if (nome == null) return;
            if (!Repo.NomeDisponivel(nome, null))
            {
                MessageBox.Show("Já existe uma pasta com esse nome.", "Nova pasta");
                return;
            }
            var p = Repo.Criar(nome);
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            _selecionada = p;
            Recarregar();
            App.MostrarFixar(new[] { p });
        }

        void Renomear()
        {
            var p = Selecionada; if (p == null) return;
            var nome = Ui.PedirTexto(this, "Renomear pasta", p.Nome);
            if (nome == null || nome == p.Nome) return;
            if (!Repo.NomeDisponivel(nome, p))
            {
                MessageBox.Show("Já existe uma pasta com esse nome.", "Renomear pasta");
                return;
            }
            Shell.ApagarAtalho(p);
            p.Nome = nome;
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            Recarregar();
        }

        void MudarCor()
        {
            var p = Selecionada; if (p == null) return;
            var cor = Ui.EscolherCor(this);
            if (cor == null) return;
            p.Cor = cor;
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            Recarregar();
        }

        void DefinirRotulo()
        {
            var p = Selecionada; if (p == null) return;
            var atual = string.IsNullOrEmpty(p.Rotulo) ? Shell.ObterTextoIcone(p) : p.Rotulo;
            var resp = Ui.PedirTexto(this, "Texto no ícone (deixe em branco para Auto)", atual);
            if (resp == null) return;
            p.MostrarNome = true;
            p.Rotulo = string.IsNullOrWhiteSpace(resp) ? null : resp.Trim().ToUpperInvariant();
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            Recarregar();
        }

        void InstalarAgora()
        {
            Repo.Dados.ModoPortatil = false;
            Repo.Salvar();
            var j = new InstalarJanela();
            j.ShowDialog();
            if (!j.Instalou) return;
            Process.Start(new ProcessStartInfo(Instalador.ExeInstalado, "--aguardar") { UseShellExecute = false });
            App.Sair();
        }

        void Fixar()
        {
            var p = Selecionada;
            App.MostrarFixar(p != null ? new[] { p } : Repo.Dados.Pastas.ToArray());
        }

        void Excluir()
        {
            var p = Selecionada; if (p == null) return;
            if (MessageBox.Show("Excluir a pasta '" + p.Nome + "'?\nOs aplicativos continuam normais no seu PC.\nDepois, desafixe o ícone dela da barra de tarefas.",
                "Excluir pasta", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Shell.ApagarAtalho(p);
            Repo.Dados.Pastas.Remove(p);
            Repo.Salvar();
            _selecionada = Repo.Dados.Pastas.FirstOrDefault();
            Recarregar();
        }
    }
}
