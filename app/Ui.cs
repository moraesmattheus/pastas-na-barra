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
        public static SolidColorBrush CorFundoJanela { get { return Pincel(TemaEscuro ? "#F2171922" : "#F8F8FAFC"); } }
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
                Func<byte, byte, byte> mistura = (a, b) => (byte)(a * 0.26 + b * 0.74);
                var br = new SolidColorBrush(Color.FromArgb(alpha, mistura(c.R, 0x12), mistura(c.G, 0x14), mistura(c.B, 0x1E)));
                br.Freeze();
                return br;
            }
            else
            {
                Func<byte, byte, byte> mistura = (a, b) => (byte)(a * 0.12 + b * 0.88);
                var br = new SolidColorBrush(Color.FromArgb(alpha, mistura(c.R, 0xF8), mistura(c.G, 0xFA), mistura(c.B, 0xFC)));
                br.Freeze();
                return br;
            }
        }

        // ---------- Efeito Vidro Acrilico Nativo via DWM (GPU-accelerated, 0% CPU) ----------
        enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        public static void AplicarAcrilicoNativo(Window window, string corHex = null)
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) return;

                int corArgb = 0;
                if (!string.IsNullOrEmpty(corHex))
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(corHex);
                        corArgb = (0x01 << 24) | (c.B << 16) | (c.G << 8) | c.R;
                    }
                    catch { }
                }

                var policy = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = corArgb
                };

                int size = Marshal.SizeOf(policy);
                IntPtr policyPtr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(policy, policyPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = 19, // WCA_ACCENT_POLICY
                        Data = policyPtr,
                        SizeOfData = size
                    };
                    SetWindowCompositionAttribute(handle, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(policyPtr);
                }
            }
            catch { }
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
            var w = CriarJanelaModerna(dono, "Adicionar aplicativo", 380, 520, out corpo);

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Busca
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Lista
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Rodape

            // Campo de busca moderno
            var tb = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13.5,
                Background = Pincel(TemaEscuro ? "#20FFFFFF" : "#0D000000"),
                Foreground = CorTexto,
                BorderBrush = CorBorda,
                BorderThickness = new Thickness(1)
            };

            var lb = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CorTexto,
                FontSize = 13,
                DisplayMemberPath = "Nome"
            };

            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            AppItem resp = null;
            Action ok = () => { if (lb.SelectedItem != null) { resp = Shell.NovoItem((StartApp)lb.SelectedItem); w.Close(); } };

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

            Grid.SetRow(tb, 0);
            Grid.SetRow(lb, 1);
            Grid.SetRow(rodape, 2);
            g.Children.Add(tb);
            g.Children.Add(lb);
            g.Children.Add(rodape);
            corpo.Children.Add(g);

            Action encher = () =>
            {
                string f = tb.Text.Trim();
                lb.ItemsSource = apps.Where(a => f.Length == 0 || a.Nome.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
                if (lb.Items.Count > 0) lb.SelectedIndex = 0;
            };
            encher();
            tb.TextChanged += (s, e) => encher();

            lb.MouseDoubleClick += (s, e) => ok();
            w.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && lb.SelectedItem != null) ok();
                else if (e.Key == Key.Down && lb.SelectedIndex < lb.Items.Count - 1) lb.SelectedIndex++;
                else if (e.Key == Key.Up && lb.SelectedIndex > 0) lb.SelectedIndex--;
            };

            w.ContentRendered += (s, e) => tb.Focus();
            w.ShowDialog();
            return resp;
        }
    }
}
