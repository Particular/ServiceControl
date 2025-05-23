From version 5.4.0 a new `Licensing Component` feature was introduced to [collect usage data](https://docs.particular.net/servicepulse/usage) from within the Particular Platform.

Usage data is collected from 3 different sources:
- Audit instance(s)
- Monitoring instance
- Directly from the broker

The Error instance is the orchestrator for collecting usage data from all the different sources.

The Audit instance(s) is/are queried once a day to obtain usage data. The initial query will grab all available historic data.
The broker is queried once a day to obtain usage data. Depending on the broker, the initial query will grab the last 30 days of data.
The Monitoring instance uses its metrics calculations to send usage data to the Error instance every 5 minutes to a pre-defined satellite queue with the default name of `servicecontrol.throughput`.


### Why is the "servicecontrol.throughput" queue not a sub queue of the Error instance?

The usage collection queue needs to be known to the Monitoring instance.

At the time of creating this feature it was decided that having a queue name that is not dependent on the name of the Error instance means less setup for majority of customers since the feature would "just work" out of the box. 

If the queue name was based on the Error instance name (i.e. "ErrorInstanceQueueName.throughput") then **every ** customer would have to make updates to their Monitoring instance config to set the correct queue name.

The decision favoured simplicity of upgrade over existing ServiceControl queue name conventions, keeping in line with the tech lead preferences for [software that "just works"](https://github.com/Particular/Strategy/blob/master/tech-lead-preferences/it-just-works.md#it-just-works) and [convenience](https://github.com/Particular/Strategy/blob/master/tech-lead-preferences/usability.md#convenience).


#### Why isn't SCMU used to ensure the names match?

SCMU is a Windows only tool, plus the install of the Monitoring instance is separate to that of the Error instance. 
Additionally, the Monitoring instance can be installed on a different server to the Error instance.
For similar reasons we do not configure the remote audit instances in SCMU.

### Can the "servicecontrol.throughput" queue be renamed?

Yes, the queue name can be changed via:

- the [LicensingComponent/ServiceControlThroughputDataQueue](https://docs.particular.net/servicecontrol/servicecontrol-instances/configuration#usage-reporting-when-using-servicecontrol-licensingcomponentservicecontrolthroughputdataqueue) setting on the Error instance
- the [Monitoring/ServiceControlThroughputDataQueue](https://docs.particular.net/servicecontrol/monitoring-instances/configuration#usage-reporting-monitoringservicecontrolthroughputdataqueue) on the Monitoring instance

These two settings must match for the usage reporting from Monitoring to work correctly.
