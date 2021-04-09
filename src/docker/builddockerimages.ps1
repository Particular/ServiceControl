#
# This file is for local developer testing only. This is not used by the build server.
#
msbuild ../ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\servicecontrol.azureservicebus.dockerfile -t particular.azurecr.io/servicecontrol.azureservicebus ./../
docker build -f .\servicecontrol.azureservicebus.audit.dockerfile -t particular.azurecr.io/servicecontrol.azureservicebus.audit ./../
docker build -f .\servicecontrol.azureservicebus.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.azureservicebus.monitoring ./../

# docker build -f .\servicecontrol.rabbitmq.conventional.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.conventional ./../
# docker build -f .\servicecontrol.rabbitmq.conventional.audit.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.conventional.audit ./../
# docker build -f .\servicecontrol.rabbitmq.conventional.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.conventional.monitoring ./../

# docker build -f .\servicecontrol.rabbitmq.direct.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.direct ./../
# docker build -f .\servicecontrol.rabbitmq.direct.audit.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.direct.audit ./../
# docker build -f .\servicecontrol.rabbitmq.direct.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.rabbitmq.direct.monitoring ./../

# docker build -f .\servicecontrol.azurestoragequeues.dockerfile -t particular.azurecr.io/servicecontrol.azurestoragequeues ./../
# docker build -f .\servicecontrol.azurestoragequeues.audit.dockerfile -t particular.azurecr.io/servicecontrol.azurestoragequeues.audit ./../
# docker build -f .\servicecontrol.azurestoragequeues.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.azurestoragequeues.monitoring ./../

# docker build -f .\servicecontrol.sqlserver.dockerfile -t particular.azurecr.io/servicecontrol.sql ./../
# docker build -f .\servicecontrol.sqlserver.audit.dockerfile -t particular.azurecr.io/servicecontrol.sql.audit ./../
# docker build -f .\servicecontrol.sqlserver.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.sql.monitoring ./../

# docker build -f .\servicecontrol.amazonsqs.dockerfile -t particular.azurecr.io/servicecontrol.amazonsqs ./../
# docker build -f .\servicecontrol.amazonsqs.audit.dockerfile -t particular.azurecr.io/servicecontrol.amazonsqs.audit ./../
# docker build -f .\servicecontrol.amazonsqs.monitoring.dockerfile -t particular.azurecr.io/servicecontrol.amazonsqs.monitoring ./../
