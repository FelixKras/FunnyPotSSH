# Graph Report - FunnyPot  (2026-05-11)

## Corpus Check
- 7 files · ~3,188 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 56 nodes · 82 edges · 11 communities (7 shown, 4 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8b63ef0a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]

## God Nodes (most connected - your core abstractions)
1. `Program` - 15 edges
2. `InputValidatorTests` - 12 edges
3. `Logger` - 9 edges
4. `InputValidator` - 5 edges
5. `SCPDetectorTests` - 2 edges
6. `int` - 2 edges
7. `SCPDetector` - 2 edges
8. `HttpClient` - 1 edges
9. `string` - 1 edges
10. `ConcurrentDictionary` - 1 edges

## Surprising Connections (you probably didn't know these)
- `Program` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 1 → community 4_

## Communities (11 total, 4 thin omitted)

### Community 1 - "Community 1"
Cohesion: 0.26
Nodes (6): bool, ConcurrentDictionary, Program, HttpClient, SemaphoreSlim, string

### Community 2 - "Community 2"
Cohesion: 0.33
Nodes (3): Logger, Lazy, object

### Community 3 - "Community 3"
Cohesion: 0.29
Nodes (5): ChatMessage, ChatRequestData, GlobalStats, HarvestedCredential, SCPDetector

## Knowledge Gaps
- **12 isolated node(s):** `HttpClient`, `string`, `ConcurrentDictionary`, `SemaphoreSlim`, `HarvestedCredential` (+7 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Program` connect `Community 1` to `Community 2`, `Community 3`, `Community 4`?**
  _High betweenness centrality (0.161) - this node is a cross-community bridge._
- **Why does `Logger` connect `Community 2` to `Community 3`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **Why does `InputValidatorTests` connect `Community 0` to `Community 5`?**
  _High betweenness centrality (0.059) - this node is a cross-community bridge._
- **What connects `HttpClient`, `string`, `ConcurrentDictionary` to the rest of the system?**
  _12 weakly-connected nodes found - possible documentation gaps or missing edges._