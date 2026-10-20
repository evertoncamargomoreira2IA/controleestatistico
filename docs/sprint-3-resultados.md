# Sprint 3 — CEP operacional

Data: 20/10/2026. Situação: implementação e testes de servidor concluídos; validação visual e prints pendentes. A sprint ainda não tem fechamento completo.

## Entrega implementada

- `/cep-operacao`: programação por data local de São Paulo, característica/revisão, turno, título, responsável ativo, meta de 1 a 100 conjuntos CEP 25 × 3 e prazo na data planejada.
- Vínculo explícito de conjuntos compatíveis, sem dupla contagem, com controle de versão. Coleta de turno ou revisão incompatível não atende à meta. Cancelamento exige motivo, preserva vínculos e histórico e retira a programação do denominador da meta.
- Painel com meta, coletas completas, conjuntos analisados e percentual de cumprimento. Sem meta, mostrar indicação explícita e nenhum percentual inventado. Planejada, em coleta, em análise, atrasada, concluída e cancelada são situações distintas. Conclusão exige meta de coleta e análise atingidas, sem ocorrência aberta.
- Aprovação de limites por Qualidade/Administração: base estimada, variação positiva, sem sinais, sem leituras fora da especificação, sem investigação aberta e característica ativa na revisão da base. Justificativa e validade obrigatórias. Uma análise origina uma aprovação; nova versão requer nova base. Revogação é evento separado e preserva a decisão original.
- Uso explícito dos limites aprovados em outro conjunto da mesma característica/revisão vigente. Leituras devem ser coletadas durante a vigência; expiração, revogação e incompatibilidade impedem novos cálculos. A base não monitora a si mesma. Resultados anteriores permanecem consultáveis.
- Análise operacional `xbar-r-25x3-v2-s{versão}-l{aprovação ou 0}` com regras das definições preservadas no resultado: fora dos limites em X̄/R, N médias do mesmo lado, N médias em tendência estrita e proximidade da especificação bilateral. As duas regras próprias de sete pontos do projeto permanecem fixas. A linha central da base aprovada é distinta da média observada no novo conjunto.
- Ocorrência automática por análise operacional com sinais, leituras fora da especificação ou variação nula; responsável inicial é o executor e o prazo inicial é 24 horas. Também há abertura manual. A análise tem uma investigação, não uma por sinal. Reanálise idêntica não duplica o registro; nova versão de regras/limites cria outra análise.
- Investigação com causa, ação, eficácia, responsável e prazo. Encerramento exige os três campos técnicos. Reabertura e atualizações mantêm histórico antes/depois. Programações e ocorrências têm tokens de concorrência; vínculos, aprovações e revogações preservam registros originais.

## Evidências verificadas

- 95 testes automatizados aprovados, zero falhas e zero ignorados; regressão final em `artifacts/TestResults/sprint3-final.trx` (ignorado no Git).
- 30 verificações HTTP aprovadas: login admin/admin, CSRF, bloqueio anônimo, rotas autenticadas, menu sem módulos retirados e renderização inicial da programação, formulário de metas e aprovação.
- Testes de vínculo único, meta, sequência coleta/análise/investigação, cancelamento, metadados inválidos, turno errado, responsável inválido, concorrência por versão, encerramento incompleto, aprovação de limites, bloqueio da própria base e de leituras anteriores à vigência, revogação e reprodução do resultado salvo.
- Regras testadas com limites fixos, linha central de referência, deslocamento, sequências, chaves liga/desliga, proximidade e validação de parâmetros. Alterar a versão das regras gera novo resultado sem substituir o anterior.
- Migração SQLite aplicada à demonstração após cópia de segurança. Duas coletas e uma análise anteriores preservadas; hash conjunto dos JSONs antes/depois: `0e08eb0077d6895ef87fab07976440c30a04822800d226113acf4b34de081467`. A conferência foi feita antes de criar novos dados. O teste de migração existente também percorre o esquema atualizado.
- Nenhuma medição nova foi inventada para os 11 dispositivos. Banco e backup permanecem locais, fora do Git.

## Bloqueio e aceite pendente

A automação CUA do navegador falhou ao iniciar, inclusive após reinicialização: `CreateProcessSecurityEnvironment failed (HRESULT = 0x80070020)`, antes de acessar a página. O mesmo problema impede os comandos shell sem a execução autorizada fora do isolamento. Build, testes e HTTP foram executados com a permissão apropriada; a ferramenta do navegador não expõe essa opção.

Não houve teste interativo nem QA visual das telas novas. Nenhum screenshot antigo foi apresentado como evidência da Sprint 3. O Word `Metodo de Uso CEP Operacional Sprint 3.docx` é minuta de uso, sem prints novos, identificada como aguardando validação visual. Reiniciar/reabrir o ambiente de automação e executar o roteiro abaixo é necessário para fechar a sprint.

1. Entrar como admin/admin, abrir CEP operacional e conferir o estado sem meta; testar desktop e celular sem transbordamento da página.
2. Criar programação, vincular um rascunho compatível, preencher e concluir a coleta, analisar e conferir os indicadores. Confirmar rejeição de vínculo duplicado e cancelamento com motivo.
3. Abrir investigação manual e automática; tentar encerrar sem campos, depois registrar causa/ação/eficácia, encerrar, reabrir e consultar antes/depois.
4. Aprovar uma base elegível, selecionar a aprovação em outra coleta feita após a aprovação, conferir cartas com centros fixos, revogar e verificar que resultados antigos continuam consultáveis.
5. Verificar permissões de Técnico, Engenharia, Qualidade e Leitor; testar mudança das regras sem sobrescrever análises anteriores. Registrar prints reais e incorporá-los ao Word; renderizar e inspecionar o documento atualizado.

## Limites funcionais atuais

O painel é o estado atual de programações da data planejada, sem fotografia congelada de fechamento diário. Ocorrências e aprovações mostram todas as datas. Vínculos não são apagados; correção requer cancelamento justificado e nova programação. Não há envio de notificações ou bloqueio automático do processo físico. SQL Server, servidor corporativo e homologação permanecem na Sprint 7. Cp/Cpk não foi iniciado.
