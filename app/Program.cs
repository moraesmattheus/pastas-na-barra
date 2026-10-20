using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WF = System.Windows.Forms;

namespace PastasBarra
{
    public static class App
    {
        static string NomePipe { get { return Inicio.NomePipe; } }
        static Application _app;
        static WF.NotifyIcon _tray;
        static Window _config, _assistente, _fixar;

        // Chamado por Inicio.Main depois de confirmar que esta e a instancia principal.
        public static void Executar(string abrir, bool bandeja, string[] soltos)
        {
            Repo.Carregar();
            _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _app.DispatcherUnhandledException += (s, e) =>
            {
                try { File.AppendAllText(Path.Combine(Repo.Raiz, "erro.log"), DateTime.Now + Environment.NewLine + e.Exception + Environment.NewLine + Environment.NewLine); } catch { }
                e.Handled = true;
            };

            CriarBandeja();
            new Thread(OuvirPipe) { IsBackground = true }.Start();
            Task.Run(() => { try { Shell.ListarApps(); } catch { } });

            _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool perguntar = !Instalador.RodandoInstalado() && !Repo.Dados.ModoPortatil && !bandeja && abrir == null;
                if (perguntar)
                {
                    var j = new InstalarJanela();
                    j.ShowDialog();
                    if (j.Instalou)
                    {
                        Process.Start(new ProcessStartInfo(Instalador.ExeInstalado, "--aguardar") { UseShellExecute = false });
                        _app.Shutdown();
                        return;
                    }
                }
                bool temSoltos = abrir != null && soltos != null && soltos.Length > 0;
                IniciarNormal(temSoltos ? null : abrir, bandeja || temSoltos);
                if (temSoltos) AdicionarArquivos(abrir, soltos);
            }));
            // pre-aquece o painel para o primeiro clique ser instantaneo
            _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var p0 = Repo.Dados.Pastas.FirstOrDefault();
                if (p0 != null) { try { Painel.Aquecer(p0); } catch { } }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            IniciarVerificacaoAutomatica();
            _app.Run();
            _tray.Visible = false;
            _tray.Dispose();
            if (_hTrayIcon != IntPtr.Zero) { try { DestroyIcon(_hTrayIcon); } catch { } _hTrayIcon = IntPtr.Zero; }
        }

        public static void Sair()
        {
            if (_app != null)
                _app.Dispatcher.BeginInvoke(new Action(() => _app.Shutdown()));
        }

        // ---------- atualizacao automatica (OTA) ----------
        // So verifica a internet se a opcao estiver ligada e este for o programa instalado.
        static void IniciarVerificacaoAutomatica()
        {
            var primeira = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            primeira.Tick += (s, e) => { primeira.Stop(); VerificarAutomatico(); };
            primeira.Start();
            var periodica = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            periodica.Tick += (s, e) => VerificarAutomatico();
            periodica.Start();
        }

        static void VerificarAutomatico()
        {
            if (!Repo.Dados.AtualizarAuto) return;
            if ((DateTime.UtcNow - new DateTime(Repo.Dados.UltimaChecagem, DateTimeKind.Utc)).TotalHours < 5) return;
            VerificarAtualizacao(false);
        }

        static void AoClicarNoAviso(object remetente, EventArgs e) { VerificarAtualizacao(true); }

        static void AbrirPaginaDeDownload()
        {
            try { Process.Start(new ProcessStartInfo(Atualizador.PaginaReleases) { UseShellExecute = true }); } catch { }
        }

        static void AtualizarAgora(InfoAtualizacao info)
        {
            var aguarde = new Window
            {
                Title = "Pastas na barra",
                Width = 340, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = Ui.Pincel("#1F2230"),
                Content = new TextBlock
                {
                    Text = "Baixando atualização " + info.Versao + "...\nPor favor, aguarde alguns instantes.",
                    Foreground = System.Windows.Media.Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16)
                }
            };
            aguarde.Show();
            Task.Run(() => Atualizador.BaixarEAplicar(info)).ContinueWith(t => _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                aguarde.Close();
                string erro = t.IsFaulted ? t.Exception.GetBaseException().Message : t.Result;
                if (!string.IsNullOrEmpty(erro))
                {
                    if (MessageBox.Show("Não foi possível atualizar automaticamente:\n" + erro + "\n\nAbrir a página de download no navegador?", "Atualizações", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        AbrirPaginaDeDownload();
                }
            })));
        }

        // manual = clicou em "Verificar atualizacoes agora" (mostra o resultado); automatico = so age se houver versao nova.
        public static void VerificarAtualizacao(bool manual)
        {
            Task.Run(() => Atualizador.Verificar()).ContinueWith(t => _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var r = t.IsFaulted ? new InfoAtualizacao { Erro = "Falha ao verificar." } : t.Result;
                Repo.Dados.UltimaChecagem = DateTime.UtcNow.Ticks;
                Repo.Salvar();
                if (r.Erro != null) { if (manual) MessageBox.Show("Não foi possível verificar atualizações:\n" + r.Erro, "Atualizações"); return; }
                if (r.SemRelease) { if (manual) MessageBox.Show("Ainda não há nenhuma versão publicada para atualizar.", "Atualizações"); return; }
                if (!r.Disponivel) { if (manual) MessageBox.Show("Você já está na versão mais recente (" + Instalador.Versao + ").", "Atualizações"); return; }

                if (manual)
                {
                    var msg = "Nova versão " + r.Versao + " disponível (você tem a " + Instalador.Versao + ").\n\nDeseja atualizar agora automaticamente via OTA?";
                    var resp = MessageBox.Show(msg, "Atualização do Pastas na barra", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                    if (resp == MessageBoxResult.Yes)
                    {
                        AtualizarAgora(r);
                    }
                    else if (resp == MessageBoxResult.No)
                    {
                        AbrirPaginaDeDownload();
                    }
                }
                else if (Repo.Dados.VersaoAvisada != r.Versao)
                {
                    Repo.Dados.VersaoAvisada = r.Versao;
                    Repo.Salvar();
                    _tray.BalloonTipClicked -= AoClicarNoAviso;
                    _tray.BalloonTipClicked += AoClicarNoAviso;
                    _tray.ShowBalloonTip(10000, "Pastas na barra", "Nova versão " + r.Versao + " disponível. Clique para atualizar.", WF.ToolTipIcon.Info);
                }
            })));
        }

        // Arquivos/atalhos soltos em cima do icone da pasta na barra de tarefas entram na pasta (em vez de serem fixados soltos).
        static void AdicionarArquivos(string idOuNome, IEnumerable<string> caminhos)
        {
            var p = Repo.Achar(idOuNome);
            if (p == null) return;
            foreach (var c in caminhos)
            {
                if (!File.Exists(c) && !Directory.Exists(c)) continue;
                if (p.Apps.Any(a => string.Equals(a.Alvo, c, StringComparison.OrdinalIgnoreCase))) continue;
                p.Apps.Add(Shell.NovoItemArquivo(c));
            }
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            Painel.MostrarOuAtualizar(p);
        }

        static void IniciarNormal(string abrir, bool bandeja)
        {
            if (Repo.Dados.PrimeiraVezConcluida)
                foreach (var p in Repo.Dados.Pastas) { try { Shell.AtualizarPasta(p); } catch { } }
            if (!Repo.Dados.PrimeiraVezConcluida) AbrirAssistente();
            else if (abrir != null) AbrirPasta(abrir);
            else if (!bandeja) AbrirConfiguracoes();
        }

        // ---------- janelas ----------
        static void Unica(ref Window w, Func<Window> criar)
        {
            if (w != null && w.IsVisible) { w.Activate(); return; }
            w = criar();
            w.Show();
            w.Activate();
        }

        public static void AbrirConfiguracoes() { Unica(ref _config, () => new ConfiguracoesJanela()); }
        public static void AbrirAssistente() { Unica(ref _assistente, () => new AssistenteJanela()); }

        public static void MostrarFixar(IEnumerable<Pasta> pastas)
        {
            var lista = pastas.ToList();
            if (_fixar != null && _fixar.IsVisible) _fixar.Close();
            _fixar = new FixarJanela(lista);
            _fixar.Show();
            _fixar.Activate();
        }

        static void AbrirPasta(string idOuNome)
        {
            var p = Repo.Achar(idOuNome);
            if (p == null) { AbrirConfiguracoes(); return; }
            Painel.Abrir(p);
        }

        // ---------- instancia unica: a 2a execucao so manda um recado para a 1a ----------
        static void Enviar(string msg)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", NomePipe, PipeDirection.Out))
                {
                    c.Connect(2000);
                    var w = new StreamWriter(c) { AutoFlush = true };
                    w.WriteLine(msg);
                }
            }
            catch { }
        }

        static void OuvirPipe()
        {
            while (true)
            {
                try
                {
                    using (var s = new NamedPipeServerStream(NomePipe, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances))
                    {
                        s.WaitForConnection();
                        string linha = new StreamReader(s).ReadLine();
                        if (linha == null) continue;
                        _app.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (linha == "sair") _app.Shutdown();
                            else if (linha.StartsWith("abrir|")) AbrirPasta(linha.Substring(6));
                            else if (linha.StartsWith("adicionar|"))
                            {
                                var partes = linha.Split('|');
                                AdicionarArquivos(partes[1], partes.Skip(2));
                            }
                            else AbrirConfiguracoes();
                        }));
                    }
                }
                catch { Thread.Sleep(300); }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        // ---------- icone da bandeja ----------
        static void CriarBandeja()
        {
            var menu = new WF.ContextMenuStrip();
            menu.Items.Add("Configurações", null, (s, e) => AbrirConfiguracoes());
            menu.Items.Add("Sugestões de pastas", null, (s, e) => AbrirAssistente());
            menu.Items.Add("Fixar pastas na barra", null, (s, e) => MostrarFixar(Repo.Dados.Pastas.ToList()));
            var inicio = new WF.ToolStripMenuItem("Iniciar com o Windows") { Checked = Shell.IniciarComWindows };
            inicio.Click += (s, e) => { Shell.IniciarComWindows = !Shell.IniciarComWindows; inicio.Checked = Shell.IniciarComWindows; };
            menu.Items.Add(inicio);
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Sair", null, (s, e) => _app.Shutdown());

            _tray = new WF.NotifyIcon { Icon = IconeApp(), Text = "Pastas na barra", Visible = true, ContextMenuStrip = menu };
            _tray.MouseClick += (s, e) => { if (e.Button == WF.MouseButtons.Left) AbrirConfiguracoes(); };
        }

        static IntPtr _hTrayIcon = IntPtr.Zero;

        public static Icon IconeApp()
        {
            try
            {
                var ico = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                if (ico != null) return ico;
            }
            catch { }

            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var cores = new[] { Color.FromArgb(125, 211, 252), Color.FromArgb(167, 139, 250), Color.FromArgb(251, 146, 60), Color.FromArgb(74, 222, 128) };
                for (int i = 0; i < 4; i++)
                    using (var b = new SolidBrush(cores[i]))
                        g.FillEllipse(b, 2 + (i % 2) * 15, 2 + (i / 2) * 15, 13, 13);
                _hTrayIcon = bmp.GetHicon();
                return Icon.FromHandle(_hTrayIcon);
            }
        }
    }
}
