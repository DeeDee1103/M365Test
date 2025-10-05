# Hybrid eDiscovery Collector - Process Flow Diagram

**Project:** Hybrid Microsoft 365 eDiscovery Collection System  
**Date:** October 4, 2025  
**Version:** 2.0 - Multi-User Concurrent Processing

---

## 1. High-Level System Architecture Flow

```mermaid
graph TB
    subgraph "User Layer"
        U1[Legal Team Member]
        U2[IT Professional]
        U3[Administrator]
        U4[Auditor]
    end

    subgraph "API Layer"
        API[eDiscovery Intake API<br/>Port: 5230]
        SWAGGER[Swagger Documentation<br/>/swagger]
        AUTH[Authentication & RBAC]
    end

    subgraph "Processing Layer"
        WM[Worker Manager]
        W1[Worker Instance 1]
        W2[Worker Instance 2]
        W3[Worker Instance N]
    end

    subgraph "Intelligence Layer"
        AR[AutoRouter Service]
        JM[Job Manager]
        LB[Load Balancer]
    end

    subgraph "Data Layer"
        DB[(Multi-User Database<br/>SQLite/Azure SQL)]
        LOGS[Structured Logs<br/>Audit Trails]
    end

    subgraph "External Services"
        GRAPH[Microsoft Graph API]
        GDC[Graph Data Connect]
        STORAGE[Storage Systems<br/>NAS/Azure Blob]
    end

    U1 --> API
    U2 --> API
    U3 --> API
    U4 --> API

    API --> AUTH
    API --> SWAGGER
    API --> JM

    JM --> AR
    JM --> LB
    JM --> DB

    LB --> W1
    LB --> W2
    LB --> W3

    W1 --> GRAPH
    W1 --> GDC
    W2 --> GRAPH
    W2 --> GDC
    W3 --> GRAPH
    W3 --> GDC

    AR --> DB
    WM --> DB
    API --> LOGS
    W1 --> LOGS
    W2 --> LOGS
    W3 --> LOGS

    W1 --> STORAGE
    W2 --> STORAGE
    W3 --> STORAGE

    classDef userClass fill:#e1f5fe
    classDef apiClass fill:#f3e5f5
    classDef processClass fill:#e8f5e8
    classDef intelligenceClass fill:#fff3e0
    classDef dataClass fill:#fce4ec
    classDef externalClass fill:#f1f8e9

    class U1,U2,U3,U4 userClass
    class API,SWAGGER,AUTH apiClass
    class WM,W1,W2,W3 processClass
    class AR,JM,LB intelligenceClass
    class DB,LOGS dataClass
    class GRAPH,GDC,STORAGE externalClass
```

---

## 2. Detailed Multi-User Concurrent Processing Flow

