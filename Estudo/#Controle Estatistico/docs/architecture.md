# Arquitetura F0

FastAPI atende páginas renderizadas por Jinja2. Rotas delegam autorização às dependências RBAC, persistência ao SQLAlchemy e eventos críticos ao serviço de auditoria. Modelos ficam em `app/models`; autenticação usa `AuthProvider`, com implementação local substituível por AD/Entra ID. Alembic controla o esquema.

SQLite local é o padrão; SQL Server usa `DATABASE_URL=mssql+pyodbc://...`. O domínio não depende de recursos exclusivos do SQLite. Para ativar HTMX, baixe a distribuição oficial para `app/static/vendor/htmx.min.js` (arquivo intencionalmente não incluído).

**Desvio arquitetural registrado:** a arquitetura-alvo no guia sugere .NET Web API + React. Por decisão do produto, esta F0 usa Python 3.12, FastAPI e HTML renderizado no servidor. A separação por camadas permite reavaliar a migração futuramente.
