# ServiceControl on Docker

The sample allows to (partially) test the SC docker images locally. 

## Issues

### host.docker.internal not working 

The `.env` file contains a `CONNECTIONSTRING` item with the value `host.docker.internal`. This value does not seem to work that needs to be set to the IP address of the host. the 

### Host cannot be accessed

Disable the Windows Firewall if the Windows container cannot access the Linux container

## Setup

1. Run the `setup.bat` scripts `rabbitmq` folder to setup a local rabbitmq Linux container 
1. Run the `setup.bat` scripts `servicecontrol` folder to setup a servicecontrol primary, audit and monitoring instance in 3 Windows container using the RabbitMQ transport

