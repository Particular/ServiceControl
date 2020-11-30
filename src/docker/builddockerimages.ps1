msbuild ../servicecontrol/ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\servicecontrol.asb.asbs -t particular/servicecontrol/asbs ./../
docker build -f .\servicecontrol.asb.asbs.init -t particular/servicecontrol/asbs.init ./../
docker build -f .\servicecontrol.asb.asbs.audit -t particular/servicecontrol/asbs.audit ./../
docker build -f .\servicecontrol.asb.asbs.audit.init -t particular/servicecontrol/asbs.audit.init ./../
docker build -f .\servicecontrol.asb.asbs.monitoring -t particular/servicecontrol/asbs.monitoring ./../
docker build -f .\servicecontrol.asb.asbs.monitoring.init -t particular/servicecontrol/asbs.monitoring.init ./../

docker build -f .\servicecontrol.asb.endpoint -t particular/servicecontrol/asb/endpoint ./../
docker build -f .\servicecontrol.asb.endpoint.init -t particular/servicecontrol/asb/endpoint.init ./../
docker build -f .\servicecontrol.asb.endpoint.audit -t particular/servicecontrol/asb/endpoint.audit ./../
docker build -f .\servicecontrol.asb.endpoint.audit.init -t particular/servicecontrol/asb/endpoint.audit.init ./../
docker build -f .\servicecontrol.asb.endpoint.monitoring -t particular/servicecontrol/asb/endpoint.monitoring ./../
docker build -f .\servicecontrol.asb.endpoint.monitoring.init -t particular/servicecontrol/asb/endpoint.monitoring.init ./../

docker build -f .\servicecontrol.asb.forwarding -t particular/servicecontrol/asb/forwarding ./../
docker build -f .\servicecontrol.asb.forwarding.init -t particular/servicecontrol/asb/forwarding.init ./../
docker build -f .\servicecontrol.asb.forwarding.audit -t particular/servicecontrol/asb/forwarding.audit ./../
docker build -f .\servicecontrol.asb.forwarding.audit.init -t particular/servicecontrol/asb/forwarding.audit.init ./../
docker build -f .\servicecontrol.asb.forwarding.monitoring -t particular/servicecontrol/asb/forwarding.monitoring ./../
docker build -f .\servicecontrol.asb.forwarding.monitoring.init -t particular/servicecontrol/asb/forwarding.monitoring.init ./../

docker build -f .\servicecontrol.rabbitmq.conventional -t particular/servicecontrol/rabbitmq/conventional ./../
docker build -f .\servicecontrol.rabbitmq.conventional.init -t particular/servicecontrol/rabbitmq/conventional.init ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit -t particular/servicecontrol/rabbitmq/conventional.audit ./../
docker build -f .\servicecontrol.rabbitmq.conventional.audit.init -t particular/servicecontrol/rabbitmq/conventional.audit.init ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring -t particular/servicecontrol/rabbitmq/conventional.monitoring ./../
docker build -f .\servicecontrol.rabbitmq.conventional.monitoring.init -t particular/servicecontrol/rabbitmq/conventional.monitoring.init ./../

docker build -f .\servicecontrol.rabbitmq.direct -t particular/servicecontrol/rabbitmq/direct ./../
docker build -f .\servicecontrol.rabbitmq.direct.init -t particular/servicecontrol/rabbitmq/direct.init ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit -t particular/servicecontrol/rabbitmq/direct.audit ./../
docker build -f .\servicecontrol.rabbitmq.direct.audit.init -t particular/servicecontrol/rabbitmq/direct.audit.init ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring -t particular/servicecontrol/rabbitmq/direct.monitoring ./../
docker build -f .\servicecontrol.rabbitmq.direct.monitoring.init -t particular/servicecontrol/rabbitmq/direct.monitoring.init ./../

docker build -f .\servicecontrol.asq -t particular/servicecontrol/asq ./../
docker build -f .\servicecontrol.asq.init -t particular/servicecontrol/asq.init ./../
docker build -f .\servicecontrol.asq.audit -t particular/servicecontrol/asq.audit ./../
docker build -f .\servicecontrol.asq.audit.init -t particular/servicecontrol/asq.audit.init ./../
docker build -f .\servicecontrol.asq.monitoring -t particular/servicecontrol/asq.monitoring ./../
docker build -f .\servicecontrol.asq.monitoring.init -t particular/servicecontrol/asq.monitoring.init ./../

docker build -f .\servicecontrol.sqlserver -t particular/servicecontrol/sql ./../
docker build -f .\servicecontrol.sqlserver.init -t particular/servicecontrol/sql.init ./../
docker build -f .\servicecontrol.sqlserver.audit -t particular/servicecontrol/sql.audit ./../
docker build -f .\servicecontrol.sqlserver.audit.init -t particular/servicecontrol/sql.audit.init ./../
docker build -f .\servicecontrol.sqlserver.monitoring -t particular/servicecontrol/sql.monitoring ./../
docker build -f .\servicecontrol.sqlserver.monitoring.init -t particular/servicecontrol/sql.monitoring.init ./../

docker build -f .\servicecontrol.sqs -t particular/servicecontrol/sqs ./../
docker build -f .\servicecontrol.sqs.init -t particular/servicecontrol/sqs.init ./../
docker build -f .\servicecontrol.sqs.audit -t particular/servicecontrol/sqs.audit ./../
docker build -f .\servicecontrol.sqs.audit.init -t particular/servicecontrol/sqs.audit.init ./../
docker build -f .\servicecontrol.sqs.monitoring -t particular/servicecontrol/sqs.monitoring ./../
docker build -f .\servicecontrol.sqs.monitoring.init -t particular/servicecontrol/sqs.monitoring.init ./../