{
  description = "Export/Import Metabase questions/dashboard/collections";

  inputs = {
    flake-parts.url = "github:hercules-ci/flake-parts";
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    nuget2nix = {
      url = "github:mdarocha/nuget-packageslock2nix/main";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = inputs@{ flake-parts, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      imports = [
        # To import a flake module
        # 1. Add foo to inputs
        # 2. Add foo as a parameter to the outputs function
        # 3. Add here: foo.flakeModule

      ];
      systems = [ "x86_64-linux" "aarch64-linux" "aarch64-darwin" "x86_64-darwin" ];
      perSystem = { config, self', inputs', pkgs, system, ... }: {
        # Per-system attributes can be defined here. The self' and inputs'
        # module parameters provide easy access to attributes of the same
        # system.

        packages.default =
          pkgs.buildDotnetModule {
            pname = "metabase-exporter";
            version = "0.0.1";
            src = ./.;
            dotnet-sdk = pkgs.dotnet-sdk_8;
            dotnet-runtime = pkgs.dotnet-runtime_8;
            # testProjectFile = ./Tests/Tests.csproj;
            nugetDeps = inputs.nuget2nix.lib {
              inherit system;
              lockfiles = [
                ./metabase-exporter/packages.lock.json
                ./Tests/packages.lock.json
              ];
            };
          };

        devShells.default = pkgs.mkShell {
          buildInputs = [ 
            pkgs.dotnet-sdk_8
          ];
        };
      };
      flake = {
        # The usual flake attributes can be defined here, including system-
        # agnostic ones like nixosModule and system-enumerating ones, although
        # those are more easily expressed in perSystem.

      };
    };
}
