#!/usr/bin/env sh

set -e

RESULTS_DIR="Hummingbird.API.Tests/TestResults"
mkdir -p "${RESULTS_DIR}"

if command -v dotnet >/dev/null 2>&1; then
    echo 'Running API unit tests (native dotnet).'
    dotnet test Hummingbird.API.Tests/Hummingbird.API.Tests.csproj \
      --verbosity normal \
      --logger "junit;LogFilePath=TestResults/junit-results.xml"
else
    echo 'dotnet not on agent — running tests via Docker (no volume mounts).'

    TAG="hummingbird-api-test-${BUILD_NUMBER:-local}"

    # Build the test image (restore + build happen inside; results collected at runtime)
    docker build -f jenkins/Dockerfile.test -t "${TAG}" .

    # Create a container without starting it
    CONTAINER_ID="$(docker create "${TAG}")"

    # Run tests — capture exit code without aborting the script
    set +e
    docker start -a "${CONTAINER_ID}"
    TEST_RC=$?
    set -e

    # Copy JUnit XML out regardless of pass/fail so Jenkins can publish it
    docker cp "${CONTAINER_ID}:/TestResults/junit-results.xml" \
              "${RESULTS_DIR}/junit-results.xml" 2>/dev/null || true

    # Clean up
    docker rm "${CONTAINER_ID}" >/dev/null 2>&1 || true
    docker rmi "${TAG}" >/dev/null 2>&1 || true

    exit "${TEST_RC}"
fi
