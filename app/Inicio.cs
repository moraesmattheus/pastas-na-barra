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
        // nome do canal por usuario (evita conflito entre contas no mesmo PC)
        public static readonly string NomePipe = "PastasBarra.pipe." + Environment.UserName;

        [STAThread]
        public static int Main(string[] args)
        {
            string abrir = null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--abrir") abrir = args[i + 1];
            // arquivos soltos em cima do icone da pasta na barra: o Windows os anexa depois dos argumentos do atalho
            var soltos = new System.Collections.Generic.List<string>();
            if (abrir != null)
                for (int i = Array.IndexOf(args, abrir) + 1; i < args.Length; i++)
                    if (!args[i].StartsWith("--")) soltos.Add(args[i]);
            bool bandeja = args.Contains("--bandeja");

            if (args.Contains("--desinstalar"))
            {
                Desinstalar();
                return 0;
            }

            bool primeiro;
            using (var mutex = new Mutex(true, "PastasBarra.Instancia", out primeiro))
            {
                // recem-instalado: espera a copia anterior (que pediu a instalacao) terminar
                if (!primeiro && args.Contains("--aguardar")) { try { primeiro = mutex.WaitOne(6000); } catch (AbandonedMutexException) { primeiro = true; } }
                // abriu um .exe novo (sem argumentos) enquanto a versao instalada roda: pede para ela sair e assume (atualizacao)
                if (!primeiro && args.Length == 0 && InstaladaDiferente())
                {
                    Enviar("sair");
                    Enviar("sair", "PastasBarra.pipe");   // versoes antigas usavam este nome de canal
                    try { primeiro = mutex.WaitOne(6000); } catch (AbandonedMutexException) { primeiro = true; }
                }
                if (!primeiro)
                {
                    if (abrir != null && soltos.Count > 0) Enviar("adicionar|" + abrir + "|" + string.Join("|", soltos));
                    else Enviar(abrir != null ? "abrir|" + abrir : "config");
                    return 0;
                }
                Rodar(abrir, bandeja, soltos.ToArray());
            }
            return 0;
        }

        // So e "atualizacao" se existe uma versao instalada E este .exe e diferente dela (outro tamanho ou data).
        // Assim, abrir duas vezes o mesmo programa nao derruba a copia que ja esta rodando.
        static bool InstaladaDiferente()
        {
            try
            {
                string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PastasBarra");
                string inst = Path.Combine(pasta, "PastasBarra.exe");
                string atual = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                if (!File.Exists(inst) || string.Equals(Path.GetDirectoryName(atual), pasta, StringComparison.OrdinalIgnoreCase)) return false;
                var a = new FileInfo(atual); var b = new FileInfo(inst);
                return a.Length != b.Length || a.LastWriteTimeUtc != b.LastWriteTimeUtc;
            }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Rodar(string abrir, bool bandeja, string[] soltos)
        {
            App.Executar(abrir, bandeja, soltos);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Desinstalar()
        {
            Instalador.Desinstalar();
        }

        static void Enviar(string msg, string pipe = null)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", pipe ?? NomePipe, PipeDirection.Out))
                {
                    c.Connect(pipe == null ? 2000 : 300);
                    var w = new StreamWriter(c) { AutoFlush = true };
                    w.WriteLine(msg);
                }
            }
            catch { }
        }
    }
}
