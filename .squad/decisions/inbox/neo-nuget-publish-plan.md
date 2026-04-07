# Decision: Comprehensive Plan for NuGet Publishing graphify-dotnet as a dotnet Global Tool

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-07  
**Status:** Plan (not yet implemented)  
**Scope:** NuGet.org publishing workflow, icon/metadata, OIDC trusted publishing, version management  
**Requested by:** Bruno Capuano

---

## Problem Statement

**Current state:**  
Graphify-dotnet targets `net10.0` and already has `Graphify.Cli.csproj` configured with:
- `PackAsTool=true`, `ToolCommandName=graphify`, `PackageId=graphify-dotnet`
- Version 0.1.0, MIT license, README.md included

**Gap:**  
The project is not distributable as a public NuGet package. Users cannot install via `dotnet tool install -g graphify-dotnet`.

**What's missing:**
1. Package icon (`images/nuget_logo.png`)
2. Symbol package configuration (`<IncludeSymbols>`, `<SymbolPackageFormat>`)
3. `<PackageIcon>` metadata pointing to the icon
4. GitHub Actions publish workflow (`.github/workflows/publish.yml`)
5. GitHub `release` environment with NUGET_USER secret
6. OIDC trusted publisher registration on NuGet.org
7. Version management strategy (Git tags, validation)
8. README badges linking to NuGet package page
9. Team documentation on release procedures

---

## Approach

**Core strategy:**  
Adapt the ElBruno.MarkItDotNet `publish.yml` pattern for our .NET 10 tooling context.

**Key differences from reference:**
- ElBruno targets `net8.0` (multi-target library); graphify-dotnet targets `net10.0` only (single-target tool)
- Use OIDC trusted publishing (no long-lived API keys in repo)
- Trigger on GitHub `release` (published) event + manual `workflow_dispatch` with optional version override

**Publish workflow:**
1. Create GitHub release with tag `v0.1.0` (version extracted, `v` prefix stripped)
2. Workflow triggers: restore → build (Release) → test → pack (generates .nupkg + .snupkg)
3. OIDC login to NuGet.org via `NuGet/login@v1`
4. Push packages with `--skip-duplicate` (safe for re-runs)
5. Add NuGet badges to README

**Security:**
- No API keys in repo; OIDC handles auth
- GitHub's `id-token: write` permission enables short-lived JWT exchange
- One-time NuGet.org trust configuration (secure, maintainable)

---

## Implementation Plan

### Phase 1: Project Metadata & Assets

**1.1 Create NuGet package icon** (Bruno — manual)
- Generate 128×128 px PNG (`images/nuget_logo.png`)
- Represent graphify-dotnet concept (graph nodes, network, AI)
- Recommended: DALL-E prompt like "minimalist graph network nodes connected by lines, modern tech aesthetic, white background"

**1.2 Add PackageIcon metadata to Directory.Build.props** (Team)
- Add `<PackageIcon>nuget_logo.png</PackageIcon>` to root PropertyGroup
- Add ItemGroup: `<None Include="images/nuget_logo.png" Pack="true" PackagePath="\" />`
- Ensures icon included in .nupkg

**1.3 Enable symbol packages in Graphify.Cli.csproj** (Team)
- Add to PropertyGroup:
  ```xml
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  ```
- Allows debuggers to resolve source + line numbers for installed tools

### Phase 2: Publish Workflow

**2.1 Create .github/workflows/publish.yml** (Team)
- Triggers: `release` event (type: published), `workflow_dispatch` with optional version input
- Environment: `release` (secrets: `NUGET_USER`)
- Permissions: `id-token: write`, `contents: read`
- Steps:
  1. Checkout code
  2. Setup .NET 10 (dotnet-version: 10.0.x)
  3. Restore dependencies
  4. Build (Release config)
  5. Test (5-min timeout)
  6. Pack (Release, generates .nupkg + .snupkg)
  7. OIDC Login (`NuGet/login@v1`)
  8. Push to NuGet with `--skip-duplicate`
- Version extraction: strip `v` prefix from tag (e.g., `v0.1.0` → `0.1.0`), validate regex `^[0-9]+\.[0-9]+\.[0-9]+$`, fallback to csproj Version

