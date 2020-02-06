`Improbable.WorkerSDKInterop` is isolated into its own solution so that it can be built first, in isolation.
The project build process downloads the Worker CInterop dll, which introduces ordering problems when the project reference is built via the normal MSBuild dependency rules.
This is because the Worker CInterop dll isn't recognized at build time (since it's downloaded AFTER those files are discovered). This then causes errors for dependent projects.

Since this setup is an interim solution before we start publishing to nuget.org, this slightly kludgy approach is good enough to enable a reasonable workflow.
