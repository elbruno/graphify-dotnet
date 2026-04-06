# Decision: Louvain Algorithm for Community Detection

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

## Context

The graphify-dotnet pipeline requires community detection (clustering) to group related code entities. The Python source (safishamsi/graphify) uses Leiden algorithm via `graspologic.partition.leiden` with a fallback to Louvain via `networkx.community.louvain_communities`.

No mature, maintained .NET Leiden implementation exists as a NuGet package. Leiden is a refinement of Louvain that adds a second phase to prevent poorly-connected communities.

## Decision

Implemented **Louvain community detection** in `ClusterEngine.cs` rather than attempting to port Leiden or use an unmaintained third-party library.

## Rationale

1. **No mature .NET Leiden library**: Searched NuGet - no well-maintained Leiden packages exist for .NET.
2. **Python fallback precedent**: The Python source already treats Louvain as an acceptable fallback when graspologic is unavailable.
3. **Simpler algorithm**: Louvain has one optimization phase (local moves until convergence). Leiden adds a refinement phase that's harder to implement correctly.
4. **Good enough for knowledge graphs**: Both algorithms optimize modularity. Louvain produces high-quality communities for our use case (code structure graphs with 100s-1000s of nodes).
5. **Community splitting mitigates**: Our implementation includes oversized community splitting (>25% of nodes), which addresses Louvain's main weakness (occasionally creating "god communities").

## Implementation Details

### Algorithm Flow
1. Initialize: Each node is its own community
2. Phase 1 (Local moves): For each node, calculate modularity gain of moving to each neighbor's community. Move to best. Repeat until convergence or max iterations.
3. Split oversized communities: Run a second Louvain pass on subgraphs >25% of total nodes (min 10 nodes).
4. Re-index: Sort communities by size descending, assign community IDs 0, 1, 2, ...

### Key Methods
- `DetectCommunities()`: Main Louvain loop
- `CalculateModularityGain()`: ΔQ formula for node moves
- `SplitCommunity()`: Recursive splitting for large communities
- `CalculateModularity()`: Global modularity score (quality metric)
- `CalculateCohesion()`: Intra-community edge density

### Configuration (ClusterOptions)
- `Resolution`: 1.0 (standard modularity). Higher → more smaller communities.
- `MaxIterations`: 100 per phase (typical convergence: <10 iterations)
- `MaxCommunityFraction`: 0.25 (split if community > 25% of nodes)
- `MinSplitSize`: 10 nodes (only split if community has at least 10 nodes)

## Alternatives Considered

1. **Port Leiden from Python**: Too complex. Leiden's refinement phase requires careful handling of singleton communities and aggregate graphs. High risk of bugs.
2. **Use third-party library**: Checked NuGet - only found abandoned/experimental projects with no test coverage.
3. **Use QuikGraph algorithms**: QuikGraph has `StronglyConnectedComponentsAlgorithm` but not modularity-based community detection.
4. **Call Python interop**: Adds heavy dependency (Python runtime) for a single algorithm. Breaks portability.

## Trade-offs

**Pros:**
- Self-contained: No external dependencies beyond QuikGraph (already in use)
- Maintainable: ~500 LOC, clearly documented algorithm
- Deterministic: Reproducible community assignments across runs
- Configurable: Resolution and size limits exposed via ClusterOptions
- Fast: O(n*m) per iteration, converges quickly on typical graphs

**Cons:**
- Not Leiden: Louvain can occasionally produce less balanced communities than Leiden
- Mitigated by: Community splitting logic prevents worst-case "god communities"

## Future Work

If Leiden becomes a requirement:
1. **Leiden.NET** could be developed as a separate library (good OSS contribution opportunity)
2. **Python interop** via `Python.NET` if we're already using Python for other features (e.g., tree-sitter)
3. For now, **Louvain + splitting** meets the product requirements for graphify-dotnet

## References

- Python source: `safishamsi/graphify/graphify/cluster.py`
- Louvain paper: Blondel et al., "Fast unfolding of communities in large networks" (2008)
- Leiden paper: Traag et al., "From Louvain to Leiden: guaranteeing well-connected communities" (2019)