**2.2 Update build.yml (optional)** (Team)
- Decision: DEFER pack validation to publish workflow
- Keep build.yml focused on test only (no CI bloat)

### Phase 3: GitHub Environment & Secrets (Bruno — Manual, One-Time)

**3.1 Create `release` environment in GitHub**
- Settings → Environments → New Environment → name: `release`
- No protection rules needed initially

**3.2 Add `NUGET_USER` secret to `release` environment**
- Value = Bruno's NuGet.org username (e.g., `elbruno`)
- Used for logging/display only; not a credential (OIDC handles auth)

**3.3 Register OIDC trusted publisher on NuGet.org**
- Log in to NuGet.org → Manage API Keys → Configure OIDC Trust
- Fill in:
  - Repository URL: https://github.com/elbruno/graphify-dotnet
  - Workflow filename: publish.yml
  - Environment: `release`
  - Package: Leave blank (or specify graphify-dotnet if supported)
- NuGet.org generates trust relationship for this workflow
- Reference: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#publish-during-continuous-integration-using-oidc

### Phase 4: Documentation & Validation

**4.1 Add NuGet badges to README.md** (Team)
- Add to README top section:
  ```markdown
  [![NuGet](https://img.shields.io/nuget/v/graphify-dotnet.svg)](https://www.nuget.org/packages/graphify-dotnet)
  [![NuGet Downloads](https://img.shields.io/nuget/dt/graphify-dotnet.svg)](https://www.nuget.org/packages/graphify-dotnet)
  ```

**4.2 Create .github/PUBLISHING.md** (Team)
- Document release procedure for team:
  1. Update version in `Graphify.Cli.csproj`
  2. Create Git tag matching version (e.g., `git tag v0.1.0`)
  3. Push tag to trigger workflow: `git push origin v0.1.0`
  4. Monitor GitHub Actions
  5. Verify package on NuGet.org
  6. Test install: `dotnet tool install -g graphify-dotnet`
- Link to OIDC docs and NuGet.org package page

**4.3 Update .github/copilot-instructions.md** (Team)
- Add NuGet publishing section:
  - Publish trigger: GitHub release tag
  - Workflow: `.github/workflows/publish.yml`
  - OIDC trusted publishing (no API keys)
  - Package: graphify-dotnet on NuGet.org
  - Instructions: See `.github/PUBLISHING.md`

**4.4 Dry-run publish workflow (Team)** 
- Before Bruno does production release:
  1. Create test tag (e.g., `v0.1.0-test`) locally — do NOT push
  2. Trigger `publish.yml` via `workflow_dispatch` with version `0.1.0-test`
  3. Verify all steps succeed (pack, login, push)
  4. Check NuGet.org for test package or error logs
  5. Delete test tag if successful

---

## Implementation Sequence

```
Phase 1: 1.1 (Bruno) → 1.2 (Team) → 1.3 (Team)
  ↓
Phase 2: 2.1 (Team) → [optional 2.2]
  ↓
Phase 3: 3.1, 3.2, 3.3 (Bruno — manual, one-time)
  ↓
Phase 4: 4.1, 4.2, 4.3 (Team) → 4.4 (Team dry-run) → Production Release
```

---

## Manual Steps for Bruno (One-Time Setup)

1. **Generate NuGet icon**
   - Create `images/nuget_logo.png` (128×128 px, PNG)
   - Minimalist graph/network visual

2. **Create GitHub `release` environment**
   - Settings → Environments → New → name: `release`

3. **Add `NUGET_USER` secret to `release` environment**
   - Value = NuGet.org username

4. **Register OIDC trusted publisher on NuGet.org**
   - Log in → Manage API Keys → Configure OIDC Trust
   - Repo URL, workflow file, environment

5. **Test the workflow** (optional but recommended)
   - Trigger `workflow_dispatch` with test version
   - Verify pack and push succeed
   - Check NuGet.org for package

---

## Notes & Considerations

### .NET 10 SDK Availability

**Concern:** GitHub runners may not have .NET 10 SDK pre-installed.

**Mitigation:** Use `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x` to fetch latest 10.0 SDK if needed.

**Risk:** If 10.0.x unavailable, workflow fails. Monitor on first run. Fallback: use ubuntu-24.04 or custom container.

