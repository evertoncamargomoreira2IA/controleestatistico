# Sprint 4 — Cp/Cpk e simulados de importação

20/10/2026. Implementação disponível para teste local. O solicitante autorizou iniciar esta sprint e adiar a validação visual da Sprint 3; a pendência anterior não foi encerrada.

## Entrega

- `/cp-coleta`: 33 amostras × 3 leituras, rascunho, conclusão, histórico e rastreabilidade. Digitação, colagem, upload CSV/XLSX de leituras e seleção de dados da base. Reutiliza as regras do serviço de coleta, com tipo persistido `CAPABILITY` e 33 posições; CEP permanece com 25 posições e tipo `CEP`.
- `/cp-dia`: progresso e conjuntos criados na data local, filtro de processo e indicação de análise salva. Mede preenchimento, não meta de programação diária.
- `/cp-estudos`: executar e consultar estudos Cp/Cpk, fórmulas, sigma, Cp/Cpk/Cpu/Cpl, faixas numéricas, sinais informativos, contagens fora da especificação, médias e 99 leituras. Histórico de estudos com autor, data e versões, mais snapshot completo de entradas e definições. O painel inicial passou a mostrar a contagem real de estudos.
- Método `cp-cpk-33means-mr2-v1`: série das 33 médias, variação local por 32 amplitudes móveis / 1,128. Interpretação, referências, casos analíticos e limites em `metodo-cp-cpk.md`. Não usa desvio padrão global como sigma local, nem atribui índices de médias às 99 leituras individuais.
- Estudos são imutáveis e salvos na mesma transação da auditoria. Reexecução idêntica reutiliza o registro; outra versão de definições gera novo estudo, preservando os resultados anteriores. Técnico coleta/consulta; Engenharia e Administração coletam/analisam; Qualidade analisa; Leitor consulta. Autorização verificada no servidor.
- Upload direto disponível também em CEP. Arquivos de leituras possuem quatro colunas: `amostra;leitura1;leitura2;leitura3`; XLSX utiliza a primeira aba. Excesso é rejeitado sem truncamento; fórmulas nas leituras, posições repetidas, não finitos, arquivo corrompido e arquivos acima de 5 MB são rejeitados. O usuário confirma substituição, confere a grade e salva; o envio não grava sem essa etapa.
- O importador da base mantém seu template completo e fluxo separado. O arquivo direto contém leituras; instrumento, data, turno e lote são preenchidos no frontend. Valores vindos da base preservam o rastreio original.

## Arquivos simulados

`src/Qms.Web/wwwroot/modelos/` contém CSV e XLSX para cada módulo:

- `Simulado_CEP_25x3`: 25 amostras / 75 leituras.
- `Simulado_CP_CPK_33x3`: 33 amostras / 99 leituras.

Os quatro downloads aparecem nas telas de coleta correspondentes. São dados fictícios, sem inserção automática no banco e sem substituir os cadastros dos 11 dispositivos. Selecionar **Altura / TL-001**, nominal 10 mm, LSL 9,5 e USL 10,5; usar instrumento INS-001 ativo e calibrado, data local válida, turno T1 e lote SIMULADO. Não interpretar os mesmos valores como medições das características dos dispositivos Athena/Peak.

Referência CEP: média 10,000000; R médio 0,080000; LIC X 9,918160; LSC X 10,081840; LSC R 0,205920. Referência Cp/Cpk: média 9,999090909091; MR médio 0,06; sigma 0,053191489362; Cp 3,133333333333; Cpk 3,127636363636. A aba `Conferencia` dos arquivos Excel possui fórmulas recalculadas e verificadas; os testes importam os arquivos efetivamente entregues.

## Evidências

- **121 testes aprovados, zero falhas e zero ignorados**, regressão `artifacts/TestResults/sprint4-final.trx`. Inclui permissões, isolamento dos módulos, variação nula, especificações inválidas/unilaterais, Cpk negativo, cronologia, rascunho, alteração de metas, imutabilidade, auditoria, arquivos reais CSV/XLSX e entradas inválidas.
- **43 verificações HTTP aprovadas**, incluindo rotas CEP/Cp/Cpk, acesso anônimo bloqueado, autenticação, CSRF, textos iniciais de 33 amostras e os quatro downloads. HTTP não é teste interativo do navegador.
- Migração `20261020193841_Sprint4Capability`: coluna StudyKind com padrão CEP nos conjuntos antigos e nova tabela de estudos. Teste de upgrade preserva leituras e análise antiga na estrutura real da Sprint 3.
- Banco local: backup `artifacts/before-sprint4.db` antes da migração. Conferência de contagem e SHA-256 das colunas anteriores em oito tabelas: três conjuntos, 75 posições e uma análise preservados; tabelas operacionais estavam vazias. Evidências `artifacts/sprint4-before.json` e `artifacts/sprint4-preservation-ok.txt`, ignoradas no Git. Nenhum conjunto simulado foi gravado automaticamente no banco do usuário.
- Word no padrão Foxconn: `manual/Metodo de Uso CP CPK Sprint 4.docx`, três páginas renderizadas e inspecionadas, incluindo reprodução da aba de conferência do Excel e roteiro de teste. A figura é do arquivo Excel, não uma captura da interface. Código e aprovação permanecem a preencher pelo controle documental.

## Pendências e limites

A tentativa de iniciar CUA nesta sprint também falhou no launcher do Windows (`trusted Node process exited unexpectedly`); não houve teste interativo nem capturas das novas telas. Os fluxos devem passar por aceite visual, incluindo celular, upload, edição, conclusão e consulta de resultados. A pendência visual da Sprint 3 continua adiada por solicitação explícita.

Cp/Cpk são índices preliminares das médias. As regras de estabilidade são fixas do método, informativas; metas numéricas vêm das definições salvas. As metas do usuário Cp 1,67 e Cpk 1,33 podem ser ajustadas nas definições, sem sobrescrever silenciosamente configurações existentes. Não existe aprovação automática de capabilidade, intervalo de confiança, teste de normalidade, Pp/Ppk, PPM, nova programação diária ou investigação de Cp/Cpk nesta sprint. Essas extensões seguem o roteiro futuro. SQL Server e implantação corporativa continuam na Sprint 7.

## Roteiro de teste do usuário

1. Entrar como admin/admin na demonstração e criar CEP de teste em Altura/TL-001.
2. Informar instrumento, data, turno e lote; marcar a substituição e enviar o CSV ou XLSX de 25 amostras. Conferir, salvar, concluir e abrir cartas.
3. Criar Cp/Cpk na mesma característica, enviar o simulado de 33 amostras e concluir. Abrir análise e comparar Cp/Cpk com o Excel.
4. Reabrir a coleta para conferir as 99 leituras e o histórico; consultar progresso e histórico de estudos. Recalcular sem mudar definições deve abrir o mesmo estudo.
5. Enviar o arquivo de 33 amostras no CEP deve produzir rejeição de excesso, sem alterar a grade. Não selecionar outra característica para tentar obter os mesmos resultados numéricos.

Não iniciar Sprint 5 automaticamente.
