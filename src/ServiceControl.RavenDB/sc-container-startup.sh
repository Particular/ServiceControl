#!/bin/bash
set -euo pipefail

echo "Checking RavenDB data for proper migration to RavenDB 6"

LEGACY_PATH="/opt/RavenDB/Server/RavenData"

if [[ -d "$LEGACY_PATH" ]]; then
  echo "RavenDB data is being mounted to the wrong location. See the upgrade guide for details on how to fix this."
  exit 1
fi

FILECOUNT="$(find "$RAVEN_DataDir" \! -writable -printf '.' | wc -c)"

if [[ $FILECOUNT -ne 0 ]]; then
  echo "RavenDB data is not writable by the ravendb user. See the upgrade guide for details on how to fix this."
  exit 1
fi

FILECOUNT="$(find "$RAVEN_DataDir" \! -readable -printf '.' | wc -c)"

if [[ $FILECOUNT -ne 0 ]]; then
  echo "RavenDB data is not readable by the ravendb user. See the upgrade guide for details on how to fix this."
  exit 1
fi

echo "Starting RavenDB"

/usr/lib/ravendb/scripts/run-raven.sh
