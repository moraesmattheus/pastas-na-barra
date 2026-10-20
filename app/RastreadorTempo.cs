using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace PastasBarra
{
    // Rastreador ultraleve de foco e tempo por eventos do Windows (0% CPU, sem polling)
    public static class RastreadorTempo
    {
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint WINEVENT_OUTOFCONTEXT = 0;

        static WinEventDelegate _proc;
        static IntPtr _hook = IntPtr.Zero;
        static DispatcherTimer _salvarTimer;

        static string _procAtual;
        static string _nomeAtual;
        static DateTime _inicioJanela = DateTime.UtcNow;
        static readonly object _trava = new object();

        // Mapeamento de nomes amigaveis
        static readonly Dictionary<string, string> NomesAmigaveis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome", "Google Chrome" },
            { "msedge", "Microsoft Edge" },
            { "firefox", "Mozilla Firefox" },
            { "brave", "Brave Browser" },
            { "opera", "Opera" },
            { "code", "Visual Studio Code" },
            { "devenv", "Visual Studio" },
            { "notion", "Notion" },
            { "slack", "Slack" },
            { "discord", "Discord" },
            { "spotify", "Spotify" },
            { "telegram", "Telegram" },
            { "whatsapp", "WhatsApp" },
            { "steam", "Steam" },
            { "obsidian", "Obsidian" },
            { "figma", "Figma" },
            { "windowsterminal", "Terminal do Windows" },
            { "powershell", "PowerShell" },
            { "cmd", "Prompt de Comando" },
            { "excel", "Microsoft Excel" },
            { "winword", "Microsoft Word" },
            { "powerpnt", "Microsoft PowerPoint" },
            { "onenote", "OneNote" },
            { "explorer", "Explorador de Arquivos" }
        };

        static readonly HashSet<string> Ignorados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PastasBarra", "LockApp", "SearchHost", "ShellExperienceHost", "StartMenuExperienceHost", "ApplicationFrameHost"
        };

        public static void Iniciar()
        {
            if (_hook != IntPtr.Zero || !Repo.Dados.RastrearTempo) return;

            _proc = AoMudarJanela;
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);

            // Timer para salvar em disco a cada 3 minutos (evita gravacao constante)
            _salvarTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
            _salvarTimer.Tick += (s, e) => SalvarParcial();
            _salvarTimer.Start();

            // Computa a janela atual na partida
            CapturarJanela(GetForegroundWindow());
        }

        public static void Parar()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
            if (_salvarTimer != null)
            {
                _salvarTimer.Stop();
                _salvarTimer = null;
            }
            ContabilizarJanelaAnterior();
            Repo.Salvar();
        }

        static void AoMudarJanela(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero || !Repo.Dados.RastrearTempo) return;
            CapturarJanela(hwnd);
        }

        static void CapturarJanela(IntPtr hwnd)
        {
            ContabilizarJanelaAnterior();

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return;

            string proc = null;
            string nome = null;

            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    proc = p.ProcessName;
                    if (Ignorados.Contains(proc)) return;

                    string amigavel;
                    if (NomesAmigaveis.TryGetValue(proc, out amigavel))
                    {
                        nome = amigavel;
                    }
                    else
                    {
                        try
                        {
                            var desc = p.MainModule != null ? p.MainModule.FileVersionInfo.FileDescription : null;
                            nome = !string.IsNullOrWhiteSpace(desc) ? desc.Trim() : proc;
                        }
                        catch
                        {
                            nome = char.ToUpper(proc[0]) + (proc.Length > 1 ? proc.Substring(1) : "");
                        }
                    }
                }
            }
            catch
            {
                return;
            }

            lock (_trava)
            {
                _procAtual = proc;
                _nomeAtual = nome;
                _inicioJanela = DateTime.UtcNow;
            }
        }

        static void ContabilizarJanelaAnterior()
        {
            lock (_trava)
            {
                if (string.IsNullOrEmpty(_procAtual)) return;

                var agora = DateTime.UtcNow;
                var duracao = agora - _inicioJanela;
                _inicioJanela = agora;

                int segundos = (int)duracao.TotalSeconds;
                if (segundos < 1) return;

                // Protecao contra inatividade: se o usuario esteve ausente mais de 3 minutos, limita o tempo a 180s
                uint ociosoMs = ObterTempoOciosoMs();
                if (ociosoMs > 180000 && segundos > 180)
                {
                    segundos = 180;
                }

                AdicionarTempo(_procAtual, _nomeAtual, segundos);
            }
        }

        static void AdicionarTempo(string proc, string nome, int segundos)
        {
            if (segundos <= 0 || string.IsNullOrEmpty(proc)) return;

            var diaHoje = DateTime.Today.ToString("yyyy-MM-dd");
            var registroDia = Repo.Dados.HistoricoUso.FirstOrDefault(d => d.Data == diaHoje);
            if (registroDia == null)
            {
                registroDia = new RegistroDiaUso { Data = diaHoje };
                Repo.Dados.HistoricoUso.Add(registroDia);
                // Limpeza automatica: mantem apenas os ultimos 7 dias para o arquivo dados.json continuar leve (<40 KB)
                if (Repo.Dados.HistoricoUso.Count > 7)
                {
                    Repo.Dados.HistoricoUso.RemoveRange(0, Repo.Dados.HistoricoUso.Count - 7);
                }
            }

            var item = registroDia.Apps.FirstOrDefault(a => string.Equals(a.Processo, proc, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                item = new ItemUsoApp { Processo = proc, Nome = nome ?? proc, Segundos = segundos };
                registroDia.Apps.Add(item);
            }
            else
            {
                item.Segundos += segundos;
                if (!string.IsNullOrEmpty(nome)) item.Nome = nome;
            }
        }

        public static void SalvarParcial()
        {
            ContabilizarJanelaAnterior();
            Repo.Salvar();
        }

        static uint ObterTempoOciosoMs()
        {
            var lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(lii);
            if (GetLastInputInfo(ref lii))
            {
                return (uint)Environment.TickCount - lii.dwTime;
            }
            return 0;
        }

        // Formata segundos em texto amigavel: "2h 45m" ou "18m"
        public static string FormatarTempo(int segundos)
        {
            if (segundos < 60) return "< 1m";
            int h = segundos / 3600;
            int m = (segundos % 3600) / 60;
            if (h > 0) return h + "h " + (m > 0 ? m + "m" : "");
            return m + "m";
        }
    }
}
