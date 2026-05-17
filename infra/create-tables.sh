#!/bin/bash

# Create all required Azure Table Storage tables for the Happie application.
# This script is idempotent — az storage table create succeeds if the table already exists.
#
# Usage: ./create-tables.sh <storage-account-name>

set -euo pipefail

if [ -z "${1:-}" ]; then
    echo "Usage: $0 <storage-account-name>"
    exit 1
fi

STORAGE_ACCOUNT="$1"

TABLES=(
    "Households"
    "Housemates"
    "AttendanceRecords"
    "DishRecords"
    "Comments"
    "DayHistory"
    "PushSubscriptions"
)

echo "Creating tables in Storage Account: $STORAGE_ACCOUNT"

for TABLE in "${TABLES[@]}"; do
    echo "  Creating table: $TABLE"
    az storage table create \
        --name "$TABLE" \
        --account-name "$STORAGE_ACCOUNT" \
        --output none
done

echo "All tables created successfully."
