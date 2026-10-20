---
name: qms-cep-sprint-03
description: Retomar a Sprint 3 do QMS CEP operacional para concluir a validação visual pendente, preservando planejamento, metas, limites aprovados, ocorrências e resultados existentes.
---

# Estado da Sprint 3

Ler `SKILL.md` da raiz e `docs/sprint-3-resultados.md`. Sprint 3 implementada, com 95 testes e 30 verificações HTTP aprovados; NÃO está totalmente encerrada. Falta navegador interativo, responsividade e prints novos do manual.

## Bloqueio conhecido

CUA falhou antes de abrir a página com `CreateProcessSecurityEnvironment failed (HRESULT = 0x80070020)`, inclusive após reset. Não repetir testes de servidor aprovados sem nova alteração. Tentar o navegador depois de restabelecido o ambiente e seguir o roteiro de aceite no relatório. Não fabricar prints nem reaproveitar antigos como se fossem da Sprint 3.

## Implementação

- `/cep-operacao`: programação e prazo por data de São Paulo, meta em conjuntos 25 × 3, turno, responsável e revisão. Estado atual, não fechamento histórico congelado. Sem meta definida significa percentual indisponível.
- `CepOperationsService` isola DbContext por operação. Criar/vincular/cancelar programação, aprovar/revogar limite e abrir/atualizar ocorrência compartilham transação com auditoria. Programações/ocorrências usam versão de concorrência.
- Vínculo único por conjunto; conta somente uma programação. Sem exclusão de vínculos: cancelar e registrar nova programação para correções. Mesma característica/revisão e turno; preenchimento, análise e ausência de investigação aberta são condições distintas.
- Aprovação apenas Qualidade/Admin. Base com limites estimados, variação positiva, sem sinais/especificação fora e sem investigação aberta. Uma aprovação por análise; nova versão requer nova base. Eventos de aprovação/revogação e vínculos preservados no DbContext.
- Novas análises da UI usam `operationalRules=true`, com método v2 contendo versão das definições e aprovação usada. `RunAsync` mantém o caminho v1 padrão para compatibilidade. Não alterar resultados históricos.
- `CepCalculator.EvaluateOperational` aplica centros/limites fixos da base quando selecionada, sem substituir média/amplitude observadas. Regras gerais capturadas no resultado, mais regras fixas de sete pontos. Proximidade = distância ao centro da faixa / meia faixa bilateral × 100; sem limites bilaterais não avaliar.
- Limites fixos exigem característica/revisão vigente, outra coleta, aprovação não expirada/revogada e leituras dentro da vigência. Resultado já salvo continua consultável após revogação.
- Uma ocorrência por análise, criada automaticamente em novas análises com sinais, leitura fora da especificação ou variação nula. Prazo inicial 24h, responsável executor. Causa/ação/eficácia obrigatórias para encerrar; reabrir mantém histórico.
- Permissões novas: cep.plan.manage (Admin/Engenharia/Qualidade), cep.limits.approve (Admin/Qualidade), cep.actions.manage (Admin/Engenharia/Qualidade/Técnico). View consulta; CollectionCreate vincula.
- Migração `20261020121937_Sprint3CepOperations` adiciona cinco tabelas. Preserva análises e coletas existentes. SQL Server ainda não homologado.

## Documentação e fechamento

Minuta Word `docs/manual/Metodo de Uso CEP Operacional Sprint 3.docx`, gerador `scripts/gerar_manual_operacional.py`. Template e helpers Foxconn reutilizados, sem prints novos enquanto navegador indisponível. Ao recuperar o navegador: testar os fluxos, salvar prints reais, incluir na minuta, renderizar e inspecionar todas as páginas, atualizar relatório e skill, depois enviar novo commit ao GitHub. Não iniciar Sprint 4 sem autorização.

Preferência persistente do usuário: baixo consumo, contexto curto e reutilização de verificações já válidas. Não criar agentes ou automações sem solicitação. Conta local de demonstração admin/admin apenas em Development.
