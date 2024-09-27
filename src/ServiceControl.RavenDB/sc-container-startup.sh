#!/bin/bash
set -euo pipefail

LEGACY_PATH="/opt/RavenDB/Server/RavenData"
NEW_PATH="$RAVEN_DataDir"

if [ -d "$LEGACY_PATH" ]; then
  echo "Unmigrated data detected. See the upgrade guide for details on how to fix this."
  exit 1
fi

read -r permissions username groupname <<<$(stat -c "%a %U %G" $NEW_PATH)

if [[ $permissions =~ ..[0-5] ]] && { [[ $groupname != "ravendb" ]] || [[ $permissions =~ .[0-5]. ]]; } && { [[ $username != "ravendb" ]] || [[ $permissions =~ [0-5].. ]]; } then
  echo "The permissions of the mounted data are not correct. See the upgrade guide for details on how to fix this."
  exit 1
fi

/usr/lib/ravendb/scripts/run-raven.sh
