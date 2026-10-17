# QMS · CEP — versão C# (.NET 10 + Blazor)

Plataforma de qualidade com foco em Controle Estatístico de Processo, com interface no estilo Indústria 5.0.
Esta é a versão em C# que substitui o protótipo em Python (pasta raiz `app/`), mantido no repositório apenas como referência.

> Ambiente de teste. Não declare conformidade com AIAG, IATF ou ISO sem validação específica.

## O que já existe

| Fase | Funcionalidade |
|---|---|
| F0 | Login (ASP.NET Core Identity), perfis com permissões, troca de senha obrigatória, usuários e histórico (auditoria append-only) |
| F1 | Cadastros Área → Produto → Linha → Posto → Tooling → Característica, Instrumentos e Definições versionadas (bom/atenção/ruim) |
| F2 | Coleta online pensada para tablet, importação CSV/XLSX com prévia, templates para download |

Próxima fase: **F3**, com as cartas CEP, o painel diário e a lista de Cp/Cpk.

## Estrutura

```
csharp/
  Qms.slnx
  src/
    Qms.Domain/          entidades e regras de domínio (sem dependências)
    Qms.Statistics/      motor estatístico puro (usado a partir da F3)
    Qms.Application/     contratos de casos de uso, Definições, validador de coleta
    Qms.Infrastructure/  EF Core (SQLite/SQL Server), Identity, auditoria, serviços, migrações
    Qms.Web/             Blazor Interactive Server (telas, tema, endpoints de template)
  tests/Qms.Tests/       xUnit
  deploy/iniciar.bat     script de execução da versão publicada
```

## Desenvolvimento

Pré-requisito: .NET 10 SDK.

```
dotnet tool restore                  # dotnet-ef e csharpier
dotnet build Qms.slnx
dotnet test Qms.slnx
dotnet csharpier format src tests    # formatação obrigatória antes de commit
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/Qms.Web --urls http://127.0.0.1:8080
```

Em `Development`, o banco SQLite `src/Qms.Web/data/qms.db` é criado automaticamente. O seed cria:

- os perfis;
- a versão 1 das Definições;
- dados de exemplo (Montagem / Produto A / Linha 1 / Posto 10 / TL-001 / Altura e INS-001);
- o usuário **admin / admin**, que é obrigado a trocar a senha no primeiro acesso.

Nada disso é criado fora de `Development`.

Para gerar uma migração nova:

```
dotnet ef migrations add <Nome> --project src/Qms.Infrastructure --startup-project src/Qms.Web --output-dir Persistence/Migrations
```

## Teste local publicado (sem SDK)

```
dotnet publish src/Qms.Web -c Release -o C:\QMS-teste\QMS-CEP-csharp\app --no-self-contained
```

Depois, copie `deploy/iniciar.bat` para `C:\QMS-teste\QMS-CEP-csharp\` e dê dois cliques. O sistema abre em http://127.0.0.1:8080 e usa apenas o runtime .NET 10 do Windows.

## Banco de produção

Em `appsettings.Production.json`, use `"Database": { "Provider": "SqlServer" }` e a connection string `Default`.

**Pendência:** as migrações atuais foram geradas para SQLite. Antes de ir para o SQL Server, é preciso gerar um conjunto de migrações específico para ele.

## Pendências conhecidas

- Gerar migrações para SQL Server e o guia de publicação no IIS.
- Usar `IDbContextFactory` nos serviços usados pelo Blazor Server, para evitar concorrência no mesmo DbContext durante o circuito.
- Traduzir para pt-BR as mensagens padrão do Identity (`IdentityErrorDescriber`).
