## Setup

1. Install ServiceControl version 2.
1. Add one instance of ServiceControl v2 with MSMQ. Use a name that relates to v2, for example `v2_SC`.
   - Configure a unique error queue name. Use a name that relates to v2, for example `v2_error`.
   - Configure a unique audit queue name. Use a name that relates to v2, for example `v2_audit`.
1. Install ServiceControl version 3.
1. Add one instance of ServiceControl v3 with MSMQ. Use a name that relates to v3, for example `v3_SC`.
   - Configure a unique error queue name. Use a name that relates to v3, for example `v3_error`.
   - Configure a unique audit queue name. Use a name that relates to v3, for example `v3_audit`.
1. Download or check out the [FaultTolerance](https://docs.particular.net/samples/faulttolerance/) sample.
1. Edit the configuration of the sample project
   - Reconfigure the endpoint to use the MSMQ transport

## V2 remote notifies V3 master about successful retry

1. Configure ServiceControl v3 as Remote.
1. Restart v3 SC
1. Configure ServiceControl v2 as Master.
1. Restart v2 SC
1. Edit the configuration of the sample project
   - Configure the error queue to the unique error queue assigned to the v2 instance of SC
   - Configure auditing to the unique audit queue assigned to the v3 instance of SC
1. Run the sample. It sends error messages to the v2 instance of SC.
1. Set the sample to process messages successfully
1. Connect ServiceInsight to the v2 instance of ServiceControl
1. Retry one or more failed messages in ServiceInsight
1. Confirm that the sample processed the messages successfully
1. Confirm that the message status is Resolved in ServiceInsight

## Reset

1. Stop the v2 instance of SC
1. Remove the database directory of the v2 instance of SC
1. Start the v2 instance of SC
1. Stop the v3 instance of SC
1. Remove the database directory of the v3 instance of SC
1. Start the v3 instance of SC

## V3 remote notifies V2 master about successful retry

1. Configure ServiceControl v2 as Remote.
1. Restart v2 SC
1. Configure ServiceControl v3 as Master.
1. Restart v3 SC
1. Edit the configuration of the sample project
   - Configure the error queue to the unique error queue assigned to the v3 instance of SC
   - Configure auditing to the unique audit queue assigned to the v2 instance of SC
1. Run the sample. It sends error messages to the v3 instance of SC.
1. Set the sample to process messages successfully
1. Connect ServiceInsight to the v3 instance of ServiceControl
1. Retry one or more failed messages in ServiceInsight
1. Confirm that the sample processed the messages successfully
1. Confirm that the message status is Resolved in ServiceInsight
