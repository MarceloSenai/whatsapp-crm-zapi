# Guia de Integração — WhatsApp CRM Z-API

> **Público-alvo:** Desenvolvedor Pleno que precisa extrair módulos deste projeto e integrá-los a um CRM já existente com repositório GitHub próprio.
>
> **Nível de detalhe:** Este guia explica não apenas O QUE fazer, mas PORQUÊ cada decisão é tomada. Se você nunca trabalhou com EF Core, Blazor Server ou Minimal APIs, não se preocupe — cada conceito é explicado no momento em que aparece.
>
> **Tempo estimado de leitura:** ~30 minutos
> **Tempo estimado de integração:** 2-5 dias (dependendo do tamanho do CRM destino)

---

## Sumário

0. [Conceitos Fundamentais](#0-conceitos-fundamentais-leia-antes-de-tudo)
1. [Visão Geral da Arquitetura](#1-visão-geral-da-arquitetura)
2. [Mapa de Dependências entre Módulos](#2-mapa-de-dependências-entre-módulos)
3. [Pré-requisitos do Projeto Destino](#3-pré-requisitos-do-projeto-destino)
4. [Estratégia de Integração (Passo a Passo)](#4-estratégia-de-integração-passo-a-passo)
5. [Módulo 1 — Entidades e Camada de Dados](#5-módulo-1--entidades-e-camada-de-dados)
6. [Módulo 2 — API Endpoints (Minimal APIs)](#6-módulo-2--api-endpoints-minimal-apis)
7. [Módulo 3 — Serviços (Z-API, Campaign Runner)](#7-módulo-3--serviços-z-api-campaign-runner)
8. [Módulo 4 — UI Blazor (Componentes e Páginas)](#8-módulo-4--ui-blazor-componentes-e-páginas)
9. [Módulo 5 — Dashboard de Performance (Lead Tracking)](#9-módulo-5--dashboard-de-performance-lead-tracking)
10. [Migração de Banco de Dados](#10-migração-de-banco-de-dados)
11. [Adaptação de Naming Conventions](#11-adaptação-de-naming-conventions)
12. [Configuração e Variáveis de Ambiente](#12-configuração-e-variáveis-de-ambiente)
13. [Docker e Deploy](#13-docker-e-deploy)
14. [Checklist Final de Integração](#14-checklist-final-de-integração)
15. [Exemplo Prático Completo: Integrando só o Dashboard](#15-exemplo-prático-completo-integrando-só-o-dashboard)
16. [Troubleshooting](#16-troubleshooting)

---

## 0. Conceitos Fundamentais (leia antes de tudo)

Se algum desses termos é novo para você, leia esta seção. Se já domina, pule para a [Seção 1](#1-visão-geral-da-arquitetura).

### O que é Minimal API?
É o jeito moderno do .NET de criar endpoints HTTP **sem Controllers**. Em vez de criar uma classe `DashboardController` com decorators `[HttpGet]`, você escreve direto:

```csharp
// Minimal API (este projeto usa isso)
app.MapGet("/api/dashboard", async (AppDbContext db) => {
    var dados = await db.Contacts.CountAsync();
    return Results.Ok(new { total = dados });
});
```

**Por que isso importa?** Quando for copiar os arquivos da pasta `Api/`, você vai ver lambdas (funções anônimas) em vez de classes. Se seu CRM usa Controllers, a [Seção 6.2](#62-se-seu-crm-usa-controllers-mvc-pattern) mostra como converter.

### O que é Blazor Server?
É um framework da Microsoft onde a UI roda **no servidor** (não no navegador). O navegador recebe HTML renderizado e se comunica via **SignalR** (WebSocket). Isso significa:

- O código C# da página roda no servidor
- Quando o usuário clica um botão, o servidor processa e atualiza a UI
- **HttpClient** dentro do Blazor chama os endpoints locais (localhost)

**Por que isso importa?** Ao integrar no seu CRM, os componentes Blazor fazem `Http.GetFromJsonAsync("/api/dashboard")` — essa chamada é **interna** (servidor para servidor), não passa pela internet.

### O que é EF Core (Entity Framework Core)?
É o ORM (Object-Relational Mapper) do .NET. Traduz classes C# para tabelas do banco. Exemplo:

```csharp
// Classe C# (Entity)
public class Contact {
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Vira automaticamente uma tabela:
// CREATE TABLE "Contact" ("id" VARCHAR, "name" VARCHAR, "createdAt" TIMESTAMPTZ)
```

**DbContext** é a classe central que gerencia todas as entidades. Pense nele como o "hub" do banco de dados.

### O que é DI (Dependency Injection)?
É o padrão onde as dependências de uma classe são **injetadas automaticamente** pelo framework, em vez de serem criadas manualmente:

```csharp
// SEM DI (código acoplado):
var db = new AppDbContext(connectionString);  // Você cria manualmente

// COM DI (este projeto usa isso):
app.MapGet("/api/dashboard", async (AppDbContext db) => { ... });
// O ASP.NET Core cria e injeta o 'db' automaticamente
```

**Por que isso importa?** No `Program.cs`, você vai ver `builder.Services.AddDbContext<AppDbContext>(...)` — isso **registra** o DbContext no container de DI. Quando um endpoint precisa dele, o framework injeta automaticamente.

### O que é SignalR?
É a tecnologia de comunicação em tempo real do .NET (WebSockets). No Blazor Server, toda interação do usuário (clique, digitação, scroll) é enviada ao servidor via SignalR, processada, e o HTML atualizado é enviado de volta. Você **não precisa configurar nada** — já vem incluído no Blazor Server.

### O que significa "seed data"?
São dados de demonstração inseridos automaticamente quando o banco está vazio. O `DatabaseSeeder.cs` cria 50 contatos, 6 campanhas, 30 deals, etc., para que o sistema funcione imediatamente após o deploy sem precisar cadastrar nada manualmente.

---

## 1. Visão Geral da Arquitetura

```
┌─────────────────────────────────────────────────────────────────┐
│                        BLAZOR SERVER UI                         │
│  Pages: Dashboard, Inbox, Pipeline, Contacts, Campaigns, etc.  │
│  Shared: ChatPanel, KanbanBoard, ContactList, CampaignList     │
│  Layout: MainLayout + Sidebar + NavItem                        │
├─────────────────────────────────────────────────────────────────┤
│                      JS INTEROP (wwwroot/js)                    │
│  charts.js (Chart.js)  │  pipeline-interop.js (SortableJS)     │
│  app.js (scroll, focus, clipboard)                              │
├─────────────────────────────────────────────────────────────────┤
│                     MINIMAL API LAYER (11 endpoints)            │
│  /api/dashboard  │  /api/contacts  │  /api/pipeline  │  etc.   │
├─────────────────────────────────────────────────────────────────┤
│                      SERVICE LAYER                              │
│  ZApiService (WhatsApp gateway)                                 │
│  CampaignRunner (BackgroundService + Channel queue)             │
│  WhatsAppSimulator (modo demo)                                  │
├─────────────────────────────────────────────────────────────────┤
│                    DATA LAYER (EF Core 9 + Npgsql)              │
│  AppDbContext  │  DatabaseSeeder  │  12 Entities                │
├─────────────────────────────────────────────────────────────────┤
│                    PostgreSQL (Neon / Render)                    │
└─────────────────────────────────────────────────────────────────┘
```

**Stack completa:**
- .NET 9.0 (ASP.NET Core + Blazor Server)
- Entity Framework Core 9.0.4 (Npgsql provider)
- PostgreSQL (qualquer provedor: Neon, Render, Supabase, AWS RDS)
- Chart.js 4.x (via CDN + JS Interop)
- SortableJS 1.15.x (via CDN + JS Interop)
- Tailwind CSS 3.4 (via CDN)
- Docker (multi-stage build)

---

## 2. Mapa de Dependências entre Módulos

Antes de copiar qualquer arquivo, entenda o grafo de dependências:

```
Entities (zero dependência externa)
    │
    ├──► AppDbContext (depende de: Entities)
    │       │
    │       ├──► API Endpoints (depende de: AppDbContext, Entities)
    │       │       │
    │       │       └──► Services (depende de: AppDbContext, Entities, HttpClient)
    │       │
    │       └──► DatabaseSeeder (depende de: AppDbContext, Entities)
    │
    └──► Blazor Components (depende de: HttpClient → API Endpoints)
            │
            └──► JS Interop (depende de: wwwroot/js/*.js + CDN libs)
```

**Regra de ouro:** Sempre integre de baixo para cima (Entities → Data → API → Services → UI).

**Por quê?** Se você copiar a UI primeiro, ela vai tentar chamar `/api/dashboard` que não existe ainda. Se copiar a API primeiro, ela vai tentar acessar `db.Conversions` que não existe no DbContext. Seguindo de baixo para cima, cada camada já encontra suas dependências prontas.

> **Dica prática:** A cada módulo integrado, rode `dotnet build`. Se compilar sem erros, avance para o próximo. Se der erro, resolva antes de prosseguir — nunca acumule erros.

---

## 3. Pré-requisitos do Projeto Destino

### 3.1 Se o CRM destino é .NET (ASP.NET Core / Blazor)

**Ideal.** A integração é direta. Certifique-se de ter:

```xml
<!-- Pacotes NuGet necessários -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.2" />
```

Se seu projeto já usa EF Core com outro banco (SQL Server, MySQL), veja [Seção 11](#11-adaptação-de-naming-conventions) para adaptações.

### 3.2 Se o CRM destino NÃO é .NET

Você ainda pode aproveitar:
- **Entidades** → Traduzir para models/schemas do seu ORM (Prisma, TypeORM, Django, etc.)
- **API Endpoints** → Traduzir a lógica de negócio para seu framework (Express, FastAPI, Laravel, etc.)
- **SQL Queries** → As queries LINQ podem ser traduzidas para SQL raw
- **UI Components** → O HTML/Tailwind é reutilizável em React, Vue, Svelte
- **charts.js** → Funciona em qualquer frontend
- **DatabaseSeeder** → Converter para seeds do seu ORM

---

## 4. Estratégia de Integração (Passo a Passo)

### Abordagem recomendada: **Feature Branch + Módulos Incrementais**

```bash
# 1. Clone seu CRM existente
git clone https://github.com/SuaOrg/seu-crm.git
cd seu-crm

# 2. Crie uma branch de integração
git checkout -b feat/whatsapp-crm-integration

# 3. Clone este projeto como referência (NÃO dentro do seu repo)
cd ..
git clone https://github.com/MarceloSenai/whatsapp-crm-zapi.git whatsapp-crm-ref
cd seu-crm

# 4. Integre módulo por módulo (ordem importa!)
#    Módulo 1: Entidades + DbContext
#    Módulo 2: API Endpoints
#    Módulo 3: Services
#    Módulo 4: UI
#    Módulo 5: Dashboard

# 5. A cada módulo: compile, teste, commite
dotnet build
git add -A && git commit -m "feat: integrate module X from whatsapp-crm"

# 6. Quando tudo estiver ok, abra um PR
git push -u origin feat/whatsapp-crm-integration
gh pr create --title "feat: WhatsApp CRM integration"
```

---

## 5. Módulo 1 — Entidades e Camada de Dados

### 5.1 Arquivos a copiar

```
src/WhatsAppCrm.Web/Entities/
├── Contact.cs              ← Entidade principal (UTM, tags, opt-out)
├── Conversation.cs         ← Conversas WhatsApp
├── Message.cs              ← Mensagens (inbound/outbound)
├── Pipeline.cs             ← Pipeline de vendas
├── Stage.cs                ← Estágios do pipeline
├── Deal.cs                 ← Negócios/oportunidades
├── Campaign.cs             ← Campanhas (WhatsApp + paid ads)
├── CampaignMessage.cs      ← Mensagens de campanha
├── Template.cs             ← Templates WhatsApp
├── ContactFeedback.cs      ← Feedbacks/notas sobre contatos
├── CampaignSpendDaily.cs   ← Gasto diário por campanha
└── Conversion.cs           ← Conversões (lead → venda)
```

### 5.2 Como adaptar ao seu DbContext existente

> **Conceito importante:** O DbContext é o "mapa" entre suas classes C# e o banco de dados. Cada `DbSet<Entidade>` representa uma tabela. Quando você adiciona `public DbSet<Conversion> Conversions => Set<Conversion>();`, está dizendo ao EF Core: "existe uma tabela Conversion no banco, e ela mapeia para a classe Conversion.cs".

**Cenário A: Seu CRM já tem `Contact`, `Deal`, etc.**

Não crie entidades duplicadas. Em vez disso, **adicione os campos faltantes** às suas entidades existentes.

> **Por que não duplicar?** Se seu CRM já tem uma tabela `Contacts` com milhares de registros reais, criar outra entidade `Contact` do zero causaria conflitos de nome e você perderia os dados existentes. O correto é enriquecer a entidade que já existe.

```csharp
// ❌ ERRADO: copiar Contact.cs inteiro (vai conflitar)
// ✅ CERTO: adicionar campos ao seu Contact existente

// No seu Contact.cs existente, adicione:
public string? UtmSource { get; set; }
public string? UtmMedium { get; set; }
public string? UtmCampaign { get; set; }
public string? UtmContent { get; set; }
public string? UtmTerm { get; set; }
public string? Gclid { get; set; }
public string? Fbclid { get; set; }
public string? LeadCampaignId { get; set; }
public Campaign? LeadCampaign { get; set; }

// Collections
public ICollection<Conversion> Conversions { get; set; } = [];
public ICollection<ContactFeedback> Feedbacks { get; set; } = [];
```

**Cenário B: Seu CRM não tem essas entidades**

Copie os arquivos inteiros, mas ajuste:

```csharp
// 1. Namespace: troque de WhatsAppCrm.Web.Entities para o seu
namespace SeuCrm.Domain.Entities;  // ou o padrão do seu projeto

// 2. IDs: este projeto usa string (GUID truncado 25 chars)
//    Se seu CRM usa int/long/Guid, ajuste:
public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
//    Para:
public int Id { get; set; }        // auto-increment
// ou
public Guid Id { get; set; }       // Guid nativo
```

### 5.3 Registrar no DbContext

Adicione ao `OnModelCreating` do seu DbContext existente:

```csharp
// DbSets (adicionar ao seu DbContext)
public DbSet<Conversation> Conversations => Set<Conversation>();
public DbSet<Message> Messages => Set<Message>();
public DbSet<Campaign> Campaigns => Set<Campaign>();
public DbSet<CampaignMessage> CampaignMessages => Set<CampaignMessage>();
public DbSet<Template> Templates => Set<Template>();
public DbSet<ContactFeedback> ContactFeedbacks => Set<ContactFeedback>();
public DbSet<CampaignSpendDaily> CampaignSpendDailies => Set<CampaignSpendDaily>();
public DbSet<Conversion> Conversions => Set<Conversion>();

// Relacionamentos no OnModelCreating (adicionar):
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // ... seus mapeamentos existentes ...

    // Conversation → Contact
    modelBuilder.Entity<Conversation>()
        .HasOne<Contact>()
        .WithMany(c => c.Conversations)
        .HasForeignKey(c => c.ContactId)
        .OnDelete(DeleteBehavior.Cascade);

    // Message → Conversation
    modelBuilder.Entity<Message>()
        .HasOne<Conversation>()
        .WithMany(c => c.Messages)
        .HasForeignKey(m => m.ConversationId)
        .OnDelete(DeleteBehavior.Cascade);

    // Deal → Contact
    modelBuilder.Entity<Deal>()
        .HasOne<Contact>()
        .WithMany(c => c.Deals)
        .HasForeignKey(d => d.ContactId)
        .OnDelete(DeleteBehavior.Cascade);

    // Campaign → LeadsFromThis (Contact.LeadCampaignId)
    modelBuilder.Entity<Contact>()
        .HasOne(c => c.LeadCampaign)
        .WithMany(camp => camp.LeadsFromThis)
        .HasForeignKey(c => c.LeadCampaignId)
        .OnDelete(DeleteBehavior.SetNull);

    // CampaignSpendDaily: índice único Campaign + Date
    modelBuilder.Entity<CampaignSpendDaily>()
        .HasIndex(s => new { s.CampaignId, s.Date })
        .IsUnique();

    // Campaign → SpendDaily
    modelBuilder.Entity<CampaignSpendDaily>()
        .HasOne<Campaign>()
        .WithMany(c => c.SpendDaily)
        .HasForeignKey(s => s.CampaignId)
        .OnDelete(DeleteBehavior.Cascade);

    // Campaign → Conversions
    modelBuilder.Entity<Conversion>()
        .HasOne<Campaign>()
        .WithMany(c => c.Conversions)
        .HasForeignKey(cv => cv.CampaignId)
        .OnDelete(DeleteBehavior.SetNull);

    // Deal → Conversions
    modelBuilder.Entity<Conversion>()
        .HasOne<Deal>()
        .WithMany(d => d.Conversions)
        .HasForeignKey(cv => cv.DealId)
        .OnDelete(DeleteBehavior.Cascade);

    // Contact → Conversions
    modelBuilder.Entity<Conversion>()
        .HasOne<Contact>()
        .WithMany(c => c.Conversions)
        .HasForeignKey(cv => cv.ContactId)
        .OnDelete(DeleteBehavior.Cascade);

    // Contact → Feedbacks
    modelBuilder.Entity<ContactFeedback>()
        .HasOne<Contact>()
        .WithMany(c => c.Feedbacks)
        .HasForeignKey(f => f.ContactId)
        .OnDelete(DeleteBehavior.Cascade);

    // Contact.Phone único
    modelBuilder.Entity<Contact>()
        .HasIndex(c => c.Phone)
        .IsUnique();
}
```

### 5.4 Atenção: Convenção de Nomes de Colunas

> **O que é "naming convention" no banco?** Cada ORM/framework tem um padrão diferente para nomear colunas:
> - **camelCase:** `createdAt`, `utmSource` (JavaScript, Prisma, este projeto)
> - **PascalCase:** `CreatedAt`, `UtmSource` (padrão .NET/SQL Server)
> - **snake_case:** `created_at`, `utm_source` (PostgreSQL, Ruby, Python)
>
> **Se o padrão do seu banco não bater com o deste projeto, as queries vão dar erro** tipo `column "utmSource" does not exist`. A seção abaixo explica como resolver.

Este projeto usa **camelCase** nas colunas do PostgreSQL (para compatibilidade com Prisma):

```csharp
// No OnModelCreating deste projeto:
foreach (var entity in modelBuilder.Model.GetEntityTypes())
{
    foreach (var property in entity.GetProperties())
    {
        var columnName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
        property.SetColumnName(columnName);
    }
}
```

**Se seu CRM usa snake_case** (ex: `created_at`), **remova esse loop** e use a convenção do seu projeto. O EF Core vai usar os nomes das properties por padrão, ou configure:

```csharp
// Para snake_case:
property.SetColumnName(ToSnakeCase(property.Name));
// Resultado: CreatedAt → created_at
```

**Se seu CRM usa PascalCase** (padrão SQL Server), basta não adicionar nenhuma convenção customizada.

---

## 6. Módulo 2 — API Endpoints (Minimal APIs)

### 6.1 Arquivos a copiar

```
src/WhatsAppCrm.Web/Api/
├── DashboardApi.cs          ← KPIs, funil, gráficos, ranking
├── ContactsApi.cs           ← CRUD contatos + UTM
├── ContactFeedbacksApi.cs   ← Notas/feedbacks por contato
├── CampaignSpendApi.cs      ← Gasto diário por campanha
├── PipelineApi.cs           ← Pipeline Kanban + auto-conversão
├── ConversationsApi.cs      ← Listagem de conversas WhatsApp
├── MessagesApi.cs           ← Envio/recebimento de mensagens
├── CampaignsApi.cs          ← CRUD campanhas + start
├── TemplatesApi.cs          ← Templates WhatsApp
├── ResetApi.cs              ← Reset demo (remover em produção)
└── ZApiWebhookApi.cs        ← Webhooks do Z-API
```

### 6.2 Se seu CRM usa Controllers (MVC pattern)

Converta de Minimal API para Controller:

```csharp
// ── ESTE PROJETO (Minimal API) ──
app.MapGet("/api/dashboard", async (string? from, string? to, AppDbContext db) =>
{
    // ... lógica ...
    return Results.Ok(new { kpi, funnel, leadsOverTime });
});

// ── SEU PROJETO (Controller) ──
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? campaignId,
        [FromQuery] string? source)
    {
        // Copie a lógica INTEIRA do lambda
        // Troque Results.Ok() por Ok()
        var dateFrom = !string.IsNullOrEmpty(from)
            ? DateTime.SpecifyKind(DateTime.Parse(from), DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-30);
        // ... restante da lógica ...
        return Ok(new { kpi, funnel, leadsOverTime });
    }
}
```

### 6.3 Se seu CRM usa outro framework (Express, FastAPI, etc.)

Traduza a lógica de negócio. Exemplo para o **Dashboard API**:

```typescript
// Express.js equivalente
router.get('/api/dashboard', async (req, res) => {
  const { from, to, campaignId, source } = req.query;

  const dateFrom = from ? new Date(from) : subDays(new Date(), 30);
  const dateTo = to ? addDays(new Date(to), 1) : addDays(new Date(), 1);

  // KPIs
  const totalLeads = await prisma.contact.count({
    where: { createdAt: { gte: dateFrom, lt: dateTo } }
  });

  const totalSpend = await prisma.campaignSpendDaily.aggregate({
    _sum: { amount: true },
    where: { date: { gte: dateFrom, lte: dateTo } }
  });

  // ... mesma lógica, adaptada ao seu ORM ...

  res.json({ kpi: { totalSpend, totalLeads, cpl, conversions, revenue, roas, roi } });
});
```

### 6.4 Registrar endpoints no seu pipeline

```csharp
// No Program.cs do seu projeto, adicione:
app.MapDashboardApi();
app.MapContactFeedbacksApi();
app.MapCampaignSpendApi();
// ... etc.

// Ou se usar Controllers, registre via:
builder.Services.AddControllers();
// E os controllers são descobertos automaticamente
```

### 6.5 Lógica de negócio crítica para preservar

#### Auto-conversão (Pipeline → Fechado/Ganho)

> **Conceito de negócio:** Quando um vendedor arrasta um deal para o estágio "Fechado/Ganho" no Kanban, o sistema automaticamente cria um registro de `Conversion`. Isso é crucial porque os KPIs de ROAS e ROI dependem das conversões para calcular o retorno sobre investimento em marketing.

```csharp
// No PipelineApi.cs, quando deal move para estágio com Order == 5:
if (newStage.Order == 5) // "Fechado/Ganho"
{
    // 1. Busca o contato do deal para pegar a campanha de origem
    var contact = await db.Contacts.FindAsync(deal.ContactId);

    // 2. Cria o registro de conversão
    db.Conversions.Add(new Conversion
    {
        Id = Guid.NewGuid().ToString("N")[..25],
        ContactId = deal.ContactId,
        DealId = deal.Id,
        CampaignId = contact?.LeadCampaignId,  // Vincula à campanha de origem (se houver)
        Value = deal.Value,                      // Valor do negócio fechado
        ConvertedAt = DateTime.UtcNow
    });
}
// Sem isso, o Dashboard mostraria R$ 0 em receita/ROAS/ROI
```

#### Fórmulas dos KPIs (entenda cada uma)

```
CPL  = totalSpend / totalLeads
       → "Quanto gastei em marketing para conseguir cada lead?"
       → Exemplo: R$ 14.000 / 50 leads = R$ 280 por lead

ROAS = revenue / totalSpend
       → "Para cada R$ 1 investido, quantos R$ voltaram em vendas?"
       → Exemplo: R$ 530.000 / R$ 14.000 = 37.8x (cada R$ 1 gerou R$ 37,80)

ROI  = (revenue - totalSpend) / totalSpend × 100
       → "Qual o percentual de lucro sobre o investimento?"
       → Exemplo: (530.000 - 14.000) / 14.000 × 100 = 3685%
```

> **Importante:** Se `totalSpend` for zero (nenhum gasto registrado), todos os KPIs de retorno ficam zero para evitar divisão por zero.

---

## 7. Módulo 3 — Serviços (Z-API, Campaign Runner)

### 7.1 Arquivos a copiar

```
src/WhatsAppCrm.Web/Services/
├── ZApiService.cs           ← Gateway Z-API (envio WhatsApp)
├── CampaignRunner.cs        ← Background job (fila de campanhas)
└── WhatsAppSimulator.cs     ← Simulador para modo demo
```

### 7.2 ZApiService — Integração com Z-API

O serviço suporta **dois modos**:

| Modo | Quando | Comportamento |
|------|--------|---------------|
| **Real** | `ZAPI_INSTANCE_ID` + `ZAPI_TOKEN` configurados | Chama API real do Z-API |
| **Simulação** | Variáveis não configuradas | Loga no console, retorna IDs fake |

**Para integrar no seu CRM:**

```csharp
// 1. Registre no DI do seu projeto:
builder.Services.AddHttpClient<IZApiService, ZApiService>();

// 2. Injete onde precisar:
public class MeuWhatsAppController(IZApiService zapi)
{
    public async Task<IActionResult> EnviarMensagem(string phone, string msg)
    {
        var result = await zapi.SendTextAsync(phone, msg);
        return Ok(result);
    }
}
```

**Se seu CRM já tem integração WhatsApp** (Twilio, MessageBird, etc.):
- Mantenha seu provider existente
- Adapte a interface `IZApiService` para ser um wrapper do seu provider
- O `CampaignRunner` funciona com qualquer implementação de `IZApiService`

### 7.3 CampaignRunner — Background Service

> **O que é um BackgroundService?** É uma tarefa que roda "por trás" da aplicação, sem depender de uma requisição HTTP. Imagine um "trabalhador" que fica esperando tarefas numa fila. Quando alguém joga uma campanha na fila (via API), ele acorda e processa.
>
> **O que é Channel?** É uma fila thread-safe do .NET (como um `Queue<T>`, mas segura para uso concorrente). O endpoint API escreve na fila, o BackgroundService lê da fila — sem locks, sem deadlocks.

```csharp
// Fluxo simplificado:
// 1. Vendedor clica "Iniciar Campanha" na UI
// 2. UI chama POST /api/campaigns/{id}/start
// 3. API faz: campaignQueue.Enqueue(campaignId)  ← joga na fila
// 4. CampaignRunner (BackgroundService) pega da fila
// 5. Para cada contato da campanha: envia WhatsApp + atualiza status
// 6. Quando termina todos: marca campanha como "completed"

// Para integrar:
builder.Services.AddSingleton<ICampaignQueue, CampaignRunner>();
builder.Services.AddHostedService(sp => (CampaignRunner)sp.GetRequiredService<ICampaignQueue>());
```

**Se seu CRM já usa Hangfire, Quartz, ou similar:**
- Substitua o `CampaignRunner` pelo seu job scheduler
- Mantenha a lógica de processamento (iteração por mensagens, rate limit)
- A lógica core está no método `ExecuteAsync` — extraia para um service dedicado

---

## 8. Módulo 4 — UI Blazor (Componentes e Páginas)

### 8.1 Estrutura de componentes

```
Components/
├── Layout/
│   ├── MainLayout.razor      ← Layout master (sidebar + content)
│   ├── Sidebar.razor          ← Menu de navegação
│   └── NavItem.razor          ← Item de menu reutilizável
├── Pages/
│   ├── Home.razor             ← Redirect para /dashboard
│   ├── DashboardPage.razor    ← Performance analytics
│   ├── Inbox.razor            ← Lista de conversas
│   ├── Contacts.razor         ← Gestão de contatos
│   ├── PipelinePage.razor     ← Kanban de vendas
│   ├── Campaigns.razor        ← Lista de campanhas
│   ├── NewCampaign.razor      ← Form criação campanha
│   ├── ZApiConfig.razor       ← Configuração Z-API
│   ├── Integration.razor      ← Página de integração
│   └── About.razor            ← Sobre
├── Shared/
│   ├── ChatPanel.razor        ← Thread de mensagens
│   ├── ConversationList.razor ← Lista de conversas
│   ├── ContactList.razor      ← Tabela de contatos (+ Origem UTM)
│   ├── ContactInfo.razor      ← Detalhe do contato
│   ├── MessageInput.razor     ← Input de mensagem
│   ├── MessageBubble.razor    ← Balão de mensagem
│   ├── KanbanBoard.razor      ← Board pipeline
│   ├── KanbanColumn.razor     ← Coluna do Kanban
│   ├── DealCard.razor         ← Card do deal
│   ├── CampaignList.razor     ← Lista campanhas
│   ├── CampaignForm.razor     ← Form campanha
│   └── Icons.razor            ← Biblioteca de ícones SVG
└── App.razor                  ← Head (CDN links), Routes
```

### 8.2 Se seu CRM usa Blazor

Copie os componentes diretamente. Adapte:

1. **Namespaces** nos `@using` de cada arquivo
2. **HttpClient** base URL (se diferente de `/api/`)
3. **Rotas** (`@page "/dashboard"`) — ajuste ao seu routing
4. **Layout** — integre com seu MainLayout existente ou substitua

### 8.3 Se seu CRM usa React/Vue/Angular

O HTML + Tailwind CSS é 100% reutilizável. Extraia:

```html
<!-- Do DashboardPage.razor, extraia o HTML dos KPI cards: -->
<div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
    <div class="bg-white rounded-xl border p-4 shadow-sm">
        <div class="flex items-center gap-2 mb-2">
            <span class="text-red-500 text-xs font-bold">$</span>
            <span class="text-xs text-gray-500">Total Gasto</span>
        </div>
        <div class="text-lg font-bold text-gray-900">
            {formatCurrency(data.kpi.totalSpend)}
        </div>
    </div>
    <!-- ... mais cards ... -->
</div>
```

**charts.js funciona direto** — basta importar no seu frontend:
```html
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<script src="/js/charts.js"></script>
```

### 8.4 JS Interop — Arquivos a copiar

```
wwwroot/js/
├── charts.js              ← 3 funções: renderAreaChart, renderBarChart, renderPieChart
├── pipeline-interop.js    ← SortableJS para Kanban drag-drop
└── app.js                 ← Helpers (scroll, focus, clipboard, textarea resize)
```

**CDN dependencies** (no `App.razor` ou no `<head>` do seu HTML):
```html
<script src="https://cdn.tailwindcss.com/3.4.17"></script>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<script src="https://cdn.jsdelivr.net/npm/sortablejs@1.15.6/Sortable.min.js"></script>
```

---

## 9. Módulo 5 — Dashboard de Performance (Lead Tracking)

Este é o módulo mais valioso para integração. Funciona de forma **independente** — precisa apenas de:
- Tabela de contatos (com `UtmSource`, `CreatedAt`)
- Tabela de campanhas (com `Platform`)
- Tabela de gastos diários (`CampaignSpendDaily`)
- Tabela de conversões (`Conversion`)
- Tabela de deals/stages (para funil)

### 9.1 Arquivos necessários (mínimo)

```
Entities/
├── CampaignSpendDaily.cs    ← NOVO (criar no seu projeto)
├── Conversion.cs            ← NOVO (criar no seu projeto)
└── ContactFeedback.cs       ← NOVO (criar no seu projeto)

Api/
├── DashboardApi.cs          ← Endpoint principal
├── CampaignSpendApi.cs      ← CRUD gasto diário
└── ContactFeedbacksApi.cs   ← CRUD feedbacks

Components/Pages/
└── DashboardPage.razor      ← UI completa

wwwroot/js/
└── charts.js                ← Chart.js interop

Helpers/
└── Formatters.cs            ← FormatCurrency, FormatPercent, FormatCompact
```

### 9.2 O que o Dashboard retorna (`GET /api/dashboard`)

```json
{
  "kpi": {
    "totalSpend": 14075.76,
    "totalLeads": 50,
    "cpl": 281.52,
    "conversions": 10,
    "revenue": 530507.00,
    "roas": 37.7,
    "roi": 3668.9
  },
  "funnel": [
    { "stage": "Novo Lead", "count": 5, "value": 230018.00 },
    { "stage": "Demo Agendada", "count": 4, "value": 162404.00 }
  ],
  "leadsOverTime": [
    { "date": "2026-02-01", "count": 3 },
    { "date": "2026-02-02", "count": 5 }
  ],
  "spendVsRevenue": [
    { "date": "2026-02-01", "spend": 450.00, "revenue": 21000.00 }
  ],
  "sourceDistribution": [
    { "source": "google_ads", "count": 12 },
    { "source": "direto", "count": 20 }
  ],
  "campaignRanking": [
    {
      "name": "Google Ads - Obraxs",
      "platform": "google_ads",
      "spend": 4540.55,
      "leads": 5,
      "conversions": 3,
      "revenue": 203920.00,
      "roas": 44.9
    }
  ]
}
```

### 9.3 Adaptação do DashboardApi ao seu schema

O `DashboardApi.cs` faz queries diretamente no EF Core. Você precisa adaptar os nomes de entidades e campos:

```csharp
// ESTE PROJETO usa:
db.Contacts.Where(c => c.CreatedAt >= dateFrom)
db.CampaignSpendDailies.Where(s => s.Date >= ...)
db.Conversions.Where(c => c.ConvertedAt >= ...)

// SE SEU CRM usa nomes diferentes:
db.Leads.Where(l => l.RegisteredAt >= dateFrom)        // Contacts → Leads
db.AdSpend.Where(s => s.SpendDate >= ...)               // CampaignSpendDailies → AdSpend
db.Sales.Where(s => s.ClosedAt >= ...)                  // Conversions → Sales
```

**Dica:** Use `Search & Replace` no arquivo inteiro. As queries LINQ são autocontidas.

---

## 10. Migração de Banco de Dados

### 10.1 Este projeto usa `EnsureCreatedAsync()` (sem migrations)

```csharp
// Program.cs — cria tabelas se não existirem
await db.Database.EnsureCreatedAsync();
```

**Para seu CRM em produção, use EF Core Migrations:**

```bash
# 1. Adicione uma migration com as novas entidades
dotnet ef migrations add AddWhatsAppCrmEntities

# 2. Revise o arquivo gerado em Migrations/
#    Verifique se não conflita com tabelas existentes

# 3. Aplique ao banco
dotnet ef database update
```

### 10.2 Se seu CRM usa Prisma

Traduza as entidades para `schema.prisma`:

```prisma
model CampaignSpendDaily {
  id         String   @id @default(cuid())
  campaignId String
  date       DateTime @db.Date
  amount     Float
  campaign   Campaign @relation(fields: [campaignId], references: [id], onDelete: Cascade)

  @@unique([campaignId, date])
  @@map("CampaignSpendDaily")
}

model Conversion {
  id          String    @id @default(cuid())
  contactId   String
  campaignId  String?
  dealId      String
  value       Float
  convertedAt DateTime  @default(now())
  contact     Contact   @relation(fields: [contactId], references: [id], onDelete: Cascade)
  campaign    Campaign? @relation(fields: [campaignId], references: [id], onDelete: SetNull)
  deal        Deal      @relation(fields: [dealId], references: [id], onDelete: Cascade)

  @@map("Conversion")
}

model ContactFeedback {
  id        String   @id @default(cuid())
  contactId String
  author    String
  text      String
  type      String   @default("note")
  createdAt DateTime @default(now())
  contact   Contact  @relation(fields: [contactId], references: [id], onDelete: Cascade)

  @@map("ContactFeedback")
}

// Campos UTM no Contact existente:
model Contact {
  // ... seus campos existentes ...
  utmSource       String?
  utmMedium       String?
  utmCampaign     String?
  utmContent      String?
  utmTerm         String?
  gclid           String?
  fbclid          String?
  leadCampaignId  String?
  leadCampaign    Campaign?          @relation("LeadCampaign", fields: [leadCampaignId], references: [id], onDelete: SetNull)
  conversions     Conversion[]
  feedbacks       ContactFeedback[]
}
```

### 10.3 Se seu CRM usa SQL puro (Knex, raw queries)

```sql
-- Novas tabelas
CREATE TABLE "CampaignSpendDaily" (
    "id" VARCHAR(25) PRIMARY KEY,
    "campaignId" VARCHAR(25) NOT NULL REFERENCES "Campaign"("id") ON DELETE CASCADE,
    "date" DATE NOT NULL,
    "amount" DOUBLE PRECISION NOT NULL DEFAULT 0,
    UNIQUE("campaignId", "date")
);

CREATE TABLE "Conversion" (
    "id" VARCHAR(25) PRIMARY KEY,
    "contactId" VARCHAR(25) NOT NULL REFERENCES "Contact"("id") ON DELETE CASCADE,
    "campaignId" VARCHAR(25) REFERENCES "Campaign"("id") ON DELETE SET NULL,
    "dealId" VARCHAR(25) NOT NULL REFERENCES "Deal"("id") ON DELETE CASCADE,
    "value" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "convertedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE "ContactFeedback" (
    "id" VARCHAR(25) PRIMARY KEY,
    "contactId" VARCHAR(25) NOT NULL REFERENCES "Contact"("id") ON DELETE CASCADE,
    "author" VARCHAR(255) NOT NULL,
    "text" TEXT NOT NULL,
    "type" VARCHAR(50) NOT NULL DEFAULT 'note',
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Campos UTM no Contact existente
ALTER TABLE "Contact" ADD COLUMN "utmSource" VARCHAR(100);
ALTER TABLE "Contact" ADD COLUMN "utmMedium" VARCHAR(100);
ALTER TABLE "Contact" ADD COLUMN "utmCampaign" VARCHAR(255);
ALTER TABLE "Contact" ADD COLUMN "utmContent" VARCHAR(255);
ALTER TABLE "Contact" ADD COLUMN "utmTerm" VARCHAR(255);
ALTER TABLE "Contact" ADD COLUMN "gclid" VARCHAR(255);
ALTER TABLE "Contact" ADD COLUMN "fbclid" VARCHAR(255);
ALTER TABLE "Contact" ADD COLUMN "leadCampaignId" VARCHAR(25) REFERENCES "Campaign"("id") ON DELETE SET NULL;
```

---

## 11. Adaptação de Naming Conventions

### Mapeamento de convenções

| Este Projeto | SQL Server (padrão) | PostgreSQL (snake_case) | Prisma (default) |
|-------------|---------------------|------------------------|-------------------|
| `createdAt` | `CreatedAt` | `created_at` | `createdAt` |
| `contactId` | `ContactId` | `contact_id` | `contactId` |
| `UtmSource` (C#) → `utmSource` (DB) | `UtmSource` | `utm_source` | `utmSource` |

### Regra prática

1. **Seu CRM usa camelCase no DB?** → Não precisa mudar nada
2. **Seu CRM usa PascalCase?** → Remova o loop de conversão no `OnModelCreating`
3. **Seu CRM usa snake_case?** → Troque o loop por:

```csharp
// Instale: dotnet add package EFCore.NamingConventions
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());  // ← Isso resolve tudo
```

---

## 12. Configuração e Variáveis de Ambiente

### Variáveis necessárias

```env
# Obrigatória
DATABASE_URL=postgresql://user:pass@host:5432/dbname

# Opcionais (Z-API - sem elas, roda em modo simulação)
ZAPI_INSTANCE_ID=seu_instance_id
ZAPI_TOKEN=seu_token
ZAPI_CLIENT_TOKEN=seu_client_token

# .NET padrão
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
```

### Como o DATABASE_URL é parseado

```csharp
// Program.cs converte URI do Render/Neon para connection string Npgsql:
// postgresql://user:pass@host:5432/dbname
// →
// Host=host;Port=5432;Database=dbname;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
```

**Se seu CRM já tem connection string própria**, ignore esse parsing e use a sua diretamente.

---

## 13. Docker e Deploy

### Dockerfile (multi-stage, otimizado para 512MB RAM)

```dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/WhatsAppCrm.Web/WhatsAppCrm.Web.csproj .
RUN dotnet restore
COPY src/WhatsAppCrm.Web/ .
RUN dotnet publish -c Release -o /app /p:ReadyToRun=true

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_GCHeapHardLimit=0x10000000
EXPOSE 8080
ENTRYPOINT ["dotnet", "WhatsAppCrm.Web.dll"]
```

### Plataformas suportadas

| Plataforma | Como configurar |
|-----------|-----------------|
| **Render** | Docker build, `DATABASE_URL` como env var, health check `/healthz` |
| **Railway** | Mesma config do Render |
| **Fly.io** | `fly launch --dockerfile Dockerfile` |
| **Azure App Service** | Container ou publish direto |
| **AWS ECS/Fargate** | Push image para ECR |
| **Heroku** | Buildpack .NET ou Docker |

---

## 14. Checklist Final de Integração

### Antes de começar
- [ ] Leu este guia completo
- [ ] Identificou quais módulos precisa (nem tudo é obrigatório)
- [ ] Criou branch de integração no seu CRM

### Módulo 1 — Dados
- [ ] Adicionou campos UTM ao seu Contact/Lead existente
- [ ] Criou entidades novas (CampaignSpendDaily, Conversion, ContactFeedback)
- [ ] Registrou DbSets e relacionamentos no DbContext
- [ ] Gerou e aplicou migration no banco
- [ ] Testou: `dotnet build` compila sem erros

### Módulo 2 — APIs
- [ ] Copiou/adaptou DashboardApi ao seu padrão (Minimal API ou Controller)
- [ ] Copiou/adaptou ContactFeedbacksApi
- [ ] Copiou/adaptou CampaignSpendApi
- [ ] Adaptou PipelineApi (auto-conversão no estágio "ganho")
- [ ] Registrou endpoints no pipeline
- [ ] Testou: `curl localhost:PORT/api/dashboard` retorna JSON válido

### Módulo 3 — Serviços
- [ ] Integrou ZApiService (ou adaptou para seu provider WhatsApp)
- [ ] Integrou CampaignRunner (ou usou seu job scheduler)
- [ ] Registrou no DI container

### Módulo 4 — UI
- [ ] Copiou componentes Blazor (ou extraiu HTML para React/Vue)
- [ ] Adicionou CDN links (Chart.js, SortableJS, Tailwind)
- [ ] Copiou wwwroot/js/ (charts.js, pipeline-interop.js, app.js)
- [ ] Copiou Formatters.cs (ou traduziu para seu framework)
- [ ] Testou: Dashboard renderiza com dados

### Módulo 5 — Dashboard
- [ ] KPI cards mostram valores corretos
- [ ] Funil de vendas renderiza todos os estágios
- [ ] Gráfico "Leads ao Longo do Tempo" funciona
- [ ] Gráfico "Gasto vs Receita" funciona
- [ ] Gráfico "Distribuição por Fonte" funciona (pie chart)
- [ ] Tabela "Ranking de Campanhas" mostra ROAS
- [ ] Filtros (data, campanha, fonte) funcionam
- [ ] Coluna "Origem" na lista de contatos mostra badges UTM

### Deploy
- [ ] Build Docker passa
- [ ] Variáveis de ambiente configuradas
- [ ] Health check `/healthz` responde 200
- [ ] Seed data popula corretamente (ou removeu DatabaseSeeder)

---

## 15. Exemplo Prático Completo: Integrando só o Dashboard

Se você quer apenas o **Dashboard de Performance** (módulo mais comum de ser integrado), siga este passo a passo detalhado:

### Passo 1: Crie as 3 entidades novas no seu projeto

Crie estes 3 arquivos na pasta de entidades do seu CRM:

```csharp
// Entities/CampaignSpendDaily.cs
// Representa o gasto diário de uma campanha de marketing
namespace SeuCrm.Entities;

public class CampaignSpendDaily
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string CampaignId { get; set; } = "";   // FK para Campaign
    public DateOnly Date { get; set; }               // Dia do gasto
    public double Amount { get; set; }               // Valor em R$
}
```

```csharp
// Entities/Conversion.cs
// Registra quando um lead vira cliente (deal fechado)
namespace SeuCrm.Entities;

public class Conversion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string ContactId { get; set; } = "";      // Quem converteu
    public string? CampaignId { get; set; }           // De qual campanha veio (pode ser null)
    public string DealId { get; set; } = "";          // Qual deal foi fechado
    public double Value { get; set; }                  // Valor da venda
    public DateTime ConvertedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// Entities/ContactFeedback.cs
// Anotações da equipe sobre um contato
namespace SeuCrm.Entities;

public class ContactFeedback
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string ContactId { get; set; } = "";
    public string Author { get; set; } = "";          // Quem escreveu
    public string Text { get; set; } = "";             // O texto da nota
    public string Type { get; set; } = "note";         // note|call|meeting|task
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Passo 2: Adicione campos UTM ao seu Contact existente

```csharp
// No seu Contact.cs existente, adicione estes campos:
public string? UtmSource { get; set; }       // google_ads, meta_ads, organic, etc.
public string? UtmMedium { get; set; }       // cpc, email, social, etc.
public string? UtmCampaign { get; set; }     // nome da campanha de marketing
public string? LeadCampaignId { get; set; }  // FK para Campaign (de qual campanha veio)
```

### Passo 3: Registre no DbContext

```csharp
// No seu DbContext, adicione os DbSets:
public DbSet<CampaignSpendDaily> CampaignSpendDailies => Set<CampaignSpendDaily>();
public DbSet<Conversion> Conversions => Set<Conversion>();
public DbSet<ContactFeedback> ContactFeedbacks => Set<ContactFeedback>();
```

### Passo 4: Gere e aplique a migration

```bash
dotnet ef migrations add AddDashboardEntities
dotnet ef database update
```

### Passo 5: Copie o DashboardApi.cs

Copie `src/WhatsAppCrm.Web/Api/DashboardApi.cs` para seu projeto. Ajuste:
- O namespace
- Os nomes das entidades (se diferentes)
- O nome do DbContext

### Passo 6: Registre o endpoint

```csharp
// No Program.cs:
app.MapDashboardApi();
```

### Passo 7: Teste

```bash
dotnet run
# Em outro terminal:
curl http://localhost:5000/api/dashboard
# Deve retornar JSON com kpi, funnel, leadsOverTime, etc.
```

### Passo 8: Copie a UI

Copie `DashboardPage.razor` + `charts.js` + adicione o CDN do Chart.js no head.

**Pronto!** Você tem um dashboard de performance funcionando no seu CRM.

---

## 16. Troubleshooting

### Erro: `column c.utmSource does not exist`
**Causa:** Banco foi criado antes de adicionar os campos UTM.
**Fix:** Rode a migration ou recrie as tabelas:
```sql
ALTER TABLE "Contact" ADD COLUMN "utmSource" VARCHAR(100);
-- ... demais campos UTM
```

### Erro: `Cannot write DateTime with Kind=Unspecified`
**Causa:** Npgsql exige `DateTimeKind.Utc` para `timestamp with time zone`.
**Fix:** Sempre use `DateTime.SpecifyKind(valor, DateTimeKind.Utc)` antes de queries.

### Erro: `The LINQ expression could not be translated`
**Causa:** EF Core não consegue traduzir `.ToString()` ou `.Date` dentro de GroupBy.
**Fix:** Faça o GroupBy no banco e formate em memória:
```csharp
// ❌ Falha:
.Select(g => new { date = g.Key.ToString("yyyy-MM-dd") })

// ✅ Funciona:
var raw = await query.GroupBy(c => c.CreatedAt.Date)
    .Select(g => new { date = g.Key, count = g.Count() })
    .ToListAsync();
var result = raw.Select(x => new { date = x.date.ToString("yyyy-MM-dd"), x.count });
```

### Erro: `Circular reference detected` na serialização JSON
**Fix:** Adicione no `Program.cs`:
```csharp
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
```

### Dashboard vazio (sem dados)
**Causa:** Sem seed data ou filtro de datas não encontra registros.
**Fix:** Verifique se o `DatabaseSeeder` rodou. Teste com datas amplas:
```
GET /api/dashboard?from=2020-01-01&to=2030-01-01
```

### Charts não renderizam
**Causa:** Chart.js CDN não carregou ou `charts.js` não foi incluído.
**Fix:** Verifique no `App.razor` (ou `_Layout.cshtml`):
```html
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<script src="js/charts.js"></script>
```

### Kanban drag-drop não funciona
**Causa:** SortableJS não carregou ou `pipeline-interop.js` não foi incluído.
**Fix:** Mesmo padrão — verifique os scripts no head/body.

---

> **Nota:** Este guia cobre os cenários mais comuns. Para dúvidas específicas sobre a integração com seu CRM, abra uma issue no repositório ou consulte a documentação do .NET:
> - [EF Core Docs](https://learn.microsoft.com/ef/core/)
> - [Blazor Docs](https://learn.microsoft.com/aspnet/core/blazor/)
> - [Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
> - [Z-API Docs](https://developer.z-api.io/)
