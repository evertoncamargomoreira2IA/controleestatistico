---
name: qms-cep
description: Retomar e concluir sprints do sistema web QMS CEP em C#, mantendo testes, histórico, progresso diário e método de uso Foxconn. Use neste repositório de controle estatístico.
---

# Continuação do QMS CEP

Esta skill acompanha o projeto; não implica instalação global. Os caminhos abaixo são relativos à raiz do repositório.

## Estado e fontes de decisão

- Versão vigente: C# / .NET 10 / Blazor Interactive Server, na raiz. O Python anterior foi substituído e continua disponível no histórico Git.
- Sprint 1: coleta CEP 25 × 3, rascunho, seleção da base, histórico e progresso de preenchimento. Ler `docs/sprint-1-resultados.md` para evidências e limites.
- Sprint 3: CEP operacional implementado e verificado em testes/HTTP. Fechamento visual e prints ainda pendentes por falha do launcher do navegador no Windows. Ler `docs/sprint-3-resultados.md` e `skills/qms-cep-sprint-03/SKILL.md` antes de retomar. Não declarar a sprint totalmente concluída. Em 20/10/2026 o usuário autorizou adiar esse aceite visual e executar a Sprint 4 de Cp/Cpk.
- Sprint 2: motor X̄-R e cartas entregues; resultados anteriores preservados. Ler `docs/sprint-2-resultados.md` para o histórico.
- Escopo vigente desde 19/10/2026: somente CEP e Cp/Cpk. Kappa e Gage R&R removidos do produto e do roteiro; valores antigos permanecem apenas no histórico do banco e do Git.
- `docs/referencias/frontend-cep-desejado.png` é o modelo solicitado para a análise: carta à esquerda, distribuição lateral e indicadores abaixo. Preservar a distinção entre especificação, nominal, média e limites de controle.
- `docs/referencias/especificacoes-tooling.png` é a fonte dos 11 cadastros. `ReferenceFixtures.cs` usa IDs provisórios e tolerâncias assimétricas; não substituir revisões existentes pelo seed.

- Sprint 4: Cp/Cpk 33 × 3 e simulados CSV/XLSX implementados, com 121 testes e 43 verificações HTTP aprovados. Ler `docs/sprint-4-resultados.md`, `docs/metodo-cp-cpk.md` e `skills/qms-cep-sprint-04/SKILL.md`. Aceite visual das novas telas ainda pendente. Não iniciar Sprint 5 automaticamente.

## Invariantes do produto

- CEP: 25 amostras, três leituras cada. Usar média na carta X̄ e amplitude na carta R; preservar todas as leituras e o rastreio.
- Cp/Cpk: 33 amostras, três leituras cada, considerando médias conforme pedido. Método implementado e conferido: sigma local das 33 médias por 32 amplitudes móveis / 1,128, com a interpretação e limites em `docs/metodo-cp-cpk.md`. Não confundir índices das médias com os das 99 leituras individuais.
- Toda nova funcionalidade inclui histórico consultável e indicação visual de andamento. Progresso de preenchimento é diferente de meta diária e de aprovação estatística.
- Dados digitados, colados, importados ou selecionados da base passam pelas mesmas regras. Não selecionar, truncar ou inventar medições silenciosamente.
- Conjuntos concluídos são somente leitura; atualizações concorrentes exigem reabertura. Salvamento e auditoria CEP compartilham transação.
- `admin/admin` é configuração solicitada para demonstração local em Development. Não ampliar essa exceção para produção nem enfraquecer a política geral de senhas.

## Desenvolvimento e fechamento da sprint

- Preferência do usuário: economizar consumo. Ler apenas arquivos necessários, reaproveitar verificações válidas, executar testes focados após ajustes pequenos e evitar agentes, buscas ou consultas periódicas sem necessidade. Manter este checkpoint atualizado para retomada curta.
- Ficha Excel CEP: `src/Qms.Web/wwwroot/modelos/Modelo_CEP_25x3.xlsx`, gerada por `scripts/gerar_modelo_cep.mjs`. Copiar B8:D32 para o conjunto; para importação de base usar o template por característica em `/coleta`.

- SDK local, se disponível: `.tools/dotnet/dotnet.exe`. Não versionar SDK, pacotes, binários, bancos ou credenciais locais.
- Verificar com `scripts/verificar.ps1 -Restore -RunName sprintN`; sem `-Restore` quando dependências já estiverem presentes. O script grava TRX em artifacts.
- Executar o fluxo afetado no navegador e testes de autorização e rastreabilidade. Para o smoke de demonstração: `scripts/testar-http.ps1 -BaseUrl http://127.0.0.1:5188`.
- `scripts/iniciar-dev.ps1` inicia o ambiente local; `scripts/gerar_manual.py` usa o template derivado em `docs/manual/modelo-foxconn.docx` e prints reais em `docs/manual/prints`.
- Ao fechar cada sprint, registrar resultados reais e pendências, atualizar o Word, renderizar e inspecionar todas as páginas, gerar uma nova skill versionada em `skills/qms-cep-sprint-NN/SKILL.md` e atualizar este ponto de retomada.
- O usuário solicitou a atualização de `evertoncamargomoreira2IA/controleestatistico` ao final das entregas. Conferir diff, excluir artefatos locais e preservar histórico remoto; não usar force push. Se outra sessão não tiver essa autorização em contexto, obter autorização antes do envio.
