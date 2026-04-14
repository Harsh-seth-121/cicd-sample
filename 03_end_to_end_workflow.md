# End-to-End Workflow — Temporal Cloud CI/CD

```mermaid
flowchart LR
    subgraph L["Ingress & Build Validation"]
        direction TB
        A[Receive trigger\nrepo + SHA + ref + event] --> B[Start PipelineIngressWorkflow]
        B --> C[Normalize metadata\nclassify branch / dedup]
        C --> D[BuildValidationWorkflow]
        D --> D1[Checkout source]
        D1 --> D2[Compile / package]
        D2 --> D3[Run unit tests]
        D3 --> D4[Integration tests + scans]
        D4 --> E{All gates passed?}
        E -- No --> X[Fail pipeline\nrecord evidence + notify]
    end

    subgraph R["Version, Publish & Deploy"]
        direction TB
        F[VersionAndPublishWorkflow] --> F1[Run GitVersion]
        F1 --> F2[Resolve SemVer + labels]
        F2 --> F3[Build / finalize image]
        F3 --> F4[Push to registry]
        F4 --> F5[Capture image digest]
        F5 --> F6[Write release manifest]
        F6 --> G[DeploymentWorkflow]
        G --> G1[Deploy digest to DEV]
        G1 --> H{DEV verified?}
        H -- No --> Y[Stop failed\nretain diagnostics]
        H -- Yes --> I{Branch == main?}
        I -- No --> Z[Success — DEV only]
        I -- Yes --> J[Deploy digest to QA]
        J --> K{QA verified?}
        K -- No --> W[Stop failed in QA]
        K -- Yes --> S[Success — DEV + QA]
    end

    E -- Yes --> F
```
