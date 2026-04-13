# Temporal Cloud CI/CD — Combined Mermaid Diagram Pack

## 1. System context

```mermaid
flowchart LR
    classDef ext fill:#eef6ff,stroke:#4f81bd,color:#1f2d3d,stroke-width:1px;
    classDef temporal fill:#f7f0ff,stroke:#7a52cc,color:#2f214d,stroke-width:1px;
    classDef worker fill:#eefbf3,stroke:#2d8a57,color:#173524,stroke-width:1px;
    classDef data fill:#fff8e8,stroke:#c48a00,color:#5d4300,stroke-width:1px;

    SCM[Source Control\npush / PR / merge / tag]:::ext
    OPS[Operator / Release Manager]:::ext
    TEMP[Temporal Cloud\norchestration + history + visibility]:::temporal
    WORKERS[.NET Worker Fleets\nWorkflow + Activity workers]:::worker
    EXEC[External Execution Systems\nrunners, registry, deploy tools]:::ext
    META[Artifact Metadata\nmanifests, digests, test evidence]:::data
    ENVS[DEV and QA Environments]:::data

    SCM --> TEMP
    OPS --> TEMP
    TEMP --> WORKERS
    WORKERS --> EXEC
    EXEC --> META
    EXEC --> ENVS
    TEMP --> META
```

## 2. Component architecture

```mermaid
flowchart LR
    classDef ext fill:#eef6ff,stroke:#4f81bd,color:#1f2d3d,stroke-width:1px;
    classDef temporal fill:#f7f0ff,stroke:#7a52cc,color:#2f214d,stroke-width:1px;
    classDef worker fill:#eefbf3,stroke:#2d8a57,color:#173524,stroke-width:1px;
    classDef data fill:#fff8e8,stroke:#c48a00,color:#5d4300,stroke-width:1px;

    subgraph A[Trigger and control plane]
        WEBHOOK[Webhook Intake API\nASP.NET Core]:::ext
        ADMIN[Ops API / Admin UI]:::ext
        SCHED[Scheduled triggers]:::ext
    end

    subgraph B[Temporal Cloud Namespace: cicd-prodctl]
        WF1[PipelineIngressWorkflow]:::temporal
        WF2[BuildValidationWorkflow]:::temporal
        WF3[VersionAndPublishWorkflow]:::temporal
        WF4[DeploymentWorkflow]:::temporal
        TQ1[Task Queue\ncicd.pipeline.orchestrator]:::temporal
        TQ2[Task Queue\ncicd.build.test]:::temporal
        TQ3[Task Queue\ncicd.gitversion]:::temporal
        TQ4[Task Queue\ncicd.publish]:::temporal
        TQ5[Task Queue\ncicd.deploy]:::temporal
        VIS[Visibility / Search Attributes]:::temporal
    end

    subgraph C[.NET worker fleets]
        W1[Workflow Worker]:::worker
        W2[Build/Test Activity Worker]:::worker
        W3[GitVersion Activity Worker]:::worker
        W4[Publish Activity Worker]:::worker
        W5[Deploy Activity Worker]:::worker
    end

    subgraph D[Execution systems]
        RUN[Runner Pool]:::ext
        GITV[GitVersion]:::ext
        REG[Container Registry]:::data
        MAN[Release Manifest Store]:::data
        DEV[DEV Environment]:::data
        QA[QA Environment]:::data
        OBS[Metrics / Logs / Traces]:::data
        NOTIFY[Notifications / Ticketing]:::ext
    end

    WEBHOOK --> WF1
    ADMIN --> WF1
    ADMIN --> WF4
    SCHED --> WF1

    WF1 --> WF2
    WF2 --> WF3
    WF3 --> WF4

    TQ1 --> W1
    TQ2 --> W2
    TQ3 --> W3
    TQ4 --> W4
    TQ5 --> W5

    WF1 -.orchestrates.-> TQ1
    WF2 -.activities.-> TQ2
    WF3 -.activities.-> TQ3
    WF3 -.activities.-> TQ4
    WF4 -.activities.-> TQ5

    W2 --> RUN
    W3 --> GITV
    W4 --> REG
    W4 --> MAN
    W5 --> DEV
    W5 --> QA

    W1 --> OBS
    W2 --> OBS
    W3 --> OBS
    W4 --> OBS
    W5 --> OBS

    WF1 --> VIS
    WF2 --> VIS
    WF3 --> VIS
    WF4 --> VIS

    W2 --> NOTIFY
    W4 --> NOTIFY
    W5 --> NOTIFY
```

## 3. End-to-end workflow

```mermaid
flowchart TD
    A[Receive trigger\nrepo + SHA + ref + event] --> B[Start PipelineIngressWorkflow]
    B --> C[Normalize metadata\nclassify branch / dedup]
    C --> D[BuildValidationWorkflow]
    D --> D1[Checkout source]
    D1 --> D2[Compile / package]
    D2 --> D3[Run unit tests]
    D3 --> D4[Run integration tests + required scans]
    D4 --> E{All required gates passed?}

    E -- No --> X[Fail pipeline\nrecord evidence\nnotify]
    E -- Yes --> F[VersionAndPublishWorkflow]
    F --> F1[Run GitVersion]
    F1 --> F2[Resolve SemVer + branch labels]
    F2 --> F3[Build/finalize image]
    F3 --> F4[Push image to registry]
    F4 --> F5[Capture image digest]
    F5 --> F6[Write release manifest]

    F6 --> G[DeploymentWorkflow]
    G --> G1[Deploy exact digest to DEV]
    G1 --> H{DEV verification passes?}
    H -- No --> Y[Stop failed\nretain artifact + diagnostics]
    H -- Yes --> I{Branch == main?}
    I -- No --> Z[Success\nDEV only]
    I -- Yes --> J[Deploy same digest to QA]
    J --> K{QA verification passes?}
    K -- No --> Q[Stop failed in QA]
    K -- Yes --> R[Success\nDEV then QA]
```

