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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PastasBarra
{
    public static class GeradorPrints
    {
        public static void Executar(string[] args)
        {
            try
            {
                Repo.Carregar();

                // Garante dados bonitos para o print
                if (Repo.Dados.Pastas.Count == 0)
                {
                    var p = new Pasta
                    {
                        Id = Guid.NewGuid().ToString(),
                        Nome = "Trabalho & Foco",
                        Cor = "#2563EB",
                        Rotulo = "TRAB",
                        MostrarNome = true
                    };
                    p.Apps.Add(new AppItem { Nome = "Visual Studio Code", Alvo = "code.exe", Tipo = "arquivo" });
                    p.Apps.Add(new AppItem { Nome = "Google Chrome", Alvo = "chrome.exe", Tipo = "arquivo" });
                    p.Apps.Add(new AppItem { Nome = "Notion", Alvo = "notion.exe", Tipo = "arquivo" });
                    p.Apps.Add(new AppItem { Nome = "Figma", Alvo = "figma.exe", Tipo = "arquivo" });
                    p.Apps.Add(new AppItem { Nome = "Terminal do Windows", Alvo = "wt.exe", Tipo = "arquivo" });
                    Repo.Dados.Pastas.Add(p);
                }

                // Garante dados no Modo Reflexao para o print de foco ficar completo
                var diaHoje = DateTime.Today.ToString("yyyy-MM-dd");
                var reg = Repo.Dados.HistoricoUso.FirstOrDefault(h => h.Data == diaHoje);
                if (reg == null)
                {
                    reg = new RegistroDiaUso { Data = diaHoje };
                    reg.Apps.Add(new ItemUsoApp { Nome = "Visual Studio Code", Processo = "Code", Segundos = 8420 });
                    reg.Apps.Add(new ItemUsoApp { Nome = "Google Chrome", Processo = "chrome", Segundos = 5140 });
                    reg.Apps.Add(new ItemUsoApp { Nome = "Terminal / PowerShell", Processo = "WindowsTerminal", Segundos = 2340 });
                    reg.Apps.Add(new ItemUsoApp { Nome = "Figma", Processo = "Figma", Segundos = 1860 });
                    reg.Apps.Add(new ItemUsoApp { Nome = "Notion", Processo = "Notion", Segundos = 950 });
                    Repo.Dados.HistoricoUso.Add(reg);
                }

                Repo.Dados.RastrearTempo = true;
                Repo.Dados.EstiloVisual = "Vidro";
                Repo.Dados.OpacidadeVidro = 90;
                Repo.Dados.BiselVidro = true;
                Repo.Dados.CantosArredondados = 18;

                var pastaArtefato = @"C:\Users\matth\.gemini\antigravity\brain\9991d2a6-9f4a-4b7b-9f52-21618b8e2180\screenshots";
                var pastaRepo = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "screenshots"));
                try { Directory.CreateDirectory(pastaArtefato); } catch { }
                try { Directory.CreateDirectory(pastaRepo); } catch { }

                var app = new Application();

                // 1. Janela de Configuracoes - Aba 0 (Pastas)
                var wConfig = new ConfiguracoesJanela();
                wConfig.SelecionarAba(0);
                wConfig.Show();
                wConfig.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                SalvarDuplo(wConfig.Moldura, "02_configuracoes_pastas.png", pastaArtefato, pastaRepo);

                // 2. Janela de Configuracoes - Aba 1 (Aparencia)
                wConfig.SelecionarAba(1);
                wConfig.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                SalvarDuplo(wConfig.Moldura, "03_configuracoes_aparencia.png", pastaArtefato, pastaRepo);

                // 3. Janela de Configuracoes - Aba 2 (Reflexao)
                wConfig.SelecionarAba(2);
                wConfig.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                SalvarDuplo(wConfig.Moldura, "04_configuracoes_reflexao.png", pastaArtefato, pastaRepo);
                wConfig.Close();

                // 4. Painel Popup da Pasta
                var pastaExemplo = Repo.Dados.Pastas.First();
                var pPainel = new Painel(pastaExemplo);
                pPainel.Moldura.Opacity = 1;
                pPainel.Moldura.RenderTransform = null;
                pPainel.Show();
                pPainel.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                SalvarDuplo(pPainel.Moldura, "01_popup_pasta.png", pastaArtefato, pastaRepo);
                pPainel.Close();

                Console.WriteLine("PRINTS_OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO_PRINTS: " + ex);
            }
        }

        static void SalvarDuplo(FrameworkElement elem, string nome, string dir1, string dir2)
        {
            var p1 = Path.Combine(dir1, nome);
            Ui.SalvarElementoComoPng(elem, p1);
            try
            {
                var p2 = Path.Combine(dir2, nome);
                File.Copy(p1, p2, true);
            }
            catch { }
        }
    }
}
