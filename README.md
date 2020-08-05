ServiceControl ![Current Version](https://img.shields.io/github/release/particular/servicecontrol.svg?style=flat&label=current%20version)
=====================

ServiceControl is the monitoring brain in the Particular Service Platform. It collects data on every single message flowing through the system (Audit Queue), errors (Error Queue), as well as additional information regarding sagas, endpoints heartbeats and custom checks (Control Queue). The information is then exposed to [ServicePulse](https://particular.net/servicepulse) and [ServiceInsight](https://particular.net/serviceinsight) via an HTTP API and SignalR notifications.

Where to Download
=====================

The easiest way to download and install ServiceControl is through the Particular Service Platform Installer, available at https://particular.net/start-platform-download.

If you would like to download ServiceControl directly you can visit https://particular.net/downloads.

User Documentation
=====================

Documentation for ServiceControl is located on the Particular Docs website at following address:

https://docs.particular.net/servicecontrol/

How to build
============

- Enable Windows Feature .NET Framework 3.5 support, which is needed to support the Wix components in the ServiceControl installer.
- If not using Visual Studio, you may need to install .NET 4.0 SDK according to https://stackoverflow.com/a/45509430
