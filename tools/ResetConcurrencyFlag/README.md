# Retries concurrency flag reset

An [issue in ServiceControl 1.6.0 to 1.6.3](https://github.com/Particular/ServiceControl/pull/565) was identified that causes the retries feature of ServiceControl to stall and not process any subsequent retries. This issue is fixed in [ServiceControl 1.6.4](https://github.com/Particular/ServiceControl/releases/tag/1.6.4), however in order to reset stalled retries a manual step is needs to be performed by the user, this is where this tool will help.

This tool resets the concurrecy flag in stalled messages that were part of a batch retry.

## How to identify messages that are stalled?

"Stalled" messages are messages that have been retried but failed to return to the input queue of an endpoint.
To identify these messages, you need to open the `particular.servicecontrol.errors` queue and look at the headers of the current messages in there.
If you find messages with `ServiceControl.Retry.UniqueMessageId` header, eg
```xml
<HeaderInfo>
	<Key>ServiceControl.Retry.UniqueMessageId</Key>
	<Value>efb8fb3d-7649-e0ea-f3fc-f77fc79abc3b</Value>
</HeaderInfo>
```
You need to copy the value of that header and run the tool against that value, eg
```cmd
ResetMessageRetry.exe efb8fb3d-7649-e0ea-f3fc-f77fc79abc3b
```

This should give the following output:
```txt
Resetting message...
Done
```

**Note:**
The tool assumes that RavenDB instance is exposed at the default url, i.e. `http://localhost:33333/storage`. However, if you customized the ServiceControl configuration to alter the URL, you can pass the full URL in on the command line after the id, like this:
```cmd
ResetMessageRetry.exe <id> http://[machineName]:[port]/storage
```

### Here is an example how to do this for MSMQ transport using QueueExplorer
![](http://i.imgur.com/EWnh4Wq.jpg)

**If you have any difficulty performing this action for other transports contact us at support@particular.net**
