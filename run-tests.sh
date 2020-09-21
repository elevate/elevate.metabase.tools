#!/usr/bin/env sh
set -e
alias compose="docker-compose -f docker-compose-tests.yml"
compose run --rm start_dependencies
trap "compose down" EXIT
sleep 5 # wait for metabase to create its tables
compose up add_user
compose build
compose up test_export
compose up test_import

