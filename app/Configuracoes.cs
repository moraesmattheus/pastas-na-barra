using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace PastasBarra
{
    // Janela de configuracoes moderna estilo Windows 11 Fluent UI com personalizacao visual e efeito vidro
    public class ConfiguracoesJanela : Window
    {
        readonly StackPanel _containerCards = new StackPanel();
        readonly Grid _painelPastas = new Grid();
        readonly Grid _painelAparencia = new Grid();
        readonly Border _abaPastasBtn;
        readonly Border _abaAparenciaBtn;
        readonly Border _previewCard = new Border();
        readonly TextBlock _previewTxtOpacidade = new TextBlock();

        Pasta _selecionada;
        bool _abaAparenciaAtiva = false;

        public ConfiguracoesJanela()
        {
            Title = "Pastas na barra · Configurações";
            Width = 660;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

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

            // Cabecalho arrastavel com titulo e fechar
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

            var btnFechar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var txtX = new TextBlock
            {
                Text = "\u2715",
                Foreground = Ui.CorSubtexto,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnFechar.Child = txtX;
            btnFechar.MouseEnter += (s, e) => { btnFechar.Background = Ui.Pincel("#E81123"); txtX.Foreground = Brushes.White; };
            btnFechar.MouseLeave += (s, e) => { btnFechar.Background = Brushes.Transparent; txtX.Foreground = Ui.CorSubtexto; };
            btnFechar.MouseLeftButtonUp += (s, e) => Close();
            cabecalho.Children.Add(btnFechar);

            // Abas de navegacao: [ 📁 Minhas Pastas ] e [ ✨ Aparência & Vidro ]
            var barraAbas = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 4) };
            _abaPastasBtn = CriarBotaoAba("📁  Minhas Pastas", true, () => AlternarAba(false));
            _abaAparenciaBtn = CriarBotaoAba("✨  Aparência & Vidro", false, () => AlternarAba(true));
            barraAbas.Children.Add(_abaPastasBtn);
            barraAbas.Children.Add(_abaAparenciaBtn);

            // Container do corpo (alterna entre pastas e aparencia)
            var containerCorpo = new Grid { Margin = new Thickness(16, 4, 16, 10) };
            MontarPainelPastas();
            MontarPainelAparencia();
            containerCorpo.Children.Add(_painelPastas);
            containerCorpo.Children.Add(_painelAparencia);
            _painelAparencia.Visibility = Visibility.Collapsed;

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

        void AlternarAba(bool paraAparencia)
        {
            _abaAparenciaAtiva = paraAparencia;
            _painelPastas.Visibility = paraAparencia ? Visibility.Collapsed : Visibility.Visible;
            _painelAparencia.Visibility = paraAparencia ? Visibility.Visible : Visibility.Collapsed;

            AtualizarEstiloAba(_abaPastasBtn, !paraAparencia);
            AtualizarEstiloAba(_abaAparenciaBtn, paraAparencia);
            if (paraAparencia) AtualizarPreview();
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

            // Lado esquerdo: lista de pastas
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

            // Lado direito: Botoes de acao
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

            // Lado esquerdo: Controles visuais
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
            stEstilo.Children.Add(new TextBlock { Text = "Acrílico com desfoque de fundo (Windows 11) ou acetinado sólido.", FontSize = 11, Foreground = Ui.CorSubtexto, Margin = new Thickness(0, 2, 0, 8), TextWrapping = TextWrapping.Wrap });

            var rowEstilos = new StackPanel { Orientation = Orientation.Horizontal };
            var btnVidro = Ui.Botao("💎 Vidro Acrílico (Frosted)", () =>
            {
                Repo.Dados.EstiloVisual = "Vidro";
                Repo.Salvar();
                MontarPainelAparenciaRecarregar();
            }, Repo.Dados.EstiloVisual != "Solido");

            var btnSolido = Ui.Botao("🏛 Sólido Acetinado", () =>
            {
                Repo.Dados.EstiloVisual = "Solido";
                Repo.Salvar();
                MontarPainelAparenciaRecarregar();
            }, Repo.Dados.EstiloVisual == "Solido");

            rowEstilos.Children.Add(btnVidro);
            rowEstilos.Children.Add(btnSolido);
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
            _previewTxtOpacidade.Text = Repo.Dados.OpacidadeVidro + "%";
            _previewTxtOpacidade.FontSize = 13;
            _previewTxtOpacidade.FontWeight = FontWeights.Bold;
            _previewTxtOpacidade.Foreground = Ui.CorDestaque;
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
                _previewTxtOpacidade.Text = val + "%";
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
            rowCantos.Children.Add(Ui.Botao("Compacto (12px)", () => { Repo.Dados.CantosArredondados = 12; Repo.Salvar(); MontarPainelAparenciaRecarregar(); }, Repo.Dados.CantosArredondados == 12));
            rowCantos.Children.Add(Ui.Botao("Padrão (18px)", () => { Repo.Dados.CantosArredondados = 18; Repo.Salvar(); MontarPainelAparenciaRecarregar(); }, Repo.Dados.CantosArredondados == 18));
            rowCantos.Children.Add(Ui.Botao("Suave (24px)", () => { Repo.Dados.CantosArredondados = 24; Repo.Salvar(); MontarPainelAparenciaRecarregar(); }, Repo.Dados.CantosArredondados == 24));
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

            _previewCard.Width = 240;
            _previewCard.Padding = new Thickness(14);
            _previewCard.HorizontalAlignment = HorizontalAlignment.Center;

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

        void MontarPainelAparenciaRecarregar()
        {
            _painelAparencia.Children.Clear();
            _painelAparencia.ColumnDefinitions.Clear();
            MontarPainelAparencia();
        }

        void AtualizarPreview()
        {
            _previewCard.CornerRadius = Ui.CantosPainel;
            _previewCard.Background = Ui.FundoPainel("#3B82F6");
            _previewCard.BorderBrush = Ui.BordaPainel();
            _previewCard.BorderThickness = new Thickness(1);
            _previewCard.Effect = Ui.SombraProfundidade;
        }

        Pasta Selecionada
        {
            get { return _selecionada ?? Repo.Dados.Pastas.FirstOrDefault(); }
        }

        void Recarregar()
        {
            var idSel = Selecionada != null ? Selecionada.Id : null;
            _containerCards.Children.Clear();

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
