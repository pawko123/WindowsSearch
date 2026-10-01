---
name: hub-workflow
description: Standard workflow protocol and coding guidelines for modifying the Hub project
---

# Hub Modification Workflow

When asked to make changes to the Hub or core components, follow this strict sequence:

1. **Plan & Approve**: Outline the exact proposed code changes and what tests need to be written. Do not edit code until the user explicitly approves the plan.
2. **Update Code**: Implement the agreed-upon application logic following the Coding Guidelines below.
3. **Update Tests**: Implement the agreed-upon tests.
4. **Update Docs (MD)**: If the change alters behavior, configuration, or architecture, update the relevant Markdown files.
5. **Feedback Loop**: Present the finalized code and tests, then get feedback from the user and reiterate.

## Coding Guidelines
- **Separation of Concerns**: Keep logic nicely separated. Do not mix heavy business logic with view/UI binding logic.
- **Class Structure**: 
  - Place all fields, properties, and variables at the top of the class.
  - Place all methods at the bottom of the class.
- **Method Ordering**: Group public methods at the top of the method section, followed by private/helper methods underneath.
- **Robustness**: Remember to use appropriate logging (`AppLogger`) and strictly catch/handle relevant exceptions.
- **Documentation**: For more information and architectural rules, always look in the project's `.md` docs (e.g., `.copilot/PROVIDER_GUIDE.md`, `.copilot/SETTINGS_GUIDE.md`).