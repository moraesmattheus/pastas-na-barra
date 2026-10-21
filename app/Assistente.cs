/*
 * Copyright 2026 Mattheus Moraes
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PastasBarra
{
    // Primeira abertura: detecta os apps instalados e sugere pastas prontas.
    public class AssistenteJanela : Window
    {
        class Modelo
        {
            public string Nome, Cor;
            public string[] Chaves;
            public Modelo(string nome, string cor, params string[] chaves) { Nome = nome; Cor = cor; Chaves = chaves; }
        }

        static readonly Modelo[] Modelos =
        {
            new Modelo("IA", "#8B5CF6", "claude", "chatgpt", "gemini", "copilot", "comet", "ollama", "antigravity", "anythingllm", "perplexity", "lm studio"),
            new Modelo("Redes Sociais", "#3B82F6", "discord", "instagram", "linkedin", "telegram", "whatsapp", "facebook", "tiktok", "twitter", "messenger", "reddit", "signal"),
            new Modelo("Jogos", "#EF4444", "steam", "epic games", "xbox", "riot", "battle.net", "forge of", "empires", "viking rise", "roblox", "minecraft", "ubisoft", "ea app", "launcher"),
            new Modelo("Google", "#10B981", "google", "chrome", "gmail", "youtube"),
            new Modelo("Microsoft", "#06B6D4", "word", "excel", "powerpoint", "outlook", "onenote", "onedrive", "publisher", "microsoft to do", "whiteboard", "teams", "access"),
            new Modelo("Dev", "#475569", "visual studio", "github", "docker", "git bash", "git cmd", "git gui", "android studio", "terminal", "powershell", "node.js", "python", "anaconda", "idle"),
            new Modelo("Mídia", "#EC4899", "spotify", "netflix", "vlc", "globoplay", "paramount", "filmes", "fotos", "reprodutor", "media player", "câmera"),
            new Modelo("Produtividade", "#F59E0B", "notion", "obsidian", "canva", "bloco de notas", "notepad", "calculadora", "captura", "calend", "notas autoadesivas", "sticky", "paint", "relógio"),
            new Modelo("Sistema", "#64748B", "gerenciador de tarefas", "configurações", "painel de controle", "explorador de arquivos", "prompt de comando", "editor do registro", "monitor de", "serviços", "gerenciamento do computador", "informações do sistema", "limpeza de disco", "executar")
        };

        static readonly string[] Ruido =
        {
            "desinstal", "uninstall", "documentation", "release notes", "faq", "ajuda", "help", "website", "homepage",
            "getting started", "user guide", "manual", "legal", "licen", "introdu", "o que há de novo", "recuva"
        };

        readonly TextBlock _status = new TextBlock();
        readonly StackPanel _lista = new StackPanel();
        readonly Border _btnCriar;
        readonly List<Tuple<Modelo, List<CheckBox>>> _grupos = new List<Tuple<Modelo, List<CheckBox>>>();

        public AssistenteJanela()
        {
            Title = "Pastas na barra · Configuração inicial";
            Width = 600;
            Height = 680;
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
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // corpo
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // rodape

            // Cabecalho
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
                Text = "Organização Inicial · Pastas na barra",
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

            // Corpo
            var corpo = new Grid { Margin = new Thickness(20, 6, 20, 10) };
            corpo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            corpo.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topo = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            topo.Children.Add(new TextBlock
            {
                Text = "Vamos organizar seus aplicativos em pastas",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto
            });
            topo.Children.Add(new TextBlock
            {
                Text = "Encontramos os programas no seu computador e sugerimos pastas prontas como no celular. Desmarque o que não quiser; você pode editar tudo depois.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 6),
                FontSize = 12.5,
                Foreground = Ui.CorSubtexto
            });
            _status.Text = "Procurando seus aplicativos...";
            _status.FontSize = 12;
            _status.Foreground = Ui.CorDestaque;
            topo.Children.Add(_status);
            corpo.Children.Add(topo);

            var rolagem = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _lista };
            Grid.SetRow(rolagem, 1);
            corpo.Children.Add(rolagem);

            // Rodape
            var rodape = new Border
            {
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 14),
                Background = Ui.CorCard
            };
            var rodapeStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var pular = Ui.Botao("Pular", () => { Repo.Dados.PrimeiraVezConcluida = true; Repo.Salvar(); Close(); });
            _btnCriar = Ui.Botao("Criar pastas selecionadas", Criar, true);
            _btnCriar.IsEnabled = false;
            _btnCriar.Opacity = 0.5;

            rodapeStack.Children.Add(pular);
            rodapeStack.Children.Add(_btnCriar);
            rodape.Child = rodapeStack;

            Grid.SetRow(cabecalho, 0);
            Grid.SetRow(corpo, 1);
            Grid.SetRow(rodape, 2);
            gridPrincipal.Children.Add(cabecalho);
            gridPrincipal.Children.Add(corpo);
            gridPrincipal.Children.Add(rodape);

            moldura.Child = gridPrincipal;
            Content = moldura;

            Loaded += (s, e) =>
            {
                Task.Run(() => Shell.ListarApps()).ContinueWith(t =>
                    Dispatcher.BeginInvoke(new Action(() => Preencher(t.IsFaulted ? new List<StartApp>() : t.Result))));
            };
        }

        void Preencher(List<StartApp> apps)
        {
            var usados = new HashSet<string>();
            int total = 0;
            var jaExistem = new HashSet<string>(Repo.Dados.Pastas.Select(p => p.Nome.ToLowerInvariant()));
            foreach (var m in Modelos)
            {
                if (jaExistem.Contains(m.Nome.ToLowerInvariant())) continue;
                var achados = apps.Where(a => !usados.Contains(a.AppId)
                    && !Ruido.Any(r => a.Nome.IndexOf(r, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    && m.Chaves.Any(c => a.Nome.IndexOf(c, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
                if (achados.Count == 0) continue;
                foreach (var a in achados) usados.Add(a.AppId);
                total += achados.Count;

                var caixas = new List<CheckBox>();

                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = Ui.CorCard,
                    BorderBrush = Ui.CorBorda,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var cardStack = new StackPanel();

                var cabGrid = new Grid();
                cabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                cabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var dot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = Ui.Pincel(m.Cor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cabGrid.Children.Add(dot);

                var cab = new CheckBox
                {
                    Content = m.Nome + "  ·  " + achados.Count + " apps",
                    IsChecked = true,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13.5,
                    Foreground = Ui.CorTexto,
                    Cursor = Cursors.Hand
                };
                Grid.SetColumn(cab, 1);
                cabGrid.Children.Add(cab);
                cardStack.Children.Add(cabGrid);

                var corpo = new WrapPanel { Margin = new Thickness(20, 8, 0, 0) };
                foreach (var a in achados)
                {
                    var cb = new CheckBox
                    {
                        Content = a.Nome,
                        Tag = a,
                        IsChecked = true,
                        Margin = new Thickness(0, 0, 14, 4),
                        Foreground = Ui.CorSubtexto,
                        FontSize = 12,
                        Cursor = Cursors.Hand
                    };
                    caixas.Add(cb);
                    corpo.Children.Add(cb);
                }
                cab.Click += (s, e) => { foreach (var c in caixas) c.IsChecked = cab.IsChecked; };
                cardStack.Children.Add(corpo);
                card.Child = cardStack;

                _lista.Children.Add(card);
                _grupos.Add(Tuple.Create(m, caixas));
            }
            _status.Text = total > 0 ? "Encontramos " + total + " apps em " + _grupos.Count + " pastas sugeridas." : "Não encontramos sugestões novas. Você pode criar pastas pelas Configurações.";
            _status.Foreground = Ui.CorSubtexto;
            _btnCriar.IsEnabled = true;
            _btnCriar.Opacity = 1;
            var sv = _lista.Parent as ScrollViewer;
            if (sv != null) sv.ScrollToTop();
        }

        void Criar()
        {
            _btnCriar.IsEnabled = false;
            _btnCriar.Opacity = 0.5;
            _status.Text = "Criando pastas...";
            _status.Foreground = Ui.CorDestaque;
            var escolhas = _grupos.Select(g => Tuple.Create(g.Item1, g.Item2.Where(c => c.IsChecked == true).Select(c => (StartApp)c.Tag).ToList()))
                                  .Where(g => g.Item2.Count > 0).ToList();
            Task.Run(() =>
            {
                var novas = new List<Pasta>();
                foreach (var g in escolhas)
                {
                    var p = new Pasta { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Nome = g.Item1.Nome, Cor = g.Item1.Cor };
                    foreach (var a in g.Item2) p.Apps.Add(Shell.NovoItem(a));
                    novas.Add(p);
                }
                return novas;
            }).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (t.IsFaulted)
                {
                    _status.Text = "Não foi possível criar as pastas. Tente de novo.";
                    _status.Foreground = Ui.Pincel("#EF4444");
                    _btnCriar.IsEnabled = true;
                    _btnCriar.Opacity = 1;
                    return;
                }
                foreach (var p in t.Result) { Repo.Dados.Pastas.Add(p); Shell.AtualizarPasta(p); }
                Repo.Dados.PrimeiraVezConcluida = true;
                Repo.Salvar();
                Close();
                if (t.Result.Count > 0) App.MostrarFixar(t.Result);
            })));
        }
    }

    // Guia para fixar: o icone de cada pasta pode ser arrastado direto para a barra de tarefas.
    public class FixarJanela : Window
    {
        readonly List<Tuple<Pasta, Border, TextBlock>> _linhas = new List<Tuple<Pasta, Border, TextBlock>>();
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        Border _badgeSep;
        TextBlock _txtSep;

        public FixarJanela(IEnumerable<Pasta> pastas)
        {
            Title = "Fixe suas pastas na barra de tarefas";
            Width = 640;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 720;
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
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // corpo
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // rodape

            // Cabecalho
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
                Text = "Fixar na Barra de Tarefas · Pastas na barra",
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

            // Corpo
            var corpo = new StackPanel { Margin = new Thickness(20, 8, 20, 12) };
            corpo.Children.Add(new TextBlock
            {
                Text = "Arraste os ícones para a barra de tarefas",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto
            });
            corpo.Children.Add(new TextBlock
            {
                Text = "Clique e arraste cada pasta para fixar lá embaixo. Dica: use o Separador visual para dividir suas pastas dos aplicativos abertos no Windows.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 14),
                FontSize = 12.5,
                Foreground = Ui.CorSubtexto
            });

            var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var p in pastas)
            {
                var pasta = p;
                var card = new Border
                {
                    Width = 110,
                    CornerRadius = new CornerRadius(12),
                    Background = Ui.CorCard,
                    BorderBrush = Ui.CorBorda,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 10, 8, 10),
                    Margin = new Thickness(6),
                    Cursor = Cursors.Hand
                };

                var item = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

                var img = new Image { Width = 64, Height = 64, Source = Mosaico(pasta), ToolTip = "Arraste para a barra de tarefas" };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                Point? inicio = null;
                img.MouseLeftButtonDown += (s, e) => inicio = e.GetPosition(null);
                img.MouseMove += (s, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed || inicio == null) return;
                    var d = e.GetPosition(null) - inicio.Value;
                    if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
                    inicio = null;
                    var dados = new DataObject(DataFormats.FileDrop, new[] { Shell.CaminhoAtalho(pasta) });
                    DragDrop.DoDragDrop(img, dados, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
                };

                var nome = new TextBlock
                {
                    Text = pasta.Nome,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 4),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Ui.CorTexto,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                var badge = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var estado = new TextBlock { FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center };
                badge.Child = estado;

                item.Children.Add(img);
                item.Children.Add(nome);
                item.Children.Add(badge);
                card.Child = item;
                wrap.Children.Add(card);

                _linhas.Add(Tuple.Create(pasta, badge, estado));
            }

            // Separador (divisor visual)
            Shell.GerarSeparador();
            var cardSep = new Border
            {
                Width = 110,
                CornerRadius = new CornerRadius(12),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 10, 8, 10),
                Margin = new Thickness(6),
                Cursor = Cursors.Hand
            };

            var itemSep = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var imgSep = new Image { Width = 64, Height = 64, Source = ImgSeparador(), ToolTip = "Arraste para separar suas pastas dos outros apps" };
            RenderOptions.SetBitmapScalingMode(imgSep, BitmapScalingMode.HighQuality);
            Point? iniSep = null;
            imgSep.MouseLeftButtonDown += (s, e) => iniSep = e.GetPosition(null);
            imgSep.MouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || iniSep == null) return;
                var d = e.GetPosition(null) - iniSep.Value;
                if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
                iniSep = null;
                var dados = new DataObject(DataFormats.FileDrop, new[] { Shell.CaminhoAtalhoSeparador });
                DragDrop.DoDragDrop(imgSep, dados, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
            };

            var nomeSep = new TextBlock
            {
                Text = "│ Separador",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto
            };

            var badgeSep = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var estadoSep = new TextBlock { FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center };
            badgeSep.Child = estadoSep;

            itemSep.Children.Add(imgSep);
            itemSep.Children.Add(nomeSep);
            itemSep.Children.Add(badgeSep);
            cardSep.Child = itemSep;
            wrap.Children.Add(cardSep);
            _badgeSep = badgeSep;
            _txtSep = estadoSep;

            corpo.Children.Add(new ScrollViewer { Content = wrap, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            // Rodape
            var rodape = new Border
            {
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 14),
                Background = Ui.CorCard
            };
            var rodapeStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var abrir = Ui.Botao("Abrir pasta dos atalhos", () => Process.Start("explorer.exe", "\"" + Repo.Atalhos + "\""));
            var pronto = Ui.Botao("Concluído", () => Close(), true);
            rodapeStack.Children.Add(abrir);
            rodapeStack.Children.Add(pronto);
            rodape.Child = rodapeStack;

            Grid.SetRow(cabecalho, 0);
            Grid.SetRow(corpo, 1);
            Grid.SetRow(rodape, 2);
            gridPrincipal.Children.Add(cabecalho);
            gridPrincipal.Children.Add(corpo);
            gridPrincipal.Children.Add(rodape);

            moldura.Child = gridPrincipal;
            Content = moldura;

            Atualizar();
            _timer.Tick += (s, e) => Atualizar();
            _timer.Start();
            Closed += (s, e) => _timer.Stop();
        }

        static BitmapImage ImgSeparador()
        {
            using (var bmp = Shell.DesenharSeparador(144))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                return bi;
            }
        }

        static BitmapImage Mosaico(Pasta p)
        {
            using (var bmp = Shell.DesenharMosaico(p, 144))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                return bi;
            }
        }

        void Atualizar()
        {
            var ids = Shell.IdsFixados();
            foreach (var l in _linhas)
            {
                bool fixada = ids.Contains(l.Item1.Id);
                l.Item2.Background = fixada ? Ui.Pincel("#2010B981") : Ui.Pincel("#10FFFFFF");
                l.Item3.Text = fixada ? "✓ Fixada" : "Arraste para a barra";
                l.Item3.Foreground = fixada ? Ui.Pincel("#10B981") : Ui.CorSubtexto;
            }
            if (_badgeSep != null && _txtSep != null)
            {
                bool fixado = Shell.SeparadorFixado();
                _badgeSep.Background = fixado ? Ui.Pincel("#2010B981") : Ui.Pincel("#10FFFFFF");
                _txtSep.Text = fixado ? "✓ Fixado" : "Arraste para a barra";
                _txtSep.Foreground = fixado ? Ui.Pincel("#10B981") : Ui.CorSubtexto;
            }
        }
    }
}
