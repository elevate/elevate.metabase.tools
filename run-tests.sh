#!/usr/bin/env nix
#! nix shell .# github:nixos/nixpkgs/nixos-23.11#bash --command bash
set -e
# Which Metabase version to test against. Defaults to the value baked into the
# test suite; CI sets this from the workflow matrix so that every supported
# version is exercised.
if [ -n "${1:-}" ]; then
    export METABASE_VERSION="$1"
fi
echo "Testing against Metabase ${METABASE_VERSION:-<default>}"
dotnet test
