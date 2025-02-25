# Metabase-exporter

Tools for managing [Metabase](https://www.metabase.com/) .

Used to export/import the current state of Metabase (questions/dashboards/collections) to/from a JSON file. 
This file can then be stored on git for versioning.

To use this [install Nix](https://github.com/DeterminateSystems/nix-installer?tab=readme-ov-file#the-determinate-nix-installer) and then run it with e.g. `nix run github:elevate/elevate.metabase.tools -- Command=Import ...`

It's recommended that you pin a specific commit for reliability e.g. `nix run github:elevate/elevate.metabase.tools/09bc3ea332f82f8b5b32d730e4de262a0dd2c3fe -- Command=Import ...`

If you're on Windows or want to develop, install the .NET 8 SDK and run it from the `metabase-exporter` with e.g. `dotnet run -- Command=Import ...`

See usage flags below.

## Limitations

This tool currently exports/imports collections, dashboards and SQL-defined questions only. 
Other kinds of questions are not supported because they are hard to make portable across Metabase instances.
Other features (e.g. pulses) are not supported simply because I don't need them :)

## Usage

Parameters can be given via [command-line, appsettings.json or environment variables](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers).

For all operations the Metabase API settings must be configured. E.g. in appsettings.json:

```json
{
  "MetabaseApi": {
    "Url": "https://localhost:32443",
    "Username": "mauricio@elevatedirect.com",
    "Password":  "123456789"
  }
}
```

### Export

This exports the state to a file. Sample usage:

`metabase-exporter.exe Command=export OutputFilename=metabase-state.json`

You can optionally exclude personal collections by setting `ExcludePersonalCollections=1`

### Import

This imports a state file into a Metabase instance. Sample usage:

`metabase-exporter.exe Command=import InputFilename=metabase-state.json DatabaseMapping:1=2 DatabaseMapping:2=3`

The `DatabaseMapping` settings map Metabase database IDs in the state file to database IDs in the target Metabase instance.
In the example above, it maps:
* the database ID 1 in the file to the database ID 2 in the target Metabase instance
* the database ID 2 in the file to the database ID 3 in the target Metabase instance

### Test questions

This runs the queries in all Metabase Questions/Cards. Use it to test that the queries (i.e. SQL) are valid.

`metabase-exporter.exe Command=test-questions`
