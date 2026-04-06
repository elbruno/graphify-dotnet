# I Built a .NET 10 Knowledge Graph Builder (Inspired by Karpathy)

![Code transforming into an interactive knowledge graph with glowing nodes and edges](images/hero-image.png)

> 🎨 **Image prompt (16:9 — Hero):** A futuristic visualization showing a C# code snippet on the left side transforming into a glowing knowledge graph on the right. The code appears in a modern terminal window with syntax highlighting (blue, orange, green). Curved arrows and light particles flow from the code toward a 3D network graph made of interconnected glowing nodes and edges in deep purple, cyan, and gold. The nodes represent code elements (functions, classes, imports). The background is a dark gradient (navy to dark purple) with subtle geometric patterns. A .NET logo and "Graphify" text float in the composition. High-quality, cinematic lighting, 4K detail. Style: modern tech visualization, clean, professional.

## The Tweet That Started It All

Earlier this year, [Andrej Karpathy tweeted about using LLMs as "knowledge compilers"](https://x.com/karpathy/status/2039805659525644595) — a mind-bending idea: instead of asking an LLM questions, feed it raw data (papers, code, images) and let it automatically build a structured, navigable knowledge base. No RAG. No vector databases. Just pure understanding compiled into a graph.

Then I saw [@socialwithaayan showcase graphify](https://x.com/socialwithaayan/status/2041192946369007924) — a Python tool that does exactly this for codebases. It reads your source code, extracts relationships, builds a knowledge graph, and exports it as an interactive visualization, Obsidian vault, Neo4j script, or JSON.

I thought: *"This needs to exist in .NET."*

So I built [**graphify-dotnet**](https://github.com/elbruno/graphify-dotnet).

## What is graphify-dotnet?

graphify-dotnet is a .NET 10 tool that reads your codebase — all those files scattered across folders, all that implicit structure hidden in class hierarchies and imports — and transforms it into a **visual knowledge graph**. No more navigating by keyword search. Instead, you see the actual structure: which classes talk to each other, which modules form natural clusters, which functions are the "god nodes" that everything depends on.

It's a multi-stage pipeline:

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Detect  │ -> │ Extract  │ -> │  Build   │ -> │ Cluster  │
│  Files   │    │ Features │    │  Graph   │    │ (Louvain)│
└──────────┘    └──────────┘    └──────────┘    └──────────┘
                                                     │
                                                     v
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Export  │ <- │  Report  │ <- │ Analyze  │ <- │ Clustered│
│ Formats  │    │Generator │    │  Graph   │    │  Graph   │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
```

**Scan → Extract relationships → Build the graph → Find communities → Analyze structure → Export to multiple formats.**

## Let's See It in Action

Here are some real commands you can run right now:

**Build a knowledge graph from your project:**

```bash
dotnet run --project src/Graphify.Cli -- run .
```

**Query the graph for connections:**

```bash
dotnet run --project src/Graphify.Cli -- query "what connects AuthService to Database?"
```

**Explain a node:**

```bash
dotnet run --project src/Graphify.Cli -- explain "UserController"
```

**Export to multiple formats:**

```bash
dotnet run --project src/Graphify.Cli -- export --format html
dotnet run --project src/Graphify.Cli -- export --format neo4j
```

The HTML export gives you an interactive vis.js graph. Click nodes, search by concept, filter by community. The Neo4j export lets you load the entire graph into a real graph database.

## Key Features

- **Multi-language parsing**: Python, TypeScript, JavaScript, Go, Rust, Java, C#, C++, and more via tree-sitter AST extraction
- **Hybrid extraction**: Deterministic AST parsing + AI semantic analysis for docs and images
- **Graph clustering**: Louvain community detection so you can see natural groupings in your architecture
- **Confidence tracking**: Every relationship tagged as EXTRACTED, INFERRED, or AMBIGUOUS
- **Multiple export formats**: JSON, HTML, SVG, GraphML, Wiki, Obsidian vault, Neo4j Cypher
- **SHA256 caching**: Skip unchanged files — incremental updates instead of full rebuilds
- **MCP server**: Integrate with Claude, Copilot, and other AI assistants
- **Multimodal**: Handles code, Markdown, PDFs, and images (diagrams, screenshots, whiteboards)

## Why This Matters

Most developers navigate codebases by searching: "Find all usages of X." "Show me the inheritance tree." We're stuck with keyword queries because we don't have a semantic map.

A knowledge graph changes that. Suddenly you see the *shape* of your system. You spot the core dependencies, the bottlenecks, the modules that should talk but don't. You understand the architecture at a glance.

And because it's built from LLM-powered extraction, the graph understands *meaning*, not just syntax. It sees that your `Repository` class is about data access. It connects concepts across files. It finds hidden relationships.

![Interactive knowledge graph in a vis.js interface showing code structure with multiple export format options](images/viz-image.png)

> 🎨 **Image prompt (1:1 — Social):** A close-up 3D render of an intricate knowledge graph network. Nodes in the center are large and detailed (various colors: cyan, magenta, yellow, orange). Edges are glowing threads connecting them. Dozens of smaller nodes radiate outward, creating depth. The graph rotates slightly, suggesting interactivity. A subtle "Graphify" watermark or logo in one corner. Background: dark gradient with occasional light flares and bokeh. The overall effect is sophisticated, scientific, beautiful. Style: 3D render, cinematic, high detail, node-link graph visualization.

## Getting Started

**Requirements**: .NET 10 SDK

```bash
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet

dotnet build graphify-dotnet.slnx

dotnet run --project src/Graphify.Cli -- run .
```

That's it. Run it on your own codebase and watch as your architecture unfolds.

## What's Coming Next?

The roadmap is ambitious. Based on Morpheus's research, here's what's in the pipeline:

**Soon:**
- **Global dotnet tool**: Install once with `dotnet tool install -g graphify`, then just run `graphify run .` anywhere
- **Azure OpenAI support**: Enterprise teams with private Azure deployments can now use graphify securely
- **Ollama / local models**: Run entirely offline for privacy-sensitive code (healthcare, finance, defense)
- **Watch mode**: File watcher that increments the graph as you code, no full rebuilds needed
- **Roslyn-powered C# extraction**: Leverage .NET's full compilation model for type-safe AST analysis (something the Python version can't do)

We're also exploring VS Code integration, Obsidian bidirectional sync, and cross-repository knowledge graphs.

## Star the Repo, Try It Out

This is open source, built for the .NET community. [Head to GitHub and check it out.](https://github.com/elbruno/graphify-dotnet)

Clone it. Run it on your own projects. Build something with it. The pipeline is extensible — add custom extractors, export formats, or clustering algorithms.

And if you find it useful, star the repo. Help us grow the graphify ecosystem in .NET.

---

## About Me

Hey, I'm [Bruno Capuano](https://github.com/elbruno). I build tools and write about AI, .NET, and open source.

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)

Thanks for reading. Now go build something.
