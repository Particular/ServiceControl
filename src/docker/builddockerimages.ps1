msbuild ../servicecontrol/ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\servicecontrol.azureservicebus-windows.dockerfile -t particular/servicecontrol/azuresericebus ./../
docker build -f .\servicecontrol.azureservicebus.init-windows.dockerfile -t particular/servicecontrol/azuresericebus/init ./../
docker build -f .\servicecontrol.azureservicebus.audit-windows.dockerfile -t particular/servicecontrol/azuresericebus/audit ./../
docker build -f .\servicecontrol.azureservicebus.audit.init-windows.dockerfile -t particular/servicecontrol/azuresericebus/audit/init ./../
docker build -f .\servicecontrol.azureservicebus.monitoring-windows.dockerfile -t particular/servicecontrol/azuresericebus/monitoring ./../
docker build -f .\servicecontrol.azureservicebus.monitoring.init-windows.dockerfile -t particular/servicecontrol/azuresericebus/monitoring/init ./../

# docker build -f .\servicecontrol.asb.endpoint -t particular/servicecontrol/asb/endpoint ./../
# docker build -f .\servicecontrol.asb.endpoint.init -t particular/servicecontrol/asb/endpoint.init ./../
# docker build -f .\servicecontrol.asb.endpoint.audit -t particular/servicecontrol/asb/endpoint.audit ./../
# docker build -f .\servicecontrol.asb.endpoint.audit.init -t particular/servicecontrol/asb/endpoint.audit.init ./../
# docker build -f .\servicecontrol.asb.endpoint.monitoring -t particular/servicecontrol/asb/endpoint.monitoring ./../
# docker build -f .\servicecontrol.asb.endpoint.monitoring.init -t particular/servicecontrol/asb/endpoint.monitoring.init ./../

# docker build -f .\servicecontrol.asb.forwarding -t particular/servicecontrol/asb/forwarding ./../
# docker build -f .\servicecontrol.asb.forwarding.init -t particular/servicecontrol/asb/forwarding.init ./../
# docker build -f .\servicecontrol.asb.forwarding.audit -t particular/servicecontrol/asb/forwarding.audit ./../
# docker build -f .\servicecontrol.asb.forwarding.audit.init -t particular/servicecontrol/asb/forwarding.audit.init ./../
# docker build -f .\servicecontrol.asb.forwarding.monitoring -t particular/servicecontrol/asb/forwarding.monitoring ./../
# docker build -f .\servicecontrol.asb.forwarding.monitoring.init -t particular/servicecontrol/asb/forwarding.monitoring.init ./../

docker build -f .\servicecontrol.rabbitmq.conventional-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional ./../
docker build -f .\servicecontrol.rabbitmq.conventional.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional.init ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional.audit ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional.audit.init ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional.monitoring ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/conventional.monitoring.init ./../

docker build -f .\servicecontrol.rabbitmq.direct-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct ./../
docker build -f .\servicecontrol.rabbitmq.direct.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct.init ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct.audit ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct.audit.init ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct.monitoring ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring.init-windows.dockerfile -t particular/servicecontrol/rabbitmq/direct.monitoring.init ./../

docker build -f .\servicecontrol.azurestoragequeues-windows.dockerfile -t particular/servicecontrol/azurestoragequeues ./../
docker build -f .\servicecontrol.azurestoragequeues.init-windows.dockerfile -t particular/servicecontrol/azurestoragequeues/init ./../
docker build -f .\servicecontrol.azurestoragequeues.audit-windows.dockerfile -t particular/servicecontrol/azurestoragequeues/audit ./../
docker build -f .\servicecontrol.azurestoragequeues.audit.init-windows.dockerfile -t particular/servicecontrol/azurestoragequeues/audit/init ./../
docker build -f .\servicecontrol.azurestoragequeues.monitoring-windows.dockerfile -t particular/servicecontrol/azurestoragequeues/monitoring ./../
docker build -f .\servicecontrol.azurestoragequeues.monitoring.init-windows.dockerfile -t particular/servicecontrol/azurestoragequeues/monitoring/init ./../

docker build -f .\servicecontrol.sqlserver-windows.dockerfile -t particular/servicecontrol/sql ./../
docker build -f .\servicecontrol.sqlserver.init-windows.dockerfile -t particular/servicecontrol/sql.init ./../
docker build -f .\servicecontrol.sqlserver.audit-windows.dockerfile -t particular/servicecontrol/sql.audit ./../
docker build -f .\servicecontrol.sqlserver.audit.init-windows.dockerfile -t particular/servicecontrol/sql.audit.init ./../
docker build -f .\servicecontrol.sqlserver.monitoring-windows.dockerfile -t particular/servicecontrol/sql.monitoring ./../
docker build -f .\servicecontrol.sqlserver.monitoring.init-windows.dockerfile -t particular/servicecontrol/sql.monitoring.init ./../

docker build -f .\servicecontrol.amazonsqs-windows.dockerfile -t particular/servicecontrol/amazonsqs ./../
docker build -f .\servicecontrol.amazonsqs.init-windows.dockerfile -t particular/servicecontrol/amazonsqs/init ./../
docker build -f .\servicecontrol.amazonsqs.audit-windows.dockerfile -t particular/servicecontrol/amazonsqs/audit ./../
docker build -f .\servicecontrol.amazonsqs.audit.init-windows.dockerfile -t particular/servicecontrol/amazonsqs/audit/init ./../
docker build -f .\servicecontrol.amazonsqs.monitoring-windows.dockerfile -t particular/servicecontrol/amazonsqs/monitoring ./../
docker build -f .\servicecontrol.amazonsqs.monitoring.init-windows.dockerfile -t particular/servicecontrol/amazonsqs/monitoring/init ./../