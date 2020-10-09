ServiceControl ![Current Version](https://img.shields.io/github/release/particular/servicecontrol.svg?style=flat&label=current%20version)
=====================

ServiceControl is the monitoring brain in the Particular Service Platform. It collects data on every single message flowing through the system (Audit Queue), errors (Error Queue), as well as additional information regarding sagas, endpoints heartbeats and custom checks (Control Queue). The information is then exposed to [ServicePulse](https://particular.net/servicepulse) and [ServiceInsight](https://particular.net/serviceinsight) via an HTTP API and SignalR notifications.

Where to Download
=====================

The current version of ServiceControl can be downloaded from https://particular.net/downloads.

User Documentation
=====================

Documentation for ServiceControl is located on the Particular Docs website at following address:

https://docs.particular.net/servicecontrol/

How to build
============

- Enable Windows Feature .NET Framework 3.5 support, which is needed to support the Wix components in the ServiceControl installer.
- If not using Visual Studio, you may need to install .NET 4.0 SDK according to https://stackoverflow.com/a/45509430


How to build and run Docker images
====================================

Each combination of ServiceControl instance, transport, and topology has a dedicated dockerfile. Select the instance, transport, and topology you want to run and build the `init` container and the runtime container by executing the following commands (using RabbitMQ Conventional topology as an example) from within the `src` folder:

```
docker build -f .\dockerfile.rabbitmq.conventional.init -t particular/servicecontrolrabbitconventional.init .
docker build -f .\dockerfile.rabbitmq.conventional -t particular/servicecontrolrabbitconventional .
```

Once the images are built, the instances can be started by first running the init container to provision the required queues and databases:

```
docker run --name servicecontrol.init -e "ServiceControl/ConnectionString={connectionstring}" -v c:/localfoldertostorethedatabasein/:c:/data/ -d particular/servicecontrolrabbitconventional.init
```

That will create the required queues and the database for ServiceControl. To run the container now that everything is provisioned:

```
docker run --name servicecontrol -p 33333:33333 -e "ServiceControl/ConnectionString={connectionstring}" -v c:/localfoldertostorethedatabasein/:c:/data/ -d particular/servicecontrolrabbitconventional
```

ServiceControl will now run in a docker container.