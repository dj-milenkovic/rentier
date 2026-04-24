# Contracts: NuGet Central Package Management

**Feature**: 041-nuget-central-package-management  
**Date**: 2025-07-15

## External Contracts

This feature does not introduce, modify, or remove any external-facing contracts:

- **No API changes**: No endpoints, methods, or interfaces are added or modified.
- **No UI changes**: No views, view models, or user interactions are affected.
- **No data format changes**: No serialization formats, database schemas, or file formats change.
- **No inter-process contracts**: No IPC, messaging, or network protocols are affected.

## Internal Build Contract

The only "contract" introduced is between NuGet's MSBuild integration and the project files:

| Contract | Producer | Consumer |
|----------|----------|----------|
| `Directory.Packages.props` | Developer (this feature) | NuGet restore (`dotnet restore`) |
| `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` | `Directory.Packages.props` | NuGet SDK targets |
| `<PackageVersion Include="..." Version="..." />` | `Directory.Packages.props` | All .csproj files via `<PackageReference>` |

This is a standard NuGet convention, not a custom contract.
