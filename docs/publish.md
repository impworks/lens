How to publish a release
========================

The `Release` workflow (`.github/workflows/release.yml`) builds all four
artifacts, pushes the NuGet packages and leaves a tagged GitHub release with
everything attached. It only ever runs when someone starts it from the Actions
tab - not on a push, and not on a tag.

The marketplace listings are updated by hand, by downloading the files from the
release and uploading them. Building a release is cheap and repeatable; putting
one in front of the public is neither, and a marketplace upload cannot be taken
back.

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

A local build gets `<series>.0`, which is what the two NuGet projects use when
nothing overrides them.

Running a release
-----------------

From the Actions tab, run `Release`. Both inputs are optional:

| Input     | Default           | Meaning                                                     |
|-----------|-------------------|-------------------------------------------------------------|
| `version` | next unused number | Release this exact version instead of the computed one.     |
| `dry_run` | `false`           | Build everything and attach it to the run, but create no tag, no release, and push nothing. |

A run builds, in parallel:

* `LENS` and `LENS.LanguageServer.Core` NuGet packages
* `lens-lang-<version>.vsix` for VS Code
* `Lens.VisualStudio-<version>.vsix` for Visual Studio
* `lens-rider-<version>.zip` for Rider

The Rider leg is the slow one: no local Rider is pointed at on the runner, so
the plugin is compiled against a downloaded SDK of well over a gigabyte.

Only once all four have been built does the last job push to NuGet and create
the release. A failure anywhere before that leaves nothing behind.

Where the version ends up
-------------------------

`LENS` and `LENS.LanguageServer.Core` are packed with `-p:Version`, which also
pins the `LENS` dependency that `LENS.LanguageServer.Core` takes.

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

A first release of a package that does not exist on nuget.org yet needs the
policy to name the package as well, since there is no existing owner to check
against.

Publishing to the marketplaces
------------------------------

All three are manual, from the files attached to the GitHub release.

* **Visual Studio** - upload `Lens.VisualStudio-<version>.vsix` at
  <https://marketplace.visualstudio.com/manage/publishers/impworks>.
* **VS Code** - upload `lens-lang-<version>.vsix` under the same publisher.
  The extension identifier is `impworks.lens-lang`.
* **Rider** - upload `lens-rider-<version>.zip` at
  <https://plugins.jetbrains.com/>. The first upload of a plugin has to be
  manual in any case; JetBrains only accepts automated uploads for a plugin
  that already exists there.
