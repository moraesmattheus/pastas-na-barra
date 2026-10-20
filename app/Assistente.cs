using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PastasBarra
{
    // Primeira abertura: detecta os apps instalados e sugere pastas prontas.
    public class AssistenteJanela : Window
    {
        class Modelo
        {
            public string Nome, Cor;
            public string[] Chaves;
            public Modelo(string nome, string cor, params string[] chaves) { Nome = nome; Cor = cor; Chaves = chaves; }
        }

        static readonly Modelo[] Modelos =
        {
            new Modelo("IA", "#8B5CF6", "claude", "chatgpt", "gemini", "copilot", "comet", "ollama", "antigravity", "anythingllm", "perplexity", "lm studio"),
            new Modelo("Redes Sociais", "#3B82F6", "discord", "instagram", "linkedin", "telegram", "whatsapp", "facebook", "tiktok", "twitter", "messenger", "reddit", "signal"),
            new Modelo("Jogos", "#EF4444", "steam", "epic games", "xbox", "riot", "battle.net", "forge of", "empires", "viking rise", "roblox", "minecraft", "ubisoft", "ea app", "launcher"),
            new Modelo("Google", "#10B981", "google", "chrome", "gmail", "youtube"),
            new Modelo("Microsoft", "#06B6D4", "word", "excel", "powerpoint", "outlook", "onenote", "onedrive", "publisher", "microsoft to do", "whiteboard", "teams", "access"),
            new Modelo("Dev", "#475569", "visual studio", "github", "docker", "git bash", "git cmd", "git gui", "android studio", "terminal", "powershell", "node.js", "python", "anaconda", "idle"),
            new Modelo("Mídia", "#EC4899", "spotify", "netflix", "vlc", "globoplay", "paramount", "filmes", "fotos", "reprodutor", "media player", "câmera"),
            new Modelo("Produtividade", "#F59E0B", "notion", "obsidian", "canva", "bloco de notas", "notepad", "calculadora", "captura", "calend", "notas autoadesivas", "sticky", "paint", "relógio"),
            new Modelo("Sistema", "#64748B", "gerenciador de tarefas", "configurações", "painel de controle", "explorador de arquivos", "prompt de comando", "editor do registro", "monitor de", "serviços", "gerenciamento do computador", "informações do sistema", "limpeza de disco", "executar")
        };

        static readonly string[] Ruido =
        {
            "desinstal", "uninstall", "documentation", "release notes", "faq", "ajuda", "help", "website", "homepage",
            "getting started", "user guide", "manual", "legal", "licen", "introdu", "o que há de novo", "recuva"
        };

        readonly TextBlock _status = new TextBlock();
        readonly StackPanel _lista = new StackPanel();
        readonly Button _criar = new Button();
        readonly List<Tuple<Modelo, List<CheckBox>>> _grupos = new List<Tuple<Modelo, List<CheckBox>>>();

        public AssistenteJanela()
        {
            Title = "Pastas na barra · Configuração inicial";
            Width = 560; Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Pincel("#FFFAFAFA");

            var raiz = new Grid { Margin = new Thickness(22) };
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topo = new StackPanel();
            topo.Children.Add(new TextBlock { Text = "Vamos organizar seus apps em pastas", FontSize = 22, FontWeight = FontWeights.SemiBold });
            topo.Children.Add(new TextBlock
            {
                Text = "Encontramos os apps instalados e sugerimos pastas, como no celular. Desmarque o que não quiser; você muda tudo depois.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8), Foreground = Brushes.Gray
            });
            _status.Text = "Procurando seus apps...";
            _status.Margin = new Thickness(0, 0, 0, 6);
            topo.Children.Add(_status);
            raiz.Children.Add(topo);

            var rolagem = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _lista };
            Grid.SetRow(rolagem, 1);
            raiz.Children.Add(rolagem);

            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var pular = new Button { Content = "Pular", Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 8, 0) };
            pular.Click += (s, e) => { Repo.Dados.PrimeiraVezConcluida = true; Repo.Salvar(); Close(); };
            _criar.Content = "Criar pastas"; _criar.Padding = new Thickness(22, 8, 22, 8); _criar.IsEnabled = false; _criar.IsDefault = true;
            _criar.Click += (s, e) => Criar();
            rodape.Children.Add(pular); rodape.Children.Add(_criar);
            Grid.SetRow(rodape, 2);
            raiz.Children.Add(rodape);
            Content = raiz;

            Loaded += (s, e) =>
            {
                Task.Run(() => Shell.ListarApps()).ContinueWith(t =>
                    Dispatcher.BeginInvoke(new Action(() => Preencher(t.Result))));
            };
        }

        void Preencher(List<StartApp> apps)
        {
            var usados = new HashSet<string>();
            int total = 0;
            var jaExistem = new HashSet<string>(Repo.Dados.Pastas.Select(p => p.Nome.ToLowerInvariant()));
            foreach (var m in Modelos)
            {
                if (jaExistem.Contains(m.Nome.ToLowerInvariant())) continue;
                var achados = apps.Where(a => !usados.Contains(a.AppId)
                    && !Ruido.Any(r => a.Nome.IndexOf(r, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    && m.Chaves.Any(c => a.Nome.IndexOf(c, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
                if (achados.Count == 0) continue;
                foreach (var a in achados) usados.Add(a.AppId);
                total += achados.Count;

                var caixas = new List<CheckBox>();
                var cab = new CheckBox { Content = m.Nome + "  ·  " + achados.Count + " apps", IsChecked = true, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 4) };
                var corpo = new WrapPanel { Margin = new Thickness(22, 0, 0, 0) };
                foreach (var a in achados)
                {
                    var cb = new CheckBox { Content = a.Nome, Tag = a, IsChecked = true, Margin = new Thickness(0, 0, 14, 3) };
                    caixas.Add(cb); corpo.Children.Add(cb);
                }
                cab.Click += (s, e) => { foreach (var c in caixas) c.IsChecked = cab.IsChecked; };
                _lista.Children.Add(cab); _lista.Children.Add(corpo);
                _grupos.Add(Tuple.Create(m, caixas));
            }
            _status.Text = total > 0 ? "Encontramos " + total + " apps em " + _grupos.Count + " pastas sugeridas." : "Não encontramos sugestões novas. Você pode criar pastas pelas Configurações.";
            _criar.IsEnabled = true;
            ((ScrollViewer)_lista.Parent).ScrollToTop();
        }

        void Criar()
        {
            _criar.IsEnabled = false;
            _status.Text = "Criando pastas...";
            var escolhas = _grupos.Select(g => Tuple.Create(g.Item1, g.Item2.Where(c => c.IsChecked == true).Select(c => (StartApp)c.Tag).ToList()))
                                  .Where(g => g.Item2.Count > 0).ToList();
            Task.Run(() =>
            {
                var novas = new List<Pasta>();
                foreach (var g in escolhas)
                {
                    var p = new Pasta { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Nome = g.Item1.Nome, Cor = g.Item1.Cor };
                    foreach (var a in g.Item2) p.Apps.Add(Shell.NovoItem(a));
                    novas.Add(p);
                }
                return novas;
            }).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var p in t.Result) { Repo.Dados.Pastas.Add(p); Shell.AtualizarPasta(p); }
                Repo.Dados.PrimeiraVezConcluida = true;
                Repo.Salvar();
                Close();
                if (t.Result.Count > 0) App.MostrarFixar(t.Result);
            })));
        }
    }

    // Guia para fixar: o icone de cada pasta pode ser arrastado direto para a barra de tarefas.
    public class FixarJanela : Window
    {
        readonly List<Tuple<Pasta, TextBlock>> _linhas = new List<Tuple<Pasta, TextBlock>>();
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };

        public FixarJanela(IEnumerable<Pasta> pastas)
        {
            Title = "Fixe suas pastas na barra de tarefas";
            Width = 600; SizeToContent = SizeToContent.Height; MaxHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Pincel("#FFFAFAFA");

            var pilha = new StackPanel { Margin = new Thickness(22) };
            pilha.Children.Add(new TextBlock { Text = "Último passo: fixar na barra", FontSize = 22, FontWeight = FontWeights.SemiBold });
            pilha.Children.Add(new TextBlock
            {
                Text = "Arraste cada ícone abaixo e solte na barra de tarefas (lá embaixo). O Windows só permite fixar assim, uma vez por pasta. Depois, se quiser, desafixe os apps soltos que entraram numa pasta.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 14), Foreground = Brushes.Gray
            });

            var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var p in pastas)
            {
                var pasta = p;
                var img = new Image { Width = 72, Height = 72, Source = Mosaico(pasta), Cursor = Cursors.Hand, ToolTip = "Arraste para a barra de tarefas" };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                Point? inicio = null;
                img.MouseLeftButtonDown += (s, e) => inicio = e.GetPosition(null);
                img.MouseMove += (s, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed || inicio == null) return;
                    var d = e.GetPosition(null) - inicio.Value;
                    if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
                    inicio = null;
                    var dados = new DataObject(DataFormats.FileDrop, new[] { Shell.CaminhoAtalho(pasta) });
                    DragDrop.DoDragDrop(img, dados, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
                };
                var estado = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                var item = new StackPanel { Margin = new Thickness(12), Width = 100 };
                item.Children.Add(img);
                item.Children.Add(new TextBlock { Text = pasta.Nome, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
                item.Children.Add(estado);
                wrap.Children.Add(item);
                _linhas.Add(Tuple.Create(pasta, estado));
            }
            pilha.Children.Add(new ScrollViewer { Content = wrap, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

            var rodape = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var abrir = new Button { Content = "Abrir pasta dos atalhos", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
            abrir.Click += (s, e) => Process.Start("explorer.exe", "\"" + Repo.Atalhos + "\"");
            var pronto = new Button { Content = "Concluído", Padding = new Thickness(22, 8, 22, 8), IsDefault = true };
            pronto.Click += (s, e) => Close();
            rodape.Children.Add(abrir); rodape.Children.Add(pronto);
            pilha.Children.Add(rodape);
            Content = pilha;

            Atualizar();
            _timer.Tick += (s, e) => Atualizar();
            _timer.Start();
            Closed += (s, e) => _timer.Stop();
        }

        static BitmapImage Mosaico(Pasta p)
        {
            using (var bmp = Shell.DesenharMosaico(p, 144))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                return bi;
            }
        }

        void Atualizar()
        {
            foreach (var l in _linhas)
            {
                bool fixada = File.Exists(Path.Combine(Shell.PastaFixados, Path.GetFileName(Shell.CaminhoAtalho(l.Item1))));
                l.Item2.Text = fixada ? "✓ fixada" : "arraste para a barra";
                l.Item2.Foreground = fixada ? Brushes.SeaGreen : Brushes.Gray;
            }
        }
    }
}
