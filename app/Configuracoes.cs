using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PastasBarra
{
    // Janela de configuracoes: gerenciar pastas e opcoes gerais.
    public class ConfiguracoesJanela : Window
    {
        readonly ListBox _lista = new ListBox { FontSize = 14, Margin = new Thickness(0, 0, 14, 0) };

        public ConfiguracoesJanela()
        {
            Title = "Pastas na barra · Configurações";
            Width = 560; Height = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Pincel("#FFFAFAFA");

            var g = new Grid { Margin = new Thickness(20) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = "Suas pastas", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

            var meio = new Grid();
            meio.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            meio.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            meio.Children.Add(_lista);
            var botoes = new StackPanel { Width = 170 };
            Grid.SetColumn(botoes, 1);
            botoes.Children.Add(Botao("Nova pasta", NovaPasta));
            botoes.Children.Add(Botao("Renomear", Renomear));
            botoes.Children.Add(Botao("Mudar cor", MudarCor));
            botoes.Children.Add(Botao("Fixar na barra...", Fixar));
            botoes.Children.Add(Botao("Nome no \u00edcone (liga/desliga)", AlternarNome));
            botoes.Children.Add(Botao("Excluir", Excluir));
            botoes.Children.Add(Botao("Sugestões de pastas...", () => App.AbrirAssistente()));
            if (Instalador.RodandoInstalado())
                botoes.Children.Add(Botao("Desinstalar...", () => System.Diagnostics.Process.Start(Instalador.ExeAtual, "--desinstalar")));
            meio.Children.Add(botoes);
            Grid.SetRow(meio, 1);
            g.Children.Add(meio);

            var inicio = new CheckBox { Content = "Iniciar junto com o Windows", IsChecked = Shell.IniciarComWindows, Margin = new Thickness(0, 14, 0, 0) };
            inicio.Click += (s, e) => Shell.IniciarComWindows = inicio.IsChecked == true;
            Grid.SetRow(inicio, 2);
            g.Children.Add(inicio);
            Content = g;
            Recarregar();
            Activated += (s, e) => Recarregar();
        }

        static Button Botao(string texto, Action acao)
        {
            var b = new Button { Content = texto, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 8) };
            b.Click += (s, e) => acao();
            return b;
        }

        Pasta Selecionada { get { return _lista.SelectedItem == null ? null : (Pasta)((ListBoxItem)_lista.SelectedItem).Tag; } }

        void Recarregar()
        {
            var id = Selecionada == null ? null : Selecionada.Id;
            _lista.Items.Clear();
            foreach (var p in Repo.Dados.Pastas)
            {
                var li = new ListBoxItem { Tag = p, Content = p.Nome + "   (" + p.Apps.Count + " apps)", Padding = new Thickness(6) };
                _lista.Items.Add(li);
                if (p.Id == id) li.IsSelected = true;
            }
        }

        void NovaPasta()
        {
            var nome = Ui.PedirTexto(this, "Nome da nova pasta", "");
            if (nome == null) return;
            var p = Repo.Criar(nome);
            Repo.Salvar();
            Shell.AtualizarPasta(p);
            Recarregar();
            App.MostrarFixar(new[] { p });
        }

        void Renomear()
        {
            var p = Selecionada; if (p == null) return;
            var nome = Ui.PedirTexto(this, "Renomear pasta", p.Nome);
            if (nome == null || nome == p.Nome) return;
            Shell.ApagarAtalho(p); p.Nome = nome;
            Repo.Salvar(); Shell.AtualizarPasta(p); Recarregar();
        }

        void MudarCor()
        {
            var p = Selecionada; if (p == null) return;
            var cor = Ui.EscolherCor(this);
            if (cor == null) return;
            p.Cor = cor; Repo.Salvar(); Shell.AtualizarPasta(p);
        }

        // Liga/desliga o nome embaixo do icone da pasta selecionada (ou de todas, se nenhuma estiver selecionada).
        void AlternarNome()
        {
            var alvo = Selecionada != null ? new[] { Selecionada } : Repo.Dados.Pastas.ToArray();
            bool ligar = !alvo.All(x => x.MostrarNome);
            foreach (var x in alvo) { x.MostrarNome = ligar; Shell.AtualizarPasta(x); }
            Repo.Salvar();
        }

        void Fixar()
        {
            var p = Selecionada;
            App.MostrarFixar(p != null ? new[] { p } : Repo.Dados.Pastas.ToArray());
        }

        void Excluir()
        {
            var p = Selecionada; if (p == null) return;
            if (MessageBox.Show("Excluir a pasta '" + p.Nome + "'?\nOs apps em si não são apagados. Depois, desafixe o ícone dela da barra.",
                "Excluir pasta", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Shell.ApagarAtalho(p); Repo.Dados.Pastas.Remove(p); Repo.Salvar(); Recarregar();
        }
    }
}
