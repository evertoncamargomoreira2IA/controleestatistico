# QMS · CEP — workspace de continuação (C#)

Esta pasta é a **continuação** da versão C# (.NET 10 + Blazor Interactive Server).
O repositório original em `Desktop\Claude\#Controle Estatistico` permanece **intocado**.

Trabalho organizado em sprints:

1. Motor estatístico (`Qms.Statistics`) — constantes, X̄-R, X̄-S, I-MR, Cp/Cpk/Pp/Ppk, PPM
2. Tela **Cartas CEP** (F3) com SVG no tema Indústria 5.0
3. Regras de carta das **Definições** ligadas ao motor
4. **Painel** com KPIs reais, lista colorida de Cp/Cpk e alertas do turno
5. Testes alinhados a `docs/spc-formulas.md` e README desta solução

Fórmulas: `docs/spc-formulas.md`. Motor JS de referência: `reference/spc.js`.

Desenvolvimento: ver `README` histórico da solução abaixo (comandos `dotnet`).
