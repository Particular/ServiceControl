# Unavailable runtime dependencies

ServiceControl instances (Main, Audit, and Monitoring) have various runtime dependencies (persistence, transport, etc) and they are expected to handle their unavailability in a predictable way. Causes of dependencies being unavailable include:

* invalid instance configuration e.g. connection string, secrets
* network outages
* invalid network configuration e.g. firewall misconfiguration
* data store failures e.g. broken indexes, DB process failures
* missing and/or invalid permissions
* uninitialized state e.g. missing indexes, missing queues
* etc.

Such cases will render some (or all) of the functionalities offered by the platform as unavailable.

## How ServiceControl handles unavailable runtime dependencies

ServiceControl instances handle unavailable runtime dependencies in two different ways, depending on the time at which the failure scenario is detected:

* Failures detected during startup -> instance stops immediately
* Failures detected after a successful startup -> instance does not stop and is expected to try and recover from the failure once a dependency becomes available again
