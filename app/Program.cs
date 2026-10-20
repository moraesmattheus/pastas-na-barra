using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WF = System.Windows.Forms;

namespace PastasBarra
{
    public static class App
    {
        const string NomePipe = "PastasBarra.pipe";
        static Application _app;
        static WF.NotifyIcon _tray;
        static Window _config, _assistente, _fixar;

        // Chamado por Inicio.Main depois de confirmar que esta e a instancia principal.
        public static void Executar(string abrir, bool bandeja)
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
            Task.Run(() => Shell.ListarApps());   // aquece a lista de apps

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
                IniciarNormal(abrir, bandeja);
            }));
            // pre-aquece o painel para o primeiro clique ser instantaneo
            _app.Dispatcher.BeginInvoke(new Action(() =>
            {
                var p0 = Repo.Dados.Pastas.FirstOrDefault();
                if (p0 != null) { try { Painel.Aquecer(p0); } catch { } }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            _app.Run();
            _tray.Visible = false;
            _tray.Dispose();
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
                    using (var s = new NamedPipeServerStream(NomePipe, PipeDirection.In))
                    {
                        s.WaitForConnection();
                        string linha = new StreamReader(s).ReadLine();
                        if (linha == null) continue;
                        _app.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (linha == "sair") _app.Shutdown();
                            else if (linha.StartsWith("abrir|")) AbrirPasta(linha.Substring(6));
                            else AbrirConfiguracoes();
                        }));
                    }
                }
                catch { Thread.Sleep(300); }
            }
        }

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

        public static Icon IconeApp()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var cores = new[] { Color.FromArgb(125, 211, 252), Color.FromArgb(167, 139, 250), Color.FromArgb(251, 146, 60), Color.FromArgb(74, 222, 128) };
                for (int i = 0; i < 4; i++)
                    using (var b = new SolidBrush(cores[i]))
                        g.FillEllipse(b, 2 + (i % 2) * 15, 2 + (i / 2) * 15, 13, 13);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
