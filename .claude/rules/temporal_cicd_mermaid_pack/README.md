# Temporal Cloud CI/CD Mermaid Diagram Pack

This pack contains polished Mermaid diagrams for a full CI/CD pipeline implemented with **Temporal Cloud** orchestration and **.NET / C#** workers.

## Contents

1. `00_combined_diagram_pack.md` — all diagrams in one document
2. `01_system_context.md` — top-level system context
3. `02_component_architecture.md` — detailed component architecture
4. `03_end_to_end_workflow.md` — end-to-end pipeline flow
5. `04_pipeline_ingress_state_machine.md` — trigger normalization and dedup
6. `05_build_validation_state_machine.md` — checkout, build, test, scan gates
7. `06_gitversion_state_machine.md` — version computation and validation
8. `07_publish_state_machine.md` — image publish and manifest creation
9. `08_deployment_state_machine.md` — DEV / QA progression rules
10. `09_failure_recovery_control_flow.md` — failure containment and recovery
11. `10_operator_controls.md` — query, signal, update, rerun, resume paths

## Design intent

- Temporal Cloud is the orchestration engine
- External systems perform build, test, publish, and deploy work
- GitVersion runs **after** required gates pass and **before** publish
- All successful branches deploy to **DEV**
- Only `main` continues from **DEV** to **QA**
- The same published image digest moves forward across environments

## Suggested use

- Use `00_combined_diagram_pack.md` for review docs and RFCs
- Use the individual files in slide decks, design notes, and implementation tickets
- Keep workflow names, task queues, and Search Attributes aligned with code
