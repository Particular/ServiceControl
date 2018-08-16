I am not sure a PR is necessary:

## Setup

1. Install ServiceControl version 2.
1. Add 1 instance of ServiceControl v2 with MSMQ. It would help to use a name relating it to v3, e.g. `v2_SC`
   - Configure a unique error queue name. It would help to use a name relating it to v2, e.g. `v2_error`
   - Configure a unique audit queue name. It would help to use a name relating it to v2, e.g. `v2_audit`
1. Install ServiceControl version 3
1. Add 1 instance of ServiceControl v3 with MSMQ. It would help to use a name relating it to v3, e.g. `v3_SC`
   - Configure a unique error queue name. It would help to use a name relating it to v3, e.g. `v3_error`
   - Configure a unique audit queue name. It would help to use a name relating it to v3, e.g. `v3_audit`
1. Download or checkout the [FaultTolerance](https://docs.particular.net/samples/faulttolerance/) sample.
1. Edit the configuration of the sample project
   - Reconfigure the endpoint to use the MSMQ transport
   
## Scenario 1   
1. Configure ServiceControl v3 as Remote.
1. Restart v3 SC
1. Configure ServiceControl v2 as Master. 
1. Restart v2 SC
1. Edit the configuration of the sample project
   - Configure the error queue to the unique error queue assigned to v2 instance of SC
   - Configure auditing to the unique audit queue assigned to v3 instance of SC.
1. Run the sample, sending error messages to the v2 instance of SC
1. Set the sample to successfully process messages
1. Connect ServiceInsight to the v2 instance of ServiceControl
1. Retry one or more failed messages in ServiceInsight
1. Confirm the message(s) successfully processed in the sample
1. Confirm the message status is Resolved in ServiceInsight

## Reset 
1. Stop the v2 instance of SC
1. Remove the database directory of the v2 instance of SC
1. Start the v2 instance of SC
1. Stop the v3 instance of SC
1. Remove the database directory of the v3 instance of SC
1. Start the v3 instance of SC

## Scenario 2
1. Configure ServiceControl v2 as Remote.
1. Restart v2 SC
1. Configure ServiceControl v3 as Master. 
1. Restart v3 SC
1. Edit the configuration of the sample project
   - Configure the error queue to the unique error queue assigned to v3 instance of SC
   - Configure auditing to the unique audit queue assigned to v2 instance of SC.
1. Run the sample, sending error messages to the v3 instance of SC
1. Set the sample to successfully process messages
1. Connect ServiceInsight to the v3 instance of ServiceControl
1. Retry one or more failed messages in ServiceInsight
1. Confirm the message(s) successfully processed in the sample
1. Confirm the message status is Resolved in ServiceInsight
