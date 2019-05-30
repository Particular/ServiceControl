msbuild ServiceControl.sln //t:Build //p:Configuration=Release

docker build -f .\dockerfile.asb -t servicecontrolasb .