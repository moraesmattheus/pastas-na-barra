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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Web.Script.Serialization;
using System.Windows.Shapes;
using WF = System.Windows.Forms;

namespace PastasBarra
{
    // O painel que abre acima da barra de tarefas ao clicar no icone de uma pasta.
    // A engrenagem liga o modo de edicao (remover, reordenar arrastando, cor, renomear, excluir).
    public class Painel : Window
    {
        const string FormatoApp = "PastasBarraApp";

        static DateTime _fechouEm = DateTime.MinValue;
        static string _fechouId = "";
        static Painel _aberto;

        readonly Pasta _pasta;
        readonly System.Drawing.Point _cursor;
        readonly TextBlock _titulo = new TextBlock();
        readonly UniformGrid _grade = new UniformGrid();
        readonly ContentControl _rodape = new ContentControl();
        readonly List<Tuple<AppItem, Ellipse>> _pontos = new List<Tuple<AppItem, Ellipse>>();
        bool _ativou, _ocupado, _editando, _fechando, _saindo, _silencioso;
        Border _moldura;
        string _snapshot;      // copia da pasta ao entrar no modo de edicao (para descartar)
        bool _alterado;        // ha mudancas ainda nao salvas
        TranslateTransform _slide;
        string _filtro = "";   // busca rapida ao digitar letras

        public static void Abrir(Pasta p)
        {
            // clicar de novo no icone com o painel aberto = fechar (ele ja fechou ao perder o foco)
            if (_fechouId == p.Id && (DateTime.Now - _fechouEm).TotalMilliseconds < 700) { _fechouId = ""; return; }
            if (_aberto != null && _aberto._pasta == p) { _aberto.FecharSuave(); return; }   // clicar de novo no icone fecha
            if (_aberto != null) { try { _aberto.Close(); } catch (InvalidOperationException) { } }
            _aberto = new Painel(p);
            _aberto.Show();
        }

        // Mostra o painel da pasta; se ja estiver aberto, so atualiza o conteudo (nao fecha).
        public static void MostrarOuAtualizar(Pasta p)
        {
            if (_aberto != null && _aberto._pasta == p) { _aberto.Montar(); return; }
            Abrir(p);
        }

        // Cria e fecha um painel fora da tela no inicio, para o primeiro clique de verdade ja ser instantaneo.
        public static void Aquecer(Pasta p)
        {
            var w = new Painel(p);
            w._silencioso = true;
            w.Show();
            w.Close();
            _fechouId = ""; _fechouEm = DateTime.MinValue;
        }

        public Border Moldura { get { return _moldura; } }

