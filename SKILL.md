---
name: qms-cep
description: Retomar e concluir sprints do sistema web QMS CEP em C#, mantendo testes, histórico, progresso diário e método de uso Foxconn. Use neste repositório de controle estatístico.
---

# Continuação do QMS CEP

Esta skill acompanha o projeto; não implica instalação global. Os caminhos abaixo são relativos à raiz do repositório.

## Estado e fontes de decisão

- Versão vigente: C# / .NET 10 / Blazor Interactive Server, na raiz. O Python anterior foi substituído e continua disponível no histórico Git.
- Sprint 1: coleta CEP 25 × 3, rascunho, seleção da base, histórico e progresso de preenchimento. Ler `docs/sprint-1-resultados.md` para evidências e limites.
- Próxima entrega: Sprint 2, motor X̄-R e cartas. Ler `docs/roteiro-sprints.md`, `docs/referencias/requisitos-referencias.md` e `docs/spc-formulas.md`.
- `docs/referencias/frontend-cep-desejado.png` é o modelo solicitado para a análise: carta à esquerda, distribuição lateral e indicadores abaixo. Preservar a distinção entre especificação, nominal, média e limites de controle.
- `docs/referencias/especificacoes-tooling.png` é a fonte dos 11 cadastros. `ReferenceFixtures.cs` usa IDs provisórios e tolerâncias assimétricas; não substituir revisões existentes pelo seed.

## Invariantes do produto

- CEP: 25 amostras, três leituras cada. Usar média na carta X̄ e amplitude na carta R; preservar todas as leituras e o rastreio.
- Cp/Cpk: 33 amostras, três leituras cada, considerando médias conforme pedido. Antes de implementar, documentar e validar a estimativa de variação e sua interpretação. Não publicar índices sem método validado.
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