### Single-Target Design

**Decision:** graphify-dotnet targets `net10.0` only.

**Why:** Tool (not library), deployment simplicity, single target matches use case.

**Implication:** No multi-target framework matrix needed in pack step.

### Symbol Packages

**Decision:** Enable `.snupkg` via `<IncludeSymbols>true</IncludeSymbols>` + `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`.

**Why:** Source + line numbers in debuggers improves support experience.

**Validation:** Confirm `NuGet/login@v1` + push step handles both .nupkg and .snupkg.

### Version Management Strategy

**Approach:** Version in code, tag in Git.

1. Update `<Version>X.Y.Z</Version>` in `Graphify.Cli.csproj`
2. Create Git tag: `git tag vX.Y.Z && git push origin vX.Y.Z`
3. Create GitHub release from tag (or workflow infers)

**Fallback:** If tag version mismatches csproj, publish.yml extracts, validates, and fails safely.

**Future:** Consider GitVersion or Nerdbank.GitVersioning if versioning becomes complex.

### OIDC vs. API Keys

**Why OIDC?**
- No long-lived credentials in GitHub secrets
- No key rotation needed
- Audit trail clearer
- Industry best practice (GitHub, PyPI, Rust crates recommend)

**Implementation:** NuGet.org supports OIDC via `NuGet/login@v1` + configured trust. One-time setup, fully automated after.

### Security Posture

**Permissions:**
```yaml
permissions:
  id-token: write    # OIDC JWT exchange
  contents: read     # Checkout
```

Minimal, environment-specific secrets. NuGet.org trust configuration prevents unauthorized publishing.

### Post-Publish Validation

**After first publish:**
1. Test local install: `dotnet tool uninstall -g graphify-dotnet && dotnet tool install -g graphify-dotnet`
2. Verify command available: `graphify --help`
3. Monitor NuGet.org metrics (downloads, package details)

**Future:** Add post-push validation step in workflow (install test, help command check).

---

## Success Criteria

✅ `graphify-dotnet` published to NuGet.org  
✅ Installation works: `dotnet tool install -g graphify-dotnet`  
✅ Command available in PATH: `graphify --help`  
✅ Package page shows icon, metadata, download counts  
✅ README badges link to package page  
✅ Publish workflow runs automatically on GitHub release  
✅ No long-lived API keys in repo  
✅ Team knows how to create releases and monitor status  

---

## Open Questions

1. **Auto-publish on main branch merges, or keep manual?**
   - Plan: Manual (release event) for stability. Auto-publish can be added later.

2. **Pre-release tag strategy (e.g., v0.1.0-beta)?**
   - Plan: Single-target releases only. Pre-releases can be tagged when needed.

3. **Post-publish validation in workflow?**
   - Plan: Document in Phase 4.4 dry-run; add automated test in future iteration.

---

## References

- **ElBruno.MarkItDotNet:** Reference publish.yml pattern (adapted for net10.0 single target)
- **NuGet.org OIDC docs:** https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#publish-during-continuous-integration-using-oidc
- **GitHub Trusted Publishing:** https://docs.github.com/en/actions/deployment/security-hardening-your-deployments

---

## Team Ownership

| Phase | Owner | Tasks |
|-------|-------|-------|
| 1 | Bruno + Team | Icon generation, metadata, symbol config |
| 2 | Team | Publish workflow creation |
| 3 | Bruno | GitHub environment setup, NuGet.org OIDC trust |
| 4 | Team | Documentation, badges, dry-run, validation |

---

## Dependencies

- GitHub repository access (Settings → Environments)
- NuGet.org account (Bruno's username)
- .NET 10 SDK availability on GitHub runners
- No external dependencies added (existing packages sufficient)

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| .NET 10 SDK unavailable on runners | Use setup-dotnet@v4 to fetch; fallback to ubuntu-24.04 |
| OIDC trust misconfiguration | Document NuGet.org setup clearly; test dry-run before production |
| Version mismatch (tag vs. csproj) | Publish.yml validates and fails safely |
| Icon missing or invalid | Phase 1.1 completion gate; validate before merge |
| First publish fails | Dry-run (Phase 4.4) catches issues before production |

