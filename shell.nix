with (import (builtins.fetchTarball {
  url = "https://github.com/NixOS/nixpkgs/archive/5fd8536a9a5932d4ae8de52b7dc08d92041237fc.tar.gz";
  sha256 = "0hyfifrhzxsdjj80sh5fpwcgm6zq5vx6ilh0lvp2dw6fzay1vrd0";
})){};
mkShell {
    buildInputs = [ 
      dotnet-sdk_8
    ];
}
