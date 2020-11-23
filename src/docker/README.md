# Docker

The Docker files in this folder follow the following naming convention:

```text
dockerfile.{transport}.{topology}.{service-control-instance-type}.{phase}
```

- `transport`: The name of the [transport](https://docs.particular.net/transports/).
- `topology`: The name of the topology used. For those transports which support only a single topology, the name is `default`.
- `service-control-instance-type`: `audit`, `main`, or `monitoring`.
- `phase`: `init` or `run`.
