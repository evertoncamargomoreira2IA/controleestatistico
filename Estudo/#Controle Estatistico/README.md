# MVP de CEP (Controle Estatístico de Processo)

Protótipo web estático para cadastrar uma característica contínua, inserir subgrupos racionais e analisar cartas XÌ„-R e capabilidade Cp/Cpk e Pp/Ppk.

> **Protótipo demonstrativo:** os dados não são salvos; ao recarregar a página tudo é perdido. Não é um QMS de produção e não certifica conformidade normativa.

## Como executar

Na raiz desta pasta, execute `python -m http.server 8000` e abra `http://localhost:8000/`. Testes: `http://localhost:8000/tests/spc.test.html`.

## Uso

Configure a característica, informe LSL e USL e o tamanho constante do subgrupo (2 a 10). Cole uma linha por subgrupo, com medições separadas por ponto e vírgula, tab ou espaço. Vírgula ou ponto decimal são aceitos. O botão “Carregar exemplo” preenche 25 subgrupos; “Limpar” remove a entrada e os resultados.

## Estrutura

- `index.html`, `src/styles.css`, `src/app.js`: interface e apresentação SVG.
- `src/spc.js`: validação e funções estatísticas puras.
- `src/sample-data.js`: gerador determinístico dos dados demonstrativos.
- `src/constants.js`: constantes e versão do método.
- `tests/`: mini-runner sem dependências externas.
- `docs/spc-formulas.md`, `docs/business-rules.md`, `docs/release-notes.md`: método e regras.

## Limitações

Sem persistência, autenticação, auditoria ou backend. Apenas subgrupos constantes n=2..10 e limites bilaterais obrigatórios. Estabilidade é avaliada somente por pontos fora de 3σ; regras Nelson/Western Electric não estão implementadas. Normalidade não é testada, MSA não é avaliado e subgrupos racionais são assumidos. Capabilidade exige interpretação cuidadosa e não deve ser tomada como liberação de produto. A tabela de constantes requer validação organizacional antes do uso produtivo.

## Próximos passos

Validar método/constantes com Qualidade; validar interface com usuários; definir persistência e rastreabilidade; implementar backend .NET conforme arquitetura-alvo quando apropriado; adicionar regras Nelson/Western Electric configuráveis, MSA, análise de distribuição/normalidade, autenticação, autorização, auditoria e revisão de segurança.

## Plataforma (Python)

A plataforma adiciona backend FastAPI e mantém o protótipo estático acima como demonstração independente. O código Python pode ser verificado com `ruff check` e formatado com `ruff format`. Para coletar amostras, acesse o menu **Coleta**, selecione a característica, baixe o template CSV ou XLSX e importe o arquivo preenchido; a prévia apresenta as validações antes da confirmação.

No Windows, na raiz do projeto:

```powershell
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
copy .env.example .env
alembic upgrade head
python scripts\create_admin.py
uvicorn app.main:app --reload
pytest
```

O `.env.example` deixa `SECRET_KEY` vazio para acionar o fallback sinalizado de desenvolvimento. Gere e configure uma chave aleatória forte no `.env` antes de hospedar; fora do desenvolvimento a aplicação exige essa chave. O banco padrão é `./data/qms.db`; para SQL Server, configure `DATABASE_URL` com URL `mssql+pyodbc` e instale `requirements-sqlserver.txt`. HTMX é carregado localmente de `app/static/vendor/htmx.min.js`; baixe a distribuição oficial para esse caminho antes de habilitar interações HTMX.


Para preparar a plataforma F1 após aplicar a migração, execute:

```powershell
alembic upgrade head
python scripts\seed_demo.py
```
