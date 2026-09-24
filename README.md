# SupportAgent.NET

[![GitHub](https://img.shields.io/badge/GitHub-rahmatafridi%2FSupportAgent.NET-181717?logo=github)](https://github.com/rahmatafridi/SupportAgent.NET)

Open-source AI Customer Support Agent Starter Kit built with **ASP.NET Core, React, SQL Server, native LLM tool calling, RAG, embeddings, and conversation state**.

Repository: **https://github.com/rahmatafridi/SupportAgent.NET**

SupportAgent.NET demonstrates how to build a real AI-enabled customer support application where the LLM works as an intelligence layer over normal .NET services instead of acting as a standalone chatbot.

The project is designed for developers who want to learn from it, fork it, connect their own database and company knowledge, or extend it into a production SaaS application.

---

## Why SupportAgent.NET?

Most AI demos stop at:

```text
User → Prompt → LLM → Text
```

SupportAgent.NET demonstrates a more realistic application architecture:

```text
User
  ↓
Support Interface
  ↓
ASP.NET Core API
  ↓
LLM
  ↓
Native Tool Calling
  ↓
.NET Business Services
  ↓
SQL Server / Company Knowledge
  ↓
LLM
  ↓
Grounded Response
```

The LLM decides when it needs a tool.

There is **no keyword-based routing** such as:

```csharp
if (prompt.Contains("customer"))
```

Instead, supported LLMs use native function/tool calling.

---

## Current Features

| Area | What you get |
|---|---|
| **AI Gateway** | Provider-independent abstraction for Ollama and OpenAI |
| **Native tool calling** | `GetCustomer`, `GetOrderStatus`, `SearchKnowledgeBase` |
| **RAG** | Company documents chunked and stored in SQL Server |
| **Semantic search** | Embeddings via `nomic-embed-text` + hybrid lexical ranking |
| **Grounding** | Business facts from tools/DB; policies from knowledge base |
| **Support workspace** | React UI: tickets, conversation, AI assistant panel |
| **Conversation state** | AI conversations persisted in SQL Server |
| **Suggested replies** | Structured draft replies for human review (not auto-sent) |

---

## Technology Stack

- **Backend:** .NET 10, ASP.NET Core Web API
- **Frontend:** React, TypeScript, Vite
- **Database:** SQL Server
- **AI:** Ollama or OpenAI via `IAIGateway`
- **Embeddings:** Ollama `nomic-embed-text` (local) or configured provider
- **Tests:** xUnit

---

## Prerequisites

Before running the project, install:

1. [.NET 10 SDK](https://dotnet.microsoft.com/download)
2. [Node.js 20+](https://nodejs.org/) and npm
3. [SQL Server](https://www.microsoft.com/sql-server) (LocalDB, Express, or full instance)
4. One AI provider:
   - **[Ollama](https://ollama.com/)** for local LLM + embeddings, or
   - **OpenAI API key** for cloud LLM

Optional:

- [Visual Studio 2022](https://visualstudio.microsoft.com/) for F5 debugging
- [EF Core CLI](https://learn.microsoft.com/ef/core/cli/dotnet) for migrations:

```bash
dotnet tool install --global dotnet-ef
```

---

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/rahmatafridi/SupportAgent.NET.git
cd SupportAgent.NET
```

### 2. Configure SQL Server

Update the connection string in `src/SupportAgent.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SupportAgentDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Adjust `Server=` for your SQL Server instance if needed.

### 3. Create the database schema

From the repository root:

```bash
dotnet restore
dotnet ef database update --project src/SupportAgent.Infrastructure --startup-project src/SupportAgent.Api
```

This applies all EF Core migrations, including:

- customers, orders, tickets, ticket messages
- knowledge documents and chunks
- embeddings (`EmbeddingJson`)
- AI conversation tables

### 4. Configure AI

Edit `src/SupportAgent.Api/appsettings.Development.json`.

**Option A — Ollama (local, default in `appsettings.json`)**

```json
{
  "AI": {
    "Ollama": {
      "Use": true,
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.1"
    },
    "Embeddings": {
      "Provider": "Ollama",
      "Model": "nomic-embed-text"
    },
    "OpenAI": {
      "Use": false,
      "BaseUrl": "https://api.openai.com",
      "Model": "gpt-4o-mini",
      "ApiKey": ""
    }
  }
}
```

Pull the models:

```bash
ollama pull llama3.1
ollama pull nomic-embed-text
```

Use a model with **native tool calling** (for example `llama3.1`). Some coding models return tool calls as plain text instead of native function calls.

**Option B — OpenAI**

```json
{
  "AI": {
    "Ollama": { "Use": false },
    "OpenAI": {
      "Use": true,
      "BaseUrl": "https://api.openai.com",
      "Model": "gpt-4o-mini",
      "ApiKey": ""
    }
  }
}
```

Set exactly one provider to `"Use": true`.

Do **not** commit API keys. Prefer environment variables:

```bash
set OPENAI_API_KEY=your-key-here
```

Or .NET user secrets:

```bash
dotnet user-secrets set "AI:OpenAI:ApiKey" "your-key-here" --project src/SupportAgent.Api
```

### 5. Start the backend

```bash
dotnet run --project src/SupportAgent.Api
```

The API runs at **http://localhost:5191**.

On first startup in Development, the app seeds sample data when the database is empty:

- Customer **101** — John Smith
- Orders **1** and **2**
- Ticket **1001** — "Where is my order?"
- Sample company knowledge (refund, shipping, etc.)

Verify the API:

```bash
curl http://localhost:5191/api/health
```

Expected:

```json
{
  "status": "ok",
  "application": "SupportAgent.NET"
}
```

### 6. Start the frontend

In a second terminal:

```bash
cd src/SupportAgent.Web
npm install
npm run dev
```

The frontend runs at **http://localhost:5173** and proxies `/api` to the backend.

### 7. Try the application

| What | URL | Purpose |
|---|---|---|
| Homepage + chat test | http://localhost:5173/ | Quick AI chat smoke test |
| Support workspace | http://localhost:5173/tickets | Ticket list + AI copilot |
| Ticket detail | http://localhost:5173/tickets/1001 | Conversation + AI panel |
| Swagger UI | http://localhost:5191/swagger | Explore all API endpoints |

**Suggested first checks:**

1. Open `/tickets/1001`
2. In the AI panel, ask: `What is happening with this customer?`
3. Click **Generate Reply** to get a suggested draft (not sent automatically)
4. On the homepage, try: `Who is customer 101?`

### 8. Run tests

```bash
dotnet test tests/SupportAgent.Tests
```

---

## Run from Visual Studio

1. Run `npm install` once in `src/SupportAgent.Web`
2. Apply migrations (step 3 above) before first run
3. Right-click the **solution** → **Properties** → **Startup Project**
4. Select **Multiple startup projects**
5. Set **SupportAgent.Api** and **SupportAgent.Web** both to **Start**
6. Press **F5**

---

## Project Structure

```text
SupportAgent.NET/
├── src/
│   ├── SupportAgent.Api/            # ASP.NET Core Web API
│   ├── SupportAgent.Core/           # Domain models, interfaces, DTOs
│   ├── SupportAgent.Infrastructure/ # EF Core, services, AI providers, RAG
│   └── SupportAgent.Web/            # React + TypeScript frontend
├── tests/
│   └── SupportAgent.Tests/          # Backend tests
├── README.md
└── SupportAgent.sln
```

---

## AI Gateway

Provider-independent AI abstraction:

```text
IAIGateway
  ↓
AIGatewayService
  ↓
IAIProvider
  ↓
Ollama / OpenAI
```

When tools are enabled, the gateway runs a tool loop:

```text
User message
  ↓
LLM decides tool(s)
  ↓
AIToolExecutor
  ↓
.NET business service
  ↓
SQL Server / knowledge retrieval
  ↓
Tool result back to LLM
  ↓
Final grounded answer
```

Configuration uses `AI:Ollama:Use` and `AI:OpenAI:Use` flags. Exactly one must be `true`.

Environment variable overrides:

| Variable | Purpose |
|---|---|
| `AI__Ollama__Use` | Enable Ollama |
| `AI__OpenAI__Use` | Enable OpenAI |
| `AI__Ollama__Model` | Ollama chat model |
| `AI__OpenAI__Model` | OpenAI chat model |
| `OPENAI_API_KEY` | OpenAI API key |

---

## Native LLM Tool Calling

The LLM can currently call:

### GetCustomer

Gets customer information from the application database.

```text
User: "Who is customer 101?"
  ↓
LLM → GetCustomer(101)
  ↓
CustomerService → SQL Server
  ↓
LLM → final answer
```

### GetOrderStatus

Gets current order information and status.

```text
User: "What is the status of order 1?"
  ↓
LLM → GetOrderStatus(1)
  ↓
OrderService → SQL Server
  ↓
LLM → final answer
```

### SearchKnowledgeBase

Searches internal company knowledge such as refund policies, shipping policies, troubleshooting docs, and support procedures.

```text
User: "What is our refund policy?"
  ↓
LLM → SearchKnowledgeBase("refund policy")
  ↓
Hybrid knowledge retrieval
  ↓
LLM → grounded answer with sources
```

Example chat request:

```bash
curl -X POST http://localhost:5191/api/ai/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"Who is customer 101?"}'
```

Example response:

```json
{
  "text": "Customer 101 is John Smith with email john@example.com.",
  "provider": "Ollama",
  "model": "llama3.1",
  "totalTokens": 120,
  "durationMs": 4500,
  "toolsUsed": [
    {
      "name": "GetCustomer",
      "arguments": { "customerId": 101 }
    }
  ],
  "sources": []
}
```

---

## RAG / Company Knowledge

Company documents flow through a retrieval pipeline:

```text
Document
  ↓
Normalized
  ↓
Split into chunks
  ↓
Stored in SQL Server
  ↓
Embedded
  ↓
Retrieved when needed
```

The application does **not** send the complete knowledge base to the LLM. Only relevant chunks are retrieved.

### Chunking

- approximately **700 characters** per chunk
- sentence-aware splitting where practical
- overlapping context between chunks
- chunks stored independently for retrieval

### Semantic search

Embeddings use **`nomic-embed-text`** via Ollama by default.

Example:

- Stored knowledge: *"Standard orders usually ship within 1–2 business days."*
- User asks: *"How long before my package leaves the warehouse?"*
- Semantic search can still match the Shipping Policy

### Hybrid search

Knowledge retrieval combines semantic similarity and lexical matching.

Default weighting:

- Semantic: **75%**
- Lexical: **25%**

Configured under `KnowledgeSearch` in appsettings.

Embeddings are stored on each chunk as JSON (`EmbeddingJson`). This keeps the project portable without requiring a native SQL Server vector type yet.

### Backfill embeddings

If chunks exist without embeddings:

```bash
curl -X POST http://localhost:5191/api/knowledge/embeddings/rebuild
```

### Add and search knowledge directly

```bash
curl -X POST http://localhost:5191/api/knowledge/documents \
  -H "Content-Type: application/json" \
  -d '{"title":"Return Policy","content":"Customers may request a return within 30 days.","source":"internal-policy"}'
```

```bash
curl "http://localhost:5191/api/knowledge/search?query=refund"
```

---

## Grounding Rules

SupportAgent.NET is designed to prevent the model from inventing business information.

- Customer information must come from tools/database
- Order information must come from tools/database
- Company policies must come from the knowledge base
- If relevant knowledge is unavailable, the assistant should say so
- Draft replies must not claim an action occurred unless tool data proves it

Knowledge answers can return their sources to the frontend.

---

## Customer Support Workflow

The application includes a ticket-oriented support workspace.

```text
Tickets | Ticket Conversation | AI Assistant
```

Support agents can:

- browse tickets
- view ticket conversations
- ask the AI questions about a ticket
- inspect tools used by the LLM
- view knowledge sources
- generate suggested customer replies
- copy the suggested reply

AI messages remain **separate** from the actual customer conversation.

SupportAgent.NET assists the human agent. It does **not** automatically send customer replies.

```text
Ticket
  ↓
Support Agent
  ↓
AI Conversation
  ↓
LLM
  ↓
Tools + Knowledge
  ↓
Grounded answer
  ↓
Suggested reply
  ↓
Human review
```

Open the workspace at **http://localhost:5173/tickets**.

---

## Conversation State

AI conversations are stored in SQL Server:

- `AIConversations`
- `AIConversationMessages`
- `AIConversationToolAudits`

The assistant can maintain context across follow-up questions.

Example:

1. Agent: *"Who is customer 101?"*
2. Then: *"What should I tell them about their order?"*

The conversation state is preserved via `conversationId`.

Dynamic information is still retrieved through tools rather than trusted from stale chat history.

Conversation history sent to the model is limited to prevent unlimited token usage.

Default:

```json
{
  "Copilot": {
    "MaxConversationMessages": 12,
    "MaxTicketMessagesInContext": 10
  }
}
```

Ticket context sent to the model is intentionally compact:

- ticket ID, subject, status, priority, customer ID
- recent ticket messages only

---

## Suggested Replies

The support assistant can generate structured draft replies for human review.

### POST /api/copilot/ask

Ask a ticket-aware question. Creates or reuses an AI conversation.

```bash
curl -X POST http://localhost:5191/api/copilot/ask \
  -H "Content-Type: application/json" \
  -d '{
    "ticketId": 1001,
    "conversationId": null,
    "message": "What is happening with this customer?"
  }'
```

Example response:

```json
{
  "conversationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "answer": "Customer 101 opened ticket 1001 about order status.",
  "toolsUsed": [
    {
      "name": "GetCustomer",
      "arguments": { "customerId": 101 }
    }
  ],
  "sources": []
}
```

Send the returned `conversationId` on follow-up questions in the same ticket context.

### POST /api/copilot/draft-reply

Generate a suggested customer reply. Does **not** send email or add ticket messages.

```bash
curl -X POST http://localhost:5191/api/copilot/draft-reply \
  -H "Content-Type: application/json" \
  -d '{"ticketId": 1001}'
```

Example response:

```json
{
  "draft": {
    "subject": "Update on your order",
    "body": "Hi John, thanks for contacting us. I checked your order and it is currently processing.",
    "tone": "professional",
    "confidence": 0.92
  },
  "toolsUsed": [],
  "sources": []
}
```

---

## API Endpoints

| Endpoint | Description |
|---|---|
| `GET /api/health` | Health check |
| `GET /api/customers/{id}` | Get customer |
| `GET /api/orders/{id}` | Get order |
| `GET /api/customers/{customerId}/orders` | List customer orders |
| `GET /api/tickets` | List tickets |
| `GET /api/tickets/{id}` | Get ticket |
| `GET /api/tickets/{id}/messages` | Get ticket messages |
| `POST /api/ai/chat` | General AI chat with tool calling |
| `POST /api/copilot/ask` | Ticket-aware AI copilot |
| `POST /api/copilot/draft-reply` | Suggested reply draft |
| `GET /api/knowledge/search?query=` | Direct knowledge search |
| `POST /api/knowledge/documents` | Add knowledge document |
| `POST /api/knowledge/embeddings/rebuild` | Backfill chunk embeddings |

Swagger UI (Development): **http://localhost:5191/swagger**

---

## Current Development Status

**Implemented (Phases 1–7)**

- ASP.NET Core Web API + React frontend
- SQL Server + EF Core with sample seed data
- Customer, order, ticket, and ticket message services
- Provider-independent AI gateway (Ollama + OpenAI)
- Native LLM tool calling (`GetCustomer`, `GetOrderStatus`, `SearchKnowledgeBase`)
- RAG pipeline with chunking and hybrid semantic search
- Embeddings stored in SQL Server
- Ticket support workspace with AI assistant panel
- Persisted AI conversation state
- Structured suggested replies for human review
- Tool execution audit records

**Not implemented yet**

- Automatic email sending
- Native vector database (Pinecone, pgvector, Azure AI Search)
- Document upload UI
- PDF extraction
- Authentication / multi-tenancy
- Billing, refunds, autonomous agents
- Additional tools such as `SendEmail` or `CreateInvoice`

---

## License

Open source. License to be added.
# Authentication and Multi-Tenancy

SupportAgent.NET uses ASP.NET Core Identity with GUID user and role keys. Every user belongs to one organization, and tenant-owned records carry an `OrganizationId` that is enforced by EF Core global query filters. The organization is read from the authenticated server-side claims context; it is never accepted from browser input or an LLM tool call.

```text
User
  -> HTTP-only Identity cookie
  -> Organization claim
  -> tenant-scoped business services
  -> constrained AI tools
  -> tenant-scoped SQL data
```

Roles are enforced by backend authorization policies:

- `Admin`: all tenant support data, AI, knowledge management, embedding rebuild, and AI usage reporting.
- `SupportAgent`: support data, AI/copilot, suggested reply drafts, and knowledge search.
- `Viewer`: read-only customer, order, ticket, and ticket-message access. No AI, knowledge, or administration access.

Self-registration is controlled by `Authentication:AllowSelfRegistration`. It defaults to `false` and is enabled in Development. Registering creates a new organization and its first Admin. Organization slugs are normalized and protected by a unique database index.

Authentication uses an HTTP-only cookie; the React application stores no token in local storage. Before a state-changing request, React calls `GET /api/auth/csrf`, receives an antiforgery request token, and sends it in the `X-CSRF-TOKEN` header. ASP.NET Core validates that token against its antiforgery cookie. Login, registration, logout, AI requests, and knowledge writes are protected by this mechanism.

Development-only users are seeded under **SupportAgent Demo**:

| Role | Email | Development password |
| --- | --- | --- |
| Admin | `admin@supportagent.local` | `SupportAgent123!` |
| SupportAgent | `agent@supportagent.local` | `SupportAgent123!` |
| Viewer | `viewer@supportagent.local` | `SupportAgent123!` |

These accounts are created only when the API runs in Development. Apply migrations before starting the updated application:

```powershell
dotnet ef database update --project src/SupportAgent.Infrastructure --startup-project src/SupportAgent.Api
```

## AI Usage Tracking

Successful Chat, CopilotAsk, and DraftReply operations record organization, user, provider, model, input/output/total tokens, duration, request type, and timestamp. Prompts and responses are not copied into usage records. Admins can view their current organization’s monthly totals at `GET /api/admin/ai-usage` or `/admin/usage`.

AI-heavy endpoints use ASP.NET Core partitioned rate limiting, keyed by authenticated user ID. Configure the per-minute limit with `RateLimiting:AIRequestsPerMinute`.

AI provider keys remain backend-only. AI tools keep their original schemas and cannot choose an organization. Suggested replies remain drafts for human review and are never sent automatically.

> SupportAgent.NET is a starter/reference project. Perform a full security review, configure production HTTPS/cookie policy, secrets, monitoring, and operational controls before deploying it to production.

## Ticket Management

Admin and SupportAgent users can create tenant-scoped tickets for existing customers, update status and priority, and add human agent replies. A new ticket creates its initial message as a customer message.

```text
Create Ticket → Customer → Initial Message → Support Agent
              → AI Assistance → Human-approved Reply → Close Ticket
```

AI-generated suggested replies remain drafts. They are never stored or sent automatically: an agent must choose **Use Draft** and then explicitly click **Send Reply**.

## Document Knowledge Ingestion

Admins can upload PDF, DOCX, and UTF-8 TXT documents from `/knowledge` or `POST /api/knowledge/upload`:

```text
Admin upload
  -> format and size validation
  -> in-memory text extraction
  -> existing text chunker
  -> configured embedding provider
  -> tenant-scoped SQL knowledge store
  -> SearchKnowledgeBase
  -> grounded AI response
```

The default maximum upload size is 10 MB and can be changed with `KnowledgeUpload:MaxFileSizeMb`. PDF extraction reads embedded text only; scanned/image-only PDFs are rejected because OCR is not included. Raw files are processed in memory and are not retained on disk or written to `wwwroot`. Only extracted chunks and safe document metadata are persisted.

Knowledge remains tenant scoped. Admins can upload and delete; Admin and SupportAgent users can list document metadata; Viewer users cannot access knowledge APIs or the `/knowledge` page.

Example request (obtain an antiforgery token and authenticated cookie first):

```bash
curl -b cookies.txt -H "X-CSRF-TOKEN: $CSRF_TOKEN" \
  -F "file=@returns-and-refunds.pdf;type=application/pdf" \
  -F "title=Returns and Refunds" \
  http://localhost:5191/api/knowledge/upload
```

List and delete documents:

```text
GET    /api/knowledge/documents
GET    /api/knowledge/documents/{id}
DELETE /api/knowledge/documents/{id}
```

## Support Workspace

The tenant-scoped support workspace provides server-side ticket search by ticket ID, subject, customer name, or customer email. Status and priority filters, an assigned-to-me filter, and newest, oldest, priority, or recently-updated sorting can be combined in one request.

Admin and SupportAgent users can assign tickets to an active Admin or SupportAgent in the same organization, assign a ticket to themselves, update ticket state, reply to customers, and maintain a separate internal-notes timeline. Internal notes are never mixed into the customer conversation and are unavailable to Viewer users.

Each ticket shows compact customer context, the three most recent orders, the three most recent related tickets, created and updated timestamps, and tenant-scoped summary counts for open, in-progress, high-priority, unassigned, and closed-today tickets.

The AI panel can return an answer, confidence score, sources, and optional suggested actions. Suggested actions are advisory only. **Use Draft** copies an AI-generated draft into the normal agent reply editor for review and editing; it does not send the message. AI never sends replies, changes ticket status or priority, or assigns tickets automatically.
