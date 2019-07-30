msbuild ServiceControl.sln //t:Build //p:Configuration=Release

docker build -f .\dockerfile.asb.endpoint -t particular/servicecontrolasbendpoint .
docker build -f .\dockerfile.asb.forwarding -t particular/servicecontrolasbforwarding .
docker build -f .\dockerfile.asb.asbs -t particular/servicecontrolasbs .

docker build -f .\dockerfile.rabbitmq.conventional -t particular/servicecontrolrabbitconventional .
docker build -f .\dockerfile.rabbitmq.direct -t particular/servicecontrolrabbitdirect .

docker build -f .\dockerfile.asq -t particular/servicecontrolasq .

docker build -f .\dockerfile.sqlserver -t particular/servicecontrolsql .

docker build -f .\dockerfile.msmq -t particular/servicecontrolmsmq .