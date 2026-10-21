/*
 * Copyright 2026 Mattheus Moraes
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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

        // Baixa o .exe diretamente do GitHub Releases, confere o SHA256 e substitui o arquivo local em 1 clique
        public static string BaixarEAplicar(InfoAtualizacao info)
        {
            if (string.IsNullOrEmpty(info.UrlExe)) return "URL de download do executável não encontrada.";
            try
            {
                PrepararRede();
                byte[] bytesExe;
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "PastasBarra/" + Instalador.Versao;
                    bytesExe = wc.DownloadData(info.UrlExe);
                }

                if (!string.IsNullOrEmpty(info.UrlHash))
                {
                    try
                    {
                        string conteudoHash;
                        using (var wc = new WebClient())
                        {
                            wc.Headers[HttpRequestHeader.UserAgent] = "PastasBarra/" + Instalador.Versao;
                            conteudoHash = wc.DownloadString(info.UrlHash);
                        }
                        string hashEsperado = conteudoHash.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        {
                            var h = sha.ComputeHash(bytesExe);
                            var hashCalculado = BitConverter.ToString(h).Replace("-", "").ToUpperInvariant();
                            if (!string.Equals(hashCalculado, hashEsperado, StringComparison.OrdinalIgnoreCase))
                                return "Arquivo corrompido ou adulterado (SHA256 incompatível).";
                        }
                    }
                    catch (Exception ex)
                    {
                        return "Erro ao validar hash: " + ex.Message;
                    }
                }

                string destino = Instalador.RodandoInstalado() ? Instalador.ExeInstalado : Instalador.ExeAtual;
                string dir = Path.GetDirectoryName(destino);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string backup = destino + ".old";
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                File.Move(destino, backup);
                File.WriteAllBytes(destino, bytesExe);

                Process.Start(new ProcessStartInfo(destino, "--aguardar") { UseShellExecute = false });
                App.Sair();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
