---
name: qms-cep-sprint-01
description: Retomar o QMS CEP após a Sprint 1, usando o estado validado da coleta 25 por 3, os cadastros fornecidos e o método Foxconn para iniciar as cartas CEP.
---

# Ponto de retomada da Sprint 1

Ficha Excel: `/cep-coleta` disponibiliza `Modelo_CEP_25x3.xlsx`, com 25 linhas e média/amplitude automáticas. Copiar somente B8:D32. O template de upload da base continua específico por característica em `/coleta`. Preferência: economizar consumo com contexto curto, scripts reaproveitados e verificações focadas.

Use os arquivos a partir da raiz deste repositório, mesmo quando esta skill estiver em uma subpasta.

- Leia `SKILL.md` da raiz para invariantes e fechamento das próximas sprints.
- Leia `docs/sprint-1-resultados.md` antes de declarar o que já foi validado.
- O conjunto é persistido em `cep_datasets` e `cep_samples`; `CepDatasetService` isola o DbContext por operação, valida permissões e grava alterações e auditoria na mesma transação.
- Telas atuais: `/cep-coleta` e `/cep-dia`. A seleção da base vincula subgrupos existentes e preserva seus IDs, operador, horário e leituras; a importação CSV/XLSX usa `/coleta/importar` antes dessa seleção.
- `docs/manual/Metodo de Uso QMS CEP Sprint 1.docx` é minuta, com código e aprovação a preencher. Os prints são da demonstração real, não uma carta já implementada.
- Os 11 cadastros Athena - Lebi e Peak Mech - Sayan são criados em Development, sem medições fabricadas. A configuração local mantém admin/admin, sem troca obrigatória nesse modo.
- A imagem solicitada para o próximo frontend está em `docs/referencias/frontend-cep-desejado.png`. Cp acima de 1,67, Cpk acima de 1,33 e regras de sete pontos são requisitos a implementar e testar, não resultados medidos desta sprint.

Ao iniciar a Sprint 2, implemente o motor X̄-R separado da interface e compare os resultados com casos de referência. A carta deve usar as 25 médias, com uma carta R complementar. Separe os limites de especificação dos limites calculados e registre parâmetros e versão do método no histórico da análise. Não antecipe a interpretação de Cp/Cpk sobre médias antes de definir o método.
