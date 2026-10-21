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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace PastasBarra
{
    // Design System & utilitarios de interface estilo Windows 11 Fluent UI
    public static class Ui
    {
        public static readonly string[] Cores =
        {
            "#475569", "#3B82F6", "#8B5CF6", "#EC4899", "#EF4444", "#F59E0B", "#10B981", "#06B6D4"
        };

        // Detecta automaticamente se o usuario usa tema escuro ou claro no Windows
        public static bool TemaEscuro
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (k != null)
                        {
                            var v = k.GetValue("AppsUseLightTheme");
                            if (v is int val) return val == 0;
                        }
                    }
                }
                catch { }
                return true;
            }
        }

        // Paleta semantica dinamica que acompanha o tema do Windows
        public static SolidColorBrush CorTexto { get { return Pincel(TemaEscuro ? "#FFFFFF" : "#0F172A"); } }
        public static SolidColorBrush CorSubtexto { get { return Pincel(TemaEscuro ? "#94A3B8" : "#64748B"); } }
        public static SolidColorBrush CorFundoJanela { get { return Pincel(TemaEscuro ? "#FC181B24" : "#FCF8FAFC"); } }
        public static SolidColorBrush CorCard { get { return Pincel(TemaEscuro ? "#18FFFFFF" : "#0A000000"); } }
        public static SolidColorBrush CorCardHover { get { return Pincel(TemaEscuro ? "#26FFFFFF" : "#14000000"); } }
        public static SolidColorBrush CorBorda { get { return Pincel(TemaEscuro ? "#24FFFFFF" : "#1E000000"); } }
        public static SolidColorBrush CorDestaque { get { return Pincel("#3B82F6"); } }

        // Cantos arredondados configuraveis (12, 18, 24 px)
        public static CornerRadius CantosPainel
        {
            get
            {
                int r = Repo.Dados.CantosArredondados;
                if (r <= 0) r = 18;
                return new CornerRadius(r);
            }
        }

        // Borda com bisel de luz (gradiente reflexivo simulando vidro lapidado)
        public static Brush BordaPainel()
        {
            if (!Repo.Dados.BiselVidro) return CorBorda;

            var lg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            if (TemaEscuro)
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 0.5));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x12, 0x00, 0x00, 0x00), 1.0));
            }
            else
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF), 0.5));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0x00, 0x00, 0x00), 1.0));
            }
            lg.Freeze();
            return lg;
        }

        // Sombra suave de profundidade com aceleracao direta por hardware
        public static DropShadowEffect SombraProfundidade
        {
            get
            {
                return new DropShadowEffect
                {
                    BlurRadius = 34,
                    ShadowDepth = 6,
                    Opacity = TemaEscuro ? 0.45 : 0.22,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Performance
                };
            }
        }

        // Fundo do painel popup (adaptado a opacidade do vidro e tema)
        public static SolidColorBrush FundoPainel(string hex)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(hex); } catch { c = Color.FromRgb(71, 85, 105); }

            bool solido = string.Equals(Repo.Dados.EstiloVisual, "Solido", StringComparison.OrdinalIgnoreCase);
            int opacidade = solido ? 100 : Repo.Dados.OpacidadeVidro;
            if (opacidade < 50 || opacidade > 100) opacidade = 85;

            byte alpha = (byte)(opacidade * 255 / 100);

            if (TemaEscuro)
            {
                Func<byte, byte, byte> mistura = (a, b) => (byte)(a * 0.18 + b * 0.82);
                var br = new SolidColorBrush(Color.FromArgb(alpha, mistura(c.R, 0x14), mistura(c.G, 0x16), mistura(c.B, 0x22)));
                br.Freeze();
                return br;
            }
            else
            {
                Func<byte, byte, byte> mistura = (a, b) => (byte)(a * 0.10 + b * 0.90);
                var br = new SolidColorBrush(Color.FromArgb(alpha, mistura(c.R, 0xF8), mistura(c.G, 0xFA), mistura(c.B, 0xFC)));
                br.Freeze();
                return br;
            }
        }

        public static SolidColorBrush Pincel(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();

        public static BitmapImage Carregar(string png)
        {
            if (string.IsNullOrEmpty(png)) return null;
            BitmapImage pronto;
            if (_cache.TryGetValue(png, out pronto)) return pronto;
            if (!File.Exists(png)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(png);
            bi.EndInit();
            bi.Freeze();
            _cache[png] = bi;
            return bi;
        }

        // ---------- Janela Moderna Windows 11 (sem bordas duras, cantos 16px, sombra suave) ----------
        public static Window CriarJanelaModerna(Window dono, string titulo, double largura, double altura, out Grid containerCorpo, Action aoFechar = null)
        {
            var w = new Window
            {
                Title = titulo,
                Width = largura,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (!double.IsNaN(altura)) w.Height = altura;
            else w.SizeToContent = SizeToContent.Height;

            if (dono != null && dono.IsVisible) w.Owner = dono;

            var moldura = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = CorFundoJanela,
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12),
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };

            var gridPrincipal = new Grid();
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // cabecalho
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // corpo

            // Cabecalho arrastavel com titulo e botao X
            var cabecalho = new Grid { Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            cabecalho.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) w.DragMove(); };

            var tituloStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            
            // Icone 4 bolinhas
            var iconeDots = new Canvas { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0) };
            var coresDots = new[] { "#7DD3FC", "#A78BFA", "#FB923C", "#4ADE80" };
            for (int i = 0; i < 4; i++)
            {
                var el = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Pincel(coresDots[i]) };
                Canvas.SetLeft(el, (i % 2) * 7);
                Canvas.SetTop(el, (i / 2) * 7);
                iconeDots.Children.Add(el);
            }
            tituloStack.Children.Add(iconeDots);

            var tbTitulo = new TextBlock
            {
                Text = titulo,
                Foreground = CorTexto,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tituloStack.Children.Add(tbTitulo);
            cabecalho.Children.Add(tituloStack);

            // Botao Fechar (X) estilo Windows 11
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
                Foreground = CorSubtexto,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnFechar.Child = txtX;
            btnFechar.MouseEnter += (s, e) => { btnFechar.Background = Pincel("#E81123"); txtX.Foreground = Brushes.White; };
            btnFechar.MouseLeave += (s, e) => { btnFechar.Background = Brushes.Transparent; txtX.Foreground = CorSubtexto; };
            btnFechar.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (aoFechar != null) aoFechar(); else w.Close();
            };
            btnFechar.MouseLeftButtonUp += (s, e) => { if (aoFechar != null) aoFechar(); else w.Close(); };
            cabecalho.Children.Add(btnFechar);

            containerCorpo = new Grid { Margin = new Thickness(16, 4, 16, 16) };
            Grid.SetRow(cabecalho, 0);
            Grid.SetRow(containerCorpo, 1);
            gridPrincipal.Children.Add(cabecalho);
            gridPrincipal.Children.Add(containerCorpo);

            moldura.Child = gridPrincipal;
            w.Content = moldura;

            w.KeyDown += (s, e) => { if (e.Key == Key.Escape) { if (aoFechar != null) aoFechar(); else w.Close(); } };
            return w;
        }

        // Botao moderno estilo pilula (Fluent UI)
        public static Border Botao(string texto, Action acao, bool destaque = false, bool perigo = false)
        {
            SolidColorBrush bgNormal, bgHover, fgNormal;
            if (destaque)
            {
                bgNormal = Pincel("#2563EB"); bgHover = Pincel("#1D4ED8"); fgNormal = Brushes.White;
            }
            else if (perigo)
            {
                bgNormal = Pincel(TemaEscuro ? "#33E5484D" : "#1AE5484D");
                bgHover = Pincel(TemaEscuro ? "#4DE5484D" : "#2EE5484D");
                fgNormal = Pincel("#EF4444");
            }
            else
            {
                bgNormal = Pincel(TemaEscuro ? "#1FFFFFFF" : "#0A000000");
                bgHover = Pincel(TemaEscuro ? "#33FFFFFF" : "#18000000");
                fgNormal = CorTexto;
            }

            var b = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(4),
                Background = bgNormal,
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(destaque ? 0 : 1),
                Cursor = Cursors.Hand,
                Focusable = true
            };
            var t = new TextBlock
            {
                Text = texto,
                Foreground = fgNormal,
                FontSize = 12,
                FontWeight = destaque ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = t;
            b.MouseEnter += (s, e) => b.Background = bgHover;
            b.MouseLeave += (s, e) => b.Background = bgNormal;
            b.MouseLeftButtonUp += (s, e) => acao();
            b.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { acao(); e.Handled = true; } };
            return b;
        }

        // Card com cantos arredondados suave
        public static Border CriarCard(UIElement conteudo, Thickness? margem = null)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = CorCard,
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = margem ?? new Thickness(0, 0, 0, 10),
                Child = conteudo
            };
        }

        // CheckBox estilizado com suporte dinamico a temas
        public static CheckBox CaixaSelecao(string texto, bool marcado, Action<bool> aoMudar = null)
        {
            var cb = new CheckBox
            {
                Content = texto,
                IsChecked = marcado,
                Foreground = CorTexto,
                FontSize = 12.5,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 4, 0, 4)
            };
            if (aoMudar != null)
            {
                cb.Click += (s, e) => aoMudar(cb.IsChecked == true);
            }
            return cb;
        }

        public static string PedirTexto(Window dono, string titulo, string inicial)
        {
            Grid corpo;
            var w = CriarJanelaModerna(dono, titulo, 360, double.NaN, out corpo);
            var pl = new StackPanel();

            var tb = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Text = inicial ?? "",
                Background = Pincel(TemaEscuro ? "#20FFFFFF" : "#0D000000"),
                Foreground = CorTexto,
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(1)
            };
            pl.Children.Add(tb);

            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            string resp = null;
            var btnCancelar = Botao("Cancelar", () => w.Close());
            var btnOk = Botao("Salvar", () => { resp = tb.Text.Trim(); w.Close(); }, true);
            rodape.Children.Add(btnCancelar);
            rodape.Children.Add(btnOk);
            pl.Children.Add(rodape);

            corpo.Children.Add(pl);
            w.ContentRendered += (s, e) => { tb.Focus(); tb.SelectAll(); };
            w.KeyDown += (s, e) => { if (e.Key == Key.Enter) { resp = tb.Text.Trim(); w.Close(); } };
            w.ShowDialog();
            return string.IsNullOrEmpty(resp) ? null : resp;
        }

        public static string EscolherCor(Window dono)
        {
            Grid corpo;
            var w = CriarJanelaModerna(dono, "Escolha uma cor", 320, double.NaN, out corpo);
            var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4) };
            string resp = null;
            foreach (var c in Cores)
            {
                var cor = c;
                var b = new Border
                {
                    Width = 44,
                    Height = 44,
                    Margin = new Thickness(6),
                    CornerRadius = new CornerRadius(22),
                    Background = Pincel(cor),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                b.MouseEnter += (s, e) => b.BorderThickness = new Thickness(2);
                b.MouseLeave += (s, e) => b.BorderThickness = new Thickness(0);
                b.MouseLeftButtonUp += (s, e) => { resp = cor; w.Close(); };
                wrap.Children.Add(b);
            }
            corpo.Children.Add(wrap);
            w.ShowDialog();
            return resp;
        }

        public static AppItem EscolherApp(Window dono)
        {
            var apps = Shell.ListarApps();
            Grid corpo;
            var w = CriarJanelaModerna(dono, "Adicionar aplicativo", 430, 550, out corpo);

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Busca
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Lista
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Rodape

            // Campo de busca moderno com bordas arredondadas e icone
            var bordaBusca = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Pincel(TemaEscuro ? "#20FFFFFF" : "#0D000000"),
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 7, 10, 7)
            };
            var gridBusca = new Grid();
            gridBusca.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridBusca.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconeLupa = new TextBlock
            {
                Text = "🔍",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Opacity = 0.7
            };
            Grid.SetColumn(iconeLupa, 0);
            gridBusca.Children.Add(iconeLupa);

            var tb = new TextBox
            {
                FontSize = 13.5,
                Background = Brushes.Transparent,
                Foreground = CorTexto,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                CaretBrush = CorTexto
            };
            Grid.SetColumn(tb, 1);
            gridBusca.Children.Add(tb);
            bordaBusca.Child = gridBusca;

            // Lista fluida moderna dentro de um ScrollViewer
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var stackLista = new StackPanel();
            scroll.Content = stackLista;

            // Paleta de cores vibrantes para as iniciais dos apps
            var coresAvatar = new[] { "#3B82F6", "#8B5CF6", "#EC4899", "#10B981", "#F59E0B", "#06B6D4", "#6366F1" };

            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            AppItem resp = null;
            StartApp appSelecionado = null;
            var cardsRenderizados = new List<Tuple<StartApp, Border, TextBlock>>();
            int indiceSelecionado = 0;

            Action ok = () =>
            {
                if (appSelecionado != null)
                {
                    resp = Shell.NovoItem(appSelecionado);
                    w.Close();
                }
            };

            Action<int> definirSelecao = (novoIdx) =>
            {
                if (cardsRenderizados.Count == 0) { appSelecionado = null; return; }
                if (novoIdx < 0) novoIdx = 0;
                if (novoIdx >= cardsRenderizados.Count) novoIdx = cardsRenderizados.Count - 1;
                indiceSelecionado = novoIdx;

                for (int i = 0; i < cardsRenderizados.Count; i++)
                {
                    bool ativo = (i == indiceSelecionado);
                    var item = cardsRenderizados[i];
                    item.Item2.Background = ativo ? Pincel("#2563EB") : Brushes.Transparent;
                    item.Item3.Foreground = ativo ? Brushes.White : CorTexto;
                    item.Item3.FontWeight = ativo ? FontWeights.SemiBold : FontWeights.Normal;
                }
                appSelecionado = cardsRenderizados[indiceSelecionado].Item1;
                cardsRenderizados[indiceSelecionado].Item2.BringIntoView();
            };

            Action encher = () =>
            {
                stackLista.Children.Clear();
                cardsRenderizados.Clear();
                string f = tb.Text.Trim();

                var filtrados = apps.Where(a => f.Length == 0 || a.Nome.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0)
                                    .Take(80)
                                    .ToList();

                if (filtrados.Count == 0)
                {
                    var vazio = new StackPanel { Margin = new Thickness(0, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                    vazio.Children.Add(new TextBlock { Text = "🔍", FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8), Opacity = 0.5 });
                    vazio.Children.Add(new TextBlock { Text = "Nenhum aplicativo encontrado", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = CorTexto, HorizontalAlignment = HorizontalAlignment.Center });
                    vazio.Children.Add(new TextBlock { Text = "Clique em 'Procurar arquivo...' para escolher qualquer executável.", FontSize = 11.5, Foreground = CorSubtexto, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
                    stackLista.Children.Add(vazio);
                    appSelecionado = null;
                    return;
                }

                for (int i = 0; i < filtrados.Count; i++)
                {
                    var app = filtrados[i];
                    int idxAtual = i;

                    var card = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 7, 10, 7),
                        Margin = new Thickness(0, 2, 4, 2),
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand
                    };

                    var gItem = new Grid();
                    gItem.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    gItem.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Avatar moderno com primeira letra
                    string inicial = string.IsNullOrEmpty(app.Nome) ? "?" : app.Nome.Substring(0, 1).ToUpper();
                    string corBg = coresAvatar[Math.Abs(app.Nome.GetHashCode()) % coresAvatar.Length];

                    var avatar = new Border
                    {
                        Width = 28,
                        Height = 28,
                        CornerRadius = new CornerRadius(7),
                        Background = Pincel(corBg),
                        Margin = new Thickness(0, 0, 12, 0),
                        Child = new TextBlock
                        {
                            Text = inicial,
                            Foreground = Brushes.White,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    Grid.SetColumn(avatar, 0);
                    gItem.Children.Add(avatar);

                    var txtNome = new TextBlock
                    {
                        Text = app.Nome,
                        Foreground = CorTexto,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(txtNome, 1);
                    gItem.Children.Add(txtNome);
                    card.Child = gItem;

                    card.MouseEnter += (s, e) =>
                    {
                        if (indiceSelecionado != idxAtual) card.Background = CorCardHover;
                    };
                    card.MouseLeave += (s, e) =>
                    {
                        if (indiceSelecionado != idxAtual) card.Background = Brushes.Transparent;
                    };
                    card.MouseLeftButtonDown += (s, e) =>
                    {
                        definirSelecao(idxAtual);
                        if (e.ClickCount == 2) ok();
                    };

                    cardsRenderizados.Add(Tuple.Create(app, card, txtNome));
                    stackLista.Children.Add(card);
                }

                definirSelecao(0);
            };

            var procurar = Botao("Procurar arquivo...", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Escolha um programa, atalho ou arquivo",
                    Filter = "Programas e atalhos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos os arquivos|*.*",
                    DereferenceLinks = false
                };
                if (dlg.ShowDialog(w) == true) { resp = Shell.NovoItemArquivo(dlg.FileName); w.Close(); }
            });

            var btnAdicionar = Botao("Adicionar", ok, true);
            rodape.Children.Add(procurar);
            rodape.Children.Add(btnAdicionar);

            Grid.SetRow(bordaBusca, 0);
            Grid.SetRow(scroll, 1);
            Grid.SetRow(rodape, 2);
            g.Children.Add(bordaBusca);
            g.Children.Add(scroll);
            g.Children.Add(rodape);
            corpo.Children.Add(g);

            encher();
            tb.TextChanged += (s, e) => encher();

            w.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { ok(); e.Handled = true; }
                else if (e.Key == Key.Down) { definirSelecao(indiceSelecionado + 1); e.Handled = true; }
                else if (e.Key == Key.Up) { definirSelecao(indiceSelecionado - 1); e.Handled = true; }
                else if (e.Key == Key.Escape) { w.Close(); e.Handled = true; }
            };

            w.ContentRendered += (s, e) => tb.Focus();
            w.ShowDialog();
            return resp;
        }

        // Exporta qualquer elemento WPF como PNG transparente em alta definicao
        public static void SalvarElementoComoPng(FrameworkElement el, string caminho, double larguraFixa = 0, double alturaFixa = 0)
        {
            double w = larguraFixa > 0 ? larguraFixa : el.Width;
            double h = alturaFixa > 0 ? alturaFixa : el.Height;

            if (double.IsNaN(w) || w <= 0) w = 680;
            if (double.IsNaN(h) || h <= 0) h = 640;

            var tamanho = new Size(w, h);
            el.Measure(tamanho);
            el.Arrange(new Rect(new Point(0, 0), tamanho));
            el.UpdateLayout();

            int iw = Math.Max(1, (int)Math.Ceiling(w));
            int ih = Math.Max(1, (int)Math.Ceiling(h));

            var rtb = new RenderTargetBitmap(iw, ih, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(el);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            Directory.CreateDirectory(Path.GetDirectoryName(caminho));
            using (var fs = File.Create(caminho))
            {
                enc.Save(fs);
            }
        }
    }
}
