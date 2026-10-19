# QMS CEP — roteiro de entrega

Requisitos acordados em 17/10/2026. Aplicação web C# / ASP.NET Core / Blazor para servidor corporativo. Base copiada de Desktop/Claude/#Controle Estatistico/csharp; original preservada. A cópia não inclui bancos de dados, binários ou caches.

Atualização de escopo em 19/10/2026: Kappa e Gage R&R retirados. Foco em CEP e Cp/Cpk. Sprints 0, 1 e 2 entregues para validação local; homologação corporativa permanece separada.

## Requisitos transversais

- Todo módulo deve oferecer histórico consultável: autor, data/hora, origem, parâmetros, revisões, resultado, situação e alterações. Estudos aprovados não são sobrescritos; correções geram revisão. Auditoria técnica e histórico operacional são complementares.
- Gestão visual diária por data local de São Paulo, área, linha, posto, tooling e característica: planejado, realizado, pendente, atrasado, em análise e concluído; responsável, última atualização e acesso ao histórico.
- Progresso de preenchimento e cumprimento da programação diária são indicadores distintos. O primeiro usa amostras completas/meta do conjunto; o segundo depende da programação cadastrada. Sem programação, apresentar “sem meta definida”, nunca inventar percentual.
- Não confundir andamento da coleta com aprovação estatística. Um conjunto completo pode apresentar sinais de instabilidade.
- Entradas pelo frontend (digitação, colagem e CSV/XLSX) e seleção de dados armazenados usam a mesma validação. Integração com banco externo depende de contrato de dados e acesso da TI.
- Preservar leituras originais, médias, identificação dos registros utilizados, revisão da característica e versão do método e das definições.
- Permissões no servidor, histórico e testes fazem parte do aceite de cada sprint.

## Coleta e interpretação

- CEP: conjunto inicial de 25 amostras com 3 leituras cada, total 75. Cada ponto X̄ usa a média das três leituras; a carta R usa sua amplitude.
- Cp/Cpk: 33 amostras com 3 leituras cada, total 99, considerando as 33 médias conforme solicitado. Definir e validar a estimativa de dispersão e a interpretação dos limites antes de implementar os índices, distinguindo resultados de médias e de medições individuais.
- Rascunho pode estar incompleto; conclusão exige a quantidade completa e válida. Seleção pela base deve mostrar os registros escolhidos explicitamente.

## Sprints e critérios de aceite

| Sprint | Entrega | Aceite |
|---|---|---|
| 0 | Consolidar cópia C#, SDK, compilação, testes e diagnóstico | Evidências de build, testes e inicialização; bloqueios documentados |
| 1 | Conjuntos CEP 25 × 3, entrada e seleção da base | 75 leituras persistidas, rascunho, histórico do conjunto e progresso de preenchimento |
| 2 | Motor X̄-R e cartas | Conjuntos de referência conferidos, limites e sinais corretos, análise reproduzível |
| 3 | CEP operacional e gestão diária | Programação diária, metas, pendências, limites aprovados, regras e ocorrências; histórico navegável |
| 4 | Cp/Cpk 33 × 3 | Método validado, índices conferidos e estudos salvos com revisão |
| 5 | Capabilidade completa | Pp/Ppk, histograma, normalidade e PPM conferidos |
| 6 | Gestão e relatórios | Ações, aprovações, filtros históricos e exportação PDF/Excel |
| 7 | Homologação corporativa | SQL Server, publicação, HTTPS, concorrência, backup e restauração testados |

## Verificação por sprint

1. Compilar e executar os testes afetados e a suíte de regressão.
2. Conferir persistência, rastreabilidade, permissões e entradas inválidas.
3. Conferir resultados estatísticos com casos de referência documentados.
4. Exercitar o fluxo funcional; registrar quais verificações foram automatizadas e quais foram manuais.
5. Atualizar relatório com resultados reais, pendências e limitações. Não declarar homologação por inspeção de código.

## Pendências estruturais identificadas na Sprint 0

- Na Sprint 0 o motor ainda não tinha implementação; o motor X̄-R e as cartas foram entregues na Sprint 2.
- Histórico existente é auditoria; faltam entidades de conjuntos, estudos, programação diária e histórico operacional.
- Painel atual conta usuários e subgrupos do dia; não mede metas ou conclusão de estudos.
- Migrações atuais SQLite: gerar e validar conjunto específico para SQL Server antes da produção.
- Revisar ciclo de vida do DbContext no Blazor para operações concorrentes.
- Login corporativo, capacidade do servidor e volume de usuários dependem da TI.
