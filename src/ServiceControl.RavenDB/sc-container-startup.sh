#!/bin/bash

LEGACY_PATH="/opt/RavenDB/Server/RavenData"

if [[ -d "$LEGACY_PATH" ]]; then
  echo "ERROR: RavenDB data is being mounted to the wrong location. Refer to the article 'Upgrade ServiceControl from Version 5 to Version 6' in the documentation for steps that must be taken to update ServiceControl to this version."
  exit 1
fi

chmod -R u+rwX $RAVEN_DataDir 2> /dev/null

if [[ $? -ne 0 ]]; then
  echo "ERROR: RavenDB data permissions are not correct. Refer to the article 'Upgrade ServiceControl from Version 5 to Version 6' in the documentation for steps that must be taken to update ServiceControl to this version."
  exit 1
fi

source /usr/lib/ravendb/scripts/run-raven.sh
