#!/bin/bash
set -euo pipefail

LEGACY_PATH="/opt/RavenDB/Server/RavenData"
NEW_PATH="$RAVEN_DataDir"

if [[ -d "$LEGACY_PATH" ]]; then
  echo "RavenDB data is being mounted to the wrong location. See the upgrade guide for details on how to fix this."
  exit 1
fi

FILECOUNT = find $NEW_PATH \! -writable -print0 | grep -zc .

if [[ $FILECOUNT -ne 0 ]]; then
  echo "RavenDB data is not writable by the ravendb user. See the upgrade guide for details on how to fix this."
  exit 1
fi

FILECOUNT = find $NEW_PATH \! -readable -print0 | grep -zc .

if [[ $FILECOUNT -ne 0 ]]; then
  echo "RavenDB data is not readable by the ravendb user. See the upgrade guide for details on how to fix this."
  exit 1
fi

/usr/lib/ravendb/scripts/run-raven.sh
