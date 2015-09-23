# Issue 558 Detector

An [issue in ServiceControl 1.6](https://github.com/Particular/ServiceControl/pull/558) was identified which causes too many messages to be retried when the user performs a bulk retry operation. This tool will check the logs in your ServiceControl instance to see if any instances of this can be found.

## How to run the tool

### 1. Shut down Service Control
Open the Services Panel, find the ServiceControl instance and stop it. 

### 2. Start Service Control in Maintenance Mode
From an administrative command prompt, run `ServiceControl.exe --maint`. This will expose the embedded database of ServiceControl. No other API or processing will be started. 

### 3. Run the tool from the command line
Run `Issue558Detector.exe` from the same machine that ServiceControl is running on. This assumes that the exposed RavenDB instance is available at `http://localhost:33333/storage`. If the RavenDB instance is made available at a different url then you can pass the full url in on the command line like this:

    Issue558Detector.exe http://[machineName]:[port]/storage

The tool outputs directly to the console window. To keep the output for examination later, pipe the output to a file using the `>` operator like this:

    Issue558Detector.exe > results.txt

### 4. Restart Service Control
In the Services Control Pane, find the ServiceControl instance and restart it.

## What does it do
This is a sample output file:

    Sample goes here

The first step that the tool takes is to create a temporary index. This index is created in the embedded RavenDB database that is embedded inside ServiceControl. This index is only required for tool and will be removed when the tool stops. Depending on the number of messages that you have, this step make take some time (in our testing we have seen it take 10-15 minutes). 

Once the index has been created the tool will check each Failed Message in the database. Failed Messages are never deleted from ServiceControl so any message which has ever failed will still be there. The check involves looking at the series of events which have happened related to that message.

If a message is Archived or Resolved and then subsequently retried then it has been affected by Issue 558. The tool will output the details of any message which meets this criteria.

The details include:
* The type of the message
* The ID of the message
* The endpoint that the message was sent to
* A status for the message (more on this below) AND
* A timeline of events for the message

Each message that is output by the tool will have one of two statuses:
* `Definitely Affected` - means that the tool has detected a case where the message was sent for re-processing when it should not have been. Messages in this category require some manual intervention (see below).
* `May be affected` - means that the tool has encountered an event in the log that it does not understand. Messages in the category require a user to interpret the timeline.

Once the tool has scanned all of the messages a footer message provides an overall health statement indicating if you have or have not been affected by Issue 558.

## Common Cases
The following are a number of common cases, what they look like, and how they should be handled.

### A Bulk Retry after an Archive Operation

```
Message Id:         13837004-af4f-4fef-a9a8-1e230a579c39
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	19-Aug-15 03:26:34 Z: [               ] MessageFailed
	19-Aug-15 03:32:05 Z: [               ] FailedMessageArchived
	19-Aug-15 23:02:58 Z: [       Affected] MessagesSubmittedForRetry
	19-Aug-15 23:02:59 Z: [May be affected] MessageFailureResolvedByRetry
```

Over the life of this message there have been 4 events.
1. The message failed - This event is expected as it indicates that the message was read from the error queue
2. The message was archived - Likely by a user of Service Pulse
3. The message was sent for Retry - This should not have happened because the message was previously archived. This is the event that causes the message to be marked as `Affected`.
4. The message was retried successfully - Note that although this is the outcome of the retry in step 3, which should not have happened. Any events that happen to a message after the incorrect Retry are also likely to be not what was expected.

Using this information it is possible to examine the endpoint (in this case `Sample.Endpoint-A`) to see if the message (`Sample.Command.SendEmails`) that was sent has had any adverse effect.

If the message was successfully processed on the final retry then it will no longer appear in ServiceControl. If the message failed processing on the final retry then it should be archived.

### A Bulk Retry after a Successful Retry

```
Message Id:         03a20f7d-bcf7-4c6a-870c-fb53a80f1544
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	19-Aug-15 03:45:34 Z: [               ] MessageFailed
	19-Aug-15 03:52:05 Z: [               ] MessageSubmittedForRetry
	19-Aug-15 03:55:06 Z: [               ] MessageFailureResolvedByRetry
	23-Aug-15 06:07:32 Z: [       Affected] MessagesSubmittedForRetry
	23-Aug-15 06:07:35 Z: [May be affected] MessageFailed
```

Over the life of this message there have been 5 events.
1. The message failed - expected
2. The message was retried - expected
3. The message was resolved by the retry - expected
4. The message was sent for Retry - This should not have happened because the message was previously resolved. This is the event that causes the message to be marked as `Affected`. 
5. The retry failed. Note that even if the incorrect retry failed, attempting to reprocess the message may still have had an effect on your system.

### A Bulk Retry after an Outstanding Retry
```
Message Id:         03a20f7d-bcf7-4c6a-870c-fb53a80f1544
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	20-Aug-15 05:30:12 Z: [               ] MessageFailed
	20-Aug-15 05:31:43 Z: [               ] MessageSubmittedForRetry
	21-Aug-15 13:09:13 Z: [       Affected] MessagesSubmittedForRetry
	21-Aug-15 13:09:15 Z: [May be affected] MessageFailed
```

Over the life of this message there have been 4 events.
1. The message failed - expected
2. The message was retried - expected
3. The message was sent for Retry - This should not have happened (see below). This is the event that causes the message to be marked as `Affected`
4. The second retry failed.

This can happen for under two circumstances:
1. Audit Ingestion is turned off - If this is the case then ServiceControl will not hear about it if a message is successfully resolved.
2. The first retry is still outstanding - If this is the case then it may be possible that a `MessageFailed` or a `MessageFailureResolvedByRetry` event will appear later. 

## Other notes
If a failed message has been retried accidentally:
1. It may have had an effect on your endpoint even if it failed again
2. If the retry failed then the message may still be marked as Unresolved in ServiceControl. It needs to be archived to prevent it from being retried again in the future. 