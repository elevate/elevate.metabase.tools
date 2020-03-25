let pkgsMaster = (import (builtins.fetchTarball {
  url = "https://github.com/NixOS/nixpkgs/archive/0538dec989ed6556bdce0e8fe59e4f2ef184915f.tar.gz";
  sha256 = "1v2j9k5m35gnih2mqxkvpa6zl3v6q9bj4214nq0xj7r2c25hqi19";
})){};
in
{ pkgs ? import <nixpkgs> {} }:
pkgs.mkShell {
    buildInputs = [ 
      (pkgsMaster.dotnetCorePackages.combinePackages [ 
        pkgsMaster.dotnetCorePackages.sdk_2_1
        pkgsMaster.dotnetCorePackages.sdk_3_1 
      ])
    ];
}
