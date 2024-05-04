with (import (builtins.fetchTarball {
  url = "https://github.com/NixOS/nixpkgs/archive/808c0d8c53c7ae50f82aca8e7df263225cf235bf.tar.gz";
  sha256 = "1kgk5jqc93kr180r6k32q1n0l9xk8vwji72i1zc2ijja61cgdvmh";
})){};
mkShell {
    buildInputs = [ 
      dotnet-sdk_6
    ];
}