        public Painel(Pasta p)
        {
            _pasta = p;
            _cursor = WF.Cursor.Position;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            UseLayoutRounding = true;
            AllowDrop = true;
            Left = _cursor.X; Top = _cursor.Y;

            var moldura = _moldura = new Border
            {
                CornerRadius = Ui.CantosPainel,
                Padding = new Thickness(16),
                Margin = new Thickness(12),
                Background = Ui.FundoPainel(p.Cor),
                BorderBrush = Ui.BordaPainel(),
                BorderThickness = new Thickness(1),
                Effect = Ui.SombraProfundidade
            };

            // cabecalho: titulo no centro + engrenagem
            var cab = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            _titulo.Foreground = Ui.CorTexto; _titulo.FontSize = 15; _titulo.FontWeight = FontWeights.SemiBold;
            _titulo.HorizontalAlignment = HorizontalAlignment.Center; _titulo.Background = Brushes.Transparent;
            _titulo.ContextMenu = MenuTitulo();
            var engrenagem = new TextBlock
            {
                Text = "\u2699", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 18, Foreground = Ui.CorSubtexto,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = "Personalizar esta pasta", Padding = new Thickness(6, 0, 0, 0)
            };
            engrenagem.MouseLeftButtonUp += (s, e) => AlternarEdicao();
            cab.Children.Add(_titulo); cab.Children.Add(engrenagem);

            var pilha = new StackPanel();
            pilha.Children.Add(cab); pilha.Children.Add(_grade); pilha.Children.Add(_rodape);
            moldura.Child = pilha;
            Content = moldura;
            moldura.Opacity = 0;
            moldura.RenderTransform = _slide = new TranslateTransform(0, 14);

            DragEnter += (s, e) => { if (!e.Data.GetDataPresent(FormatoApp)) { e.Effects = DragDropEffects.Copy; _moldura.BorderBrush = Ui.Pincel("#CCFFFFFF"); e.Handled = true; } };
            DragOver += (s, e) => { if (!e.Data.GetDataPresent(FormatoApp)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
            DragLeave += (s, e) => _moldura.BorderBrush = Ui.CorBorda;
            Drop += (s, e) => Soltar(e);
            ContentRendered += (s, e) => { Reposicionar(); Activate(); Entrar(); };
            SizeChanged += (s, e) => Reposicionar();
            Activated += (s, e) => _ativou = true;
            Closing += (s, e) => _fechando = true;
            // no modo de edicao o painel fica aberto (permite arrastar apps de outras janelas para ele)
            Deactivated += (s, e) => { if (_ativou && !_ocupado && !_fechando && !_editando) FecharSuave(); };
            
            // Busca rapida ao digitar letras diretamente com o painel aberto
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    if (!string.IsNullOrEmpty(_filtro)) { _filtro = ""; Montar(); }
                    else if (_editando) AlternarEdicao();
                    else FecharSuave();
                }
                else if (e.Key == Key.Enter && !string.IsNullOrEmpty(_filtro))
                {
                    var primeiro = _pasta.Apps.FirstOrDefault(a => a.Nome.IndexOf(_filtro, StringComparison.CurrentCultureIgnoreCase) >= 0);
                    if (primeiro != null) { Shell.Abrir(primeiro); FecharSuave(); }
                }
                else if (e.Key == Key.Back)
                {
                    if (_filtro.Length > 0)
                    {
                        _filtro = _filtro.Substring(0, _filtro.Length - 1);
                        Montar();
                    }
                }
            };
            TextInput += (s, e) =>
            {
                if (_editando) return;
                if (!string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
                {
                    _filtro += e.Text;
                    Montar();
                }
            };
            Closed += (s, e) => { _fechouEm = DateTime.Now; _fechouId = _pasta.Id; if (_aberto == this) _aberto = null; };
            Montar();

            // pontinhos de "app aberto": calcula em segundo plano para nao atrasar a abertura
            Task.Run(() => Shell.AtualizarProcessos()).ContinueWith(t =>
                Dispatcher.BeginInvoke(new Action(AtualizarPontos)));
        }

        // Algo foi solto no painel (arquivo, atalho, pasta, app do Menu Iniciar).
        void Soltar(DragEventArgs e)
        {
            _moldura.BorderBrush = Ui.Pincel("#33FFFFFF");
            if (e.Data.GetDataPresent(FormatoApp)) return;
            int novos = 0;
            foreach (var item in Shell.ItensDoDrop(e.Data))
            {
                if (_pasta.Apps.Any(a => a.Alvo == item.Alvo)) continue;
                _pasta.Apps.Add(item); novos++;
            }
            if (novos > 0) { Salvar(); return; }
            _rodape.Content = new TextBlock
            {
                Text = "N\u00e3o consegui adicionar isso. Use o bot\u00e3o + para escolher da lista.",
                Foreground = Ui.Pincel("#FFB4B6"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0)
            };
        }

        // ---------- animacao: entra subindo e aparecendo; sai com fade rapido ----------
        void Entrar()
        {
            if (_silencioso) { _moldura.Opacity = 1; _slide.Y = 0; return; }
            var suave = new CubicEase { EasingMode = EasingMode.EaseOut };
            _moldura.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { EasingFunction = suave });
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = suave });
        }

        void FecharSuave()
        {
            if (_saindo || _fechando) return;
            if (_editando && _alterado && !ResolverAlteracoes()) return;   // pergunta se quer salvar
            _saindo = true;
            _fechouEm = DateTime.Now; _fechouId = _pasta.Id;
            var a = new DoubleAnimation(_moldura.Opacity, 0, TimeSpan.FromMilliseconds(90));
            a.Completed += (s, e) => Close();
            _moldura.BeginAnimation(OpacityProperty, a);
        }

        // ---------- conteudo ----------
        void Montar()
        {
            if (string.IsNullOrEmpty(_filtro))
            {
                _titulo.Text = _pasta.Nome;
            }
            else
            {
                _titulo.Text = "🔍 " + _filtro;
            }
            _titulo.Foreground = Ui.CorTexto;
            _moldura.CornerRadius = Ui.CantosPainel;
            _moldura.Background = Ui.FundoPainel(_pasta.Cor);
            _moldura.BorderBrush = Ui.BordaPainel();
            _moldura.Effect = Ui.SombraProfundidade;
            _grade.Children.Clear();
            _pontos.Clear();

            var apps = string.IsNullOrEmpty(_filtro)
                ? _pasta.Apps.ToList()
                : _pasta.Apps.Where(a => a.Nome.IndexOf(_filtro, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();

            if (apps.Count == 0 && !string.IsNullOrEmpty(_filtro))
            {
                _grade.Columns = 1;
                var vazio = new TextBlock
                {
                    Text = "Nenhum app encontrado para \"" + _filtro + "\"\n(Pressione Esc para limpar)",
                    Foreground = Ui.CorSubtexto,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(24, 18, 24, 18)
                };
                _grade.Children.Add(vazio);
            }
            else
            {
                _grade.Columns = Math.Min(5, Math.Max(2, apps.Count + (_editando ? 1 : 0)));
                foreach (var app in apps)
                {
                    var img = new Image { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Center, Source = Ui.Carregar(app.Icone) };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    var ponto = new Ellipse { Width = 5, Height = 5, Margin = new Thickness(0, 4, 0, 0), Fill = Ui.Pincel("#7DD3FC"), Opacity = 0 };
                    var celula = NovaCelula(app.Nome, img, ponto);
                    _pontos.Add(Tuple.Create(app, ponto));
                    celula.ContextMenu = MenuApp(app);
                    var a = app;
                    var host = new Grid();
                    host.Children.Add(celula);
                    if (_editando)
                    {
                        celula.Cursor = Cursors.SizeAll;
                        ConfigurarArrasto(host, celula, a);
                        var x = new Border
                        {
                            Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = Ui.Pincel("#E5484D"),
                            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand,
                            Child = new TextBlock { Text = "✕", Foreground = Brushes.White, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe UI Symbol") }
                        };
                        x.MouseLeftButtonUp += (s, e) => { _pasta.Apps.Remove(a); Salvar(); e.Handled = true; };
                        host.Children.Add(x);
                    }
                    else
                    {
                        celula.MouseLeftButtonUp += (s, e) => { Shell.Abrir(a); FecharSuave(); };
                    }
                    _grade.Children.Add(host);
                }

                if (_editando)
                {
                    var mais = new TextBlock
                    {
                        Text = "+", Foreground = Ui.CorSubtexto, FontSize = 34, Height = 48,
                        HorizontalAlignment = HorizontalAlignment.Center, LineHeight = 44, LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                    };
                    var cel = NovaCelula("Adicionar", mais, null);
                    cel.MouseLeftButtonUp += (s, e) =>
                    {
                        AppItem item = null;
                        Dialogo(() => item = Ui.EscolherApp(this));
                        if (item == null) return;
                        if (_pasta.Apps.Any(x => x.Alvo == item.Alvo)) return;
                        _pasta.Apps.Add(item);
                        Salvar();
                    };
                    _grade.Children.Add(cel);
                }
            }

            _rodape.Content = _editando ? BarraEdicao() : Dica();
            AtualizarPontos();
            UpdateLayout();
            Reposicionar();
        }

        void AtualizarPontos()
        {
            foreach (var t in _pontos) t.Item2.Opacity = Shell.Rodando(t.Item1) ? 1 : 0;
        }

        UIElement Dica()
        {
            return new TextBlock
            {
                Text = "⚙ para personalizar e arrastar apps para cá",
                Foreground = Ui.CorSubtexto, FontSize = 10.5,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0)
            };
        }

        // Barra do modo de edicao: cores + acoes da pasta.
        UIElement BarraEdicao()
        {
            var pl = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            var cores = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var c in Ui.Cores)
            {
                var cor = c;
                bool atual = string.Equals(cor, _pasta.Cor, StringComparison.OrdinalIgnoreCase);
                var b = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(4), Background = Ui.Pincel(cor),
                    BorderBrush = atual ? Brushes.White : Ui.CorBorda, BorderThickness = new Thickness(atual ? 2 : 1), Cursor = Cursors.Hand
                };
                b.MouseLeftButtonUp += (s, e) => { _pasta.Cor = cor; Salvar(); };
                cores.Children.Add(b);
            }
            var acoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            acoes.Children.Add(Acao("Renomear", Renomear, false));
            acoes.Children.Add(Acao("Nova pasta", NovaPasta, false));
            acoes.Children.Add(Acao("Configurações", () => { FecharSuave(); App.AbrirConfiguracoes(); }, false));
            acoes.Children.Add(Acao("Excluir", Excluir, true));
            var acoes2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            acoes2.Children.Add(Acao(_pasta.MostrarNome ? "Nome no ícone: ligado ✓" : "Nome no ícone: desligado", () => { _pasta.MostrarNome = !_pasta.MostrarNome; Salvar(); }, false));
            if (_pasta.MostrarNome)
            {
                var txtRot = string.IsNullOrEmpty(_pasta.Rotulo) ? "Texto: Auto (" + Shell.ObterTextoIcone(_pasta) + ")" : "Texto: " + _pasta.Rotulo;
                acoes2.Children.Add(Acao(txtRot, DefinirRotulo, false));
            }
            var gravar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            var botaoSalvar = Acao(_alterado ? "Salvar alterações" : "Tudo salvo ✓", () => { if (_alterado) Persistir(); }, false);
            if (_alterado) botaoSalvar.Background = Ui.Pincel("#2563EB");
            gravar.Children.Add(botaoSalvar);
            if (_alterado) gravar.Children.Add(Acao("Descartar", Descartar, true));
            pl.Children.Add(cores); pl.Children.Add(acoes); pl.Children.Add(acoes2); pl.Children.Add(gravar);
            pl.Children.Add(new TextBlock
            {
                Text = "Solte aqui arquivos, atalhos ou apps · arraste os ícones para reordenar · ✕ remove · ⚙ conclui",
                Foreground = Ui.CorSubtexto, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0)
            });
            return pl;
        }

        Border Acao(string texto, Action acao, bool perigo)
        {
            var bgNormal = perigo ? Ui.Pincel(Ui.TemaEscuro ? "#33E5484D" : "#1AE5484D") : Ui.CorCard;
            var bgHover = perigo ? Ui.Pincel(Ui.TemaEscuro ? "#4DE5484D" : "#2EE5484D") : Ui.CorCardHover;
            var fg = perigo ? Ui.Pincel("#EF4444") : Ui.CorTexto;
            var b = new Border
            {
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand,
                Background = bgNormal,
                BorderBrush = Ui.CorBorda,
                BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = texto, FontSize = 11.5, Foreground = fg }
            };
            b.MouseEnter += (s, e) => b.Background = bgHover;
            b.MouseLeave += (s, e) => b.Background = bgNormal;
            b.MouseLeftButtonUp += (s, e) => acao();
            return b;
        }

        Border NovaCelula(string texto, UIElement icone, UIElement ponto)
        {
            var celula = new Border
            {
                Width = 92, Padding = new Thickness(4, 8, 4, 6), CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            var col = new StackPanel();
            col.Children.Add(icone);
            col.Children.Add(new TextBlock
            {
                Text = texto, Foreground = Ui.CorTexto, FontSize = 11.5, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 32, Margin = new Thickness(0, 6, 0, 0)
            });
            col.Children.Add(ponto ?? new Ellipse { Width = 5, Height = 5, Margin = new Thickness(0, 4, 0, 0), Opacity = 0 });
            celula.Child = col;

            var hoverBg = Ui.CorCardHover;
            var hoverBorda = Ui.Pincel(Ui.TemaEscuro ? "#30FFFFFF" : "#18000000");
            celula.MouseEnter += (s, e) =>
            {
                celula.Background = hoverBg;
                if (Repo.Dados.BiselVidro) celula.BorderBrush = hoverBorda;
            };
            celula.MouseLeave += (s, e) =>
            {
                celula.Background = Brushes.Transparent;
                celula.BorderBrush = Brushes.Transparent;
            };
            return celula;
        }

        // ---------- reordenar arrastando (modo de edicao) ----------
        void ConfigurarArrasto(Grid host, Border celula, AppItem app)
        {
            Point? inicio = null;
            celula.MouseLeftButtonDown += (s, e) => inicio = e.GetPosition(null);
            celula.MouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || inicio == null) return;
                var d = e.GetPosition(null) - inicio.Value;
                if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
                inicio = null;
                _ocupado = true;
                try { DragDrop.DoDragDrop(celula, new DataObject(FormatoApp, _pasta.Apps.IndexOf(app).ToString()), DragDropEffects.Move); }
                finally { _ocupado = false; Activate(); }
            };
            host.AllowDrop = true;
            host.DragOver += (s, e) => { if (e.Data.GetDataPresent(FormatoApp)) { e.Effects = DragDropEffects.Move; e.Handled = true; } };
            host.Drop += (s, e) =>
            {
                if (!e.Data.GetDataPresent(FormatoApp)) return;
                int de;
                if (!int.TryParse((string)e.Data.GetData(FormatoApp), out de)) return;
                int para = _pasta.Apps.IndexOf(app);
                e.Handled = true;
                if (de < 0 || para < 0 || de == para) return;
                var item = _pasta.Apps[de];
                _pasta.Apps.RemoveAt(de);
                _pasta.Apps.Insert(para, item);
                Salvar();
            };
        }

        // ---------- menus (botao direito) ----------
        ContextMenu NovoMenu()
        {
            var m = new ContextMenu();
            m.Opened += (s, e) => _ocupado = true;
            m.Closed += (s, e) => _ocupado = false;
            return m;
        }

        MenuItem Item(string texto, Action acao)
        {
            var mi = new MenuItem { Header = texto };
            mi.Click += (s, e) => acao();
            return mi;
        }

        ContextMenu MenuApp(AppItem app)
        {
            var m = NovoMenu();
            m.Items.Add(Item("Mover para a esquerda", () => Mover(app, -1)));
            m.Items.Add(Item("Mover para a direita", () => Mover(app, 1)));
            m.Items.Add(Item("Remover da pasta", () => { _pasta.Apps.Remove(app); Salvar(); }));
            return m;
        }

        ContextMenu MenuTitulo()
        {
            var m = NovoMenu();
            m.Items.Add(Item("Renomear...", Renomear));
            m.Items.Add(Item("Mudar cor...", MudarCor));
            m.Items.Add(new Separator());
            m.Items.Add(Item("Nova pasta...", NovaPasta));
            m.Items.Add(Item("Configurações...", () => { FecharSuave(); App.AbrirConfiguracoes(); }));
            m.Items.Add(new Separator());
            m.Items.Add(Item("Excluir esta pasta", Excluir));
            return m;
        }

        // ---------- acoes ----------
        void Dialogo(Action a)
        {
            _ocupado = true;
            try { a(); } finally { _ocupado = false; Activate(); }
        }

        // No modo de edicao as mudancas ficam como rascunho (visiveis no painel) ate clicar em Salvar;
        // fora dele (menu de botao direito, soltar arquivos) gravam na hora.
        void Salvar()
        {
            if (_editando) { _alterado = true; Montar(); }
            else Persistir();
        }

        void Persistir()
        {
            Repo.Salvar();
            Shell.AtualizarPasta(_pasta);
            _alterado = false;
            Montar();
        }

        void AlternarEdicao()
        {
            if (!_editando)
            {
                _snapshot = new JavaScriptSerializer().Serialize(_pasta);
                _alterado = false;
                _editando = true;
                Montar();
                return;
            }
            if (!ResolverAlteracoes()) return;
            _editando = false;
            Montar();
        }

        // Retorna false se o usuario cancelou (continua editando).
        bool ResolverAlteracoes()
        {
            if (!_alterado) return true;
            var r = MessageBoxResult.Cancel;
            Dialogo(() => r = MessageBox.Show("Salvar as altera\u00e7\u00f5es desta pasta?", "Pastas na barra", MessageBoxButton.YesNoCancel));
            if (r == MessageBoxResult.Cancel) return false;
            if (r == MessageBoxResult.Yes) Persistir(); else Descartar();
            return true;
        }

        void Descartar()
        {
            if (_snapshot != null)
            {
                var o = new JavaScriptSerializer().Deserialize<Pasta>(_snapshot);
                _pasta.Cor = o.Cor; _pasta.MostrarNome = o.MostrarNome;
                _pasta.Apps.Clear(); _pasta.Apps.AddRange(o.Apps);
            }
            _alterado = false;
            Montar();
        }

        void Mover(AppItem app, int delta)
        {
            int i = _pasta.Apps.IndexOf(app), j = i + delta;
            if (i < 0 || j < 0 || j >= _pasta.Apps.Count) return;
            _pasta.Apps.RemoveAt(i); _pasta.Apps.Insert(j, app);
            Salvar();
        }

        void Renomear()
        {
            string nome = null;
            Dialogo(() => nome = Ui.PedirTexto(this, "Renomear pasta", _pasta.Nome));
            if (nome == null || nome == _pasta.Nome) return;
            if (!Repo.NomeDisponivel(nome, _pasta)) { Dialogo(() => MessageBox.Show("Já existe uma pasta com esse nome.", "Renomear pasta")); return; }
            Shell.ApagarAtalho(_pasta);
            _pasta.Nome = nome;
            Persistir();
        }

        void DefinirRotulo()
        {
            string texto = null;
            Dialogo(() => texto = Ui.PedirTexto(this, "Texto no ícone da barra", _pasta.Rotulo ?? ""));
            if (texto == null) return;
            _pasta.Rotulo = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
            Salvar();
        }

        void MudarCor()
        {
            string cor = null;
            Dialogo(() => cor = Ui.EscolherCor(this));
            if (cor == null) return;
            _pasta.Cor = cor;
            Salvar();
        }

        void NovaPasta()
        {
            string nome = null;
            Dialogo(() => nome = Ui.PedirTexto(this, "Nome da nova pasta", ""));
            if (nome == null) return;
            if (!Repo.NomeDisponivel(nome, null)) { Dialogo(() => MessageBox.Show("J\u00e1 existe uma pasta com esse nome.", "Nova pasta")); return; }
            var p = Repo.Criar(nome);
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            FecharSuave();
            App.MostrarFixar(new[] { p });
        }

        void Excluir()
        {
            bool sim = false;
            Dialogo(() => sim = MessageBox.Show("Excluir a pasta '" + _pasta.Nome + "'?\nOs apps em si não são apagados. Depois, desafixe o ícone dela da barra.",
                "Excluir pasta", MessageBoxButton.YesNo) == MessageBoxResult.Yes);
            if (!sim) return;
            Shell.ApagarAtalho(_pasta);
            Repo.Dados.Pastas.Remove(_pasta);
            Repo.Salvar();
            FecharSuave();
        }

        // ---------- posicao: junto da barra de tarefas (embaixo, em cima ou nas laterais), perto de onde o mouse clicou ----------
        void Reposicionar()
        {
            if (!IsLoaded) return;
            var src = PresentationSource.FromVisual(this);
            if (src == null || src.CompositionTarget == null) return;
            var m = src.CompositionTarget.TransformFromDevice;
            var tela = WF.Screen.FromPoint(_cursor);
            var area = tela.WorkingArea; var limites = tela.Bounds;
            var tl = m.Transform(new Point(area.Left, area.Top));
            var br = m.Transform(new Point(area.Right, area.Bottom));
            var cur = m.Transform(new Point(_cursor.X, _cursor.Y));
            double w = ActualWidth, h = ActualHeight;
            Func<double, double, double, double> limitar = (v, min, max) => Math.Max(min, Math.Min(max, v));
            if (area.Left > limites.Left)          // barra na esquerda
            { Left = tl.X; Top = Math.Max(tl.Y, limitar(cur.Y - h / 2, tl.Y, br.Y - h)); }
            else if (area.Right < limites.Right)   // barra na direita
            { Left = br.X - w; Top = Math.Max(tl.Y, limitar(cur.Y - h / 2, tl.Y, br.Y - h)); }
            else if (area.Top > limites.Top)       // barra em cima
            { Left = limitar(cur.X - w / 2, tl.X, br.X - w); Top = tl.Y; }
            else                                   // barra embaixo (padrao)
            { Left = limitar(cur.X - w / 2, tl.X, br.X - w); Top = Math.Max(tl.Y, br.Y - h); }
        }
    }
}
