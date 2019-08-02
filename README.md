# .NET Core C# Worker (preview)

![Build Status](https://badge.buildkite.com/f426be96e9dc5ae832eccefdfedf4291a8bbd6c6ec3b8abce0.svg?branch=master) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

This is an empty base worker, ready for you to add your game's specific logic to it.
It is intended to be added to an existing SpatialOS project and as such, does not include SpatialOS configuration files and only an empty schema.

> This is a preview, and the APIs and other aspects of this project can and will change based on your feedback.

It is assumed that you are moderately familiar with the C# language, as well as SpatialOS and its concepts.

# High level

The C# worker APIs encourage the use of `Tasks` and other parallel constructs, so thread safety is a major concern.

To aid with this:

* Schema `types` and `components` are generated as `readonly` structs.
* `list<>` and `map<>` fields are represented by `ImmutableArray<>` and `ImmutableDictionary<>`.
* `option<>` fields are represented as `System.Nullable<>`.

There are also a few helpers available:

* `Improbable.Stdlib.ComponentCollection` is a thread-safe view of the worker's checked out entity components.
* `Improbable.Stdlib.WorkerConnection` is a thread-safe wrapper around the low-level SpatialOS `Connection` object. It enables the use of `async/await` constructs when sending SpatialOS command requests.
* `Improbable.Stdlib.OpList` is a wrapper around the low-level SpatialOS `OpList`. It enables the construction of LINQ queries, and include op-specific helpers like `OfOpType<>` and `OfComponent`.

## Machine setup

1. Install .NET Core v2.2.6 (SDK and Runtime) [from the download archive](https://dotnet.microsoft.com/download/dotnet-core/2.2) for your system.

## Project layout

* `CSharpCodeGenerator` - A schemalang-to-C# code generator. This can also be used as a basis for making a code generator for other languages.
* `GeneratedCode` - The output of the `CSharpCodeGenerator`.
* `Improbable` - The source for supporting libraries meant to be used by your worker. Once APIs have finalized, these will also be available on [nuget.org]
* `schema` - A placeholder, empty schema, so the project builds out of the box.
* `scripts` - Scripts that automate common and repetitive tasks.
* `Workers` - Your worker(s).

## Local setup

We currently build local Nuget packages of our supporting libraries.
The Nuget.config in the root directory allows nuget to resolve these packages from the local filesystem.

*Windows*
```powershell
scripts/build-nuget-packages.ps1
```

*macOS/Linux*
```bash
scripts/build-nuget-packages.sh
```

> You will need to re-run this if you make changes to the projects in the `Improbable` directory, or if you delete the `nupkgs` directory in the root of this project.

## Schema location

This project contains a simple blank schema file to ensure everything compiles immediately after you've checked it out.

To point the code generator to your project's schema, do the following:

1. Open `_root_directory_/Directory.Build.props`
2. Find the `SchemaInputDir` element. By default it looks like this:
```
<SchemaInputDir Include="$(SolutionDir)\schema" />
```
3. Change it to point to your project's `schema` directory, for example:
```
<SchemaInputDir Include="$(SolutionDir)\..\SpatialOS\schema" />
```
Note that depending on your target project, you may need more than one schema input directory. You may need to link to the standard schema library for example.
Once you've done this, you can delete the placeholder `schema` directory.


## Building the solution

* Open the solution in JetBrains Rider, Visual Studio, Visual Studio Code and select and build the  `x64` platform.
* Or, build directly using `dotnet build Workers.sln -p:Platform=x64`.

## Running the worker

If you want to explore the available command line options, you can run the worker:

`dotnet run -p Workers/GameLogic -- help`

## Building for the cloud

Build your managed workers for the cloud by running:

**Windows**

```powershell
`scripts/publish-linux-worker.ps1`
```

**macOS/Linux**

```bash
`scripts/publish-linux-worker.sh`
```

## How do I...

### View components that my worker has checked out?

`Improbable.Stdlib.ComponentCollection` provides a way to keep track of components that are visible to your worker.

Each type of component has a static `CreateComponentCollection` method.

`private readonly ComponentCollection<EntityAcl> acls = EntityAcl.CreateComponentCollection();`

Whenever you process an OpList, call `ProcessOpList` so the collection can keep up-to-date.

`acls.ProcessOpList(opList);`

Then access the component data using `Get` or `TryGet`.

`var component = acls.Get(entityId);`

### Send a component update?

Each component defines an `Update` type which can be used to build an update.
Call `update.ToSchemaUpdate()` to copy the data to SpatialOS.

```
var update = new DatabaseSyncService.Update();
update.AddPathsUpdatedEvent(new PathsUpdated(changedPaths));

DatabaseSyncService.SendUpdate(connection, tagretEntityId, update);
```

### Process specific types of ops?

`Improbable.Stdlib.OpList` allows for using LINQ methods to filter the ops that your worker is concerned with.

Process only `AddEntityOp`:
`var addEntityOps = opList.OfOpType<AddEntityOp>();`

Process only component updates for a specific component:
```var componentUpdates = opList
    .OfOpType<ComponentUpdateOp>()
    .OfComponent(EntityAcl.ComponentId);
```


### Send a command?

Each component that defines commands provides static methods of the form `Task<ResponseType> Send<CommandName>Async(WorkerConnection, RequestType, ...);`

```
var children = await DatabaseSyncService.SendGetItemsAsync(connection, new GetItemsRequest(profileId, GetItemDepth.Recursive, connection.WorkerId))
                    .ConfigureAwait(false);
```

### Respond to a command?

Each component that defines commands provides a `Commands` enum. You can use this with LINQ queries to filter to a specific command type.
```
var ops = opList
    .OfOpType<CommandRequestOp>()
    .OfComponent(DatabaseSyncService.ComponentId)
    .Where(op => DatabaseSyncService.GetCommandType(op) == DatabaseSyncService.Commands.Create);
```

Each component that defines commands provides static methods of the form `void Send<CommandName>Response(WorkerConnection, ResponseId, ResponseType, ...);`
```
DatabaseSyncService.SendGetItemsResponse(connection, commandRequestOp.RequestId, new GetItemsResponse(children));
```

### Send a command failure?

`connection.SendCommandFailure(commandRequestOp.RequestId, "Error message");`

### Know when a component has updated?

```
var updateOps = opList
    .OfOpType<ComponentUpdateOp>()
    .OfComponent(MyComponent.ComponentId)
    .Select(updateOp => updateOp.EntityId);

foreach(var entityId in updateOps)
{
    Log.Information("Updated entity {EntityId}", entityId);
}
```

### Know when a worker flag changes?

```
if (opList.TryGetWorkerFlagChange(key, ref newValue))
{
    flagValues[key] = newValue;
}
```

# Known issues

* Changes to `.schema` files won't always cause the `GeneratedCode` project to rebuild when open in Visual Studio. Manually "Rebuild" the `GeneratedCode` project to work around this.

[nuget.org]: https://nuget.org

# License

This software is licensed under MIT. See the [LICENSE](./LICENSE.md) file for details.

# Contributing

We currently don't accept PRs from external contributors - sorry about that! We do accept bug reports and feature requests in the form of issues, though.
