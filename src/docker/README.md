# ServiceControl docker files

This folder contains all the Docker files required to build all ServiceControl container images for all the supported transports. For each transports there are 6 `.dockerfile` files. One for the main ServiceControl instance, one for ServiceControl.Audit instances, and one for ServiceControl.Monitoring instances. Each one has a corresponding `*.init-windows.dockerfile` to initialize the environment.

To build all the container images at once, run the `builddockerimages.ps1` PowerShell script.

NOTE: The script is provided to ease development stages only. To run container images in production refer to the ones available on Docker Hub.
