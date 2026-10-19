---
name: qms-cep-sprint-02
description: Retomar o sistema C# QMS após as cartas CEP 25 por 3 da Sprint 2, preservando método, resultados, histórico e escopo CEP e Cp/Cpk.
---

# Retomada após as cartas CEP

Ler `SKILL.md` da raiz e `docs/sprint-2-resultados.md`. Não refazer entregas ou verificações válidas sem motivo. O usuário prefere baixo consumo e nenhum agente paralelo sem autorização.

- Rota `/cep-cartas`: seleciona conjunto concluído por data de criação ou recebe `datasetId`; `analysisId` abre o resultado salvo. Entrada e resultado vêm do snapshot, não do cadastro atual.
- Motor `src/Qms.Statistics/CepCalculator.cs`, método `xbar-r-25x3-v1`: 25 posições únicas, três números finitos, A₂=1,023, D₃=0, D₄=2,574. Precisão integral no cálculo. Regras fixas: fora dos limites e sete pontos estritamente monotônicos no lado solicitado do nominal. Sem nominal: não avaliar tendência. Amplitude zero: aviso.
- Especificação e controle são distintos. Contar separadamente leituras individuais e médias. Não publicar Cp/Cpk com as 25 médias do CEP.
- `CepAnalysisService` valida StudiesRun no servidor; consulta exige View. Tabela `cep_analyses`, índice único conjunto/versão/método; entrada, resultado, data, autor e método imutáveis. Criação e auditoria na mesma transação.
- Chart.js 4.5.1 e licença MIT em wwwroot, sem CDN em execução. Componente `CepPlots` dispõe os gráficos ao sair da tela. Conferência acessível por tabela. Distribuição de seis classes das 25 médias.
- Kappa e Gage R&R removidos do menu, formulários, modelo ativo de definições, classificações e permissão msa.approve. Históricos antigos não são apagados.
- Migração `Sprint2CepAnalyses` apenas adiciona análise e índice. Não remover migrações anteriores. SQL Server ainda precisa migrações e homologação próprias.
- Word complementar: `docs/manual/Metodo de Uso Cartas CEP Sprint 2.docx`, gerado por `scripts/gerar_manual_cep.py`, reutilizando template Foxconn e helpers do manual da coleta. Manter aprovação e código como campos a preencher.
- Próxima etapa acordada: CEP operacional, programação, limites aprovados e ações; depois Cp/Cpk 33 × 3, com método das médias explicitamente validado. Não iniciar outra sprint automaticamente.

Fechar futuras sprints com testes relevantes, relatório, Word renderizado e inspecionado, skill versionada e GitHub atualizado, respeitando autorização vigente e preservando histórico remoto.
