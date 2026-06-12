# FunnyPot AutoResearch Program

Improve FunnyPot by running short, comparable experiments.

Allowed mutable files are configured in `config/app-config.yaml` under `auto-research.mutable-paths`. Do not edit files outside that list during an experiment.

Default objective: improve honeypot realism without weakening safety controls or breaking tests.

Experiment loop:

1. Propose one small code or data change.
2. Run the configured experiment command.
3. Emit `autoresearch_metric=<number>` if the experiment has a project-specific score. Higher is better by default.
4. Keep only changes that improve the best metric and pass the experiment command.

The configured runner passes this file to the agent over stdin and also runs the test suite after the agent exits. Prefer narrow, reviewable changes over broad rewrites.

Good candidate metrics for this project:

- Number of deterministic attacker commands handled without LLM calls.
- Test coverage for command emulation edge cases.
- Lower failure ratio in replayed honeypot sessions, encoded as a higher score.
