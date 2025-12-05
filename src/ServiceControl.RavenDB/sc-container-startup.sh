#!/bin/bash

LEGACY_PATH="/opt/RavenDB/Server/RavenData"

if [[ -d "$LEGACY_PATH" ]]; then
  echo "ERROR: RavenDB data is being mounted to the wrong location. It should be mounted to $RAVEN_DataDir. The owner and group needs to be set to ID 999. Refer to the article 'Upgrade ServiceControl from Version 5 to Version 6' in the documentation for more details about the steps that must be taken to update ServiceControl to this version."
  exit 1
fi

source /usr/lib/ravendb/scripts/run-raven.sh &
RAVEN_PID=$!

# Wait for RavenDB to be ready (with retries)
echo "Waiting for RavenDB to start..."
for i in {1..30}; do
  if curl -s http://localhost:8080/admin/stats > /dev/null 2>&1; then
    echo "RavenDB is ready!"
    break
  fi
  sleep 1
done

# Register license
echo "Registering license..."
LICENSE_JSON=$(cat /usr/lib/ravendb/servicecontrol-license.json)
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST http://localhost:8080/admin/license/activate \
  -H "Content-Type: application/json" \
  -d "$LICENSE_JSON")

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
if [ "$HTTP_CODE" -eq 200 ] || [ "$HTTP_CODE" -eq 204 ]; then
  echo "License registered successfully!"
else
  echo "Warning: License registration returned HTTP $HTTP_CODE"
fi

wait $RAVEN_PID