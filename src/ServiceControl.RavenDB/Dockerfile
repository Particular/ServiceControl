ARG BASETAG
ARG VERSION
FROM ravendb/ravendb:$BASETAG AS ravendb

COPY src/ServiceControl.RavenDB/RavenLicense.json /usr/lib/ravendb/servicecontrol-license.json
COPY src/ServiceControl.RavenDB/sc-container-startup.sh /usr/lib/ravendb/scripts/

USER root
RUN chown 999:999 /var/lib/ravendb/data
USER ravendb

ENV RAVEN_License_Eula_Accepted=true \
    RAVEN_Setup_Mode=None

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.RavenDB \
      org.opencontainers.image.description="ServiceControl database storage using RavenDB"

CMD [ "/bin/bash", "/usr/lib/ravendb/scripts/sc-container-startup.sh" ]
