msbuild ../ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\dockerfile.asb.asbs -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.asbs.init -t particular/servicecontrolasbs.init ./../
docker build -f .\dockerfile.asb.asbs.audit -t particular/servicecontrolasbs.audit ./../
docker build -f .\dockerfile.asb.asbs.audit.init -t particular/servicecontrolasbs.audit.init ./../
docker build -f .\dockerfile.asb.asbs.monitoring -t particular/servicecontrolasbs.monitoring ./../
docker build -f .\dockerfile.asb.asbs.monitoring.init -t particular/servicecontrolasbs.monitoring.init ./../

docker build -f .\dockerfile.asb.endpoint -t particular/servicecontrolasbendpoint ./../
docker build -f .\dockerfile.asb.endpoint.init -t particular/servicecontrolasbendpoint.init ./../
docker build -f .\dockerfile.asb.endpoint.audit -t particular/servicecontrolasbendpoint.audit ./../
docker build -f .\dockerfile.asb.endpoint.audit.init -t particular/servicecontrolasbendpoint.audit.init ./../
docker build -f .\dockerfile.asb.endpoint.monitoring -t particular/servicecontrolasbendpoint.monitoring ./../
docker build -f .\dockerfile.asb.endpoint.monitoring.init -t particular/servicecontrolasbendpoint.monitoring.init ./../

docker build -f .\dockerfile.asb.forwarding -t particular/servicecontrolasbforwarding ./../
docker build -f .\dockerfile.asb.forwarding.init -t particular/servicecontrolasbforwarding.init ./../
docker build -f .\dockerfile.asb.forwarding.audit -t particular/servicecontrolasbforwarding.audit ./../
docker build -f .\dockerfile.asb.forwarding.audit.init -t particular/servicecontrolasbforwarding.audit.init ./../
docker build -f .\dockerfile.asb.forwarding.monitoring -t particular/servicecontrolasbforwarding.monitoring ./../
docker build -f .\dockerfile.asb.forwarding.monitoring.init -t particular/servicecontrolasbforwarding.monitoring.init ./../

docker build -f .\dockerfile.rabbitmq.conventional -t particular/servicecontrolrabbitconventional ./../
docker build -f .\dockerfile.rabbitmq.conventional.init -t particular/servicecontrolrabbitconventional.init ./../
docker build -f .\dockerfile.rabbitmq.conventional.audit -t particular/servicecontrolrabbitconventional.audit ./../
docker build -f .\dockerfile.rabbitmq.conventional.audit.init -t particular/servicecontrolrabbitconventional.audit.init ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring -t particular/servicecontrolrabbitconventional.monitoring ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring.init -t particular/servicecontrolrabbitconventional.monitoring.init ./../

docker build -f .\dockerfile.rabbitmq.direct -t particular/servicecontrolrabbitdirect ./../
docker build -f .\dockerfile.rabbitmq.direct.init -t particular/servicecontrolrabbitdirect.init ./../
docker build -f .\dockerfile.rabbitmq.direct.audit -t particular/servicecontrolrabbitdirect.audit ./../
docker build -f .\dockerfile.rabbitmq.direct.audit.init -t particular/servicecontrolrabbitdirect.audit.init ./../
docker build -f .\dockerfile.rabbitmq.direct.monitoring -t particular/servicecontrolrabbitdirect.monitoring ./../
docker build -f .\dockerfile.rabbitmq.direct.monitoring.init -t particular/servicecontrolrabbitdirect.monitoring.init ./../

docker build -f .\dockerfile.asq -t particular/servicecontrolasq ./../
docker build -f .\dockerfile.asq.init -t particular/servicecontrolasq.init ./../
docker build -f .\dockerfile.asq.audit -t particular/servicecontrolasq.audit ./../
docker build -f .\dockerfile.asq.audit.init -t particular/servicecontrolasq.audit.init ./../
docker build -f .\dockerfile.asq.monitoring -t particular/servicecontrolasq.monitoring ./../
docker build -f .\dockerfile.asq.monitoring.init -t particular/servicecontrolasq.monitoring.init ./../

docker build -f .\dockerfile.sqlserver -t particular/servicecontrolsql ./../
docker build -f .\dockerfile.sqlserver.init -t particular/servicecontrolsql.init ./../
docker build -f .\dockerfile.sqlserver.audit -t particular/servicecontrolsql.audit ./../
docker build -f .\dockerfile.sqlserver.audit.init -t particular/servicecontrolsql.audit.init ./../
docker build -f .\dockerfile.sqlserver.monitoring -t particular/servicecontrolsql.monitoring ./../
docker build -f .\dockerfile.sqlserver.monitoring.init -t particular/servicecontrolsql.monitoring.init ./../

docker build -f .\dockerfile.sqs -t particular/servicecontrolsqs ./../
docker build -f .\dockerfile.sqs.init -t particular/servicecontrolsqs.init ./../
docker build -f .\dockerfile.sqs.audit -t particular/servicecontrolsqs.audit ./../
docker build -f .\dockerfile.sqs.audit.init -t particular/servicecontrolsqs.audit.init ./../
docker build -f .\dockerfile.sqs.monitoring -t particular/servicecontrolsqs.monitoring ./../
docker build -f .\dockerfile.sqs.monitoring.init -t particular/servicecontrolsqs.monitoring.init ./../