ARG BASETAG
ARG VERSION
FROM ravendb/ravendb:$BASETAG AS ravendb

COPY src/ServiceControl.RavenDB/RavenLicense.json /opt/RavenDB/servicecontrol-license.json

ENV RAVEN_License_Eula_Accepted=true \
    RAVEN_License_Path=/opt/RavenDB/servicecontrol-license.json \
    RAVEN_Setup_Mode=None

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.RavenDB \
      org.opencontainers.image.description="ServiceControl database storage using RavenDB"
