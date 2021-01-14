#
# This file is for local developer testing only. This is not used by the build server.
#
msbuild ../ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\servicecontrol.azureservicebus-windows.dockerfile -t particular/servicecontrol.azureservicebus-windows ./../
docker build -f .\servicecontrol.azureservicebus.init-windows.dockerfile -t particular/servicecontrol.azureservicebus.init-windows ./../
docker build -f .\servicecontrol.azureservicebus.audit-windows.dockerfile -t particular/servicecontrol.azureservicebus.audit-windows ./../
docker build -f .\servicecontrol.azureservicebus.audit.init-windows.dockerfile -t particular/servicecontrol.azureservicebus.audit.init-windows ./../
docker build -f .\servicecontrol.azureservicebus.monitoring-windows.dockerfile -t particular/servicecontrol.azureservicebus.monitoring-windows ./../
docker build -f .\servicecontrol.azureservicebus.monitoring.init-windows.dockerfile -t particular/servicecontrol.azureservicebus.monitoring.init-windows ./../

docker build -f .\servicecontrol.rabbitmq.conventional-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional-windows ./../
docker build -f .\servicecontrol.rabbitmq.conventional.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional.init-windows ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional.audit-windows ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional.audit.init-windows ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional.monitoring-windows ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.conventional.monitoring.init-windows ./../

docker build -f .\servicecontrol.rabbitmq.direct-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct-windows ./../
docker build -f .\servicecontrol.rabbitmq.direct.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct.init-windows ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct.audit-windows ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct.audit.init-windows ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct.monitoring-windows ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring.init-windows.dockerfile -t particular/servicecontrol.rabbitmq.direct.monitoring.init-windows ./../

docker build -f .\servicecontrol.azurestoragequeues-windows.dockerfile -t particular/servicecontrol.azurestoragequeues-windows ./../
docker build -f .\servicecontrol.azurestoragequeues.init-windows.dockerfile -t particular/servicecontrol.azurestoragequeues.init-windows ./../
docker build -f .\servicecontrol.azurestoragequeues.audit-windows.dockerfile -t particular/servicecontrol.azurestoragequeues.audit-windows ./../
docker build -f .\servicecontrol.azurestoragequeues.audit.init-windows.dockerfile -t particular/servicecontrol.azurestoragequeues.audit.init-windows ./../
docker build -f .\servicecontrol.azurestoragequeues.monitoring-windows.dockerfile -t particular/servicecontrol.azurestoragequeues.monitoring-windows ./../
docker build -f .\servicecontrol.azurestoragequeues.monitoring.init-windows.dockerfile -t particular/servicecontrol.azurestoragequeues.monitoring.init-windows ./../

docker build -f .\servicecontrol.sqlserver-windows.dockerfile -t particular/servicecontrol.sql-windows ./../
docker build -f .\servicecontrol.sqlserver.init-windows.dockerfile -t particular/servicecontrol.sql.init-windows ./../
docker build -f .\servicecontrol.sqlserver.audit-windows.dockerfile -t particular/servicecontrol.sql.audit-windows ./../
docker build -f .\servicecontrol.sqlserver.audit.init-windows.dockerfile -t particular/servicecontrol.sql.audit.init-windows ./../
docker build -f .\servicecontrol.sqlserver.monitoring-windows.dockerfile -t particular/servicecontrol.sql.monitoring-windows ./../
docker build -f .\servicecontrol.sqlserver.monitoring.init-windows.dockerfile -t particular/servicecontrol.sql.monitoring.init-windows ./../

docker build -f .\servicecontrol.amazonsqs-windows.dockerfile -t particular/servicecontrol.amazonsqs-windows ./../
docker build -f .\servicecontrol.amazonsqs.init-windows.dockerfile -t particular/servicecontrol.amazonsqs.init-windows ./../
docker build -f .\servicecontrol.amazonsqs.audit-windows.dockerfile -t particular/servicecontrol.amazonsqs.audit-windows ./../
docker build -f .\servicecontrol.amazonsqs.audit.init-windows.dockerfile -t particular/servicecontrol.amazonsqs.audit.init-windows ./../
docker build -f .\servicecontrol.amazonsqs.monitoring-windows.dockerfile -t particular/servicecontrol.amazonsqs.monitoring-windows ./../
docker build -f .\servicecontrol.amazonsqs.monitoring.init-windows.dockerfile -t particular/servicecontrol.amazonsqs.monitoring.init-windows ./../