```mermaid
sequenceDiagram
    participant U1 as User 1 (Legal)
    participant U2 as User 2 (IT Pro)
    participant API as Intake API
    participant AUTH as Authentication
    participant JM as Job Manager
    participant DB as Database
    participant LB as Load Balancer
    participant W1 as Worker 1
    participant W2 as Worker 2
    participant GRAPH as MS Graph API
    participant AUDIT as Audit Logger

    Note over U1,AUDIT: Multi-User Concurrent Collection Process

    %% User 1 Flow
    U1->>API: POST /api/matters (Create Matter A)
    API->>AUTH: Validate User & Role (Legal)
    AUTH-->>API: Authorized
    API->>DB: Insert Matter A
    API->>AUDIT: Log: Matter Created by User 1
    API-->>U1: Matter A Created

    U1->>API: POST /api/jobs (Create Collection Job A)
    API->>JM: Process Job Request A
    JM->>DB: INSERT Job A (Status: Pending)
    API-->>U1: Job A Queued

    %% User 2 Flow (Concurrent)
    U2->>API: POST /api/matters (Create Matter B)
    API->>AUTH: Validate User & Role (IT Pro)
    AUTH-->>API: Authorized
    API->>DB: Insert Matter B
    API->>AUDIT: Log: Matter Created by User 2
    API-->>U2: Matter B Created

    U2->>API: POST /api/jobs (Create Collection Job B)
    API->>JM: Process Job Request B
    JM->>DB: INSERT Job B (Status: Pending)
    API-->>U2: Job B Queued

    %% Worker Processing (Atomic Job Assignment)
    Note over W1,W2: Workers Poll for Available Jobs

    W1->>JM: GetNextAvailableJobAsync()
    JM->>DB: BEGIN TRANSACTION
    JM->>DB: SELECT Job WHERE Status = Pending
    JM->>DB: UPDATE Job A SET Status = Processing
    JM->>DB: INSERT JobAssignment (Job A → Worker 1)
    JM->>DB: COMMIT TRANSACTION
    JM-->>W1: Return Job A (Locked)

    W2->>JM: GetNextAvailableJobAsync()
    JM->>DB: BEGIN TRANSACTION
    JM->>DB: SELECT Job WHERE Status = Pending
    JM->>DB: UPDATE Job B SET Status = Processing
    JM->>DB: INSERT JobAssignment (Job B → Worker 2)
    JM->>DB: COMMIT TRANSACTION
    JM-->>W2: Return Job B (Locked)

    %% Concurrent Processing
    par Worker 1 Processing Job A
        W1->>AUDIT: Log: Job A Processing Started
        W1->>GRAPH: Authenticate & Collect Data
        GRAPH-->>W1: Email/OneDrive Data
        W1->>DB: Update Progress & Heartbeat
        W1->>AUDIT: Log: Collection Progress
        W1->>DB: Mark Job A Complete
        W1->>AUDIT: Log: Job A Completed
    and Worker 2 Processing Job B
        W2->>AUDIT: Log: Job B Processing Started
        W2->>GRAPH: Authenticate & Collect Data
        GRAPH-->>W2: Email/OneDrive Data
        W2->>DB: Update Progress & Heartbeat
        W2->>AUDIT: Log: Collection Progress
        W2->>DB: Mark Job B Complete
        W2->>AUDIT: Log: Job B Completed
    end

    %% Notification Back to Users
    JM->>API: Job A Status Update
    API-->>U1: Job A Completed
    JM->>API: Job B Status Update
    API-->>U2: Job B Completed
```

---

## 3. AutoRouter Decision Process Flow

```mermaid
flowchart TD
    START([Collection Request Received]) --> VALIDATE{Validate Request}
    VALIDATE -->|Valid| ESTIMATE[Estimate Data Size & Volume]
    VALIDATE -->|Invalid| ERROR[Return Error]

    ESTIMATE --> QUOTA[Check Current Graph API Quota]
    QUOTA --> DECISION{Route Decision Logic}

    DECISION -->|Small Dataset<br/>< 100GB<br/>< 500k Items| GRAPHAPI[Route to Graph API]
    DECISION -->|Large Dataset<br/>> 100GB<br/>> 500k Items| GDC[Route to Graph Data Connect]
    DECISION -->|Quota Exceeded| GDC

    GRAPHAPI --> CONFIDENCE[Calculate Confidence Score]
    GDC --> CONFIDENCE

    CONFIDENCE --> LOG[Log Routing Decision]
    LOG --> RETURN[Return Route Recommendation]
    RETURN --> END([Process Complete])

    ERROR --> END

    classDef startEnd fill:#4caf50,stroke:#333,stroke-width:2px,color:#fff
    classDef decision fill:#ff9800,stroke:#333,stroke-width:2px,color:#fff
    classDef process fill:#2196f3,stroke:#333,stroke-width:2px,color:#fff
    classDef error fill:#f44336,stroke:#333,stroke-width:2px,color:#fff

    class START,END startEnd
    class VALIDATE,DECISION decision
    class ESTIMATE,QUOTA,GRAPHAPI,GDC,CONFIDENCE,LOG,RETURN process
    class ERROR error
```

---

## 4. Worker Instance Lifecycle Flow

