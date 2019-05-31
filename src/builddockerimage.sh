msbuild ServiceControl.sln //t:Build //p:Configuration=Release

docker build -f .\dockerfile.asb -t servicecontrolasb .
docker build -f .\dockerfile.rabbitmq.conventional -t servicecontrolrabbitconventional .