#!/bin/bash
# Prepares the restore of a Docker test, and is sourced by the run.sh of each of them.
#
# It sets RESTORE_ONLY_ARGUMENTS to what only the restore takes, and COMMON_ARGUMENTS to what the restore and
# the test both take: the version of the licensing component, and the directory the build writes to.
#
# In the continuous integration build the packages and the version file of that component are directories of
# the repository, which this container reaches unchanged, and this script has nothing to do. On a development
# machine the dependency is resolved to a directory of the host, named by a Windows path. DockerBuild.ps1 mounts
# each of those directories under /mnt/<drive>, so the paths are translated here. Every file that is written is
# written to /tmp, because the files of the repository belong to the host and are what the host builds with.

set -e

# Translates a Windows path into the path this container mounts it at.
translate_path() {
    echo "$1" | sed -E 's#^([A-Za-z]):\\#/mnt/\l\1/#' | sed -E 's#\\#/#g'
}

RESTORE_ONLY_ARGUMENTS=""
COMMON_ARGUMENTS=""

if grep -qE 'value="[A-Za-z]:\\' nuget.config; then
    echo "The dependencies are resolved to directories of the host. Translating the paths that name them."

    sed -E 's#value="([A-Za-z]):\\#value="/mnt/\l\1/#g' nuget.config \
        | sed -E '/\/mnt\//s#\\#/#g' > /tmp/nuget.config

    RESTORE_ONLY_ARGUMENTS="--configfile /tmp/nuget.config"

    # eng/Versions.g.props imports eng/Versions.Debug.g.props by an absolute path of the host, so MSBuild skips
    # it here and the version of the licensing component falls back to the placeholder of
    # eng/AutoUpdatedVersions.props, which names no package that exists. The version is therefore read from the
    # file that the dependency resolution wrote and passed on the command line, where it wins over that
    # placeholder.
    version_file=$( grep -oE '<VersionFile>[^<]+' eng/Versions.Debug.g.props | head -1 | cut -d '>' -f 2 )

    if [ -n "$version_file" ]; then
        version_file=$( translate_path "$version_file" )

        if [ ! -f "$version_file" ]; then
            echo "The version file of the dependency is not mounted at '$version_file'."
            exit 1
        fi

        backstage_version=$( grep -oE '<BackstageVersion>[^<]+' "$version_file" | head -1 | cut -d '>' -f 2 )

        echo "The licensing component is version $backstage_version."
        COMMON_ARGUMENTS="-p:BackstageVersion=$backstage_version"
    fi
fi

# The release archive is not what these tests exercise. Building it starts a nested dotnet publish, which
# restores a second time and with the configuration of the repository rather than the one prepared here.
COMMON_ARGUMENTS="$COMMON_ARGUMENTS -p:ProduceReleaseArchive=false"

# The build writes outside the repository. The repository is mounted, so its bin and obj directories belong to
# the host, and a build of this container would leave the host with an assets file naming paths that exist in
# the container alone.
COMMON_ARGUMENTS="$COMMON_ARGUMENTS -p:ArtifactsPath=/tmp/artifacts"

export RESTORE_ONLY_ARGUMENTS
export COMMON_ARGUMENTS
