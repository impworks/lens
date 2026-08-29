How to publish a release
========================

The `Release` workflow (`.github/workflows/release.yml`) builds the artifacts,
pushes the NuGet package and leaves a tagged GitHub release with everything
attached. It only ever runs when someone starts it from the Actions tab - not on
a push, and not on a tag.

The marketplace listings are updated by hand, by downloading the files from the
release and uploading them. Building a release is cheap and repeatable; putting
one in front of the public is neither, and a marketplace upload cannot be taken
back.

The Rider plugin is the exception: it is not built by the workflow at all, and
has to be built locally and attached to the release by hand - see
[The Rider plugin](#the-rider-plugin).

Versioning
----------

`Directory.Build.props` holds the release series and nothing else:

```xml
<VersionSeries>5.0</VersionSeries>
```

The number after it is not written down anywhere. The workflow looks at the
tags that already exist, takes the highest one within the series and adds one;
if the series has no tags yet, it starts at `.0`. The tag is created at the very
end of the run, so a version number is never spent on a release that failed to
build.

To restart the numbering, bump the series by hand:

```xml
<VersionSeries>5.1</VersionSeries>
```

The next release then finds no `5.1.*` tag and produces `5.1.0`.

A local build gets `<series>.0`, which is what the .NET projects use when
nothing overrides them.

Running a release
-----------------

From the Actions tab, run `Release`. Both inputs are optional:

| Input     | Default           | Meaning                                                     |
|-----------|-------------------|-------------------------------------------------------------|
| `version` | next unused number | Release this exact version instead of the computed one.     |
| `dry_run` | `false`           | Build everything and attach it to the run, but create no tag, no release, and push nothing. |

A run builds, in parallel:

* the `LENS` NuGet package
* `LensLang-VSCode-<version>.vsix` for VS Code
* `LensLang-VisualStudio-<version>.vsix` for Visual Studio

Only once all three have been built does the last job push to NuGet and create
the release. A failure anywhere before that leaves nothing behind.

The Rider plugin
----------------

Currently not built by the pipeline - investigating the possible ways to avoid downloading the whole 14GB SDK.

Everything else about it is intact: `build/Set-Version.ps1` still stamps
`gradle.properties`, so a release build is

```
cd editors/rider
gradlew.bat buildPlugin
```

after running `build/Set-Version.ps1 -Version <version>` from the repository
root. The zip lands in `editors/rider/build/distributions/` and can be attached
to the GitHub release by hand. See `editors/rider/README.md` for the build
prerequisites.

Where the version ends up
-------------------------

`LENS` is packed with `-p:Version`. It is the only package that is published:
`Lens.LanguageServer.Core` is marked `IsPackable=false`, because every consumer
of it is in this repository and references the project directly.

The other three manifests keep a version of their own, and
`build/Set-Version.ps1` stamps them before their build. It edits the working
tree only - nothing but the series is committed:

* `editors/vs/Lens.VisualStudio/source.extension.vsixmanifest` and the VSIX
  project file
* `editors/vscode/package.json`
* `editors/rider/gradle.properties`

NuGet trusted publishing
------------------------

There is no NuGet API key in this repository. The workflow asks GitHub for an
OIDC token and exchanges it for a short lived key through `NuGet/login@v1`,
which is the approach nuget.org now recommends over long lived keys.

This works only once a policy has been registered on nuget.org, under the
account's *Trusted Publishing* page. The policy names:

* the package owner - the `user` input of the `NuGet/login` step, currently
  `impworks`, has to be that same nuget.org account
* the repository, `impworks/lens`
* the workflow file, `release.yml`

Publishing to the marketplaces
------------------------------

All three are manual, from the files attached to the GitHub release.

* **Visual Studio** - upload `LensLang-VisualStudio-<version>.vsix` at
  <https://marketplace.visualstudio.com/manage/publishers/impworks>.
* **VS Code** - upload `LensLang-VSCode-<version>.vsix` under the same publisher.
  The extension identifier is `impworks.lens-lang`.
* **Rider** - build the plugin locally as described above, then upload
  `LensLang-Rider-<version>.zip` at <https://plugins.jetbrains.com/>. The first
  upload of a plugin has to be manual in any case; JetBrains only accepts
  automated uploads for a plugin that already exists there.
