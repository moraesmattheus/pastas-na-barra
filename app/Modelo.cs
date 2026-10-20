using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PastasBarra
{
    public class AppItem
    {
        public string Nome;
        public string Alvo;      // shell:AppsFolder\<AppID>  ou caminho de arquivo/atalho
        public string Args = "";
        public string Tipo;      // "shell" | "arquivo"
        public string Icone;     // png em cache
    }

    public class Pasta
    {
        public string Id;
        public string Nome;
        public string Rotulo;    // texto/sigla exibido no icone (se vazio, usa modo auto)
        public string Cor = "#475569";
        public bool MostrarNome;
        public string IcoArq;
        public string Assinatura;
        public List<AppItem> Apps = new List<AppItem>();
    }

    public class Dados
    {
        public bool PrimeiraVezConcluida;
        public bool ModoPortatil;
        public bool AtualizarAuto;      // atualizacao automatica (OTA) ligada
        public long UltimaChecagem;
        public string VersaoAvisada;    // ultima versao ja avisada (para nao repetir o aviso)
        public string EstiloVisual = "Vidro";   // "Vidro" | "Solido"
        public int OpacidadeVidro = 85;         // 70 a 100
        public bool BiselVidro = true;          // borda reflexiva com efeito bisel
        public int CantosArredondados = 18;     // 12, 18, 24
        public List<Pasta> Pastas = new List<Pasta>();
    }

    public static class Repo
    {
        public static readonly string Raiz = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PastasBarra");
        public static readonly string Icones = Path.Combine(Raiz, "icones");
        public static readonly string Atalhos = Path.Combine(Raiz, "atalhos");
        static readonly string Arq = Path.Combine(Raiz, "dados.json");

        public static Dados Dados = new Dados();

        public static void Carregar()
        {
            Directory.CreateDirectory(Icones);
            Directory.CreateDirectory(Atalhos);
            try
            {
                if (File.Exists(Arq))
                {
                    Dados = new JavaScriptSerializer().Deserialize<Dados>(File.ReadAllText(Arq)) ?? new Dados();
                    if (Dados.OpacidadeVidro < 50 || Dados.OpacidadeVidro > 100) Dados.OpacidadeVidro = 85;
                    if (Dados.CantosArredondados <= 0) Dados.CantosArredondados = 18;
                    if (string.IsNullOrEmpty(Dados.EstiloVisual)) Dados.EstiloVisual = "Vidro";
                    MigrarCores();
                }
            }
            catch
            {
                // arquivo corrompido: guarda uma copia antes de comecar vazio (para nao perder nada no proximo salvamento)
                try { File.Copy(Arq, Arq + ".corrompido-" + DateTime.Now.ToString("yyyyMMddHHmmss")); } catch { }
                Dados = new Dados();
            }
        }

        // Cores antigas (muito escuras, quase iguais) viram a paleta vibrante atual.
        static void MigrarCores()
        {
            var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "#343844", "#475569" }, { "#1E3A5F", "#3B82F6" }, { "#3B2A5E", "#8B5CF6" }, { "#5E2A4A", "#EC4899" },
                { "#4A2B2B", "#EF4444" }, { "#5E4A1E", "#F59E0B" }, { "#1F4D3A", "#10B981" }, { "#222222", "#475569" }
            };
            var porNome = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "IA", "#8B5CF6" }, { "Redes Sociais", "#3B82F6" }, { "Jogos", "#EF4444" }, { "Google", "#10B981" },
                { "Microsoft", "#06B6D4" }, { "Dev", "#475569" }, { "M\u00eddia", "#EC4899" }, { "Produtividade", "#F59E0B" }, { "Sistema", "#64748B" }
            };
            bool mudou = false;
            foreach (var p in Dados.Pastas)
            {
                string nova;
                if (p.Cor != null && mapa.TryGetValue(p.Cor, out nova))
                {
                    string porN;
                    if (porNome.TryGetValue(p.Nome ?? "", out porN)) nova = porN;
                    p.Cor = nova; mudou = true;
                }
            }
            if (mudou) Salvar();
        }

        public static void Salvar()
        {
            Directory.CreateDirectory(Raiz);
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            // grava em arquivo temporario e troca: uma queda no meio da escrita nao corrompe os dados
            string tmp = Arq + ".tmp";
            File.WriteAllText(tmp, js.Serialize(Dados));
            try
            {
                if (File.Exists(Arq)) File.Replace(tmp, Arq, null);
                else File.Move(tmp, Arq);
            }
            catch
            {
                try { File.Copy(tmp, Arq, true); File.Delete(tmp); } catch { }
            }
        }

        public static Pasta Achar(string idOuNome)
        {
            return Dados.Pastas.FirstOrDefault(p => string.Equals(p.Id, idOuNome, StringComparison.OrdinalIgnoreCase))
                ?? Dados.Pastas.FirstOrDefault(p => string.Equals(p.Nome, idOuNome, StringComparison.OrdinalIgnoreCase));
        }

        // Nome seguro para arquivo (o atalho da pasta tem o nome dela).
        public static string NomeArquivo(string nome)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nome = nome.Replace(c, '-');
            return nome.Trim().TrimEnd('.');
        }

        // Duas pastas nao podem ter o mesmo nome (o atalho de cada uma e um arquivo com esse nome).
        public static bool NomeDisponivel(string nome, Pasta ignorar)
        {
            string novo = NomeArquivo(nome);
            return !Dados.Pastas.Any(x => x != ignorar && string.Equals(NomeArquivo(x.Nome), novo, StringComparison.OrdinalIgnoreCase));
        }

        public static Pasta Criar(string nome)
        {
            var p = new Pasta { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Nome = nome };
            Dados.Pastas.Add(p);
            return p;
        }
    }
}
