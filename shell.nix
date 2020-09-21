with (import (builtins.fetchTarball {
  url = "https://github.com/NixOS/nixpkgs/archive/0538dec989ed6556bdce0e8fe59e4f2ef184915f.tar.gz";
  sha256 = "048fvyw4hdhsmjiwsbv3wijbzfrgdi5zjizkal3gm8ib79p5zxsz";
})){};
mkShell {
    buildInputs = [ 
      (dotnetCorePackages.combinePackages [ 
        dotnetCorePackages.sdk_2_1
        dotnetCorePackages.sdk_3_1 
      ])
      docker-compose
    ];
}