```mermaid
stateDiagram-v2
    [*] --> Initializing

    Initializing --> Registering: Configuration Loaded
    Registering --> Healthy: Registration Successful
    Registering --> Failed: Registration Failed

    Healthy --> Polling: Start Job Polling
    Polling --> Acquiring: Job Available
    Polling --> Polling: No Jobs Available

    Acquiring --> Processing: Job Acquired (Locked)
    Acquiring --> Polling: Job Already Taken

    Processing --> Heartbeat: Send Heartbeat
    Heartbeat --> Processing: Continue Processing
    Processing --> Completing: Collection Finished
    Processing --> Retrying: Temporary Failure
    Processing --> Failed: Permanent Failure

    Retrying --> Processing: Retry Successful
    Retrying --> Failed: Max Retries Exceeded

    Completing --> Polling: Job Completed Successfully

    Failed --> Cleanup: Clean up Resources
    Cleanup --> [*]

    Healthy --> Shutdown: Graceful Shutdown Signal
    Polling --> Shutdown: Graceful Shutdown Signal
    Processing --> Shutdown: Complete Current Job First

    Shutdown --> [*]

    note right of Heartbeat
        Every 30 seconds:
        - Update last heartbeat
        - Report current status
        - Update system metrics
    end note

    note right of Processing
        Concurrent Processing:
        - Atomic job acquisition
        - Real-time progress updates
        - Chain of custody logging
        - Error handling & retry
    end note
```

---

## 5. Database Transaction Flow for Concurrent Job Assignment

```mermaid
sequenceDiagram
    participant W1 as Worker 1
    participant W2 as Worker 2
    participant JM as Job Manager
    participant DB as Database
    participant LOCK as Lock Manager

    Note over W1,LOCK: Concurrent Job Assignment with Pessimistic Locking

    par Worker 1 Request
        W1->>JM: GetNextAvailableJobAsync()
        JM->>DB: BEGIN TRANSACTION
        JM->>LOCK: Acquire Pessimistic Lock
        LOCK-->>JM: Lock Acquired
        JM->>DB: SELECT * FROM CollectionJobs WHERE Status = 'Pending'
        DB-->>JM: Return Job 123
        JM->>DB: UPDATE CollectionJobs SET Status = 'Processing' WHERE Id = 123
        JM->>DB: INSERT INTO JobAssignments (JobId=123, WorkerId=W1, LockToken=ABC123)
        JM->>DB: COMMIT TRANSACTION
        JM->>LOCK: Release Lock
        JM-->>W1: Job 123 Assigned
    and Worker 2 Request
        W2->>JM: GetNextAvailableJobAsync()
        Note over JM: Wait for Lock
        JM->>DB: BEGIN TRANSACTION
        JM->>LOCK: Acquire Pessimistic Lock
        LOCK-->>JM: Lock Acquired
        JM->>DB: SELECT * FROM CollectionJobs WHERE Status = 'Pending'
        DB-->>JM: Return Job 124 (Job 123 now Processing)
        JM->>DB: UPDATE CollectionJobs SET Status = 'Processing' WHERE Id = 124
        JM->>DB: INSERT INTO JobAssignments (JobId=124, WorkerId=W2, LockToken=DEF456)
        JM->>DB: COMMIT TRANSACTION
        JM->>LOCK: Release Lock
        JM-->>W2: Job 124 Assigned
    end

    Note over W1,W2: Both Workers Now Process Different Jobs Concurrently
```

---

## 6. User Role-Based Access Control Flow

