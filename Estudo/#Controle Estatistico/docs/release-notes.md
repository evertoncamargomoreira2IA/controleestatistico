# Notas de versão

## 0.2.0-dev — F0

- Base FastAPI, SQLAlchemy 2 e Alembic, com SQLite local e URL configurável para SQL Server.
- Login local Argon2, sessão assinada, proteção CSRF, bloqueio configurável e RBAC.
- Gestão administrativa de usuários, alteração da senha própria e trilha de auditoria.
- Interface pt-BR com módulos futuros identificados como “em breve”.

## 0.3.0-dev — F1 Cadastros e Definições

- Hierarquia de Área a Característica, instrumentos, validações de especificação, subgrupo e revisão.
- Definições versionadas, classificação dos resultados e auditoria de alterações.
- Migração Alembic 0002 e dados genéricos de demonstração.

## 0.4.0-dev — F2 Coleta

- Coleta online de subgrupos e importação CSV/XLSX com template, prévia de validação e importação somente das linhas válidas.
- Validação de característica contínua ativa, tamanho do subgrupo, medições, instrumento ativo e calibração, data/hora e turno; registro da revisão da característica e auditoria.
- Limites de arquivo e linhas, prevenção de reimportação por SHA-256 e exclusão lógica com motivo.
- Correção: a auditoria agora serializa datas; instrumentos com vencimento de calibração deixaram de falhar ao salvar.
- Correção: erros de gravação não são mais rotulados como duplicidade.
- Correção: a importação usa os valores numéricos convertidos, inclusive quando a entrada usa vírgula decimal.
- Correção: a prévia apresenta data e hora no horário local, em vez de UTC.

## 0.4.2-dev - Admin padrao e troca obrigatoria de senha

- Criacao do admin padrao apenas em desenvolvimento e quando nao ha usuarios.
- Troca obrigatoria da senha padrao e apos redefinicao administrativa.
- Migracao adiciona o indicador de troca obrigatoria de senha.
