#!/usr/bin/env nix
#! nix shell .# github:nixos/nixpkgs/nixos-23.11#bash --command bash
set -e
dotnet test
