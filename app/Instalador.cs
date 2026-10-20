using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace PastasBarra
{
    // Instalacao por usuario (sem administrador): copia o .exe para %LOCALAPPDATA%\Programs\PastasBarra,
    // cria o atalho no Menu Iniciar e registra o desinstalador em "Aplicativos instalados".
    public static class Instalador
    {
        const string ChaveUn = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PastasBarra";
        const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string Versao = "0.3.4";

        public static readonly string Pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PastasBarra");
        public static string ExeInstalado { get { return Path.Combine(Pasta, "PastasBarra.exe"); } }
        public static string ExeAtual { get { return Process.GetCurrentProcess().MainModule.FileName; } }
        static string AtalhoMenu { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pastas na barra.lnk"); } }

        public static bool RodandoInstalado()
        {
            return string.Equals(Path.GetDirectoryName(ExeAtual), Pasta, StringComparison.OrdinalIgnoreCase);
        }

        public static bool Instalar(bool iniciarComWindows, out string erro)
        {
            erro = null;
            try
            {
                Directory.CreateDirectory(Pasta);
                File.Copy(ExeAtual, ExeInstalado, true);

                Shell.CriarAtalho(AtalhoMenu, ExeInstalado, "", ExeInstalado + ",0", "Pastas de apps na barra de tarefas");

                using (var k = Registry.CurrentUser.CreateSubKey(ChaveUn))
                {
                    k.SetValue("DisplayName", "Pastas na barra");
                    k.SetValue("DisplayVersion", Versao);
                    k.SetValue("Publisher", "Mattheus Moraes");
                    k.SetValue("DisplayIcon", ExeInstalado);
                    k.SetValue("InstallLocation", Pasta);
                    k.SetValue("UninstallString", "\"" + ExeInstalado + "\" --desinstalar");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("HelpLink", "https://github.com/moraesmattheus/pastas-na-barra");
                    k.SetValue("URLInfoAbout", "https://github.com/moraesmattheus/pastas-na-barra");
                    try
                    {
                        var fi = new FileInfo(ExeInstalado);
                        k.SetValue("EstimatedSize", (int)(fi.Length / 1024), RegistryValueKind.DWord);
                    }
                    catch { }
                }
                if (iniciarComWindows)
                    using (var k = Registry.CurrentUser.CreateSubKey(ChaveRun))
                        k.SetValue("PastasBarra", "\"" + ExeInstalado + "\" --bandeja");
                return true;
            }
            catch (Exception e)
            {
                erro = e.Message;
                return false;
            }
        }

        // ---------- desinstalar ----------
        // Roda no proprio lugar (sem se copiar para a pasta temporaria): remove tudo que nao esta em uso; o proprio
        // .exe (em execucao) fica para o usuario apagar.
        public static void Desinstalar()
        {
            if (!RodandoInstalado())
            {
                MessageBox.Show("O Pastas na barra não está instalado nesta cópia.", "Desinstalar");
                return;
            }
            if (MessageBox.Show("Remover o Pastas na barra deste computador?", "Desinstalar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            bool apagarDados = MessageBox.Show("Apagar também suas pastas e configurações?\n(Escolha Não para manter, caso volte a instalar.)",
                "Desinstalar", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

            PedirParaSair();
            try { using (var k = Registry.CurrentUser.OpenSubKey(ChaveRun, true)) if (k != null) k.DeleteValue("PastasBarra", false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(ChaveUn, false); } catch { }
            try { File.Delete(AtalhoMenu); Shell.NotificarShell(); } catch { }
            string eu = ExeAtual;
            try
            {
                foreach (var f in Directory.GetFiles(Pasta))
                    if (!string.Equals(f, eu, StringComparison.OrdinalIgnoreCase)) { try { File.Delete(f); } catch { } }
                foreach (var d in Directory.GetDirectories(Pasta)) { try { Directory.Delete(d, true); } catch { } }
            }
            catch { }
            if (apagarDados) { try { Directory.Delete(Repo.Raiz, true); } catch { } }

            MessageBox.Show("Pastas na barra foi removido.\n\nDesafixe os ícones das pastas da barra de tarefas (botão direito > Desafixar).\n\nSó este arquivo (o programa que está terminando) ficou na pasta de instalação; apague a pasta quando quiser.", "Desinstalar");
            try { Process.Start("explorer.exe", "/select,\"" + eu + "\""); } catch { }
        }

        static void PedirParaSair()
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", Inicio.NomePipe, PipeDirection.Out))
                {
                    c.Connect(1500);
                    var w = new StreamWriter(c) { AutoFlush = true };
                    w.WriteLine("sair");
                }
                Thread.Sleep(800);
            }
            catch { }
        }
    }

    // Pergunta na primeira execucao (fora da pasta de instalacao).
    public class InstalarJanela : Window
    {
        public bool Instalou;

        public InstalarJanela()
        {
            Title = "Pastas na barra";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };

            var moldura = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = Ui.CorFundoJanela,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };

            var gridPrincipal = new Grid();
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // cabecalho
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // corpo
            gridPrincipal.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // rodape

            // Cabecalho
            var cabecalho = new Grid { Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.SizeAll };
            cabecalho.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };

            var tituloStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var iconeDots = new Canvas { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0) };
            var coresDots = new[] { "#7DD3FC", "#A78BFA", "#FB923C", "#4ADE80" };
            for (int i = 0; i < 4; i++)
            {
                var el = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Ui.Pincel(coresDots[i]) };
                Canvas.SetLeft(el, (i % 2) * 7);
                Canvas.SetTop(el, (i / 2) * 7);
                iconeDots.Children.Add(el);
            }
            tituloStack.Children.Add(iconeDots);
            tituloStack.Children.Add(new TextBlock
            {
                Text = "Instalação · Pastas na barra",
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
                Cursor = System.Windows.Input.Cursors.Hand
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
            var corpo = new StackPanel { Margin = new Thickness(22, 10, 22, 14) };
            bool atualizando = File.Exists(Instalador.ExeInstalado);
            corpo.Children.Add(new TextBlock
            {
                Text = atualizando ? "Atualizar Pastas na barra" : "Organize sua barra de tarefas",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Ui.CorTexto
            });
            corpo.Children.Add(new TextBlock
            {
                Text = "Instalação leve para o seu usuário (sem precisar de senha de administrador). Você pode desinstalar quando quiser em Configurações do Windows.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 14),
                FontSize = 12.5,
                Foreground = Ui.CorSubtexto
            });

            var cardDestaques = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Ui.CorCard,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var stDestaques = new StackPanel();
            Action<string> addDestaque = (txt) =>
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                row.Children.Add(new TextBlock { Text = "✓", Foreground = Ui.Pincel("#10B981"), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 8, 0) });
                row.Children.Add(new TextBlock { Text = txt, Foreground = Ui.CorTexto, FontSize = 12 });
                stDestaques.Children.Add(row);
            };
            addDestaque("Agrupamento de apps e pastas estilo smartphone");
            addDestaque("Busca instantânea ao vivo enquanto você digita");
            addDestaque("100% nativo, sem navegadores embutidos, ultraleve");
            cardDestaques.Child = stDestaques;
            corpo.Children.Add(cardDestaques);

            var inicio = Ui.CaixaSelecao("Abrir junto com o Windows (deixa a resposta instantânea)", true);
            var auto = Ui.CaixaSelecao("Avisar quando houver nova versão (consulta o GitHub)", false);
            corpo.Children.Add(inicio);
            corpo.Children.Add(auto);

            // Rodape
            var rodape = new Border
            {
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 12, 16, 14),
                Background = Ui.CorCard
            };
            var rodapeStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var so = Ui.Botao("Só executar", () => { Repo.Dados.ModoPortatil = true; Repo.Salvar(); Close(); });
            var ok = Ui.Botao(atualizando ? "Atualizar agora" : "Instalar", () =>
            {
                string erro;
                if (Instalador.Instalar(inicio.IsChecked == true, out erro))
                {
                    Repo.Dados.AtualizarAuto = auto.IsChecked == true;
                    Repo.Salvar();
                    Instalou = true;
                    Close();
                }
                else MessageBox.Show("Não foi possível instalar:\n" + erro, "Pastas na barra");
            }, true);

            rodapeStack.Children.Add(so);
            rodapeStack.Children.Add(ok);
            rodape.Child = rodapeStack;

            Grid.SetRow(cabecalho, 0);
            Grid.SetRow(corpo, 1);
            Grid.SetRow(rodape, 2);
            gridPrincipal.Children.Add(cabecalho);
            gridPrincipal.Children.Add(corpo);
            gridPrincipal.Children.Add(rodape);

            moldura.Child = gridPrincipal;
            Content = moldura;
        }
    }
}
