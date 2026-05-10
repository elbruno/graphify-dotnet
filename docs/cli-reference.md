# CLI Reference

graphify-dotnet provides four commands: `run`, `watch`, `benchmark`, and `config`.

## Install

```bash
dotnet tool install -g graphify-dotnet
```

Or build from source — see [Building from Source](#building-from-source) below.

## `graphify run`

Run the full extraction and graph-building pipeline.

```bash
graphify run [path] [options]
```

| Argument/Option | Default | Description |
|-----------------|---------|-------------|
| `path` | `.` | Path to the project to analyze |
| `--output`, `-o` | `graphify-out` | Output directory |
| `--format`, `-f` | `json,html,report` | Export formats (comma-separated): `json`, `html`, `svg`, `neo4j`, `ladybug`, `obsidian`, `wiki`, `report` |
| `--verbose`, `-v` | `false` | Enable detailed progress output |
| `--provider`, `-p` | *(from config)* | AI provider: `azureopenai`, `ollama`, `copilotsdk` |
| `--endpoint` | *(from config)* | AI service endpoint URL |
| `--api-key` | *(from config)* | API key for the AI provider |
| `--model` | *(from config)* | Model ID (e.g., `gpt-4o`, `llama3.2`) |
| `--deployment` | *(from config)* | Azure OpenAI deployment name |
| `--config`, `-c` | `false` | Launch interactive config wizard before running |

### Examples

```bash
# Analyze current directory with defaults
graphify run

# Analyze a specific project
graphify run ./your-project

# All export formats with verbose output
graphify run . --format json,html,svg,neo4j,ladybug,obsidian,wiki,report -v

# Use Ollama locally
graphify run . --provider ollama --model codellama

# Custom output directory
graphify run . --output my-output-dir

# Configure and run in one step
graphify run --config
```

## `graphify watch`

Watch for file changes and incrementally update the graph. Runs an initial full pipeline, then monitors for changes using SHA256 content hashing.

```bash
graphify watch [path] [options]
```

Accepts the same options as `run` (except `--config`).

### Examples

```bash
# Watch current directory
graphify watch

# Watch a specific project with verbose output
graphify watch ./your-project -v

# Watch with specific export formats
graphify watch . --format json,html
```

See [Watch Mode](watch-mode.md) for details on incremental processing.

## `graphify benchmark`

Measure token reduction achieved by the knowledge graph compared to raw source.

```bash
graphify benchmark [graph-path]
```

| Argument | Default | Description |
|----------|---------|-------------|
| `graph-path` | `graphify-out/graph.json` | Path to the graph JSON file |

### Example

```bash
graphify benchmark graphify-out/graph.json
```

## `graphify config`

Interactive configuration management. Running `graphify config` with no subcommand presents a menu:

```
? What would you like to do?
  📋 View current configuration
  🔧 Set up AI provider
  📂 Set folder to analyze
```

### Subcommands

| Subcommand | Description |
|------------|-------------|
| `config show` | Display all resolved provider settings |
| `config set` | Launch AI provider wizard |
| `config folder` | Set the default project folder to analyze |

### Examples

```bash
# Interactive menu
graphify config

# View resolved config
graphify config show

# Set up AI provider
graphify config set

# Set default folder
graphify config folder
```

See [Configuration](configuration.md) for details on the layered config system.

## Building from Source

```bash
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet
dotnet build graphify-dotnet.slnx
dotnet run --project src/Graphify.Cli -- run .
```

When building from source, prefix commands with `dotnet run --project src/Graphify.Cli --`:

```bash
dotnet run --project src/Graphify.Cli -- run ./your-project --format json,html -v
dotnet run --project src/Graphify.Cli -- watch .
dotnet run --project src/Graphify.Cli -- config show
```
