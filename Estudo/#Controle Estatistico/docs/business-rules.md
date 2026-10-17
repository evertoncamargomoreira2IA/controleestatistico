# Regras de negócio implementadas

- Nome obrigatório; LSL e USL obrigatórios e numéricos, com LSL < USL. O MVP não permite especificação unilateral.
- Alvo opcional; quando informado, deve ser numérico e estar entre LSL e USL.
- n inteiro de 2 a 10. Cada subgrupo deve ter exatamente n valores finitos. Medições aceitam vírgula ou ponto decimal.
- Mínimo de dois subgrupos para limites e cartas. Menos de 20 gera aviso de limites preliminares; menos de 25 gera aviso de amostra abaixo da recomendação.
- Sinais de instabilidade: somente ponto fora dos limites de 3σ nas cartas X̄ e R. Não são aplicadas regras Nelson/Western Electric.
- Capabilidade é calculada, mas só interpretável após avaliar estabilidade e premissas. Normalidade não testada, MSA não avaliado, subgrupos racionais assumidos.
- PPM fora da especificação é observação empírica; não é estimativa de longo prazo.
- LSL/USL são limites da voz do cliente; LIC/LC/LSC são limites da voz do processo. Especificações só são desenhadas na carta X̄ quando solicitado.
- Sem persistência: dados existem apenas na página atual. Não há certificação de conformidade AIAG/IATF/ISO.
## Coleta de amostras

A coleta online é feita no menu **Coleta** e registra um subgrupo por vez. A característica deve estar ativa e ser contínua, e o subgrupo deve conter exatamente o tamanho `n` configurado. Cada valor precisa ser finito; o separador decimal pode ser vírgula ou ponto.

O instrumento é obrigatório, deve estar ativo e sua calibração precisa estar válida na data da coleta. A data e hora não podem ultrapassar o horário atual em mais de cinco minutos. Horários informados sem fuso são interpretados no horário local `America/Sao_Paulo` e convertidos para UTC. Os turnos aceitos são `T1`, `T2` e `T3`.

A importação aceita arquivos CSV e XLSX, gerados a partir do template disponível para a característica. O arquivo tem limite de 5 MB e a importação aceita até 5000 linhas de dados. A prévia valida as linhas antes da gravação, e somente as linhas válidas são importadas; as demais são contabilizadas como rejeitadas. Um arquivo com o mesmo conteúdo (hash SHA-256) não pode ser importado novamente.

A exclusão de um subgrupo é lógica: o registro permanece para rastreabilidade e exige um motivo. Cada subgrupo guarda a revisão da característica vigente no momento da coleta. Criações, importações e exclusões são registradas na trilha de auditoria.
## Cadastros e Definições (F1)

A hierarquia Área → Produto → Linha → Posto → Tooling → Característica mantém registros inativos para preservar rastreabilidade. Nomes são únicos dentro do pai; código de Tooling é único dentro do Posto e código de instrumento é globalmente único. Um pai com filhos ativos não pode ser desativado. Características contínuas validam LSL < USL e nominal dentro dos limites; atributos não usam especificações. O tamanho de subgrupo deve corresponder ao tipo de carta: I-MR = 1, X̄-R = 2 a 10 e X̄-S = 2 a 25. Alterações em especificações, subgrupo ou carta criam uma revisão da característica.

Definições são imutáveis e versionadas: cada salvamento cria uma nova versão e exige comentário. A versão de maior número é a ativa. Classificações de capabilidade, Gage R&R e Kappa usam os limites registrados em `app/services/settings_service.py`; todas as alterações de cadastros e definições são auditadas.
