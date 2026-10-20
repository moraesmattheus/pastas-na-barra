using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

    // Aviso de versao nova a partir das Releases do proprio repositorio no GitHub.
    // De proposito, o programa NAO baixa nem troca o proprio .exe (comportamento que antivirus tratam como malware):
    // ele so le o numero da ultima versao (texto) e, se houver uma nova, abre a pagina de download no navegador.
    // So consulta a internet se o usuario ligou a opcao (ou clicou em "Verificar agora").
    public static class Atualizador
    {
        const string Repositorio = "moraesmattheus/pastas-na-barra";
        public const string PaginaReleases = "https://github.com/" + Repositorio + "/releases/latest";

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
    }
}
