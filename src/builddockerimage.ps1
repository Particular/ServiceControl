msbuild ServiceControl.sln //t:Build //p:Configuration=Release

docker build -f .\dockerfile.asb.endpoint -t servicecontrolasbendpoint .
docker build -f .\dockerfile.asb.forwarding -t servicecontrolasbforwarding .
docker build -f .\dockerfile.asb.asbs -t servicecontrolasbs .

docker build -f .\dockerfile.rabbitmq.conventional -t servicecontrolrabbitconventional .
docker build -f .\dockerfile.rabbitmq.direct -t servicecontrolrabbitdirect .

docker build -f .\dockerfile.asq -t servicecontrolasq .

docker build -f .\dockerfile.sql -t servicecontrolsql .

docker build -f .\dockerfile.msmq -t servicecontrolmsmq .