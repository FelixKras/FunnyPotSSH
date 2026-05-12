# Graph Report - FunnyPot  (2026-05-12)

## Corpus Check
- 10 files · ~6,069 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 104 nodes · 145 edges · 15 communities (10 shown, 5 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `4aafe331`
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
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]

## God Nodes (most connected - your core abstractions)
1. `Program` - 18 edges
2. `DataHarvester` - 14 edges
3. `InputValidatorTests` - 12 edges
4. `Logger` - 11 edges
5. `InputValidator` - 5 edges
6. `Data Harvesting Specification: LLM-Driven SSH Analytics` - 5 edges
7. `Data Collection Modules` - 5 edges
8. `Storage and Publication` - 5 edges
9. `DataHarvesterTests` - 4 edges
10. `Processing and Correlation` - 3 edges

## Surprising Connections (you probably didn't know these)
- `Program` --references--> `int`  [EXTRACTED]
  FunnyPot/Program.cs → FunnyPot/Program.cs  _Bridges community 0 → community 7_

## Communities (15 total, 5 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.18
Nodes (7): bool, ConcurrentDictionary, FieldInfo, Program, HttpClient, SemaphoreSlim, string

### Community 1 - "Community 1"
Cohesion: 0.12
Nodes (15): Asset Value Perception, Data Acquisition Architecture, Data Collection Modules, Data Harvesting Specification: LLM-Driven SSH Analytics, GitHub Pages Publication, Log Format, M-1: SSH Protocol and Authentication Metadata, M-2: Behavioral and Tactical Metrics (+7 more)

### Community 2 - "Community 2"
Cohesion: 0.14
Nodes (12): AuthAttemptLogEntry, ChatMessage, ChatRequestData, CommandLogEntry, CommandResultLogEntry, DhsCommandAnalysis, GlobalStats, HarvestedCredential (+4 more)

### Community 3 - "Community 3"
Cohesion: 0.29
Nodes (3): Logger, Lazy, object

## Knowledge Gaps
- **32 isolated node(s):** `HttpClient`, `string`, `ConcurrentDictionary`, `SemaphoreSlim`, `FieldInfo` (+27 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Program` connect `Community 0` to `Community 2`, `Community 3`, `Community 4`, `Community 7`?**
  _High betweenness centrality (0.111) - this node is a cross-community bridge._
- **Why does `DataHarvester` connect `Community 4` to `Community 0`, `Community 2`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Why does `Logger` connect `Community 3` to `Community 2`?**
  _High betweenness centrality (0.055) - this node is a cross-community bridge._
- **What connects `HttpClient`, `string`, `ConcurrentDictionary` to the rest of the system?**
  _32 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.14 - nodes in this community are weakly interconnected._