```mermaid
flowchart TD
    USER[User Login] --> AUTH{Authentication}
    AUTH -->|Valid| ROLE{Determine Role}
    AUTH -->|Invalid| DENY[Access Denied]

    ROLE -->|Administrator| ADMIN[Full System Access<br/>• User Management<br/>• All Matters & Jobs<br/>• System Configuration<br/>• Unlimited Concurrency]

    ROLE -->|Manager| MANAGER[Team Management Access<br/>• Team Matters & Jobs<br/>• Assign Jobs to Team<br/>• View Team Activity<br/>• 10 Concurrent Jobs]

    ROLE -->|Legal Analyst| ANALYST[Basic Collection Access<br/>• Own Matters & Jobs<br/>• Create Collections<br/>• View Own Activity<br/>• 3 Concurrent Jobs]

    ROLE -->|Auditor| AUDITOR[Read-Only Access<br/>• View All Data<br/>• Audit Logs<br/>• Compliance Reports<br/>• 5 Concurrent Views]

    ADMIN --> SESSION[Create Session]
    MANAGER --> SESSION
    ANALYST --> SESSION
    AUDITOR --> SESSION

    SESSION --> TRACK[Track User Activity]
    TRACK --> AUDIT[Log All Actions]
    AUDIT --> EXPIRE{Session Timeout?}

    EXPIRE -->|No| CONTINUE[Continue Session]
    EXPIRE -->|Yes| LOGOUT[Auto Logout]

    CONTINUE --> TRACK
    LOGOUT --> END[Session Ended]
    DENY --> END

    classDef roleClass fill:#e3f2fd
    classDef accessClass fill:#f3e5f5
    classDef securityClass fill:#e8f5e8
    classDef endClass fill:#ffebee

    class ADMIN,MANAGER,ANALYST,AUDITOR roleClass
    class SESSION,TRACK,CONTINUE accessClass
    class AUTH,AUDIT,EXPIRE securityClass
    class DENY,LOGOUT,END endClass
```

---

## 7. End-to-End Collection Process Flow

```mermaid
journey
    title Multi-User eDiscovery Collection Journey

    section User Onboarding
        User Login: 5: User
        Role Assignment: 4: Administrator
        Session Creation: 5: System
        Access Validation: 5: System

    section Matter Management
        Create Legal Matter: 5: Legal Team
        Define Custodians: 4: Legal Team
        Set Collection Scope: 4: Legal Team
        Review & Approve: 3: Manager

    section Job Creation
        Create Collection Job: 5: User
        AutoRouter Analysis: 5: System
        Route Selection: 4: AutoRouter
        Job Queuing: 5: System

    section Concurrent Processing
        Worker Assignment: 5: Load Balancer
        Atomic Job Lock: 5: Job Manager
        Data Collection: 4: Worker
        Progress Updates: 5: Worker
        Heartbeat Monitoring: 5: System

    section Quality Assurance
        Data Validation: 4: Worker
        Hash Generation: 5: Worker
        Chain of Custody: 5: System
        Audit Logging: 5: System

    section Completion
        Job Completion: 5: Worker
        Notification: 5: System
        Report Generation: 4: System
        Archive & Store: 5: System
```

---

## 8. System Performance & Monitoring Flow

```mermaid
graph LR
    subgraph "Performance Metrics"
        CPU[CPU Usage]
        MEM[Memory Usage]
        DISK[Disk I/O]
        NET[Network Traffic]
    end

    subgraph "Application Metrics"
        JOBS[Active Jobs]
        USERS[Active Users]
        THROUGHPUT[Data Throughput]
        LATENCY[Response Latency]
    end

    subgraph "Health Monitoring"
        WORKER[Worker Health]
        API[API Health]
        DB[Database Health]
        EXTERNAL[External Services]
    end

    subgraph "Alerting System"
        THRESHOLD{Threshold Check}
        ALERT[Generate Alert]
        NOTIFY[Notify Administrators]
        ESCALATE[Escalate if Critical]
    end

    subgraph "Audit & Compliance"
        AUDIT[Audit Logs]
        COMPLIANCE[Compliance Reports]
        RETENTION[Data Retention]
        EXPORT[Export Capabilities]
    end

    CPU --> THRESHOLD
    MEM --> THRESHOLD
    JOBS --> THRESHOLD
    USERS --> THRESHOLD
    WORKER --> THRESHOLD
    API --> THRESHOLD

    THRESHOLD -->|Exceeded| ALERT
    THRESHOLD -->|Normal| AUDIT

    ALERT --> NOTIFY
    NOTIFY --> ESCALATE

    AUDIT --> COMPLIANCE
    COMPLIANCE --> RETENTION
    RETENTION --> EXPORT

    classDef metricsClass fill:#e1f5fe
    classDef healthClass fill:#e8f5e8
    classDef alertClass fill:#fff3e0
    classDef auditClass fill:#fce4ec

    class CPU,MEM,DISK,NET,JOBS,USERS,THROUGHPUT,LATENCY metricsClass
    class WORKER,API,DB,EXTERNAL healthClass
    class THRESHOLD,ALERT,NOTIFY,ESCALATE alertClass
    class AUDIT,COMPLIANCE,RETENTION,EXPORT auditClass
```

