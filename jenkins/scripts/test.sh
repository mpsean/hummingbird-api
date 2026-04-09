#!/usr/bin/env sh

set -e

echo 'Running API unit tests.'
dotnet test Hummingbird.API.Tests/Hummingbird.API.Tests.csproj \
  --verbosity normal \
  --logger "junit;LogFilePath=TestResults/junit-results.xml"
