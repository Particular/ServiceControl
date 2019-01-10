### Glossary

Transport integration tests -- an acceptance test that should be run for all supported transports to prove that SC can be run on a given transport

Business logic test -- an acceptance test that mainly exercises ServiceControl's business logic that does not have to be run for all supported transports. It is OK to run this test on a single selected transport (e.g. Learning)

### General guidance

Acceptance tests that can be turned into a convention tests should be removed. Here are some examples:
 * Transforming domain events into external integration events
 * Sending domain events via SinglR to SP
 * Creating event log items based on domain events 

### HeartbeatMonitoring

 * Retain a single transport integration test `When_an_endpoint_with_heartbeat_plugin_starts_up`
 
 * Run all other tests as business logic tests 

### CustomChecks

 * Retain a single per-transport test that processes a custom check status message and proves that the message is processed and status of the check return via API is correct

 * Have a transport-agnostic tests that verifies that all IUserInterfaceEvent-derived domain events are sent via SignalR -- this test would also be useful for other features e.g. EventLog
 
 * Have a convention unit test verifies that all events that should be sent via SignalR are marked as IUserInterfaceEvent

### SagaAudit

 * Run `When_a_message_that_is_handled_by_a_saga`  as a transport integration test to verify if saga audit information added to audit messages can be processed across all transports

 * Run `When_a_saga_instance_is_being_created` as a transport integration tests to verify if saga audit plugin messages can be processed with all transports

 * Run remaining tests as business logic tests

### ExternalIntegrations

 * Retain a single transport integration test that verifies that SC can publish events and that an external endpoint can receive them. This tests should be run for each transport because transports differ when it comes to support of Pub/Sub. It can be any of the existing tests.

 * Have a a convention unit tests that verifies if all domain events that are supposed to trigger external integration have their matching EventPublishers.

### MessageRedirects

 * Keep the tests as-is but treat them all as business logic tests.

### Audits

 * Retain `When_a_message_fails_to_import` and `When_processed_message_is_imported` as transport integration tests for success and failure cases of message processing

 * Keep all other tests as-is but treat them all business logic tests

 * Move `When_a_message_is_imported_twice` to the Audits folder and treat as business logic test

### MessageFailures

 * Run `When_a_message_fails_to_import` as a transport integration test

 * Run remaining tests as business logic tests

 * Use convention unit test instead of `When_a_message_has_failed.Should_add_an_event_log_item` to verify that event log item is added.

### Recoverability

 * Merge that folder with MessageFailures

 * Retain as transport integration tests the tests that has been created to verify fixes for specific transport integration bugs:

   - `When_a_message_is_retried_and_succeeds_with_a_reply`
   - `When_a_message_is_retried_with_a_replyTo_header`
   - `When_a_message_without_a_correlationid_header_is_retried`

 * Retain `When_a_group_is_retried` as a transport integration tests to verify group retries work across all supported transports

 * Treat all remaining group retry tests as business logic tests


### MultiInstance 

 * Run `When_a_message_retry_audit_is_sent_to_a_remote_instance` and `When_endpoint_detected_via_audits_on_slave` as transport integration tests because they depend on cross-instance communication via pub/Sub

 * Run remaining tests as business logic tests


### Summary

After the change there will be ~15 transport integration tests that need to be run across all the transport. That should significantly shorten the duration of a full test run.

