# QMS CEP em C#

Sistema web de qualidade para servidor corporativo, com .NET 10, Blazor Interactive Server, Entity Framework Core e SQLite no desenvolvimento. SQL Server e homologação no servidor permanecem no roteiro.

## Estado atual — Sprint 3 em validação

- Login, perfis, cadastros, instrumentos, definições, coleta e importação CSV/XLSX.
- Conjuntos CEP de **25 amostras × 3 leituras**, com digitação, colagem do Excel ou seleção explícita de amostras da base.
- Rascunho, média e amplitude por amostra, controle de versão, conclusão somente com 75 leituras, histórico antes/depois e bloqueio de edição após conclusão.
- Progresso diário por data de criação e filtro de processo. Percentual de preenchimento; programação e metas do turno na Sprint 3.
- 11 cadastros de teste Athena - Lebi e Peak Mech - Sayan, conforme imagens fornecidas, sem medições inventadas para esses dispositivos.
- Cartas X̄-R, distribuição lateral das 25 médias, limites estimados, regras de sete pontos e contagens fora da especificação.
- Análises salvas com entrada, resultados, autor, método e revisão; reabertura sem sobrescrever ou duplicar a mesma versão.
- Escopo vigente: CEP e Cp/Cpk. Kappa e Gage R&R retirados dos menus, definições, classificações e permissões; registros antigos preservados.
- CEP operacional: programação por data, característica, turno, responsável, meta e prazo; vínculo explícito de conjuntos e consulta de andamento.
- Aprovação e revogação de limites por Qualidade/Administração; novas análises preservam regras e limites usados. Ocorrências automáticas ou manuais com investigação, ação e eficácia.
- Sprint 3: testes de servidor e HTTP aprovados; validação visual e prints novos ainda pendentes por indisponibilidade da automação do navegador no ambiente Windows. O manual operacional é uma minuta sem prints desta sprint.

- Sprint 4: Cp/Cpk de 33 × 3 sobre as médias, upload direto CSV/XLSX em ambos os módulos, progresso e estudos imutáveis. 121 testes e 43 verificações HTTP aprovados. Aceite visual permanece pendente.

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
| `/cep-cartas` | Executar e consultar análises X̄-R salvas |
| `/cep-operacao` | Programação, metas, limites aprovados, ocorrências e histórico |
| `/cp-coleta` | Coleta Cp/Cpk 33 × 3 e upload de leituras |
| `/cp-dia` | Progresso e histórico de coletas Cp/Cpk |
| `/cp-estudos` | Executar e consultar estudos sobre as 33 médias |
| `/coleta` | Coleta de subgrupos e templates |
| `/coleta/importar` | Importar CSV/XLSX na base antes de selecionar no conjunto |
| `/historico` | Auditoria geral, conforme perfil |

## Documentação e continuidade

- [Resultados da Sprint 4](docs/sprint-4-resultados.md)
- [Método Cp/Cpk sobre médias](docs/metodo-cp-cpk.md)
- [Guia de uso e simulados em Word](docs/manual/Metodo%20de%20Uso%20CP%20CPK%20Sprint%204.docx)
- [Simulado CEP Excel](src/Qms.Web/wwwroot/modelos/Simulado_CEP_25x3.xlsx) · [CSV](src/Qms.Web/wwwroot/modelos/Simulado_CEP_25x3.csv)
- [Simulado Cp/Cpk Excel](src/Qms.Web/wwwroot/modelos/Simulado_CP_CPK_33x3.xlsx) · [CSV](src/Qms.Web/wwwroot/modelos/Simulado_CP_CPK_33x3.csv)
- [Skill da Sprint 4](skills/qms-cep-sprint-04/SKILL.md)
- [Roteiro das sprints](docs/roteiro-sprints.md)
- [Resultados da Sprint 0](docs/sprint-0-resultados.md)
- [Resultados da Sprint 1](docs/sprint-1-resultados.md)
- [Resultados da Sprint 2](docs/sprint-2-resultados.md)
- [Resultados e pendências da Sprint 3](docs/sprint-3-resultados.md)
- [Minuta do método operacional](docs/manual/Metodo%20de%20Uso%20CEP%20Operacional%20Sprint%203.docx)
- [Método das cartas em Word](docs/manual/Metodo%20de%20Uso%20Cartas%20CEP%20Sprint%202.docx)
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
- `src/Qms.Statistics`: motor X̄-R 25 × 3, regras versionadas e Cp/Cpk das 33 médias.
- `src/Qms.Web`: aplicação Blazor.
- `tests/Qms.Tests`: testes xUnit.
- `reference`: motor JavaScript anterior como referência de cálculo.

Compilar e testar com `scripts/verificar.ps1`; evidências locais em `artifacts/`. O workflow GitHub Actions executa build e testes em pushes para main e pull requests.

## Pendências de produção

Migrações SQL Server, configuração do servidor e HTTPS, provisionamento administrativo de produção, credenciais corporativas, carga simultânea, backup e restauração precisam de homologação. A demonstração local não constitui liberação produtiva ou certificação normativa.

A raiz foi consolidada a partir das cópias C# anteriores. Os protótipos Python/HTML e cópias duplicadas removidos da árvore atual continuam disponíveis no histórico Git.
