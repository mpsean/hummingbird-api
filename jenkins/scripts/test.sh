#!/usr/bin/env sh

set -e

echo 'Running API unit tests.'

if command -v dotnet >/dev/null 2>&1; then
    dotnet test Hummingbird.API.Tests/Hummingbird.API.Tests.csproj \
      --verbosity normal \
      --logger "junit;LogFilePath=TestResults/junit-results.xml"
else
    echo 'dotnet not found on agent — running tests inside Docker SDK container.'
    docker run --rm \
      -v "$(pwd):/src" \
      -w /src \
      mcr.microsoft.com/dotnet/sdk:8.0 \
      dotnet test Hummingbird.API.Tests/Hummingbird.API.Tests.csproj \
        --verbosity normal \
        --logger "junit;LogFilePath=TestResults/junit-results.xml"
fi
