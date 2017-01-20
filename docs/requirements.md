At its heart ServiceControl and ServicePulse provide operational support for systems built on the Particular Service Platform. They do this by:

- monitoring system health
- diagnosing and treating errors

In this context, system refers to NServiceBus endpoints and communication with third-party services.

ServiceControl should provide the following features:

- process and store the contents of the error queue
- replay messages from the error queue
- process and store the contents of the audit queue
- generate and collect heartbeat messages
- generate and collect saga state changes
- provide an API for monitoring user-defined conditions
- provide operational events for consumption by other applications

Implicit in these requirements is that ServiceControl should:

- scale to an entire system based on the Particular Service Platform
- be durable (i.e. no message loss)

### Process and store the contents of the error queue

Rationale: To view the contents of the error queue in order to triage potential bugs or infrastructure failures in a system.

### Replay messages from the error queue

Rationale: To recover from bugs or infrastructure failures in order to maintain stability of a system.

### Process and store the contents of the audit queue

Rationale: To verify that a replayed message completed successfully. To understand the emergent design of a system through correllated messages. To identify design flaws in a system.

### Generate and collect heartbeat messages

Rationale: Verify that endpoints are up and able to send messages. Facilitates notification in the event an endpoint is no longer operational.

### Generate and collect saga state changes

Rationale: To understand the operational behavior of a long-running process and to troubleshoot design flaws.

### Provide an API for monitoring user-defined conditions

Rationale: To monitor endpoint dependencies. Allows team to more quickly identify and react to infrastructure failures.

### Provide operational events for consumption by other applications

Rationale: To provide extensibility for developers to react to events in ServiceControl. Allows users to provide better diagnostics for their systems.
