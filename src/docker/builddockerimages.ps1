#
# This file is for local developer testing only. This is not used by the build server.
#
msbuild ../ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\servicecontrol.azureservicebus.dockerfile -t particular/servicecontrol.azureservicebus ./../
docker build -f .\servicecontrol.azureservicebus.audit.dockerfile -t particular/servicecontrol.azureservicebus.audit ./../
docker build -f .\servicecontrol.azureservicebus.monitoring.dockerfile -t particular/servicecontrol.azureservicebus.monitoring ./../

docker build -f .\servicecontrol.rabbitmq.conventional.dockerfile -t particular/servicecontrol.rabbitmq.conventional ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit.dockerfile -t particular/servicecontrol.rabbitmq.conventional.audit ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring.dockerfile -t particular/servicecontrol.rabbitmq.conventional.monitoring ./../

docker build -f .\servicecontrol.rabbitmq.direct.dockerfile -t particular/servicecontrol.rabbitmq.direct ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit.dockerfile -t particular/servicecontrol.rabbitmq.direct.audit ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring.dockerfile -t particular/servicecontrol.rabbitmq.direct.monitoring ./../

docker build -f .\servicecontrol.azurestoragequeues.dockerfile -t particular/servicecontrol.azurestoragequeues ./../
docker build -f .\servicecontrol.azurestoragequeues.audit.dockerfile -t particular/servicecontrol.azurestoragequeues.audit ./../
docker build -f .\servicecontrol.azurestoragequeues.monitoring.dockerfile -t particular/servicecontrol.azurestoragequeues.monitoring ./../

docker build -f .\servicecontrol.sqlserver.dockerfile -t particular/servicecontrol.sql ./../
docker build -f .\servicecontrol.sqlserver.audit.dockerfile -t particular/servicecontrol.sql.audit ./../
docker build -f .\servicecontrol.sqlserver.monitoring.dockerfile -t particular/servicecontrol.sql.monitoring ./../

docker build -f .\servicecontrol.amazonsqs.dockerfile -t particular/servicecontrol.amazonsqs ./../
docker build -f .\servicecontrol.amazonsqs.audit.dockerfile -t particular/servicecontrol.amazonsqs.audit ./../
docker build -f .\servicecontrol.amazonsqs.monitoring.dockerfile -t particular/servicecontrol.amazonsqs.monitoring ./../
