# Testing scenarios

A long (though not exhaustive) list, although not every change will merit running every single test.

## Instance management installation

- [ ] Upgrade an existing instance of ServiceControl to the version being released
- [ ] Create a new instance of ServiceControl
- [ ] Check with both localhost and a custom hostname
- [ ] Upgrade an existing instance of ServiceControl to the version being released using the Powershell Scripts
- [ ] Create a new instance of ServiceControl using the Powershell Scripts

## Functionality

- [ ] Install and run ServiceControl Primary, Audit, and Monitoring instances
- [ ] [Install the ServiceControl.SmokeTesting tool](https://github.com/Particular/ServiceControl.SmokeTest#installing)
- [ ] Start the smoke testing tool e.g.: `dotnet servicecontrol.smoketest rabbitmq`
- [ ] Test Custom checks
   - Run `check-fail 1` and verify in [ServicePulse->Custom Checks] that a warning appeared
   - Run `check-pass 1` and verify in [ServicePulse->Custom Checks] that the warning is gone
- [ ] Test heartbeats
   - With the tool running verify in [ServicePulse->Heartbeats->Healthy Endpoints] that `Endpoints0` to `Endpoints5` and `Sender` endpoints are reported
   - Run `stop 1` and verify in [ServicePulse->Heartbeats->Healthy Endpoints] that `Endpoint1` has moved to [Unhealthy Endpoints]
- [ ] Recoverability
   - [ ] Retry single message 
      - Run `throw 1` and `send 1 1` commands
      - Run `recover 1` and retry the message from ServicePulse
   - [ ] Retry message group
      - Run `throw 1` and `send 1 50` commands
      - Run `recover 1` and retry the whole message group from ServicePulse
   - [ ] Message body editing
      - Run `throw 1` and `send 1 1` commands
      - Run `recover 1`, edit the message body, and retry the message from ServicePulse
   - [ ] Failed queue redirection
      - Run `throw 1` and `send 1 1` commands
      - Go to [Service Pulse->Configuration->Create redirect] and add `Endpoint1` to `Endpoint2` redirect
      - Retry the failed message
- [ ] Audits
   - [ ] Generate messages e.g. `send 1 10` and check if these are accessible in ServiceInsight
   - [ ] Generate message messages with non-trivial content using `send-fulltext 1 10`. Open ServiceInsight and check if a message can be found in the search box using one of the strings from the `LongString` property
   - [ ] Generate messages using `fanout` and check in the [Sequence Diagram] view in ServiceInsight that the graph is properly visualized
- [ ] Saga Auditing
   - [ ] Generate messages using `saga-audits 1` and check in the [Saga] view in ServieInsight that the graph is properly visualized
- [ ] Integration Events
   - [ ] Download [integration events sample](https://docs.particular.net/samples/servicecontrol/events-subscription/). Switch the sample to the appropriate transport. 
   - [ ] Generate a failing message in the `NServiceBusEndptoin` and validate that an integration event `MessageFailed` is received by the `EndpointsMonitor` 
- [ ] Monitoring
   - [ ] Nativate to the [Monitoring] tab in ServicePulse and verify that all 6 endpoints are visible
   - [ ] Verify that the failed messages indicator is rendered for endpoints with failed messages
   - [ ] Navigate to the details of `Endpoint0` and verify that all the graphs are properly rendered  

## Chaos testing

Try to break ServiceControl instances by gracefully (CTRL+C) and ungracefully (kill) processes to validate if both storage and logic behavior correctly. This type of testing is very difficult to automate.

- [ ] Ingestion, have the smoketest tool or the load generator generator create a large number of messsages:
  - [ ] Gracefully stop (CTRL+C) processes
  - [ ] Ungracefully (kill)  processes
- [ ] Retry groups, create a large retry group and interrup these:
  - [ ] Gracefully stop (CTRL+C) processes
  - [ ] Ungracefully (kill)  processes

## Performance/Load testing

Test the new version against the previous version.

- [ ] Test performance with a clean database
- [ ] Test performance with a moderate database that exceeds the RAM of the machine
- [ ] Test performance of a large database that exceeds 500 GB
- [ ] Test stability by:
  - [ ] Rebooting the machine and verifying that ServiceControl instances start in a reasonable amount of time and behave correctly
  - [ ] Stopping ServiceControl Windows services, verifying they stop as expected, and subsequently start in a reasonable amount of time and behave correctly
  - [ ] Killing the hosting virtual machine from the Azure portal and verifying instances behave correctly after the reboot
  - [ ] Trying to upgrade instances to newer versions while ingestion runs at full speed under load, and verify the upgrade is successful 

Review CPU/RAM utilization and disk IO.

