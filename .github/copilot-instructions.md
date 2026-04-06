# graphify-dotnet Repository Conventions

These instructions define the conventions for the graphify-dotnet project. Apply them when creating, modifying, or reviewing any project in this repo.

> **This is NOT a NuGet package.** graphify-dotnet is a .NET 10 console/library application. There is no NuGet publishing, no pack workflows, no multi-target frameworks.

---

## Repository Structure

```
graphify-dotnet/
├── README.md                    # Project overview, badges, quick start
├── LICENSE                      # MIT License
├── Directory.Build.props        # Shared MSBuild properties (all projects)
├── global.json                  # SDK version with rollForward
├── .gitignore                   # .NET + IDE + OS ignores
├── graphify-dotnet.slnx         # XML-based solution file
├── src/
│   ├── Graphify/                # Core library (graph pipeline, data structures, export)
│   ├── Graphify.Cli/            # CLI tool (System.CommandLine)
│   ├── Graphify.Sdk/            # GitHub Copilot SDK integration
│   ├── Graphify.Mcp/            # MCP stdio server
│   └── tests/
│       ├── Graphify.Tests/              # Unit tests (xUnit)
│       └── Graphify.Integration.Tests/  # Integration tests
└── .github/workflows/           # CI/CD workflows
```

- **Root files only:** README.md, LICENSE, Directory.Build.props, global.json, .gitignore, graphify-dotnet.slnx
- **All source code** (libraries, tests, CLI, servers) lives under `src/`
- No `images/`, `docs/`, or `samples/` directories initially

---

## ⚠️ Critical Rule: All Code Lives Under `src/`

**Every** project — libraries, tests, tools, servers — MUST be placed under the `src/` directory. Nothing gets created at the repo root except:
- README.md
- LICENSE
- Directory.Build.props
- global.json
- .gitignore
- .editorconfig
- graphify-dotnet.slnx

**Structure under `src/`:**
- `src/Graphify/` — Core library (graph pipeline, data structures, export)
- `src/Graphify.Cli/` — CLI tool (dotnet tool, System.CommandLine)
- `src/Graphify.Sdk/` — GitHub Copilot SDK integration
- `src/Graphify.Mcp/` — MCP stdio server (ModelContextProtocol)
- `src/tests/Graphify.Tests/` — Unit tests (xUnit)
- `src/tests/Graphify.Integration.Tests/` — Integration tests

**Golden rule: NEVER create project directories at the repo root. NEVER put tests or tools outside `src/`.**

---

## Solution Format

- Use `.slnx` (XML-based solution format), **not** `.sln`
- All projects (libraries, tests, CLI, servers) must be included in the solution file
- Use `<Folder>` elements for logical grouping: `/src/`, `/src/tests/`

---

## Project Conventions

### Library / Application Projects
- Target `net10.0` only (single target — this is a .NET 10 project, not multi-target)
- Project naming: `Graphify`, `Graphify.Cli`, `Graphify.Sdk`, `Graphify.Mcp`
- Enable deterministic CI builds: `<ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>`
- Include `<InternalsVisibleTo>` for the corresponding test project where needed
- NO NuGet packaging properties (`<IncludeSymbols>`, `<PackageIcon>`, `<PackAsTool>`, etc.)

### Test Projects
- Target `net10.0`
- Framework: xUnit with coverlet.collector
- Naming: `Graphify.Tests`, `Graphify.Integration.Tests`
- Location: `src/tests/{TestProject}/`
- Set `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`

---

## Directory.Build.props

Place at repo root. Shared by all projects. **No NuGet package properties** — this is not a published package:

```xml
<Project>
  <PropertyGroup>
    <!-- Language -->
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>

    <!-- Code analysis -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>

    <!-- Repository information -->
    <RepositoryUrl>https://github.com/elbruno/graphify-dotnet</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
</Project>
```

---

## global.json

Target .NET 10 with `"rollForward": "latestMajor"` for flexibility:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMajor"
  }
}
```

---

## .gitignore

Standard .NET patterns:

```gitignore
# Build outputs
[Bb]in/
[Oo]bj/
[Dd]ebug/
[Rr]elease/

# NuGet
*.nupkg
*.snupkg
.nuget/
packages/

# IDE
.vs/
*.user
*.suo
*.cache
*.DotSettings.user

# OS
Thumbs.db
.DS_Store
```

---

## GitHub Actions — CI Build (`build.yml`)

- **Triggers:** push to `main`, PR to `main`
- **Runner:** `ubuntu-latest`
- **SDK:** `dotnet-version: 10.0.x`
- **Commands use solution-level operations** (not per-project)
- **No publish workflow** — this is not a NuGet package

```yaml
name: CI Build

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Restore
        run: dotnet restore graphify-dotnet.slnx
      - name: Build
        run: dotnet build graphify-dotnet.slnx --no-restore
      - name: Test
        run: dotnet test graphify-dotnet.slnx --no-build
```

---

## README.md Structure

Follow this exact order:

1. **Title:** `# graphify-dotnet` (first line)
2. **Badges row** (immediately after title):
   - CI Build, License: MIT, GitHub stars
   - Format: `[![Name](badge-url)](link-url)`
   - **No NuGet badges** — this is not a published package
3. **Tagline** with emoji (one line)
4. **Description** (one paragraph)
5. **Features** section
6. **Getting Started / Installation** — clone + build instructions (NOT `dotnet add package`)
7. **Usage** — CLI commands and examples
8. **Architecture** section — pipeline overview (detect → extract → build → cluster → analyze → report → export)
9. **Building from Source** section
10. **License** section
11. **Author section** with Bruno's links:
    - Blog: https://elbruno.com
    - YouTube: https://youtube.com/elbruno
    - LinkedIn: https://linkedin.com/in/elbruno
    - Twitter: https://twitter.com/elbruno
    - Podcast: https://notienenombre.com
12. **Acknowledgments** section (credit to safishamsi/graphify)

### README Rules
- Installation is `git clone` + `dotnet build` — do NOT use `dotnet add package`
- Badge URLs use shields.io
- No NuGet version or download badges

---

## Testing

- Framework: **xUnit** with coverlet for coverage
- Test project naming: `Graphify.Tests`, `Graphify.Integration.Tests`
- Tests live in `src/tests/{TestProject}/`
- All tests must pass before merging (enforced in CI)
- Test projects target `net10.0`

---

## Key NuGet Dependencies

These are project dependencies (consumed, not published):

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.AI | 10.4.1 | AI abstraction layer |
| GitHub.Copilot.SDK | 0.2.0 | Copilot integration |
| TreeSitter.DotNet | 1.3.0 | Multi-language AST parsing |
| QuikGraph | latest | Graph data structures and algorithms |
| System.CommandLine | latest | CLI framework |
| ModelContextProtocol | latest | MCP server protocol |
| xUnit | latest | Testing framework |

---

## Architecture — Pipeline Pattern

The core pipeline mirrors the Python source (safishamsi/graphify) but expressed in C# idioms:

```
detect → extract → build_graph → cluster → analyze → report → export
```

- **Composition over inheritance** — use interfaces and DI, not deep class hierarchies
- **Pipeline as composed services** — each stage is an interface implementation
- **Graph structures** — QuikGraph for adjacency/edge models, not custom implementations
- **AST parsing** — TreeSitter.DotNet for multi-language support
- **Export** — JSON, GraphML, and vis.js HTML output

---

## Author Information

- **Author:** Bruno Capuano (ElBruno)
- **License:** MIT
- **Repository:** https://github.com/elbruno/graphify-dotnet
