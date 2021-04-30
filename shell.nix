with (import (builtins.fetchTarball {
  url = "https://github.com/NixOS/nixpkgs/archive/ff13163e3fd5283d997d11fac04061f243d93f7c.tar.gz";
  sha256 = "0d1pn4r8mlyz6mndik6dw4m6h12nv04vkv79r0zkcaqgab5x9170";
})){};
mkShell {
    buildInputs = [ 
      dotnet-sdk_5
      docker-compose
    ];
}
