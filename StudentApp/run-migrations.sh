#!/bin/bash
# run-migrations.sh
# With Dapper there are NO migration files.
# The DatabaseInitializer class runs automatically at API startup and:
#   1. Creates the database (StudentAppDb) if it doesn't exist
#   2. Creates the Students table if it doesn't exist
#   3. Seeds 3 sample students if the table is empty
#
# To reset the database completely, run:
#   docker-compose down -v          (removes volumes including SQL data)
#   docker-compose up -d            (fresh start, DB created on first API boot)
echo "No migrations needed — Dapper project uses DatabaseInitializer at startup."
echo "Just run: docker-compose up -d"
