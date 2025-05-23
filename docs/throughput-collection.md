From version 5.4.0 we have introduce a new feature to collect usage data as part of the Platform.

Usage data is collected from 3 different sources:
- Audited messages
- Monitoring metrics
- Directly from the broker

The error instance is the orchestrator for all the collection from all these different endpoints.

Audited messages usage is collected  by querying the audit instances every so often.
Broker collection is also collected by querying the borker directly every 24 hours.
The monitoring metrics collection is collected by the metrics intance sending a direct message to a well known queue that by default is named "servicecontrol.throughput".


### Why is the "servicecontrol.throughput" queue hardcoded?
The TF that initially created this new feature decided that by hardcoding the queue name, it would be simpler for the customers to get started because they don't need to configure anything up front.
This was decided based on simplicity of upgrade vs maintanability.

### Why is the "servicecontrol.throughput" not a sub queue of the error instance?
Because this would mean customer would have to configure the sending endpoint (monitoring instance) with the name of this queue, which we wanted to avoid.

#### But we have SCMU, can't that make sure the queue names match?
SCMU is a Windows only tool, as well as, this would only work if they are installing all instances on the same machine.
Same reasons why, we do not configure the remote audit instances.

### Can the "servicecontrol.throughput" queue be renamed?
Yes, see https://docs.particular.net/servicecontrol/monitoring-instances/configuration#usage-reporting
