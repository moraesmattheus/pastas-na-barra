using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace PastasBarra
{
    public class InfoAtualizacao
    {
        public bool Disponivel;     // existe versao mais nova
        public bool SemRelease;     // o repositorio ainda nao tem nenhuma versao publicada
        public string Versao;       // versao mais nova (ex.: "0.2.1")
        public string UrlExe, UrlHash;
        public string Erro;
    }

    // Atualizacao pela internet (OTA) a partir das Releases do proprio repositorio no GitHub.
    // Seguranca: so baixa de https://github.com/<repositorio>/releases/download/ e so aplica se o SHA-256 do arquivo
    // baixado bater com o arquivo .sha256 publicado na mesma versao. Nada e baixado se o usuario nao ligou a opcao
    // (ou clicou em "Verificar agora").
    public static class Atualizador
    {
        const string Repositorio = "moraesmattheus/pastas-na-barra";
        const string PrefixoDownload = "https://github.com/" + Repositorio + "/releases/download/";
        const long TamanhoMaximo = 60L * 1024 * 1024;

        static void PrepararRede()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static InfoAtualizacao Verificar()
        {
            var r = new InfoAtualizacao();
            try
            {
                PrepararRede();
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repositorio + "/releases/latest");
                req.UserAgent = "PastasBarra/" + Instalador.Versao;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 15000;
                string json;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                    json = sr.ReadToEnd();
                return Interpretar(json, Instalador.Versao);
            }
            catch (WebException e)
            {
                var http = e.Response as HttpWebResponse;
                if (http != null && http.StatusCode == HttpStatusCode.NotFound) { r.SemRelease = true; return r; }
                r.Erro = e.Message;
                return r;
            }
            catch (Exception e)
            {
                r.Erro = e.Message;
                return r;
            }
        }

        // Le a resposta da API do GitHub (separado para poder ser testado sem internet).
        internal static InfoAtualizacao Interpretar(string json, string versaoAtual)
        {
            var r = new InfoAtualizacao();
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (d == null || !d.ContainsKey("tag_name")) { r.Erro = "Resposta inesperada do GitHub."; return r; }
            r.Versao = ((string)d["tag_name"]).TrimStart('v', 'V');
            var assets = d.ContainsKey("assets") ? d["assets"] as System.Collections.IEnumerable : null;
            if (assets != null)
            {
                foreach (var a in assets)
                {
                    var ad = a as Dictionary<string, object>;
                    if (ad == null) continue;
                    string nome = ad.ContainsKey("name") ? ad["name"] as string : null;
                    string url = ad.ContainsKey("browser_download_url") ? ad["browser_download_url"] as string : null;
                    if (nome == "PastasBarra.exe") r.UrlExe = url;
                    else if (nome == "PastasBarra.exe.sha256") r.UrlHash = url;
                }
            }
            r.Disponivel = Maior(r.Versao, versaoAtual);
            return r;
        }

        internal static bool Maior(string nova, string atual)
        {
            Version n, a;
            if (!Version.TryParse(nova, out n) || !Version.TryParse(atual, out a)) return false;
            return n > a;
        }

        // Baixa a versao nova para a PROPRIA pasta de instalacao (nunca para a temporaria), confere o SHA-256 e troca o
        // arquivo por renomeacao (o Windows permite renomear um .exe em execucao). Nada baixado e executado de outro lugar:
        // depois da troca, o programa apenas reabre o seu proprio .exe ja instalado.
        public static bool BaixarEAplicar(InfoAtualizacao info, out string erro)
        {
            erro = null;
            string novo = Instalador.ExeInstalado + ".novo";
            string velho = Instalador.ExeInstalado + ".old";
            try
            {
                if (!Instalador.RodandoInstalado()) { erro = "Atualização automática só funciona no programa instalado."; return false; }
                if (string.IsNullOrEmpty(info.UrlExe) || string.IsNullOrEmpty(info.UrlHash))
                { erro = "A versão publicada não tem o arquivo e o hash esperados."; return false; }
                if (!info.UrlExe.StartsWith(PrefixoDownload, StringComparison.Ordinal) || !info.UrlHash.StartsWith(PrefixoDownload, StringComparison.Ordinal))
                { erro = "Endereço de download não confiável."; return false; }

                PrepararRede();
                string hashTxt;
                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "PastasBarra/" + Instalador.Versao;
                    hashTxt = wc.DownloadString(info.UrlHash);
                    if (File.Exists(novo)) File.Delete(novo);
                    wc.DownloadFile(info.UrlExe, novo);
                }
                var tam = new FileInfo(novo).Length;
                if (tam < 100 * 1024 || tam > TamanhoMaximo) { File.Delete(novo); erro = "O arquivo baixado tem um tamanho inesperado."; return false; }

                string esperado = (hashTxt ?? "").Trim().Split(' ', '\t', '\r', '\n')[0];
                string obtido;
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(novo))
                    obtido = string.Concat(sha.ComputeHash(fs).Select(b => b.ToString("x2")));
                if (!string.Equals(esperado, obtido, StringComparison.OrdinalIgnoreCase))
                { File.Delete(novo); erro = "A verificação de integridade (SHA-256) falhou. A atualização foi descartada."; return false; }

                if (File.Exists(velho)) File.Delete(velho);
                File.Move(Instalador.ExeInstalado, velho);
                try { File.Move(novo, Instalador.ExeInstalado); }
                catch { File.Move(velho, Instalador.ExeInstalado); throw; }   // desfaz se algo der errado

                Process.Start(new ProcessStartInfo(Instalador.ExeInstalado, "--aguardar --bandeja") { UseShellExecute = false });
                return true;
            }
            catch (Exception e)
            {
                erro = e.Message;
                try { if (File.Exists(novo)) File.Delete(novo); } catch { }
                return false;
            }
        }
    }
}
