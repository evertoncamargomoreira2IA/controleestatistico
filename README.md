# Controle Estatístico de Processo (CEP)

Workspace de desenvolvimento de uma aplicação de Gestão da Qualidade com foco em Controle Estatístico de Processo. Reúne uma plataforma Python e uma continuação em C#/.NET, além de um protótipo web estático usado como referência.

## Projetos

- [`Estudo/#Controle Estatistico/`](Estudo/%23Controle%20Estatistico/) — plataforma Python com backend FastAPI e protótipo estático. Consulte o [README do projeto](Estudo/%23Controle%20Estatistico/README.md) para instalação, configuração e limitações.
- [`Estudo/QMS-CEP-csharp/`](Estudo/QMS-CEP-csharp/) — continuação em C# com .NET 10 e Blazor Interactive Server. Consulte o [README da solução](Estudo/QMS-CEP-csharp/README.md) e a documentação em `docs/`.
- `codex-mcp.js`, `codex-run.js` e `start-codex-mcp.cmd` — scripts auxiliares para integração com Codex MCP.

## Executar a versão Python

No PowerShell, a partir da raiz do repositório:

```powershell
cd "Estudo\#Controle Estatistico"
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
alembic upgrade head
python scripts\create_admin.py
uvicorn app.main:app --reload
```

A aplicação fica disponível em `http://localhost:8000`. Para os demais detalhes de configuração e execução, consulte o README da versão Python.

## Executar a versão C#

Com o .NET 10 SDK instalado, no PowerShell a partir da raiz do repositório:

```powershell
cd "Estudo\QMS-CEP-csharp"
dotnet restore Qms.slnx
dotnet test Qms.slnx
dotnet run --project src\Qms.Web\Qms.Web.csproj
```

## Escopo e limitações

As versões Python e C# são projetos em desenvolvimento, e o protótipo estático é demonstrativo. Não considere os resultados como certificação de conformidade ou liberação de produto sem validação do método, das constantes e dos processos pela área de Qualidade. Consulte a documentação de cada projeto antes de utilizá-los.
