# Issue 558 Detector

An [issue in ServiceControl 1.6](https://github.com/Particular/ServiceControl/pull/558) was identified which causes too many messages to be retried when the user performs a bulk retry operation. This tool will check the logs in your ServiceControl instance to see if any instances of this can be found.

## How to run the tool

### 1. Shut down Service Control
Open the Services Panel, find the ServiceControl instance and stop it. 

### 2. Start Service Control in Maintenance Mode
From an administrative command prompt, run `ServiceControl.exe --maint`. This will expose the embedded RavenDB database via RavenDB Studio (by default at `http://localhost:33333/storage`). ServiceControl will keep processing messages as usual.

### 3. Run the tool from the command line
On the machine that ServiceControl is running, open a command prompt and run it in administrative mode. Navigate to directory where `Issue558Detector.exe` is stored and run it. 
The tool assumes that RavenDB instance is exposed at the default url, i.e. `http://localhost:33333/storage`. However, if you customized configuration then you can pass the full url in on the command line like this:

    Issue558Detector.exe http://[machineName]:[port]/storage

The tool outputs directly to the console window. To keep the output for examination later, pipe the output to a file using the `>` operator like this:

    Issue558Detector.exe > results.txt

### 4. Restart Service Control
In the Services Control Pane, find the ServiceControl instance and restart it.

## About the tool

### Sample output:

```
This tool is going to examine ServiceControl for potential messages affected by issue https://github.com/Particular/ServiceControl/pull/558


Creating Temp Index (this may take some time)...
 DONE

Scanning for messages that were sent for reprocessing and require your attention.

Message Id:         ddd7a384-907e-4e53-b8c4-a4f700ebfdb9
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	17-Aug-15 19:20:22 Z: [               ] MessageFailed
	17-Aug-15 21:23:36 Z: [               ] MessagesSubmittedForRetry
	19-Aug-15 03:34:07 Z: [               ] FailedMessageArchived
	19-Aug-15 05:56:15 Z: [       Affected] MessagesSubmittedForRetry
	19-Aug-15 16:14:12 Z: [May be affected] FailedMessageArchived

You are affected by issue https://github.com/Particular/ServiceControl/pull/558
Please upgrade ServiceControl to the latest version immediately.
Latest release:  https://github.com/Particular/ServiceControl/releases
Contact us via support (http://particular.net/support) and we will work with you to get your situation sorted out.

Removing Temp Index...
 DONE
```
### How it works?

The first step that the tool takes is to create a temporary index. This index is created in the embedded RavenDB database that is embedded inside ServiceControl. This index is only required for the tool and will be removed when the tool done executing. Depending on the number of messages that you have, this step make take some time (in our testing we have seen it take 10-15 minutes). 

Once the index has been created the tool will check each Failed Message in the database. Failed Messages are never deleted from ServiceControl so any message which has ever failed will still be there. The check involves looking at the series of events which have happened related to that failed message.

If a message is Archived or Resolved and then subsequently retried then it has been affected by Issue 558. The tool will output the details of any message which meets this criteria.

The details include:
* The type of the message
* The ID of the message
* The endpoint that the message was sent to
* A status for the message (more on this below) AND
* A timeline of events for the message

Each message that is output by the tool will have one of two statuses:
* `Definitely Affected` - means that the tool has detected a case where the message was sent for re-processing when it should not have been. Messages in this category require some manual intervention (see below).
* `May be affected` - means that the tool has encountered an event in the log that is ambiguous. Messages in the category require a user to interpret the timeline.

Once the tool has scanned all of the messages a footer message provides an overall health statement indicating if you have or have not been affected by Issue 558.

## Common results

The following are sample timelines which the detection tool might identify in your database, with explanation of what they mean and suggestion on how they should be handled. If you observe other timelines and you are not sure how to proceed, do not hesitate to contact our support.

### FailedMessageArchived > MessagesSubmittedForRetry
#### Sample detection tool output 

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

```
Message Id:         13837004-af4f-4fef-a9a8-1e230a579c39
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	19-Aug-15 03:26:34 Z: [               ] MessageFailed
	19-Aug-15 03:32:05 Z: [               ] FailedMessageArchived
	19-Aug-15 23:02:58 Z: [       Affected] MessagesSubmittedForRetry
	19-Aug-15 23:02:59 Z: [May be affected] MessageFailed
```

#### What that means?

The previously archived message was retried.

#### What should you do?

Using this information it is possible to examine the endpoint (in this case `Sample.Endpoint-A`) to see if the message (`Sample.Command.SendEmails`) that was sent has had any adverse effect.

If the message was successfully processed on the final retry then it will no longer appear in ServiceControl. If the message failed processing on the final retry then it should be archived.

### MessageFailureResolvedByRetry > MessagesSubmittedForRetry or MessageFailureResolvedByRetry > MessageSubmittedForRetry
#### Sample detection tool output
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
	23-Aug-15 06:07:35 Z: [May be affected] MessageFailureResolvedByRetry
```

#### What that means?

The message in question was successfully retried in the past. However, it was picked up again during "Retry all" or "Retry group" operation and was incorrectly attempted to be processed again.

#### What should you do?
Investigate the handlers for the retried message, depending on your implementation it may or may not have a negative impact on the system. If handlers are idempotent then there should not be negative impact on the system.
 
### MessageSubmittedForRetry > MessagesSubmittedForRetry or MessageSubmittedForRetry > MessageSubmittedForRetry
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
```
Message Id:         03a20f7d-bcf7-4c6a-870c-fb53a80f1544
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	20-Aug-15 05:30:12 Z: [               ] MessageFailed
	20-Aug-15 05:31:43 Z: [               ] MessageSubmittedForRetry
	21-Aug-15 13:09:13 Z: [       Affected] MessagesSubmittedForRetry
	21-Aug-15 13:09:15 Z: [May be affected] MessageResolvedByRetry
```
```
Message Id:         03a20f7d-bcf7-4c6a-870c-fb53a80f1544
Message Type:       Sample.Command.SendEmails
Receiving Endpoint: Sample.Endpoint-A
Status:             Definitely affected
Message History: 
	20-Aug-15 05:30:12 Z: [               ] MessageFailed
	20-Aug-15 05:31:43 Z: [               ] MessageSubmittedForRetry
	21-Aug-15 13:09:13 Z: [       Affected] MessagesSubmittedForRetry
```

#### What that means?

This timeline can be the result of the following situations:
  1. Audit Ingestion is turned off - If this is the case then ServiceControl will not hear about it if a message is successfully resolved.
  2. The first retry is still outstanding - If this is the case then it may be possible that a `MessageFailed` or a `MessageFailureResolvedByRetry` event will appear later. 
  3. The message was successfully retried before ServiceControl upgrade, but the `MessageFailureResolvedByRetry` event wasn't emitted. That might happen for older versions of ServiceControl (e.g. 1.4.4).
  4. The failed message (which could fail multiple times) was correctly retried.

#### What should you do?
There are a few additional check that can help you determine which of the following scenarios took place:
  1. Find the suspicious message in ServiceInsight. If you notice two messages with the same id, it means that the message was successfully retried before the ServiceControl upgrade. If both messages are green then the incorrect retry was successful. If one of the messages is red then it means the incorrect retry attempt failed.
  2. Navigate to the exposed RavenDB studio (by default it's exposed at http://localhost:33333/storage, [more details](http://docs.particular.net/servicecontrol/use-ravendb-studio)), open Documents tab and go to FailedMessages. There you can export data to CSV file for easier search. 
      - If the suspicious message does not have any FailureGroup and has Status 2 (Resolved), then it means it was incorrectly retried and that retry succeeded.
      - If the suspicious message does have a FailureGroup assigned and has Status 2 (Resolved), then it was most likely correctly retried failed message.
      - If the suspicious message does have a FailureGroup and has Status 1 (Unresolved), then the last retry attempt failed, but we can't determine its previous state.
  
## Other notes
If a failed message has been retried accidentally:
  1. It may have had an effect on your endpoint even if it failed again
  2. If the retry failed then the message may still be marked as Unresolved in ServiceControl. It needs to be archived to prevent it from being retried again in the future.
  3. MessageSubmittedForRetry vs MessagesSubmittedForRetry - the latter event was introduced in ServiceControl version 1.6.0, so it can help you determine whether the specific retry was attempted before or after the upgrade.