## 4. Pipeline ingress state machine

```mermaid
stateDiagram-v2
    [*] --> Received
    Received --> ValidatingEvent
    ValidatingEvent --> Invalid: malformed / unauthorized / unsupported
    ValidatingEvent --> Normalizing: valid trigger

    Normalizing --> DeriveIdentity
    DeriveIdentity --> Deduplicate
    Deduplicate --> DuplicateIgnored: workflow already exists / idempotent replay
    Deduplicate --> StartWorkflow: new workflow id accepted

    StartWorkflow --> UpsertSearchAttributes
    UpsertSearchAttributes --> DispatchBuildValidation
    DispatchBuildValidation --> Completed

    Invalid --> [*]
    DuplicateIgnored --> [*]
    Completed --> [*]
```

## 5. Build validation state machine

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Checkout
    Checkout --> CheckoutFailed: repo/ref unavailable
    Checkout --> Build

    Build --> BuildFailed: compile/package failed
    Build --> UnitTests

    UnitTests --> UnitFailed: unit tests failed
    UnitTests --> IntegrationTests

    IntegrationTests --> IntegrationFailed: integration tests failed
    IntegrationTests --> RequiredScans

    RequiredScans --> ScanFailed: policy/security gate failed
    RequiredScans --> ValidationPassed

    CheckoutFailed --> Failed
    BuildFailed --> Failed
    UnitFailed --> Failed
    IntegrationFailed --> Failed
    ScanFailed --> Failed

    ValidationPassed --> [*]
    Failed --> [*]
```

## 6. GitVersion state machine

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> LoadRepoContext
    LoadRepoContext --> RepoContextInvalid: shallow clone / wrong ref / missing config
    LoadRepoContext --> ComputeVersion

    ComputeVersion --> VersionComputed: semver + prerelease + metadata resolved
    ComputeVersion --> VersionError: cannot derive version

    VersionComputed --> PersistVersionMetadata
    PersistVersionMetadata --> Completed

    RepoContextInvalid --> Failed
    VersionError --> Failed
    Completed --> [*]
    Failed --> [*]
```

## 7. Publish state machine

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> PrepareImageMetadata
    PrepareImageMetadata --> BuildOrFinalizeImage
    BuildOrFinalizeImage --> ImageBuildFailed
    BuildOrFinalizeImage --> PushTagsAndDigest

    PushTagsAndDigest --> RegistryPushFailed
    PushTagsAndDigest --> CaptureDigest

    CaptureDigest --> WriteReleaseManifest
    WriteReleaseManifest --> ManifestWriteFailed
    WriteReleaseManifest --> Published

    ImageBuildFailed --> Failed
    RegistryPushFailed --> Failed
    ManifestWriteFailed --> Failed

    Published --> [*]
    Failed --> [*]
```

## 8. Deployment state machine

```mermaid
stateDiagram-v2
    [*] --> Published
    Published --> DeployDEV

    DeployDEV --> DEVDeployFailed: deploy call failed
    DeployDEV --> VerifyDEV

    VerifyDEV --> DEVVerifyFailed: health / smoke / policy failed
    VerifyDEV --> DEVReady

    DEVDeployFailed --> Failed
    DEVVerifyFailed --> Failed

    DEVReady --> BranchDecision
    BranchDecision --> SuccessDEVOnly: branch != main
    BranchDecision --> DeployQA: branch == main

    DeployQA --> QADeployFailed: deploy call failed
    DeployQA --> VerifyQA

    VerifyQA --> QAVerifyFailed: health / smoke / policy failed
    VerifyQA --> SuccessDEVQA

    QADeployFailed --> Failed
    QAVerifyFailed --> Failed

    SuccessDEVOnly --> [*]
    SuccessDEVQA --> [*]
    Failed --> [*]
```

## 9. Failure recovery control flow

```mermaid
flowchart TD
    A[Stage starts] --> B{Transient failure?}
    B -- Yes --> C[Retry Activity\nbounded attempts + backoff]
    C --> D{Recovered?}
    D -- Yes --> E[Continue stage]
    D -- No --> F[Mark stage failed]

    B -- No --> G{Deterministic / policy failure?}
    G -- Yes --> F
    G -- No --> H[Escalate to operator path]

    F --> I[Persist evidence\nlogs, status, search attributes]
    I --> J{Safe to resume from checkpoint?}
    J -- Yes --> K[Operator Update / rerun from stage]
    J -- No --> L[End failed and require new pipeline]

    H --> M[Signal / Update workflow]
    M --> N[Pause, cancel, override, or resume]
    N --> O[Controlled continuation or terminal failure]
```

## 10. Operator controls

```mermaid
flowchart LR
    classDef op fill:#eef6ff,stroke:#4f81bd,color:#1f2d3d,stroke-width:1px;
    classDef wf fill:#f7f0ff,stroke:#7a52cc,color:#2f214d,stroke-width:1px;
    classDef act fill:#eefbf3,stroke:#2d8a57,color:#173524,stroke-width:1px;

    O1[Operator / SRE]:::op --> Q1[Query current stage]:::wf
    O1 --> S1[Signal cancel / pause]:::wf
    O1 --> U1[Update resume / override]:::wf

    Q1 --> WF[Temporal Workflow]:::wf
    S1 --> WF
    U1 --> WF

    WF --> A1[Re-dispatch stage Activity]:::act
    WF --> A2[Skip blocked path\nif policy allows]:::act
    WF --> A3[Terminate workflow]:::act
    WF --> A4[Continue and notify]:::act
```
