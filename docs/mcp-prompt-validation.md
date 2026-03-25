# MCP Prompt Validation

This document records the prompt-validation scenario set for the ServiceControl MCP surface.

The validation perspective is intentionally narrow: assume the agent only sees discovered tool names, tool descriptions, and parameter descriptions. It does not rely on `docs/mcp-investigation-guide.md` or repository source code.

## Error Scenarios

| Prompt | Expected tool choice | Validation notes |
| --- | --- | --- |
| What are the biggest current failure categories? | `get_errors_summary` or `get_failure_groups` | `get_failure_groups` is positioned as the first step for root-cause analysis; detail and mutating tools are not framed as starting points. |
| Why are messages failing in Billing? | `get_failure_groups` -> `get_failed_messages_by_endpoint` -> `get_failed_message_last_attempt` | The metadata separates grouped root-cause analysis, endpoint-scoped inspection, and last-attempt detail lookup. |
| Retry only the timeout-related failures | `get_failure_groups` -> `retry_failure_group` | `retry_failure_group` is described as the grouped retry for one root cause, while broader retry tools explicitly warn about broad impact. |
| Show me details for this failed message | `get_failed_message_by_id` | The tool description says it is for a specific failed message and points agents to list/group tools only when an ID is not yet known. |
| Retry everything | `retry_all_failed_messages` | The metadata allows the broad tool when explicitly requested, while warning that it changes system state and may affect a large number of messages. |

## Audit Scenarios

| Prompt | Expected tool choice | Validation notes |
| --- | --- | --- |
| Find messages related to order 12345 | `search_audit_messages` | The description explicitly says it is for a specific business identifier or text, and browsing tools point agents toward search for targeted lookups. |
| Show me what happened in this conversation | `get_audit_messages_by_conversation` | The description frames it as tracing a full flow across multiple endpoints once a conversation ID is known. |
| What is endpoint Billing doing? | `get_audit_messages_by_endpoint` | The metadata positions this as the single-endpoint activity view rather than a cross-endpoint trace. |
| Show recent system activity | `get_audit_messages` | The browsing tool is positioned for recent activity and timeline exploration. |
| Show the payload of this message | `get_audit_message_body` | The description explicitly says it is for inspecting payload or message data after locating a specific audit message. |

## Outcome

- Summary and grouping tools are preferred before detail tools for error investigation.
- Search and browse are clearly separated for audit scenarios.
- Conversation tracing and endpoint-centric inspection are differentiated.
- Broad mutating tools remain discoverable but are framed as explicit, risky choices rather than defaults.
- Identifier and endpoint parameter descriptions support the scenario selection by clarifying where IDs and names come from.