---

## 9. Error Handling & Recovery Flow

```mermaid
flowchart TD
    ERROR[Error Detected] --> TYPE{Error Type}

    TYPE -->|Network Error| NETWORK[Network Error Handler]
    TYPE -->|Authentication Error| AUTH[Auth Error Handler]
    TYPE -->|Database Error| DATABASE[Database Error Handler]
    TYPE -->|Processing Error| PROCESSING[Processing Error Handler]

    NETWORK --> RETRY1{Retry Count < 3}
    AUTH --> REAUTH[Re-authenticate]
    DATABASE --> RECONNECT[Reconnect Database]
    PROCESSING --> RETRY2{Retry Count < 5}

    RETRY1 -->|Yes| WAIT1[Wait with Exponential Backoff]
    RETRY1 -->|No| FAIL1[Mark Job Failed]

    RETRY2 -->|Yes| WAIT2[Wait with Exponential Backoff]
    RETRY2 -->|No| FAIL2[Mark Job Failed]

    REAUTH --> SUCCESS1{Auth Success}
    RECONNECT --> SUCCESS2{Connection Success}

    SUCCESS1 -->|Yes| CONTINUE1[Continue Processing]
    SUCCESS1 -->|No| FAIL3[Mark Job Failed]

    SUCCESS2 -->|Yes| CONTINUE2[Continue Processing]
    SUCCESS2 -->|No| FAIL4[Mark Job Failed]

    WAIT1 --> NETWORK
    WAIT2 --> PROCESSING

    FAIL1 --> CLEANUP[Cleanup Resources]
    FAIL2 --> CLEANUP
    FAIL3 --> CLEANUP
    FAIL4 --> CLEANUP

    CLEANUP --> REASSIGN[Reassign to Healthy Worker]
    REASSIGN --> NOTIFY[Notify Administrators]

    CONTINUE1 --> END[Resume Normal Processing]
    CONTINUE2 --> END
    NOTIFY --> END

    classDef errorClass fill:#ffcdd2
    classDef handlerClass fill:#fff3e0
    classDef retryClass fill:#e8f5e8
    classDef successClass fill:#c8e6c9
    classDef failClass fill:#ffcdd2

    class ERROR errorClass
    class NETWORK,AUTH,DATABASE,PROCESSING handlerClass
    class RETRY1,RETRY2,WAIT1,WAIT2,REAUTH,RECONNECT retryClass
    class SUCCESS1,SUCCESS2,CONTINUE1,CONTINUE2,END successClass
    class FAIL1,FAIL2,FAIL3,FAIL4,CLEANUP,REASSIGN,NOTIFY failClass
```

---

## Summary

This comprehensive process flow diagram illustrates the complete hybrid eDiscovery collector system with multi-user concurrent processing capabilities. The diagrams show:

1. **High-level system architecture** with all components and their interactions
2. **Detailed multi-user concurrent processing** with atomic job assignment
3. **AutoRouter decision logic** for intelligent routing between Graph API and GDC
4. **Worker lifecycle management** with health monitoring and load balancing
5. **Database transaction flows** ensuring data consistency in concurrent operations
6. **Role-based access control** with security and session management
7. **End-to-end user journey** from login to job completion
8. **Performance monitoring** and alerting systems
9. **Error handling and recovery** mechanisms for system resilience

The system successfully implements enterprise-grade multi-user concurrent processing with proper job coordination, security isolation, and comprehensive audit trails as requested in your original requirement to "handle many users can run this tool and process at the same time."
