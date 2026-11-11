#!/bin/bash

LEGACY_PATH="/opt/RavenDB/Server/RavenData"

if [[ -d "$LEGACY_PATH" ]]; then
  echo "ERROR: RavenDB data is being mounted to the wrong location. It should be mounted to $RAVEN_DataDir. The owner and group needs to be set to ID 999. Refer to the article 'Upgrade ServiceControl from Version 5 to Version 6' in the documentation for more details about the steps that must be taken to update ServiceControl to this version."
  exit 1
fi

# RavenDB 7 converts RavenDB 6 data one-way, so each image variant checks the data directory first.
# The acknowledgement variable avoids the RAVEN_ prefix because RavenDB reads those variables as its own settings.
DATA_DIR="${RAVEN_DataDir:-/var/lib/ravendb/data}"
MARKER_FILE="$DATA_DIR/servicecontrol-ravendb7.marker"

fail() {
  echo "ERROR: $1" >&2
  shift
  for line in "$@"; do
    echo "$line" >&2
  done
  exit 1
}

require_access() {
  if [[ -d "$1" ]] && { [[ ! -r "$1" ]] || [[ ! -x "$1" ]]; }; then
    fail "Cannot read $1. The owner and group of the RavenDB data directory needs to be set to ID 999."
  fi
}

has_databases() {
  [[ -d "$DATA_DIR/Databases" ]] && [[ -n "$(find "$DATA_DIR/Databases" -mindepth 1 -maxdepth 1 -type d -print -quit)" ]]
}

write_marker() {
  mkdir -p "$DATA_DIR" 2>/dev/null
  if ! printf 'This RavenDB data directory belongs to RavenDB 7. Do not delete this file.\nWritten by servicecontrol-ravendb on %s.\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$MARKER_FILE" 2>/dev/null; then
    fail "Cannot write $MARKER_FILE. The owner and group of the RavenDB data directory needs to be set to ID 999, and the directory must be writable."
  fi
}

require_access "$DATA_DIR"
require_access "$DATA_DIR/Databases"

case "$SERVICECONTROL_RAVENDB_MAJOR" in
  7)
    if [[ ! -e "$MARKER_FILE" ]]; then
      if has_databases; then
        if [[ "${RAVENDB7_ACCEPT_STORAGE_CONVERSION,,}" != "true" ]]; then
          fail "The RavenDB data directory $DATA_DIR contains databases that were written by an earlier version of RavenDB." \
            "This image runs RavenDB 7. Starting it converts the data one-way: RavenDB 6 cannot open the data afterwards." \
            "On a large database the conversion can take hours or days, and ServiceControl is unavailable until it finishes." \
            "Choose one of the following:" \
            "  * Stay on RavenDB 6: use the same image tag with the -ravendb6 suffix (for example latest-ravendb6), and set PersistenceType=RavenDB on the ServiceControl and ServiceControl.Audit containers." \
            "  * Convert to RavenDB 7: back up the data, then set RAVENDB7_ACCEPT_STORAGE_CONVERSION=true on this container."
        fi
        echo "WARNING: RAVENDB7_ACCEPT_STORAGE_CONVERSION=true. RavenDB 7 converts the databases in $DATA_DIR one-way. This can take hours or days, and ServiceControl is unavailable until it finishes."
      fi
      write_marker
    fi
    ;;
  6)
    if [[ -e "$MARKER_FILE" ]]; then
      fail "The RavenDB data directory $DATA_DIR has been converted to RavenDB 7, and RavenDB 6 cannot open it." \
        "Use the default tag of this image (without the -ravendb6 suffix) and set PersistenceType=RavenDB7 on the ServiceControl and ServiceControl.Audit containers." \
        "To return to RavenDB 6, restore a backup taken before the conversion."
    fi
    ;;
  *)
    fail "The RavenDB major version of this image is unknown. Build the image with the RAVENDBMAJOR build argument set to 6 or 7."
    ;;
esac

source /usr/lib/ravendb/scripts/run-raven.sh
