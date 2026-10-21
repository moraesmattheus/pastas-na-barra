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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace PastasBarra
{
    public class StartApp
    {
        public string Nome { get; set; }
        public string AppId { get; set; }
        public string CaminhoArquivo { get; set; }
    }

    public static class Shell
    {
        // ---------- icones do shell (arquivo, atalho ou shell:AppsFolder\...) ----------
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

        public static Bitmap Icone(string caminho, int tam)
        {
            try
            {
                Guid iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                IShellItemImageFactory f;
                SHCreateItemFromParsingName(caminho, IntPtr.Zero, ref iid, out f);
                if (f == null) return null;
                IntPtr hbm = IntPtr.Zero;
                try
                {
                    if (f.GetImage(new SIZE(tam, tam), 0, out hbm) != 0 || hbm == IntPtr.Zero) return null;
                    using (Bitmap src = Image.FromHbitmap(hbm))
                    {
                        Rectangle r = new Rectangle(0, 0, src.Width, src.Height);
                        BitmapData d = src.LockBits(r, ImageLockMode.ReadOnly, src.PixelFormat);
                        try
                        {
                            if (src.PixelFormat != PixelFormat.Format32bppArgb && src.PixelFormat != PixelFormat.Format32bppRgb)
                                return new Bitmap(src);
                            using (Bitmap viaAlpha = new Bitmap(src.Width, src.Height, d.Stride, PixelFormat.Format32bppArgb, d.Scan0))
                                return new Bitmap(viaAlpha);
                        }
                        finally { src.UnlockBits(d); }
                    }
                }
                finally
                {
                    if (hbm != IntPtr.Zero) DeleteObject(hbm);
                    Marshal.ReleaseComObject(f);
                }
            }
            catch { return null; }
        }

        static string Seguro(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString().Trim('-');
        }

        static string SalvarIcone(string alvo, string nome)
        {
            string png = Path.Combine(Repo.Icones, "app-" + Seguro(nome) + "-" + (uint)alvo.GetHashCode() + ".png");
            if (File.Exists(png)) return png;
            using (var bmp = Icone(alvo, 96))
            {
                if (bmp == null) return null;
                try
                {
                    bmp.Save(png, ImageFormat.Png);
                    return png;
                }
                catch { return null; }
            }
        }

        // ---------- apps instalados (lista nativa do shell: a mesma "Todos os apps" do Windows) ----------
        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int GetParent(out IShellItem ppsi);
            [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
            [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IEnumShellItems
        {
            [PreserveSig] int Next(uint celt, out IShellItem rgelt, out uint pceltFetched);
            [PreserveSig] int Skip(uint celt);
            [PreserveSig] int Reset();
            [PreserveSig] int Clone(out IEnumShellItems ppenum);
        }

        [DllImport("shell32.dll", PreserveSig = false)]
        static extern void SHGetKnownFolderItem(ref Guid rfid, int flags, IntPtr hToken, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        static string Nome(IShellItem item, uint sigdn)
        {
            IntPtr p;
            if (item.GetDisplayName(sigdn, out p) != 0 || p == IntPtr.Zero) return null;
            string s = Marshal.PtrToStringUni(p);
            Marshal.FreeCoTaskMem(p);
            return s;
        }

        static List<StartApp> _apps;

        public static List<StartApp> ListarApps()
        {
            if (_apps != null) return _apps;
            var lista = new List<StartApp>();
            var nomesRegistrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Atalhos de programas instalados no Menu Iniciar e Desktop
            try
            {
                var pastasAtalhos = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                };

                foreach (var pasta in pastasAtalhos)
                {
                    if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) continue;
                    try
                    {
                        var arquivos = Directory.GetFiles(pasta, "*.lnk", SearchOption.AllDirectories);
                        foreach (var f in arquivos)
                        {
                            try
                            {
                                string nomeSemExt = Path.GetFileNameWithoutExtension(f);
                                if (string.IsNullOrEmpty(nomeSemExt)) continue;
                                if (nomesRegistrados.Add(nomeSemExt))
                                {
                                    lista.Add(new StartApp
                                    {
                                        Nome = nomeSemExt.Trim(),
                                        AppId = f,
                                        CaminhoArquivo = f
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Aplicativos UWP da Windows Store e registrados via shell:AppsFolder
            try
            {
                Guid appsFolder = new Guid("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
                Guid iidItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
                Guid bhidEnum = new Guid("94f60519-2850-4924-aa5a-d15e84868039");
                Guid iidEnum = new Guid("70629033-e363-4a28-a567-0db78006e6d7");
                IShellItem pasta;
                SHGetKnownFolderItem(ref appsFolder, 0, IntPtr.Zero, ref iidItem, out pasta);
                IntPtr ppv;
                if (pasta.BindToHandler(IntPtr.Zero, ref bhidEnum, ref iidEnum, out ppv) == 0)
                {
                    var en = (IEnumShellItems)Marshal.GetObjectForIUnknown(ppv);
                    IShellItem item; uint n;
                    while (en.Next(1, out item, out n) == 0 && n == 1)
                    {
                        string nome = Nome(item, 0);            // SIGDN_NORMALDISPLAY
                        string id = Nome(item, 0x80018001);     // SIGDN_PARENTRELATIVEPARSING = AppUserModelID
                        if (!string.IsNullOrEmpty(nome) && !string.IsNullOrEmpty(id))
                        {
                            if (nomesRegistrados.Add(nome.Trim()))
                            {
                                lista.Add(new StartApp { Nome = nome.Trim(), AppId = id });
                            }
                        }
                    }
                }
            }
            catch { }

            _apps = lista.OrderBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
            return _apps;
        }

        // ---------- itens soltos no painel (arquivo, atalho, pasta ou app do Menu Iniciar) ----------
        [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemArray
        {
            [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
            [PreserveSig] int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
            [PreserveSig] int GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig] int GetCount(out uint pdwNumItems);
            [PreserveSig] int GetItemAt(uint dwIndex, out IShellItem ppsi);
            [PreserveSig] int EnumItems(out IntPtr ppenumShellItems);
        }

        [DllImport("shell32.dll", PreserveSig = false)]
        static extern void SHCreateShellItemArrayFromDataObject(System.Runtime.InteropServices.ComTypes.IDataObject pdo, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppv);

        // Converte o que foi solto em itens. Primeiro tenta a lista de arquivos; se nao houver (ex.: arrastar do Menu Iniciar),
        // pergunta ao shell do Windows quais itens estao no pacote.
        public static List<AppItem> ItensDoDrop(System.Windows.IDataObject dados)
        {
            var itens = new List<AppItem>();
            var arquivos = dados.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (arquivos != null)
            {
                foreach (var f in arquivos) itens.Add(NovoItemArquivo(f));
                return itens;
            }
            try
            {
                var com = dados as System.Runtime.InteropServices.ComTypes.IDataObject;
                if (com == null) return itens;
                Guid iid = new Guid("b63ea76d-1f85-456f-a19c-48159efa858b");
                IShellItemArray arr;
                SHCreateShellItemArrayFromDataObject(com, ref iid, out arr);
                uint n; arr.GetCount(out n);
                for (uint i = 0; i < n; i++)
                {
                    IShellItem it;
                    if (arr.GetItemAt(i, out it) != 0) continue;
                    string nome = Nome(it, 0);               // nome de exibicao
                    string abs = Nome(it, 0x80028000);       // SIGDN_DESKTOPABSOLUTEPARSING
                    string rel = Nome(it, 0x80018001);       // SIGDN_PARENTRELATIVEPARSING
                    if (!string.IsNullOrEmpty(abs) && (File.Exists(abs) || Directory.Exists(abs)))
                        itens.Add(NovoItemArquivo(abs));
                    else if (!string.IsNullOrEmpty(abs) && abs.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                        itens.Add(new AppItem { Nome = nome, Alvo = abs, Tipo = "shell", Icone = SalvarIcone(abs, nome) });
                    else if (!string.IsNullOrEmpty(rel) && !string.IsNullOrEmpty(nome) && rel.IndexOf('!') > 0)
                        itens.Add(NovoItem(new StartApp { Nome = nome, AppId = rel }));      // app da Loja (AppUserModelID)
                    else
                        Registrar("Drop nao reconhecido: nome=" + nome + " | abs=" + abs + " | rel=" + rel);
                }
            }
            catch (Exception e) { Registrar("Drop falhou: " + e.Message); }
            return itens;
        }

        static void Registrar(string texto)
        {
            try { File.AppendAllText(Path.Combine(Repo.Raiz, "erro.log"), DateTime.Now + " " + texto + Environment.NewLine); } catch { }
        }

        public static AppItem NovoItem(StartApp a)
        {
            if (!string.IsNullOrEmpty(a.CaminhoArquivo) && (File.Exists(a.CaminhoArquivo) || Directory.Exists(a.CaminhoArquivo)))
            {
                return NovoItemArquivo(a.CaminhoArquivo);
            }
            string alvo = "shell:AppsFolder\\" + a.AppId;
            return new AppItem { Nome = a.Nome, Alvo = alvo, Tipo = "shell", Icone = SalvarIcone(alvo, a.Nome) };
        }

        public static AppItem NovoItemArquivo(string caminho)
        {
            string nome = Path.GetFileNameWithoutExtension(caminho);
            return new AppItem { Nome = nome, Alvo = caminho, Tipo = "arquivo", Icone = SalvarIcone(caminho, nome) };
        }

        public static void Abrir(AppItem a)
        {
            try
            {
                if (a.Tipo == "shell")
                {
                    Process.Start("explorer.exe", a.Alvo);
                }
                else
                {
                    var psi = new ProcessStartInfo(a.Alvo)
                    {
                        UseShellExecute = true,
                        Arguments = a.Args ?? ""
                    };
                    try
                    {
                        if (File.Exists(a.Alvo)) psi.WorkingDirectory = Path.GetDirectoryName(a.Alvo);
                    }
                    catch { }
                    Process.Start(psi);
                }
            }
            catch { }
        }

        // ---------- app aberto? (pontinho) ----------
        // Usa so o NOME dos processos que tem janela (nao le titulos de janelas nem conteudo: privacidade).
        static HashSet<string> _procs = new HashSet<string>();

        static readonly Dictionary<string, string> _aliasProc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "blocodenotas", "notepad" },
            { "calculadora", "calculator" },
            { "ferramentadecaptura", "snippingtool" },
            { "captura", "snippingtool" },
            { "gerenciadordetarefas", "taskmgr" },
            { "configuraes", "systemsettings" },
            { "configuracoes", "systemsettings" },
            { "exploradordearquivos", "explorer" },
            { "reprodutordemidia", "wmplayer" },
            { "promptdecomando", "cmd" },
            { "editordoregistro", "regedit" },
            { "cmera", "windowscamera" },
            { "camera", "windowscamera" },
            { "gravadordevoz", "soundrecorder" },
            { "fotos", "microsoftphotos" }
        };

        static string Norm(string s)
        {
            return new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        public static void AtualizarProcessos()
        {
            var l = new HashSet<string>();
            foreach (var p in Process.GetProcesses())
            {
                try { if (p.MainWindowHandle != IntPtr.Zero) l.Add(Norm(p.ProcessName)); }
                catch { }
                finally { p.Dispose(); }
            }
            _procs = l;
        }

        public static bool Rodando(AppItem a)
        {
            string n = Norm(a.Nome);
            string fn = "";
            try { if (!string.IsNullOrEmpty(a.Alvo) && a.Tipo == "arquivo") fn = Norm(Path.GetFileNameWithoutExtension(a.Alvo)); } catch { }
            string alias;
            _aliasProc.TryGetValue(n, out alias);

            foreach (var proc in _procs)
            {
                if (proc == n) return true;
                if (!string.IsNullOrEmpty(fn) && (proc == fn || (fn.Length >= 4 && proc.Length >= 4 && (proc.Contains(fn) || fn.Contains(proc))))) return true;
                if (!string.IsNullOrEmpty(alias) && (proc == alias || proc.Contains(alias) || alias.Contains(proc))) return true;
                if (n.Length >= 4 && proc.Length >= 4 && (proc.Contains(n) || n.Contains(proc))) return true;
            }
            return false;
        }

        // ---------- icone-mosaico da pasta + atalho que se fixa na barra ----------
        public static string CaminhoIcone(Pasta p)
        {
            return Path.Combine(Repo.Icones, string.IsNullOrEmpty(p.IcoArq) ? "_pasta-" + p.Id + ".ico" : p.IcoArq);
        }

        public static string CaminhoAtalho(Pasta p)
        {
            return Path.Combine(Repo.Atalhos, Repo.NomeArquivo(p.Nome) + ".lnk");
        }

        public static string ObterTextoIcone(Pasta p)
        {
            if (!string.IsNullOrEmpty(p.Rotulo)) return p.Rotulo.Trim();
            string n = (p.Nome ?? "").Trim();
            if (n.Length <= 4) return n;

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Produtividade", "PROD" },
                { "Produtivos", "PROD" },
                { "Desenvolvimento", "DEV" },
                { "Programação", "DEV" },
                { "Programacao", "DEV" },
                { "Sistema", "SIST" },
                { "Configurações", "CONF" },
                { "Configuracoes", "CONF" },
                { "Ferramentas", "UTIL" },
                { "Utilitários", "UTIL" },
                { "Utilitarios", "UTIL" },
                { "Redes Sociais", "REDES" },
                { "Social", "SOC" },
                { "Comunicação", "CHAT" },
                { "Comunicacao", "CHAT" },
                { "Inteligência Artificial", "IA" },
                { "Inteligencia Artificial", "IA" },
                { "Multimídia", "MIDIA" },
                { "Multimidia", "MIDIA" },
                { "Navegadores", "WEB" },
                { "Arquivos", "DOCS" },
                { "Documentos", "DOCS" },
                { "Microsoft", "MSFT" },
                { "Google", "GOOG" }
            };

            string alias;
            if (aliases.TryGetValue(n, out alias)) return alias;

            if (n.Contains(" "))
            {
                var partes = n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (partes[0].Length <= 5) return partes[0];
                return partes[0].Substring(0, 4);
            }

            if (n.Length > 5) return n.Substring(0, 4);
            return n;
        }

        // Desenha o mosaico 3x3 (e, se ligado, o nome embaixo) em um quadrado de t x t pixels.
        public static Bitmap DesenharMosaico(Pasta p, int t)
        {
            var bmp = new Bitmap(t, t, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                bool nome = p.MostrarNome && t >= 24;
                float s = nome ? t * 0.52f : t;                 // mosaico fica em 52% dando bastante espaco para o nome
                float x0 = (t - s) / 2f, y0 = 0f;
                float m = s * 0.03f, w = s - 2 * m, r = s * 0.22f;
                var path = new GraphicsPath();
                path.AddArc(x0 + m, y0 + m, r, r, 180, 90); path.AddArc(x0 + m + w - r, y0 + m, r, r, 270, 90);
                path.AddArc(x0 + m + w - r, y0 + m + w - r, r, r, 0, 90); path.AddArc(x0 + m, y0 + m + w - r, r, r, 90, 90);
                path.CloseFigure();
                Color cor;
                try { cor = ColorTranslator.FromHtml(p.Cor); } catch { cor = Color.FromArgb(71, 85, 105); }
                using (var br = new SolidBrush(Color.FromArgb(245, cor))) g.FillPath(br, path);
                float cel = s * 0.22f, esp = s * 0.04f, ini = (s - (3 * cel + 2 * esp)) / 2;
                var icones = p.Apps.Where(a => a.Icone != null && File.Exists(a.Icone)).Take(9).ToList();
                for (int i = 0; i < icones.Count; i++)
                {
                    float x = x0 + ini + (i % 3) * (cel + esp), y = y0 + ini + (i / 3) * (cel + esp);
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(icones[i].Icone);
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                            g.DrawImage(img, x, y, cel, cel);
                    }
                    catch { }
                }
                if (nome)
                {
                    float yTexto = y0 + s + (t * 0.02f);
                    float altura = t - yTexto;
                    float largura = t * 0.98f;
                    float tam = Math.Min(altura * 0.72f, t * 0.36f);
                    float minimo = Math.Max(7f, t * 0.22f);
                    string texto = ObterTextoIcone(p);
                    Font fonte = new Font("Segoe UI", tam, FontStyle.Bold, GraphicsUnit.Pixel);
                    while (g.MeasureString(texto, fonte).Width > largura && tam > minimo)
                    {
                        tam -= 0.5f;
                        fonte.Dispose();
                        fonte = new Font("Segoe UI", tam, FontStyle.Bold, GraphicsUnit.Pixel);
                    }
                    using (fonte)
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        var ret = new RectangleF(0, yTexto, t, altura);

                        // Pilula de fundo translucida para contraste e nitidez absoluta
                        var sz = g.MeasureString(texto, fonte);
                        float pw = Math.Min(t * 0.98f, sz.Width + (t * 0.08f));
                        float ph = Math.Min(altura, sz.Height * 0.90f);
                        var pill = new RectangleF((t - pw) / 2f, ret.Y + (ret.Height - ph) / 2f, pw, ph);
                        float pr = Math.Min(pill.Width, pill.Height) * 0.35f;
                        if (pr > 1f && t >= 32)
                        {
                            using (var ppath = new GraphicsPath())
                            {
                                ppath.AddArc(pill.X, pill.Y, pr * 2, pr * 2, 180, 90);
                                ppath.AddArc(pill.Right - pr * 2, pill.Y, pr * 2, pr * 2, 270, 90);
                                ppath.AddArc(pill.Right - pr * 2, pill.Bottom - pr * 2, pr * 2, pr * 2, 0, 90);
                                ppath.AddArc(pill.X, pill.Bottom - pr * 2, pr * 2, pr * 2, 90, 90);
                                ppath.CloseFigure();
                                using (var pb = new SolidBrush(Color.FromArgb(175, 15, 23, 42)))
                                    g.FillPath(pb, ppath);
                            }
                        }

                        float d = Math.Max(1f, t / 64f);
                        var sombra = new RectangleF(ret.X + d, ret.Y + d, ret.Width, ret.Height);
                        using (var bs = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) g.DrawString(texto, fonte, bs, sombra, fmt);
                        g.DrawString(texto, fonte, Brushes.White, ret, fmt);
                    }
                }
            }
            return bmp;
        }

        static string Assinatura(Pasta p)
        {
            return "v4|" + p.Cor + "|" + p.MostrarNome + "|" + p.Nome + "|" + p.Rotulo + "|" + string.Join(",", p.Apps.Take(9).Select(a => a.Icone));
        }

        // Gera o .ico com varios tamanhos (a barra escolhe o mais nitido). Usa um nome novo a cada mudanca
        // para a barra de tarefas recarregar o icone (ela guarda cache por caminho).
        public static void GerarIconePasta(Pasta p)
        {
            string assin = Assinatura(p);
            if (assin == p.Assinatura && File.Exists(CaminhoIcone(p))) return;

            int[] tamanhos = { 16, 24, 32, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (int t in tamanhos)
                using (var bmp = DesenharMosaico(p, t))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }

            string antigo = CaminhoIcone(p);
            p.IcoArq = "_pasta-" + p.Id + "-" + DateTime.Now.Ticks + ".ico";
            using (var fs = File.Create(CaminhoIcone(p)))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)tamanhos.Length);
                uint offset = (uint)(6 + 16 * tamanhos.Length);
                for (int i = 0; i < tamanhos.Length; i++)
                {
                    byte d = (byte)(tamanhos[i] >= 256 ? 0 : tamanhos[i]);
                    bw.Write(d); bw.Write(d); bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((ushort)1); bw.Write((ushort)32); bw.Write((uint)pngs[i].Length); bw.Write(offset);
                    offset += (uint)pngs[i].Length;
                }
                foreach (var png in pngs) bw.Write(png);
            }
            p.Assinatura = assin;
            try { if (File.Exists(antigo)) File.Delete(antigo); } catch { }
            Repo.Salvar();
        }

        // ---------- separador / divisor visual na barra de tarefas ----------
        public static readonly string CaminhoAtalhoSeparador = Path.Combine(Repo.Atalhos, "│ Separador.lnk");
        public static readonly string CaminhoIconeSeparador = Path.Combine(Repo.Icones, "_separador.ico");

        public static Bitmap DesenharSeparador(int t)
        {
            var bmp = new Bitmap(t, t, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float larg = Math.Max(2f, t * 0.045f);
                float alt = t * 0.52f;
                float x = (t - larg) / 2f;
                float y = (t - alt) / 2f;
                float raio = larg / 2f;
                var path = new GraphicsPath();
                path.AddArc(x, y, larg, larg, 180, 180);
                path.AddArc(x, y + alt - larg, larg, larg, 0, 180);
                path.CloseFigure();
                // Linha semitransparente em tom neutro que combina com tema escuro e claro
                using (var br = new SolidBrush(Color.FromArgb(180, 160, 174, 192)))
                    g.FillPath(br, path);
            }
            return bmp;
        }

        public static void GerarSeparador()
        {
            if (File.Exists(CaminhoAtalhoSeparador) && File.Exists(CaminhoIconeSeparador)) return;
            int[] tamanhos = { 16, 24, 32, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (int t in tamanhos)
                using (var bmp = DesenharSeparador(t))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }

            using (var fs = File.Create(CaminhoIconeSeparador))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)tamanhos.Length);
                uint offset = (uint)(6 + 16 * tamanhos.Length);
                for (int i = 0; i < tamanhos.Length; i++)
                {
                    byte d = (byte)(tamanhos[i] >= 256 ? 0 : tamanhos[i]);
                    bw.Write(d); bw.Write(d); bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((ushort)1); bw.Write((ushort)32); bw.Write((uint)pngs[i].Length); bw.Write(offset);
                    offset += (uint)pngs[i].Length;
                }
                foreach (var png in pngs) bw.Write(png);
            }

            string alvo = Instalador.RodandoInstalado() ? Instalador.ExeInstalado : Process.GetCurrentProcess().MainModule.FileName;
            CriarAtalho(CaminhoAtalhoSeparador, alvo, "--separador", CaminhoIconeSeparador + ",0", "Separador da barra de tarefas");
        }

        public static bool SeparadorFixado()
        {
            try
            {
                if (!Directory.Exists(PastaFixados)) return false;
                foreach (var f in Directory.GetFiles(PastaFixados, "*.lnk"))
                {
                    string alvo, args, ico; int idx;
                    if (LerAtalho(f, out alvo, out args, out ico, out idx))
                        if ((args ?? "").Contains("--separador")) return true;
                }
            }
            catch { }
            return false;
        }

        // ---------- atalhos nativos via COM (sem WScript.Shell / scripts) ----------
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
        }

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore ppv);

        public static void DefinirAppIdJanela(IntPtr hwnd, string appId)
        {
            try
            {
                var guid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                IPropertyStore store;
                if (SHGetPropertyStoreForWindow(hwnd, ref guid, out store) == 0 && store != null)
                {
                    var pkey = new PROPERTYKEY
                    {
                        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                        pid = 5
                    };
                    var pv = new PROPVARIANT
                    {
                        vt = 31, // VT_LPWSTR
                        pwszVal = Marshal.StringToCoTaskMemUni(appId)
                    };
                    try
                    {
                        store.SetValue(ref pkey, ref pv);
                        store.Commit();
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pv.pwszVal);
                    }
                }
            }
            catch { }
        }

        public static void NotificarShell()
        {
            try { SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero); } catch { }
        }

        public static bool LerAtalho(string lnk, out string alvo, out string args, out string icone, out int iconeIdx)
        {
            alvo = null; args = null; icone = null; iconeIdx = 0;
            try
            {
                var sl = (IShellLinkW)new ShellLink();
                ((IPersistFile)sl).Load(lnk, 0);
                var sbAlvo = new StringBuilder(260);
                sl.GetPath(sbAlvo, sbAlvo.Capacity, IntPtr.Zero, 0);
                alvo = sbAlvo.ToString();
                var sbArgs = new StringBuilder(1024);
                sl.GetArguments(sbArgs, sbArgs.Capacity);
                args = sbArgs.ToString();
                var sbIco = new StringBuilder(260);
                sl.GetIconLocation(sbIco, sbIco.Capacity, out iconeIdx);
                icone = sbIco.ToString();
                Marshal.ReleaseComObject(sl);
                return true;
            }
            catch { return false; }
        }

        public static void CriarAtalho(string lnk, string alvo, string args, string icone, string desc = "")
        {
            try
            {
                var sl = (IShellLinkW)new ShellLink();
                if (File.Exists(lnk))
                {
                    try { ((IPersistFile)sl).Load(lnk, 0); } catch { }
                }
                sl.SetPath(alvo);
                if (args != null) sl.SetArguments(args);
                if (!string.IsNullOrEmpty(icone))
                {
                    string icoPath = icone;
                    int idx = 0;
                    int virgula = icone.LastIndexOf(',');
                    if (virgula > 0)
                    {
                        int.TryParse(icone.Substring(virgula + 1), out idx);
                        icoPath = icone.Substring(0, virgula);
                    }
                    sl.SetIconLocation(icoPath, idx);
                }
                if (!string.IsNullOrEmpty(desc)) sl.SetDescription(desc);
                string dir = Path.GetDirectoryName(lnk);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                ((IPersistFile)sl).Save(lnk, true);
                Marshal.ReleaseComObject(sl);
            }
            catch { }
        }

        // Cria/atualiza o atalho da pasta (e o icone) e tambem as copias dela fixadas na barra.
        public static string AtualizarPasta(Pasta p)
        {
            GerarIconePasta(p);
            string lnk = CaminhoAtalho(p);
            GravarAtalho(lnk, p);
            foreach (var f in FixadosDe(p)) GravarAtalho(f, p);
            NotificarShell();
            return lnk;
        }

        // So regrava se algo mudou (evita mexer na barra de tarefas toda vez que o programa inicia).
        static void GravarAtalho(string lnk, Pasta p)
        {
            string alvo = Process.GetCurrentProcess().MainModule.FileName;
            string args = "--abrir " + p.Id;
            string ico = CaminhoIcone(p);
            string atualAlvo, atualArgs, atualIco; int atualIdx;
            if (File.Exists(lnk) && LerAtalho(lnk, out atualAlvo, out atualArgs, out atualIco, out atualIdx))
            {
                if (string.Equals(atualAlvo, alvo, StringComparison.OrdinalIgnoreCase)
                    && (atualArgs ?? "").Trim() == args
                    && string.Equals(atualIco, ico, StringComparison.OrdinalIgnoreCase)) return;
            }
            CriarAtalho(lnk, alvo, args, ico + ",0", "Pasta " + p.Nome);
        }

        // Copias desta pasta fixadas na barra: achadas pelo ID nos argumentos (nao pelo nome do arquivo,
        // que nao muda quando a pasta e renomeada). Tambem reconhece as do prototipo antigo (abrir.vbs "Nome").
        static List<string> FixadosDe(Pasta p)
        {
            var r = new List<string>();
            try
            {
                if (!Directory.Exists(PastaFixados)) return r;
                foreach (var f in Directory.GetFiles(PastaFixados, "*.lnk"))
                {
                    string alvo, args, ico; int idx;
                    if (!LerAtalho(f, out alvo, out args, out ico, out idx)) continue;
                    args = (args ?? "").Trim();
                    if (args == "--abrir " + p.Id || (args.Contains("abrir.vbs") && args.EndsWith("\"" + p.Nome + "\""))) r.Add(f);
                }
            }
            catch { }
            return r;
        }

        // IDs das pastas que ja estao fixadas na barra (uma varredura so).
        public static HashSet<string> IdsFixados()
        {
            var ids = new HashSet<string>();
            try
            {
                if (!Directory.Exists(PastaFixados)) return ids;
                foreach (var f in Directory.GetFiles(PastaFixados, "*.lnk"))
                {
                    string alvo, args, ico; int idx;
                    if (!LerAtalho(f, out alvo, out args, out ico, out idx)) continue;
                    args = (args ?? "").Trim();
                    if (args.StartsWith("--abrir "))
                    {
                        var id = args.Substring(8).Trim().Split(' ')[0];
                        if (!string.IsNullOrEmpty(id)) ids.Add(id);
                    }
                }
            }
            catch { }
            return ids;
        }

        public static readonly string PastaFixados = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        public static void ApagarAtalho(Pasta p)
        {
            try { File.Delete(CaminhoAtalho(p)); } catch { }
            try { File.Delete(CaminhoIcone(p)); } catch { }
            NotificarShell();
        }

        // ---------- iniciar com o Windows ----------
        const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IniciarComWindows
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(ChaveRun))
                    return k != null && k.GetValue("PastasBarra") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(ChaveRun))
                {
                    if (k != null)
                    {
                        if (value)
                        {
                            string exe = Instalador.RodandoInstalado() ? Instalador.ExeInstalado : Process.GetCurrentProcess().MainModule.FileName;
                            k.SetValue("PastasBarra", "\"" + exe + "\" --bandeja");
                        }
                        else
                        {
                            k.DeleteValue("PastasBarra", false);
                        }
                    }
                }
            }
        }
    }
}
