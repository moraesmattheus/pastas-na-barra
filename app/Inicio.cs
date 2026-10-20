using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PastasBarra
{
    // Ponto de entrada enxuto: nao toca em WPF. Se ja ha uma instancia rodando, so manda o recado e sai
    // (rapido, sem carregar a interface). Isso faz o clique na barra abrir o painel quase na hora.
    public static class Inicio
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string abrir = null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--abrir") abrir = args[i + 1];
            bool bandeja = args.Contains("--bandeja");

            if (args.Contains("--desinstalar") || args.Contains("--desinstalar-final"))
            {
                Desinstalar(args.Contains("--desinstalar-final"));
                return 0;
            }

            bool primeiro;
            using (var mutex = new Mutex(true, "PastasBarra.Instancia", out primeiro))
            {
                // recem-instalado: espera a copia anterior (que pediu a instalacao) terminar
                if (!primeiro && args.Contains("--aguardar")) { try { primeiro = mutex.WaitOne(6000); } catch (AbandonedMutexException) { primeiro = true; } }
                // abriu um .exe novo (sem argumentos) enquanto a versao instalada roda: pede para ela sair e assume (atualizacao)
                if (!primeiro && args.Length == 0 && !EstaNaPastaInstalada())
                {
                    Enviar("sair");
                    try { primeiro = mutex.WaitOne(6000); } catch (AbandonedMutexException) { primeiro = true; }
                }
                if (!primeiro)
                {
                    Enviar(abrir != null ? "abrir|" + abrir : "config");
                    return 0;
                }
                Rodar(abrir, bandeja);
            }
            return 0;
        }

        static bool EstaNaPastaInstalada()
        {
            string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PastasBarra");
            string atual = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            return string.Equals(atual, pasta, StringComparison.OrdinalIgnoreCase);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Rodar(string abrir, bool bandeja)
        {
            App.Executar(abrir, bandeja);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Desinstalar(bool final)
        {
            Instalador.Desinstalar(final);
        }

        static void Enviar(string msg)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", "PastasBarra.pipe", PipeDirection.Out))
                {
                    c.Connect(2000);
                    var w = new StreamWriter(c) { AutoFlush = true };
                    w.WriteLine(msg);
                }
            }
            catch { }
        }
    }
}
