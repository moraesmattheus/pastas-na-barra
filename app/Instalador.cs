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
        public const string Versao = "0.2.4";

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
            Width = 480; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Ui.Pincel("#FFFAFAFA");

            var pl = new StackPanel { Margin = new Thickness(24) };
            bool atualizando = File.Exists(Instalador.ExeInstalado);
            pl.Children.Add(new TextBlock { Text = atualizando ? "Atualizar Pastas na barra?" : "Instalar Pastas na barra?", FontSize = 22, FontWeight = FontWeights.SemiBold });
            pl.Children.Add(new TextBlock
            {
                Text = "Vamos copiar o programa para a sua conta (sem precisar de administrador), criar um atalho no Menu Iniciar e fazer ele abrir junto com o Windows. Você pode desinstalar depois em Configurações > Aplicativos.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16), Foreground = Brushes.Gray
            });
            var inicio = new CheckBox { Content = "Abrir junto com o Windows (deixa o painel mais r\u00e1pido)", IsChecked = true, Margin = new Thickness(0, 0, 0, 6) };
            var auto = new CheckBox { Content = "Avisar quando houver vers\u00e3o nova (consulta o GitHub; opcional, desligado por padr\u00e3o)", IsChecked = false, Margin = new Thickness(0, 0, 0, 14) };
            pl.Children.Add(inicio); pl.Children.Add(auto);
            var linha = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var so = new Button { Content = "Só executar", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = atualizando ? "Atualizar" : "Instalar", Padding = new Thickness(24, 8, 24, 8), IsDefault = true };
            linha.Children.Add(so); linha.Children.Add(ok);
            pl.Children.Add(linha);
            Content = pl;

            so.Click += (s, e) => { Repo.Dados.ModoPortatil = true; Repo.Salvar(); Close(); };
            ok.Click += (s, e) =>
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
            };
        }
    }
}
