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

Each combination of ServiceControl instance, transport, and topology has a dedicated dockerfile. Select the instance, transport, and topology you want to run and build the `init` container and the runtime container by executing the following commands (using RabbitMQ Conventional topology as an example) from within the `src\docker` folder:

```
docker build -f .\dockerfile.rabbitmq.conventional.init -t particular/servicecontrolrabbitconventional.init ./../
docker build -f .\dockerfile.rabbitmq.conventional -t particular/servicecontrolrabbitconventional ./../
docker build -f .\dockerfile.rabbitmq.conventional.audit.init -t particular/servicecontrolrabbitconventional.audit.init ./../
docker build -f .\dockerfile.rabbitmq.conventional.audit -t particular/servicecontrolrabbitconventional.audit ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring.init -t particular/servicecontrolrabbitconventional.monitoring.init ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring -t particular/servicecontrolrabbitconventional.monitoring ./../
```

Once the images are built, the instances can be started by first running the init container to provision the required queues and databases:

```
docker run --name servicecontrol.init -e "ServiceControl/ConnectionString=host=[connectionstring]" -v c:/data/:c:/data/ -d particular/servicecontrolrabbitdirect.init
docker run --name servicecontrol.monitoring.init -e "Monitoring/ConnectionString=[connectionstring]" -d particular/servicecontrolrabbitconventional.monitoring.init
docker run --name servicecontrol.audit.init -e "ServiceControl.Audit/ConnectionString=host=[connectionstring]" -v c:/data/:c:/data/ -d particular/servicecontrolrabbitdirect.audit.init
```

That will create the required queues and the database for ServiceControl and ServiceControl.Audit. To run the containers now that everything is provisioned, first run the audit container:

```
docker run --name servicecontrol.audit -p 44444:44444 -e "ServiceControl.Audit/ConnectionString=host=[connectionstring]" -e 'ServiceControl.Audit/LicenseText=[licensecontents]' -e 'ServiceControl.Audit/ServiceControlQueueAddress=Particular.ServiceControl' -v c:/data/:c:/data/ -d particular/servicecontrolrabbitdirect.audit
```

Then grab its IP address using `docker inspect`, and specify it using the `ServiceControl/RemoteInstances` environment variable when starting the servicecontrol container. 

```
docker run --name servicecontrol -p 33333:33333 -e "ServiceControl/ConnectionString=host=[connectionstring]" -e 'ServiceControl/LicenseText=[licensecontents]' -e 'ServiceControl.Audit/ServiceControlQueueAddress=Particular.ServiceControl' -e "ServiceControl/RemoteInstances=[{'api_uri':'http://172.28.XXX.XXX:44444/api'}]" -v c:/data/:c:/data/ -d particular/servicecontrolrabbitdirect
```

ServiceControl will now run in a docker container.

To run a ServiceControl Monitoring instance:

```
docker run --name servicecontrol.monitoring -p 33633:33633 -e "Monitoring/ConnectionString=host=[connectionstring]" -e 'Monitoring/LicenseText=[licensecontents]' -d particular/servicecontrolrabbitdirect.monitoring
```