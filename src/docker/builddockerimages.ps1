msbuild ../ServiceControl.sln /t:Build /p:Configuration=Release

docker build -f .\dockerfile.asb.default.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.default.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.default.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.default.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.default.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb.default.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.asb-legacy.endpoint.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.endpoint.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.endpoint.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.endpoint.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.endpoint.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.endpoint.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.asb-legacy.forwarding.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.forwarding.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.forwarding.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.forwarding.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.forwarding.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asb-legacy.forwarding.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.asq.default.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asq.default.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asq.default.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asq.default.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asq.default.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.asq.default.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.rabbitmq.conventional.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.conventional.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.conventional.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.conventional.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.conventional.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.rabbitmq.direct.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.direct.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.direct.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.direct.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.direct.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.rabbitmq.direct.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.sqlserver.default.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqlserver.default.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqlserver.default.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqlserver.default.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqlserver.default.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqlserver.default.monitoring.run -t particular/servicecontrolasbs ./../

docker build -f .\dockerfile.sqs.default.audit.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqs.default.audit.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqs.default.main.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqs.default.main.run -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqs.default.monitoring.init -t particular/servicecontrolasbs ./../
docker build -f .\dockerfile.sqs.default.monitoring.run -t particular/servicecontrolasbs ./../
