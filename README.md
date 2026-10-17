# QMS CEP em C#

Sistema web de qualidade para servidor corporativo, com .NET 10, Blazor Interactive Server, Entity Framework Core e SQLite no desenvolvimento. SQL Server e homologação no servidor permanecem no roteiro.

## Estado atual — Sprint 1

- Login, perfis, cadastros, instrumentos, definições, coleta e importação CSV/XLSX.
- Conjuntos CEP de **25 amostras × 3 leituras**, com digitação, colagem do Excel ou seleção explícita de amostras da base.
- Rascunho, média e amplitude por amostra, controle de versão, conclusão somente com 75 leituras, histórico antes/depois e bloqueio de edição após conclusão.
- Progresso diário por data de criação e filtro de processo. Percentual de preenchimento; programação e metas do turno na Sprint 3.
- 11 cadastros de teste Athena - Lebi e Peak Mech - Sayan, conforme imagens fornecidas, sem medições inventadas para esses dispositivos.
- Referência visual da carta recebida para a Sprint 2. O motor estatístico ainda será implementado.

## Executar

Instalar o SDK .NET 10. A pasta `.tools/dotnet`, quando presente, é usada pelos scripts locais e não faz parte do repositório.

```powershell
./scripts/verificar.ps1 -Restore -RunName sprint1
./scripts/iniciar-dev.ps1
```

Abrir http://127.0.0.1:5188. **Demonstração local: admin / admin**, conforme solicitado. Somente em Development, `Security:DemoAdminCredentials` mantém esse acesso; a política geral de senha e a configuração de produção não foram relaxadas. Para voltar ao fluxo de primeiro acesso com senha provisória, desabilitar a configuração em um ambiente de desenvolvimento apropriado.

A inicialização aplica as migrações SQLite e cria os cadastros de demonstração, preservando revisões existentes. Sem SDK local, também é possível executar `dotnet run --project src/Qms.Web -c Release --no-launch-profile --urls http://127.0.0.1:5188 --environment Development`.

## Rotas principais

| Rota | Uso |
|---|---|
| `/cep-coleta` | Criar conjunto CEP |
| `/cep-coleta?id=ID` | Preencher ou consultar conjunto e histórico |
| `/cep-dia` | Progresso e consulta por data de criação |
| `/coleta` | Coleta de subgrupos e templates |
| `/coleta/importar` | Importar CSV/XLSX na base antes de selecionar no conjunto |
| `/historico` | Auditoria geral, conforme perfil |

## Documentação e continuidade

- [Roteiro das sprints](docs/roteiro-sprints.md)
- [Resultados da Sprint 0](docs/sprint-0-resultados.md)
- [Resultados da Sprint 1](docs/sprint-1-resultados.md)
- [Método de uso em Word](docs/manual/Metodo%20de%20Uso%20QMS%20CEP%20Sprint%201.docx)
- [Ficha Excel CEP 25 × 3](src/Qms.Web/wwwroot/modelos/Modelo_CEP_25x3.xlsx): preencher e copiar B8:D32 para a colagem do conjunto. Para upload à base, usar o template da característica em `/coleta`.
- [Especificações e frontend desejado](docs/referencias/requisitos-referencias.md)
- [Skill atual de retomada](SKILL.md)
- [Skill da Sprint 1](skills/qms-cep-sprint-01/SKILL.md)

O manual é uma minuta no padrão Foxconn do procedimento de referência, com prints reais de demonstração. Código oficial, revisão e aprovação devem passar pelo controle documental. Para regenerar: executar `scripts/gerar_manual.py` com Python e dependências de documentos; o template sem corpo está em `docs/manual/modelo-foxconn.docx`. Renderizar e conferir todas as páginas após editar.

## Estrutura

- `src/Qms.Domain`: entidades e regras.
- `src/Qms.Application`: contratos e validação.
- `src/Qms.Infrastructure`: persistência, auditoria, Identity e serviços.
- `src/Qms.Statistics`: reservado ao motor estatístico da próxima sprint.
- `src/Qms.Web`: aplicação Blazor.
- `tests/Qms.Tests`: testes xUnit.
- `reference`: motor JavaScript anterior como referência de cálculo.

Compilar e testar com `scripts/verificar.ps1`; evidências locais em `artifacts/`. O workflow GitHub Actions executa build e testes em pushes para main e pull requests.

## Pendências de produção

Migrações SQL Server, configuração do servidor e HTTPS, provisionamento administrativo de produção, credenciais corporativas, carga simultânea, backup e restauração precisam de homologação. A demonstração local não constitui liberação produtiva ou certificação normativa.

A raiz foi consolidada a partir das cópias C# anteriores. Os protótipos Python/HTML e cópias duplicadas removidos da árvore atual continuam disponíveis no histórico Git.
