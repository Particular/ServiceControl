 # ServiceControl.DockerImages

WARNING: This project is not automatically built when building the solution to keep the overall build time under control.

To build Docker images explicitly build this project.

## Notes

* Each `ADD` or `ENV` statement creates an additional layer in the Docker container, so these statements should be combined into one and not split out.
