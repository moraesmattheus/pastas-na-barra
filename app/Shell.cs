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
        public string Nome;
        public string AppId;
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
                IntPtr hbm;
                if (f.GetImage(new SIZE(tam, tam), 0, out hbm) != 0) return null;
                try
                {
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
                finally { DeleteObject(hbm); }
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
            var bmp = Icone(alvo, 96);
            if (bmp == null) return null;
            string png = Path.Combine(Repo.Icones, "app-" + Seguro(nome) + "-" + (uint)alvo.GetHashCode() + ".png");
            bmp.Save(png, ImageFormat.Png);
            bmp.Dispose();
            return png;
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
                            lista.Add(new StartApp { Nome = nome.Trim(), AppId = id });
                    }
                }
            }
            catch { }
            _apps = lista.GroupBy(a => a.Nome.ToLowerInvariant()).Select(g => g.First())
                         .OrderBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
            return _apps;
        }

        public static AppItem NovoItem(StartApp a)
        {
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
                if (a.Tipo == "shell") Process.Start("explorer.exe", a.Alvo);
                else Process.Start(new ProcessStartInfo(a.Alvo) { UseShellExecute = true, Arguments = a.Args ?? "" });
            }
            catch { }
        }

        // ---------- app aberto? (pontinho) ----------
        static List<Tuple<string, string>> _procs = new List<Tuple<string, string>>();

        static string Norm(string s)
        {
            return new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        public static void AtualizarProcessos()
        {
            var l = new List<Tuple<string, string>>();
            foreach (var p in Process.GetProcesses())
            {
                try { if (p.MainWindowHandle != IntPtr.Zero) l.Add(Tuple.Create(Norm(p.ProcessName), p.MainWindowTitle ?? "")); }
                catch { }
                finally { p.Dispose(); }
            }
            _procs = l;
        }

        public static bool Rodando(AppItem a)
        {
            string n = Norm(a.Nome);
            if (n.Length < 3) return false;
            foreach (var t in _procs)
            {
                if (t.Item1 == n) return true;
                if (n.Length >= 4 && t.Item1.Length >= 4 && (t.Item1.Contains(n) || n.Contains(t.Item1))) return true;
                if (n.Length >= 4 && t.Item2.IndexOf(a.Nome, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
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
            string nome = p.Nome;
            foreach (char c in Path.GetInvalidFileNameChars()) nome = nome.Replace(c, '-');
            return Path.Combine(Repo.Atalhos, nome + ".lnk");
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
                float s = nome ? t * 0.74f : t;                 // lado do mosaico
                float x0 = (t - s) / 2f, y0 = nome ? t * 0.01f : 0f;
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
                    using (var img = Image.FromFile(icones[i].Icone)) g.DrawImage(img, x, y, cel, cel);
                }
                if (nome)
                {
                    float altura = t - (y0 + s);
                    using (var fonte = new Font("Segoe UI Semibold", Math.Max(6f, t * 0.19f), FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        var ret = new RectangleF(0, y0 + s, t, altura);
                        var sombra = new RectangleF(ret.X + Math.Max(1, t / 128f), ret.Y + Math.Max(1, t / 128f), ret.Width, ret.Height);
                        using (var bs = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) g.DrawString(p.Nome, fonte, bs, sombra, fmt);
                        g.DrawString(p.Nome, fonte, Brushes.White, ret, fmt);
                    }
                }
            }
            return bmp;
        }

        static string Assinatura(Pasta p)
        {
            return p.Cor + "|" + p.MostrarNome + "|" + p.Nome + "|" + string.Join(",", p.Apps.Take(9).Select(a => a.Icone));
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

        // Cria/atualiza o atalho da pasta (e o icone). Se ja existir uma copia fixada na barra, atualiza o icone dela tambem.
        public static string AtualizarPasta(Pasta p)
        {
            GerarIconePasta(p);
            string lnk = CaminhoAtalho(p);
            GravarAtalho(lnk, p);
            string fixado = Path.Combine(PastaFixados, Path.GetFileName(lnk));
            if (File.Exists(fixado)) GravarAtalho(fixado, p);
            return lnk;
        }

        static void GravarAtalho(string lnk, Pasta p)
        {
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic a = sh.CreateShortcut(lnk);
            a.TargetPath = Process.GetCurrentProcess().MainModule.FileName;
            a.Arguments = "--abrir " + p.Id;
            a.IconLocation = CaminhoIcone(p) + ",0";
            a.Description = "Pasta " + p.Nome;
            a.Save();
        }

        public static readonly string PastaFixados = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        public static void ApagarAtalho(Pasta p)
        {
            try { File.Delete(CaminhoAtalho(p)); } catch { }
            try { File.Delete(CaminhoIcone(p)); } catch { }
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
                using (var k = Registry.CurrentUser.OpenSubKey(ChaveRun, true))
                {
                    if (value) k.SetValue("PastasBarra", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\" --bandeja");
                    else k.DeleteValue("PastasBarra", false);
                }
            }
        }
    }
}
