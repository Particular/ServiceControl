# Guidelines

A guideline states rules that a change in this repository must follow, such as conventions for UI components or public APIs. A `docs/` page describes how the current design works, and an ADR under `docs/decisions/` records why a decision was made; a guideline says what a change must do.

Write a guideline only for a rule a reviewer would enforce. When a tool can check the rule, prefer an analyzer or a lint rule, and keep the guideline for rules that need judgment.

## Structure

Name each file `short-title.md`. Each guideline contains:

- Applies to: every change under paths, given as glob patterns relative to the repository root; a kind of change that is visible in the diff, such as adding a public member; or code of a certain shape, such as implementations of a type. When it combines these, say whether a change must match all of them or any of them.
- Rules: each rule as one sentence with must or should.
- Rationale: a link to the pull request, ADR, or `docs/` page that explains why, or one or two sentences when no record exists.
- Skill: the name of the skill that helps apply the guideline, if any.

List every guideline in the Guidelines section of `docs/README.md`, with when it applies and its main rule in one clause.

Do not mark files with comments to bring them into a guideline's scope; describe the code shape instead. A pointer comment is for a single exception that the description cannot express.

## Skills

A skill is a folder `.agents/skills/<name>/` with a `SKILL.md` that follows the [Agent Skills specification](https://agentskills.io/specification). Copilot and pi read `.agents/skills/`. Claude Code reads `.claude/skills/`, so each skill also has a symbolic link `.claude/skills/<name>` to `../../.agents/skills/<name>`. Edit a skill only under `.agents/skills/`.

- Use only the frontmatter fields the specification defines: `name`, `description`, `license`, `compatibility`, `metadata`, and `allowed-tools`. `name` matches the folder name.
- Start the `description` with when to use the skill, naming the same paths and kinds of change as the guideline's Applies to field. Agents see only the name and description until they load the skill, and in a first test they loaded a skill whose description named its trigger on every matching change and one that described only its subject on almost none.
- A skill that applies a guideline sets `metadata.particular-guideline` to the guideline's path, links to the guideline in its body, and does not restate its rules. The guideline controls.
- A skill that sets no rule, such as a procedure for running a test suite, omits `particular-guideline`. When a `docs/` page already covers the procedure, the skill links to it and adds only what an agent needs on top.
- The `particular-` name prefix is reserved for skills synchronized across repositories. Do not edit those in one repository.

On Windows, Git checks out the links as plain files unless `core.symlinks` is `true`, and Claude Code then does not find the skills.
