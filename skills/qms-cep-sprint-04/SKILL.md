---
name: qms-cep-sprint-04
description: Retomar a Sprint 4 do QMS web C# com coleta Cp/Cpk de 33 trios, índices sobre médias, simulados CSV/XLSX e pendências de aceite visual. Use para continuar ou validar esta entrega preservando CEP e históricos.
---

# Checkpoint da Sprint 4

Ler o SKILL.md da raiz, docs/sprint-4-resultados.md e docs/metodo-cp-cpk.md. Em 20/10/2026 o usuário autorizou seguir para Cp/Cpk com a validação visual da Sprint 3 adiada. Não encerrar a pendência anterior por inferência. Não iniciar Sprint 5 sem solicitação.

## Invariantes

- CEP: 25 × 3, StudyKind CEP. Cp/Cpk: 33 × 3, StudyKind CAPABILITY. CepDataset/CepSample e o serviço de coleta são compartilhados, com RequiredSamples dinâmico. DailyAsync filtra por tipo; chamadas antigas mantêm CEP como padrão. Não misturar conjuntos no painel ou nas análises. Programações CEP rejeitam conjuntos CAPABILITY.
- Cp/Cpk considera as 33 médias; sigma local = média das 32 amplitudes móveis consecutivas / 1,128. Método cp-cpk-33means-mr2-v1. Não substituir por desvio padrão global, R médio intratrio ou sigma dividido por raiz de três. A aplicação às médias é decisão explícita do projeto, com limites de interpretação documentados.
- Exigir especificação bilateral finita, 33 posições únicas e 99 valores finitos. Variação das médias nula não produz infinito nem aprovação. Cpk negativo permanece. Conclusão exige ordem cronológica não decrescente; posições mantêm a sequência quando horários são iguais.
- CapabilityStudy é append-only, com entradas, definições, resultado, autor e versões. Unicidade por conjunto/versão/versão das definições/método. Auditoria e gravação na mesma transação. Alterar definições gera estudo novo; consultar registros antigos não recalcula nem reclassifica.
- As faixas vêm das definições salvas e não equivalem a aprovação do processo. Não sobrescrever metas existentes. Referência visual solicita Cp 1,67 e Cpk 1,33. Normalidade, independência e estabilidade dependem de avaliação; não atribuir capabilidade das médias às medições individuais.
- StudiesRun autoriza Engenharia, Qualidade e Administração. CollectionCreate mantém Técnico/Engenharia/Administração. View consulta; checagens no servidor. Escopos por operação evitam DbContext de longa duração nos serviços de estudo.

## Arquivos e interface

- /cp-coleta, /cp-dia, /cp-estudos. Coleta e progresso reutilizam componentes com rotas e quantidades distintas. Painel inicial conta estudos reais.
- Upload direto CSV/XLSX em ambos os módulos: primeira aba com amostra/leitura1/leitura2/leitura3, até 25 ou 33 linhas, 5 MB, sem fórmulas nas leituras e sem truncar excesso. Confirmação de substituição, conferência da grade e salvamento explícito. Informar instrumento, data, turno e lote no sistema. Importação da base continua com template próprio completo.
- Simulado_CEP_25x3.csv/.xlsx e Simulado_CP_CPK_33x3.csv/.xlsx em src/Qms.Web/wwwroot/modelos. Dados fictícios para Altura/TL-001, nominal 10, LSL 9,5 e USL 10,5; não são dados dos dispositivos Athena/Peak.
- Gerador scripts/gerar_simulados.mjs usa runtime bundled e artifact-tool. Os testes leem os arquivos reais. Conferência Cp 3,133333333333 / Cpk 3,127636363636. Não carregar simulados automaticamente no banco de uso.
- Word docs/manual/Metodo de Uso CP CPK Sprint 4.docx, gerador scripts/gerar_manual_capabilidade.py. Três páginas renderizadas/inspecionadas. Figura do Excel entregue, sem alegar screenshot das telas. Template Foxconn preservado, código/aprovação a preencher.

## Estado verificado e retomada

121 testes e 43 verificações HTTP aprovados. Migração 20261020193841_Sprint4Capability aplicada com backup local e preservação das tabelas anteriores conferida. Banco local artifacts/sprint1-ui.db; não versionar banco, backup ou artefatos. Análises CEP existentes preservadas. Ver relatório para evidências.

CUA continua falhando antes de abrir o navegador. Não confundir HTTP com teste interativo; faltam aceite visual das telas e prints reais. Próxima sessão deve priorizar teste pelo usuário ou restauração do navegador, sem refazer build/testes aprovados se não houver alterações. Preservar a pendência visual da Sprint 3.

Baixo consumo: sem agentes ou automações sem pedido. Usar scripts/verificar.ps1 quando mudar código; scripts/testar-http.ps1 para smoke local. GitHub autorizado: evertoncamargomoreira2IA/controleestatistico, preservar histórico e não fazer force push. Credenciais admin/admin apenas Development. Sprint 5 (Pp/Ppk/normalidade/PPM) permanece futura.
