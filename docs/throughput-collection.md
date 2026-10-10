Version 5.4.0 introduced the `Licensing Component` feature, which [collects usage data](https://docs.particular.net/servicepulse/usage) from within the Particular Platform.

The Error instance orchestrates the collection of usage data from three sources:

- Audit instances are queried once a day. The initial query retrieves all available historic data.
- The broker is queried once a day. Depending on the broker, the initial query retrieves the last 30 days of data.
- The Monitoring instance uses its metrics calculations to send usage data to the Error instance every 5 minutes. It sends the data to a predefined satellite queue with the default name `servicecontrol.throughput`.


### Why is the "servicecontrol.throughput" queue not a sub queue of the Error instance?

The usage collection queue needs to be known to the Monitoring instance.

At the time of creating this feature it was decided that having a queue name that is not dependent on the name of the Error instance means less setup for most customers, because the feature would "just work".

If the queue name was based on the Error instance name (for example "ErrorInstanceQueueName.throughput") then **every** customer would have to make updates to their Monitoring instance config to set the correct queue name.

The decision favored simplicity of upgrade over existing ServiceControl queue name conventions, consistent with the tech lead preferences for [software that "just works"](https://github.com/Particular/Strategy/blob/master/tech-lead-preferences/it-just-works.md#it-just-works) and [convenience](https://github.com/Particular/Strategy/blob/master/tech-lead-preferences/usability.md#convenience).


#### Why isn't the ServiceControl Management Utility (SCMU) used to ensure the names match?

SCMU is a Windows-only tool, and the installation of the Monitoring instance is separate from that of the Error instance.
The Monitoring instance can also be installed on a different server than the Error instance.
For similar reasons we do not configure the remote audit instances in SCMU.

### Can the "servicecontrol.throughput" queue be renamed?

Yes, the queue name can be changed via:

- the [LicensingComponent/ServiceControlThroughputDataQueue](https://docs.particular.net/servicecontrol/servicecontrol-instances/configuration#usage-reporting-when-using-servicecontrol-licensingcomponentservicecontrolthroughputdataqueue) setting on the Error instance
- the [Monitoring/ServiceControlThroughputDataQueue](https://docs.particular.net/servicecontrol/monitoring-instances/configuration#usage-reporting-monitoringservicecontrolthroughputdataqueue) on the Monitoring instance

These two settings must match for the usage reporting from Monitoring to work correctly.
