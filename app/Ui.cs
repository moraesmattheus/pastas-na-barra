using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PastasBarra
{
    // Janelinhas e utilitarios de interface compartilhados.
    public static class Ui
    {
        public static readonly string[] Cores =
        {
            "#475569", "#3B82F6", "#8B5CF6", "#EC4899", "#EF4444", "#F59E0B", "#10B981", "#06B6D4"
        };

        // Fundo do painel: a cor da pasta bem escurecida, para o painel combinar com o icone.
        public static SolidColorBrush FundoPainel(string hex)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(hex); } catch { c = Color.FromRgb(71, 85, 105); }
            Func<byte, byte, byte> mistura = (a, b) => (byte)(a * 0.28 + b * 0.72);
            var br = new SolidColorBrush(Color.FromArgb(0xEE, mistura(c.R, 0x14), mistura(c.G, 0x16), mistura(c.B, 0x20)));
            br.Freeze();
            return br;
        }

        public static SolidColorBrush Pincel(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();

        public static BitmapImage Carregar(string png)
        {
            if (string.IsNullOrEmpty(png)) return null;
            BitmapImage pronto;
            if (_cache.TryGetValue(png, out pronto)) return pronto;
            if (!File.Exists(png)) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(png);
            bi.EndInit();
            bi.Freeze();
            _cache[png] = bi;
            return bi;
        }

        static Window Base(Window dono, string titulo, double largura)
        {
            var w = new Window
            {
                Title = titulo,
                Width = largura,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (dono != null && dono.IsVisible) w.Owner = dono;
            return w;
        }

        public static string PedirTexto(Window dono, string titulo, string inicial)
        {
            var w = Base(dono, titulo, 320);
            var pl = new StackPanel { Margin = new Thickness(14) };
            var tb = new TextBox { FontSize = 14, Padding = new Thickness(6), Text = inicial ?? "" };
            var bt = new Button { Content = "OK", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6), IsDefault = true };
            pl.Children.Add(tb); pl.Children.Add(bt); w.Content = pl;
            string resp = null;
            bt.Click += (s, e) => { resp = tb.Text.Trim(); w.Close(); };
            w.KeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.ContentRendered += (s, e) => { tb.Focus(); tb.SelectAll(); };
            w.ShowDialog();
            return string.IsNullOrEmpty(resp) ? null : resp;
        }

        public static string EscolherCor(Window dono)
        {
            var w = Base(dono, "Cor da pasta", 300);
            var wrap = new WrapPanel { Margin = new Thickness(14) };
            string resp = null;
            foreach (var c in Cores)
            {
                var cor = c;
                var b = new Button { Width = 56, Height = 56, Margin = new Thickness(4), Background = Pincel(cor), BorderThickness = new Thickness(0) };
                b.Click += (s, e) => { resp = cor; w.Close(); };
                wrap.Children.Add(b);
            }
            w.Content = wrap;
            w.KeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.ShowDialog();
            return resp;
        }

        public static AppItem EscolherApp(Window dono)
        {
            var apps = Shell.ListarApps();
            var w = new Window
            {
                Title = "Adicionar app",
                Width = 340,
                Height = 460,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (dono != null && dono.IsVisible) w.Owner = dono;
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tb = new TextBox { Margin = new Thickness(10), Padding = new Thickness(6), FontSize = 14 };
            var lb = new ListBox { Margin = new Thickness(10, 0, 10, 0), FontSize = 13, DisplayMemberPath = "Nome" };
            var bt = new Button { Content = "Adicionar", Margin = new Thickness(10, 10, 5, 10), Padding = new Thickness(14, 6, 14, 6), IsDefault = true };
            var procurar = new Button { Content = "Procurar no computador...", Margin = new Thickness(5, 10, 10, 10), Padding = new Thickness(14, 6, 14, 6) };
            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rodape.Children.Add(procurar); rodape.Children.Add(bt);
            Grid.SetRow(lb, 1); Grid.SetRow(rodape, 2);
            g.Children.Add(tb); g.Children.Add(lb); g.Children.Add(rodape);
            w.Content = g;
            Action encher = () =>
            {
                string f = tb.Text.Trim();
                lb.ItemsSource = apps.Where(a => f.Length == 0 || a.Nome.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
                if (lb.Items.Count > 0) lb.SelectedIndex = 0;
            };
            encher();
            tb.TextChanged += (s, e) => encher();
            AppItem resp = null;
            Action ok = () => { if (lb.SelectedItem != null) { resp = Shell.NovoItem((StartApp)lb.SelectedItem); w.Close(); } };
            procurar.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Escolha um programa, atalho ou arquivo",
                    Filter = "Programas e atalhos|*.exe;*.lnk;*.bat;*.cmd;*.url|Todos os arquivos|*.*",
                    DereferenceLinks = false
                };
                if (dlg.ShowDialog(w) == true) { resp = Shell.NovoItemArquivo(dlg.FileName); w.Close(); }
            };
            bt.Click += (s, e) => ok();
            lb.MouseDoubleClick += (s, e) => ok();
            w.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) w.Close();
                else if (e.Key == Key.Down && lb.SelectedIndex < lb.Items.Count - 1) lb.SelectedIndex++;
                else if (e.Key == Key.Up && lb.SelectedIndex > 0) lb.SelectedIndex--;
            };
            w.ContentRendered += (s, e) => tb.Focus();
            w.ShowDialog();
            return resp;
        }
    }
}
