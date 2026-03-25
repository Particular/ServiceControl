# ServiceControl MCP Investigation Guide

This guide explains how to use the ServiceControl MCP tools for investigation work.

The MCP surface is designed to help AI agents and human operators choose the right tool based on intent, scope, and risk.

## Tool Inventory

### Primary instance tools

| Tool | Category | Risk | Notes |
| --- | --- | --- | --- |
| `get_errors_summary` | summary | safe | Best first step for overall failed-message health |
| `get_failure_groups` | summary | safe | Best first step for root-cause analysis |
| `get_retry_history` | detail | safe | Confirms whether similar retries were already attempted |
| `get_failed_messages` | list | safe | Broad failed-message listing |
| `get_failed_messages_by_endpoint` | list | safe | Use when the endpoint is already known |
| `get_failed_message_by_id` | detail | safe | Full failed-message history |
| `get_failed_message_last_attempt` | detail | safe | Lighter detail view for the latest failure |
| `retry_failed_message` | action | moderate | Narrow retry for one failed message |
| `retry_failed_messages` | action | moderate | Retry a specific set of failed messages |
| `retry_failed_messages_by_queue` | action | high | Retries all unresolved failures in one queue |
| `retry_all_failed_messages_by_endpoint` | action | high | Retries all failures for one endpoint |
| `retry_failure_group` | action | moderate | Best grouped retry after fixing one root cause |
| `retry_all_failed_messages` | action | high | Broadest retry operation |
| `archive_failed_message` | action | moderate | Dismiss one failed message |
| `archive_failed_messages` | action | moderate | Dismiss a chosen set of failed messages |
| `archive_failure_group` | action | high | Dismiss all failed messages in one failure group |
| `unarchive_failed_message` | action | moderate | Restore one archived failed message |
| `unarchive_failed_messages` | action | moderate | Restore a chosen set of archived failed messages |
| `unarchive_failure_group` | action | high | Restore all archived messages in one failure group |

### Audit instance tools

| Tool | Category | Risk | Notes |
| --- | --- | --- | --- |
| `get_known_endpoints` | discovery | safe | Start here when you need endpoint names |
| `get_endpoint_audit_counts` | summary | safe | Throughput trends for one endpoint |
| `get_audit_messages` | list | safe | Broad audit-message browsing |
| `search_audit_messages` | search | safe | Full-text lookup for specific terms or IDs |
| `get_audit_messages_by_endpoint` | list/search | safe | Scoped endpoint investigation |
| `get_audit_messages_by_conversation` | detail | safe | Trace a message flow across related messages |
| `get_audit_message_body` | detail | safe | Inspect serialized payload content |

## Read-only vs State-changing

### Read-only tools

Use these first during an investigation. They do not change system state.

- Error investigation: `get_errors_summary`, `get_failure_groups`, `get_retry_history`, `get_failed_messages`, `get_failed_messages_by_endpoint`, `get_failed_message_by_id`, `get_failed_message_last_attempt`
- Audit investigation: `get_known_endpoints`, `get_endpoint_audit_counts`, `get_audit_messages`, `search_audit_messages`, `get_audit_messages_by_endpoint`, `get_audit_messages_by_conversation`, `get_audit_message_body`

### State-changing tools

Use these only when the user explicitly wants to retry, archive, or restore failed messages.

- Retry tools: `retry_failed_message`, `retry_failed_messages`, `retry_failed_messages_by_queue`, `retry_all_failed_messages_by_endpoint`, `retry_failure_group`, `retry_all_failed_messages`
- Archive tools: `archive_failed_message`, `archive_failed_messages`, `archive_failure_group`
- Restore tools: `unarchive_failed_message`, `unarchive_failed_messages`, `unarchive_failure_group`

Broad actions such as `retry_all_failed_messages`, `retry_failed_messages_by_queue`, `retry_all_failed_messages_by_endpoint`, `archive_failure_group`, and `unarchive_failure_group` can affect many messages. Prefer the narrowest tool that matches the user's intent.

## Commonly Confused Tool Pairs

### Error tools

- `get_failed_messages` vs `get_failed_messages_by_endpoint`: use the endpoint-specific tool only when the endpoint is already known
- `retry_failed_messages` vs `retry_failure_group`: use the grouped retry when messages share the same root cause; use the ID-list retry when the user selected specific failed messages
- `archive_failed_messages` vs `archive_failure_group`: use the grouped archive when the whole failure group should be dismissed; use the ID-list archive when only some failed messages should be archived

### Audit tools

- `get_audit_messages` vs `search_audit_messages`: browse with `get_audit_messages` when the user wants an overview; search with `search_audit_messages` when the user supplies a concrete term, identifier, or phrase
- `get_audit_messages_by_endpoint` vs `get_audit_messages_by_conversation`: use the endpoint tool for one receiver endpoint; use the conversation tool to follow a cross-endpoint message flow
- `get_audit_messages` vs `get_audit_message_body`: browse metadata first, then fetch body content only when the actual payload matters

## Recommended Investigation Flows

### Error investigation flow

1. `get_errors_summary`
2. `get_failure_groups`
3. `get_failed_messages` or `get_failed_messages_by_endpoint`
4. `get_failed_message_by_id` or `get_failed_message_last_attempt`
5. `get_retry_history` when a retry decision depends on prior attempts
6. Only then consider retry, archive, or unarchive tools

### Audit investigation flow

1. `get_known_endpoints` if the endpoint name is not known yet
2. `get_audit_messages` for broad browsing, or `search_audit_messages` for a concrete term or identifier
3. `get_audit_messages_by_endpoint` to narrow to one receiver endpoint
4. `get_audit_messages_by_conversation` to trace the related message flow
5. `get_audit_message_body` when the payload content is needed

## Task-to-tool Mappings

- "What is failing right now?" -> `get_errors_summary`, then `get_failure_groups`
- "Show recent failures in Sales" -> `get_failed_messages_by_endpoint`
- "Show the full history for this failure" -> `get_failed_message_by_id`
- "Show only the latest exception for this failure" -> `get_failed_message_last_attempt`
- "Retry the failures caused by this bug" -> `retry_failure_group`
- "Retry everything in this queue" -> `retry_failed_messages_by_queue`
- "Dismiss this one failure" -> `archive_failed_message`
- "Restore the archived failures for this root cause" -> `unarchive_failure_group`
- "What endpoints do we have?" -> `get_known_endpoints`
- "Show recent audit traffic" -> `get_audit_messages`
- "Find audit messages mentioning order 12345" -> `search_audit_messages`
- "Show what Billing processed" -> `get_audit_messages_by_endpoint`
- "Trace this conversation" -> `get_audit_messages_by_conversation`
- "Show me the payload for this audit message" -> `get_audit_message_body`
