# Retries concurrency flag reset

An [issue in ServiceControl 1.6.0 to 1.6.3](https://github.com/Particular/ServiceControl/pull/565) was identified that causes the retries feature of ServiceControl to stall and not process any subsequent retries. This issue is fixed in [ServiceControl 1.7.0](https://github.com/Particular/ServiceControl/releases/tag/1.7.0), however in order to reset stalled retries a manual step is needs to be performed by the user, this is where this tool will help.

When a message is retried in ServiceControl 1.6.x, a lock is placed on the message to ensure it cannot be retried again. If we did not do this then a message could be send to the receiving endpoint more than once and processed multiple times. If the message fails again the lock is removed allowing subsequent retries.

A situation can occur where ServiceControl creates the lock on the failed message but the message cannot be sent to the original destination. When this happens the message being retried gets placed on the ServiceControl error queue but the lock remains.

This tool allows you to remove the lock from a failed message allowing it to be retried again.

## How to identify messages that are stalled?

"Stalled" messages are messages that have been retried but failed to return to the input queue of an endpoint.
To identify these messages, you need to open the `particular.servicecontrol.errors` queue (note the queue name changes based on the name of the windows service) and look at the headers of the current messages in there.
If you find messages with `ServiceControl.Retry.UniqueMessageId` header, eg
```xml
<HeaderInfo>
	<Key>ServiceControl.Retry.UniqueMessageId</Key>
	<Value>efb8fb3d-7649-e0ea-f3fc-f77fc79abc3b</Value>
</HeaderInfo>
```
You need to copy the value of that header and run the tool against that value.

#### Here is an example how to obtain a stalled message IDs using QueueExplorer for MSMQ
![](http://i.imgur.com/EWnh4Wq.jpg)

**If you have any difficulty performing this action for other transports contact us at support@particular.net**

## How to run the tool

### 1. Shut down Service Control
Open the Services Panel, find the `Particular ServiceControl` instance and stop it. 

### 2. Start Service Control in Maintenance Mode
From an administrative command prompt, run `ServiceControl.exe --maint`. This will expose the embedded RavenDB database via RavenDB Studio (by default at `http://localhost:33333/storage`). ServiceControl will keep processing messages as usual.

### 3. Run the tool from the command line
Example:
```cmd
ResetMessageRetry.exe efb8fb3d-7649-e0ea-f3fc-f77fc79abc3b
```

This should give the following output:
```txt
Resetting message...
Done
```

**Note:**
The tool assumes that RavenDB instance is exposed at the default url, i.e. http://localhost:33333/storage. If you have altered the URL in the ServiceControl configuration, you can pass the full URL to the executable like this:
```cmd
ResetMessageRetry.exe <id> http://[machineName]:[port]/storage
```

After the tool finishes running, press `Enter` in ServiceControl to exit maintenance mode.

### 4. Restart Service Control
In the Services Control Pane, find the `Particular ServiceControl` instance and restart it.
